# Conquer Others

A playable Unity MVP for a small third-person medieval battle. The prototype is
built entirely from runtime-generated low-poly primitives, so it has no external
asset dependencies or manual scene setup.

The organized product and technical specification set starts at
[`specs/00-spec-index.md`](specs/00-spec-index.md).

## Play

Open `Assets/Scenes/SampleScene.unity` in Unity 6.3 LTS and press Play. Press
Enter or click on the title screen to begin the battle.

Create a standalone build with `Conquer Others > Build Windows MVP` in the
Unity menu. The executable is written to `Builds/Windows/ConquerOthers.exe`.

## Controls

| Input | Action |
|---|---|
| W / A / S / D | Move |
| Mouse | Rotate camera and select combat direction |
| Left Shift | Sprint |
| Space | Dodge |
| Hold/release left mouse | Prepare/release directional attack |
| Hold right mouse | Block in selected direction |
| Escape | Release cursor |
| R | Restart battle |

Defeat all four red soldiers while fighting alongside the three blue allied
soldiers. The battle ends in victory when the enemy force is eliminated or in
defeat when the player dies.

Combat follows `conquer_others_directional_combat_canvas.md`. Recent dominant
mouse movement selects left slash, right slash, overhead, or thrust. A block
stops all damage only when its direction matches the incoming attack and the
attacker is inside the defender's front arc.

The camera uses a close over-the-shoulder combat view. Sprinting pulls it back
slightly for awareness, while blocking tightens the view toward nearby threats.

The `Conquer Others` Unity menu also includes a battle smoke test that launches
the match and captures representative gameplay states.
