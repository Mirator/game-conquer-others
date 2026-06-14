# Combat System Canvas: Directional Attack + Directional Block

## 1. Feature Name

**Directional Melee Combat**

## 2. Game

**Conquer Others**

## 3. Purpose

The purpose of this feature is to create a skill-based medieval melee system inspired by *Mount & Blade*, where both attacking and defending depend on direction, timing, positioning, and weapon reach.

The player should not win only by clicking quickly. The player should win by reading the enemy, choosing the correct attack direction, blocking correctly, and punishing mistakes.

---

## 4. Core Design Goal

Create a melee combat system where:

- Attacks come from one of several directions.
- Blocks must match the incoming attack direction.
- Players can attack, block, feint, and reposition.
- Combat is readable in one-on-one fights.
- The same system can later scale into small group battles.

---

## 5. Combat Directions

The PoC uses **four attack directions** and **four block directions**.

| Direction | Attack Type | Block Type |
|---|---|---|
| Left | Left slash | Block left |
| Right | Right slash | Block right |
| Up | Overhead strike | Block high |
| Down / Forward | Thrust / stab | Block thrust |

---

## 6. Player Inputs

### Default Keyboard + Mouse Controls

| Input | Action |
|---|---|
| Mouse movement | Select attack/block direction |
| Left Mouse Button | Start attack |
| Hold Left Mouse Button | Hold attack preparation |
| Release Left Mouse Button | Release attack |
| Right Mouse Button | Block |
| Hold Right Mouse Button | Keep blocking |
| WASD | Move |
| Shift | Sprint |
| Space | Dodge/jump, optional |

---

## 7. Attack Direction Selection

### Recommended PoC Method

Attack direction is selected by recent mouse movement before or while pressing attack.

| Mouse Movement | Selected Attack |
|---|---|
| Mouse moved left | Left slash |
| Mouse moved right | Right slash |
| Mouse moved up | Overhead strike |
| Mouse moved down | Thrust / stab |

### Input Rule

When the player presses or holds **Left Mouse Button**, the game reads the latest dominant mouse movement direction.

Example:

- Player moves mouse left.
- Player presses Left Mouse Button.
- Character prepares a left slash.
- Player releases Left Mouse Button.
- Character performs left slash.

### Fallback Rule

If mouse movement is too small, use the previous attack direction or default to right slash.

---

## 8. Attack Phases

Each attack has four phases.

| Phase | Description |
|---|---|
| Wind-up | Character prepares the attack |
| Hold | Player may hold attack before release |
| Release | Weapon moves forward and can deal damage |
| Recovery | Character finishes attack and cannot immediately attack again |

### PoC Timing Values

| Attack | Wind-up | Release Window | Recovery | Damage |
|---|---:|---:|---:|---:|
| Left slash | 0.35s | 0.25s | 0.45s | 25 |
| Right slash | 0.35s | 0.25s | 0.45s | 25 |
| Overhead | 0.50s | 0.25s | 0.60s | 35 |
| Thrust | 0.30s | 0.20s | 0.40s | 20 |

These values are starting points only and should be tuned during playtesting.

---

## 9. Blocking System

### Basic Rule

A block succeeds only if the defender is blocking in the same direction as the incoming attack.

| Incoming Attack | Correct Block |
|---|---|
| Left slash | Block left |
| Right slash | Block right |
| Overhead | Block high |
| Thrust | Block thrust |

### Failed Block

A block fails when:

- The defender is not blocking.
- The defender blocks in the wrong direction.
- The defender starts blocking too late.
- The attack hits from outside the defender’s block angle.

### Successful Block Result

When a block succeeds:

- Defender takes no health damage.
- Attacker is briefly slowed or placed into recovery.
- Block sound/effect plays.
- Defender may counterattack.

---

## 10. Block Direction Selection

### Recommended PoC Method

When the player holds **Right Mouse Button**, the block direction is selected using mouse movement.

| Mouse Movement | Block Direction |
|---|---|
| Mouse left | Block left |
| Mouse right | Block right |
| Mouse up | Block high |
| Mouse down | Block thrust |

### Alternative Simpler Method

Use camera-relative direction:

- Looking slightly left + block = block left
- Looking slightly right + block = block right
- Looking up + block = high block
- Looking down + block = thrust block

For the PoC, mouse movement is preferred because it is closer to *Mount & Blade*.

---

## 11. Damage Rules

Damage is applied only during the attack’s release window.

Damage is valid if:

- Attacker is in release phase.
- Weapon hitbox collides with a valid target.
- Target is alive.
- Target is on the opposite team.
- Target has not already been hit by the same attack swing.

### Blocked Hit

If the target is blocking correctly:

```text
Final Damage = 0
```

### Wrong Block or No Block

If the target is not blocking correctly:

```text
Final Damage = Weapon Damage
```

### Optional Partial Block

Later, wrong-direction blocks may reduce damage instead of failing completely.

Example:

```text
Wrong Direction Block = 50% damage reduction
Correct Direction Block = 100% damage reduction
```

For the PoC, use strict blocking first.

---

## 12. Weapon Hit Detection

### Recommended PoC Implementation

Use a weapon collider that is enabled only during the release window.

Example:

- Attack starts.
- Weapon collider disabled during wind-up.
- Animation event enables weapon collider during release.
- Weapon collider detects enemy hit.
- Weapon collider disables at end of release.

### Required Rules

