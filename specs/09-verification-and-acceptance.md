# 09 - Verification and Acceptance

## Acceptance Gates

The current MVP is accepted when:

1. Unity scripts compile without errors.
2. A Windows standalone build succeeds.
3. The player can begin, play, finish, and restart a battle.
4. All four attack and block directions are represented.
5. Every matching directional block prevents all damage.
6. Every wrong directional block takes full damage.
7. A matching block fails when the attacker is outside the front arc.
8. Friendly fire and repeated same-swing damage do not occur.
9. Natural eight-fighter group combat remains stable.
10. Standalone logs contain no managed exceptions.

## Editor Smoke Test

Editor menu: `Conquer Others > Run Battle Smoke Test`.

The editor smoke test:

- Enters Play mode.
- Begins the battle.
- Captures opening and battle screenshots.
- Logs battle summaries.
- Stops Play mode after completion.

## Standalone Smoke Test

Launch the executable with `-smoketest`.

The standalone smoke test:

- Begins the battle automatically.
- Audits all four correct blocks.
- Audits all four wrong blocks.
- Audits rear-angle block bypass.
- Captures opening, combat, and late-battle screenshots.
- Exits after the run.

Add `-smokevictory` to:

- Force enemy elimination.
- Verify victory.
- Verify restart creates a fresh Ready battle.

## Current Verified Status

- Live Unity compilation: passed.
- Windows standalone build: passed.
- Directional block audit: passed.
- Natural group battle: passed.
- Forced victory and restart: passed.
- Managed exception scan: passed.

