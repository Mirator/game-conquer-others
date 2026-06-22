# 07 - UI and Battle Lifecycle

## Title Screen

The game opens on a persistent `FrontendUi` title screen showing:

- The title `CONQUER OTHERS` and a subtitle.
- `CONTINUE`, shown only when a save exists
  (`GameDirector.HasSavedCampaign` / `CampaignSaveService`).
- `NEW CAMPAIGN`, `CUSTOM BATTLE`, `SETTINGS`, and `QUIT`.

`CUSTOM BATTLE` opens a sandbox setup screen for configuring a one-off fight —
the number of melee allies, allied archers, and enemies (up to the battlefield
deployment cap), the arena biome, and the player's weapon. Launching it
(`GameDirector.LaunchCustomBattle`) starts the battle directly from the title and
touches no campaign state: when the fight concludes the game returns to the title
without applying victory or defeat. It is a testing/exploration tool, especially
for large commanded battles and formation/hold-fire orders.

A battle can be paused with Escape to a `BATTLE PAUSED` screen offering Resume,
Settings, Return to Title, and Quit.

## Ready Screen

The ready screen communicates:

- A per-encounter title from `BattleHud`: `TRAINING ARENA`,
  `ROUT THE <enemy>` for a bandit field battle, or `ASSAULT ON <hold>` for a
  settlement assault.
- The equipped weapon.
- Movement, dodge, and the directional attack or bow controls for the loadout.
- A mouse click begins the battle.

## Fighting HUD

- Bottom-left player panel with health and stamina bars. The bars ease toward
  their value instead of snapping, and the health bar carries a lagging "chip"
  layer that exposes the slice just lost.
- Top-right count of living blue and red fighters.
- Top-left active ally order and `1 Follow / 2 Hold / 3 Charge / 4 Advance`
  controls during campaign battles.
- A single center `+` reticle:
  - It reads `DRAW` while a bow charges and `STEADY` once the bow shot is
    precise.
  - It reads `COUNTER` while a perfect-block counter window is open.
  - It shows no directional tick marks, no `AIM/ATTACK/BLOCK` direction label,
    and no primary-threat incoming-direction cue.
- Temporary battle and block messages that fade in with a brief scale punch and
  fade out at the end of their timer.
- No directional text or telegraph bars are drawn above enemies.
- World-space health bars only for recently damaged fighters and the current
  primary threat.
- World-space floating combat readouts on player-involved hits — a damage number
  and the short cues `PARRY!`, `BLOCK`, and `GUARD BROKEN` — gated behind the
  `showDamageNumbers` setting (default on).
- Red damage, gold block, and cyan perfect-block screen flashes.
- The `reduceMotion` setting snaps the animated bars and messages to their value
  instead of easing them.

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
- Escape opens the pause screen and releases/shows the cursor while paused.
- Clicking during battle locks the cursor again.
- Result screens unlock and show the cursor.

## Lifecycle Ownership

`BattleManager` owns state, battle timer, counts, outcomes, cursor state, target
queries, statistics, and combat feedback routing. `BattleHud` owns battle UI
rendering and delegates result confirmation back to `BattleManager`.

`CampaignMapController` owns the overworld HUD. It keeps a slim top resource strip
(with the day/night dial) and a slim bottom action panel permanently on screen, and
hosts the Recruit, Promote, and Captain Equipment panels as on-demand overlays
summoned from a bottom icon toolbar — a single open-panel field shows only one at a
time. `RefreshUi` rebuilds only the open panel's text; opening a panel marks the HUD
dirty so it refreshes that frame.
