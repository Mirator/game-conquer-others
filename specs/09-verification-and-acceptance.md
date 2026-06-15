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
- Launches a battle from the opening campaign map encounter.
- Begins the battle.
- Captures opening and battle screenshots.
- Logs battle summaries.
- Stops Play mode after completion.

## Standalone Smoke Test

Launch the executable with `-smoketest`.

The standalone smoke test:

- Starts on the campaign map and assaults the first attackable territory.
- Begins the battle automatically.
- Audits all four correct blocks.
- Audits all four wrong blocks.
- Audits rear-angle block bypass.
- Audits gesture jitter rejection, diagonal hysteresis, deliberate direction
  selection, held-attack cancellation into block, and swept-target filtering.
- Audits AI attack-permission limits and living enemy target assignments.
- Captures opening, combat, and late-battle screenshots.
- Exits after the run.

Add `-smokelarge` to run a 6v6 encounter and verify coordination and spacing
under a larger fighter count.

Add `-smokeduel` to run a one-on-one readability scenario. It audits fresh and
corrected-direction perfect blocks, ordinary blocks, the counter damage state,
and captures a deterministic primary-threat telegraph.

Add `-smokevictory` to:

- Force enemy elimination.
- Verify victory.
- Verify the loop returns to the map with the territory captured and the roster
  updated to the survivors.

Add `-smokearena=<Courtyard|Forest|Marsh|Highlands>` to force a specific arena.

Combine `-smokevictory -smokecampaign` to verify several consecutive
conquests and persistent campaign progression.

## Current Verified Status

- Live Unity compilation: passed.
- Windows standalone build: passed (`Builds/Windows/ConquerOthers.exe`).
- Editor smoke test: opening state correct (Blue=4, Red=4, Fighting); battle log shows stable group combat.
- Directional block audit: passed.
- Natural group battle: passed.
- Forced victory and restart: passed.
- Managed exception scan: passed.
- P0/P1 responsive combat audit: passed.
- P2 tactical AI coordination audit: passed.
- P3 threat readability and perfect-block/counter audit: passed.
- Deterministic duel telegraph capture: passed; one primary threat displays one
  incoming-direction cue near the reticle without enemy-attached text.
- Three repeated natural battles and a 6v6 stress battle passed with the new
  combat feedback and counter systems.
- Repeated natural 4v4 battles: player attacker limit held at one; no central
  fighter knot observed.
- Large 6v6 battle: coordination audit passed; no close-pair cluster at seven
  seconds; both teams remained active through the late-battle capture.
- Campaign economy audit: recruitment, gold spending, conquest reward, income,
  and typed survivor persistence passed.
- Arena matrix: courtyard, forest, marsh, and highlands natural-combat runs
  passed.
- Multi-conquest campaign: three consecutive captures preserved state and grew
  the realm to four territories, the warband to six, and the treasury to 401.
