# 14 - Weapons, Equipment, and Training Arena

## Weapon Loadouts

Every fighter has one authoritative `WeaponType`:

| Weapon | Role |
|---|---|
| Sword & Shield | Balanced directional melee; shield blocks arrows while raised and facing the shooter. |
| Two-Handed Sword | Longer reach and heavier damage with slower attacks; directional weapon blocks; no shield. |
| Bow | Ranged projectile weapon; cannot block. |

A fighter's weapon is chosen by its archetype: `ArchetypeCatalog.Weapon` gives a
two-handed sword to Berserkers, a bow to Archers, and a sword and shield to
Soldiers, Shieldbearers, and Captains. `WeaponCatalog.DefaultFor(UnitType)`
remains only as a tier-only fallback when an encounter has no archetype
composition (legacy militia sword-and-shield, veteran two-handed, guard bow).
Player and training-opponent loadouts may override the chosen weapon.

## Bow Combat

- Hold left mouse to draw.
- Mouse look remains active while drawing.
- Release left mouse to fire through the center crosshair.
- Shots begin with a wide 7.5-degree spread. Precision does not improve until
  the 0.7-second draw threshold, then tightens steadily to a 0.25-degree spread
  at 1.4 seconds. The threshold is later than the minimum release time, so a
  quick shot remains fully loose.
- The bow reticle starts wide and orange, contracts as precision improves, and
  becomes tight and green at full precision. A draw bar marks the minimum
  threshold and the labels `DRAW`, `TIGHTENING`, and `STEADY` communicate state.
- The over-the-shoulder projectile direction converges on the crosshair rather
  than firing parallel to the offset camera.
- Arrows have travel time, gravity, arena-geometry collision, friendly-fire
  filtering, and one-hit damage.
- AI archers maintain ranged spacing, evade close threats, compensate for
  projectile drop, wait for useful precision, and fire from normal battlefield
  opening distance.

## Bow Presentation

- The procedural bow uses four curved limb segments, a distinct wrapped grip,
  and two string segments.
- While drawing, both string halves converge on the arrow nock and the held
  arrow moves backward with the string.
- The draw arm pulls inward toward the face and fired arrows include
  team-colored fletching.

## Campaign Equipment

- The campaign map always shows a Captain Equipment panel.
- The selected player weapon persists across campaign battles and training.
- Recruitment offers a stat tier crossed with an archetype: cost is set by the
  tier (militia, veteran, guard) while the archetype is a free choice of
  behavior that communicates the fighter's weapon and role.

## Training Arena

- The Training Arena is a separate, named, clickable campaign-map node.
- Selecting it opens a 1v1 setup panel.
- Player equipment comes from the Captain Equipment panel.
- Enemy equipment can be cycled independently in the training panel.
- Training launches a one-player-versus-one-enemy courtyard battle.
- Victory or defeat returns to the map without affecting gold, roster,
  territories, campaign victory, or campaign defeat.
