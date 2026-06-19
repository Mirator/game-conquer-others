using NUnit.Framework;
using UnityEngine;

public sealed class OverworldSimulationTests
{
    private static CampaignState StateWithRoster(int militia)
    {
        CampaignState state = new CampaignState();
        state.Units.Add(UnitType.Militia, Archetype.Soldier, militia);
        return state;
    }

    private static EnemyParty Band(Vector2 position, int strength)
        => new EnemyParty { Position = position, Strength = strength, Name = "BANDITS", Arena = ArenaType.Courtyard };

    [Test]
    public void IsThreat_UsesSixtyPercentOfPlayerStrength()
    {
        // Roster 3 -> PlayerStrength 4 -> ceil(4 * 0.6) = ceil(2.4) = 3.
        OverworldSimulation sim = new OverworldSimulation(StateWithRoster(3));
        Assert.That(sim.PlayerStrength, Is.EqualTo(4));
        Assert.That(sim.IsThreat(Band(Vector2.zero, 3)), Is.True, "Strength at the threshold threatens.");
        Assert.That(sim.IsThreat(Band(Vector2.zero, 2)), Is.False, "A much weaker band does not.");
    }

    [Test]
    public void Tick_AccruesDaysByDistanceTravelled()
    {
        CampaignState state = StateWithRoster(3);
        state.Day = 1;
        OverworldSimulation sim = new OverworldSimulation(state);
        sim.BeginTravel(new Vector2(100f, 0f), null, null);

        // Move exactly 10 units in one tick. 10 / DistancePerDay(4) = 2 full days.
        sim.Tick(10f / OverworldSimulation.TravelSpeed);

        Assert.That(state.PartyPosition.x, Is.EqualTo(10f).Within(0.001f));
        Assert.That(state.Day, Is.EqualTo(3), "Two days elapse, remainder carries.");
    }

    [Test]
    public void Tick_WeakBandIsMarchedPastWithoutBattle()
    {
        CampaignState state = StateWithRoster(8); // strong host; strength-2 band is no threat
        EnemyParty weak = Band(new Vector2(1f, 0f), 2);
        state.Parties.Add(weak);
        OverworldSimulation sim = new OverworldSimulation(state);
        sim.BeginTravel(new Vector2(20f, 0f), null, null);

        OverworldOutcome outcome = sim.Tick(0.1f);
        Assert.That(outcome.Kind, Is.EqualTo(OverworldOutcomeKind.None), "A weak band is passed harmlessly.");
        Assert.That(weak.Position, Is.EqualTo(new Vector2(1f, 0f)), "A non-threat holds position.");
    }

    [Test]
    public void Tick_ThreateningBandStartsFieldBattle()
    {
        CampaignState state = StateWithRoster(2); // PlayerStrength 3 -> threat >= ceil(1.8) = 2
        EnemyParty strong = Band(new Vector2(0.5f, 0f), 3);
        state.Parties.Add(strong);
        OverworldSimulation sim = new OverworldSimulation(state);
        sim.BeginTravel(new Vector2(20f, 0f), null, null);

        OverworldOutcome outcome = sim.Tick(0.05f);
        Assert.That(outcome.Kind, Is.EqualTo(OverworldOutcomeKind.FieldBattle));
        Assert.That(outcome.Party, Is.SameAs(strong));
        Assert.That(sim.Travelling, Is.False, "An encounter ends travel.");
    }

    [Test]
    public void Tick_HuntedWeakBandStillStartsFieldBattle()
    {
        CampaignState state = StateWithRoster(10); // weak band is not a threat...
        EnemyParty weak = Band(new Vector2(0.5f, 0f), 2);
        state.Parties.Add(weak);
        OverworldSimulation sim = new OverworldSimulation(state);
        sim.BeginTravel(weak.Position, null, weak); // ...but the player is hunting it

        OverworldOutcome outcome = sim.Tick(0.05f);
        Assert.That(outcome.Kind, Is.EqualTo(OverworldOutcomeKind.FieldBattle));
        Assert.That(outcome.Party, Is.SameAs(weak));
    }

    [Test]
    public void Tick_ResolvesArrivalByHoldOwnership()
    {
        foreach (TerritoryOwner owner in new[] { TerritoryOwner.Enemy, TerritoryOwner.Player })
        {
            CampaignState state = StateWithRoster(3);
            Territory hold = new Territory { Id = 0, Name = "Greyhold", MapPosition = new Vector2(0.3f, 0f), Owner = owner };
            state.Territories.Add(hold);
            OverworldSimulation sim = new OverworldSimulation(state);
            sim.BeginTravel(hold.MapPosition, hold, null);

            OverworldOutcome outcome = sim.Tick(1f); // large step -> arrive this tick
            OverworldOutcomeKind expected = owner == TerritoryOwner.Enemy
                ? OverworldOutcomeKind.ArriveEnemy
                : OverworldOutcomeKind.RestAtHold;
            Assert.That(outcome.Kind, Is.EqualTo(expected));
            Assert.That(outcome.Territory, Is.SameAs(hold));
        }
    }

