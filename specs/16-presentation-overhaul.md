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
- Escape in battle opens a pause screen with Resume, Settings, Return to Title,
  and Quit.
- Settings persist through `PlayerPrefs` and cover volume, sensitivity, camera
  shake, fullscreen, resolution, quality, and VSync.

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

## Performance Target

The target is stable 1080p/60 FPS with 16 fighters on a midrange Windows PC.
Realtime shadow-casting lights are limited, materials are reused, and effects
are pooled.
