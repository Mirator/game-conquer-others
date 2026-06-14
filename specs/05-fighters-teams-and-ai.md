# 05 - Fighters, Teams, and AI

## Shared Fighter Model

Player and AI fighters share:

- Health, stamina, team membership, and death rules.
- Directional attack and block state.
- CharacterController-based movement.
- Procedural sword, shield, limbs, and team-colored model.
- Hit flash, stagger, knockback, footstep, and death presentation.

## Combat States

| State | Meaning |
|---|---|
| Idle | Fighter may move, attack, or block. |
| AttackWindup | Fighter prepares an attack. |
| AttackHold | Prepared attack waits for release. |
| AttackRelease | Attack may deal damage. |
| AttackRecovery | Fighter is committed after the attack. |
| HitReaction | Fighter is briefly unable to act. |
| Dead | Fighter is disabled and cannot be targeted. |

Blocking is an active condition allowed only while the fighter can act and is
not attacking.

## AI Targeting

- AI selects the nearest living opposing fighter.
- AI decisions refresh every 0.2 to 0.42 seconds.
- AI faces its target and approaches until near preferred range.
- Separation steering reduces fighter overlap.
- AI may retreat when too close to an attacking target.
- AI strafes while at combat range.

## AI Combat

- Preferred range varies between 1.45 and 1.85 units.
- Attack direction is selected randomly.
- Attacks occur on a 1.5 to 2.5 second cooldown.
- AI automatically releases attacks after preparation.
- When threatened, AI has a 30% block decision chance.
- A chosen block has a 40% chance to match the incoming direction.
- Otherwise the AI deliberately chooses a wrong block direction.
- Blocks last between 0.4 and 0.8 seconds.

## Team Behavior

Allied and enemy AI use the same behavior and combat rules. Their only
functional difference is which team they target and their health value.

## Future AI Features

Commands, formations, difficulty profiles, morale, coordinated tactics, and
follow/charge/hold behavior are outside the current MVP.

