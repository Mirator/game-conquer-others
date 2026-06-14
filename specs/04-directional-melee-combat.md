# 04 - Directional Melee Combat

## Goal

Combat must reward reading the opponent, selecting a direction, timing release,
matching blocks, and maintaining useful range.

## Directions

| Direction | Attack | Matching Block |
|---|---|---|
| Left | Left slash | Block left |
| Right | Right slash | Block right |
| Up | Overhead strike | Block high |
| Thrust | Forward stab | Block thrust |

## Attack Input and Phases

- Pressing left mouse begins attack preparation in the current direction.
- While holding left mouse during wind-up or hold, moving the mouse re-aims
  the attack. The direction commits when the button is released.
- Releasing left mouse releases the attack.
- Releasing during wind-up queues release as soon as wind-up completes.

| Phase | Rule |
|---|---|
| AttackWindup | Readable preparation; direction still adjustable; movement slowed. |
| AttackHold | Prepared pose remains until release; direction still adjustable. |
| AttackRelease | Attack lunges forward and may deal damage once; direction locked. |
| AttackRecovery | Fighter cannot start another attack. |

## Timing and Damage

| Attack | Wind-up | Release | Recovery | Damage |
|---|---:|---:|---:|---:|
| Left slash | 0.35s | 0.25s | 0.45s | 25 |
| Right slash | 0.35s | 0.25s | 0.45s | 25 |
| Overhead | 0.50s | 0.25s | 0.60s | 35 |
| Thrust | 0.30s | 0.20s | 0.40s | 20 |

## Attack Restrictions

- Starting an attack costs 18 stamina.
- A fighter cannot attack while blocking, staggered, dead, or already attacking.
- Attacks use a 2.2-unit reach and a 100-degree target cone.
- Damage is attempted once during release.
- One swing can damage at most one living opposing fighter.

## Blocking

A block succeeds only when all conditions are true:

- The defender is actively blocking.
- The defender's block direction matches the incoming attack direction.
- The attacker is inside the defender's 90-degree front arc.

Successful block:

- Deals zero health damage.
- Plays a metal clash and sparks.
- Briefly reacts the defender.
- Forces the attacker into extended recovery.

Wrong-direction, late, absent, or rear-angle block:

- Deals full attack damage.
- Interrupts blocking.
- Applies hit reaction and knockback.

## Feedback and Readability

- All four attacks use distinct procedural weapon poses.
- All four blocks use distinct shield poses.
- HUD displays the selected attack or active block direction.
- Hits and blocks use different sound, spark color, reaction, and camera shake.
- Dead fighters cannot be hit.

## Future Combat Features

Feints, partial blocks, parries, armor, additional weapons, mounted attacks, and
friendly fire are outside the current MVP.

