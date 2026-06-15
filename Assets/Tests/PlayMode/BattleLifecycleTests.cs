using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BattleLifecycleTests
{
    [UnityTest]
    public IEnumerator BattleManager_TransitionsFromReadyToVictoryAndConcludesOnce()
    {
        GameObject root = new("Battle Test");
        BattleManager manager = root.AddComponent<BattleManager>();
        manager.Configure(null, null);
        PlayerFighter player = CreateFighter<PlayerFighter>(root, manager, Team.Allies, true);
        CreateFighter<AIFighter>(root, manager, Team.Enemies, false);
        int conclusions = 0;
        BattleResult result = null;
        manager.OnBattleConcluded = value =>
        {
            conclusions++;
            result = value;
        };

        manager.BeginBattle();
        manager.DebugEliminateTeam(Team.Enemies);
        yield return null;

        Assert.That(manager.State, Is.EqualTo(BattleManager.BattleState.Victory));
        Assert.That(root.GetComponent<BattleHud>(), Is.Not.Null);
        manager.ConfirmResult();
        manager.ConfirmResult();
        Assert.That(conclusions, Is.EqualTo(1));
        Assert.That(result.PlayerWon, Is.True);

        Object.Destroy(root);
        yield return null;
    }

    private static T CreateFighter<T>(GameObject root, BattleManager manager, Team team, bool player)
        where T : BattleFighter
    {
        GameObject go = new(typeof(T).Name);
        go.transform.SetParent(root.transform);
        T fighter = go.AddComponent<T>();
        fighter.Configure(manager, team, player);
        manager.Register(fighter);
        return fighter;
    }
}
