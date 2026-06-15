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
