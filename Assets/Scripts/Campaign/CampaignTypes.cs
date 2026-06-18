using System.Collections.Generic;

public enum UnitType
{
    Militia,
    Veteran,
    Guard
}

public enum ArenaType
{
    Courtyard,
    Forest,
    Marsh,
    Highlands
}

// What kind of encounter a battle is, so the battlefield can be dressed to match
// (a fortified hold for an assault, an open bandit camp for a field fight).
public enum BattleKind
{
    Training,
    SettlementAssault,
    BanditField
}

public enum WeaponType
{
    SwordAndShield,
    TwoHandedSword,
    Bow
}

// A fighter's combat personality: weapon, AI behavior, and a stat modifier
// layered on top of its UnitType stat tier. Decoupled from UnitType so any tier
// can field any archetype.
public enum Archetype
{
    Soldier,
    Shieldbearer,
    Berserker,
    Archer,
    Captain
}

public static class ArchetypeCatalog
{
    public static WeaponType Weapon(Archetype archetype) => archetype switch
    {
        Archetype.Berserker => WeaponType.TwoHandedSword,
        Archetype.Archer => WeaponType.Bow,
        _ => WeaponType.SwordAndShield
    };

    public static AIProfile Profile(Archetype archetype) => archetype switch
    {
        Archetype.Shieldbearer => AIProfile.Shieldbearer(),
        Archetype.Berserker => AIProfile.Berserker(),
        Archetype.Archer => AIProfile.Archer(),
        Archetype.Captain => AIProfile.Captain(),
        _ => AIProfile.Soldier()
    };

    // Multiplies the UnitType health/damage scale so the same archetype reads
    // distinctly across tiers (a tanky shieldbearer, a glassy berserker, an
    // elite captain).
    public static float HealthScale(Archetype archetype) => archetype switch
    {
        Archetype.Shieldbearer => 1.15f,
        Archetype.Berserker => 0.95f,
        Archetype.Archer => 0.9f,
        Archetype.Captain => 1.45f,
        _ => 1f
    };

    public static float DamageScale(Archetype archetype) => archetype switch
    {
        Archetype.Shieldbearer => 0.9f,
        Archetype.Berserker => 1.2f,
        Archetype.Captain => 1.25f,
        _ => 1f
    };

    public static string Label(Archetype archetype) => archetype switch
    {
        Archetype.Shieldbearer => "SHIELDBEARER",
        Archetype.Berserker => "BERSERKER",
        Archetype.Archer => "ARCHER",
        Archetype.Captain => "CAPTAIN",
        _ => "SOLDIER"
    };
}

public static class WeaponCatalog
{
    public static string Label(WeaponType type) => type switch
    {
        WeaponType.TwoHandedSword => "TWO-HANDED SWORD",
        WeaponType.Bow => "BOW",
        _ => "SWORD & SHIELD"
    };

    public static string ShortLabel(WeaponType type) => type switch
    {
        WeaponType.TwoHandedSword => "2H SWORD",
        WeaponType.Bow => "BOW",
        _ => "SWORD + SHIELD"
    };

    public static string Description(WeaponType type) => type switch
    {
        WeaponType.TwoHandedSword => "Long reach and heavy damage. Directional weapon blocks.",
        WeaponType.Bow => "Draw with LMB. Hold until steady for accurate shots.",
        _ => "Balanced melee weapon with a shield that can stop arrows."
    };

    public static WeaponType DefaultFor(UnitType unitType) => unitType switch
    {
        UnitType.Veteran => WeaponType.TwoHandedSword,
        UnitType.Guard => WeaponType.Bow,
        _ => WeaponType.SwordAndShield
    };

    public static WeaponType Next(WeaponType type) => (WeaponType)(((int)type + 1) % 3);
    public static WeaponType Previous(WeaponType type) => (WeaponType)(((int)type + 2) % 3);
}

public static class UnitCatalog
{
    public static int Cost(UnitType type) => type switch
    {
        UnitType.Veteran => 70,
        UnitType.Guard => 110,
        _ => 35
    };

    public static float HealthScale(UnitType type) => type switch
    {
        UnitType.Veteran => 1.18f,
        UnitType.Guard => 1.42f,
        _ => 0.92f
    };

    public static float DamageScale(UnitType type) => type switch
    {
        UnitType.Veteran => 1.12f,
        UnitType.Guard => 1.28f,
        _ => 0.9f
    };

    public static string Label(UnitType type) => type switch
    {
        UnitType.Veteran => "VETERAN",
        UnitType.Guard => "GUARD",
        _ => "MILITIA"
    };
}

[System.Serializable]
public sealed class RosterEntry
{
    public UnitType Tier;
    public Archetype Archetype;
    public int Count;
}

// The warband as counts per (tier x archetype). Stored as a list so it stays
// JsonUtility-serializable for saves. Read-only tier views keep tier-only
// callers and tests working.
public sealed class UnitRoster
{
    public List<RosterEntry> Entries = new();

    public int Total
    {
        get
        {
            int total = 0;
            foreach (RosterEntry entry in Entries)
                total += entry.Count;
            return total;
        }
    }

    public int TierCount(UnitType tier)
    {
        int total = 0;
        foreach (RosterEntry entry in Entries)
            if (entry.Tier == tier)
                total += entry.Count;
        return total;
    }

    public int Militia => TierCount(UnitType.Militia);
    public int Veterans => TierCount(UnitType.Veteran);
    public int Guards => TierCount(UnitType.Guard);

    public int Count(UnitType tier, Archetype archetype)
    {
        foreach (RosterEntry entry in Entries)
            if (entry.Tier == tier && entry.Archetype == archetype)
                return entry.Count;
        return 0;
    }

    public void Add(UnitType tier, Archetype archetype, int count = 1)
    {
        if (count == 0)
            return;
        foreach (RosterEntry entry in Entries)
            if (entry.Tier == tier && entry.Archetype == archetype)
            {
                entry.Count = System.Math.Max(0, entry.Count + count);
                return;
            }
        if (count > 0)
            Entries.Add(new RosterEntry { Tier = tier, Archetype = archetype, Count = count });
    }

    public void Clear() => Entries.Clear();

    public UnitRoster Copy()
    {
        UnitRoster copy = new UnitRoster();
        foreach (RosterEntry entry in Entries)
            copy.Entries.Add(new RosterEntry { Tier = entry.Tier, Archetype = entry.Archetype, Count = entry.Count });
        return copy;
    }
}
