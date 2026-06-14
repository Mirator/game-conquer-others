using System.Collections.Generic;
using UnityEngine;

// The persistent campaign model: the territory graph plus the player's warband
// roster. Pure data and logic — no MonoBehaviour, so it survives map/battle
// root teardown by living on the GameDirector.
public sealed class CampaignState
{
    public readonly List<Territory> Territories = new();
    public int Roster;        // available allied soldiers, carried between battles
    public int Seed;
    public bool CampaignOver; // set when the player is defeated

    private static readonly string[] Names =
    {
        "Greyhold", "Redfen", "Northwatch", "Ashmoor", "Stonebridge", "Duskvale", "Irongate", "Westmarch"
    };

    public static CampaignState CreateDefault(int seed)
    {
        System.Random rng = new System.Random(seed);
        CampaignState state = new CampaignState { Seed = seed, Roster = 3 };

        const int count = 6;
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
                Garrison = 2 + rng.Next(0, 3) // 2-4 defenders
            });

        state.ConnectGraph(homeIndex);
        return state;
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

    public BattleSetup BuildSetupFor(Territory t) => new BattleSetup
    {
        AllyCount = Mathf.Clamp(Roster, 0, 12),
        EnemyCount = Mathf.Clamp(t.Garrison, 1, 12),
        EnemyHealthScale = t.DifficultyScale,
        TargetName = t.Name.ToUpperInvariant()
    };

    public void ApplyVictory(Territory t, int alliesSurvived)
    {
        t.Owner = TerritoryOwner.Player;
        Roster = Mathf.Max(0, alliesSurvived); // survivors persist, deaths are permanent
    }

    public void ApplyDefeat() => CampaignOver = true;
}
