using UnityEngine;

// The per-campaign-day warband economy, extracted from CampaignState: owned-land
// income, troop wages and garrison upkeep, morale drift, recruit-pool regeneration,
// and desertion. Operates on the state it is given; CampaignState.ApplyDayTick
// delegates here. Tuning constants and the public daily-total queries stay on
// CampaignState (its public surface); this owns the once-per-day orchestration.
public static class CampaignEconomy
{
    public static void ApplyDayTick(CampaignState state)
    {
        int moraleStart = state.Morale;
        int income = state.DailyIncome();
        int wages = state.DailyWage();
        int upkeep = state.DailyGarrisonUpkeep();
        state.Gold += income;

        int expenses = wages + upkeep;
        bool paid = state.Gold >= expenses;
        if (paid)
            state.Gold -= expenses;
        else
        {
            state.Gold = 0;
            state.Morale = Mathf.Clamp(state.Morale - CampaignState.MoraleUnpaidPenalty, 0, 100);
        }

        state.Renown += CampaignState.RenownPerHoldPerDay * state.PlayerTerritoryCount();
        state.Morale = Mathf.Clamp(StepToward(state.Morale, MoraleTarget(state, paid), CampaignState.MoraleDriftPerDay), 0, 100);
        RegenerateRecruits(state);

        UnitType? deserted = null;
        if (state.Morale < CampaignState.DesertionMoraleFloor)
            deserted = Desert(state);

        // Surface the day's economy and morale so passing days are legible instead of
        // silent. The map renders LastReport; on a multi-day march the latest line shows.
        state.LastReport = BuildDayReport(state, income, expenses, paid, moraleStart, deserted);
    }

    private static string BuildDayReport(CampaignState state, int income, int expenses, bool paid, int moraleStart, UnitType? deserted)
    {
        int net = income - expenses;
        string netStr = (net >= 0 ? "+" : "") + net;
        string report = $"Day {state.Day}: +{income}g income, -{expenses}g wages & upkeep (net {netStr}g).";
        if (!paid)
            report += " Coffers ran dry - troops went unpaid!";
        report += moraleStart == state.Morale ? $" Morale {state.Morale}." : $" Morale {moraleStart}->{state.Morale}.";
        if (deserted.HasValue)
            report += $" A {UnitCatalog.Label(deserted.Value)} deserted in the night.";
        else if (state.Morale < CampaignState.DesertionMoraleFloor + CampaignState.MoraleDriftPerDay)
            report += " Morale is fraying - desertions loom.";
        return report;
    }

    private static int MoraleTarget(CampaignState state, bool wagesPaid)
    {
        int target = CampaignState.MoraleTargetBase;
        if (!wagesPaid)
            target -= CampaignState.MoraleUnpaidTargetDrop;
        int over = state.Roster - state.LeadershipCap;
        if (over > 0)
            target -= over * CampaignState.MoraleOvercapPenalty;
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

    private static void RegenerateRecruits(CampaignState state)
    {
        foreach (Territory t in state.Territories)
        {
            int max = SettlementCatalog.MaxRecruits(t.Settlement);
            if (t.Recruits < max)
                t.Recruits++;
        }
    }

    // Morale has cratered: the least-committed fighter (lowest tier) slips away in the
    // night. Being rid of the malcontent steadies the rest a little. Returns the
    // deserter's tier (or null if the warband was empty) so the day report can name it.
    private static UnitType? Desert(CampaignState state)
    {
        RosterEntry victim = null;
        foreach (RosterEntry entry in state.Units.Entries)
            if (entry.Count > 0 && (victim == null || entry.Tier < victim.Tier))
                victim = entry;
        if (victim == null)
            return null;
        UnitType tier = victim.Tier;
        victim.Count--;
        state.Morale = Mathf.Clamp(state.Morale + CampaignState.DesertionMoraleRebound, 0, 100);
        return tier;
    }
}
