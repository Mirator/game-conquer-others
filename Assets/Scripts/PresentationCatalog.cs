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
