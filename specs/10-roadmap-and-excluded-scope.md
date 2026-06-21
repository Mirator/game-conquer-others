# 10 - Roadmap and Excluded Scope

## Current Priority

Improve the feel and readability of the existing small battle before expanding
the game's strategic scope. The ordered remediation plan is defined in
[12-combat-readability-and-responsiveness.md](12-combat-readability-and-responsiveness.md).

## Near-Term Candidates

- Tune directional combat timings, reach, movement penalties, and AI cadence.
- Continue improving attack poses and animation readability without textual
  directional cues.
- Replace primitive fighters with coherent animated character assets.
- Replace synthesized audio with recorded weapon, armor, voice, and ambience.

## Combat Extensions

- Feints and attack cancellation.
- Perfect blocks or parries.
- Partial damage for wrong-direction blocks.
- Additional weapon classes beyond the current sword-and-shield, two-handed
  sword, and bow.
- Armor and damage mitigation.
- Spears and mounted combat.

## Battle Extensions

- Sieges proper: wall-breaching, ladders, and gate-assault mechanics (settlement
  assaults already build a fortified hold, but its walls are not yet climbed or
  broken).
- Battlefield terrain that shapes tactics — elevation, chokepoints, cover that
  changes engagement — beyond the current decorative biome dressing.

## Delivered Beyond The Original MVP

- Free-roam overworld campaign map (click-to-move party, day clock, roaming
  bandit parties, settlement assaults) with a persistent warband roster (see
  [11-campaign-map-and-meta-loop.md](11-campaign-map-and-meta-loop.md)).
- Campaign save/load (PlayerPrefs snapshot, format version 5) with Continue on
  the title screen; autosaves on new campaign, battle launch, battle conclusion,
  return-to-title, and quit, and is deleted on defeat.
- Fighter archetypes (Soldier, Shieldbearer, Berserker, Archer, Captain) layered
  on the three stat tiers, recruited per-archetype and persisting across battles.
- Gold, three recruitable unit tiers, escalating territory threats, and four
  regional battlefields (see
  [13-campaign-economy-units-and-regions.md](13-campaign-economy-units-and-regions.md)).
- A party-survival economy: daily owned-land income against daily troop wages,
  party morale with desertion, and a Renown-driven leadership cap on warband size
  (see [13-campaign-economy-units-and-regions.md](13-campaign-economy-units-and-regions.md)).
- Troop tier progression: soldiers bank battle experience and are manually
  promoted Militia → Veteran → Guard for experience plus gold, keeping archetype.
- Typed settlement recruitment (Village / Town / Castle) with limited,
  day-regenerating volunteer pools, available at any settlement in range.
- Persistent player equipment, two-handed swords, bows, weapon-specific AI,
  and a consequence-free Training Arena (see
  [14-weapons-equipment-and-training.md](14-weapons-equipment-and-training.md)).
- Follow, Hold, Charge, and Advance ally orders, captain-centered formations
  (Line / Shield Wall / Skirmish) with archer hold-fire, and morale-driven
  retreats (see
  [15-tactical-commands-formations-and-morale.md](15-tactical-commands-formations-and-morale.md)).
- Larger commanded battles: army size scales with the leadership cap and enemy
  strength up to 60 per side, with fighters laid out in dynamic ranks. A
  title-screen custom-battle sandbox runs a one-off fight without touching
  campaign state.

## Explicitly Excluded From Current Scope

- Trading and settlement management (settlements recruit, but cannot be developed,
  garrisoned, or traded with).
- Individual character progression (RPG character sheets, named-hero skill trees);
  troop *type* progression — the Militia → Veteran → Guard upgrade path — is in
  scope and delivered.
- Inventory and complex equipment.
- Diplomacy and quests.
- Multiplayer.
- Massive battles of hundreds of combatants (commanded battles now scale to dozens
  per side, up to the 60-per-side deployment ceiling).

## Scope Rule

Future work should only enter implementation after the current directional
combat and small-group battle remain stable, readable, and enjoyable.
