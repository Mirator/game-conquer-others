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
