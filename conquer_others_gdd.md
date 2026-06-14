# Game Design Document: Conquer Others

## 1. High Concept

**Conquer Others** is a 3D medieval action-combat prototype inspired by *Mount & Blade*. The player controls a warrior in third-person view, fights enemies using melee weapons, commands a small group of allied soldiers, and wins battles by defeating the opposing force.

The goal of the Proof of Concept is not to build a full medieval sandbox, but to validate whether the core battle gameplay feels promising.

---

## 2. Project Goal

Build a small playable 3D prototype in **Unity** that proves the following:

- Third-person medieval movement feels acceptable.
- Melee combat is understandable and fun.
- Basic enemy AI can attack, block, chase, and die.
- Small group battles are technically possible.
- The game direction has enough potential to continue development.

---

## 3. Target Platform

### PoC Platform

- PC
- Keyboard and mouse
- Unity 3D

### Possible Future Platforms

- PC
- Steam
- Possibly console later, but not relevant for the PoC

---

## 4. Target Audience

Players who enjoy:

- Medieval combat
- Third-person action games
- Tactical battles
- Light army management
- Games like *Mount & Blade*, *Chivalry*, *Mordhau*, and small-scale tactical RPGs

---

## 5. Core Fantasy

The player starts as a simple warrior and gradually becomes a battlefield leader.

For the PoC, the core fantasy is:

> “I am a medieval fighter leading a small group of soldiers into battle and personally influencing the result through skillful combat.”

---

## 6. PoC Scope

### Included in PoC

- One small battlefield arena
- One playable character
- One enemy faction
- Basic melee weapons
- Basic third-person movement
- Basic melee attacks
- Basic blocking
- Basic health and damage system
- 3–5 allied soldiers
- 3–5 enemy soldiers
- Simple win/lose condition
- Basic UI for health and battle result

### Not Included in PoC

- Open world campaign map
- Economy
- Quests
- Diplomacy
- Castle sieges
- Large armies
- Multiplayer
- Character progression
- Inventory system
- Complex equipment system
- Mounted combat, unless time allows

---

## 7. Core Gameplay Loop

1. Player enters a battlefield.
2. Player moves toward enemies.
3. Player fights using melee attacks and blocking.
4. Allied and enemy soldiers fight around the player.
5. Characters lose health and die.
6. Battle ends when one side is defeated.
7. Game shows victory or defeat screen.
8. Player can restart the battle.

---

## 8. Player Controls

### Movement

| Input | Action |
|---|---|
| W / A / S / D | Move character |
| Mouse | Rotate camera |
| Shift | Sprint |
| Space | Jump or dodge, depending on implementation |
| Left Mouse Button | Attack |
| Right Mouse Button | Block |
| Mouse movement + attack | Choose attack direction, optional |
| Tab | Show battle status, optional |
| R | Restart battle |

---

## 9. Combat System

### Minimum Combat System

For the first version, combat should include:

- Light melee attack
- Block
- Health points
- Damage
- Death animation or ragdoll
- Hit detection with weapon collider or animation event

### Preferred Combat System

If time allows, add directional combat:

- Overhead attack
- Left attack
- Right attack
- Stab
- Directional block

### Combat Design Principles

- Combat should be readable.
- Attacks should have clear wind-up and impact.
- Blocking should feel useful but not overpowered.
- Player should be able to defeat enemies through timing, positioning, and blocking.
- One-on-one combat should work before group combat is expanded.

---

## 10. Player Character

The player is a simple medieval fighter.

### PoC Stats

| Attribute | Value |
|---|---:|
| Health | 100 |
| Damage | 25 |
| Movement Speed | Medium |
| Sprint Speed | Fast |
| Weapon | Sword |
| Shield | Optional |

### Required Animations

- Idle
- Walk
- Run
- Attack
- Block
- Hit reaction
- Death

---

## 11. Enemy AI

Enemies should be simple but functional.

### Enemy Behavior

Enemy soldiers should:

- Detect nearby opponents
- Move toward target
- Stop at attack range
- Attack repeatedly
- Occasionally block
- Die when health reaches zero

### Basic AI States

- Idle
- Searching for target
- Chasing target
- Attacking
- Blocking
- Dead

### PoC AI Goal

The AI does not need to be smart. It only needs to create the feeling of a small medieval battle.

---

## 12. Allied Soldiers

Allied soldiers use the same AI as enemies, but target enemy units.

### PoC Behavior

Allies should:

- Follow the player at battle start, optional
- Engage enemies when close
- Fight independently
- Die normally

### Optional Command System

If time allows, add simple commands:

| Command | Description |
|---|---|
| Follow me | Allies stay near the player |
| Charge | Allies attack nearest enemy |
| Hold position | Allies stay in place |

For the first PoC, this can be skipped.

---

## 13. Battlefield

### PoC Battlefield

