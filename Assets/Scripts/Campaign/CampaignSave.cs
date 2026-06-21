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
    private const string Key = "ConquerOthers.Campaign";

    public static bool HasSave => !string.IsNullOrEmpty(PlayerPrefs.GetString(Key, ""));

    public static void Save(CampaignState state)
    {
        if (state == null)
            return;
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
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static CampaignState Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json))
            return null;
        CampaignSaveData data = JsonUtility.FromJson<CampaignSaveData>(json);
        // Version 5 adds only a travel-day fraction, so version 4 campaigns can
        // safely resume at the start of their current day instead of being deleted.
        bool supportedVersion = data != null && (data.version == CampaignSaveData.CurrentVersion || data.version == 4);
        if (!supportedVersion || data.territories == null)
        {
            Delete();
            return null;
        }

        CampaignState state = new CampaignState
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
                state.Units.Add(entry.Tier, entry.Archetype, entry.Count);
                state.Units.AddXp(entry.Tier, entry.Archetype, entry.Xp);
            }
        if (data.parties != null)
            foreach (PartySaveData p in data.parties)
                state.Parties.Add(new EnemyParty
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
            state.Territories.Add(territory);
        }
        return state;
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
