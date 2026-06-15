using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatRulesTests
{
    [UnityTest]
    public IEnumerator DirectionalBlockDiagnostics_PassInFreshDuel()
    {
        BattleManager battle = CreateDuel(out GameObject root);
        Assert.That(BattleDiagnostics.AuditDirectionalBlock(battle), Is.True);
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator ResponsiveCombatDiagnostics_PassInFreshDuel()
    {
        BattleManager battle = CreateDuel(out GameObject root);
        Assert.That(BattleDiagnostics.AuditResponsiveCombat(battle), Is.True);
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator PerfectBlockAndCounterDiagnostics_PassInFreshDuel()
    {
        BattleManager battle = CreateDuel(out GameObject root);
        Assert.That(BattleDiagnostics.AuditCombatExcellence(battle), Is.True);
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator Tactics_EnforceAttackPermissionLimits()
    {
        GameObject root = new("Tactics Permissions");
        BattleManager manager = CreateManager(root);
        PlayerFighter player = CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter enemyOne = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, Vector3.forward);
        AIFighter enemyTwo = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, Vector3.forward * 2f);
        SetTarget(enemyOne, player);
        SetTarget(enemyTwo, player);

        Assert.That(manager.TryClaimAttackPermission(enemyOne, player), Is.True);
        Assert.That(manager.TryClaimAttackPermission(enemyTwo, player), Is.False);

        AIFighter allyTarget = CreateFighter<AIFighter>(root, manager, Team.Allies, false, Vector3.right * 4f);
        AIFighter enemyThree = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, Vector3.right * 5f);
        SetTarget(enemyOne, allyTarget);
        SetTarget(enemyTwo, allyTarget);
        SetTarget(enemyThree, allyTarget);
        manager.ReleaseAttackPermission(enemyOne);

        Assert.That(manager.TryClaimAttackPermission(enemyOne, allyTarget), Is.True);
        Assert.That(manager.TryClaimAttackPermission(enemyTwo, allyTarget), Is.True);
        Assert.That(manager.TryClaimAttackPermission(enemyThree, allyTarget), Is.False);
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator Tactics_DistributeSupportersAndApplySymmetricSeparation()
    {
        GameObject root = new("Tactics Positioning");
        BattleManager manager = CreateManager(root);
        PlayerFighter target = CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter first = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, new Vector3(-0.5f, 0f, 1f));
        AIFighter second = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, new Vector3(0.5f, 0f, 1f));
        SetTarget(first, target);
        SetTarget(second, target);

        Vector3 firstSlot = manager.GetEngagementPosition(first, target, false, 1.8f);
        Vector3 secondSlot = manager.GetEngagementPosition(second, target, false, 1.8f);
        Assert.That(Vector3.Distance(firstSlot, secondSlot), Is.GreaterThan(1f));
        Assert.That(Vector3.Distance(firstSlot, target.transform.position), Is.InRange(2.79f, 3.51f));
        Assert.That(Vector3.Distance(secondSlot, target.transform.position), Is.InRange(2.79f, 3.51f));

        target.transform.position = Vector3.forward * 10f;
        Vector3 firstSeparation = manager.GetSeparation(first);
        Vector3 secondSeparation = manager.GetSeparation(second);
        Assert.That(firstSeparation.magnitude, Is.GreaterThan(0f).And.LessThanOrEqualTo(1.4f));
        Assert.That((firstSeparation + secondSeparation).magnitude, Is.LessThan(0.001f));
        yield return DestroyRoot(root);
    }

    private static BattleManager CreateDuel(out GameObject root)
    {
        root = new GameObject("Diagnostic Duel");
        BattleManager manager = CreateManager(root);
        CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        CreateFighter<AIFighter>(root, manager, Team.Enemies, false, Vector3.forward * 1.5f);
        manager.BeginBattle();
        return manager;
    }

    private static BattleManager CreateManager(GameObject root)
    {
        BattleManager manager = root.AddComponent<BattleManager>();
        manager.Configure(null, null);
        return manager;
    }

    private static T CreateFighter<T>(GameObject root, BattleManager manager, Team team, bool player, Vector3 position)
        where T : BattleFighter
    {
        GameObject go = new(typeof(T).Name);
        go.transform.SetParent(root.transform);
        go.transform.position = position;
        T fighter = go.AddComponent<T>();
        fighter.Configure(manager, team, player);
        manager.Register(fighter);
        return fighter;
    }

    private static void SetTarget(AIFighter fighter, BattleFighter target)
    {
        typeof(AIFighter).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(fighter, target);
    }

    private static IEnumerator DestroyRoot(GameObject root)
    {
        Object.Destroy(root);
        yield return null;
    }
}
