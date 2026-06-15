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
        if (seeker.Team == Team.Enemies && captain != null && captain.IsAlive && CountAssignedTo(captain, seeker) == 0)
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
        int limit = target.IsPlayer ? 1 : 2;
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

    public Vector3 GetEngagementPosition(AIFighter seeker, BattleFighter target, bool activeAttacker, float preferredRange)
    {
        Vector3 radial = seeker.transform.position - target.transform.position;
        radial.y = 0f;
        if (radial.sqrMagnitude < 0.01f)
            radial = target.transform.forward;
        radial.Normalize();
        if (activeAttacker)
            return target.transform.position + radial * preferredRange;

        int index = 0;
        int seekerId = seeker.GetInstanceID();
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai.IsAlive && ai != seeker && ai.CurrentTarget == target
                && !ai.HasAttackPermission && ai.GetInstanceID() < seekerId)
                index++;
        float angle = SupportAngles[index % SupportAngles.Length] + index / SupportAngles.Length * 18f;
        Vector3 slotDirection = Quaternion.AngleAxis(angle, Vector3.up) * target.transform.forward;
        return target.transform.position + slotDirection.normalized * Mathf.Lerp(2.8f, 3.5f, index % 3 / 2f);
    }

    public Vector3 GetSeparation(BattleFighter seeker)
    {
        if (separationFrame != Time.frameCount)
            RebuildSeparationCache();
        return separationByFighter.TryGetValue(seeker, out Vector3 result) ? result : Vector3.zero;
    }

    private void RebuildSeparationCache()
    {
        separationFrame = Time.frameCount;
        separationByFighter.Clear();
        foreach (BattleFighter fighter in fighters)
            if (fighter.IsAlive)
                separationByFighter[fighter] = Vector3.zero;
        for (int i = 0; i < fighters.Count; i++)
        {
            BattleFighter first = fighters[i];
            if (!first.IsAlive)
                continue;
            for (int j = i + 1; j < fighters.Count; j++)
            {
                BattleFighter second = fighters[j];
                if (!second.IsAlive)
                    continue;
                Vector3 offset = first.transform.position - second.transform.position;
                offset.y = 0f;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared >= 6.25f || distanceSquared <= 0.001f)
                    continue;
                float distance = Mathf.Sqrt(distanceSquared);
                Vector3 force = offset / distance * Mathf.Clamp01((2.5f - distance) / 1.8f);
                separationByFighter[first] += force;
                separationByFighter[second] -= force;
            }
        }
        foreach (BattleFighter fighter in fighters)
            if (fighter.IsAlive && separationByFighter.TryGetValue(fighter, out Vector3 result))
                separationByFighter[fighter] = Vector3.ClampMagnitude(result, 1.4f);
    }

    public void UpdateTelemetry()
    {
        CleanupAttackPermissions();
        int closePairs = 0;
        for (int i = 0; i < fighters.Count; i++)
        {
            if (!fighters[i].IsAlive)
                continue;
            for (int j = i + 1; j < fighters.Count; j++)
            {
                if (!fighters[j].IsAlive)
                    continue;
                float distance = Vector3.Distance(fighters[i].transform.position, fighters[j].transform.position);
                minimumFighterDistance = Mathf.Min(minimumFighterDistance, distance);
                if (distance < 1.05f)
                    closePairs++;
            }
        }
        maxClosePairs = Mathf.Max(maxClosePairs, closePairs);
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
        int assigned = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai != seeker && ai.IsAlive && ai.CurrentTarget == target)
                assigned++;
        float score = distance + assigned * 3.1f;
        if (target.IsPlayer)
            score -= 0.65f;
        if (seeker.Team == Team.Allies && target is AIFighter enemy && enemy.CurrentTarget == player())
            score += 4.5f;
        if (current)
            score -= 2.4f;
        if (target.IsAttackThreatening)
            score += 0.35f;
        return score;
    }

    private int CountAssignedTo(BattleFighter target, AIFighter except)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai != except && ai.IsAlive && ai.CurrentTarget == target)
                count++;
        return count;
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

    private static readonly float[] SupportAngles = { -72f, 72f, -138f, 138f, 180f, -105f, 105f };
}
