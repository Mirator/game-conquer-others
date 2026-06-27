using System.Collections.Generic;
using UnityEngine;

// The persistent campaign model: the territory graph plus the player's warband
// roster. Pure data and logic — no MonoBehaviour, so it survives map/battle
// root teardown by living on the GameDirector.
public sealed class CampaignState
{
    public readonly List<Territory> Territories = new();
    public readonly List<EnemyParty> Parties = new();
    public readonly UnitRoster Units = new();
    public int Roster => Units.Total;
    public int Gold;
    public int Seed;
    public Vector2 PartyPosition;     // the player warband's overworld location
    public int Day = 1;               // campaign clock, advanced by travel
    // Fraction of the current campaign day already spent travelling. Persisted so
    // entering a battle or reloading cannot erase the remaining daylight/time cost.
    public float DayProgress;
    public bool CampaignOver;         // set when the player is defeated
    public string LastReport = "March your warband across the land. Hunt bandits, raid holds, grow your host.";
    public WeaponType PlayerWeapon = WeaponType.SwordAndShield;
    public WeaponType TrainingEnemyWeapon = WeaponType.SwordAndShield;

    public int Renown;                  // earned from victories and held land; raises the cap
    public int Morale = StartingMorale; // 0..100 party morale; low morale breeds desertion

    // Leadership: the warband size cap grows with Renown, from BaseLeadership up to
    // MaxLeadership. This is the player's campaign command ceiling and is kept
    // separate from (and smaller than) the battlefield deployment ceiling
    // (BattleSetup.MaxDeployed) so bigger battles do not inflate warband economy.
    public const int BaseLeadership = 6;
    public const int MaxLeadership = 24;
    public const int RenownPerCapStep = 15;

    // Economy / morale tuning.
    public const int StartingMorale = 60;
    public const int MoraleTargetBase = 60;
    public const int MoraleDriftPerDay = 5;
    public const int MoraleUnpaidPenalty = 15;
    public const int MoraleUnpaidTargetDrop = 40;
    public const int MoraleOvercapPenalty = 5;
    public const int MoraleVictoryBonus = 10;
    public const int MoraleFieldWinBonus = 5;
    public const int DesertionMoraleFloor = 25;
    public const int DesertionMoraleRebound = 10;
    public const int RenownPerHoldPerDay = 1;
    public const int XpPerEnemyDefeated = 10;
    public const int GarrisonUpkeepPerHold = 5;

    public int LeadershipCap => Mathf.Clamp(BaseLeadership + Renown / RenownPerCapStep, BaseLeadership, MaxLeadership);

    // Procedural map generation lives in CampaignMapGenerator; this stays the public
    // entry point so callers and saves are unaffected.
    public static CampaignState CreateDefault(int seed) => CampaignMapGenerator.CreateDefault(seed);

    public Territory GetById(int id) => Territories[id];

    public bool AdjacentToPlayer(Territory t)
    {
        foreach (int id in t.AdjacentIds)
            if (Territories[id].Owner == TerritoryOwner.Player)
                return true;
        return false;
    }

    public bool IsAttackable(Territory t) => t.Owner != TerritoryOwner.Player && AdjacentToPlayer(t);

    public IEnumerable<Territory> AttackableTargets()
    {
        foreach (Territory t in Territories)
            if (IsAttackable(t))
                yield return t;
    }

    public int PlayerTerritoryCount()
    {
        int count = 0;
        foreach (Territory t in Territories)
            if (t.Owner == TerritoryOwner.Player)
                count++;
        return count;
    }

    // Gold collected each campaign day from every owned hold.
    public int DailyIncome()
    {
        int total = 0;
        foreach (Territory territory in Territories)
            if (territory.Owner == TerritoryOwner.Player)
                total += territory.Income;
        return total;
    }

    // Gold owed each campaign day in troop wages, summed over the warband.
    public int DailyWage()
    {
        int wage = 0;
        foreach (RosterEntry entry in Units.Entries)
            wage += entry.Count * UnitCatalog.Upkeep(entry.Tier);
        return wage;
    }

