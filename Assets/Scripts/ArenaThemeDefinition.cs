using UnityEngine;

[CreateAssetMenu(menuName = "Conquer Others/Arena Theme", fileName = "ArenaTheme")]
public sealed class ArenaThemeDefinition : ScriptableObject
{
    public ArenaType arena;
    public GameObject visualPrefab;
    public Color sunlight = new(1f, 0.8f, 0.6f);
    public Color ambient = new(0.25f, 0.28f, 0.3f);
    public Color fog = new(0.45f, 0.5f, 0.52f);
    [Range(0f, 0.05f)] public float fogDensity = 0.012f;
    public AudioClip ambience;
    public AudioClip music;
}
