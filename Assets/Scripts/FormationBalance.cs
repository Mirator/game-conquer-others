using UnityEngine;

// Central formation tuning access, mirroring CombatBalance. Values come from a
// FormationBalanceData asset in Resources when present (live in-editor tuning) and
// otherwise fall back to the baked defaults. Per-shape getters keep Formation's
// slot math readable.
public static class FormationBalance
{
    private static FormationBalanceData active;

    public static FormationBalanceData Active => active != null
        ? active
        : (active = Resources.Load<FormationBalanceData>(FormationBalanceData.ResourceName)
            ?? ScriptableObject.CreateInstance<FormationBalanceData>());

    // Lets tests or tuning tools swap (or, with null, reset) the active set.
    public static void Override(FormationBalanceData data) => active = data;

    public static int Width(FormationShape shape) => shape switch
    {
        FormationShape.ShieldWall => Active.shieldWallWidth,
        FormationShape.Skirmish => Active.skirmishWidth,
        _ => Active.lineWidth
    };

    public static float SideSpacing(FormationShape shape) => shape switch
    {
        FormationShape.ShieldWall => Active.shieldWallSideSpacing,
        FormationShape.Skirmish => Active.skirmishSideSpacing,
        _ => Active.lineSideSpacing
    };

    public static float RankDepth(FormationShape shape) => shape switch
    {
        FormationShape.ShieldWall => Active.shieldWallRankDepth,
        FormationShape.Skirmish => Active.skirmishRankDepth,
        _ => Active.lineRankDepth
    };

    public static float FrontDepth(FormationShape shape) => shape switch
    {
        FormationShape.ShieldWall => Active.shieldWallFrontDepth,
        FormationShape.Skirmish => Active.skirmishFrontDepth,
        _ => Active.lineFrontDepth
    };

    public static float SpeedScale(FormationShape shape) => shape switch
    {
        FormationShape.ShieldWall => Active.shieldWallSpeedScale,
        FormationShape.Skirmish => Active.skirmishSpeedScale,
        _ => Active.lineSpeedScale
    };

    public static float SkirmishJitter => Active.skirmishJitter;
    public static float AdvanceStep => Active.advanceStep;
    public static float AdvanceSpeed => Active.advanceSpeed;
}
