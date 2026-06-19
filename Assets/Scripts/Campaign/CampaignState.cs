using System.Collections.Generic;
using UnityEngine;

// The persistent campaign model: the territory graph plus the player's warband
// roster. Pure data and logic — no MonoBehaviour, so it survives map/battle
// root teardown by living on the GameDirector.
public sealed class CampaignState
{
    public readonly List<Territory> Territories = new();
    public readonly List<EnemyParty> Parties = new();
    public readonly UnitRoster Units = new();
    public int Roster => Units.Total;
    public int Gold;
    public int Seed;
    public Vector2 PartyPosition;     // the player warband's overworld location
    public int Day = 1;               // campaign clock, advanced by travel
    public bool CampaignOver;         // set when the player is defeated
    public string LastReport = "March your warband across the land. Hunt bandits, raid holds, grow your host.";
    public WeaponType PlayerWeapon = WeaponType.SwordAndShield;
    public WeaponType TrainingEnemyWeapon = WeaponType.SwordAndShield;

    public int Renown;                  // earned from victories and held land; raises the cap
    public int Morale = StartingMorale; // 0..100 party morale; low morale breeds desertion

    // Leadership: the warband size cap grows with Renown, from BaseLeadership up to
    // MaxLeadership. MaxLeadership matches the battlefield deployment ceiling, so a
    // full warband always fields together.
    public const int BaseLeadership = 6;
    public const int MaxLeadership = BattleSetup.MaxDeployed;
    public const int RenownPerCapStep = 15;

    // Economy / morale tuning.
    public const int StartingMorale = 60;
    public const int MoraleTargetBase = 60;
    public const int MoraleDriftPerDay = 5;
    public const int MoraleUnpaidPenalty = 15;
    public const int MoraleUnpaidTargetDrop = 40;
    public const int MoraleOvercapPenalty = 5;
    public const int MoraleVictoryBonus = 10;
    public const int MoraleFieldWinBonus = 5;
    public const int DesertionMoraleFloor = 25;
    public const int DesertionMoraleRebound = 10;
    public const int RenownPerHoldPerDay = 1;
    public const int XpPerEnemyDefeated = 10;

    public int LeadershipCap => Mathf.Clamp(BaseLeadership + Renown / RenownPerCapStep, BaseLeadership, MaxLeadership);

    private static readonly string[] Names =
    {
        "Greyhold", "Redfen", "Northwatch", "Ashmoor", "Stonebridge", "Duskvale", "Irongate", "Westmarch"
    };

    public static CampaignState CreateDefault(int seed)
    {
        System.Random rng = new System.Random(seed);
        CampaignState state = new CampaignState { Seed = seed, Gold = 150 };
        state.Units.Add(UnitType.Militia, Archetype.Soldier, 3);

        const int count = 8;
        List<Vector2> positions = new List<Vector2>();
        int guard = 0;
        while (positions.Count < count && guard++ < 2000)
        {
            Vector2 p = new Vector2((float)(rng.NextDouble() * 48 - 24), (float)(rng.NextDouble() * 36 - 16));
            bool spaced = true;
            foreach (Vector2 q in positions)
                if ((p - q).sqrMagnitude < 100f) { spaced = false; break; } // keep nodes >= 10 apart
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
                Owner = TerritoryOwner.Enemy,         // the player owns nothing at the start
                Garrison = i == homeIndex ? 2 : 2 + rng.Next(0, 5),
                Arena = (ArenaType)(i % 4),
                RewardGold = 55 + rng.Next(0, 6) * 10,
                Income = 8 + rng.Next(0, 4) * 4,
                Threat = i == homeIndex ? 1 : 1 + rng.Next(0, 3)   // southernmost hold is the weakest
            });

