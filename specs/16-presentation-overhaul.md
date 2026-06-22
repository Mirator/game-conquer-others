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
- Curated Quaternius fighter, weapon, village, and nature assets populate the
  catalog. Procedural geometry remains only as gameplay collision and as a
  fallback when a catalog reference is unavailable.
- Captain, militia, veteran, guard, and enemy entries resolve to distinct
  generated fighter prefabs, even when they share an underlying Quaternius
  outfit mesh. Captains read as elite through a 1.18x silhouette and a bright
  gold crest.
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
- Quaternius outfit FBXs ship without a base head mesh, so a simple skin-toned
  head is generated at runtime and bound to the Head bone. The procedural
  primitive rig remains only as a fallback when a catalog reference is missing.

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
- Recorded clip sets are preferred; synthesized audio remains a fallback for
  events without an approved recorded clip.
- Battlefields are dressed by encounter kind (assault hold, bandit camp, or
  training yard) over a shared biome, with time-of-day lighting from the campaign
  clock and a per-region procedural skybox. Bandit-camp dressing reuses curated
  Kenney Survival Kit FBXs.
- A per-arena ambient bed (`ArenaThemeDefinition.ambience`, swapped in by
  `BattleEffects.Initialize`) plays under the distant-drum bed, both scaled by
  music volume.
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
pooled.
