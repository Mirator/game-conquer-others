# 07 - UI and Battle Lifecycle

## Ready Screen

The ready screen communicates:

- Game title and courtyard battle premise.
- Movement, dodge, directional attack, and directional block controls.
- The rule that matching the incoming direction stops damage.
- Enter or click begins the battle.

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
- World-space health bars for damaged non-player fighters.
- Red screen flash when the player takes damage.

## Result Screens

Victory and defeat screens show:

- Outcome title.
- Battle time.
- Remaining blue and red fighter counts.
- R restart prompt.

## Cursor Rules

- Cursor is locked and hidden during active battle.
- Escape releases and shows the cursor.
- Clicking during battle locks the cursor again.
- Result screens unlock and show the cursor.

## Lifecycle Ownership

`BattleManager` owns state, battle timer, counts, outcomes, UI rendering, cursor
state, target queries, and combat feedback routing.

