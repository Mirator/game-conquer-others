# 08 - Runtime Architecture and Build

## Runtime Construction

The MVP requires no manual scene setup beyond loading `SampleScene.unity`.
`BattleBootstrap` initializes after scene load and creates the complete battle:

- Battle root.
- Manager and effects systems.
- Lighting and arena.
- Camera and camera rig.
- Player and AI fighters.
- Optional standalone smoke runner.

Restart destroys the battle root and repeats this construction.

## Main Runtime Components

| Component | Responsibility |
|---|---|
| `BattleBootstrap` | Builds and resets the complete runtime battle. |
| `BattleManager` | Owns battle rules, queries, lifecycle, and UI. |
| `BattleFighter` | Shared fighter state, combat, health, and visuals. |
| `PlayerFighter` | Player input, movement, dodge, and direction selection. |
| `AIFighter` | Targeting, movement, attacks, and blocks. |
| `ThirdPersonCamera` | Orbit, collision, framing, FOV, and shake. |
| `BattleEffects` | Procedural audio and impact sparks. |
| `BattleRuntimeSmoke` | Standalone automated battle verification. |

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

