using System;
using System.Collections.Generic;
using UnityEngine;

// Owns AI target distribution, attack permissions, engagement slots, and
// coordination telemetry. BattleManager remains the public gameplay facade.
public sealed class BattleTactics
{
    private readonly List<BattleFighter> fighters;
    private readonly Func<PlayerFighter> player;
    private readonly Dictionary<BattleFighter, List<AIFighter>> attackPermissions = new();
    private readonly Dictionary<BattleFighter, Vector3> separationByFighter = new();
    private readonly List<BattleFighter> emptyPermissionTargets = new();
    private readonly Dictionary<AIFighter, int> engagementSlots = new();
    private readonly Dictionary<BattleFighter, List<AIFighter>> engagementGroups = new();
    // Neighbour acceleration so per-frame proximity work scales past O(n^2). The
    // cell size equals the separation cutoff (sqrt(6.25) == 2.5), so a fighter's
    // 3x3 cell block contains every neighbour that can exert a separation force.
    private readonly SpatialHashGrid grid = new(2.5f);
    private readonly List<BattleFighter> neighborScratch = new();
    private readonly Dictionary<BattleFighter, int> assignedCountByTarget = new();
    private int gridFrame = -1;
    private int assignedCountFrame = -1;
    private int engagementSlotFrame = -1;
    private int separationFrame = -1;
    private int maxPlayerAttackers;
    private int maxTargetAttackers;
    private int maxClosePairs;
    private float minimumFighterDistance = float.MaxValue;

    public string DebugSummary => $"MaxPlayerAttackers={maxPlayerAttackers}, MaxTargetAttackers={maxTargetAttackers}, MinFighterDistance={minimumFighterDistance:0.00}, MaxClosePairs={maxClosePairs}";

    public BattleTactics(List<BattleFighter> battleFighters, Func<PlayerFighter> currentPlayer)
    {
        fighters = battleFighters;
        player = currentPlayer;
    }

    public BattleFighter SelectTarget(AIFighter seeker, BattleFighter current)
    {
        PlayerFighter captain = player();
        if (seeker.Team == Team.Enemies && captain != null && captain.IsAlive && AssignedTo(captain, seeker) == 0)
            return captain;
        BattleFighter best = current != null && current.IsAlive ? current : null;
        float bestScore = best != null ? ScoreTarget(seeker, best, true) : float.MaxValue;
        foreach (BattleFighter fighter in fighters)
        {
            if (!fighter.IsAlive || fighter.Team == seeker.Team)
                continue;
            float score = ScoreTarget(seeker, fighter, fighter == current);
            if (score < bestScore)
            {
                bestScore = score;
                best = fighter;
            }
        }
        return best;
    }

    public bool TryClaimAttackPermission(AIFighter attacker, BattleFighter target)
    {
        CleanupAttackPermissions();
        if (target == null || !target.IsAlive)
            return false;
        if (!attackPermissions.TryGetValue(target, out List<AIFighter> attackers))
        {
            attackers = new List<AIFighter>();
            attackPermissions[target] = attackers;
        }
        if (attackers.Contains(attacker))
            return true;
        int limit = target.IsPlayer ? CombatBalance.MaxPlayerAttackers : CombatBalance.MaxTargetAttackers;
        if (attackers.Count >= limit)
            return false;
        attackers.Add(attacker);
        return true;
    }

    public void ReleaseAttackPermission(AIFighter attacker)
    {
        foreach (List<AIFighter> attackers in attackPermissions.Values)
            attackers.Remove(attacker);
    }

    // Eagerly drop a fighter from tactics state when it dies or retreats: release any
    // attack slot it holds and remove its own target slot list, so survivors are not
    // stalled waiting on a permission held by someone who has left the battle.
    public void OnFighterRemoved(BattleFighter fighter)
    {
        if (fighter is AIFighter ai)
            ReleaseAttackPermission(ai);
        attackPermissions.Remove(fighter);
    }

    public Vector3 GetEngagementPosition(AIFighter seeker, BattleFighter target, bool activeAttacker, float preferredRange)
    {
        Vector3 radial = seeker.transform.position - target.transform.position;
        radial.y = 0f;
        if (radial.sqrMagnitude < 0.01f)
            radial = target.transform.forward;
        radial.Normalize();
        if (activeAttacker)
            return target.transform.position + radial * preferredRange;

        if (engagementSlotFrame != Time.frameCount)
            RebuildEngagementSlots();
        int index = engagementSlots.TryGetValue(seeker, out int slot) ? slot : 0;
        float[] supportAngles = CombatBalance.SupportAngles;
        float angle = supportAngles[index % supportAngles.Length]
            + index / supportAngles.Length * CombatBalance.SupportSlotSpreadDegrees;
        Vector3 slotDirection = Quaternion.AngleAxis(angle, Vector3.up) * target.transform.forward;
        return target.transform.position + slotDirection.normalized
            * Mathf.Lerp(CombatBalance.SupportEngagementNear, CombatBalance.SupportEngagementFar, index % 3 / 2f);
    }

