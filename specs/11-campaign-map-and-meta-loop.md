# 11 - Campaign Map and Meta Loop

## Purpose

The campaign map is the strategic shell around the battle. The player, as a blue
captain, conquers a map of red-held territories by winning the courtyard battle
at each one. This fulfills the game's title and gives the battle a reason to
repeat.

## Campaign Model

- The map is a graph of 5 to 8 procedurally-placed `Territory` nodes.
- Each territory has an owner (Player, Enemy, or Neutral) and a garrison size.
- Territories are connected by adjacency edges. The graph is always fully
  connected, so every territory is eventually reachable.
- One territory starts owned by the player; the rest start enemy-owned.
- The player keeps a persistent warband **roster**: the count of allied soldiers
  carried between battles. It starts at 3.

## Map Rules

- A territory is **attackable** when it is not player-owned and borders at least
  one player-owned territory.
- Selecting an attackable territory and confirming launches a battle.
- The battle is parameterized by the encounter:
  - Allied soldiers spawned = the current roster (clamped to the arena cap).
  - Enemy soldiers spawned = the target's garrison.
  - Enemy health scale = the target's difficulty scale (1.0 in the first slice).

## Battle Outcome

- **Victory**: the target territory becomes player-owned. Surviving allied
  soldiers persist back to the roster; allied deaths are permanent.
- **Defeat** (player dies): the campaign is lost.
- The player dismisses the result screen with Enter to apply the outcome and
  return to the map.

## Win and Loss

- **Campaign victory**: every territory is player-owned.
- **Campaign defeat**: the player dies in any battle.
- Both end screens offer R to begin a fresh campaign.

## Map Controls

| Input | Action |
|---|---|
| Left mouse | Select a territory |
| Enter | Assault the selected attackable territory |
| R (on an end screen) | Begin a new campaign |

## Presentation

- The map is runtime-generated in the same low-poly style as the battle, reusing
  `BattleBootstrap.CreateMaterial`.
- Overhead camera over a table-like ground plane.
- Territory nodes are colored by owner (blue / red / grey); attackable enemy
  nodes pulse. Adjacency edges are drawn between nodes.
- An IMGUI HUD shows the roster, conquered count, the selected territory, and the
  assault prompt, matching the battle HUD style.

## Future Campaign Features

Recruitment and roster growth, garrison and difficulty escalation by distance,
neutral territories, enemy counter-attacks, pre-battle events, and saving the
campaign are outside the current slice.
