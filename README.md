# Conquer Others

A third-person medieval battle game built in Unity. It is built entirely from
runtime-generated low-poly primitives and curated CC0 assets, so it has no manual
scene setup.

The organized product and technical specification set starts at
[`specs/00-spec-index.md`](specs/00-spec-index.md).

## Play

Open `Assets/Scenes/SampleScene.unity` in Unity 6.3 LTS and press Play. The game
opens on the **campaign map**: click a glowing enemy territory bordering your
lands, compare its threat, reward, income, and arena, then click its assault
button. Each assault launches that territory's regional battle; click to begin
the fight.

Conquer every territory to win the campaign; if your captain falls, the campaign
is lost.

Spend gold on militia, veterans, and guards from the recruitment panel. Victory
earns conquest gold and income, while surviving unit types carry into the next
battle. Choose the captain's sword and shield, two-handed sword, or bow from
the equipment panel, and choose the Training Arena opponent's weapon there too.
The separate Training Arena node launches consequence-free 1v1 practice. See
[`specs/13-campaign-economy-units-and-regions.md`](specs/13-campaign-economy-units-and-regions.md).

Create a standalone build with `Conquer Others > Build Windows` in the
Unity menu. The executable is written to `Builds/Windows/ConquerOthers.exe`.

## Controls

| Input | Action |
|---|---|
| W / A / S / D | Move |
| Left Shift | Sprint |
| Space | Dodge |
| 1 / 2 / 3 / 4 | Order allies to Follow / Hold / Charge / Advance |
| F / H | Cycle formation / order allied archers to hold fire |
| Hold left mouse + move mouse | Aim a directional attack |
| Release left mouse | Strike in the aimed direction |
| Hold right mouse + move mouse | Block in that direction |
| Hold / release left mouse with bow | Draw while aiming / fire through crosshair |
| Escape | Pause battle (Resume or return to title) |

Bow shots begin inaccurate. Hold through the orange `DRAW` state; the reticle
contracts after the marked threshold and turns green with `STEADY` at maximum
precision.

The battle ends in victory when the enemy force is eliminated or in defeat when
the player dies. Army size, enemy quality, and battlefield layout depend on the
campaign territory.

While holding the left mouse button, move the mouse to choose left slash, right
slash, overhead, or thrust. The four ticks around the crosshair show your live
direction and fill gold as the swing charges. Release to strike.

A block stops all damage only when its direction matches the incoming attack and
the attacker is inside the defender's front arc. Raise or correct the matching
block at the last moment for a perfect block, then strike while the reticle is
gold to land a faster, stronger counter.

The camera uses a close over-the-shoulder combat view. Sprinting pulls it back
slightly for awareness, while blocking tightens the view toward nearby threats.

The `Conquer Others` Unity menu also includes a battle smoke test that launches
the match and captures representative gameplay states.

## Verification

Run the complete local verification gate from PowerShell:

```powershell
.\Tools\Verify.ps1
```

It runs EditMode and PlayMode tests, creates a Windows build, and runs headless
victory and 6v6 natural-combat standalone smoke tests. Pass `-SkipBuild` to
reuse an existing build or `-UnityEditorPath <path>` when Unity is installed
elsewhere.

Run `.\Tools\RunStandaloneSmokes.ps1` to verify an existing Windows build
without launching Unity. Verification is local-only because Unity batch builds
and tests require an activated editor license.

Standalone smoke screenshots are captured in interactive runs. Add
`-smokescreenshots` to explicitly request them in batch mode.
