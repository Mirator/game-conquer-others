using NUnit.Framework;
using UnityEngine;

// Pure presentation/rules helpers for the campaign map HUD, extracted from
// CampaignMapController so the recruit-gate reasons and display strings can be
// verified without the map's GameObject lifecycle.
public sealed class CampaignMapViewTests
{
    [Test]
    public void WorldOf_MapsPositionToTableSpace()
    {
        Vector3 w = CampaignMapView.WorldOf(new Vector2(2f, 3f));
        Assert.AreEqual(2.8f, w.x, 1e-4f);
        Assert.AreEqual(0.2f, w.y, 1e-4f);
        Assert.AreEqual(4.2f, w.z, 1e-4f);
    }

    [Test]
    public void WorldOf_TerritoryMatchesItsMapPosition()
    {
        Territory t = new Territory { MapPosition = new Vector2(-5f, 1.5f) };
        Assert.AreEqual(CampaignMapView.WorldOf(t.MapPosition), CampaignMapView.WorldOf(t));
    }

    [TestCase(0, "☆☆☆☆☆")]
    [TestCase(3, "★★★☆☆")]
    [TestCase(5, "★★★★★")]
    [TestCase(7, "★★★★★")]  // clamps above 5
    [TestCase(-2, "☆☆☆☆☆")] // clamps below 0
    public void Stars_RendersClampedGauge(int threat, string expected)
    {
        Assert.AreEqual(expected, CampaignMapView.Stars(threat));
    }

    [TestCase(0, "SAME DAY")]
    [TestCase(1, "1d")]
    [TestCase(4, "4d")]
    public void TravelTimeLabel_FormatsDays(int days, string expected)
    {
        Assert.AreEqual(expected, CampaignMapView.TravelTimeLabel(days));
    }

    [Test]
    public void RecruitBlockReason_BlocksWhileTravelling()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        Territory castle = new Territory { Settlement = SettlementType.Castle, Recruits = 5 };
        Assert.AreEqual("On the march - halt to recruit.",
            CampaignMapView.RecruitBlockReason(true, castle, UnitType.Militia, campaign));
    }

    [Test]
    public void RecruitBlockReason_BlocksWithNoSettlementInRange()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        Assert.AreEqual("No settlement in range.",
            CampaignMapView.RecruitBlockReason(false, null, UnitType.Militia, campaign));
    }

    [Test]
    public void RecruitBlockReason_BlocksTierAboveSettlementCeiling()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        Territory village = new Territory { Settlement = SettlementType.Village, Recruits = 3 };
        // A village tops out at Militia, so Guard is refused with the ceiling message.
        string reason = CampaignMapView.RecruitBlockReason(false, village, UnitType.Guard, campaign);
        Assert.AreEqual("VILLAGE offers up to MILITIA.", reason);
    }

    [Test]
    public void RecruitBlockReason_BlocksWhenNoVolunteers()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        Territory castle = new Territory { Settlement = SettlementType.Castle, Recruits = 0 };
        Assert.AreEqual("No volunteers left here.",
            CampaignMapView.RecruitBlockReason(false, castle, UnitType.Militia, campaign));
    }

    [Test]
    public void RecruitBlockReason_BlocksWhenGoldShort()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        campaign.Gold = 0;
        Territory castle = new Territory { Settlement = SettlementType.Castle, Recruits = 5 };
        string reason = CampaignMapView.RecruitBlockReason(false, castle, UnitType.Militia, campaign);
        Assert.AreEqual($"Need {UnitCatalog.Cost(UnitType.Militia)} gold.", reason);
    }

    [Test]
    public void RecruitBlockReason_AllowsWhenEverythingIsSatisfied()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        campaign.Gold = 9999;
        Territory castle = new Territory { Settlement = SettlementType.Castle, Recruits = 5 };
        // Fresh warband is below the leadership cap, settlement allows Militia, gold is
        // ample, volunteers remain -> no block reason.
        Assert.IsNull(CampaignMapView.RecruitBlockReason(false, castle, UnitType.Militia, campaign));
    }
}
