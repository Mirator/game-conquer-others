# 15 - Tactical Commands, Formations, and Morale

## Captain Commands

During a campaign battle, the player commands allied soldiers with the number
keys:

| Input | Order | Behavior |
|---|---|---|
| 1 | Follow | Allies form around the captain and defend against nearby threats. |
| 2 | Hold | Allies defend positions captured when the order is issued. |
| 3 | Charge | Allies leave formation and use unrestricted tactical targeting. |

Training battles do not use ally commands.

## Formation Behavior

- Battles begin with the `Follow` order.
- Follow uses four-soldier ranks centered on the captain. The first rank forms
  a protective line ahead of the captain and additional ranks trail behind.
- Allies move into formation while enemies are outside the order's defense
  radius.
- A nearby enemy overrides formation movement so allies can defend themselves
  and the captain.
- Hold captures a distinct position for every living allied soldier. Allies
  return to those positions after dealing with nearby threats.
- Charge preserves the existing engagement-slot, target-distribution, and
  attack-permission combat behavior without formation constraints.

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

- The fighting HUD shows the active order and the `1 / 2 / 3` command controls.
- Issuing an order displays a brief central confirmation message.
- Retreats display a brief message when a soldier leaves the battle.

## Verification

Run the standalone build with `-smoketest -smokecommands` to verify:

- The battle starts in Follow.
- Allies assemble around the captain.
- Hold positions remain anchored after the captain moves.
- Charge removes formation constraints.
- A shattered enemy force breaks, retreats, withdraws, and ends the battle.