    // Gold owed each day to garrison the holds the warband controls. Unlike troop
    // wages (which scale with army size), this scales with territory — so holding
    // land carries an ongoing cost and taking every weak hold is no longer free
    // upside. Net daily cashflow is income minus wages minus this.
    public int DailyGarrisonUpkeep() => GarrisonUpkeepPerHold * PlayerTerritoryCount();

    // Advances the warband economy by one campaign day: collect owned-land income,
    // pay troop wages (morale suffers and the purse empties if the coffers run dry),
    // earn renown from held land, drift morale toward its target, refill settlement
    // recruit pools, and let an unhappy soldier desert. Called once per day elapsed
    // by OverworldSimulation.
    public void ApplyDayTick()
    {
        int moraleStart = Morale;
        int income = DailyIncome();
        int wages = DailyWage();
        int upkeep = DailyGarrisonUpkeep();
        Gold += income;

        int expenses = wages + upkeep;
        bool paid = Gold >= expenses;
        if (paid)
            Gold -= expenses;
        else
        {
            Gold = 0;
            Morale = Mathf.Clamp(Morale - MoraleUnpaidPenalty, 0, 100);
        }

        Renown += RenownPerHoldPerDay * PlayerTerritoryCount();
        Morale = Mathf.Clamp(StepToward(Morale, MoraleTarget(paid), MoraleDriftPerDay), 0, 100);
        RegenerateRecruits();

        UnitType? deserted = null;
        if (Morale < DesertionMoraleFloor)
            deserted = Desert();

        // Surface the day's economy and morale so passing days are legible instead
        // of silent. The map renders LastReport; on a multi-day march the latest
        // day's line is shown.
        LastReport = BuildDayReport(income, expenses, paid, moraleStart, deserted);
    }

    private string BuildDayReport(int income, int expenses, bool paid, int moraleStart, UnitType? deserted)
    {
        int net = income - expenses;
        string netStr = (net >= 0 ? "+" : "") + net;
        string report = $"Day {Day}: +{income}g income, -{expenses}g wages & upkeep (net {netStr}g).";
        if (!paid)
            report += " Coffers ran dry - troops went unpaid!";
        report += moraleStart == Morale ? $" Morale {Morale}." : $" Morale {moraleStart}->{Morale}.";
        if (deserted.HasValue)
            report += $" A {UnitCatalog.Label(deserted.Value)} deserted in the night.";
        else if (Morale < DesertionMoraleFloor + MoraleDriftPerDay)
            report += " Morale is fraying - desertions loom.";
        return report;
    }

    private int MoraleTarget(bool wagesPaid)
    {
        int target = MoraleTargetBase;
        if (!wagesPaid)
            target -= MoraleUnpaidTargetDrop;
        int over = Roster - LeadershipCap;
        if (over > 0)
            target -= over * MoraleOvercapPenalty;
        return Mathf.Clamp(target, 0, 100);
    }

    private static int StepToward(int current, int target, int step)
    {
        if (current < target)
            return Mathf.Min(current + step, target);
        if (current > target)
            return Mathf.Max(current - step, target);
        return current;
    }

    private void RegenerateRecruits()
    {
        foreach (Territory t in Territories)
        {
            int max = SettlementCatalog.MaxRecruits(t.Settlement);
            if (t.Recruits < max)
                t.Recruits++;
        }
    }

    // Morale has cratered: the least-committed fighter (lowest tier) slips away in
    // the night. Being rid of the malcontent steadies the rest a little. Returns the
    // deserter's tier (or null if the warband was empty) so the day report can name it.
    private UnitType? Desert()
    {
        RosterEntry victim = null;
        foreach (RosterEntry entry in Units.Entries)
            if (entry.Count > 0 && (victim == null || entry.Tier < victim.Tier))
                victim = entry;
        if (victim == null)
            return null;
        UnitType tier = victim.Tier;
        victim.Count--;
        Morale = Mathf.Clamp(Morale + DesertionMoraleRebound, 0, 100);
        return tier;
    }