        state.ConnectGraph(homeIndex);
        state.ScaleThreatFromHome(homeIndex);
        state.AssignSettlements();
        // Start alone in open ground just south of the weakest hold.
        state.PartyPosition = new Vector2(positions[homeIndex].x, positions[homeIndex].y - 5f);
        state.SpawnInitialParties(rng);
        return state;
    }

    private static readonly string[] PartyNames = { "BANDITS", "RAIDERS", "DESERTERS", "BRIGANDS", "OUTLAWS" };

    private void SpawnInitialParties(System.Random rng)
    {
        int count = 3 + rng.Next(0, 2);
        for (int i = 0; i < count; i++)
            Parties.Add(new EnemyParty
            {
                Position = new Vector2((float)(rng.NextDouble() * 48 - 24), (float)(rng.NextDouble() * 36 - 16)),
                Strength = 2 + rng.Next(0, 3),
                Name = PartyNames[i % PartyNames.Length],
                Arena = (ArenaType)rng.Next(0, 4)
            });
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

    // Classes each territory as a Village/Town/Castle by relative strength: the two
    // strongest holds become castles (all tiers), the next three towns (up to
    // veterans), and the rest — including the weak home — villages (militia only).
    // Recruit pools start full. Deterministic for a given map.
    private void AssignSettlements()
    {
        List<Territory> byStrength = new List<Territory>(Territories);
        byStrength.Sort((a, b) =>
        {
            int byThreat = b.Threat.CompareTo(a.Threat);
            if (byThreat != 0)
                return byThreat;
            int byGarrison = b.Garrison.CompareTo(a.Garrison);
            return byGarrison != 0 ? byGarrison : a.Id.CompareTo(b.Id);
        });
        for (int i = 0; i < byStrength.Count; i++)
        {
            SettlementType type = i < 2 ? SettlementType.Castle
                : i < 5 ? SettlementType.Town
                : SettlementType.Village;
            byStrength[i].Settlement = type;
            byStrength[i].Recruits = SettlementCatalog.MaxRecruits(type);
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

    public int PlayerTerritoryCount()
    {
        int count = 0;
        foreach (Territory t in Territories)
            if (t.Owner == TerritoryOwner.Player)
                count++;
        return count;
    }

    // Gold collected each campaign day from every owned hold.
    public int DailyIncome()
    {
        int total = 0;
        foreach (Territory territory in Territories)
            if (territory.Owner == TerritoryOwner.Player)
                total += territory.Income;
        return total;
    }

    // Gold owed each campaign day in troop wages, summed over the warband.
    public int DailyWage()
    {
        int wage = 0;
        foreach (RosterEntry entry in Units.Entries)
            wage += entry.Count * UnitCatalog.Upkeep(entry.Tier);
        return wage;
    }

    // Advances the warband economy by one campaign day: collect owned-land income,
    // pay troop wages (morale suffers and the purse empties if the coffers run dry),
    // earn renown from held land, drift morale toward its target, refill settlement
    // recruit pools, and let an unhappy soldier desert. Called once per day elapsed
    // by OverworldSimulation.
    public void ApplyDayTick()
    {
        Gold += DailyIncome();

        int wage = DailyWage();
        bool paid = Gold >= wage;
        if (paid)
            Gold -= wage;
        else
        {
            Gold = 0;
            Morale = Mathf.Clamp(Morale - MoraleUnpaidPenalty, 0, 100);
        }

        Renown += RenownPerHoldPerDay * PlayerTerritoryCount();
        Morale = Mathf.Clamp(StepToward(Morale, MoraleTarget(paid), MoraleDriftPerDay), 0, 100);
        RegenerateRecruits();

        if (Morale < DesertionMoraleFloor)
            Desert();
    }

    private int MoraleTarget(bool wagesPaid)
    {
        int target = MoraleTargetBase;
        if (!wagesPaid)
            target -= MoraleUnpaidTargetDrop;
        int over = Roster - LeadershipCap;
        if (over > 0)
            target -= over * MoraleOvercapPenalty;
        return Mathf.Clamp(target, 0, 100);
    }

    private static int StepToward(int current, int target, int step)
    {
        if (current < target)
            return Mathf.Min(current + step, target);
        if (current > target)
            return Mathf.Max(current - step, target);
        return current;
    }

    private void RegenerateRecruits()
    {
        foreach (Territory t in Territories)
        {
            int max = SettlementCatalog.MaxRecruits(t.Settlement);
            if (t.Recruits < max)
                t.Recruits++;
        }
    }

    // Morale has cratered: the least-committed fighter (lowest tier) slips away in
    // the night. Being rid of the malcontent steadies the rest a little.
    private void Desert()
    {
        RosterEntry victim = null;
        foreach (RosterEntry entry in Units.Entries)
            if (entry.Count > 0 && (victim == null || entry.Tier < victim.Tier))
                victim = entry;
        if (victim == null)
            return;
        victim.Count--;
        Morale = Mathf.Clamp(Morale + DesertionMoraleRebound, 0, 100);
        LastReport = $"Morale is low. A {UnitCatalog.Label(victim.Tier)} deserted in the night.";
    }

    // Maps a campaign day to a 0..1 time of day. The golden-ratio step spreads
    // successive days across the cycle while staying deterministic (a given day
    // always lights the same), so a retried battle looks identical.
    public static float TimeOfDayForDay(int day) => Mathf.Repeat(0.18f + (day - 1) * 0.61803f, 1f);

    public BattleSetup BuildSetupFor(Territory t) => new BattleSetup
    {
        AllyCount = Mathf.Clamp(Roster, 0, BattleSetup.MaxDeployed),
        AllyMilitia = Units.Militia,
        AllyVeterans = Units.Veterans,
        AllyGuards = Units.Guards,
        AllyComposition = BuildAllyComposition(),
        EnemyCount = Mathf.Clamp(t.Garrison, 1, 12),
        EnemyVeterans = Mathf.Clamp((t.Threat - 1) / 2, 0, t.Garrison),
        EnemyGuards = t.Threat >= 4 ? 1 : 0,
        EnemyComposition = BuildEnemyComposition(t),
        EnemyHealthScale = t.DifficultyScale,
        TargetName = t.Name.ToUpperInvariant(),
        Arena = t.Arena,
        PlayerWeapon = PlayerWeapon,
        Kind = BattleKind.SettlementAssault,
        TimeOfDay = TimeOfDayForDay(Day)
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

    // Expands the warband roster into one UnitSpec per fighter, each carrying its
    // archetype's weapon, so allies fight with their chosen personalities.
    private List<UnitSpec> BuildAllyComposition()
    {
        List<UnitSpec> specs = new();
        foreach (RosterEntry entry in Units.Entries)
            for (int i = 0; i < entry.Count && specs.Count < BattleSetup.MaxDeployed; i++)
                specs.Add(new UnitSpec(entry.Tier, entry.Archetype, ArchetypeCatalog.Weapon(entry.Archetype)));
        return specs;
    }

    public BattleSetup BuildTrainingSetup() => new BattleSetup
    {
        AllyCount = 0,
        EnemyCount = 1,
        EnemyHealthScale = 1f,
        TargetName = "TRAINING ARENA",
        Arena = ArenaType.Courtyard,
        PlayerWeapon = PlayerWeapon,
        TrainingEnemyWeapon = TrainingEnemyWeapon,
        IsTraining = true,
        Kind = BattleKind.Training,
        TimeOfDay = 0.5f
    };

    // Recruitment draws volunteers from a settlement in range. The settlement's
    // type sets the highest tier on offer and a limited pool that refills over days;
    // cost is set by the tier, and the archetype is a free choice of behavior. The
    // warband size is gated by Leadership. The range check itself lives in the sim/UI.
    public bool CanRecruit(UnitType tier, Archetype archetype, Territory settlement)
        => settlement != null
           && SettlementCatalog.Allows(settlement.Settlement, tier)
           && settlement.Recruits > 0
           && Roster < LeadershipCap
           && Gold >= UnitCatalog.Cost(tier);

    public bool Recruit(UnitType tier, Archetype archetype, Territory settlement)
    {
        if (!CanRecruit(tier, archetype, settlement))
            return false;
        int cost = UnitCatalog.Cost(tier);
        Gold -= cost;
        settlement.Recruits--;
        Units.Add(tier, archetype, 1);
        LastReport = $"{ArchetypeCatalog.Label(archetype)} {UnitCatalog.Label(tier)} recruited at {settlement.Name} for {cost} gold.";
        return true;
    }

    // A fighter that has banked enough battle experience can be promoted to the next
    // tier (keeping its archetype) for gold. Experience is pooled per stack.
    public bool CanUpgrade(UnitType tier, Archetype archetype)
        => UnitCatalog.CanUpgrade(tier)
           && Units.Count(tier, archetype) > 0
           && Units.Xp(tier, archetype) >= UnitCatalog.UpgradeXp(tier)
           && Gold >= UnitCatalog.UpgradeCost(tier);

    public bool TryUpgrade(UnitType tier, Archetype archetype)
    {
        if (!CanUpgrade(tier, archetype))
            return false;
        int cost = UnitCatalog.UpgradeCost(tier);
        if (!Units.Upgrade(tier, archetype))
            return false;
        Gold -= cost;
        LastReport = $"{ArchetypeCatalog.Label(archetype)} promoted to {UnitCatalog.Label(UnitCatalog.NextTier(tier))} for {cost} gold.";
        return true;
    }

    // A field battle against a roaming party: bandit-tier enemies scaled by the
    // party's strength, fought wherever the party was caught.
    public BattleSetup BuildPartySetup(EnemyParty party) => new BattleSetup
    {
        AllyCount = Mathf.Clamp(Roster, 0, BattleSetup.MaxDeployed),
        AllyComposition = BuildAllyComposition(),
        EnemyCount = Mathf.Clamp(party.Strength, 1, 12),
        EnemyComposition = BuildBanditComposition(party),
        EnemyHealthScale = 1f,
        TargetName = party.Name,
        Arena = party.Arena,
        PlayerWeapon = PlayerWeapon,
        Kind = BattleKind.BanditField,
        TimeOfDay = TimeOfDayForDay(Day)
    };

    private static List<UnitSpec> BuildBanditComposition(EnemyParty party)
    {
        int count = Mathf.Clamp(party.Strength, 1, 12);
        Archetype[] pool = { Archetype.Soldier, Archetype.Berserker, Archetype.Archer };
        List<UnitSpec> specs = new();
        for (int i = 0; i < count; i++)
        {
            Archetype archetype = pool[i % pool.Length];
            specs.Add(new UnitSpec(UnitType.Militia, archetype, ArchetypeCatalog.Weapon(archetype)));
        }
        return specs;
    }

    public void ApplyVictory(Territory t, BattleResult result)
    {
        int reward = t.RewardGold;
        int renown = 20 + t.Threat * 5;
        int enemies = Mathf.Clamp(t.Garrison, 1, 12);
        t.Owner = TerritoryOwner.Player;
        ApplySurvivors(result, enemies);
        Gold += reward;
        Renown += renown;
        Morale = Mathf.Clamp(Morale + MoraleVictoryBonus, 0, 100);
        LastReport = $"{t.Name} captured for {reward} conquest gold (+{renown} renown).";
    }

    public void ResolveFieldBattle(EnemyParty party, BattleResult result)
    {
        int enemies = Mathf.Clamp(party.Strength, 1, 12);
        int renown = 5 + party.Strength * 2;
        Parties.Remove(party);
        int loot = 25 + party.Strength * 15;
        Gold += loot;
        ApplySurvivors(result, enemies);
        Renown += renown;
        Morale = Mathf.Clamp(Morale + MoraleFieldWinBonus, 0, 100);
        LastReport = $"Defeated {party.Name}. Looted {loot} gold (+{renown} renown).";
    }

    // Rebuilds the warband from a battle's allied survivors, preserving tier and
    // archetype. Pooled XP belongs to the (tier x archetype) stack rather than to
    // individuals, so it is carried across the rebuild and dropped only for stacks
    // that were wiped out. Falls back to tier-only counts for smoke/test results.
    private void ApplySurvivors(BattleResult result, int enemiesDefeated)
    {
        Dictionary<(UnitType, Archetype), int> bankedXp = new();
        foreach (RosterEntry entry in Units.Entries)
            bankedXp[(entry.Tier, entry.Archetype)] = entry.Xp;

        Units.Clear();
        if (result.SurvivingUnits != null && result.SurvivingUnits.Count > 0)
            foreach (RosterEntry entry in result.SurvivingUnits)
                Units.Add(entry.Tier, entry.Archetype, Mathf.Max(0, entry.Count));
        else
        {
            Units.Add(UnitType.Militia, Archetype.Soldier, Mathf.Max(0, result.MilitiaSurvived));
            Units.Add(UnitType.Veteran, Archetype.Soldier, Mathf.Max(0, result.VeteransSurvived));
            Units.Add(UnitType.Guard, Archetype.Soldier, Mathf.Max(0, result.GuardsSurvived));
        }

        foreach (RosterEntry entry in Units.Entries)
            if (bankedXp.TryGetValue((entry.Tier, entry.Archetype), out int xp))
                entry.Xp = xp;

        AwardBattleXp(enemiesDefeated);
    }

    // Spreads the battle's experience across the surviving warband, pooled onto each
    // stack so the units that fought earn toward a promotion.
    private void AwardBattleXp(int enemiesDefeated)
    {
        int survivors = Roster;
        if (survivors <= 0 || enemiesDefeated <= 0)
            return;
        int per = Mathf.Max(1, enemiesDefeated * XpPerEnemyDefeated / survivors);
        foreach (RosterEntry entry in Units.Entries)
            if (entry.Count > 0)
                entry.Xp += per * entry.Count;
    }

    public void ApplyDefeat()
    {
        CampaignOver = true;
        LastReport = "The captain fell. The campaign is lost.";
    }
}
