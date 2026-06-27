using System;
using UnityEngine;

// Serializable snapshot of a campaign plus the service that persists it to
// PlayerPrefs. A full snapshot (not just the seed) is stored so a save survives
// changes to procedural map generation. Settings persistence lives separately in
// SettingsService.
[Serializable]
public sealed class CampaignSaveData
{
    public const int CurrentVersion = 5;

    public int version = CurrentVersion;
    public int seed;
    public int gold;
    public int renown;
    public int morale;
    public bool campaignOver;
    public string lastReport;
    public int playerWeapon;
    public int trainingEnemyWeapon;
    public Vector2 partyPosition;
    public int day;
    public float dayProgress;
    public RosterEntry[] units;
    public TerritorySaveData[] territories;
    public PartySaveData[] parties;
}

[Serializable]
public sealed class PartySaveData
{
    public Vector2 position;
    public int strength;
    public string name;
    public int arena;
}

[Serializable]
public sealed class TerritorySaveData
{
    public int id;
    public string name;
    public Vector2 mapPosition;
    public int owner;
    public int garrison;
    public float difficultyScale;
    public int arena;
    public int rewardGold;
    public int income;
    public int threat;
    public int settlement;
    public int recruits;
    public int[] adjacentIds;
}

public static class CampaignSaveService
{
    // Public so white-box tests can simulate a corrupt slot. BackupKey holds the
    // last-known-good save promoted before each overwrite.
    public const string Key = "ConquerOthers.Campaign";
    public const string BackupKey = Key + ".bak";

    public static bool HasSave =>
        !string.IsNullOrEmpty(PlayerPrefs.GetString(Key, "")) || !string.IsNullOrEmpty(PlayerPrefs.GetString(BackupKey, ""));

    public static void Save(CampaignState state)
    {
        if (state == null)
            return;
        string json;
        try
        {
            json = JsonUtility.ToJson(BuildData(state));
        }
        catch (Exception e)
        {
            // Never clobber a good save with a failed serialization.
            Debug.LogWarning($"Campaign save failed to serialize: {e.Message}");
            return;
        }
        // Promote the current primary to the backup slot before overwriting, so a bad or
        // partial write can't lose the last good save.
        string existing = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(existing))
            PlayerPrefs.SetString(BackupKey, existing);
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
    }

    public static CampaignState Load()
    {
        if (TryDeserialize(PlayerPrefs.GetString(Key, ""), out CampaignState state))
            return state;
        // Primary missing/corrupt — recover the last-known-good backup and restore it as
        // the primary so subsequent loads are stable instead of losing the campaign.
        string backup = PlayerPrefs.GetString(BackupKey, "");
        if (TryDeserialize(backup, out state))
        {
            PlayerPrefs.SetString(Key, backup);
            PlayerPrefs.Save();
            return state;
        }
        // Nothing recoverable; clear both so the title doesn't offer a broken continue.
        Delete();
        return null;
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.DeleteKey(BackupKey);
        PlayerPrefs.Save();
    }

    private static CampaignSaveData BuildData(CampaignState state)
    {
        CampaignSaveData data = new CampaignSaveData
        {
            seed = state.Seed,
            gold = state.Gold,
            renown = state.Renown,
            morale = state.Morale,
            campaignOver = state.CampaignOver,
            lastReport = state.LastReport,
            playerWeapon = (int)state.PlayerWeapon,
            trainingEnemyWeapon = (int)state.TrainingEnemyWeapon,
            partyPosition = state.PartyPosition,
            day = state.Day,
            dayProgress = state.DayProgress,
            units = state.Units.Entries.ToArray(),
            territories = new TerritorySaveData[state.Territories.Count],
            parties = new PartySaveData[state.Parties.Count]
        };
        for (int i = 0; i < state.Parties.Count; i++)
        {
            EnemyParty party = state.Parties[i];
            data.parties[i] = new PartySaveData
            {
                position = party.Position,
                strength = party.Strength,
                name = party.Name,
                arena = (int)party.Arena
            };
        }
        for (int i = 0; i < state.Territories.Count; i++)
        {
            Territory t = state.Territories[i];
            data.territories[i] = new TerritorySaveData
            {
                id = t.Id,
                name = t.Name,
                mapPosition = t.MapPosition,
                owner = (int)t.Owner,
                garrison = t.Garrison,
                difficultyScale = t.DifficultyScale,
                arena = (int)t.Arena,
                rewardGold = t.RewardGold,
                income = t.Income,
                threat = t.Threat,
                settlement = (int)t.Settlement,
                recruits = t.Recruits,
                adjacentIds = t.AdjacentIds.ToArray()
            };
        }
        return data;
    }

    // Parses one save slot. Returns false (rather than throwing or deleting) on empty,
    // malformed, or unsupported-version data so the caller can fall back to the backup.
    private static bool TryDeserialize(string json, out CampaignState state)
    {
        state = null;
        if (string.IsNullOrEmpty(json))
            return false;
        CampaignSaveData data;
        try
        {
            data = JsonUtility.FromJson<CampaignSaveData>(json);
        }
        catch
        {
            return false;
        }
        // Version 5 adds only a travel-day fraction, so version 4 campaigns can safely
        // resume at the start of their current day instead of being discarded.
        bool supportedVersion = data != null && (data.version == CampaignSaveData.CurrentVersion || data.version == 4);
        if (!supportedVersion || data.territories == null)
            return false;

        CampaignState loaded = new CampaignState
        {
            Seed = data.seed,
            Gold = data.gold,
            Renown = data.renown,
            Morale = data.morale,
            CampaignOver = data.campaignOver,
            LastReport = data.lastReport,
            PlayerWeapon = (WeaponType)data.playerWeapon,
            TrainingEnemyWeapon = (WeaponType)data.trainingEnemyWeapon,
            PartyPosition = data.partyPosition,
            Day = data.day,
            DayProgress = data.version >= CampaignSaveData.CurrentVersion ? Mathf.Clamp01(data.dayProgress) : 0f
        };
        if (data.units != null)
            foreach (RosterEntry entry in data.units)
            {
                loaded.Units.Add(entry.Tier, entry.Archetype, entry.Count);
                loaded.Units.AddXp(entry.Tier, entry.Archetype, entry.Xp);
            }
        if (data.parties != null)
            foreach (PartySaveData p in data.parties)
                loaded.Parties.Add(new EnemyParty
                {
                    Position = p.position,
                    Strength = p.strength,
                    Name = p.name,
                    Arena = (ArenaType)p.arena
                });
        foreach (TerritorySaveData t in data.territories)
        {
            Territory territory = new Territory
            {
                Id = t.id,
                Name = t.name,
                MapPosition = t.mapPosition,
                Owner = (TerritoryOwner)t.owner,
                Garrison = t.garrison,
                DifficultyScale = t.difficultyScale,
                Arena = (ArenaType)t.arena,
                RewardGold = t.rewardGold,
                Income = t.income,
                Threat = t.threat,
                Settlement = (SettlementType)t.settlement,
                Recruits = t.recruits
            };
            if (t.adjacentIds != null)
                territory.AdjacentIds.AddRange(t.adjacentIds);
            loaded.Territories.Add(territory);
        }
        state = loaded;
        return true;
    }
}
