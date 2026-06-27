using UnityEngine;

// Pure presentation/rules helpers for the campaign map HUD, split out of
// CampaignMapController so they can be unit-tested without the map's GameObject
// lifecycle: map-position -> table world placement, threat/eta display strings, and
// the recruit-gate reason shown when a tier can't be recruited at a settlement.
public static class CampaignMapView
{
    // Map graph coordinates -> table world position (the map lies flat on a table at
    // y ~ 0.2, scaled 1.4x).
    public static Vector3 WorldOf(Vector2 mapPosition) => new Vector3(mapPosition.x * 1.4f, 0.2f, mapPosition.y * 1.4f);
    public static Vector3 WorldOf(Territory t) => WorldOf(t.MapPosition);

    // Threat as a five-pip star gauge (filled vs hollow), clamped to 0..5.
    public static string Stars(int threat)
    {
        int filled = Mathf.Clamp(threat, 0, 5);
        return new string('★', filled) + new string('☆', 5 - filled);
    }

    public static string TravelTimeLabel(int days) => days == 0 ? "SAME DAY" : $"{days}d";

    // The reason the given tier can't be recruited at the in-range settlement, or null
    // if recruiting is allowed. Mirrors the tier-level gates the HUD checks before a
    // recruit (settlement reach, settlement tier ceiling, volunteers, leadership cap,
    // gold); archetype-level checks stay in CampaignState.CanRecruit.
    public static string RecruitBlockReason(bool travelling, Territory settlement, UnitType tier, CampaignState campaign)
    {
        if (travelling)
            return "On the march - halt to recruit.";
        if (settlement == null)
            return "No settlement in range.";
        if (!SettlementCatalog.Allows(settlement.Settlement, tier))
            return $"{SettlementCatalog.Label(settlement.Settlement)} offers up to {UnitCatalog.Label(SettlementCatalog.MaxTier(settlement.Settlement))}.";
        if (settlement.Recruits <= 0)
            return "No volunteers left here.";
        if (campaign.Roster >= campaign.LeadershipCap)
            return $"Warband full ({campaign.LeadershipCap}) - raise renown.";
        if (campaign.Gold < UnitCatalog.Cost(tier))
            return $"Need {UnitCatalog.Cost(tier)} gold.";
        return null;
    }
}
