using System;
using System.Collections.Generic;
using UnityEngine;

// The captain's battlefield command layer — ally orders (Follow/Hold/Charge/Advance),
// formations, and archer hold-fire — extracted from BattleManager. Owns the command and
// formation state, the per-frame advance march, and formation slotting. BattleManager
// keeps the public API as thin facades and announces HUD lines via the supplied callback.
public sealed class BattleCommands
{
    private readonly List<BattleFighter> fighters;
    private readonly Func<PlayerFighter> player;
    private readonly Func<bool> isTraining;
    private readonly Action<string> announce;

    private readonly Dictionary<AIFighter, Vector3> holdPositions = new();
    private readonly Dictionary<AIFighter, int> allyFormationIndex = new();
    private readonly List<AIFighter> allyOrderScratch = new();
    private int allyOrderFrame = -1;
    // Advance order: a marching anchor that creeps forward each frame along the
    // captain's facing at the time the order was given, so the line presses ahead.
    private Vector3 advanceAnchor;
    private Vector3 advanceFacing = Vector3.forward;

    public BattleManager.AllyCommand CurrentAllyCommand { get; private set; } = BattleManager.AllyCommand.Follow;
    public FormationShape CurrentFormation { get; private set; } = FormationShape.Line;
    public bool AllyHoldFire { get; private set; }
    public float FormationSpeedScale => FormationBalance.SpeedScale(CurrentFormation);

    public BattleCommands(List<BattleFighter> fighters, Func<PlayerFighter> player, Func<bool> isTraining, Action<string> announce)
    {
        this.fighters = fighters;
        this.player = player;
        this.isTraining = isTraining;
        this.announce = announce;
    }

    public void SetAllyCommand(BattleManager.AllyCommand command)
    {
        if (isTraining())
            return;

        CurrentAllyCommand = command;
        holdPositions.Clear();
        if (command == BattleManager.AllyCommand.Hold)
        {
            foreach (BattleFighter fighter in fighters)
                if (fighter is AIFighter ally && ally.IsAlive && ally.Team == Team.Allies)
                    holdPositions[ally] = ally.transform.position;
        }
        else if (command == BattleManager.AllyCommand.Advance)
        {
            PlayerFighter captain = player();
            advanceFacing = FlattenForward(captain != null ? captain.transform.forward : Vector3.forward);
            Vector3 origin = captain != null ? captain.transform.position : Vector3.zero;
            advanceAnchor = ArenaMetrics.Clamp(origin + advanceFacing * FormationBalance.AdvanceStep);
        }

        announce(command switch
        {
            BattleManager.AllyCommand.Follow => "ALLIES: FORM ON ME",
            BattleManager.AllyCommand.Hold => "ALLIES: HOLD THIS GROUND",
            BattleManager.AllyCommand.Advance => "ALLIES: ADVANCE",
            _ => "ALLIES: CHARGE"
        });
    }

    public void CycleFormation()
    {
        if (isTraining())
            return;
        CurrentFormation = CurrentFormation switch
        {
            FormationShape.Line => FormationShape.ShieldWall,
            FormationShape.ShieldWall => FormationShape.Skirmish,
            _ => FormationShape.Line
        };
        announce($"FORMATION: {BattleManager.FormationName(CurrentFormation)}");
    }

