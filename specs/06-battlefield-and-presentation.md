# 06 - Battlefield and Presentation

## Battlefields

The campaign uses four runtime-generated biomes, each dressed by the kind of
encounter being fought. `BattleBootstrap` builds in layers: a shared biome
(ground, scatter, containment boundary) plus a kind-specific structure layer.
All battlefield dimensions derive from a single source, `ArenaMetrics` — playable
footprint (`HalfWidth`×`HalfDepth` = 26×30), ground plane (`GroundSize` = 68),
boundary (`WallOffset` = 33), team spawn lanes, and retreat edges — so the arena
can be resized coherently. The footprint is the same across every biome and kind;
encounter dressing is kept clear of the enemy spawn lane (`StructureMinZ` = 24).
The field is an open, cinematic vista: the same troop counts fight near the centre
while the surrounding ground and a framing tree-line give room to manoeuvre.

### Courtyard

- 68 by 68 ground plane (≈26×30 playable footprint).
- Grassy ground with a north-south dirt road and a crossing road.
- An *invisible* containment boundary (collider only), framed by a border tree-line
  and scattered ground clutter rather than a visible wall.

### Deep Forest

- Stands of Quaternius tree variants (common + pine) over the field, a central dirt
  track, and fallen-log cover.
- Darker green ambient lighting and forest fog.

### Foggy Marsh

- Glossy shallow-water pools, reed/fern clutter, a raised causeway, and wreckage.
- Cool lighting and heavier fog.

### Rocky Highlands

- Tall side ridges, model boulders (rock variants), standing stones, and open
  central approaches.
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

- The player and the blue warband deploy near the south side (lanes from
  `ArenaMetrics.AllySpawnZ`); the red enemy force near the north side
  (`EnemySpawnZ`). The larger field pushes the lanes apart, so closing the distance
  takes a little longer.
- Each side is laid out in dynamic rows sized to its headcount
  (`perRow = clamp(ceil(sqrt(count * 2)), 6, 12)`), so a handful of fighters form a
  single short rank while dozens stack into deeper ranks, all clamped within the
  arena walls.
- Both teams begin facing generally toward the opposing side.

## Time of Day and Sky

Battle lighting is driven by the campaign day clock. `CampaignState.TimeOfDayForDay(day)`
produces a deterministic 0..1 value (carried in `BattleSetup.TimeOfDay`) so the
hour the player arrived lights the fight, and a retried battle looks identical.

- `BattleBootstrap.ApplySunAndSky` derives sun angle, color, and intensity, a
  three-band Trilight ambient (sky/equator/ground), and fog from that value. Fog is
  tuned so the distant tree-line/boundary fades into the sky and frames the vista.
- Night is lit as cool moonlight (not a dim warm sun) with a raised intensity/ambient
  floor, cool-blue thinner fog, and a higher skybox night-exposure + thicker atmosphere
  so the horizon is a scattered dark-blue rather than a black void — keeping combat
  readable after dark.
- A cool, shadowless fill light opposite the sun keeps silhouettes readable across
  the larger field (dropped on the low quality tier).
- Torch and campfire lights dim toward midday and brighten at night.
- **Weather** is chosen deterministically from the day clock (so a retried battle
  looks identical): the marsh is always misty, the highlands sometimes snow, and open
  arenas sometimes rain. Each drives a particle layer (`BattleBootstrap.ApplyWeather`,
  quality-gated), thicker/greyer fog and a dimmer sun, a wet/glossy ground in rain, and
  a rain ambience bed. Clear weather adds nothing.
- Each region uses a procedural skybox (`RuntimeAssets.Skybox`, `Skybox/Procedural`)
  tinted by time of day; the camera clears to that skybox.

The overworld map runs its own time-of-day cycle from
`CampaignState.OverworldSunPhase(day, dayFraction)` — a *continuous* dawn→dusk→night
arc that advances while marching and freezes when idle (see
[11-campaign-map-and-meta-loop.md](11-campaign-map-and-meta-loop.md)). It drives the
map sun, ambient, fog, and camera background through a lightweight, arena-free
counterpart of `ApplySunAndSky` in `CampaignMapController` (the map has no skybox).
This is distinct from the battle `TimeOfDayForDay` value, whose golden-ratio step
spreads hours across days for variety rather than a smooth cycle.

## Visual Style

- Heroic, readable, stylized low-poly medieval presentation.
- Biome scatter is real Quaternius models — textured tree/rock/clutter variant pools
  (`PresentationCatalog.RandomTree/RandomRock/RandomClutter`) rather than flat-tinted
  primitives. `AuthoredVisual` keeps each model's imported materials; tinting is
  reserved for team heraldry (e.g. defender banners).
