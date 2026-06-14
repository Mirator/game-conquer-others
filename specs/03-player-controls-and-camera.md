# 03 - Player Controls and Camera

## Controls

| Input | Action |
|---|---|
| W / A / S / D | Camera-relative movement |
| Left Shift | Sprint |
| Space | Dodge |
| Hold left mouse + move mouse | Aim a directional attack |
| Release left mouse | Strike in the aimed direction |
| Hold right mouse + move mouse | Block in that direction |
| Escape | Release cursor |
| R | Restart battle |

## Movement

- Normal movement speed is 4.7 units per second.
- Sprint speed is 7.4 units per second.
- Dodge speed is 11.5 units per second for 0.3 seconds.
- Dodge costs 32 stamina and has a 0.6 second cooldown.
- Attacking and blocking reduce movement speed.
- While blocking or preparing an attack, the player faces camera-forward.

## Combat Direction Input

- The latest dominant mouse delta chooses Left, Right, Up, or Thrust.
- Horizontal movement selects Left or Right.
- Upward movement selects Up.
- Downward movement selects Thrust.
- Mouse movement below the threshold preserves the previous direction.
- The initial fallback direction is Right.
- While holding left mouse during wind-up or hold, continued mouse movement
  re-aims the attack. The direction commits when the button is released.
- While holding right mouse, mouse movement updates the block direction live.

## Camera

- Camera style is close third-person over-the-shoulder, never first-person.
- The complete player silhouette should remain visible during ordinary combat.
- Mouse orbit pitch is clamped between -12 and 36 degrees.
- Camera collision uses a sphere cast to avoid clipping through arena geometry.
- Movement adds subtle bob and sway.
- Impacts and dodges may add short camera shake.
- Mouse look is damped to 28% of normal sensitivity while left mouse is held,
  so direction flicks do not spin the camera. Damped to 55% while blocking.

## Camera Modes

| Mode | Distance | Field of View |
|---|---:|---:|
| Normal | 5.95 | 59 |
| Sprinting | 6.6 | 64 |
| Blocking | 5.45 | 56 |

