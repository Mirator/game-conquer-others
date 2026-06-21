# 03 - Player Controls and Camera

## Controls

| Input | Action |
|---|---|
| W / A / S / D | Camera-relative movement |
| Left Shift | Sprint |
| Space | Dodge |
| 1 / 2 / 3 / 4 | Order allies to Follow / Hold / Charge / Advance |
| F | Cycle ally formation (Line / Shield Wall / Skirmish) |
| H | Toggle allied archers' hold-fire |
| Hold left mouse + move mouse | Aim a directional attack |
| Release left mouse | Strike in the aimed direction |
| Hold right mouse + move mouse | Block in that direction |
| Hold left mouse with bow | Draw bow while freely aiming the camera |
| Release left mouse with bow | Fire through the crosshair |
| Escape | Release cursor |

## Movement

- Normal movement speed is 4.7 units per second.
- Sprint speed is 7.4 units per second.
- Dodge speed is 11.5 units per second for 0.3 seconds.
- Dodge costs 32 stamina and has a 0.6 second cooldown.
- Attacking and blocking reduce movement speed.
- While blocking or preparing an attack, the player faces camera-forward.

## Combat Direction Input

- Mouse delta is accumulated across an 80 millisecond gesture window.
- A gesture must travel at least 10 pixels before it can change direction.
- Axis hysteresis rejects ambiguous diagonal movement and prevents jitter.
- Horizontal movement selects Left or Right.
- Upward movement selects Up.
- Downward movement selects Thrust.
- Mouse movement below the threshold or without a dominant axis preserves the
  previous direction.
- The initial fallback direction is Right.
- While holding left mouse during wind-up or hold, continued mouse movement
  re-aims the attack. The direction commits when the button is released.
- While holding right mouse, mouse movement updates the block direction live.
- Attack and block requests are buffered for 150 milliseconds.
- Right mouse cancels AttackWindup or AttackHold directly into a block.

## Camera

- Camera style is close third-person over-the-shoulder, never first-person.
- The complete player silhouette should remain visible during ordinary combat.
- Mouse orbit pitch is clamped between -12 and 36 degrees.
- Camera collision uses a sphere cast to avoid clipping through arena geometry.
- Movement adds subtle bob and sway.
- Impacts and dodges may add short camera shake.
- Mouse look pauses while either combat mouse button is held, so direction
  gestures do not rotate the camera. Bow aiming is the exception: mouse look
  remains active while the bow is drawn.

## Camera Modes

| Mode | Distance | Field of View |
|---|---:|---:|
| Normal | 5.95 | 59 |
| Sprinting | 6.6 | 64 |
| Blocking | 5.45 | 56 |
| Bow aiming | 5.15 | 51 |

## Campaign Map Camera

The overworld uses a separate top-down camera, controlled by
`CampaignMapController.CameraControls`:

- The campaign opens zoomed in on the player's warband (not the whole map); the
  player zooms/pans out from there.
- Mouse-wheel scroll zooms along the camera's forward axis, clamped between
  heights 14 and 78.
- Holding the right mouse button and dragging pans across the ground plane; pan
  speed scales with the current zoom height.
- The map camera does not rotate or pitch and has no edge-scroll.
- The bottom HUD toolbar's buttons summon the Recruit, Promote, and Captain
  Equipment panels (one open at a time); clicking left-mouse on the map plans a
  march target. See [11-campaign-map-and-meta-loop.md](11-campaign-map-and-meta-loop.md).
