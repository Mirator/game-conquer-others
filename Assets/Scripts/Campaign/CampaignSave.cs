using System;
using UnityEngine;

// Serializable snapshot of a campaign plus the service that persists it to
// PlayerPrefs. A full snapshot (not just the seed) is stored so a save survives
// changes to procedural map generation. Settings persistence lives separately in
// SettingsService.
[Serializable]
public sealed class CampaignSaveData
{
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public int seed;
    public int gold;
    public bool campaignOver;
    public string lastReport;
    public int playerWeapon;
    public int trainingEnemyWeapon;
    public Vector2 partyPosition;
    public int day;
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
            campaignOver = state.CampaignOver,
            lastReport = state.LastReport,
            playerWeapon = (int)state.PlayerWeapon,
            trainingEnemyWeapon = (int)state.TrainingEnemyWeapon,
            partyPosition = state.PartyPosition,
            day = state.Day,
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
        if (data == null || data.version != CampaignSaveData.CurrentVersion || data.territories == null)
        {
            Delete();
            return null;
        }

        CampaignState state = new CampaignState
        {
            Seed = data.seed,
            Gold = data.gold,
            CampaignOver = data.campaignOver,
            LastReport = data.lastReport,
            PlayerWeapon = (WeaponType)data.playerWeapon,
            TrainingEnemyWeapon = (WeaponType)data.trainingEnemyWeapon,
            PartyPosition = data.partyPosition,
            Day = data.day
        };
        if (data.units != null)
            foreach (RosterEntry entry in data.units)
                state.Units.Add(entry.Tier, entry.Archetype, entry.Count);
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
                Threat = t.threat
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
