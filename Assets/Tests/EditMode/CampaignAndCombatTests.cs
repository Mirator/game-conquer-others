using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CampaignAndCombatTests
{
    [Test]
    public void DefaultCampaign_IsConnectedAndOffersAnAttack()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        HashSet<int> visited = new() { campaign.Territories[0].Id };
        Queue<int> frontier = new();
        frontier.Enqueue(campaign.Territories[0].Id);
        while (frontier.Count > 0)
        {
            foreach (int adjacent in campaign.GetById(frontier.Dequeue()).AdjacentIds)
                if (visited.Add(adjacent))
                    frontier.Enqueue(adjacent);
        }

        Assert.That(visited.Count, Is.EqualTo(campaign.Territories.Count));
        Assert.That(campaign.PlayerTerritoryCount(), Is.EqualTo(1));
        Assert.That(campaign.AttackableTargets(), Is.Not.Empty);
    }

    [Test]
    public void RecruitmentAndVictory_PersistTypedSurvivorsAndEconomy()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Territory target = FirstAttackable(campaign);
        int startingGold = campaign.Gold;

        Assert.That(campaign.Recruit(UnitType.Veteran), Is.True);
        Assert.That(campaign.Gold, Is.EqualTo(startingGold - UnitCatalog.Cost(UnitType.Veteran)));
        campaign.ApplyVictory(target, new BattleResult
        {
            PlayerWon = true,
            MilitiaSurvived = 2,
            VeteransSurvived = 1,
            GuardsSurvived = 0
        });

        Assert.That(target.Owner, Is.EqualTo(TerritoryOwner.Player));
        Assert.That(campaign.Units.Militia, Is.EqualTo(2));
        Assert.That(campaign.Units.Veterans, Is.EqualTo(1));
        Assert.That(campaign.Gold, Is.GreaterThan(startingGold - UnitCatalog.Cost(UnitType.Veteran)));
    }

    [Test]
    public void Recruit_FailsWithoutGoldOrWarbandSpace()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);

        campaign.Gold = UnitCatalog.Cost(UnitType.Guard) - 1;
        Assert.That(campaign.CanRecruit(UnitType.Guard), Is.False);
        Assert.That(campaign.Recruit(UnitType.Guard), Is.False, "Recruiting with too little gold should fail.");
        Assert.That(campaign.Gold, Is.EqualTo(UnitCatalog.Cost(UnitType.Guard) - 1), "A failed recruit must not spend gold.");

        campaign.Gold = 100000;
        int guard = 0;
        while (campaign.Recruit(UnitType.Militia) && guard++ < 100) { }
        Assert.That(campaign.Roster, Is.EqualTo(CampaignState.WarbandCap), "Recruiting should stop at the warband cap.");
        Assert.That(campaign.CanRecruit(UnitType.Militia), Is.False);
        Assert.That(campaign.Recruit(UnitType.Militia), Is.False, "Recruiting past the warband cap should fail.");
    }

    [Test]
    public void ThreatScaling_KeepsHomeSafeAndEnemiesWithinBounds()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        foreach (Territory t in campaign.Territories)
        {
            Assert.That(t.Threat, Is.InRange(1, 5), $"{t.Name} threat must stay within [1,5].");
            if (t.Owner == TerritoryOwner.Player)
            {
                Assert.That(t.Threat, Is.EqualTo(1), "The home territory should be the safest.");
            }
            else
            {
                Assert.That(t.Garrison, Is.InRange(2, 10), $"{t.Name} garrison must stay within [2,10].");
                Assert.That(t.DifficultyScale, Is.GreaterThanOrEqualTo(1f), $"{t.Name} should never be easier than baseline.");
                Assert.That(t.RewardGold, Is.GreaterThan(0), $"{t.Name} should award conquest gold.");
            }
        }
    }

    [Test]
    public void Victory_GrowsIncomeAndPaysRewardPlusIncome()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Territory target = FirstAttackable(campaign);
        int incomeBefore = campaign.IncomePerVictory();
        int goldBefore = campaign.Gold;
        int reward = target.RewardGold;

        campaign.ApplyVictory(target, new BattleResult { PlayerWon = true });

        Assert.That(campaign.IncomePerVictory(), Is.GreaterThan(incomeBefore), "Capturing a territory should grow income.");
        Assert.That(campaign.Gold, Is.EqualTo(goldBefore + reward + campaign.IncomePerVictory()),
            "Victory should pay the conquest reward plus the new total income.");
    }

    [Test]
    public void CampaignSave_RoundTripsProgress()
    {
        CampaignSaveService.Delete();
        CampaignState original = CampaignState.CreateDefault(11);
        Territory target = FirstAttackable(original);
        int targetId = target.Id;
        original.Recruit(UnitType.Veteran);
        original.PlayerWeapon = WeaponType.Bow;
        original.ApplyVictory(target, new BattleResult { PlayerWon = true, VeteransSurvived = 1 });

        CampaignSaveService.Save(original);
        Assert.That(CampaignSaveService.HasSave, Is.True);
        CampaignState loaded = CampaignSaveService.Load();
        CampaignSaveService.Delete();

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.Seed, Is.EqualTo(original.Seed));
        Assert.That(loaded.Gold, Is.EqualTo(original.Gold));
        Assert.That(loaded.PlayerWeapon, Is.EqualTo(WeaponType.Bow));
        Assert.That(loaded.Units.Veterans, Is.EqualTo(original.Units.Veterans));
        Assert.That(loaded.Territories.Count, Is.EqualTo(original.Territories.Count));
        Assert.That(loaded.GetById(targetId).Owner, Is.EqualTo(TerritoryOwner.Player));
        Assert.That(loaded.PlayerTerritoryCount(), Is.EqualTo(original.PlayerTerritoryCount()));
        Assert.That(loaded.AttackableTargets(), Is.Not.Empty, "Adjacency must survive the round trip.");
    }

    [Test]
    public void Defeat_EndsCampaign()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Assert.That(campaign.CampaignOver, Is.False);
        campaign.ApplyDefeat();
        Assert.That(campaign.CampaignOver, Is.True);
    }

    [TestCase(UnitType.Militia, 35)]
    [TestCase(UnitType.Veteran, 70)]
    [TestCase(UnitType.Guard, 110)]
    public void UnitCatalog_HasExpectedCosts(UnitType type, int cost)
    {
        Assert.That(UnitCatalog.Cost(type), Is.EqualTo(cost));
    }

    [TestCase(UnitType.Militia, WeaponType.SwordAndShield)]
    [TestCase(UnitType.Veteran, WeaponType.TwoHandedSword)]
    [TestCase(UnitType.Guard, WeaponType.Bow)]
    public void WeaponCatalog_MapsDefaultWeaponPerUnit(UnitType type, WeaponType weapon)
    {
        Assert.That(WeaponCatalog.DefaultFor(type), Is.EqualTo(weapon));
    }

    [Test]
    public void WeaponCatalog_NextAndPreviousCycleAndInvert()
    {
        foreach (WeaponType weapon in System.Enum.GetValues(typeof(WeaponType)))
        {
            Assert.That(WeaponCatalog.Previous(WeaponCatalog.Next(weapon)), Is.EqualTo(weapon),
                "Previous should undo Next.");
            Assert.That(WeaponCatalog.Next(weapon), Is.Not.EqualTo(weapon), "Next should always advance.");
        }
    }

    [TestCase(12f, 0f, CombatDirection.Right)]
    [TestCase(-12f, 0f, CombatDirection.Left)]
    [TestCase(0f, 12f, CombatDirection.Up)]
    [TestCase(0f, -12f, CombatDirection.Thrust)]
    public void Gesture_ResolvesCardinalDirections(float x, float y, CombatDirection expected)
    {
        Assert.That(CombatGesture.TryResolve(new Vector2(x, y), out CombatDirection direction), Is.True);
        Assert.That(direction, Is.EqualTo(expected));
    }

    [Test]
    public void Gesture_RejectsJitterAndDiagonalAmbiguity()
    {
        Assert.That(CombatGesture.TryResolve(new Vector2(4f, 3f), out _), Is.False);
        Assert.That(CombatGesture.TryResolve(new Vector2(10f, 9f), out _), Is.False);
    }

    private static Territory FirstAttackable(CampaignState campaign)
    {
        foreach (Territory territory in campaign.AttackableTargets())
            return territory;
        return null;
    }
}
