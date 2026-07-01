# 16 - Presentation Overhaul

## Direction

Conquer Others uses a heroic, readable medieval presentation. Gameplay state
and combat timing remain authoritative; presentation may decorate, animate,
and sonify that state but must not alter it.

## Runtime Architecture

- `PresentationCatalog` is loaded from `Resources` and owns curated visual,
  arena-theme, audio, effect, and frontend references.
- `ArenaThemeDefinition` separates replaceable visual themes from gameplay
  collision, boundaries, spawn lanes, and critical combat routes.
- `FighterView` defines animator, hand/projectile/effect anchors, team-tint
  renderers, and weapon trails for authored fighter prefabs.
- Curated Quaternius fighter, weapon (including bow and arrow), village,
  settlement-building, and nature assets populate the catalog. Procedural geometry
  remains only as gameplay collision and as a fallback when a catalog reference is
  unavailable.
- Biome scatter draws from variant pools (`treeVariants`, `pineVariants`,
  `deadTreeVariants`, `rockVariants`, `groundClutter`) so stands read as natural
  rather than cloned; `RandomTree/RandomPine/RandomDeadTree/RandomRock/RandomClutter`
  fall back to the single-model fields when a pool is empty. The editor builder
  extracts a curated subset of MegaKit FBXs plus the nature `Textures/` folder and
  imports them with material-description + normal-mapped textures so trees and rocks
  render textured (`PresentationAssetBuilder.EnsureNatureTextures`).
- `BattleBootstrap.AuthoredVisual` keeps each model's imported (textured) materials;
  it only tints when a heraldry/team colour is explicitly supplied (defender banners).
- Captain, militia, veteran, guard, and enemy entries resolve to distinct
  generated fighter prefabs, even when they share an underlying Quaternius
  outfit mesh. Captains read as elite through a 1.18x silhouette and a bright
  gold crest.
- Each combat archetype is given its own silhouette and trim at build time
  (`BattleFighterPresentation.StyleFor`) so it reads at a glance on both the
  authored and primitive paths: Berserkers are bulkier, bare-headed, and
  blood-red-trimmed with a smaller shield; Shieldbearers are sturdier with an
  oversized shield and steel trim; Archers are leaner with green trim; plain
  Soldiers keep the per-tier accent (Guard gold, Veteran silver).
- Runtime must not attach unbound rank accent geometry to authored animated
  fighters; any added geometry (weapons, head) must attach to validated bones or
  sockets so it follows the animation.
- The battle runtime uses the authored Quaternius humanoid bodies, driven by the
  shared `Fighter` AnimatorController (idle, walk, jog, formation walk, attack,
  block, hit, death). `FighterView` rebinds the controller from `Resources` at
  runtime so a fighter never falls back to its bind (T) pose if the prefab's
  serialized controller reference is lost during a catalog rebuild.
- Sword, shield, and bow attach to the validated right/left hand bones. Weapon
  prefabs keep their native Quaternius FBX scale (multiplied, not overwritten).
- The authored sword and shield FBXs ship with flat, untextured white materials,
  so they are repainted on instantiation (`RuntimeAssets.TintModel`) — the blade to
  steel, the shield to the team colour — instead of reading as glowing white discs.
- The bow, the held arrow, and the flying arrow projectile use authored Medieval
  Weapons Pack models; the primitive cylinder/cube weapon rig remains as the
  fallback when the catalog slot is empty.
- Campaign settlement structures (small/large houses, town hall, castle keep and
  corner towers) are composed by the editor builder from Medieval Village MegaKit
  pieces (walls + roof + door) and saved as prefabs under
  `Resources/Presentation/Buildings`; the diorama falls back to primitive blocks
  when a slot is null.
- Quaternius outfit FBXs ship without a base head mesh (the head is meant to come
  from the Universal Base Character kit). A real head — skin, eyes and brows carved
  out of `Superhero_Male_FullBody` (vertices weighted to the Head/neck bones) and
  baked into Head-bone-local space by `PresentationAssetBuilder.EnsureFighterHead` —
  is stored on the catalog as mesh + material references and mounted on the Head bone
  at runtime by `FighterView.CreateHead`. Because it is baked rigid to the Head bone
  it follows head animation, and it fits both bare-headed (peasant) and hooded
  (ranger) bodies. The player's ranger hood (`Male_Ranger_Head_Hood`) is disabled at
  spawn so the hero's face reads clearly; other ranger-based units (enemy captains,
  veterans, guards) keep theirs. The head is referenced as meshes/materials rather
  than a prefab because baking mesh references into a prefab during the builder pass
  serialized as null. A skin-toned primitive sphere remains the fallback when the
  base character is not imported.

## Post-Processing And Lighting

- A runtime global Volume (`BattlePostProcessing.Apply`) is built from a generated
  `VolumeProfile` — ACES tonemapping, bloom, a light colour grade, and a vignette —
  and `renderPostProcessing` is enabled on the battle, title, and campaign-map
  cameras. SSAO is a URP renderer feature (already on `PC_Renderer`) and activates
  once post-processing is enabled. **Both the Volume stack and SSAO require an
  assigned URP asset; under the built-in renderer that currently ships (see
  [specs/08](08-runtime-architecture-and-build.md)) they are inert, and the battle
  look comes from lighting, fog, and the procedural skybox.** The code builds the
  stack unconditionally so it lights up automatically if a URP asset is assigned.
