# 11 - Campaign Map and Meta Loop

## Purpose

The campaign map is a free-roam overworld wrapped around the battle. The player
steers a warband party across hostile ground, hunting roaming bandits and
assaulting enemy-held holds to survive and grow the host. There is no conquest
victory — the campaign is open-ended survival that ends only on defeat. This
gives the battle a reason to repeat.

## Campaign Model

- The map is a graph of 8 procedurally-placed `Territory` nodes.
- Each territory has an owner, garrison, threat, reward, income, and arena type.
- Every territory starts enemy-owned; the player owns nothing and begins alone in
  open ground just south of the weakest hold.
- Adjacency edges still connect the nodes (drawn on the map and used to scale
  threat outward from the weakest hold), but they no longer gate travel — the
  player moves freely by click-to-move.
- Roaming bandit parties (`EnemyParty`) wander the overworld. A band only chases
  the player when it is at least ~60% of the player's strength, forcing a field
  battle on collision; weaker bands hold position and can only be hunted down by
  the player.
- The player keeps a persistent warband carried between battles, each fighter a
  tier-by-archetype combatant. It starts with 3 militia and grows through
  recruitment up to the leadership cap (6 at first, rising with Renown to 24).

## Map Rules

- Click-to-select drives travel: clicking a hold, a bandit band, or open ground
  selects it as a destination and shows a dotted route to it, the number of
  complete campaign days that will elapse, and — for a hold — its garrison,
  threat, reward, and income. A separate
  **march** button commits the journey; it resolves on arrival (assault an enemy
  hold, hunt a band, or rest at a friendly hold). Reviewing before committing
  means a misclick no longer sends the warband on a long march.
- A day clock advances while the warband travels (about 4 map units per day) and
  pauses whenever the warband is idle.
- A **Wait a Day** action passes a single day in place; roaming bands close in
  while the warband holds position.
- Each day that elapses (marching or waiting) runs the party economy: owned-land
  income is collected, troop wages and per-hold garrison upkeep are paid, renown
  accrues from held land, morale drifts, and settlement recruit pools refill.
- Recruitment is available while standing near any settlement (within about 2.4
  map units), regardless of who holds it; the settlement's size class sets which
  tiers and how many volunteers are on offer.
- The captain equipment panel selects the player's persistent weapon.
- A separate Training Arena node launches a consequence-free 1v1. The player
  chooses their weapon from captain equipment and chooses the opponent weapon
  in the training setup panel.
- The battle is parameterized by the encounter:
  - Allied soldiers spawned = the current roster (clamped to the arena cap).
  - Enemy soldiers spawned = the hold's garrison or the bandit band's strength.
  - Enemy quality and health scale = the hold's threat (bandits are bandit-tier).
  - Arena layout = the hold's or band's arena type.

## Battle Outcome

- **Settlement-assault victory**: the hold becomes player-owned. The player earns
  conquest gold and renown; the hold's income then accrues per day rather than in a
  lump. Surviving allies persist back to the roster by tier and archetype (keeping
  banked experience); allied deaths are permanent.
- **Field-battle victory** (a roaming band): the party is destroyed and removed
  from the map. The player loots `25 + 15 * strength` gold but captures no land.
  Survivors persist as above.
- **Defeat** (player dies): the campaign is lost and the save is deleted.
- **Training result**: returns to the map without changing campaign economy,
  roster, ownership, or defeat state.
- The player clicks the result button to apply the outcome and return to the map.

The campaign is persisted as a versioned full snapshot (`CampaignSaveService`). Each
write first promotes the current save to a last-known-good backup slot, so a corrupt or
partial primary write recovers from the backup on load instead of losing the campaign;
both slots are cleared only when neither parses (or on defeat).

## Loss (no victory condition)

- Free-roam survival: there is no win state. The campaign continues open-ended as
  the player hunts bandits and raids holds.
- **Campaign defeat**: the player dies in any battle. This is the only ending.
- The end screen offers R to begin a fresh campaign.

## Map Controls

| Input | Action |
|---|---|
| Left mouse | Click a hold, a bandit band, or open ground to select it as a march destination (the march button then commits) |
| Mouse wheel | Zoom the camera |
| Right-button drag | Pan the camera across the map (kept within the map table) |
| Home | Recenter the camera on the warband |
| R (on the end screen) | Begin a new campaign |

## Presentation

