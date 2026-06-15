# 07 - UI and Battle Lifecycle

## Ready Screen

The ready screen communicates:

- Game title and courtyard battle premise.
- Movement, dodge, directional attack, and directional block controls.
- The rule that matching the incoming direction stops damage.
- A mouse click begins the battle.

## Fighting HUD

- Bottom-left player panel with health and stamina bars.
- Top-right count of living blue and red fighters.
- Center crosshair with four directional tick marks:
  - Ticks are always visible, dimmed when idle.
  - The active direction tick brightens gold while attacking or aiming,
    cyan while blocking.
  - Ticks spread outward as an attack charges during wind-up and hold.
- Direction label below the crosshair:
  - `AIM LEFT/RIGHT/HIGH/THRUST` while no button is held.
  - `ATTACK LEFT/RIGHT/HIGH/THRUST` while preparing or holding a swing.
  - `BLOCK LEFT/RIGHT/HIGH/THRUST` while blocking.
- Temporary battle and block messages.
- A single primary-threat cue showing the incoming direction near the reticle.
- No directional text or telegraph bars are drawn above enemies.
- A gold counter-ready reticle and counter prompt after a perfect block.
- World-space health bars only for recently damaged fighters and the current
  primary threat.
- Red damage, gold block, and cyan perfect-block screen flashes.

## Result Screens

Victory and defeat screens show:

- Outcome title.
- Battle time.
- Battle statistics:
  - Damage dealt by the player versus damage dealt by allied soldiers.
  - Damage the player took.
  - Player kills.
  - Perfect blocks and landed counter strikes.
  - Blue and red losses (fallen / starting count).
- Return-to-map prompt.

## Cursor Rules

- Cursor is locked and hidden during active battle.
- Escape releases and shows the cursor.
- Clicking during battle locks the cursor again.
- Result screens unlock and show the cursor.

## Lifecycle Ownership

`BattleManager` owns state, battle timer, counts, outcomes, cursor state, target
queries, statistics, and combat feedback routing. `BattleHud` owns battle UI
rendering and delegates result confirmation back to `BattleManager`.
