using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class FrontendLifecycleTests
{
    [UnityTest]
    public IEnumerator Director_PausesAndResumesBattle()
    {
        GameDirector director = Object.FindFirstObjectByType<GameDirector>();
        Assert.That(director, Is.Not.Null);
        director.LaunchBattle(BattleSetup.Default());
        while (!director.IsModeReady(GameDirector.Mode.Battle))
            yield return null;

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        battle.BeginBattle();
        director.Pause();
        Assert.That(director.IsPaused, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(Cursor.visible, Is.True);

        director.Resume();
        Assert.That(director.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        director.ReturnToTitle();
        while (!director.IsModeReady(GameDirector.Mode.Title))
            yield return null;
    }
}