- Battle ambient uses `AmbientMode.Trilight` (sky/equator/ground bands) plus a cool
  shadowless fill light opposite the sun. Shadow distance is set from the quality
  tier to cover the larger field.
- `RuntimeAssets.Material(color, smoothness, metallic, emissive)` provides tunable
  PBR surfaces (e.g. glossy marsh water, metal) while the default matte overload is
  unchanged; results are cached on quantized smoothness/metallic so instancing holds.

## Frontend

- All active game UI uses responsive uGUI canvases.
- The game opens on a title screen with New Campaign, Settings, and Quit.
- Escape in battle, and Escape or a MENU button on the campaign map, open a
  pause screen with Resume, Settings, Return to Title, and Quit.
- Settings persist through `PlayerPrefs` and cover volume, sensitivity, camera
  shake, fullscreen, resolution, quality, VSync, and reduced motion.

## Assets And Audio

- Curated third-party assets live under provider-specific folders.
- Only CC0 files listed in `THIRD_PARTY_NOTICES.md` may ship.
- Full Quaternius animation-library FBXs are treated as ignored intake
  sources. The editor builder may temporarily extract them from
  `AssetDownloads` to regenerate curated `.anim` clips, then must remove the
  source FBXs from `Assets/ThirdParty`.
- A curated subset of MegaKit model FBXs (nature variants, clutter) and the nature
  kit textures are committed under `Assets/ThirdParty/Quaternius/Nature`; unlike the
  animation FBXs these are permanent so they ship in the build. Only CC0/standard-
  licensed files listed in `THIRD_PARTY_NOTICES.md` may ship.
- The bow/arrow FBXs (Medieval Weapons Pack) and the settlement-building pieces
  (Medieval Village MegaKit: a wall plus round-tile roofs) are likewise extracted by the
  editor builder from `AssetDownloads` on first use and committed permanently under
  `Assets/ThirdParty/Quaternius/Weapons` and `.../MedievalVillage`. Quaternius
  "Standard" FBXs carry vertex colours, so these need no companion textures.
- Recorded clip sets are preferred; synthesized audio remains a fallback for
  events without an approved recorded clip.
- Battlefields are dressed by encounter kind (assault hold, bandit camp, or
  training yard) over a shared biome, with time-of-day lighting from the campaign
  clock and a per-region procedural skybox. Bandit-camp dressing reuses curated
  Kenney Survival Kit FBXs.
- A per-arena ambient bed (`ArenaThemeDefinition.ambience`, swapped in by
  `BattleEffects.Initialize`) plays under the distant-drum bed, both scaled by
  music volume.
- A music bed plays over the drums: the arena theme's `music`, the catalog-wide
  `battleMusic`, or a synthesized `ProceduralMusic` theme as fallback. The
  overworld map has its own looping theme (`mapMusic` or synth fallback). The
  martial beds duck out on victory so the fanfare stands clear. All music tracks
  the `musicVolume` setting live. Curated CC0 music is optional: the editor builder
  auto-wires `Battle`/`Overworld`/`Victory` clips dropped into
  `Assets/ThirdParty/OpenGameArt/Music/` into the catalog, else the synth themes play.
- Signature combat cues prefer curated CC0 clips — blade clashes for
  perfect-block and counter, cloth for heavy swings, catalog impacts for arrows —
  with synth fallbacks. Spatial SFX play through a pooled set of 16 voices.
- Impact effects use pooled particle systems and presentation code avoids
  recurring per-frame allocations.
- A guard broken by stamina exhaustion plays a dedicated metallic shatter cue and
  a bright spark burst (`BattleEffects.PlayGuardBreak`), distinct from the soft
  thud of a guard that held.
- Combat reads through animation, impact effects, audio, and camera feedback
  alone. No floating damage numbers or textual hit cues are drawn over fighters —
  the battle stays diegetic.

## Performance Target

The target is stable 1080p/60 FPS with up to 60 fighters per side (120 total) on a
midrange Windows PC, so commanded battles can field large lines. Reaching that
scale with the runtime-generated, no-prefab approach relies on:

- **GPU instancing** on the shared generated materials, so the many primitives of
  one colour batch into few draw calls.
- **Dirty-flagged property blocks** — fighter hit-flash colours are rewritten only
  on the frames the flash turns on or off, not every frame, keeping renderers on
  the SRP Batcher's fast path the rest of the time.
- **Authored-body fighters skip their fallback primitive meshes** entirely (rather
  than creating then hiding them), and only the player carries a sword trail.
- **Spatial-hash neighbour queries** replace the per-frame O(n²) separation and
  proximity scans, and AI target scoring / formation slotting read once-per-frame
  caches.

Realtime shadow-casting lights are limited, materials are reused, and effects are
pooled. The larger field and richer presentation (post-processing, scatter density,
shadow distance, fill light, anti-aliasing) scale with `GraphicsQuality` so the low
tier preserves the framerate; collider-free scatter keeps the simulation unchanged.
Dense and distant scatter (ground clutter, border/distant trees, reeds, boulders) is
**excluded from the shadow pass** — only fighters, hero trees, and structures cast
shadows — so hundreds of instances don't multiply shadow-map cost. Grass is GPU-
instanced and shadowless. The `PerformanceHud` overlay (F3) reports FPS / frame time /
fighter count for confirming the budget holds at scale.