    // Maps a campaign day to a 0..1 time of day. The golden-ratio step spreads
    // successive days across the cycle while staying deterministic (a given day
    // always lights the same), so a retried battle looks identical.
    public static float TimeOfDayForDay(int day) => Mathf.Repeat(0.18f + (day - 1) * 0.61803f, 1f);

    // Continuous overworld sun phase 0..1: each day runs a natural
    // dawn->midday->dusk->night arc, advancing with dayFraction while marching and
    // holding when idle. 0/1 = midnight, .25 dawn, .5 midday, .75 dusk (matching
    // BattleBootstrap.ApplySunAndSky). The 0.35 offset opens every fresh day in broad
    // daylight (just before noon) rather than the dim pre-dawn. Distinct from the
    // golden-ratio battle mapping above, which spreads days for variety.
    public static float OverworldSunPhase(int day, float dayFraction) =>
        Mathf.Repeat(0.35f + (day - 1) + Mathf.Clamp01(dayFraction), 1f);

    public BattleSetup BuildSetupFor(Territory t) => new BattleSetup
    {
        AllyCount = Mathf.Clamp(Roster, 0, MaxLeadership),
        AllyMilitia = Units.Militia,
        AllyVeterans = Units.Veterans,
        AllyGuards = Units.Guards,
        AllyComposition = BuildAllyComposition(),
        EnemyCount = Mathf.Clamp(t.Garrison, 1, 12),
        EnemyVeterans = Mathf.Clamp((t.Threat - 1) / 2, 0, t.Garrison),
        EnemyGuards = t.Threat >= 4 ? 1 : 0,
        EnemyComposition = BuildEnemyComposition(t),
        EnemyHealthScale = t.DifficultyScale,
        TargetName = t.Name.ToUpperInvariant(),
        Arena = t.Arena,
        PlayerWeapon = PlayerWeapon,
        Kind = BattleKind.SettlementAssault,
        TimeOfDay = TimeOfDayForDay(Day)
    };

    // Builds the garrison's archetype mix: soldiers at low threat, joined by
    // shieldbearers, then archers/berserkers and an arena-flavored bias, with a
    // captain anchoring the strongest holds. Tiers rise with threat.
    private static List<UnitSpec> BuildEnemyComposition(Territory t)
    {
        int count = Mathf.Clamp(t.Garrison, 1, 12);
        int threat = t.Threat;
        List<UnitSpec> specs = new();

        if (threat >= 4 && count > 1)
            specs.Add(Enemy(Archetype.Captain, UnitType.Veteran));

        List<Archetype> pool = new() { Archetype.Soldier };
        if (threat >= 2)
            pool.Add(Archetype.Shieldbearer);
        if (threat >= 3)
        {
            pool.Add(Archetype.Archer);
            pool.Add(Archetype.Berserker);
            pool.Add(ArenaBias(t.Arena));
        }

        int i = 0;
        while (specs.Count < count)
        {
            Archetype archetype = pool[i % pool.Count];
            specs.Add(Enemy(archetype, TierFor(archetype, threat)));
            i++;
        }
        return specs;
    }

    private static Archetype ArenaBias(ArenaType arena) => arena switch
    {
        ArenaType.Highlands => Archetype.Berserker,
        ArenaType.Marsh => Archetype.Archer,
        ArenaType.Forest => Archetype.Archer,
        _ => Archetype.Shieldbearer
    };

    private static UnitType TierFor(Archetype archetype, int threat)
    {
        if (archetype == Archetype.Captain || threat >= 4)
            return UnitType.Veteran;
        if (threat >= 3 && (archetype == Archetype.Shieldbearer || archetype == Archetype.Berserker))
            return UnitType.Veteran;
        return UnitType.Militia;
    }

