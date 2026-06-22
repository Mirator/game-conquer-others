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

    [UnityTest]
    public IEnumerator TargetSelection_PrefersTheWoundedEnemy()
    {
        GameObject root = new("Target Selection Wounded");
        BattleManager manager = CreateManager(root);
        CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        // An ally seeker has no captain-shortcut, so it scores enemy targets directly.
        AIFighter seeker = CreateFighter<AIFighter>(root, manager, Team.Allies, false, Vector3.zero);
        AIFighter healthy = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, new Vector3(3f, 0f, 0f));
        AIFighter wounded = CreateFighter<AIFighter>(root, manager, Team.Enemies, false, new Vector3(-3f, 0f, 0f));
        wounded.DebugRestoreHealth(1f);

        Assert.That(manager.SelectTacticalTarget(seeker, null), Is.EqualTo((BattleFighter)wounded),
            "An ally should converge on a badly wounded enemy over an equidistant healthy one.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator CorrectDirectionBlock_StopsAllDamage()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        float startingHealth = battle.Player.CurrentHealth;
        foreach (CombatDirection direction in System.Enum.GetValues(typeof(CombatDirection)))
        {
            battle.Player.DebugSetBlock(true, direction);
            battle.Player.ReceiveHit(25f, attacker, direction);
            Assert.That(Mathf.Approximately(battle.Player.CurrentHealth, startingHealth), Is.True,
                $"Block {direction} should stop all damage from a {direction} attack.");
            battle.Player.DebugRestoreHealth(startingHealth);
            battle.Player.DebugSetBlock(false, direction);
            battle.Player.DebugClearCombatReaction();
        }
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator WrongDirectionBlock_TakesFullDamage()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        float startingHealth = battle.Player.CurrentHealth;
        foreach (CombatDirection direction in System.Enum.GetValues(typeof(CombatDirection)))
        {
            CombatDirection wrongDirection = (CombatDirection)(((int)direction + 1) % 4);
            battle.Player.DebugSetBlock(true, wrongDirection);
            battle.Player.ReceiveHit(25f, attacker, direction);
            Assert.That(Mathf.Approximately(battle.Player.CurrentHealth, startingHealth - 25f), Is.True,
                $"Block {wrongDirection} should not stop a {direction} attack.");
            battle.Player.DebugRestoreHealth(startingHealth);
            battle.Player.DebugSetBlock(false, direction);
            battle.Player.DebugClearCombatReaction();
        }
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator AttackFromBehind_BypassesBlock()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        float startingHealth = battle.Player.CurrentHealth;
        Vector3 toward = attacker.transform.position - battle.Player.transform.position;
        toward.y = 0f;
        battle.Player.transform.rotation = Quaternion.LookRotation(-toward.normalized);
        battle.Player.DebugSetBlock(true, CombatDirection.Up);
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Up);
        Assert.That(Mathf.Approximately(battle.Player.CurrentHealth, startingHealth - 25f), Is.True,
            "An attack from behind should bypass the block.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator PerfectBlock_GrantsCounterAndTakesNoDamage()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        float startingHealth = battle.Player.CurrentHealth;
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        Assert.That(battle.Player.IsCounterReady, Is.True, "A perfect block should grant a counter.");
        Assert.That(Mathf.Approximately(battle.Player.CurrentHealth, startingHealth), Is.True,
            "A perfect block should take no damage.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator CounterAttack_HasIncreasedDamageMultiplier()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        battle.Player.DebugClearCombatReaction();
        battle.Player.DebugSetBlock(false, CombatDirection.Right);
        Assert.That(battle.Player.DebugPrepareAttack(CombatDirection.Up), Is.True,
            "Should be able to prepare an attack after earning a counter.");
        Assert.That(battle.Player.IsCounterAttack, Is.True, "The prepared attack should be a counter attack.");
        Assert.That(Mathf.Approximately(battle.Player.CurrentAttackDamageMultiplier, 1.45f), Is.True,
            "A counter attack should have an increased damage multiplier.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator ExpiredWindowBlock_BlocksDamageButGrantsNoCounter()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        float startingHealth = battle.Player.CurrentHealth;
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.DebugExpirePerfectBlock();
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        Assert.That(Mathf.Approximately(battle.Player.CurrentHealth, startingHealth), Is.True,
            "An expired-window block should still block damage.");
        Assert.That(battle.Player.IsCounterReady, Is.False,
            "An expired-window block should not grant a counter.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator CorrectedBlockDirection_StillEarnsPerfectBlock()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        float startingHealth = battle.Player.CurrentHealth;
        battle.Player.DebugSetBlock(true, CombatDirection.Left);
        battle.Player.DebugExpirePerfectBlock();
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        Assert.That(battle.Player.IsCounterReady, Is.True,
            "Re-aiming the block to the incoming direction should reset the perfect-block window.");
        Assert.That(Mathf.Approximately(battle.Player.CurrentHealth, startingHealth), Is.True,
            "A corrected-direction perfect block should take no damage.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator Attack_ConsumesStamina()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out _);
        battle.Player.DebugRestoreStamina();
        float before = battle.Player.StaminaNormalized;
        Assert.That(battle.Player.DebugPrepareAttack(CombatDirection.Right), Is.True);
        Assert.That(battle.Player.StaminaNormalized, Is.LessThan(before), "Preparing an attack should spend stamina.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator Attack_RequiresStamina()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out _);
        battle.Player.DebugSetStamina(0f);
        Assert.That(battle.Player.DebugPrepareAttack(CombatDirection.Right), Is.False,
            "An exhausted fighter should not be able to attack.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator NonPerfectBlock_DrainsStaminaButStillBlocks()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        battle.Player.DebugRestoreStamina();
        float startingHealth = battle.Player.CurrentHealth;
        float before = battle.Player.StaminaNormalized;
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.DebugExpirePerfectBlock();
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        Assert.That(battle.Player.StaminaNormalized, Is.LessThan(before), "A non-perfect block should cost stamina.");
        Assert.That(Mathf.Approximately(battle.Player.CurrentHealth, startingHealth), Is.True,
            "A non-perfect block with stamina to spare should still stop the damage.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator AttackCooldown_BlocksRapidMeleeSwings()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out _);
        battle.Player.DebugRestoreStamina();
        battle.Player.DebugSetAttackCooldown(0.2f);
        Assert.That(battle.Player.DebugPrepareAttack(CombatDirection.Right), Is.False,
            "The post-swing cooldown should block an immediate next swing.");
        battle.Player.DebugSetAttackCooldown(0f);
        Assert.That(battle.Player.DebugPrepareAttack(CombatDirection.Right), Is.True,
            "A swing should be allowed once the cooldown elapses.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator AttackCooldown_DoesNotBlockCounters()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right); // perfect block opens a counter window
        battle.Player.DebugClearCombatReaction();
        battle.Player.DebugSetBlock(false, CombatDirection.Right);
        battle.Player.DebugRestoreStamina();
        battle.Player.DebugSetAttackCooldown(0.2f);
        Assert.That(battle.Player.DebugPrepareAttack(CombatDirection.Up), Is.True,
            "A perfect-block counter should bypass the swing cooldown.");
        Assert.That(battle.Player.IsCounterAttack, Is.True, "The bypassing attack should be the counter.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator ExhaustedBlock_BreaksGuardAndTakesFullDamage()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter attacker);
        float startingHealth = battle.Player.CurrentHealth;
        // A correct-direction, expired-window block would normally absorb the blow
        // (see NonPerfectBlock_DrainsStaminaButStillBlocks). With no stamina to pay,
        // the guard breaks and the hit lands in full — the guard-break path.
        battle.Player.DebugSetStamina(0f);
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.DebugExpirePerfectBlock();
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        Assert.That(Mathf.Approximately(battle.Player.CurrentHealth, startingHealth - 25f), Is.True,
            "An exhausted guard should break and take full damage despite a correct-direction block.");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator LateGuardRead_SnapsSkilledGuardToTheReaimedSwing()
    {
        BattleManager battle = CreateFacingDuel(out GameObject root, out BattleFighter enemy);
        AIFighter ai = (AIFighter)enemy;
        ai.SetProfile(new AIProfile { blockChance = 1f, lateReadChance = 1f });
        SetTarget(ai, battle.Player);
        // The AI guessed the wrong guard (Left) and is holding it.
        SetPrivate(ai, "blockTimer", 0.5f);
        SetPrivate(ai, "lateReadArmed", true);
        SetPrivate(ai, "plannedBlockDirection", CombatDirection.Left);

        // The player commits a Right swing, now in the release that the AI reads.
        Assert.That(battle.Player.DebugForceAttackRelease(CombatDirection.Right), Is.True);
        Assert.That(battle.Player.Phase, Is.EqualTo(CombatPhase.AttackRelease));

        Invoke(ai, "TryLateGuardRead");
        Assert.That(GetPrivate<CombatDirection>(ai, "plannedBlockDirection"), Is.EqualTo(CombatDirection.Right),
            "A lateReadChance=1 fighter should re-snap its guard to the swing's true line.");
        yield return DestroyRoot(root);
    }

    private static BattleManager CreateFacingDuel(out GameObject root, out BattleFighter attacker)
    {
        BattleManager battle = CreateDuel(out root);
        attacker = battle.FindNearestOpponent(battle.Player);
        Vector3 toward = attacker.transform.position - battle.Player.transform.position;
        toward.y = 0f;
        battle.Player.transform.rotation = Quaternion.LookRotation(toward.normalized);
        return battle;
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

    private static void SetPrivate(object obj, string field, object value)
        => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(obj, value);

    private static T GetPrivate<T>(object obj, string field)
        => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);

    private static void Invoke(object obj, string method)
        => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(obj, null);

    private static IEnumerator DestroyRoot(GameObject root)
    {
        Object.Destroy(root);
        yield return null;
    }
}
