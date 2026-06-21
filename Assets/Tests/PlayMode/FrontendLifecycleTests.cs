using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class FrontendLifecycleTests
{
    [UnityTest]
    public IEnumerator Director_ActivatesTitleBackdropOnlyInTitleMode()
    {
        GameDirector director = Object.FindFirstObjectByType<GameDirector>();
        Assert.That(director, Is.Not.Null);
        director.ReturnToTitle();
        while (!director.IsModeReady(GameDirector.Mode.Title))
            yield return null;

        TitleBackdrop backdrop = Object.FindFirstObjectByType<TitleBackdrop>();
        Assert.That(backdrop, Is.Not.Null);
        Assert.That(backdrop.IsVisible, Is.True);
        Assert.That(backdrop.TitleCamera, Is.Not.Null);
        Assert.That(backdrop.TitleCamera.enabled, Is.True);

        director.StartNewCampaign();
        while (!director.IsModeReady(GameDirector.Mode.Map))
            yield return null;
        Assert.That(backdrop.IsVisible, Is.False);
        Assert.That(backdrop.TitleCamera.enabled, Is.False);

        director.ReturnToTitle();
        while (!director.IsModeReady(GameDirector.Mode.Title))
            yield return null;
    }

    [UnityTest]
    public IEnumerator Director_BuildsReadableCampaignDiorama()
    {
        GameDirector director = Object.FindFirstObjectByType<GameDirector>();
        Assert.That(director, Is.Not.Null);
        director.StartNewCampaign();
        while (!director.IsModeReady(GameDirector.Mode.Map))
            yield return null;
        yield return null; // let deferred collider removal on decorative props complete

        GameObject diorama = GameObject.Find("Campaign Diorama");
        Assert.That(diorama, Is.Not.Null);
        int districts = 0;
        foreach (Transform child in diorama.GetComponentsInChildren<Transform>())
            if (child.name.StartsWith("Diorama District"))
                districts++;
        Assert.That(districts, Is.EqualTo(director.Campaign.Territories.Count));

        GameObject table = GameObject.Find("Map Table");
        Assert.That(table, Is.Not.Null);
        Assert.That(table.GetComponent<Collider>(), Is.Not.Null, "The table remains the ground-click target.");
        Assert.That(diorama.GetComponentsInChildren<Collider>(), Has.Length.EqualTo(1),
            "Diorama dressing must not intercept clicks intended for holds, parties, or ground.");

        director.ReturnToTitle();
        while (!director.IsModeReady(GameDirector.Mode.Title))
            yield return null;
    }

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
