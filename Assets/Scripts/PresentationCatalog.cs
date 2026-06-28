using UnityEngine;

[CreateAssetMenu(menuName = "Conquer Others/Presentation Catalog", fileName = "PresentationCatalog")]
public sealed class PresentationCatalog : ScriptableObject
{
    public const string ResourceName = "PresentationCatalog";

    [Header("Fighters")]
    public GameObject captainPrefab;
    public GameObject militiaPrefab;
    public GameObject veteranPrefab;
    public GameObject guardPrefab;
    public GameObject enemyPrefab;
    public GameObject swordPrefab;
    public GameObject shieldPrefab;
    public GameObject bowPrefab;
    public GameObject arrowPrefab;

    [Header("Buildings")]
    // Authored settlement structures for the campaign diorama; MapDioramaBuilder
    // falls back to primitive blocks when a slot is null.
    public GameObject houseSmall;
    public GameObject houseLarge;
    public GameObject townHall;
    public GameObject castleKeep;
    public GameObject castleTower;

    [Header("Arenas")]
    public ArenaThemeDefinition courtyard;
    public ArenaThemeDefinition forest;
    public ArenaThemeDefinition marsh;
    public ArenaThemeDefinition highlands;
    public GameObject villageWall;
    public GameObject villageArch;
    public GameObject villageWagon;
    public GameObject villageCrate;
    public GameObject villageFence;
    public GameObject villageTowerRoof;
    public GameObject banner;
    public GameObject barrel;
    public GameObject propCrate;
    public GameObject weaponStand;
    public GameObject commonTree;
    public GameObject pineTree;
    public GameObject deadTree;
    public GameObject rock;
    public GameObject bush;

    [Header("Scatter")]
    // Variant pools so biomes read as natural stands rather than cloned props; the
    // Random* accessors fall back to the single-model fields when a pool is empty.
    public GameObject[] treeVariants;
    public GameObject[] pineVariants;
    public GameObject[] deadTreeVariants;
    public GameObject[] rockVariants;
    public GameObject[] groundClutter; // lush detail: flowers, mushrooms, pebbles, bushes
    public GameObject[] barrenClutter; // rocky/highland detail: pebbles, mushrooms (no flowers)
    public GameObject[] tallGrass;      // plane-based grass/fern, used sparsely for marsh reeds only

    [Header("Camp")]
    public GameObject campfire;
    public GameObject tent;
    public GameObject bedroll;
    public GameObject campFence;

    [Header("Frontend")]
    public Sprite panelBorder;
    public Sprite buttonBorder;

    [Header("Audio")]
    public AudioClip mapMusic;
    public AudioClip battleMusic;
    public AudioClip victoryMusic;
    public AudioClip defeatMusic;
    public AudioClip[] swings;
    public AudioClip[] impacts;
    public AudioClip[] blocks;
    public AudioClip[] arrows;
    public AudioClip[] footsteps;
    public AudioClip[] ui;

    public static PresentationCatalog Load() => Resources.Load<PresentationCatalog>(ResourceName);

    public GameObject RandomTree() => Pick(treeVariants) ?? commonTree;
    public GameObject RandomPine() => Pick(pineVariants) ?? pineTree;
    public GameObject RandomDeadTree() => Pick(deadTreeVariants) ?? deadTree;
    public GameObject RandomRock() => Pick(rockVariants) ?? rock;
    public GameObject RandomClutter() => Pick(groundClutter);
    public GameObject RandomBarrenClutter() => Pick(barrenClutter);
    public GameObject RandomTallGrass() => Pick(tallGrass);
    // Houses come in two silhouettes; callers pick by settlement size and fall back
    // to whichever single slot is populated.
    public GameObject House(bool large) => (large ? houseLarge : houseSmall) ?? houseSmall ?? houseLarge;

    private static GameObject Pick(GameObject[] pool)
        => pool != null && pool.Length > 0 ? pool[Random.Range(0, pool.Length)] : null;

    public ArenaThemeDefinition Theme(ArenaType arena) => arena switch
    {
        ArenaType.Forest => forest,
        ArenaType.Marsh => marsh,
        ArenaType.Highlands => highlands,
        _ => courtyard
    };

    public GameObject Fighter(UnitType unit, Team team, bool captain)
    {
        if (captain && captainPrefab != null)
            return captainPrefab;
        return unit switch
        {
            UnitType.Veteran => veteranPrefab != null ? veteranPrefab : captainPrefab != null ? captainPrefab : militiaPrefab,
            UnitType.Guard => guardPrefab != null ? guardPrefab : captainPrefab != null ? captainPrefab : militiaPrefab,
            _ => team == Team.Enemies && enemyPrefab != null ? enemyPrefab : militiaPrefab
        };
    }
}
