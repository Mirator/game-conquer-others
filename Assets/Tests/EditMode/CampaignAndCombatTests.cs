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
