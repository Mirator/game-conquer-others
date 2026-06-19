# 15 - Tactical Commands, Formations, and Morale

## Captain Commands

A captain commands allied soldiers along two independent axes: an **order**
(stance) and a **formation** (shape). Orders are bound to the number keys;
formation cycling and hold-fire are letter keys:

| Input | Order | Behavior |
|---|---|---|
| 1 | Follow | Allies form around the captain and defend against nearby threats. |
| 2 | Hold | Allies defend positions captured when the order is issued. |
| 3 | Charge | Allies leave formation and use unrestricted tactical targeting. |
| 4 | Advance | Allies hold formation around an anchor that marches forward. |

| Input | Control | Behavior |
|---|---|---|
| F | Cycle formation | Line → Shield Wall → Skirmish, then back to Line. |
| H | Hold fire | Toggles allied archers between loosing and holding ready. |

Training battles do not use ally commands.

## Formation Behavior

- Battles begin with the `Follow` order in the `Line` formation.
- **Order** decides stance; **formation** decides the shape allies hold while not
  in melee. The two are orthogonal — any shape can be used under Follow, Hold, or
  Advance. Charge ignores formation entirely.
- The three shapes, all centered on the captain (or, under Advance, on the
  marching anchor):
  - **Line** — wide ranks with one protective rank ahead of the captain and the
    rest trailing behind. The default, and the most spread-out fighting line.
  - **Shield Wall** — narrower, deeper, and slower; allies hold the wall until an
    enemy is almost in contact before breaking to defend.
  - **Skirmish** — loose and spread, with per-soldier scatter; allies peel off to
    engage threats sooner.
- Each living allied soldier is assigned a stable formation slot, so soldiers do
  not swap places frame to frame.
- Allies move into formation while enemies are outside the order's defense radius;
  that radius is shape-aware (tighter for Shield Wall, wider for Skirmish).
- A nearby enemy overrides formation movement so allies can defend themselves and
  the captain.
- Hold captures a distinct position for every living allied soldier. Allies return
  to those positions after dealing with nearby threats.
- Advance reuses the formation slot maths around an anchor that creeps forward
  along the captain's facing at the time the order was given, clamped inside the
  arena, so the line presses ahead.
- Charge preserves the existing engagement-slot, target-distribution, and
  attack-permission combat behavior without formation constraints.

## Hold Fire

- Hold-fire affects allied archers only (Bow weapon); it has no effect if the
  warband has no archers.
- Held archers keep positioning and aiming but neither draw nor loose, reading as
  soldiers held at the ready. Releasing hold-fire returns them to loosing at will.
- The player's own bow fires from input and is unaffected by hold-fire.

## Morale And Retreat

This morale is a per-battle, AI-only measure of whether a force breaks and
retreats. It is distinct from the overworld **party morale** in spec 13, which
tracks the warband's day-to-day contentment on the campaign map.

- AI forces with at least three initial combatants evaluate morale from
  casualties and the current balance of living combatants.
- A force breaks when reduced to its final quarter, or when reduced to half
  strength while outnumbered at least two to one.
- Breaking AI soldiers stop attacking, run toward their deployment edge, and
  withdraw from combat.
- Withdrawn allied soldiers remain campaign survivors because retreat is not
  death.
- When the final enemy withdraws, the battle ends in victory.

## Presentation

- The fighting HUD shows the active order, the active formation, and a hold-fire
  indicator, alongside the `1 / 2 / 3 / 4` order keys and the `F` / `H` controls.
- Issuing an order, cycling formation, or toggling hold-fire displays a brief
  central confirmation message.
- Retreats display a brief message when a soldier leaves the battle.

## Verification

Run the standalone build with `-smoketest -smokecommands` to verify:

- The battle starts in Follow / Line.
- Allies assemble around the captain.
- Hold positions remain anchored after the captain moves.
- Charge removes formation constraints.
- Advance is accepted and formation cycling re-shapes the line.
- An allied archer holds fire while hold-fire is set and looses again once released.
- A shattered enemy force breaks, retreats, withdraws, and ends the battle.

The formation slot geometry (per-shape footprint, stable unique slots, the Advance
anchor sitting ahead of the captain, Charge releasing formation) is covered by the
`FormationTests` (EditMode) and `FormationCommandTests` (PlayMode) suites.