- The map is runtime-generated as a low-poly 3D diorama, reusing shared
  materials, curated props, and primitive dressing. It sits on a mottled
  parchment war-table surface framed by a raised timber rim, so the regional
  districts read as terrain laid over a map rather than floating in a void.
  Every territory gets a deterministic regional district: forest groves, marsh
  pools/reeds, highland ridges/boulders, or courtyard roads/fences. Roads and
  roadside stones connect adjacent holds without becoming collision geometry.
- The ground is a procedurally height-mapped, flat-shaded terrain mesh draped in
  the parchment material: gentle rolling relief that stays calm in the central
  basin and rises into hills toward the table edges, so the map reads as sculpted
  land rather than a flat board. The terrain is forced level over a pad around
  every hold and the Training Arena, and along every road corridor, so
  settlements, markers, and roads never float over or sink into a slope; a
  MeshCollider on it catches map clicks. Scattered woods and the occasional pond
  dress the open ground (seated on the terrain surface, kept clear of holds and
  roads) — relief comes from the ground itself, not from prop hills. District
  dressing (e.g. highland boulders) is biased behind the settlement, away from the
  fixed southward-looking camera, so a hold's buildings are never occluded.
- Settlements have readable silhouettes by size: villages are clusters of homes,
  towns add a hall and denser buildings, and castles add a keep, towers, gate, and
  standard. Houses, hall, keep, and towers use authored models composed from the
  Medieval Village MegaKit (primitive blocks remain the fallback), tinted to warm
  timber and stone tones on the map so they read as buildings rather than
  blown-out white blocks; forest trees are likewise tinted to map greens.
  Decoration never
  catches map clicks; the table, hold nodes, parties, and Training Arena remain the
  only interactive map geometry.
- The map is roughly twice as large per axis, viewed through a zoom/pan camera
  over the miniature world.
- Hold nodes are colored by owner (blue player / red enemy), labelled with the
  hold's name on the map (the name color doubles as an assault cue — red enemy
  holds you can march on, blue your own), and all enemy holds pulse. The selected
  destination glows gold with a dotted route drawn from the warband to it.
  Adjacency edges are drawn between nodes.
- The player party and the bandit parties are authored soldier models (the player's
  captain model and the enemy model), scaled up so they read against the steep
  overhead camera, with floating unit-count labels above them; a primitive
  captain-style figure is the fallback. Each figure idles via its own animator when
  halted and, while its band moves, turns to face its heading and plays a walk clip.
- Hovering a hold, party, or the Training Arena adds a gold emphasis and a compact
  world-space tooltip. Holds also show their settlement tier and garrison; a small
  HUD legend explains blue ownership, red ownership, and gold special/selected sites.
- The map runs a day/night cycle. `CampaignState.OverworldSunPhase(day, dayFraction)`
  gives a continuous 0..1 phase that advances smoothly while the warband marches
  (tracking `OverworldSimulation.DayFraction`) and freezes when it stands still, so
  each campaign day reads as a natural dawn→midday→dusk→night arc. The map sun
  angle/color/intensity, ambient, fog, and camera background follow it each frame.
  (This is a coherent cycle, distinct from the golden-ratio per-day battle hour in
  [06-battlefield-and-presentation.md](06-battlefield-and-presentation.md).)
- The HUD keeps the map clear. A slim top resource strip carries a sun/moon dial
  with a DAWN/MIDDAY/DUSK/NIGHT phase label and a single status line: day, gold and
  net daily cashflow, morale, renown, warband size against the leadership cap, and
  owned-hold count; the latest report sits just below. A slim bottom action panel
  inspects the current selection — a hold's garrison, threat (stars), reward, and
  income (and its garrison upkeep if owned), or a band's strength against the
  warband's — with the march cost in days and a contextual assault/hunt/rest confirm
  button, alongside a Wait-a-Day action. The Recruit, Promote, and Captain Equipment
  panels are hidden until summoned from a bottom icon toolbar; opening one closes the
  others. Recruitment uses a tier selector and per-archetype recruit buttons under a
  status line that names why recruiting is blocked when it is (no settlement in
  range, warband full, too poor, tier not offered). Promotion shows an XP progress
  bar toward each stack's next tier. Captain equipment selects the player's weapon.

## Future Campaign Features

Neutral territories, enemy counter-attacks, pre-battle events, settlement
upgrades, and trading are outside the current slice. Saving and loading the
campaign is implemented (see [10-roadmap-and-excluded-scope.md](10-roadmap-and-excluded-scope.md)).
