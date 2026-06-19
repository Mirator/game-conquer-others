# 06 - Battlefield and Presentation

## Battlefields

The campaign uses four runtime-generated biomes, each dressed by the kind of
encounter being fought. `BattleBootstrap` builds in layers: a shared biome
(ground, scatter, containment boundary) plus a kind-specific structure layer.
The shared combat footprint and team spawn lanes are unchanged across every
biome and kind; encounter dressing is kept clear of the enemy spawn lane (z>=13).

### Courtyard

- 34 by 34 ground area.
- Grassy ground with a north-south dirt road and a crossing road.
- Low stone containment boundary.

### Deep Forest

- Dense tree lines, a central dirt track, and fallen-log cover.
- Darker green ambient lighting and forest fog.

### Foggy Marsh

- Shallow-water visuals, reeds, a raised causeway, wreckage, and stone cover.
- Cool lighting and heavier fog.

### Rocky Highlands

- Tall side ridges, boulders, standing stones, and narrow central approaches.
- Open, exposed terrain with strong stone silhouettes.

## Encounter Dressing by Kind

The structure layer dresses the chosen biome for the encounter:

- **Settlement Assault** — a fortified hold on the defender (+z) side: ramparts
  and battlements, a village gate with wall sections, two corner towers (reusing
  the village tower roof), defender banners, and torches.
- **Bandit Field** — the open biome plus a bandit camp behind the enemy line:
  a lit campfire (with a point light), tents, bedrolls, wagons, barrels,
  palisade fences, and loot crates, using curated Kenney Survival Kit FBXs. No
  fortress walls, so it reads as an ambush.
- **Training** — a neutral practice yard: barricades, stone cover, four torches,
  a weapon stand, barrels, and edge fences.

## Spawn Layout

- Player and three blue allies begin near the south side.
- Four red enemies begin near the north side.
- Both teams begin facing generally toward the opposing side.

## Time of Day and Sky

Battle lighting is driven by the campaign day clock. `CampaignState.TimeOfDayForDay(day)`
produces a deterministic 0..1 value (carried in `BattleSetup.TimeOfDay`) so the
hour the player arrived lights the fight, and a retried battle looks identical.

- `BattleBootstrap.ApplySunAndSky` derives sun angle, color, and intensity,
  ambient light, and fog from that value.
- Torch and campfire lights dim toward midday and brighten at night.
- Each region uses a procedural skybox (`RuntimeAssets.Skybox`, `Skybox/Procedural`)
  tinted by time of day; the camera clears to that skybox.

## Visual Style

- Heroic, readable, stylized low-poly medieval presentation.
- Blue and red team colors provide immediate faction readability.
- Arena visuals and fighter views are replaceable through `PresentationCatalog`.
- Warm directional sunlight, ambient fill, fog, landmarks, and limited torch
  lights establish mood without obscuring combat routes.
- Procedural presentation is a migration fallback, not the final shipping path.

## Animation and Reactions

- Procedural walk cycle and body bob.
- Direction-specific sword preparation and release.
- Direction-specific shield blocks.
- White hit flash.
- Stagger lean and knockback.
- Fallen-body death pose.

## Audio and Effects

Recorded CC0 clip sets are used where curated assets exist:

- A per-arena ambient bed, swapped in by `BattleEffects.Initialize(arena)` from
  the arena theme's `ambience` clip, falling back to synthesized wind when none
  is set. The ambient bed and a distant-drum bed both scale with music volume.
- A synthesized victory fanfare.
- Real CC0 clips back the signature combat cues: blade-on-blade clashes for
  perfect-block and counter, cloth swooshes for heavy two-handed swings, and
  catalog impacts for arrow hits, each with a synth fallback.
- Randomized spatial sword swings, blocks, and footsteps, played through a pool
  of 16 spatial voices.
- Pooled flesh-hit, metal-block, and perfect-block particles.
- Camera shake localized to player-involved events.
- Kill feedback: a meaty hit-stop on the player's killing blow plus a
  blood-particle burst and a camera kick, and damage-scaled hit-stop on landed
  player hits.
