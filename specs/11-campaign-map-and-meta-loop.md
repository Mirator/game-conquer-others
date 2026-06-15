# 11 - Campaign Map and Meta Loop

## Purpose

The campaign map is the strategic shell around the battle. The player, as a blue
captain, conquers a map of red-held territories by winning the courtyard battle
at each one. This fulfills the game's title and gives the battle a reason to
repeat.

## Campaign Model

- The map is a graph of 8 procedurally-placed `Territory` nodes.
- Each territory has an owner, garrison, threat, reward, income, and arena type.
- Territories are connected by adjacency edges. The graph is always fully
  connected, so every territory is eventually reachable.
- One territory starts owned by the player; the rest start enemy-owned.
- The player keeps a persistent mixed-unit warband carried between battles. It
  starts with 3 militia and can grow to 12 soldiers through recruitment.

## Map Rules

- A territory is **attackable** when it is not player-owned and borders at least
  one player-owned territory.
- Selecting an attackable territory and confirming launches a battle.
- The captain equipment panel selects the player's persistent weapon.
- A separate Training Arena node launches a consequence-free 1v1. The player
  chooses their weapon from captain equipment and chooses the opponent weapon
  in the training setup panel.
- The battle is parameterized by the encounter:
  - Allied soldiers spawned = the current roster (clamped to the arena cap).
  - Enemy soldiers spawned = the target's garrison.
  - Enemy quality and health scale = the target's threat.
  - Arena layout = the target's regional arena type.

## Battle Outcome

- **Victory**: the target territory becomes player-owned. Surviving allied unit
  types persist back to the roster; allied deaths are permanent. The player
  earns conquest gold plus income from all owned lands.
- **Defeat** (player dies): the campaign is lost.
- **Training result**: returns to the map without changing campaign economy,
  roster, territory ownership, or defeat state.
- The player clicks the result button to apply the outcome and return to the map.

## Win and Loss

- **Campaign victory**: every territory is player-owned.
- **Campaign defeat**: the player dies in any battle.
- Both end screens offer R to begin a fresh campaign.

## Map Controls

| Input | Action |
|---|---|
| Left mouse | Select a territory |
| R (on an end screen) | Begin a new campaign |

## Presentation

- The map is runtime-generated in the same low-poly style as the battle, reusing
  shared materials from `RuntimeAssets`.
- Overhead camera over a table-like ground plane.
- Territory nodes are colored by owner (blue / red / grey); attackable enemy
  nodes pulse. Adjacency edges are drawn between nodes.
- An IMGUI HUD shows gold, income, typed roster, conquered count, latest report,
  recruitment controls, target risk/reward, and the assault prompt.

## Future Campaign Features

Neutral territories, enemy counter-attacks, pre-battle events, settlement
upgrades, trading, and saving the campaign are outside the current slice.
