using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// AI targeting invariants and the retreat lifecycle. Deterministic (no frame-timing
// assumptions) — they drive BattleManager/BattleTactics directly. Combat resolution
// (blocks, stamina, permissions, target preferences) is covered by CombatRulesTests.
public sealed class AiBehaviorTests
{
    [UnityTest]
    public IEnumerator EnemyAi_LocksOntoThePlayerCaptain()
    {
        GameObject root = new("AI Captain Target");
        BattleManager manager = CreateManager(root);
        PlayerFighter player = CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter enemy = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, new Vector3(0f, 0f, 4f));
        // A closer allied soldier (an opponent of this enemy) exists only to prove the
        // captain shortcut wins over raw proximity for the first unassigned attacker.
        CreateFighter<AIFighter>(root, manager, Team.Allies, false, new Vector3(0f, 0f, 3.5f));

        Assert.That(manager.SelectTacticalTarget(enemy, null), Is.EqualTo((BattleFighter)player),
            "An enemy with no current focus should lock onto the player captain.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator TargetSelection_ReturnsNull_WhenNoEnemiesRemain()
    {
        GameObject root = new("AI No Enemies");
        BattleManager manager = CreateManager(root);
        CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter seeker = CreateFighter<AIFighter>(root, manager, Team.Allies, false, Vector3.zero);

        Assert.That(manager.SelectTacticalTarget(seeker, null), Is.Null,
            "With no living opponents, target selection yields nothing.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator TargetSelection_NeverChoosesAFriendly()
    {
        GameObject root = new("AI Friendly Fire Guard");
        BattleManager manager = CreateManager(root);
        CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter seeker = CreateFighter<AIFighter>(root, manager, Team.Allies, false, Vector3.zero);
        CreateFighter<AIFighter>(root, manager, Team.Allies, false, new Vector3(1f, 0f, 0f));
        AIFighter enemy = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, new Vector3(6f, 0f, 0f));

        BattleFighter chosen = manager.SelectTacticalTarget(seeker, null);
        Assert.That(chosen, Is.EqualTo((BattleFighter)enemy));
        Assert.That(chosen.Team, Is.Not.EqualTo(seeker.Team), "An AI never targets its own side.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator Retreat_WithdrawsFighterAndDropsAliveCount()
    {
        GameObject root = new("AI Retreat");
        BattleManager manager = CreateManager(root);
        CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter enemy = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, new Vector3(0f, 0f, 4f));
        int before = manager.EnemiesAlive;

        manager.NotifyRetreat(enemy);

        Assert.That(enemy.IsAlive, Is.False, "A retreated fighter has withdrawn from the battle.");
        Assert.That(manager.EnemiesAlive, Is.LessThan(before), "Withdrawal drops the living-enemy count.");
        yield return DestroyRoot(root);
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

    private static IEnumerator DestroyRoot(GameObject root)
    {
        Object.Destroy(root);
        yield return null;
    }
}
