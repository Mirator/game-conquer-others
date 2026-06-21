# 08 - Runtime Architecture and Build

## Runtime Construction

The game requires no manual scene setup beyond loading `SampleScene.unity`.
`GameDirector` is the single entry point that initializes after scene load. It is
a persistent singleton (`DontDestroyOnLoad`) that:

- Disables the scene's default camera and light exactly once at startup.
- Owns the persistent `CampaignState`.
- Switches between three modes, each under its own root GameObject:
  - `frontendRoot` — the persistent title/pause/settings UI (`FrontendUi`).
  - `mapRoot` — the campaign map (`CampaignMapController`).
  - `battleRoot` — a battle built by `BattleBootstrap`.
- Opens on the title screen; smoke-test launches skip straight to the map.
- Persists and restores the campaign through `CampaignSaveService` (PlayerPrefs)
  on mode changes and quit, and deletes the save when a campaign ends.

A battle root contains the manager and effects systems, lighting and arena,
camera and camera rig, and the player and AI fighters. All mode changes go
through one transition that destroys the current root, waits one frame, then
builds the next — so there is never more than one active camera or audio
listener. Each mode's camera carries the single active `AudioListener`.

## Main Runtime Components

| Component | Responsibility |
|---|---|
| `GameDirector` | Entry point; owns campaign state and title/map/battle mode switching, including a consequence-free custom battle launched from the title screen. |
| `CampaignState` | Persistent territory graph, economy, mixed-unit warband, and progression rules (`Territory.cs` also defines `EnemyParty` and the `Settlement` size class). |
| `CampaignTypes` | Unit catalog, unit roster, weapon catalog, and arena-type definitions; also defines `Archetype` + `ArchetypeCatalog`, the `BattleKind` enum, the `SettlementType` enum + `SettlementCatalog`, and the per-(tier x archetype) `RosterEntry` (which banks battle experience). |
| `OverworldSimulation` | Pure deterministic overworld travel and enemy-party simulation. |
| `CampaignSaveService` | PlayerPrefs campaign save/load/delete (save v5). |
| `CampaignMapController` | Builds and runs the campaign map view, camera, and UI. |
| `AIProfile` | Archetype behavior presets layered on a fighter's unit stats and weapon. |
| `CombatBalance` / `CombatBalanceData` | Static tuning facade over an optional Resources `ScriptableObject` with baked defaults. |
| `FormationBalance` / `FormationBalanceData` | Same facade pattern for formation spacing, speeds, and advance tuning. |
| `Formation` | Pure, allocation-free formation slot geometry (`FormationShape` → captain-relative offset). |
| `SpatialHashGrid` | Uniform XZ grid for near-O(1) neighbour queries (separation, proximity telemetry). |
| `FrontendUi` | Persistent title, pause, and settings UI. |
| `BattleBootstrap` | Builds a battle (arena, fighters) under a supplied root. |
| `BattleManager` | Public battle facade; owns lifecycle, combat queries, statistics, and feedback state. |
| `BattleHud` | Renders ready, fighting, and result battle UI from the manager facade. |
| `BattleTactics` | Owns AI target distribution, attack permissions, engagement slots, and telemetry. |
| `BattleDiagnostics` | Owns deterministic combat checks used by smoke tooling. |
| `BattleFighter` | Shared fighter state, combat, health, and movement. |
| `BattleFighterPresentation` | Builds and animates the procedural fighter model. |
| `PlayerFighter` | Player input, movement, dodge, and direction selection. |
| `AIFighter` | Targeting, movement, attacks, and blocks. |
| `ThirdPersonCamera` | Orbit, collision, framing, FOV, and shake. |
| `BattleEffects` | Procedural audio and impact sparks. |
| `RuntimeAssets` | Caches and shares generated materials and procedural audio clips. |
| `BattleRuntimeSmoke` | Standalone automated campaign-step verification. |

## Rendering and Assets

- Models, arena props, effects, and audio are generated at runtime.
- Generated materials and procedural audio clips are cached and shared across
  map and battle rebuilds.
- The project supports an active render pipeline or built-in renderer fallback.
- Four shaders are always included for standalone procedural material
  reliability: Standard, Legacy Shaders/Diffuse, Sprites/Default, and
  Skybox/Procedural (the last for the procedural skybox).

## Build

- Editor menu: `Conquer Others > Build Windows MVP`.
- Target: Windows 64-bit standalone.
- Scene: `Assets/Scenes/SampleScene.unity`.
- Output: `Builds/Windows/ConquerOthers.exe`.
- Build and standalone smoke verification run locally because Unity batch
  builds and tests require an activated editor license.

Other editor tooling:

- `Conquer Others > Create Combat Balance Asset` (`CombatBalanceAssetTool`).
- `Conquer Others > Create Formation Balance Asset` (`FormationBalanceAssetTool`).
- `Conquer Others > Wire Survival Kit Props` (`SurvivalKitImporter`, also
  runnable headless via `-executeMethod`).

## Design Constraint

New MVP systems should preserve the runtime-bootstrap approach unless a
deliberate migration to authored scenes and imported assets is approved.
