using System.Collections.Generic;

// One spawned fighter: a stat tier crossed with a combat archetype, plus the
// weapon it carries. Weapon is explicit so allied/fallback rosters can keep
// their tier-default weapon while archetype-composed rosters use the
// archetype's weapon.
public sealed class UnitSpec
{
    public UnitType Tier;
    public Archetype Archetype;
    public WeaponType Weapon;

    public UnitSpec(UnitType tier, Archetype archetype, WeaponType weapon)
    {
        Tier = tier;
        Archetype = archetype;
        Weapon = weapon;
    }
}

// Parameters that describe a single battle encounter and the result it produces.
// Plain data classes (not MonoBehaviours) so they can be passed between the
// campaign layer and the battle builder without GameObject lifecycle concerns.
public sealed class BattleSetup
{
    // The most fighters either side can field on the battlefield. The spawner lays
    // soldiers out in dynamic rows, so this is the per-side deployment ceiling for
    // big commanded battles. It is intentionally larger than the player's campaign
    // leadership cap (CampaignState.MaxLeadership): garrisons and bandit hordes can
    // outnumber the warband.
    public const int MaxDeployed = 60;

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

    // Encounter context for dressing the battlefield to the overworld.
    public BattleKind Kind = BattleKind.Training;
    public float TimeOfDay = 0.5f;   // 0=midnight, .25 dawn, .5 midday, .75 dusk

    // Explicit archetype composition. When set it overrides the tier-count
    // fields above; when null the builder falls back to those counts.
    public List<UnitSpec> EnemyComposition;
    public List<UnitSpec> AllyComposition;

    public static BattleSetup Default() => new BattleSetup();
}

public sealed class BattleResult
{
    public bool PlayerWon;
    public int MilitiaSurvived;
    public int VeteransSurvived;
    public int GuardsSurvived;

    // Allied survivors by tier x archetype. Authoritative when set; the tier
    // counts above remain as a fallback for tier-only callers.
    public List<RosterEntry> SurvivingUnits;
}
