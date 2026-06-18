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

public sealed class UnitRoster
{
    public int Militia;
    public int Veterans;
    public int Guards;

    public int Total => Militia + Veterans + Guards;

    public int Get(UnitType type) => type switch
    {
        UnitType.Veteran => Veterans,
        UnitType.Guard => Guards,
        _ => Militia
    };

    public void Add(UnitType type, int count = 1)
    {
        if (type == UnitType.Veteran)
            Veterans += count;
        else if (type == UnitType.Guard)
            Guards += count;
        else
            Militia += count;
    }

    public UnitRoster Copy() => new UnitRoster
    {
        Militia = Militia,
        Veterans = Veterans,
        Guards = Guards
    };
}
