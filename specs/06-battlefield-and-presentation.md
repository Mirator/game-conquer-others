# 06 - Battlefield and Presentation

## Battlefields

The campaign uses four runtime-generated arenas. Every arena preserves the
shared combat footprint and team spawn lanes while changing terrain, cover,
atmosphere, and movement routes.

### Fortified Courtyard

- 34 by 34 ground area.
- Stone boundary walls and battlements.
- North-south dirt road and crossing road.
- Wooden barricades and stone cover near the center.
- Blue banners at the blue side and red banners at the red side.
- Supply crates near outer lanes.
- Four lit torches.

### Deep Forest

- Dense tree lines, a central dirt track, and fallen-log cover.
- Darker green ambient lighting and forest fog.

### Foggy Marsh

- Shallow-water visuals, reeds, a raised causeway, wreckage, and stone cover.
- Cool lighting and heavier fog.

### Rocky Highlands

- Tall side ridges, boulders, standing stones, and narrow central approaches.
- Open, exposed terrain with strong stone silhouettes.

## Spawn Layout

- Player and three blue allies begin near the south side.
- Four red enemies begin near the north side.
- Both teams begin facing generally toward the opposing side.

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

- Randomized spatial sword swings, blocks, and footsteps.
- Looping courtyard wind and distant drums.
- Victory cue.
- Pooled flesh-hit, metal-block, and perfect-block particles.
- Small impact camera shake.
