using System.Collections.Generic;
using UnityEngine;

// The persistent campaign model: the territory graph plus the player's warband
// roster. Pure data and logic — no MonoBehaviour, so it survives map/battle
// root teardown by living on the GameDirector.
public sealed class CampaignState
{
    public readonly List<Territory> Territories = new();
    public readonly UnitRoster Units = new();
    public int Roster => Units.Total;
    public int Gold;
    public int Seed;
    public bool CampaignOver; // set when the player is defeated
    public string LastReport = "Choose a border territory or strengthen your warband.";
    public WeaponType PlayerWeapon = WeaponType.SwordAndShield;
    public WeaponType TrainingEnemyWeapon = WeaponType.SwordAndShield;

    public const int WarbandCap = 12;

    private static readonly string[] Names =
    {
        "Greyhold", "Redfen", "Northwatch", "Ashmoor", "Stonebridge", "Duskvale", "Irongate", "Westmarch"
    };

    public static CampaignState CreateDefault(int seed)
    {
        System.Random rng = new System.Random(seed);
        CampaignState state = new CampaignState { Seed = seed, Gold = 150 };
        state.Units.Militia = 3;

        const int count = 8;
        List<Vector2> positions = new List<Vector2>();
        int guard = 0;
        while (positions.Count < count && guard++ < 2000)
        {
            Vector2 p = new Vector2((float)(rng.NextDouble() * 24 - 12), (float)(rng.NextDouble() * 18 - 8));
            bool spaced = true;
            foreach (Vector2 q in positions)
                if ((p - q).sqrMagnitude < 36f) { spaced = false; break; } // keep nodes >= 6 apart
            if (spaced)
                positions.Add(p);
        }

        int homeIndex = 0;
        for (int i = 1; i < positions.Count; i++)
            if (positions[i].y < positions[homeIndex].y)
                homeIndex = i;

        for (int i = 0; i < positions.Count; i++)
            state.Territories.Add(new Territory
            {
                Id = i,
                Name = Names[i % Names.Length],
                MapPosition = positions[i],
                Owner = i == homeIndex ? TerritoryOwner.Player : TerritoryOwner.Enemy,
                Garrison = i == homeIndex ? 2 : 2 + rng.Next(0, 5),
                Arena = (ArenaType)(i % 4),
                RewardGold = i == homeIndex ? 0 : 55 + rng.Next(0, 6) * 10,
                Income = i == homeIndex ? 20 : 8 + rng.Next(0, 4) * 4,
                Threat = i == homeIndex ? 1 : 1 + rng.Next(0, 3)
            });

        state.ConnectGraph(homeIndex);
        state.ScaleThreatFromHome(homeIndex);
        return state;
    }

    private void ScaleThreatFromHome(int homeIndex)
    {
        Queue<int> frontier = new Queue<int>();
        Dictionary<int, int> distance = new Dictionary<int, int> { [homeIndex] = 0 };
        frontier.Enqueue(homeIndex);
        while (frontier.Count > 0)
        {
            int id = frontier.Dequeue();
            foreach (int adjacent in Territories[id].AdjacentIds)
            {
                if (distance.ContainsKey(adjacent))
                    continue;
                distance[adjacent] = distance[id] + 1;
                frontier.Enqueue(adjacent);
            }
        }

        foreach (Territory territory in Territories)
        {
            int depth = distance.TryGetValue(territory.Id, out int value) ? value : 1;
            territory.Threat = Mathf.Clamp(territory.Threat + depth - 1, 1, 5);
            if (territory.Owner != TerritoryOwner.Player)
            {
                territory.Garrison = Mathf.Clamp(territory.Garrison + depth / 2, 2, 10);
                territory.DifficultyScale = 1f + (territory.Threat - 1) * 0.08f;
                territory.RewardGold += depth * 12;
            }
        }
    }

    // Prim-style minimum spanning tree from the home node guarantees the whole
    // map is reachable; a few nearest-neighbour edges add alternate routes.
    private void ConnectGraph(int homeIndex)
    {
        HashSet<int> inTree = new HashSet<int> { homeIndex };
        while (inTree.Count < Territories.Count)
        {
            float best = float.MaxValue;
            int bestA = -1, bestB = -1;
            foreach (int a in inTree)
                for (int b = 0; b < Territories.Count; b++)
                {
                    if (inTree.Contains(b))
                        continue;
                    float d = (Territories[a].MapPosition - Territories[b].MapPosition).sqrMagnitude;
                    if (d < best) { best = d; bestA = a; bestB = b; }
                }
            AddEdge(bestA, bestB);
            inTree.Add(bestB);
        }

        for (int a = 0; a < Territories.Count; a++)
        {
            int nearest = -1;
            float best = float.MaxValue;
            for (int b = 0; b < Territories.Count; b++)
            {
                if (b == a || Territories[a].AdjacentIds.Contains(b))
                    continue;
                float d = (Territories[a].MapPosition - Territories[b].MapPosition).sqrMagnitude;
                if (d < best) { best = d; nearest = b; }
            }
            if (nearest >= 0 && best < 64f) // only link genuinely close neighbours
                AddEdge(a, nearest);
        }
    }

    private void AddEdge(int a, int b)
    {
        if (!Territories[a].AdjacentIds.Contains(b))
            Territories[a].AdjacentIds.Add(b);
        if (!Territories[b].AdjacentIds.Contains(a))
            Territories[b].AdjacentIds.Add(a);
    }

