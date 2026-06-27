using NUnit.Framework;
using UnityEngine;

// Hardened-save behaviour: the backup slot must recover a campaign from a corrupt or
// partial primary write rather than silently losing all progress.
public sealed class CampaignSaveTests
{
    [SetUp]
    public void SetUp() => CampaignSaveService.Delete();

    [TearDown]
    public void TearDown() => CampaignSaveService.Delete();

    [Test]
    public void Load_RecoversFromBackup_WhenPrimaryIsCorrupt()
    {
        CampaignState first = CampaignState.CreateDefault(11);
        first.Gold = 500;
        CampaignSaveService.Save(first);              // primary = first; no backup yet

        CampaignState second = CampaignState.CreateDefault(11);
        second.Gold = 999;
        CampaignSaveService.Save(second);             // backup = first (500), primary = second (999)

        PlayerPrefs.SetString(CampaignSaveService.Key, "{ corrupt");
        PlayerPrefs.Save();

        CampaignState recovered = CampaignSaveService.Load();
        Assert.That(recovered, Is.Not.Null);
        Assert.That(recovered.Gold, Is.EqualTo(500)); // last-known-good backup

        // The recovered backup is restored as the primary, so a second load is stable.
        Assert.That(CampaignSaveService.Load().Gold, Is.EqualTo(500));
    }

    [Test]
    public void Load_ReturnsNullAndClears_WhenBothSlotsCorrupt()
    {
        CampaignState state = CampaignState.CreateDefault(5);
        CampaignSaveService.Save(state);
        CampaignSaveService.Save(state);              // ensure the backup slot is populated

        PlayerPrefs.SetString(CampaignSaveService.Key, "garbage");
        PlayerPrefs.SetString(CampaignSaveService.BackupKey, "garbage");
        PlayerPrefs.Save();

        Assert.That(CampaignSaveService.Load(), Is.Null);
        Assert.That(CampaignSaveService.HasSave, Is.False);
    }

    [Test]
    public void Load_ReturnsNull_OnUnsupportedVersion()
    {
        CampaignSaveData future = new CampaignSaveData { version = 999, territories = new TerritorySaveData[0] };
        PlayerPrefs.SetString(CampaignSaveService.Key, JsonUtility.ToJson(future));
        PlayerPrefs.Save();

        Assert.That(CampaignSaveService.Load(), Is.Null);
        Assert.That(CampaignSaveService.HasSave, Is.False);
    }

    [Test]
    public void Load_ReturnsNull_OnGarbagePrimaryWithoutBackup()
    {
        PlayerPrefs.SetString(CampaignSaveService.Key, "not even json {{{");
        PlayerPrefs.Save();

        Assert.That(CampaignSaveService.Load(), Is.Null);
    }

    [Test]
    public void Save_RoundTripsEdgeValues()
    {
        CampaignState state = CampaignState.CreateDefault(3);
        state.Gold = 0;
        state.Morale = 0;
        state.Day = 999;
        state.DayProgress = 1f;
        CampaignSaveService.Save(state);

        CampaignState loaded = CampaignSaveService.Load();
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.Gold, Is.EqualTo(0));
        Assert.That(loaded.Morale, Is.EqualTo(0));
        Assert.That(loaded.Day, Is.EqualTo(999));
        Assert.That(loaded.DayProgress, Is.EqualTo(1f));
    }
}
