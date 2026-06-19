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
        Territory site = RecruitSiteFor(campaign, UnitType.Veteran);
        int startingGold = campaign.Gold;

        Assert.That(campaign.Recruit(UnitType.Veteran, Archetype.Soldier, site), Is.True);
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
    public void Recruit_FailsWithoutGoldOrLeadershipSpace()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Territory castle = SettlementOfType(campaign, SettlementType.Castle);

        campaign.Gold = UnitCatalog.Cost(UnitType.Guard) - 1;
        Assert.That(campaign.CanRecruit(UnitType.Guard, Archetype.Soldier, castle), Is.False);
        Assert.That(campaign.Recruit(UnitType.Guard, Archetype.Soldier, castle), Is.False, "Recruiting with too little gold should fail.");
        Assert.That(campaign.Gold, Is.EqualTo(UnitCatalog.Cost(UnitType.Guard) - 1), "A failed recruit must not spend gold.");

        // Renown lifts the leadership cap to its ceiling, then fill the warband.
        campaign.Gold = 100000;
        campaign.Renown = 1000;
        int guard = 0;
        while (campaign.Roster < campaign.LeadershipCap && guard++ < 100)
        {
            castle.Recruits = SettlementCatalog.MaxRecruits(castle.Settlement); // keep the pool stocked
            if (!campaign.Recruit(UnitType.Militia, Archetype.Soldier, castle))
                break;
        }
        Assert.That(campaign.Roster, Is.EqualTo(campaign.LeadershipCap), "Recruiting should stop at the leadership cap.");
        castle.Recruits = SettlementCatalog.MaxRecruits(castle.Settlement);
        Assert.That(campaign.CanRecruit(UnitType.Militia, Archetype.Soldier, castle), Is.False);
        Assert.That(campaign.Recruit(UnitType.Militia, Archetype.Soldier, castle), Is.False, "Recruiting past the leadership cap should fail.");
    }

    [Test]
    public void Recruit_SettlementTypeGatesTierAndPoolDepletes()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        campaign.Gold = 100000;
        campaign.Renown = 1000; // generous cap so the leadership gate is not what blocks us
        Territory village = SettlementOfType(campaign, SettlementType.Village);
        Territory castle = SettlementOfType(campaign, SettlementType.Castle);

        Assert.That(campaign.CanRecruit(UnitType.Veteran, Archetype.Soldier, village), Is.False, "Villages raise only militia.");
        Assert.That(campaign.CanRecruit(UnitType.Militia, Archetype.Soldier, village), Is.True);
        Assert.That(campaign.CanRecruit(UnitType.Guard, Archetype.Soldier, castle), Is.True, "Castles raise every tier.");

        int pool = village.Recruits;
        Assert.That(pool, Is.GreaterThan(0));
        for (int i = 0; i < pool; i++)
            Assert.That(campaign.Recruit(UnitType.Militia, Archetype.Soldier, village), Is.True);
        Assert.That(village.Recruits, Is.EqualTo(0));
        Assert.That(campaign.Recruit(UnitType.Militia, Archetype.Soldier, village), Is.False, "An empty pool blocks recruiting.");
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
    public void Victory_PaysRewardAndGrowsDailyIncome()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Territory target = FirstEnemyTerritory(campaign);
        int goldBefore = campaign.Gold;
        int reward = target.RewardGold;

        Assert.That(campaign.DailyIncome(), Is.EqualTo(0), "No daily income before the first hold.");
        campaign.ApplyVictory(target, new BattleResult { PlayerWon = true });

        Assert.That(campaign.DailyIncome(), Is.GreaterThan(0), "Capturing a hold yields daily income.");
        Assert.That(campaign.Gold, Is.EqualTo(goldBefore + reward),
            "Victory pays the conquest reward; owned-land income now accrues per day, not in a lump.");
        Assert.That(campaign.Renown, Is.GreaterThan(0), "Conquest earns renown.");
    }

    [Test]
    public void CampaignSave_RoundTripsProgress()
    {
        CampaignSaveService.Delete();
        CampaignState original = CampaignState.CreateDefault(11);
        Territory target = FirstEnemyTerritory(original);
        int targetId = target.Id;
        original.Recruit(UnitType.Veteran, Archetype.Soldier, RecruitSiteFor(original, UnitType.Veteran));
        original.PlayerWeapon = WeaponType.Bow;
        original.ApplyVictory(target, new BattleResult { PlayerWon = true, VeteransSurvived = 1 });
        original.Renown = 55;
        original.Morale = 42;
        original.Units.AddXp(UnitType.Veteran, Archetype.Soldier, 77);

        CampaignSaveService.Save(original);
        Assert.That(CampaignSaveService.HasSave, Is.True);
        CampaignState loaded = CampaignSaveService.Load();
        CampaignSaveService.Delete();

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.Seed, Is.EqualTo(original.Seed));
        Assert.That(loaded.Gold, Is.EqualTo(original.Gold));
        Assert.That(loaded.Renown, Is.EqualTo(55), "Renown survives the round trip.");
        Assert.That(loaded.Morale, Is.EqualTo(42), "Morale survives the round trip.");
        Assert.That(loaded.PlayerWeapon, Is.EqualTo(WeaponType.Bow));
        Assert.That(loaded.Units.Veterans, Is.EqualTo(original.Units.Veterans));
        Assert.That(loaded.Units.Xp(UnitType.Veteran, Archetype.Soldier),
            Is.EqualTo(original.Units.Xp(UnitType.Veteran, Archetype.Soldier)), "Banked XP survives the round trip.");
        Assert.That(loaded.Territories.Count, Is.EqualTo(original.Territories.Count));
        Assert.That(loaded.GetById(targetId).Owner, Is.EqualTo(TerritoryOwner.Player));
        Assert.That(loaded.GetById(targetId).Settlement, Is.EqualTo(original.GetById(targetId).Settlement),
            "Settlement type survives the round trip.");
        Assert.That(loaded.GetById(targetId).Recruits, Is.EqualTo(original.GetById(targetId).Recruits),
            "Recruit pool survives the round trip.");
        Assert.That(loaded.PlayerTerritoryCount(), Is.EqualTo(original.PlayerTerritoryCount()));
        Assert.That(loaded.GetById(targetId).AdjacentIds.Count,
            Is.EqualTo(original.GetById(targetId).AdjacentIds.Count), "Adjacency must survive the round trip.");
    }

    [Test]
    public void Recruit_TracksTierAndArchetypeIndependently()
    {
        CampaignState campaign = CampaignState.CreateDefault(3);
        campaign.Gold = 100000;
        Territory castle = SettlementOfType(campaign, SettlementType.Castle);
        Assert.That(campaign.Recruit(UnitType.Veteran, Archetype.Berserker, castle), Is.True);
        Assert.That(campaign.Recruit(UnitType.Veteran, Archetype.Shieldbearer, castle), Is.True);
        Assert.That(campaign.Recruit(UnitType.Militia, Archetype.Archer, castle), Is.True);

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
        Assert.That(campaign.DailyIncome(), Is.EqualTo(0), "No income before capturing a hold.");
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

    [Test]
    public void BattleXp_AccruesToSurvivorsOnVictory()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Territory target = FirstEnemyTerritory(campaign);
        campaign.ApplyVictory(target, new BattleResult
        {
            PlayerWon = true,
            SurvivingUnits = new List<RosterEntry>
            {
                new RosterEntry { Tier = UnitType.Militia, Archetype = Archetype.Soldier, Count = 3 }
            }
        });
        Assert.That(campaign.Units.Xp(UnitType.Militia, Archetype.Soldier), Is.GreaterThan(0),
            "Survivors bank experience from the enemies they defeated.");
    }

    [Test]
    public void Upgrade_RequiresXpAndGold_PromotesPreservingArchetype()
    {
        CampaignState campaign = CampaignState.CreateDefault(3);
        campaign.Units.Clear();
        campaign.Units.Add(UnitType.Militia, Archetype.Berserker, 1);
        campaign.Units.AddXp(UnitType.Militia, Archetype.Berserker, UnitCatalog.UpgradeXp(UnitType.Militia));

        campaign.Gold = UnitCatalog.UpgradeCost(UnitType.Militia) - 1;
        Assert.That(campaign.CanUpgrade(UnitType.Militia, Archetype.Berserker), Is.False, "Not enough gold to promote.");

        campaign.Gold = 1000;
        int goldBefore = campaign.Gold;
        Assert.That(campaign.CanUpgrade(UnitType.Militia, Archetype.Berserker), Is.True);
        Assert.That(campaign.TryUpgrade(UnitType.Militia, Archetype.Berserker), Is.True);
        Assert.That(campaign.Units.Count(UnitType.Militia, Archetype.Berserker), Is.EqualTo(0));
        Assert.That(campaign.Units.Count(UnitType.Veteran, Archetype.Berserker), Is.EqualTo(1),
            "Promotion raises the tier and keeps the archetype.");
        Assert.That(campaign.Gold, Is.EqualTo(goldBefore - UnitCatalog.UpgradeCost(UnitType.Militia)));
        Assert.That(campaign.Units.Xp(UnitType.Militia, Archetype.Berserker), Is.EqualTo(0), "Promotion spends the banked XP.");
    }

    [Test]
    public void Upgrade_GuardIsTerminal()
    {
        CampaignState campaign = CampaignState.CreateDefault(3);
        campaign.Units.Clear();
        campaign.Units.Add(UnitType.Guard, Archetype.Soldier, 1);
        campaign.Units.AddXp(UnitType.Guard, Archetype.Soldier, 100000);
        campaign.Gold = 100000;
        Assert.That(campaign.CanUpgrade(UnitType.Guard, Archetype.Soldier), Is.False);
        Assert.That(campaign.TryUpgrade(UnitType.Guard, Archetype.Soldier), Is.False, "Guards are the top tier.");
    }

    [Test]
    public void DayTick_PaysIncomeAndDrainsWages()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        campaign.Gold = 100;
        int wage = campaign.DailyWage();
        Assert.That(wage, Is.GreaterThan(0), "A warband owes wages.");

        campaign.ApplyDayTick();
        Assert.That(campaign.Gold, Is.EqualTo(100 - wage), "With no holds, a day costs the wage bill.");

        // Capture a hold, then a day nets income minus wages.
        campaign.ApplyVictory(FirstEnemyTerritory(campaign), new BattleResult { PlayerWon = true });
        int income = campaign.DailyIncome();
        int wage2 = campaign.DailyWage();
        int goldBefore = campaign.Gold;
        campaign.ApplyDayTick();
        Assert.That(campaign.Gold, Is.EqualTo(goldBefore + income - wage2), "Daily cashflow is income minus wages.");
    }

    [Test]
    public void DayTick_LowMoraleCausesDesertion()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        campaign.Gold = 100000; // wages are paid, so morale is the only pressure
        campaign.Morale = 10;
        int rosterBefore = campaign.Roster;

        campaign.ApplyDayTick();
        Assert.That(campaign.Roster, Is.EqualTo(rosterBefore - 1), "A miserable soldier deserts overnight.");
    }

    [Test]
    public void LeadershipCap_GrowsWithRenownUpToCeiling()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        Assert.That(campaign.LeadershipCap, Is.EqualTo(CampaignState.BaseLeadership), "Starts at base leadership.");
        campaign.Renown = CampaignState.RenownPerCapStep * (CampaignState.MaxLeadership - CampaignState.BaseLeadership);
        Assert.That(campaign.LeadershipCap, Is.EqualTo(CampaignState.MaxLeadership), "Renown raises the cap to its ceiling.");
        campaign.Renown = 100000;
        Assert.That(campaign.LeadershipCap, Is.EqualTo(CampaignState.MaxLeadership), "The cap never exceeds the ceiling.");
    }

    [Test]
    public void DayTick_RefillsSettlementRecruitPools()
    {
        CampaignState campaign = CampaignState.CreateDefault(11);
        campaign.Gold = 100000;
        Territory village = SettlementOfType(campaign, SettlementType.Village);
        village.Recruits = 0;
        campaign.ApplyDayTick();
        Assert.That(village.Recruits, Is.EqualTo(1), "Pools refill a volunteer per day.");
    }

    private static Territory FirstEnemyTerritory(CampaignState campaign)
    {
        foreach (Territory territory in campaign.Territories)
            if (territory.Owner == TerritoryOwner.Enemy)
                return territory;
        return null;
    }

    private static Territory SettlementOfType(CampaignState campaign, SettlementType type)
    {
        foreach (Territory territory in campaign.Territories)
            if (territory.Settlement == type)
                return territory;
        return null;
    }

    private static Territory RecruitSiteFor(CampaignState campaign, UnitType tier)
    {
        foreach (Territory territory in campaign.Territories)
            if (territory.Recruits > 0 && SettlementCatalog.Allows(territory.Settlement, tier))
                return territory;
        return null;
    }
}
