# 13 - Campaign Economy, Units, and Regions

## Strategic Loop

1. March the overworld, sizing up holds and roaming bands by threat, garrison,
   arena, reward, and future income — while daily wages quietly drain the purse.
2. Recruit volunteers at any settlement in range and promote blooded veterans.
3. Hunt a bandit party for loot, or assault an enemy hold to capture it.
4. Preserve valuable soldiers during the battle; survivors keep their banked
   experience.
5. Capture a hold for its conquest reward and renown, then march on — the hold's
   income now offsets the daily bill, though garrisoning it adds an upkeep of its
   own, so each conquest is weighed for whether it pays for itself.

## Economy

- A campaign begins with 150 gold.
- Capturing a territory grants its one-time conquest reward.
- Defeating a bandit party loots `25 + 15 * strength` gold but captures no land.
- Each campaign day, owned-land income is collected and the day's expenses — troop
  wages plus garrison upkeep — are paid, so the net daily cashflow is
  `income - wages - garrison upkeep`. Income is zero until the first hold is
  captured, so an idle warband bleeds gold.
- Daily wages are `2 / 4 / 6` gold per Militia / Veteran / Guard.
- Holding land is not free: every owned hold costs a flat garrison upkeep of 5
  gold per day. Expansion therefore carries an ongoing cost — a sprawling realm of
  low-income holds can run a deficit — so consolidating a few strong holds is a
  genuine alternative to grabbing every weak one.
- If the purse cannot cover a day's expenses it empties and morale takes a hit.
- Gold persists across the campaign and is spent on recruitment and promotions.
- Each day tick writes a player-facing ledger line (shown on the map as the last
  report): the day's income, wages + upkeep, net cashflow, and the morale change,
  plus a warning when wages go unpaid, when morale is fraying toward the desertion
  floor, or when a soldier deserts. Passing days are legible rather than silent; on
  a multi-day march the most recent day's line is shown.

## Morale, Renown, and Leadership

- **Morale** runs 0–100, starting at 60. Each day it drifts toward a target set by
  whether wages were paid (unpaid craters it) and whether the warband is over its
  leadership cap. Winning a battle lifts it (capture +10, field win +5). When
  morale falls below 25 the least-committed soldier deserts overnight.
  (This is the *campaign* party morale; the separate in-battle morale that drives
  retreats and formations lives in spec 15.)
- **Renown** is earned by capturing holds (`20 + 5 * threat`), winning field
  battles (`5 + 2 * strength`), and passively from each held hold per day.
- **Leadership** caps the warband size. It starts at 6 and rises with Renown up to
  a ceiling of 24, so a growing host must be earned through victories and territory.
  Leadership is the player's command ceiling and is distinct from — and smaller
  than — the battlefield deployment limit (60 per side): enemy garrisons and bandit
  hordes can field more soldiers than the warband, so the player can be outnumbered.

## Warband

The warband size is capped by Leadership (see above), from 6 up to 24 soldiers,
excluding the player captain.

| Unit | Cost | Upkeep/day | Role |
|---|---:|---:|---|
| Militia | 35 gold | 2 gold | Affordable numbers; lower health and damage. |
| Veteran | 70 gold | 4 gold | Durable, stronger line fighter. |
| Guard | 110 gold | 6 gold | Elite fighter with the highest health and damage. |

Each fighter also has an archetype chosen at recruitment — Soldier, Shieldbearer,
Berserker, or Archer (Captain appears only in strong enemy garrisons). The
archetype sets the weapon (Berserker → two-handed sword, Archer → bow, others →
sword and shield), the AI behavior, and a health/damage modifier layered on the
tier. Cost depends only on the tier.

Survivors persist by tier and archetype, while deaths remain permanent.

## Troop Progression

Soldiers earn battle experience and can be promoted up the tiers — a Mount &
Blade-style growth loop. Progression is by troop *type*, not individual character
sheets.

- A surviving warband banks experience after each win, pooled per
  (tier × archetype) stack and proportional to the enemy strength defeated.
- A stack with enough banked experience can be promoted one soldier at a time:
  Militia → Veteran (100 XP, 25 gold) → Guard (200 XP, 50 gold). The archetype is
  preserved; Guard is the top tier.
- Promotion is a manual choice (it costs gold as well as experience), surfaced in
  the campaign UI. Banked experience persists across battles and saves.

## Settlement Recruitment

Settlements come in three size classes that gate recruitment:

| Settlement | Volunteers | Tiers offered |
|---|---:|---|
| Village | up to 3 | Militia |
| Town | up to 5 | Militia, Veteran |
| Castle | up to 6 | Militia, Veteran, Guard |

- Volunteers can be recruited at **any** settlement the warband stands near,
  regardless of who holds it; the size class sets the highest tier on offer.
- Each settlement holds a limited pool that depletes as you recruit and refills by
  one volunteer per day.
- Recruiting still costs gold by tier and is bounded by the leadership cap.

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

- The player can buy each unit type when gold, leadership space, and the
  settlement's tier ceiling and volunteer pool allow.
- Fighters can be recruited as any archetype of an offered tier at any settlement
  in range; the pool depletes per recruit and refills over days.
- Recruitment changes the roster and the next deployed army.
- Each day on the map collects owned-land income and pays the day's expenses
  (troop wages plus per-hold garrison upkeep); an unpaid day empties the purse and
  lowers morale, and low morale causes desertion.
- Renown rises with victories and held land, raising the leadership cap.
- A blooded soldier can be promoted to the next tier for banked experience plus
  gold, keeping its archetype; banked experience survives saves.
- Defeating a bandit party loots gold without capturing territory.
- Victory preserves surviving unit types, captures the territory, and awards
  conquest gold and renown.
- All four arena types build and support stable natural combat.
- Several consecutive conquests complete without resetting campaign state.
