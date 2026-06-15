// Parameters that describe a single battle encounter and the result it produces.
// Plain data classes (not MonoBehaviours) so they can be passed between the
// campaign layer and the battle builder without GameObject lifecycle concerns.
public sealed class BattleSetup
{
    public int AllyCount = 3;       // allied AI soldiers spawned alongside the player
    public int EnemyCount = 4;      // enemy soldiers (the target territory's garrison)
    public int AllyMilitia;
    public int AllyVeterans;
    public int AllyGuards;
    public int EnemyVeterans;
    public int EnemyGuards;
    public float EnemyHealthScale = 1f;
    public string TargetName = "THE OLD COURTYARD";
    public ArenaType Arena = ArenaType.Courtyard;
    public WeaponType PlayerWeapon = WeaponType.SwordAndShield;
    public WeaponType TrainingEnemyWeapon = WeaponType.SwordAndShield;
    public bool IsTraining;

    public static BattleSetup Default() => new BattleSetup();
}

public sealed class BattleResult
{
    public bool PlayerWon;
    public int AlliesSurvived;
    public int MilitiaSurvived;
    public int VeteransSurvived;
    public int GuardsSurvived;
}
