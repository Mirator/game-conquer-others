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
        Assert.That(campaign.Territories.Exists(t => t.Owner == TerritoryOwner.Enemy), Is.True,
            "The map offers enemy holds to take.");
    }

    [Test]
    public void RecruitmentAndVictory_PersistTypedSurvivorsAndEconomy()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Territory target = FirstEnemyTerritory(campaign);
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
        Territory target = FirstEnemyTerritory(campaign);
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
        Territory target = FirstEnemyTerritory(original);
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
        Assert.That(loaded.GetById(targetId).AdjacentIds.Count,
            Is.EqualTo(original.GetById(targetId).AdjacentIds.Count), "Adjacency must survive the round trip.");
    }

    [Test]
    public void Recruit_TracksTierAndArchetypeIndependently()
    {
        CampaignState campaign = CampaignState.CreateDefault(3);
        campaign.Gold = 100000;
        Assert.That(campaign.Recruit(UnitType.Veteran, Archetype.Berserker), Is.True);
        Assert.That(campaign.Recruit(UnitType.Veteran, Archetype.Shieldbearer), Is.True);
        Assert.That(campaign.Recruit(UnitType.Militia, Archetype.Archer), Is.True);

        Assert.That(campaign.Units.Count(UnitType.Veteran, Archetype.Berserker), Is.EqualTo(1));
        Assert.That(campaign.Units.Count(UnitType.Veteran, Archetype.Shieldbearer), Is.EqualTo(1));
        Assert.That(campaign.Units.Count(UnitType.Militia, Archetype.Archer), Is.EqualTo(1));
        Assert.That(campaign.Units.Veterans, Is.EqualTo(2), "Tier view sums archetypes of that tier.");
    }

    [Test]
    public void Victory_PreservesSurvivingArchetypes()
    {
        CampaignState campaign = CampaignState.CreateDefault(3);
        Territory target = FirstEnemyTerritory(campaign);
        campaign.ApplyVictory(target, new BattleResult
        {
            PlayerWon = true,
            SurvivingUnits = new List<RosterEntry>
            {
                new RosterEntry { Tier = UnitType.Veteran, Archetype = Archetype.Berserker, Count = 2 },
                new RosterEntry { Tier = UnitType.Militia, Archetype = Archetype.Archer, Count = 1 }
            }
        });

        Assert.That(campaign.Units.Count(UnitType.Veteran, Archetype.Berserker), Is.EqualTo(2));
        Assert.That(campaign.Units.Count(UnitType.Militia, Archetype.Archer), Is.EqualTo(1));
        Assert.That(campaign.Units.Total, Is.EqualTo(3));
    }

    [Test]
    public void ArchetypeCatalog_MapsWeaponsAndDistinctProfiles()
    {
        Assert.That(ArchetypeCatalog.Weapon(Archetype.Berserker), Is.EqualTo(WeaponType.TwoHandedSword));
        Assert.That(ArchetypeCatalog.Weapon(Archetype.Archer), Is.EqualTo(WeaponType.Bow));
        Assert.That(ArchetypeCatalog.Weapon(Archetype.Soldier), Is.EqualTo(WeaponType.SwordAndShield));
        Assert.That(ArchetypeCatalog.Profile(Archetype.Berserker).blockChance,
            Is.LessThan(ArchetypeCatalog.Profile(Archetype.Soldier).blockChance), "Berserkers guard less.");
        Assert.That(ArchetypeCatalog.Profile(Archetype.Shieldbearer).blockCorrectChanceVsPlayer,
            Is.GreaterThan(ArchetypeCatalog.Profile(Archetype.Soldier).blockCorrectChanceVsPlayer), "Shieldbearers guard better.");
        Assert.That(ArchetypeCatalog.HealthScale(Archetype.Captain),
            Is.GreaterThan(ArchetypeCatalog.HealthScale(Archetype.Soldier)), "Captains are tougher.");
    }

    [Test]
    public void EnemyComposition_FillsGarrisonAndScalesWithThreat()
    {
        CampaignState campaign = CampaignState.CreateDefault(5);
        BattleSetup low = campaign.BuildSetupFor(new Territory { Name = "Low", Garrison = 4, Threat = 1, Arena = ArenaType.Courtyard });
        BattleSetup high = campaign.BuildSetupFor(new Territory { Name = "High", Garrison = 7, Threat = 5, Arena = ArenaType.Highlands });

        Assert.That(low.EnemyComposition, Is.Not.Null);
        Assert.That(low.EnemyComposition.Count, Is.EqualTo(4), "Composition fills the garrison size.");
        Assert.That(high.EnemyComposition.Count, Is.EqualTo(7));

        Assert.That(low.EnemyComposition.TrueForAll(s => s.Archetype == Archetype.Soldier),
            Is.True, "A threat-1 garrison is all soldiers.");
        Assert.That(low.EnemyComposition.Exists(s => s.Archetype == Archetype.Captain),
            Is.False, "No captain at low threat.");
        Assert.That(high.EnemyComposition.Exists(s => s.Archetype == Archetype.Captain),
            Is.True, "A captain anchors a high-threat garrison.");
        Assert.That(high.EnemyComposition.Exists(s => s.Archetype == Archetype.Berserker),
            Is.True, "Highlands bias fields berserkers at high threat.");
    }

    [Test]
    public void FieldBattle_RemovesPartyAndLoots()
    {
        CampaignState campaign = CampaignState.CreateDefault(7);
        Assert.That(campaign.Parties, Is.Not.Empty, "A fresh campaign spawns roaming parties.");
        EnemyParty party = campaign.Parties[0];
        int goldBefore = campaign.Gold;

        BattleSetup setup = campaign.BuildPartySetup(party);
        Assert.That(setup.EnemyComposition.Count, Is.EqualTo(Mathf.Clamp(party.Strength, 1, 12)));

        campaign.ResolveFieldBattle(party, new BattleResult { PlayerWon = true });
        Assert.That(campaign.Parties.Contains(party), Is.False, "A defeated party leaves the map.");
        Assert.That(campaign.Gold, Is.GreaterThan(goldBefore), "Defeating a party loots gold.");
    }

    [Test]
    public void CreateDefault_PlayerStartsAloneWithNoHolds()
    {
        CampaignState campaign = CampaignState.CreateDefault(9);
        Assert.That(campaign.PlayerTerritoryCount(), Is.EqualTo(0), "Player owns no hold at the start.");
        Assert.That(campaign.IncomePerVictory(), Is.EqualTo(0), "No income before capturing a hold.");
        Assert.That(campaign.Parties, Is.Not.Empty, "Roaming bands seed the map.");
        foreach (Territory t in campaign.Territories)
            Assert.That(t.Owner, Is.EqualTo(TerritoryOwner.Enemy), "Every hold begins enemy-held.");
    }

    [Test]
    public void CreateDefault_IsDeterministicForSeed()
    {
        CampaignState a = CampaignState.CreateDefault(42);
        CampaignState b = CampaignState.CreateDefault(42);
        Assert.That(b.Territories.Count, Is.EqualTo(a.Territories.Count));
        Assert.That(b.Parties.Count, Is.EqualTo(a.Parties.Count));
        Assert.That(b.PartyPosition, Is.EqualTo(a.PartyPosition));
        for (int i = 0; i < a.Territories.Count; i++)
        {
            Assert.That(b.Territories[i].MapPosition, Is.EqualTo(a.Territories[i].MapPosition));
            Assert.That(b.Territories[i].Threat, Is.EqualTo(a.Territories[i].Threat));
            Assert.That(b.Territories[i].Garrison, Is.EqualTo(a.Territories[i].Garrison));
        }
    }

    [Test]
    public void CampaignSave_DeleteClearsSave()
    {
        CampaignSaveService.Delete();
        CampaignSaveService.Save(CampaignState.CreateDefault(4));
        Assert.That(CampaignSaveService.HasSave, Is.True);
        CampaignSaveService.Delete();
        Assert.That(CampaignSaveService.HasSave, Is.False);
        Assert.That(CampaignSaveService.Load(), Is.Null);
    }

    [Test]
    public void TimeOfDayForDay_IsDeterministicAndInRange()
    {
        for (int day = 1; day <= 60; day++)
        {
            float t = CampaignState.TimeOfDayForDay(day);
            Assert.That(t, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            Assert.That(t, Is.EqualTo(CampaignState.TimeOfDayForDay(day)), "Same day always lights the same.");
        }
    }

    [Test]
    public void SetupBuilders_TagEncounterKind()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Assert.That(campaign.BuildSetupFor(FirstEnemyTerritory(campaign)).Kind, Is.EqualTo(BattleKind.SettlementAssault));
        Assert.That(campaign.BuildPartySetup(campaign.Parties[0]).Kind, Is.EqualTo(BattleKind.BanditField));
        Assert.That(campaign.BuildTrainingSetup().Kind, Is.EqualTo(BattleKind.Training));
        Assert.That(campaign.BuildTrainingSetup().TimeOfDay, Is.EqualTo(0.5f));
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

    private static Territory FirstEnemyTerritory(CampaignState campaign)
    {
        foreach (Territory territory in campaign.Territories)
            if (territory.Owner == TerritoryOwner.Enemy)
                return territory;
        return null;
    }
}
