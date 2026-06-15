# 05 - Fighters, Teams, and AI

## Shared Fighter Model

Player and AI fighters share:

- Health, stamina, team membership, and death rules.
- Directional attack and block state.
- CharacterController-based movement.
- Procedural sword-and-shield, two-handed sword, or bow model plus limbs and
  team-colored clothing.
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

- AI scores living opponents by distance, existing target assignments, and
  whether the opponent is the player.
- Target locks last 0.85 to 1.45 seconds so fighters do not rapidly switch
  opponents.
- Target-assignment pressure distributes fighters across available opponents.
- At least one living enemy remains assigned to the player so the player has a
  readable primary threat.
- AI decisions refresh every 0.2 to 0.42 seconds.
- Continuous separation steering and obstacle avoidance reduce overlap and
  prevent fighters from walking directly into walls.

## AI Combat

- Fighters claim attack permission before entering the active duel space:
  - At most one AI may actively attack the player.
  - At most two AI may actively attack another AI.
- Active attackers maintain roughly 1.65 to 1.95 units of preferred range.
- Supporting fighters seek distributed engagement slots and hold roughly 2.8
  to 3.5 units from their target.
- Attack timers are staggered to prevent synchronized opening swings.
- Active attackers close distance, back away when crowded, circle, and retreat
  from threatening swings.
- Attack direction has a 72% chance to exploit a target's active block and
  favors overhead attacks against targets in recovery.
- AI automatically releases attacks after preparation.
- AI reacts to the most immediate incoming threat, including opponents other
  than its current target.
- A chosen block has roughly a 62% chance to match a player's incoming
  direction and 52% against another AI.
- Blocks last between 0.4 and 0.8 seconds.
- Archers hold ranged spacing, compensate for arrow drop, evade nearby enemies,
  and can begin firing across the opening formation distance.
- Two-handed swordsmen use a longer preferred melee range than shield users.

## Team Behavior

Allied and enemy AI use the same behavior and combat rules. Their only
functional difference is which team they target and their health value. Allied
target scoring avoids the enemy currently threatening the player, preserving a
readable primary duel unless no better opponent is available.

Allied soldiers additionally obey Follow, Hold, and Charge orders. AI forces
can break morale after severe casualties and retreat from battle. See
[15-tactical-commands-formations-and-morale.md](15-tactical-commands-formations-and-morale.md).

## Future AI Features

Difficulty profiles and more advanced coordinated maneuvers are outside the
current MVP.
