using System.Collections.Generic;
using UnityEngine;

// Procedural campaign-map generation, extracted from CampaignState so the state model
// stays focused on runtime data + economy. Pure and seed-deterministic: builds the
// territory graph, scales threat from the home hold, classes settlements, and seeds
// roaming parties. CampaignState.CreateDefault delegates here.
public static class CampaignMapGenerator
{
    private static readonly string[] Names =
    {
        "Greyhold", "Redfen", "Northwatch", "Ashmoor", "Stonebridge", "Duskvale", "Irongate", "Westmarch"
    };

    private static readonly string[] PartyNames = { "BANDITS", "RAIDERS", "DESERTERS", "BRIGANDS", "OUTLAWS" };

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

        ConnectGraph(state, homeIndex);
        ScaleThreatFromHome(state, homeIndex);
        AssignSettlements(state);
        // Start alone in open ground just south of the weakest hold.
        state.PartyPosition = new Vector2(positions[homeIndex].x, positions[homeIndex].y - 5f);
        SpawnInitialParties(state, rng);
        return state;
    }

    private static void SpawnInitialParties(CampaignState state, System.Random rng)
    {
        int count = 3 + rng.Next(0, 2);
        for (int i = 0; i < count; i++)
            state.Parties.Add(new EnemyParty
            {
                Position = new Vector2((float)(rng.NextDouble() * 48 - 24), (float)(rng.NextDouble() * 36 - 16)),
                Strength = 2 + rng.Next(0, 3),
                Name = PartyNames[i % PartyNames.Length],
                Arena = (ArenaType)rng.Next(0, 4)
            });
    }

    private static void ScaleThreatFromHome(CampaignState state, int homeIndex)
    {
        Queue<int> frontier = new Queue<int>();
        Dictionary<int, int> distance = new Dictionary<int, int> { [homeIndex] = 0 };
        frontier.Enqueue(homeIndex);
        while (frontier.Count > 0)
        {
            int id = frontier.Dequeue();
            foreach (int adjacent in state.Territories[id].AdjacentIds)
            {
                if (distance.ContainsKey(adjacent))
                    continue;
                distance[adjacent] = distance[id] + 1;
                frontier.Enqueue(adjacent);
            }
        }

        foreach (Territory territory in state.Territories)
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
    private static void AssignSettlements(CampaignState state)
    {
        List<Territory> byStrength = new List<Territory>(state.Territories);
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
    private static void ConnectGraph(CampaignState state, int homeIndex)
    {
        List<Territory> territories = state.Territories;
        HashSet<int> inTree = new HashSet<int> { homeIndex };
        while (inTree.Count < territories.Count)
        {
            float best = float.MaxValue;
            int bestA = -1, bestB = -1;
            foreach (int a in inTree)
                for (int b = 0; b < territories.Count; b++)
                {
                    if (inTree.Contains(b))
                        continue;
                    float d = (territories[a].MapPosition - territories[b].MapPosition).sqrMagnitude;
                    if (d < best) { best = d; bestA = a; bestB = b; }
                }
            if (bestB < 0)
                break; // no connectable node found (degenerate positions); stop rather than crash
            AddEdge(state, bestA, bestB);
            inTree.Add(bestB);
        }

        for (int a = 0; a < territories.Count; a++)
        {
            int nearest = -1;
            float best = float.MaxValue;
            for (int b = 0; b < territories.Count; b++)
            {
                if (b == a || territories[a].AdjacentIds.Contains(b))
                    continue;
                float d = (territories[a].MapPosition - territories[b].MapPosition).sqrMagnitude;
                if (d < best) { best = d; nearest = b; }
            }
            if (nearest >= 0 && best < 64f) // only link genuinely close neighbours
                AddEdge(state, a, nearest);
        }
    }

    private static void AddEdge(CampaignState state, int a, int b)
    {
        if (!state.Territories[a].AdjacentIds.Contains(b))
            state.Territories[a].AdjacentIds.Add(b);
        if (!state.Territories[b].AdjacentIds.Contains(a))
            state.Territories[b].AdjacentIds.Add(a);
    }
}
