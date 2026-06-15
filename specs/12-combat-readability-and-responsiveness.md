# 12 - Combat Readability and Responsiveness

## Problem Statement

The directional rules function, but the current battle feels messy, chaotic,
and unresponsive. Functional correctness is not enough: the player must be able
to identify a threat, express an intention, understand the result, and recover
before the next important decision.

## Current Evidence

- A standalone natural smoke battle ended after 16.3 seconds with all four blue
  fighters dead and all four red fighters alive.
- At seven seconds, most fighters visually overlap in one central cluster.
- Every AI independently selects its nearest opponent and may converge on the
  same target.
- There is no limit on simultaneous attackers around one defender.
- Close-range AI strafing does not include separation steering.
- Attack and block direction use raw per-frame mouse delta with a two-pixel
  threshold and no hysteresis or gesture accumulation.
- The same mouse delta also rotates the camera while selecting combat direction.
- Releasing attack during wind-up queues the strike until wind-up finishes,
  creating up to 0.5 seconds of apparent release latency.
- Blocking input is rejected throughout all attack phases, including recovery,
  and is not buffered.
- Hits select the nearest target inside a 2.2-unit, 100-degree cone at one
  instant rather than following the visible sword path.
- Every impact adds camera shake, including distant AI-versus-AI impacts.
- Swing audio plays when preparation begins, not when the sword is released.

## Design Pillars

### One Important Threat

The player may see a battle around them, but should normally need to read and
answer only one immediate attack at a time.

### Intent Is Sticky

Small mouse jitter must not change a chosen attack or block. Inputs should
remain stable until the player makes a clear new gesture.

### Results Match Animation

Hits, misses, blocks, sounds, and reactions must visibly correspond to the
weapon motion and defender pose.

### Local Feedback

Only events important to the player should strongly affect the camera, screen,
or central HUD.

## Required Improvements

Implementation status: P0, P1, P2, and P3 are delivered.

### P0 - Restore Player Responsiveness

1. Replace raw frame-by-frame direction selection with a short gesture sampler:
   - Accumulate mouse delta across roughly 60 to 100 milliseconds.
   - Require an 8 to 12 pixel gesture before changing direction.
   - Add hysteresis so diagonal jitter does not repeatedly switch axes.
   - Preserve the last committed direction when input is ambiguous.
2. Separate camera look from combat-direction gestures:
   - Direction gestures should not meaningfully rotate the camera.
   - Resume normal look immediately after attack or block input ends.
3. Add a 150 millisecond action buffer:
   - Buffered block begins as soon as recovery permits.
   - Buffered attack begins as soon as the fighter may attack.
4. Allow RMB to cancel AttackWindup or AttackHold into a block.
5. Keep AttackRelease and early recovery committed and non-cancelable.
6. Move swing audio to AttackRelease; use a quieter preparation cue if needed.

### P1 - Make Hits Trustworthy

1. Replace broad nearest-target cone selection with a swept directional strike
   test that follows the visible sword path during release.
2. Keep one-hit-per-swing and friendly-fire protections.
3. Distinguish a hit, block, and whiff with separate sound and animation cues.
4. Remove automatic forward lunge when no valid target is near the strike path.
5. Add brief local hit-stop or attacker/defender reaction on a landed player hit.
6. Camera shake must be strong only when the player is hit, blocks, or lands a
   nearby important strike. Distant AI impacts should not shake the camera.

### P2 - Control Group Combat

1. Add engagement slots around each target instead of unrestricted convergence.
2. Limit active attack permission:
   - At most one AI may actively attack the player at a time in the first pass.
   - At most two AI may actively attack another AI at a time.
   - Other fighters circle, hold distance, or seek another opponent.
3. Apply separation steering at all distances, especially inside combat range.
4. Add a retarget cooldown so fighters do not rapidly switch opponents.
5. Stagger initial and subsequent attack timers to prevent synchronized swings.
6. Non-active attackers should hold roughly 2.5 to 3.5 units from the target,
   leaving a readable duel space.

Delivered with tactical target distribution, stable target locks, engagement
slots, attack permissions, staggered attacks, continuous separation, and
obstacle avoidance. Runtime telemetry audits player/general attacker limits,
minimum fighter distance, close pairs, and invalid target assignments.

### P3 - Improve Threat Readability

1. Give AI attacks a clear preparation pose and minimum readable telegraph.
2. Show an incoming-direction cue only for the player's current primary threat.
3. Reduce or hide health bars inside dense clusters unless recently damaged.
4. Keep the primary opponent visible; avoid allies fully occluding the duel.
5. Add a brief post-block counter window through longer attacker recovery.

Delivered with one primary-threat direction/progress cue, damage-gated health
bars, allies avoiding the player's active opponent, longer blocked-attacker
recovery, and a timed perfect-block/counter system.

## Initial Tuning Targets

| Value | Target |
|---|---:|
| Direction gesture window | 60-100 ms |
| Direction gesture threshold | 8-12 pixels |
| Action input buffer | 150 ms |
| Active attackers versus player | 1 |
| Active attackers versus AI | 2 maximum |
| Non-active engagement distance | 2.5-3.5 units |
| Retarget cooldown | 0.8-1.5 seconds |
| Strong camera shake distance | Player-involved events only |

These are starting targets and must be validated by playtesting.

## Implementation Order

1. Add a duel-focused debug encounter and responsiveness telemetry.
2. Implement gesture-based direction input and buffered block/attack actions.
3. Localize camera feedback and correct release audio.
4. Add engagement slots, attacker limits, and close-range separation.
5. Replace cone hit selection with swept directional strike tests.
6. Tune AI telegraphs, timings, and primary-threat UI.
7. Re-test one-on-one first, then 4v4, then larger campaign encounters.

## Acceptance Criteria

### Responsiveness

- A clear direction gesture selects the intended direction reliably.
- Mouse jitter does not change direction.
- A buffered block begins on the first legal frame.
- RMB cancels held preparation into block.
- Attack release, block, hit, and whiff each have immediate matching feedback.

### Readability

- During ordinary 4v4 play, the player normally faces one immediate attacker.
- The player can identify the incoming attack direction before release.
- Visible sword motion and registered hit agree.
- Distant AI combat does not shake the player's camera.

### Group Stability

- Fighters do not remain stacked in one central knot.
- Non-active fighters maintain useful spacing.
- Both teams can deal damage in repeated unattended 4v4 simulations.
- A natural battle should not consistently end as a one-sided untouched-team
  wipe unless encounter balance intentionally causes it.

## Verification Additions

The existing correctness smoke tests remain required, but add:

- Direction gesture tests for jitter, diagonals, and deliberate flicks.
- Input-buffer test for block pressed during recovery.
- Cancellation test from hold into block.
- Attacker-token test proving the player receives only one active AI attack.
- Spacing telemetry for minimum fighter distance and cluster duration.
- Repeated unattended battle samples reporting duration, damage, and survivors.
- Visual capture of a duel, a block, a whiff, and a 4v4 mid-battle state.
