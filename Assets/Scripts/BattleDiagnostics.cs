using UnityEngine;

// Deterministic checks used only by smoke-test tooling. Keeping these outside
// BattleManager prevents verification concerns from owning production state.
public static class BattleDiagnostics
{
    public static bool AuditDirectionalBlock(BattleManager battle)
    {
        if (battle.Player == null)
            return false;
        BattleFighter attacker = battle.FindNearestOpponent(battle.Player);
        if (attacker == null)
            return false;

        float startingHealth = battle.Player.CurrentHealth;
        Quaternion startingRotation = battle.Player.transform.rotation;
        Vector3 towardAttacker = attacker.transform.position - battle.Player.transform.position;
        towardAttacker.y = 0f;
        battle.Player.transform.rotation = Quaternion.LookRotation(towardAttacker.normalized);

        bool allCorrectBlocksStoppedDamage = true;
        bool allWrongBlocksTookDamage = true;
        foreach (CombatDirection direction in System.Enum.GetValues(typeof(CombatDirection)))
        {
            battle.Player.DebugSetBlock(true, direction);
            battle.Player.ReceiveHit(25f, attacker, direction);
            allCorrectBlocksStoppedDamage &= Mathf.Approximately(battle.Player.CurrentHealth, startingHealth);
            battle.Player.DebugRestoreHealth(startingHealth);

            CombatDirection wrongDirection = (CombatDirection)(((int)direction + 1) % 4);
            battle.Player.DebugSetBlock(true, wrongDirection);
            battle.Player.ReceiveHit(25f, attacker, direction);
            allWrongBlocksTookDamage &= Mathf.Approximately(battle.Player.CurrentHealth, startingHealth - 25f);
            battle.Player.DebugRestoreHealth(startingHealth);
        }

        battle.Player.transform.rotation = Quaternion.LookRotation(-towardAttacker.normalized);
        battle.Player.DebugSetBlock(true, CombatDirection.Up);
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Up);
        bool rearAttackBypassedBlock = Mathf.Approximately(battle.Player.CurrentHealth, startingHealth - 25f);
        battle.Player.DebugRestoreHealth(startingHealth);
        battle.Player.DebugSetBlock(false, CombatDirection.Right);
        battle.Player.transform.rotation = startingRotation;
        return allCorrectBlocksStoppedDamage && allWrongBlocksTookDamage && rearAttackBypassedBlock;
    }

    public static bool AuditResponsiveCombat(BattleManager battle)
    {
        bool jitterIgnored = !CombatGesture.TryResolve(new Vector2(4f, 3f), out _);
        bool diagonalIgnored = !CombatGesture.TryResolve(new Vector2(10f, 9f), out _);
        bool deliberateLeft = CombatGesture.TryResolve(new Vector2(-12f, 0f), out CombatDirection direction)
            && direction == CombatDirection.Left;

        battle.Player.DebugSetBlock(false, CombatDirection.Right);
        bool prepared = battle.Player.DebugPrepareAttack(CombatDirection.Up);
        battle.Player.DebugSetBlock(true, CombatDirection.Left);
        bool heldAttackCancelledIntoBlock = prepared && battle.Player.Phase == CombatPhase.Idle
            && battle.Player.IsBlocking && battle.Player.BlockDirection == CombatDirection.Left;
        battle.Player.DebugSetBlock(false, CombatDirection.Right);
        battle.Player.DebugRestoreStamina();

        BattleFighter enemy = battle.FindNearestOpponent(battle.Player);
        bool sweptTargetFound = false;
        if (enemy != null)
        {
            Vector3 center = enemy.transform.position + Vector3.up * 1.05f;
            sweptTargetFound = battle.FindSweptStrikeTarget(battle.Player, center + Vector3.left * 0.5f,
                center + Vector3.right * 0.5f, 0.72f) == enemy;
        }
        return jitterIgnored && diagonalIgnored && deliberateLeft && heldAttackCancelledIntoBlock && sweptTargetFound;
    }

    public static bool AuditCombatExcellence(BattleManager battle)
    {
        if (battle.Player == null)
            return false;
        BattleFighter attacker = battle.FindNearestOpponent(battle.Player);
        if (attacker == null)
            return false;

        Quaternion startingRotation = battle.Player.transform.rotation;
        Vector3 towardAttacker = attacker.transform.position - battle.Player.transform.position;
        towardAttacker.y = 0f;
        battle.Player.transform.rotation = Quaternion.LookRotation(towardAttacker.normalized);
        float health = battle.Player.CurrentHealth;

        battle.Player.DebugSetBlock(false, CombatDirection.Right);
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        bool perfectGrantedCounter = battle.Player.IsCounterReady && Mathf.Approximately(battle.Player.CurrentHealth, health);

        battle.Player.DebugClearCombatReaction();
        battle.Player.DebugSetBlock(false, CombatDirection.Right);
        bool counterPrepared = battle.Player.DebugPrepareAttack(CombatDirection.Up) && battle.Player.IsCounterAttack
            && Mathf.Approximately(battle.Player.CurrentAttackDamageMultiplier, 1.45f);
        battle.Player.DebugSetBlock(true, CombatDirection.Left);
        battle.Player.DebugSetBlock(false, CombatDirection.Left);

        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.DebugExpirePerfectBlock();
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        bool normalBlockNoCounter = !battle.Player.IsCounterReady && Mathf.Approximately(battle.Player.CurrentHealth, health);

        battle.Player.DebugClearCombatReaction();
        battle.Player.DebugSetBlock(true, CombatDirection.Left);
        battle.Player.DebugExpirePerfectBlock();
        battle.Player.DebugSetBlock(true, CombatDirection.Right);
        battle.Player.ReceiveHit(25f, attacker, CombatDirection.Right);
        bool correctedDirectionPerfect = battle.Player.IsCounterReady && Mathf.Approximately(battle.Player.CurrentHealth, health);

        battle.Player.DebugSetBlock(false, CombatDirection.Right);
        battle.Player.DebugRestoreHealth(health);
        battle.Player.DebugRestoreStamina();
        battle.Player.transform.rotation = startingRotation;
        return perfectGrantedCounter && counterPrepared && normalBlockNoCounter && correctedDirectionPerfect;
    }
}