A small enclosed medieval arena.

### Required Elements

- Flat terrain
- Some obstacles
- Spawn area for player team
- Spawn area for enemy team
- Simple medieval props
- Clear battlefield boundaries

### Optional Elements

- Wooden fences
- Castle walls
- Watchtowers
- Village houses
- Hills or terrain variation

---

## 14. Visual Style

### Recommended Style

Low-poly medieval 3D.

### Reason

Low-poly assets are easier to combine, cheaper to produce, and more suitable for a solo prototype.

### Asset Sources

Recommended free asset sources:

- Quaternius
- Kenney
- Mixamo
- KayKit free assets
- Unity Starter Assets

---

## 15. Camera

Third-person camera behind the player.

### Requirements

- Camera follows player smoothly.
- Mouse controls camera direction.
- Camera should not feel too close.
- Player should see enemies clearly.
- Camera should not break during combat.

---

## 16. UI

### Required UI

- Player health bar
- Enemy/allied unit count
- Victory screen
- Defeat screen
- Restart button or key prompt

### Optional UI

- Crosshair or attack direction indicator
- Damage numbers
- Ally command display
- Minimap

---

## 17. Technical Design

### Engine

Unity 3D

### Recommended Packages

- Unity Starter Assets - Third Person Controller
- Cinemachine
- Unity Input System
- Unity AI Navigation / NavMesh
- Animation Rigging, optional

### Suggested Architecture

Core scripts:

- `PlayerController`
- `CharacterStats`
- `CombatController`
- `WeaponHitbox`
- `Health`
- `AIController`
- `TeamManager`
- `BattleManager`
- `UIManager`

### Teams

Each character belongs to a team:

- Player Team
- Enemy Team

Characters search for targets from the opposite team.

---

## 18. Battle Win/Lose Conditions

### Victory

The player wins when all enemy units are dead.

### Defeat

The player loses when:

- Player dies, or
- All allied units die, optional

For the first PoC, the player dying should trigger defeat.

---

## 19. Development Milestones

### Milestone 1: Movement Prototype

Goal: Player can move in third person.

Deliverables:

- Player character
- Camera
- Movement
- Sprinting
- Simple test arena

---

### Milestone 2: One-on-One Combat

Goal: Player can fight one enemy.

Deliverables:

- Sword attack
- Health system
- Damage system
- Enemy death
- Basic enemy AI

---

### Milestone 3: Small Group Battle

Goal: Small battle works.

Deliverables:

- 3 allied soldiers
- 3 enemy soldiers
- AI target selection
- Team-based combat
- Victory/defeat condition

---

### Milestone 4: Combat Feel Improvement

Goal: Battle becomes more readable and fun.

Deliverables:

- Better attack timing
- Hit reactions
- Blocking
- Improved camera
- Basic UI

---

### Milestone 5: PoC Polish

Goal: Create a small playable demo.

Deliverables:

- Start screen
- Restart flow
- Simple battlefield environment
- Basic sound effects
- Clear win/lose screen

---

## 20. Success Criteria

The PoC is successful if:

- The player can move and fight without major bugs.
- One-on-one combat is understandable.
- Small group battles work with at least 6–10 characters.
- The battle has a clear beginning and end.
- The prototype feels promising enough to expand.

The PoC is not required to look beautiful or have deep systems.

---

## 21. Main Risks

### Risk 1: Combat Feels Bad

Melee combat may feel clunky.

Mitigation:

- Start with very simple combat.
- Tune attack speed, range, and animations.
- Focus on readability before complexity.

### Risk 2: AI Is Too Stupid

Enemies may behave awkwardly.

Mitigation:

- Keep AI simple.
- Use clear states.
- Avoid complex tactics in the PoC.

### Risk 3: Scope Gets Too Big

Mount & Blade-like games are very large.

Mitigation:

- No campaign map in PoC.
- No economy.
- No inventory.
- No multiplayer.
- No big armies.
- Focus only on battle gameplay.

### Risk 4: Asset Integration Takes Too Long

Free assets may have different rigs, scales, and styles.

Mitigation:

- Use one main visual style.
- Prefer low-poly assets.
- Use placeholder animations.
- Do not chase visual quality early.

---

## 22. Future Expansion Ideas

Only after the PoC works:

- Mounted combat
- Larger battles
- Formations
- Recruitment
- Character progression
- Equipment system
- Campaign map
- Settlements
- Factions
- Quests
- Economy
- Castle battles

---

## 23. PoC Definition of Done

The PoC is done when the player can:

1. Launch the game.
2. Enter a small medieval battlefield.
3. Move in third person.
4. Attack and block.
5. Fight alongside allied soldiers.
6. Defeat enemy soldiers.
7. See a victory or defeat result.
8. Restart the battle.

The final PoC should be a small but complete battle demo, not a full game.
