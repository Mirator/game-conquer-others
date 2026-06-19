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
- Attack direction, guard choice, and aggression come from the fighter's
  `AIProfile`, so different archetypes fight differently. With the baseline
  Soldier profile, attack direction has roughly a 72% chance to exploit a
  target's active block (`feintChance`) and a 55% chance to favor an overhead
  against a target in recovery (`recoveryPunishChance`). Archetypes shift these:
  a Berserker rarely guards and punishes recovery harder, while a Shieldbearer
  or Captain guards more often and more skillfully.
- AI automatically releases attacks after preparation.
- AI reacts to the most immediate incoming threat, including opponents other
  than its current target.
- With the baseline Soldier profile, a chosen block has roughly a 62% chance to
  match a player's incoming direction and 52% against another AI
  (`blockCorrectChanceVsPlayer` / `blockCorrectChanceVsAi`); skilled archetypes
  read the incoming direction better.
- Blocks last between 0.42 and 0.72 seconds.
- Archers hold ranged spacing, compensate for arrow drop, evade nearby enemies,
  and can begin firing across the opening formation distance.
- Two-handed swordsmen use a longer preferred melee range than shield users.

## Archetypes and AI Profiles

Every fighter is a stat tier (`UnitType`: Militia, Veteran, Guard) crossed with
an `Archetype` (Soldier, Shieldbearer, Berserker, Archer, Captain). The two are
decoupled, so any tier can field any archetype. `ArchetypeCatalog` maps each
archetype to a weapon, an `AIProfile`, and health/damage multipliers layered on
top of the tier's own stat scale.

| Archetype | Weapon | Personality | Health x | Damage x |
|---|---|---|---:|---:|
| Soldier | Sword & Shield | Balanced line infantry; the baseline. | 1.0 | 1.0 |
| Shieldbearer | Sword & Shield | Patient turtle: guards constantly and skillfully, attacks slowly. | 1.15 | 0.9 |
| Berserker | Two-Handed | Relentless: rarely guards, fast, punishes recovery, near-fearless. | 0.95 | 1.2 |
| Archer | Bow | Ranged skirmisher: never melee-guards, falls back early. | 0.9 | 1.0 |
| Captain | Sword & Shield | Elite duelist and morale anchor: skilled defense, tricky offense, holds long. Reads as elite via a 1.18x model and a bright gold crest. | 1.45 | 1.25 |

An `AIProfile` carries aggression (plus per-fighter jitter), a range scale,
guard chance, block-correctness against the player and against AI, feint chance,
recovery-punish chance, and retreat bravery. The Soldier (Default) profile
reproduces the original pre-archetype behavior, so an unassigned fighter is
unchanged.

## Team Behavior

Allied and enemy AI use the same behavior and combat rules. Their only
functional difference is which team they target and their health value. Allied
target scoring avoids the enemy currently threatening the player, preserving a
readable primary duel unless no better opponent is available.

Allied soldiers additionally obey Follow, Hold, Charge, and Advance orders and
hold captain-centered formations. AI forces can break morale after severe
casualties and retreat from battle. See
[15-tactical-commands-formations-and-morale.md](15-tactical-commands-formations-and-morale.md).

## Future AI Features

Difficulty profiles and more advanced coordinated maneuvers are outside the
current MVP.