    public Vector3 GetSeparation(BattleFighter seeker)
    {
        if (separationFrame != Time.frameCount)
            RebuildSeparationCache();
        return separationByFighter.TryGetValue(seeker, out Vector3 result) ? result : Vector3.zero;
    }

    // Fills `results` with the fighters in the spatial-grid cell block around `position`
    // (the once-per-frame grid is shared with separation/telemetry). Lets callers do
    // bounded proximity tests (e.g. melee swept-strike) without scanning the roster.
    public void QueryNeighbors(Vector3 position, List<BattleFighter> results)
    {
        EnsureGrid();
        grid.QueryNeighbors(position, results);
    }

    // Each fighter accumulates a push away from neighbours within 2.5m. Querying
    // the spatial grid per fighter is O(n*k); the symmetric +/-force split the old
    // pairwise loop used is unnecessary here because every fighter computes its own
    // push from its own neighbours, yielding the identical per-fighter result.
    private void RebuildSeparationCache()
    {
        separationFrame = Time.frameCount;
        separationByFighter.Clear();
        EnsureGrid();
        // The 2.5m onset radius (and its 6.25 squared cull) is fixed to the spatial-hash
        // cell size so the 3x3 neighbour block covers every fighter that can push; only
        // the force shaping is tuning, hoisted here so the inner loop reads no properties.
        float falloff = CombatBalance.SeparationFalloff;
        float maxForce = CombatBalance.SeparationMaxForce;
        for (int i = 0; i < fighters.Count; i++)
        {
            BattleFighter self = fighters[i];
            if (!self.IsAlive)
                continue;
            grid.QueryNeighbors(self.transform.position, neighborScratch);
            Vector3 force = Vector3.zero;
            for (int n = 0; n < neighborScratch.Count; n++)
            {
                BattleFighter other = neighborScratch[n];
                if (other == self || !other.IsAlive)
                    continue;
                Vector3 offset = self.transform.position - other.transform.position;
                offset.y = 0f;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared >= 6.25f || distanceSquared <= 0.001f)
                    continue;
                float distance = Mathf.Sqrt(distanceSquared);
                force += offset / distance * Mathf.Clamp01((2.5f - distance) / falloff);
            }
            separationByFighter[self] = Vector3.ClampMagnitude(force, maxForce);
        }
    }

    private void EnsureGrid()
    {
        if (gridFrame == Time.frameCount)
            return;
        gridFrame = Time.frameCount;
        grid.Rebuild(fighters);
    }

    public void UpdateTelemetry()
    {
        CleanupAttackPermissions();
        EnsureGrid();
        // Grid-based proximity scan: minimumFighterDistance and the close-pair count
        // are clumping diagnostics, so only neighbours within a cell matter (combat is
        // melee, so the meaningful minimum always falls inside the grid range). Each
        // unordered close pair is seen from both fighters, hence the halving.
        int closeNeighbors = 0;
        for (int i = 0; i < fighters.Count; i++)
        {
            BattleFighter self = fighters[i];
            if (!self.IsAlive)
                continue;
            grid.QueryNeighbors(self.transform.position, neighborScratch);
            for (int n = 0; n < neighborScratch.Count; n++)
            {
                BattleFighter other = neighborScratch[n];
                if (other == self || !other.IsAlive)
                    continue;
                float distance = Vector3.Distance(self.transform.position, other.transform.position);
                minimumFighterDistance = Mathf.Min(minimumFighterDistance, distance);
                if (distance < 1.05f)
                    closeNeighbors++;
            }
        }
        maxClosePairs = Mathf.Max(maxClosePairs, closeNeighbors / 2);
        foreach (KeyValuePair<BattleFighter, List<AIFighter>> pair in attackPermissions)
        {
            maxTargetAttackers = Mathf.Max(maxTargetAttackers, pair.Value.Count);
            if (pair.Key != null && pair.Key.IsPlayer)
                maxPlayerAttackers = Mathf.Max(maxPlayerAttackers, pair.Value.Count);
        }
    }

    public bool AuditCoordination()
    {
        CleanupAttackPermissions();
        PlayerFighter captain = player();
        int playerAttackers = captain != null && attackPermissions.TryGetValue(captain, out List<AIFighter> attackers)
            ? attackers.Count : 0;
        int largestGroup = 0;
        foreach (List<AIFighter> group in attackPermissions.Values)
            largestGroup = Mathf.Max(largestGroup, group.Count);
        bool stableTargets = true;
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai.IsAlive && ai.CurrentTarget != null)
                stableTargets &= ai.CurrentTarget.Team != ai.Team && ai.CurrentTarget.IsAlive;
        return playerAttackers <= 1 && largestGroup <= 2 && stableTargets;
    }

    private float ScoreTarget(AIFighter seeker, BattleFighter target, bool current)
    {
        float distance = Vector3.Distance(seeker.transform.position, target.transform.position);
        int assigned = AssignedTo(target, seeker);
        float score = distance + assigned * 3.1f;
        if (target.IsPlayer)
            score -= 0.65f;
        if (seeker.Team == Team.Allies && target is AIFighter enemy && enemy.CurrentTarget == player())
            score += 4.5f;
        if (current)
            score -= 2.4f;
        if (target.IsAttackThreatening)
            score += 0.35f;
        // Finish the wounded: a hurt target draws converging fighters. Capped just
        // below the sticky-target bonus so a freshly-nicked foe does not yank the
        // whole line off its current target — only meaningfully hurt targets pull
        // focus, and the assigned-count term still caps how many pile on.
        score -= (1f - target.HealthNormalized) * 2.2f;
        // Prioritise high-value, fragile threats: archers (soft and lethal at range)
        // and enemy captains (elite morale anchors) are worth ganging up on.
        if (target.IsRanged)
            score -= 1.1f;
        if (target.Archetype == Archetype.Captain)
            score -= 0.8f;
        return score;
    }

    // Number of living AI targeting `target`, excluding `except`. Backed by a
    // once-per-frame snapshot so target scoring is O(1) per candidate instead of an
    // O(n) scan per candidate per seeker (which made selection O(n^2) overall).
    private int AssignedTo(BattleFighter target, AIFighter except)
    {
        if (assignedCountFrame != Time.frameCount)
            RebuildAssignedCounts();
        int count = assignedCountByTarget.TryGetValue(target, out int value) ? value : 0;
        if (except != null && except.IsAlive && except.CurrentTarget == target)
            count--;
        return count;
    }

    private void RebuildAssignedCounts()
    {
        assignedCountFrame = Time.frameCount;
        assignedCountByTarget.Clear();
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai.IsAlive && ai.CurrentTarget != null)
                assignedCountByTarget[ai.CurrentTarget] =
                    (assignedCountByTarget.TryGetValue(ai.CurrentTarget, out int existing) ? existing : 0) + 1;
    }

    private void CleanupAttackPermissions()
    {
        emptyPermissionTargets.Clear();
        foreach (KeyValuePair<BattleFighter, List<AIFighter>> pair in attackPermissions)
        {
            for (int i = pair.Value.Count - 1; i >= 0; i--)
            {
                AIFighter ai = pair.Value[i];
                if (ai == null || !ai.IsAlive || ai.CurrentTarget != pair.Key)
                    pair.Value.RemoveAt(i);
            }
            if (pair.Key == null || !pair.Key.IsAlive || pair.Value.Count == 0)
                emptyPermissionTargets.Add(pair.Key);
        }
        foreach (BattleFighter target in emptyPermissionTargets)
            attackPermissions.Remove(target);
    }

    // Assigns each non-attacking supporter a stable support-slot index (its rank by
    // instance id among fighters sharing the same target), rebuilt once per frame so
    // GetEngagementPosition is O(1) per call instead of an O(n) scan per fighter.
    private void RebuildEngagementSlots()
    {
        engagementSlotFrame = Time.frameCount;
        engagementSlots.Clear();
        foreach (List<AIFighter> group in engagementGroups.Values)
            group.Clear();
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai.IsAlive && ai.CurrentTarget != null && !ai.HasAttackPermission)
            {
                if (!engagementGroups.TryGetValue(ai.CurrentTarget, out List<AIFighter> group))
                {
                    group = new List<AIFighter>();
                    engagementGroups[ai.CurrentTarget] = group;
                }
                group.Add(ai);
            }
        foreach (List<AIFighter> group in engagementGroups.Values)
        {
            group.Sort(CompareByInstanceId);
            for (int i = 0; i < group.Count; i++)
                engagementSlots[group[i]] = i;
        }
    }

    private static int CompareByInstanceId(AIFighter a, AIFighter b) => a.GetInstanceID().CompareTo(b.GetInstanceID());
}
