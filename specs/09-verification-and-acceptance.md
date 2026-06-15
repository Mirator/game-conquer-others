# 09 - Verification and Acceptance

## Acceptance Gates

The current MVP is accepted when:

1. Unity scripts compile without errors.
2. A Windows standalone build succeeds.
3. The player can begin, play, finish, and confirm a battle result.
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

- Can only be requested while the editor is outside Play mode.
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
- Exits with code 0 only when every audit passes; failed audits and transition
  timeouts exit with code 1.

Combat diagnostics run in a disposable duel before the natural-combat battle,
so their hits, blocks, counters, statistics, and recovery state cannot affect
the natural-combat verification.

Batch-mode smoke runs skip screenshots by default so functional verification
works headlessly. Add `-smokescreenshots` to explicitly capture them.

Add `-smokelarge` to run a 6v6 encounter and verify coordination and spacing
under a larger fighter count.

Add `-smokeduel` to run a one-on-one readability scenario. It audits fresh and
corrected-direction perfect blocks, ordinary blocks, the counter damage state,
and forces a deterministic primary-threat attack telegraph.

Add `-smokeweapons` to verify:

- Campaign equipment reaches the player.
- Training equipment reaches both duelists.
- Player bow projectiles deal damage at range.
- Bow NPCs independently fire and deal damage at range.
- Repeated training results do not change campaign gold, roster, lands, or
  defeat state.

Add `-smokecommands` to verify Follow formation assembly, anchored Hold
positions, unrestricted Charge behavior, and morale-driven enemy retreat.

Add `-smokevictory` to:

- Force enemy elimination.
- Verify victory.
- Verify the loop returns to the map with the territory captured and the roster
  updated to the survivors.

Add `-smokearena=<Courtyard|Forest|Marsh|Highlands>` to force a specific arena.

Combine `-smokevictory -smokecampaign` to verify several consecutive
conquests and persistent campaign progression.

## Automated Verification

Run `.\Tools\Verify.ps1` from PowerShell to execute EditMode tests, PlayMode
tests, the custom Windows shipping build, a headless standalone victory smoke,
a headless 6v6 natural-combat smoke, and a command/morale smoke.
`.\Tools\RunStandaloneSmokes.ps1` re-runs all three smokes against an existing
build.

Deterministic PlayMode tests cover directional combat diagnostics, perfect
blocks and counters, attack-permission limits, engagement-slot distribution,
separation steering, and battle-result lifecycle behavior.

Verification runs locally because Unity batch builds and tests require an
activated editor license. The repository does not run a GitHub Actions
verification workflow.

## Current Verified Status

- Live Unity compilation: passed.
- Windows standalone build: passed (`Builds/Windows/ConquerOthers.exe`).
- Editor smoke test: opening state correct (Blue=4, Red=4, Fighting); battle log shows stable group combat.
- Directional block audit: passed.
- Natural group battle: passed.
- Forced victory and return-to-map flow: passed.
- Managed exception scan: passed.
- P0/P1 responsive combat audit: passed.
- P2 tactical AI coordination audit: passed.
- P3 threat readability and perfect-block/counter audit: passed.
- Deterministic duel telegraph audit: passed; attack animation remains readable
  without enemy-attached or textual directional cues.
- Weapon and training audit: passed; bow player damage, bow NPC damage,
  two-handed loadouts, selected equipment, and consequence-free training return
  were verified.
- Tactical command and morale audit: passed; Follow formation, anchored Hold,
  Charge, enemy withdrawal, and allied-retreater roster preservation were
  verified.
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
