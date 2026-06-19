using UnityEngine;

// What advancing the overworld a step produced. The map controller dispatches
// each variant to the GameDirector / report.
public enum OverworldOutcomeKind { None, FieldBattle, ArriveEnemy, RestAtHold }

public readonly struct OverworldOutcome
{
    public readonly OverworldOutcomeKind Kind;
    public readonly EnemyParty Party;
    public readonly Territory Territory;

    private OverworldOutcome(OverworldOutcomeKind kind, EnemyParty party, Territory territory)
    {
        Kind = kind;
        Party = party;
        Territory = territory;
    }

    public static OverworldOutcome None() => new OverworldOutcome(OverworldOutcomeKind.None, null, null);
    public static OverworldOutcome FieldBattle(EnemyParty party) => new OverworldOutcome(OverworldOutcomeKind.FieldBattle, party, null);
    public static OverworldOutcome ArriveEnemy(Territory territory) => new OverworldOutcome(OverworldOutcomeKind.ArriveEnemy, null, territory);
    public static OverworldOutcome RestAtHold(Territory territory) => new OverworldOutcome(OverworldOutcomeKind.RestAtHold, null, territory);
}

// Pure, deterministic overworld travel and encounter simulation. The map
// controller owns rendering, input, and the camera; this owns the movement math
// and encounter decisions so the campaign loop is unit-testable in EditMode.
// Takes deltaTime as a parameter (no Time.deltaTime) and mutates CampaignState
// directly. It only reads/moves Parties; membership changes (removing a defeated
// band) happen later in CampaignState.ResolveFieldBattle.
public sealed class OverworldSimulation
{
    public const float TravelSpeed = 9f;        // map units per second while marching
    public const float DistancePerDay = 4f;     // map units that elapse one campaign day
    public const float EnemySightRange = 12f;   // bandits chase the player within this range
    public const float EnemyChaseRatio = 0.55f; // bandit speed as a fraction of the player's
    public const float EncounterRadius = 1.4f;  // party collision distance
    public const float ThreatStrengthFactor = 0.6f;  // bands below this fraction of the player ignore it
    public const float SettlementRecruitRange = 2.4f; // how close a friendly hold must be to recruit

    private readonly CampaignState campaign;

    public bool Travelling { get; private set; }
    private Vector2 travelTarget;
    private Territory pendingTerritory;
    private EnemyParty pendingParty;
    private float dayAccumulator;

    public OverworldSimulation(CampaignState campaign) => this.campaign = campaign;

    // The player's fighting strength: the captain plus the warband.
    public int PlayerStrength => campaign.Roster + 1;

    // Bands weaker than this fraction of the player's host ignore it (no point
    // ambushing a host they cannot beat); the player can still hunt them down.
    public bool IsThreat(EnemyParty party) => party.Strength >= Mathf.CeilToInt(PlayerStrength * ThreatStrengthFactor);

    public bool AtFriendlyCity()
    {
        foreach (Territory t in campaign.Territories)
            if (t.Owner == TerritoryOwner.Player
                && (t.MapPosition - campaign.PartyPosition).sqrMagnitude <= SettlementRecruitRange * SettlementRecruitRange)
                return true;
        return false;
    }

    // The settlement the warband can recruit from right now: the nearest one within
    // recruit range, regardless of who holds it (volunteers come from the locals).
    // Null when no settlement is close enough.
    public Territory RecruitSettlement()
    {
        Territory best = null;
        float bestSqr = SettlementRecruitRange * SettlementRecruitRange;
        foreach (Territory t in campaign.Territories)
        {
            float sqr = (t.MapPosition - campaign.PartyPosition).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }
        return best;
    }

    // Whole campaign days a march to target would take from the current position,
    // at DistancePerDay map units per day. Used for the travel preview/ETA; a march
    // of any non-zero length costs at least one day.
    public int DaysTo(Vector2 target)
    {
        float distance = (target - campaign.PartyPosition).magnitude;
        return distance <= 0.01f ? 0 : Mathf.Max(1, Mathf.CeilToInt(distance / DistancePerDay));
    }

    public void BeginTravel(Vector2 target, Territory territory, EnemyParty party)
    {
        travelTarget = target;
        pendingTerritory = territory;
        pendingParty = party;
        Travelling = true;
    }

    // Advances travel by one tick: moves the party toward its destination, accrues
    // days, steps enemy parties, and returns the encounter/arrival outcome
    // (None while still in transit).
    public OverworldOutcome Tick(float deltaTime)
    {
        if (!Travelling)
            return OverworldOutcome.None();

        Vector2 toDest = travelTarget - campaign.PartyPosition;
        float distance = toDest.magnitude;
        float step = TravelSpeed * deltaTime;
        Vector2 move = distance <= step ? toDest : toDest.normalized * step;
        campaign.PartyPosition += move;

        dayAccumulator += move.magnitude;
        while (dayAccumulator >= DistancePerDay)
        {
            dayAccumulator -= DistancePerDay;
            campaign.Day++;
            campaign.ApplyDayTick();
        }

        StepEnemyParties(move.magnitude);

        // A threatening band catching the player, or the band the player is
        // hunting, starts a battle; weaker bands are marched past harmlessly.
        foreach (EnemyParty party in campaign.Parties)
            if ((party.Position - campaign.PartyPosition).magnitude <= EncounterRadius
                && (IsThreat(party) || party == pendingParty))
                return EndTravelWith(OverworldOutcome.FieldBattle(party));

        if (distance <= step + 0.001f)
        {
            if (pendingParty != null)
                return EndTravelWith(OverworldOutcome.FieldBattle(pendingParty));
            if (pendingTerritory != null)
            {
                Territory hold = pendingTerritory;
                return EndTravelWith(hold.Owner == TerritoryOwner.Enemy
                    ? OverworldOutcome.ArriveEnemy(hold)
                    : OverworldOutcome.RestAtHold(hold));
            }
            return EndTravelWith(OverworldOutcome.None());
        }

        return OverworldOutcome.None();
    }

    // Passes a day in place: the day advances and roaming threats close in. Only
    // valid when stopped (the controller guards this).
    public OverworldOutcome WaitOneDay()
    {
        campaign.Day++;
        campaign.ApplyDayTick();
        StepEnemyParties(DistancePerDay);
        foreach (EnemyParty party in campaign.Parties)
            if (IsThreat(party) && (party.Position - campaign.PartyPosition).magnitude <= EncounterRadius)
                return OverworldOutcome.FieldBattle(party);
        return OverworldOutcome.None();
    }

    public void StepEnemyParties(float playerStep)
    {
        float chase = playerStep * EnemyChaseRatio;
        foreach (EnemyParty party in campaign.Parties)
        {
            if (!IsThreat(party))
                continue; // weak bands hold position rather than chase a stronger host
            Vector2 toPlayer = campaign.PartyPosition - party.Position;
            float d = toPlayer.magnitude;
            if (d > 0.001f && d < EnemySightRange)
                party.Position += toPlayer.normalized * Mathf.Min(chase, d);
        }
    }

    private OverworldOutcome EndTravelWith(OverworldOutcome outcome)
    {
        Travelling = false;
        pendingTerritory = null;
        pendingParty = null;
        return outcome;
    }
}