    [Test]
    public void StepEnemyParties_OnlyThreatsInSightChase()
    {
        CampaignState state = StateWithRoster(2); // threat threshold = ceil(3 * 0.6) = 2
        EnemyParty threat = Band(new Vector2(5f, 0f), 3);
        EnemyParty weak = Band(new Vector2(-5f, 0f), 1);
        EnemyParty far = Band(new Vector2(20f, 0f), 3);
        state.Parties.Add(threat);
        state.Parties.Add(weak);
        state.Parties.Add(far);
        OverworldSimulation sim = new OverworldSimulation(state);

        sim.StepEnemyParties(2f); // chase = 2 * 0.55 = 1.1
        Assert.That(threat.Position.x, Is.EqualTo(3.9f).Within(0.001f), "An in-sight threat closes in.");
        Assert.That(weak.Position.x, Is.EqualTo(-5f).Within(0.001f), "A weak band holds.");
        Assert.That(far.Position.x, Is.EqualTo(20f).Within(0.001f), "Beyond sight, a threat holds.");
    }

    [Test]
    public void AtFriendlyCity_TrueOnlyForOwnedHoldInRange()
    {
        CampaignState state = StateWithRoster(3);
        state.Territories.Add(new Territory { Name = "Foe", MapPosition = new Vector2(0.5f, 0f), Owner = TerritoryOwner.Enemy });
        OverworldSimulation sim = new OverworldSimulation(state);
        Assert.That(sim.AtFriendlyCity(), Is.False, "An enemy hold in range does not count.");

        state.Territories.Add(new Territory { Name = "Home", MapPosition = new Vector2(2f, 0f), Owner = TerritoryOwner.Player });
        Assert.That(sim.AtFriendlyCity(), Is.True, "An owned hold within range counts.");

        state.PartyPosition = new Vector2(10f, 0f);
        Assert.That(sim.AtFriendlyCity(), Is.False, "Out of range, recruiting is unavailable.");
    }

    [Test]
    public void RecruitSettlement_ReturnsNearestInRangeRegardlessOfOwner()
    {
        CampaignState state = StateWithRoster(3);
        Territory enemyVillage = new Territory
        {
            Name = "Foe", MapPosition = new Vector2(1f, 0f), Owner = TerritoryOwner.Enemy,
            Settlement = SettlementType.Village, Recruits = 3
        };
        state.Territories.Add(enemyVillage);
        OverworldSimulation sim = new OverworldSimulation(state);

        Assert.That(sim.RecruitSettlement(), Is.SameAs(enemyVillage),
            "Any settlement in range offers volunteers, even one the player does not own.");

        state.PartyPosition = new Vector2(10f, 0f);
        Assert.That(sim.RecruitSettlement(), Is.Null, "Out of range, no settlement is available.");
    }

    [Test]
    public void Tick_DayElapsedDrawsWages()
    {
        CampaignState state = StateWithRoster(3); // wage = 3 * 2 = 6 per day
        state.Gold = 100;
        state.Day = 1;
        OverworldSimulation sim = new OverworldSimulation(state);
        sim.BeginTravel(new Vector2(100f, 0f), null, null);

        // Travel 8 units in one tick -> 2 full days elapse.
        sim.Tick(8f / OverworldSimulation.TravelSpeed);

        Assert.That(state.Day, Is.EqualTo(3));
        Assert.That(state.Gold, Is.EqualTo(100 - 6 * 2), "Each day on the march draws the wage bill.");
    }

    [Test]
    public void DaysTo_CountsWholeDaysAtDistancePerDay()
    {
        CampaignState state = StateWithRoster(3);
        state.PartyPosition = Vector2.zero;
        OverworldSimulation sim = new OverworldSimulation(state);

        // DistancePerDay is 4: a 10-unit march rounds up to 3 days.
        Assert.That(sim.DaysTo(new Vector2(10f, 0f)), Is.EqualTo(3));
        Assert.That(sim.DaysTo(new Vector2(4f, 0f)), Is.EqualTo(1));
        Assert.That(sim.DaysTo(Vector2.zero), Is.EqualTo(0), "Standing still costs no day.");
        Assert.That(sim.DaysTo(new Vector2(0.005f, 0f)), Is.EqualTo(0), "A negligible hop costs no day.");
    }

    [Test]
    public void WaitOneDay_AdvancesDayAndCatchesAdjacentThreat()
    {
        CampaignState state = StateWithRoster(2);
        state.Day = 1;
        state.Parties.Add(Band(new Vector2(1f, 0f), 3)); // within EncounterRadius and a threat
        OverworldSimulation sim = new OverworldSimulation(state);

        OverworldOutcome outcome = sim.WaitOneDay();
        Assert.That(state.Day, Is.EqualTo(2));
        Assert.That(outcome.Kind, Is.EqualTo(OverworldOutcomeKind.FieldBattle));
    }
}
