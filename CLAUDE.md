# CLAUDE.md

## Overview

Conquer Others is a third-person medieval battle MVP built in **Unity 6000.3.16f1**
(Unity 6.3 LTS) with URP and C#.
There are **no prefabs and no manual scene wiring** — every GameObject, mesh,
material, and UI element is generated at runtime from primitives.
On top of the moment-to-moment battle loop sits a **campaign meta-loop**: a map of
territories, an economy, recruitment, and threat scaling.
All code lives in the **global namespace** (there are zero `namespace` declarations)
inside a single runtime assembly, `ConquerOthers.Runtime`.
The authoritative product/tech specs live under [specs/](specs/), starting with the
index at [specs/00-spec-index.md](specs/00-spec-index.md).

## Build / test / verify

The full local gate is `.\Tools\Verify.ps1` (PowerShell).
Pass `-SkipBuild` to run only the test suites without producing a player build.
Verification runs Unity in batchmode and therefore **requires an activated Unity
license**; it only runs locally.
There is **no CI** — a GitHub Actions workflow was deliberately removed, so all
verification is manual on a licensed machine.

## Directory map

- [Assets/Scripts/](Assets/Scripts/) — runtime gameplay (battle loop, fighters, AI, HUD, effects).
- [Assets/Scripts/Campaign/](Assets/Scripts/Campaign/) — campaign meta-loop (map, territories, economy, state).
- [Assets/Editor/](Assets/Editor/) — [MvpBuilder.cs](Assets/Editor/MvpBuilder.cs) (menu: `Conquer Others > Build Windows MVP`) and [BattleSmokeRunner.cs](Assets/Editor/BattleSmokeRunner.cs) (menu smoke test). Editor-only; never shipped in player builds.
- [Assets/Tests/EditMode/](Assets/Tests/EditMode/) — fast logic tests (`[Test]`), no MonoBehaviour lifecycle.
- [Assets/Tests/PlayMode/](Assets/Tests/PlayMode/) — tests needing GameObjects/coroutines (`[UnityTest]`).
- [Tools/Verify.ps1](Tools/Verify.ps1), [Tools/RunStandaloneSmokes.ps1](Tools/RunStandaloneSmokes.ps1) — local verification gate.
- [specs/](specs/) — authoritative product/tech specs, numbered 00–15.

## Key classes

All runtime classes live in [Assets/Scripts/](Assets/Scripts/):

- [BattleManager.cs](Assets/Scripts/BattleManager.cs) — public gameplay facade; owns
  the fighter registry, the battle state machine (`Ready/Fighting/Victory/Defeat`),
  telemetry, and ally commands.
- [BattleFighter.cs](Assets/Scripts/BattleFighter.cs) — abstract base for all
  combatants: health, stamina, the attack phase machine, and the
  hit/block/perfect-block/counter rules in `ReceiveHitInternal` (around line 245).
  `PlayerFighter` and `AIFighter` derive from it.
- [BattleTactics.cs](Assets/Scripts/BattleTactics.cs) — AI target distribution,
  attack-permission slots, and separation.
- [BattleFighterPresentation.cs](Assets/Scripts/BattleFighterPresentation.cs) /
  [BattleEffects.cs](Assets/Scripts/BattleEffects.cs) /
  [BattleHud.cs](Assets/Scripts/BattleHud.cs) — visuals and UI, kept separate from
  the simulation.
- [BattleDiagnostics.cs](Assets/Scripts/BattleDiagnostics.cs) — deterministic
  combat-rule audits used by the smoke tests.
- [Campaign/CampaignState.cs](Assets/Scripts/Campaign/CampaignState.cs) — campaign
  data plus `CreateDefault`, `Recruit`, `ApplyVictory`, and threat scaling.
  See also [Campaign/Territory.cs](Assets/Scripts/Campaign/Territory.cs) and
  [Campaign/CampaignTypes.cs](Assets/Scripts/Campaign/CampaignTypes.cs)
  (`UnitType`, `WeaponType`, `ArenaType`, `UnitCatalog`, `WeaponCatalog`).

## Conventions

Match the existing source:

- Classes are `sealed` unless meant to be inherited (`BattleFighter` is `abstract`).
- 4-space indentation; `private` written explicitly; camelCase private fields with **no** underscore prefix; PascalCase for types, methods, and properties.
- Expression-bodied members and `switch` expressions are used liberally.
- Each class file opens with a short `//` comment describing its role — see the header of [BattleTactics.cs](Assets/Scripts/BattleTactics.cs) (lines 5–6). Match this.
- Tests set private fields via reflection when needed — see `SetTarget` in [Assets/Tests/PlayMode/CombatRulesTests.cs](Assets/Tests/PlayMode/CombatRulesTests.cs) (lines 114–117). Combat tests drive `Debug*` hooks exposed on `BattleFighter` (e.g. `DebugSetBlock`, `DebugPrepareAttack`, `DebugRestoreHealth`).

## Unity traps

- Every `.cs` file (and every asset) has a sibling `.meta` file that Unity manages. **Do not hand-author `.meta` files** — Unity regenerates them on import. When you add a new script without a running editor, note that its `.meta` will be created on the next editor open.
- New runtime scripts belong under [Assets/Scripts/](Assets/Scripts/) so they join the `ConquerOthers.Runtime` assembly automatically (defined by [Assets/Scripts/ConquerOthers.Runtime.asmdef](Assets/Scripts/ConquerOthers.Runtime.asmdef)). Test code referencing them must live under [Assets/Tests/EditMode/](Assets/Tests/EditMode/) or [Assets/Tests/PlayMode/](Assets/Tests/PlayMode/) — those asmdefs already reference the runtime assembly.
- EditMode tests (`[Test]`) are pure logic with no MonoBehaviour lifecycle; PlayMode tests (`[UnityTest]`) are for anything needing GameObjects or coroutines. Pick the right folder accordingly.
- Verification requires an activated Unity license and runs locally only (batchmode). There is no CI.

## How to extend

- **Add a unit type:** extend `UnitType` and `UnitCatalog` in [Assets/Scripts/Campaign/CampaignTypes.cs](Assets/Scripts/Campaign/CampaignTypes.cs); recruitment and economy flow through `CampaignState.Recruit` / `CampaignState.ApplyVictory` in [Assets/Scripts/Campaign/CampaignState.cs](Assets/Scripts/Campaign/CampaignState.cs); add a covering case to [Assets/Tests/EditMode/CampaignAndCombatTests.cs](Assets/Tests/EditMode/CampaignAndCombatTests.cs).
- **Tune combat numbers:** damage, timing, and stamina live in [Assets/Scripts/BattleFighter.cs](Assets/Scripts/BattleFighter.cs) (`GetDamage`, `GetWindup`, `GetRelease`, `GetRecovery`, `PrepareAttack`, and the named consts at lines 47–54). Gesture feel is in [CombatGesture.cs](Assets/Scripts/CombatGesture.cs).
- **Add a combat-rule test:** drive the `Debug*` hooks on `BattleFighter`; model the test after [Assets/Tests/PlayMode/CombatRulesTests.cs](Assets/Tests/PlayMode/CombatRulesTests.cs).
