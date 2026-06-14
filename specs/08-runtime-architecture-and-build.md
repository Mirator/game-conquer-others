# 08 - Runtime Architecture and Build

## Runtime Construction

The game requires no manual scene setup beyond loading `SampleScene.unity`.
`GameDirector` is the single entry point that initializes after scene load. It is
a persistent singleton (`DontDestroyOnLoad`) that:

- Disables the scene's default camera and light exactly once at startup.
- Owns the persistent `CampaignState`.
- Switches between two modes, each under its own root GameObject:
  - `mapRoot` — the campaign map (`CampaignMapController`).
  - `battleRoot` — a battle built by `BattleBootstrap`.
- Opens on the campaign map.

A battle root contains the manager and effects systems, lighting and arena,
camera and camera rig, and the player and AI fighters. All mode changes go
through one transition that destroys the current root, waits one frame, then
builds the next — so there is never more than one active camera or audio
listener. Each mode's camera carries the single active `AudioListener`.

## Main Runtime Components

| Component | Responsibility |
|---|---|
| `GameDirector` | Entry point; owns campaign state and map/battle mode switching. |
| `CampaignState` | Persistent territory graph, economy, mixed-unit warband, and progression rules. |
| `CampaignTypes` | Unit catalog, unit roster, and arena-type definitions. |
| `CampaignMapController` | Builds and runs the campaign map view and UI. |
| `BattleBootstrap` | Builds a battle (arena, fighters) under a supplied root. |
| `BattleManager` | Owns battle rules, queries, lifecycle, and UI. |
| `BattleFighter` | Shared fighter state, combat, health, and visuals. |
| `PlayerFighter` | Player input, movement, dodge, and direction selection. |
| `AIFighter` | Targeting, movement, attacks, and blocks. |
| `ThirdPersonCamera` | Orbit, collision, framing, FOV, and shake. |
| `BattleEffects` | Procedural audio and impact sparks. |
| `BattleRuntimeSmoke` | Standalone automated campaign-step verification. |

## Rendering and Assets

- Models, arena props, materials, effects, and audio are generated at runtime.
- The project supports an active render pipeline or built-in renderer fallback.
- Standard, Legacy Diffuse, and Sprites/Default shaders are always included for
  standalone procedural material reliability.

## Build

- Editor menu: `Conquer Others > Build Windows MVP`.
- Target: Windows 64-bit standalone.
- Scene: `Assets/Scenes/SampleScene.unity`.
- Output: `Builds/Windows/ConquerOthers.exe`.

## Design Constraint

New MVP systems should preserve the runtime-bootstrap approach unless a
deliberate migration to authored scenes and imported assets is approved.