- The ground is an undulating terrain mesh (`BattleBootstrap.BuildTerrainMesh`):
  flat across the playable footprint so combat/spawns/formations are unchanged, then
  rolling into hills toward the rim and horizon. It is surfaced with a tiling,
  runtime-generated seamless noise texture **and matching normal map**
  (`RuntimeAssets.GroundMaterial`) per biome palette, and carries a `MeshCollider`.
- Scatter is placed in clusters (copses, grass patches) and sampled onto the terrain
  height; trees, grass, and reeds carry a `WindSway` lean (disabled on the low tier
  and under reduced-motion).
- A dense grass carpet is drawn with **GPU instancing** (`GrassField` via
  `Graphics.DrawMeshInstanced`, a procedural solid blade mesh + double-sided gradient
  material) so the grassy biomes read as lush at a few draw calls; density scales with
  quality. The flat plane-based Quaternius grass/fern models are kept out of the general
  clutter (they read as cardboard without their alpha texture) and reserved as sparse
  marsh reeds (`tallGrass`); open-ground clutter is solid detail only (flowers,
  mushrooms, pebbles, bushes). Clutter is biome-aware — the rocky highlands use a
  flower-free `barrenClutter` pool (pebbles/mushrooms). Scatter is kept off the central
  road/causeway lane so the clash zone stays clear, and the marsh stays a wetland
  (reeds at the pools, no carpet).
- Marsh pools use a glossy water material whose ripple normal-map offset is scrolled by
  `WaterAnimator` for moving highlights (animated water without a custom shader).
- A dark, fog-blended **distant silhouette ring** of trees (or crags in the highlands)
  sits beyond the boundary on the rolling rim, forming the horizon of the vista.
- The containment boundary is invisible (collider only); the playable edge is felt,
  not seen — the open field is framed by the tree-line and fog.
- A runtime cinematic post-processing stack (`BattlePostProcessing`) drives ACES
  tonemapping, bloom (on torches/flames), a light colour grade, vignette, and SSAO
  on the battle, title, and campaign-map cameras.
- Heavy presentation scales with `GraphicsQuality` (scatter density, bloom, shadow
  distance, fill light, anti-aliasing) so the low tier holds a stable framerate while
  the gameplay-critical layout is identical on every tier.
- Blue and red team colors provide immediate faction readability.
- Arena visuals and fighter views are replaceable through `PresentationCatalog`.
- Warm directional sunlight, Trilight ambient, fog, landmarks, and limited torch
  lights establish mood without obscuring combat routes.
- Procedural primitives remain only as a fallback when a catalog model is missing.

## Animation and Reactions

- Procedural walk cycle and body bob.
- Direction-specific sword preparation and release.
- Direction-specific shield blocks.
- White hit flash.
- Stagger lean and knockback.
- Fallen-body death from the humanoid Death clip, dropped to rest on the ground (the
  root-motion-free clip otherwise leaves the corpse floating at hip height), and varied
  so a field of fallen fighters doesn't look cloned: a mirrored clip variant, randomized
  playback speed, and randomized facing (yaw). (Pitch/roll are never stacked on the
  humanoid death clip — that re-submerges the body.)
- A pre-battle establishing camera flyover over the lines (`ThirdPersonCamera.PlaySweep`)
  that eases into the follow camera; the first click skips it, and it is skipped under
  reduced motion.

## Audio and Effects

Recorded CC0 clip sets are used where curated assets exist:

- A per-arena ambient bed, swapped in by `BattleEffects.Initialize(arena)` from
  the arena theme's `ambience` clip, falling back to synthesized wind when none
  is set. The ambient bed and a distant-drum bed both scale with music volume.
- Positional 3D ambient emitters placed per biome (`BattleEffects.AddBirdsong/
  AddMarshChorus/AddWindGust`) give the field spatial life — synthesized birdsong in
  wooded arenas, a frog/insect chorus near the marsh, gusting wind in the highlands.
  These also scale with music volume.
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
- Kicked-up dust puffs (`BattleEffects.PlayDust`, soft pooled particles) on the
  player's footsteps and on killing blows; skipped on the low tier.
- **Persistent battlefield damage** (`BattleDecals`): blood splats and a chance of
  dropped-gear debris at kill sites, trample scuffs where the lines clash, and arrows
  that stay embedded where they land — all flat ground decals / stuck props, capped and
  recycled (oldest first) so they never grow unbounded.
