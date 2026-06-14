# 02 - Battle Loop and Rules

A battle is one encounter launched from the campaign map (see
[11-campaign-map-and-meta-loop.md](11-campaign-map-and-meta-loop.md)). This
document covers a single battle; the campaign map owns the higher-level loop.

## Core Loop

1. The game presents the ready screen.
2. The player begins the battle with Enter or a mouse click.
3. Blue and red fighters move toward opposing targets.
4. The player moves, attacks, blocks, dodges, and fights beside allies.
5. Fighters lose health and die.
6. The battle ends in victory or defeat.
7. The player presses R to rebuild and restart the battle.

## Teams

- Blue team: player plus three allied AI fighters.
- Red team: four enemy AI fighters.
- Fighters may only target and damage the opposing team.
- Friendly fire is disabled.

## Battle States

| State | Meaning |
|---|---|
| Ready | Battle is built but simulation has not begun. |
| Fighting | Movement, AI, combat, and battle timer are active. |
| Victory | All red-team fighters are dead. |
| Defeat | The player is dead. |

## Win and Loss Rules

- Victory occurs when no enemy fighter remains alive.
- Defeat occurs immediately when the player dies.
- Allied deaths alone do not trigger defeat.

## Restart Rule

Pressing R rebuilds the current battle (same encounter setup) in the Ready state
with restored fighters, UI, camera, arena, and effects. Dismissing a result
screen instead returns to the campaign map.

## Current Balance Context

- Player health: 125.
- Allied AI health: 110.
- Enemy AI health: 100 (multiplied by the encounter's enemy health scale).
- Maximum stamina: 100.
- Battle size is parameterized by the encounter: blue fighters = player + roster,
  red fighters = the target territory's garrison. The opening encounter defaults
  to 4 blue versus 4 red.