- One target can only be hit once per attack.
- Friendly fire is disabled for the PoC.
- Dead characters cannot be hit.
- Weapon reach should matter.

---

## 13. Character Combat States

Each combat character uses a simple state machine.

| State | Description |
|---|---|
| Idle | Character is not attacking or blocking |
| Moving | Character is moving normally |
| AttackWindup | Character prepares attack |
| AttackHold | Character holds prepared attack |
| AttackRelease | Character can deal damage |
| AttackRecovery | Character cannot attack again yet |
| Blocking | Character is actively blocking |
| HitReaction | Character was hit |
| Dead | Character is dead |

### State Restrictions

| Current State | Can Move? | Can Attack? | Can Block? |
|---|---:|---:|---:|
| Idle | Yes | Yes | Yes |
| Moving | Yes | Yes | Yes |
| AttackWindup | Slow movement | No | Cancel optional |
| AttackHold | Slow movement | Release only | Cancel optional |
| AttackRelease | Limited movement | No | No |
| AttackRecovery | Slow movement | No | Optional |
| Blocking | Slow movement | No | Yes |
| HitReaction | No | No | No |
| Dead | No | No | No |

---

## 14. Animation Requirements

### Required Attack Animations

- Left slash
- Right slash
- Overhead attack
- Thrust/stab

### Required Block Animations

- Block left
- Block right
- Block high
- Block thrust

### Required Reaction Animations

- Hit reaction
- Block impact
- Death

### PoC Animation Rule

Animations do not need to be perfect. They only need to make the direction readable.

Readability is more important than visual polish.

---

## 15. Combat Feedback

The player needs clear feedback.

### Required Feedback

| Event | Feedback |
|---|---|
| Attack prepared | Character pose changes |
| Attack released | Weapon swing animation |
| Hit landed | Hit sound, small effect, enemy reaction |
| Block succeeded | Metal clash sound, spark effect, small camera shake |
| Block failed | Damage sound, health loss, hit reaction |
| Enemy killed | Death animation/ragdoll |
| Player damaged | Screen flash or health bar change |

### Optional Feedback

- Direction indicator near crosshair
- Incoming attack direction icon
- Stamina bar
- Damage numbers
- Slow-motion on perfect block

---

## 16. AI Behavior

Enemy AI should use the same combat rules as the player.

### Basic Enemy Decision Logic

Enemy AI can:

- Move toward target.
- Stop at attack range.
- Pick random attack direction.
- Attack after short delay.
- Block sometimes.
- Choose correct block sometimes depending on difficulty.

### PoC AI Difficulty

| Difficulty | Behavior |
|---|---|
| Easy | Blocks randomly, attacks slowly |
| Normal | Sometimes blocks correctly |
| Hard | Often blocks correctly, attacks faster |

### Recommended PoC AI

For the first version:

- Enemy chooses random attack direction.
- Enemy blocks randomly 30% of the time.
- Enemy has a 40% chance to block in the correct direction.
- Enemy attacks every 1.5–2.5 seconds when in range.

The AI does not need to be smart. It only needs to create readable combat.

---

## 17. Balancing Rules

### Starting Values

| Value | Suggested PoC Number |
|---|---:|
| Player health | 100 |
| Enemy health | 75 |
| Sword damage | 25 |
| Overhead damage | 35 |
| Thrust damage | 20 |
| Attack range | 1.8m |
| Block angle | 90 degrees front arc |
| Enemy attack cooldown | 1.5–2.5s |
| Player movement while blocking | 60% normal speed |
| Player movement while attacking | 50–70% normal speed |

---

## 18. Success Criteria

This feature is successful if:

- Player understands which direction they are attacking from.
- Player understands which direction they are blocking.
- Correct block reliably stops damage.
- Wrong block reliably fails.
- One-on-one fights feel readable.
- The player can defeat an enemy through skill, not only button mashing.
- The same system works when 3–6 characters fight in the same arena.

---

## 19. PoC Implementation Plan

### Step 1: Basic Combat

- Add one attack.
- Add health and damage.
- Add enemy death.

### Step 2: Four Attack Directions

- Add left slash.
- Add right slash.
- Add overhead.
- Add thrust.
- Add mouse-based attack direction selection.

### Step 3: Blocking

- Add basic block.
- Add block direction selection.
- Match incoming attack direction against current block direction.

### Step 4: Feedback

- Add block sound.
- Add hit sound.
- Add hit reaction.
- Add direction indicator.

### Step 5: Enemy AI

- Enemy attacks with random direction.
- Enemy blocks randomly.
- Enemy sometimes blocks correctly.

---

## 20. Future Extensions

After the PoC works, the system can be expanded with:

- Stamina cost for attacking and blocking
- Perfect block / parry window
- Feints
- Shields
- Spears and polearms
- Two-handed weapons
- Weapon speed differences
- Armor damage reduction
- Mounted directional attacks
- AI personality profiles
- Group tactics
- Formation combat
- Friendly fire
- Morale system

---

## 21. Definition of Done

Directional Attack + Directional Block is done when:

1. Player can choose one of four attack directions.
2. Player can choose one of four block directions.
3. Enemies use the same attack direction system.
4. Correct block prevents damage.
5. Wrong block does not prevent damage.
6. Hit detection works reliably.
7. Attack and block animations are readable.
8. One-on-one combat is playable.
9. Small group combat remains functional.
10. The system is stable enough to build the PoC battle around it.
