# 02 - Battle Loop and Rules

A battle is one encounter launched from the campaign map (see
[11-campaign-map-and-meta-loop.md](11-campaign-map-and-meta-loop.md)). This
document covers a single battle; the campaign map owns the higher-level loop.

## Core Loop

1. The game presents the ready screen.
2. The player begins the battle with a mouse click.
3. Blue and red fighters move toward opposing targets.
4. The player moves, attacks, blocks, dodges, and fights beside allies.
5. Fighters lose health and die.
6. The battle ends in victory or defeat.
7. The player confirms the result and returns to the campaign map.

## Teams

- Blue team: the player plus the allied warband (the campaign roster), scaling
  with the player's leadership cap.
- Red team: the enemy force — a hold's garrison or a bandit band — which can
  outnumber the warband.
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

## Campaign Consequences

Battles cannot be restarted. The result must be confirmed so defeat and allied
casualties are applied to the campaign.

## Current Balance Context

- Player health: 125.
- Allied AI health: 110.
- Enemy AI health: 100 (multiplied by the encounter's enemy health scale).
- Maximum stamina: 100.
- Battle size is parameterized by the encounter: blue fighters = player + the
  allied roster (capped by leadership, up to 24), red fighters = the hold's
  garrison or the bandit band's strength. Either side may field up to the
  per-side deployment ceiling of 60, so the player can be outnumbered. The first
  fight is whatever the player marches into; with the starting warband of three
  militia that is 4 blue against the chosen target's strength.
