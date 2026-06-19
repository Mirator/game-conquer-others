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

// A settlement's size class. It sets how many volunteers the settlement offers
// and the highest unit tier you can raise there, so villages yield raw militia
// while castles can field guards.
public enum SettlementType
{
    Village,
    Town,
    Castle
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

    // Gold paid per fighter of this tier each campaign day. Together with daily
    // owned-land income this is the warband's running cashflow.
    public static int Upkeep(UnitType type) => type switch
    {
        UnitType.Veteran => 4,
        UnitType.Guard => 6,
        _ => 2
    };

    // The next tier a fighter promotes into (Guard is terminal).
    public static UnitType NextTier(UnitType type) => type switch
    {
        UnitType.Militia => UnitType.Veteran,
        UnitType.Veteran => UnitType.Guard,
        _ => UnitType.Guard
    };

    public static bool CanUpgrade(UnitType type) => type != UnitType.Guard;

    // Battle experience a fighter must bank before it can be promoted.
    public static int UpgradeXp(UnitType type) => type switch
    {
        UnitType.Militia => 100,
        UnitType.Veteran => 200,
        _ => int.MaxValue
    };

    // Gold the promotion costs on top of the banked experience.
    public static int UpgradeCost(UnitType type) => type switch
    {
        UnitType.Militia => 25,
        UnitType.Veteran => 50,
        _ => 0
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

// Maps a settlement's size class to its recruitment offer: how many volunteers
// wait there and the highest tier they come in. Owners do not change these — any
// settlement in range can be recruited from, the type just sets the ceiling.
public static class SettlementCatalog
{
    public static int MaxRecruits(SettlementType type) => type switch
    {
        SettlementType.Castle => 6,
        SettlementType.Town => 5,
        _ => 3
    };

    public static UnitType MaxTier(SettlementType type) => type switch
    {
        SettlementType.Castle => UnitType.Guard,
        SettlementType.Town => UnitType.Veteran,
        _ => UnitType.Militia
    };

    public static bool Allows(SettlementType type, UnitType tier) => (int)tier <= (int)MaxTier(type);

    public static string Label(SettlementType type) => type switch
    {
        SettlementType.Castle => "CASTLE",
        SettlementType.Town => "TOWN",
        _ => "VILLAGE"
    };
}

[System.Serializable]
public sealed class RosterEntry
{
    public UnitType Tier;
    public Archetype Archetype;
    public int Count;
    public int Xp;        // battle experience banked by this (tier x archetype) stack
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

    public int Xp(UnitType tier, Archetype archetype)
    {
        foreach (RosterEntry entry in Entries)
            if (entry.Tier == tier && entry.Archetype == archetype)
                return entry.Xp;
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

    // Banks experience onto an existing (tier x archetype) stack. No-op if the
    // stack is absent, so XP only accrues to units actually in the roster.
    public void AddXp(UnitType tier, Archetype archetype, int amount)
    {
        if (amount == 0)
            return;
        foreach (RosterEntry entry in Entries)
            if (entry.Tier == tier && entry.Archetype == archetype)
            {
                entry.Xp = System.Math.Max(0, entry.Xp + amount);
                return;
            }
    }

    // Promotes one fighter of a stack to the next tier, keeping its archetype and
    // spending the tier's upgrade XP from the stack's pool. Returns false if the
    // stack is empty, already top tier, or has not banked enough experience. Gold
    // is handled by the caller (CampaignState.TryUpgrade).
    public bool Upgrade(UnitType tier, Archetype archetype)
    {
        if (!UnitCatalog.CanUpgrade(tier))
            return false;
        foreach (RosterEntry entry in Entries)
            if (entry.Tier == tier && entry.Archetype == archetype)
            {
                int need = UnitCatalog.UpgradeXp(tier);
                if (entry.Count <= 0 || entry.Xp < need)
                    return false;
                entry.Count--;
                entry.Xp -= need;
                Add(UnitCatalog.NextTier(tier), archetype, 1);
                return true;
            }
        return false;
    }

    public void Clear() => Entries.Clear();

    public UnitRoster Copy()
    {
        UnitRoster copy = new UnitRoster();
        foreach (RosterEntry entry in Entries)
            copy.Entries.Add(new RosterEntry
            {
                Tier = entry.Tier,
                Archetype = entry.Archetype,
                Count = entry.Count,
                Xp = entry.Xp
            });
        return copy;
    }
}