    private static UnitSpec Enemy(Archetype archetype, UnitType tier)
        => new UnitSpec(tier, archetype, ArchetypeCatalog.Weapon(archetype));

    // Expands the warband roster into one UnitSpec per fighter, each carrying its
    // archetype's weapon, so allies fight with their chosen personalities.
    private List<UnitSpec> BuildAllyComposition()
    {
        List<UnitSpec> specs = new();
        foreach (RosterEntry entry in Units.Entries)
            for (int i = 0; i < entry.Count && specs.Count < MaxLeadership; i++)
                specs.Add(new UnitSpec(entry.Tier, entry.Archetype, ArchetypeCatalog.Weapon(entry.Archetype)));
        return specs;
    }

    public BattleSetup BuildTrainingSetup() => new BattleSetup
    {
        AllyCount = 0,
        EnemyCount = 1,
        EnemyHealthScale = 1f,
        TargetName = "TRAINING ARENA",
        Arena = ArenaType.Courtyard,
        PlayerWeapon = PlayerWeapon,
        TrainingEnemyWeapon = TrainingEnemyWeapon,
        IsTraining = true,
        Kind = BattleKind.Training,
        TimeOfDay = 0.5f
    };

    // Recruitment draws volunteers from a settlement in range. The settlement's
    // type sets the highest tier on offer and a limited pool that refills over days;
    // cost is set by the tier, and the archetype is a free choice of behavior. The
    // warband size is gated by Leadership. The range check itself lives in the sim/UI.
    public bool CanRecruit(UnitType tier, Archetype archetype, Territory settlement)
        => settlement != null
           && SettlementCatalog.Allows(settlement.Settlement, tier)
           && settlement.Recruits > 0
           && Roster < LeadershipCap
           && Gold >= UnitCatalog.Cost(tier);

    public bool Recruit(UnitType tier, Archetype archetype, Territory settlement)
    {
        if (!CanRecruit(tier, archetype, settlement))
            return false;
        int cost = UnitCatalog.Cost(tier);
        Gold -= cost;
        settlement.Recruits--;
        Units.Add(tier, archetype, 1);
        LastReport = $"{ArchetypeCatalog.Label(archetype)} {UnitCatalog.Label(tier)} recruited at {settlement.Name} for {cost} gold.";
        return true;
    }

    // A fighter that has banked enough battle experience can be promoted to the next
    // tier (keeping its archetype) for gold. Experience is pooled per stack.
    public bool CanUpgrade(UnitType tier, Archetype archetype)
        => UnitCatalog.CanUpgrade(tier)
           && Units.Count(tier, archetype) > 0
           && Units.Xp(tier, archetype) >= UnitCatalog.UpgradeXp(tier)
           && Gold >= UnitCatalog.UpgradeCost(tier);

    public bool TryUpgrade(UnitType tier, Archetype archetype)
    {
        if (!CanUpgrade(tier, archetype))
            return false;
        int cost = UnitCatalog.UpgradeCost(tier);
        if (!Units.Upgrade(tier, archetype))
            return false;
        Gold -= cost;
        LastReport = $"{ArchetypeCatalog.Label(archetype)} promoted to {UnitCatalog.Label(UnitCatalog.NextTier(tier))} for {cost} gold.";
        return true;
    }

    // A field battle against a roaming party: bandit-tier enemies scaled by the
    // party's strength, fought wherever the party was caught.
    public BattleSetup BuildPartySetup(EnemyParty party) => new BattleSetup
    {
        AllyCount = Mathf.Clamp(Roster, 0, MaxLeadership),
        AllyComposition = BuildAllyComposition(),
        EnemyCount = Mathf.Clamp(party.Strength, 1, 12),
        EnemyComposition = BuildBanditComposition(party),
        EnemyHealthScale = 1f,
        TargetName = party.Name,
        Arena = party.Arena,
        PlayerWeapon = PlayerWeapon,
        Kind = BattleKind.BanditField,
        TimeOfDay = TimeOfDayForDay(Day)
    };

