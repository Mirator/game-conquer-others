# 13 - Campaign Economy, Units, and Regions

## Strategic Loop

1. March the overworld, sizing up holds and roaming bands by threat, garrison,
   arena, reward, and future income.
2. Rest in an owned hold to recruit a warband by tier and archetype.
3. Hunt a bandit party for loot, or assault an enemy hold to capture it.
4. Preserve valuable soldiers during the battle.
5. Capture a hold for its conquest reward plus ongoing owned-land income (income
   stays at zero until the first capture), then march on to the next encounter.

## Economy

- A campaign begins with 150 gold.
- Capturing a territory grants its one-time conquest reward.
- Defeating a bandit party loots `25 + 15 * strength` gold but captures no land.
- Every victory also collects income from all player-owned territories; income is
  zero until the first hold is captured.
- Gold persists across the campaign and is spent on recruitment.

## Warband

The warband cap is 12 soldiers, excluding the player captain.

| Unit | Cost | Role |
|---|---:|---|
| Militia | 35 gold | Affordable numbers; lower health and damage. |
| Veteran | 70 gold | Durable, stronger line fighter. |
| Guard | 110 gold | Elite fighter with the highest health and damage. |

Each fighter also has an archetype chosen at recruitment — Soldier, Shieldbearer,
Berserker, or Archer (Captain appears only in strong enemy garrisons). The
archetype sets the weapon (Berserker → two-handed sword, Archer → bow, others →
sword and shield), the AI behavior, and a health/damage modifier layered on the
tier. Cost depends only on the tier.

Survivors persist by tier and archetype, while deaths remain permanent.

## Territory Progression

- The procedural campaign contains eight connected territories.
- Every territory has a garrison, threat rating, conquest reward, income value,
  and arena type.
- Threat, enemy quality, health scaling, and rewards generally increase farther
  from the starting territory.

## Region Arenas

| Arena | Battlefield identity |
|---|---|
| Fortified Courtyard | Walls, battlements, roads, barricades, and torches. |
| Deep Forest | Trees, a central track, fallen logs, and dense side lanes. |
| Foggy Marsh | Shallow pools, reeds, a causeway, wreckage, and heavy fog. |
| Rocky Highlands | Ridges, boulders, standing stones, and narrow approaches. |

## Acceptance

- The player can buy each unit type when gold and capacity allow.
- Fighters can be recruited as any archetype of the selected tier, but only while
  standing in an owned hold.
- Recruitment changes the roster and the next deployed army.
- Defeating a bandit party loots gold without capturing territory.
- Victory preserves surviving unit types, captures the territory, and awards
  conquest gold plus owned-territory income.
- All four arena types build and support stable natural combat.
- Several consecutive conquests complete without resetting campaign state.
