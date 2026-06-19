using UnityEngine;

// Serialized formation tuning. Defaults below are the authoritative values; create
// an instance in a Resources folder (Conquer Others > Create Formation Balance
// Asset, or Assets > Create > Conquer Others > Formation Balance named
// "FormationBalance") to override them and tune spacing/speeds live in the
// inspector during play. Line defaults intentionally match the original Follow
// grid so existing battles read unchanged.
[CreateAssetMenu(menuName = "Conquer Others/Formation Balance", fileName = "FormationBalance")]
public sealed class FormationBalanceData : ScriptableObject
{
    public const string ResourceName = "FormationBalance";

    [Header("Line (default — wide, one protective rank ahead)")]
    public int lineWidth = 6;
    public float lineSideSpacing = 1.65f;
    public float lineRankDepth = 1.7f;
    public float lineFrontDepth = 1.5f;
    public float lineSpeedScale = 1f;

    [Header("Shield Wall (tighter, deeper, slower)")]
    public int shieldWallWidth = 4;
    public float shieldWallSideSpacing = 1.15f;
    public float shieldWallRankDepth = 1.35f;
    public float shieldWallFrontDepth = 1.3f;
    public float shieldWallSpeedScale = 0.7f;

    [Header("Skirmish (loose, spread, mobile)")]
    public int skirmishWidth = 5;
    public float skirmishSideSpacing = 2.6f;
    public float skirmishRankDepth = 2.4f;
    public float skirmishFrontDepth = 1.5f;
    public float skirmishSpeedScale = 1.1f;
    // Per-slot deterministic position scatter (metres) applied only to Skirmish.
    public float skirmishJitter = 0.6f;

    [Header("Advance (formation marches forward)")]
    // How far ahead of the captain the marching anchor starts when Advance is given.
    public float advanceStep = 5f;
    // How fast the anchor (and the whole line) creeps forward each second.
    public float advanceSpeed = 1.6f;
}
