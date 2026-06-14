Original prompt: /goal Lets build the MVP according to the [conquer_others_gdd.md](conquer_others_gdd.md)

## Progress

- Chose a runtime-bootstrapped Unity prototype so the MVP has no imported-asset or manual scene setup dependency.
- Added a low-poly primitive battlefield, third-person camera, player controls, melee attacks, blocking, dodge, health, death, team AI, HUD, win/lose state, and restart flow.
- Added `Conquer Others > Build Windows MVP` for a one-click standalone build.
- Verified a clean Unity 6.3 script compilation in the live editor.
- Verified a successful isolated Windows standalone build.
- Began a second quality pass while preserving the same PoC scope.
- Improved combat with stamina, guard pressure/breaks, clearer attack phases, lunges, stagger, knockback, hit sparks, camera shake, and procedural weapon sounds.
- Improved fighter silhouettes and procedural animation, AI spacing/defense, arena dressing, and HUD readability.
- Added automated editor and standalone smoke paths with rendered screenshot capture.
- Removed the template URP build dependency, pinned procedural shaders, and verified the standalone player launches and completes a battle without managed exceptions.
- Verified rendered standalone opening, defeat, victory, and restart paths.
- Final standalone gates: natural battle reached defeat and exited cleanly; forced victory reached victory, reset to a fresh ready battle, and exited cleanly.
- Added an immersion pass with a close over-the-shoulder camera, movement sway, tighter combat framing, spatial weapon/impact audio, footsteps, wind, drums, warmer lighting, fog, and torches.
- Tuned the close camera through three rendered passes, corrected close-view player weapon proportions, and verified readable opening and mid-combat framing.
- Verified the immersive standalone build reaches natural defeat, victory, and restart with clean code-0 exits and no managed exceptions.
- Corrected the close camera after visual feedback showed it reading as first-person; restored full player weapon proportions and moved the rig to an unmistakable close third-person view.
- Adopted `conquer_others_directional_combat_canvas.md` as the directional melee source of truth.
- Added four-direction mouse-selected attacks with wind-up, hold, release, and recovery phases plus direction-specific timing, damage, and procedural poses.
- Added four-direction strict blocking, AI directional attacks/blocks, block recoil, and a crosshair direction readout.
- Added a runtime directional combat audit that verifies correct blocks take zero damage and wrong blocks take full damage.
- Verified all four correct blocks, all four wrong blocks, rear-angle block bypass, natural group combat, forced victory, and restart in the final standalone build.

## Controls

- WASD: move
- Mouse: camera
- Left Shift: sprint
- Space: dodge
- Mouse movement: select attack/block direction
- Hold/release left mouse: prepare/release attack
- Hold right mouse: block in selected direction
- Escape: release cursor
- R: restart battle

## TODO / Polish

- Replace procedural primitive fighters with animated character assets.
- Add command controls (follow/charge/hold) after the core battle feel is approved.
- Replace synthesized prototype audio with recorded weapon and voice assets during a later production pass.