    public Territory GetById(int id) => Territories[id];

    public bool AdjacentToPlayer(Territory t)
    {
        foreach (int id in t.AdjacentIds)
            if (Territories[id].Owner == TerritoryOwner.Player)
                return true;
        return false;
    }

    public bool IsAttackable(Territory t) => t.Owner != TerritoryOwner.Player && AdjacentToPlayer(t);

    public IEnumerable<Territory> AttackableTargets()
    {
        foreach (Territory t in Territories)
            if (IsAttackable(t))
                yield return t;
    }

    public bool AllConquered()
    {
        foreach (Territory t in Territories)
            if (t.Owner != TerritoryOwner.Player)
                return false;
        return true;
    }

    public int PlayerTerritoryCount()
    {
        int count = 0;
        foreach (Territory t in Territories)
            if (t.Owner == TerritoryOwner.Player)
                count++;
        return count;
    }

    public int IncomePerVictory()
    {
        int total = 0;
        foreach (Territory territory in Territories)
            if (territory.Owner == TerritoryOwner.Player)
                total += territory.Income;
        return total;
    }

    public BattleSetup BuildSetupFor(Territory t) => new BattleSetup
    {
        AllyCount = Mathf.Clamp(Roster, 0, 12),
        AllyMilitia = Units.Militia,
        AllyVeterans = Units.Veterans,
        AllyGuards = Units.Guards,
        EnemyCount = Mathf.Clamp(t.Garrison, 1, 12),
        EnemyVeterans = Mathf.Clamp((t.Threat - 1) / 2, 0, t.Garrison),
        EnemyGuards = t.Threat >= 4 ? 1 : 0,
        EnemyComposition = BuildEnemyComposition(t),
        EnemyHealthScale = t.DifficultyScale,
        TargetName = t.Name.ToUpperInvariant(),
        Arena = t.Arena,
        PlayerWeapon = PlayerWeapon
    };

    // Builds the garrison's archetype mix: soldiers at low threat, joined by
    // shieldbearers, then archers/berserkers and an arena-flavored bias, with a
    // captain anchoring the strongest holds. Tiers rise with threat.
    private static List<UnitSpec> BuildEnemyComposition(Territory t)
    {
        int count = Mathf.Clamp(t.Garrison, 1, 12);
        int threat = t.Threat;
        List<UnitSpec> specs = new();

        if (threat >= 4 && count > 1)
            specs.Add(Enemy(Archetype.Captain, UnitType.Veteran));

        List<Archetype> pool = new() { Archetype.Soldier };
        if (threat >= 2)
            pool.Add(Archetype.Shieldbearer);
        if (threat >= 3)
        {
            pool.Add(Archetype.Archer);
            pool.Add(Archetype.Berserker);
            pool.Add(ArenaBias(t.Arena));
        }

        int i = 0;
        while (specs.Count < count)
        {
            Archetype archetype = pool[i % pool.Count];
            specs.Add(Enemy(archetype, TierFor(archetype, threat)));
            i++;
        }
        return specs;
    }

    private static Archetype ArenaBias(ArenaType arena) => arena switch
    {
        ArenaType.Highlands => Archetype.Berserker,
        ArenaType.Marsh => Archetype.Archer,
        ArenaType.Forest => Archetype.Archer,
        _ => Archetype.Shieldbearer
    };

    private static UnitType TierFor(Archetype archetype, int threat)
    {
        if (archetype == Archetype.Captain || threat >= 4)
            return UnitType.Veteran;
        if (threat >= 3 && (archetype == Archetype.Shieldbearer || archetype == Archetype.Berserker))
            return UnitType.Veteran;
        return UnitType.Militia;
    }

    private static UnitSpec Enemy(Archetype archetype, UnitType tier)
        => new UnitSpec(tier, archetype, ArchetypeCatalog.Weapon(archetype));

    public BattleSetup BuildTrainingSetup() => new BattleSetup
    {
        AllyCount = 0,
        EnemyCount = 1,
        EnemyHealthScale = 1f,
        TargetName = "TRAINING ARENA",
        Arena = ArenaType.Courtyard,
        PlayerWeapon = PlayerWeapon,
        TrainingEnemyWeapon = TrainingEnemyWeapon,
        IsTraining = true
    };

    public bool CanRecruit(UnitType type) => Roster < WarbandCap && Gold >= UnitCatalog.Cost(type);

    public bool Recruit(UnitType type)
    {
        int cost = UnitCatalog.Cost(type);
        if (!CanRecruit(type))
            return false;
        Gold -= cost;
        Units.Add(type);
        LastReport = $"{UnitCatalog.Label(type)} recruited for {cost} gold.";
        return true;
    }

    public void ApplyVictory(Territory t, BattleResult result)
    {
        int reward = t.RewardGold;
        t.Owner = TerritoryOwner.Player;
        Units.Militia = Mathf.Max(0, result.MilitiaSurvived);
        Units.Veterans = Mathf.Max(0, result.VeteransSurvived);
        Units.Guards = Mathf.Max(0, result.GuardsSurvived);
        int income = IncomePerVictory();
        Gold += reward + income;
        LastReport = $"{t.Name} captured. Earned {reward} conquest gold and {income} income.";
    }

    public void ApplyDefeat()
    {
        CampaignOver = true;
        LastReport = "The captain fell. The campaign is lost.";
    }
}