    public void ToggleHoldFire()
    {
        if (isTraining())
            return;
        bool hasArcher = false;
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai.IsAlive && ai.Team == Team.Allies && ai.Weapon == WeaponType.Bow)
            {
                hasArcher = true;
                break;
            }
        if (!hasArcher)
        {
            announce("NO ARCHERS TO HOLD");
            return;
        }
        AllyHoldFire = !AllyHoldFire;
        announce(AllyHoldFire ? "ARCHERS: HOLD FIRE" : "ARCHERS: LOOSE AT WILL");
    }

    // Creeps the Advance anchor forward along the captain's order-time facing, clamped
    // inside the walls, so the line presses ahead each frame. No-op for other orders.
    public void TickAdvance()
    {
        if (CurrentAllyCommand == BattleManager.AllyCommand.Advance)
            advanceAnchor = ArenaMetrics.Clamp(advanceAnchor + advanceFacing * FormationBalance.AdvanceSpeed * Time.deltaTime);
    }

    // Drop a removed (dead/retreated) ally from the hold-position book.
    public void OnFighterRemoved(AIFighter fighter) => holdPositions.Remove(fighter);

    public bool TryGetCommandPosition(AIFighter ally, BattleFighter target, out Vector3 position)
    {
        position = ally.transform.position;
        PlayerFighter captain = player();
        if (ally.Team != Team.Allies || CurrentAllyCommand == BattleManager.AllyCommand.Charge || captain == null || !captain.IsAlive)
            return false;

        float threatDistance = target != null && target.IsAlive ? ally.DistanceTo(target) : float.MaxValue;
        // A nearby enemy breaks formation so allies defend themselves. Shield Wall holds
        // tighter (breaks only on contact) while Skirmish peels off sooner — shape-aware.
        float defenseRadius = CurrentAllyCommand == BattleManager.AllyCommand.Hold ? 4.5f : 5.5f;
        defenseRadius += CurrentFormation switch
        {
            FormationShape.ShieldWall => -1.2f,
            FormationShape.Skirmish => 1.5f,
            _ => 0f
        };
        if (threatDistance <= defenseRadius)
            return false;

        if (CurrentAllyCommand == BattleManager.AllyCommand.Hold && holdPositions.TryGetValue(ally, out Vector3 held))
            position = held;
        else
            position = GetFormationPosition(ally);
        return true;
    }

    // The captain-relative slot for an ally, in the current formation shape. Follow and
    // Hold orient on the captain; Advance orients on the marching anchor.
    public Vector3 GetFormationPosition(AIFighter ally)
    {
        int index = AllyFormationIndex(ally);
        int count = allyOrderScratch.Count;
        Vector3 offset = Formation.SlotLocalOffset(index, count, CurrentFormation);
        bool advancing = CurrentAllyCommand == BattleManager.AllyCommand.Advance;
        PlayerFighter captain = player();
        Vector3 facing = advancing ? advanceFacing : FlattenForward(captain.transform.forward);
        Vector3 anchor = advancing ? advanceAnchor : captain.transform.position;
        return anchor + Quaternion.LookRotation(facing) * offset;
    }

    // A stable per-frame ordering of living allied AI (sorted by instance id), so each
    // ally's formation slot is an O(1) lookup rather than an O(n) scan, rebuilt lazily
    // once per frame.
    public int AllyFormationIndex(AIFighter ally)
    {
        if (allyOrderFrame != Time.frameCount)
            RebuildAllyOrdering();
        return allyFormationIndex.TryGetValue(ally, out int index) ? index : 0;
    }

    private void RebuildAllyOrdering()
    {
        allyOrderFrame = Time.frameCount;
        allyOrderScratch.Clear();
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai.IsAlive && ai.Team == Team.Allies)
                allyOrderScratch.Add(ai);
        allyOrderScratch.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        allyFormationIndex.Clear();
        for (int i = 0; i < allyOrderScratch.Count; i++)
            allyFormationIndex[allyOrderScratch[i]] = i;
    }

    public int AlliesNearCommandPositions(float tolerance)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
        {
            if (fighter is not AIFighter ally || !ally.IsAlive || ally.Team != Team.Allies)
                continue;
            Vector3 destination = CurrentAllyCommand == BattleManager.AllyCommand.Hold && holdPositions.TryGetValue(ally, out Vector3 held)
                ? held : GetFormationPosition(ally);
            if (Vector3.Distance(ally.transform.position, destination) <= tolerance)
                count++;
        }
        return count;
    }

    private static Vector3 FlattenForward(Vector3 forward)
    {
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }
}