    private static List<UnitSpec> BuildBanditComposition(EnemyParty party)
    {
        int count = Mathf.Clamp(party.Strength, 1, 12);
        Archetype[] pool = { Archetype.Soldier, Archetype.Berserker, Archetype.Archer };
        List<UnitSpec> specs = new();
        for (int i = 0; i < count; i++)
        {
            Archetype archetype = pool[i % pool.Length];
            specs.Add(new UnitSpec(UnitType.Militia, archetype, ArchetypeCatalog.Weapon(archetype)));
        }
        return specs;
    }

    public void ApplyVictory(Territory t, BattleResult result)
    {
        int reward = t.RewardGold;
        int renown = 20 + t.Threat * 5;
        int enemies = Mathf.Clamp(t.Garrison, 1, 12);
        t.Owner = TerritoryOwner.Player;
        ApplySurvivors(result, enemies);
        Gold += reward;
        Renown += renown;
        Morale = Mathf.Clamp(Morale + MoraleVictoryBonus, 0, 100);
        LastReport = $"{t.Name} captured for {reward} conquest gold (+{renown} renown).";
    }

    public void ResolveFieldBattle(EnemyParty party, BattleResult result)
    {
        int enemies = Mathf.Clamp(party.Strength, 1, 12);
        int renown = 5 + party.Strength * 2;
        Parties.Remove(party);
        int loot = 25 + party.Strength * 15;
        Gold += loot;
        ApplySurvivors(result, enemies);
        Renown += renown;
        Morale = Mathf.Clamp(Morale + MoraleFieldWinBonus, 0, 100);
        LastReport = $"Defeated {party.Name}. Looted {loot} gold (+{renown} renown).";
    }

    // Rebuilds the warband from a battle's allied survivors, preserving tier and
    // archetype. Pooled XP belongs to the (tier x archetype) stack rather than to
    // individuals, so it is carried across the rebuild and dropped only for stacks
    // that were wiped out. Falls back to tier-only counts for smoke/test results.
    private void ApplySurvivors(BattleResult result, int enemiesDefeated)
    {
        Dictionary<(UnitType, Archetype), int> bankedXp = new();
        foreach (RosterEntry entry in Units.Entries)
            bankedXp[(entry.Tier, entry.Archetype)] = entry.Xp;

        Units.Clear();
        if (result.SurvivingUnits != null && result.SurvivingUnits.Count > 0)
            foreach (RosterEntry entry in result.SurvivingUnits)
                Units.Add(entry.Tier, entry.Archetype, Mathf.Max(0, entry.Count));
        else
        {
            Units.Add(UnitType.Militia, Archetype.Soldier, Mathf.Max(0, result.MilitiaSurvived));
            Units.Add(UnitType.Veteran, Archetype.Soldier, Mathf.Max(0, result.VeteransSurvived));
            Units.Add(UnitType.Guard, Archetype.Soldier, Mathf.Max(0, result.GuardsSurvived));
        }

        foreach (RosterEntry entry in Units.Entries)
            if (bankedXp.TryGetValue((entry.Tier, entry.Archetype), out int xp))
                entry.Xp = xp;

        AwardBattleXp(enemiesDefeated);
    }

    // Spreads the battle's experience across the surviving warband, pooled onto each
    // stack so the units that fought earn toward a promotion.
    private void AwardBattleXp(int enemiesDefeated)
    {
        int survivors = Roster;
        if (survivors <= 0 || enemiesDefeated <= 0)
            return;
        int per = Mathf.Max(1, enemiesDefeated * XpPerEnemyDefeated / survivors);
        foreach (RosterEntry entry in Units.Entries)
            if (entry.Count > 0)
                entry.Xp += per * entry.Count;
    }

    public void ApplyDefeat()
    {
        CampaignOver = true;
        LastReport = "The captain fell. The campaign is lost.";
    }
}
