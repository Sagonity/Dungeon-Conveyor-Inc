# Expedition: Deepshaft — Game Design Summary

**Version 3.4 · 2026-11-12** · Mobile Mining & Autobattler

> **This is the source of truth for the game design.** Where another doc
> disagrees with this one, this one wins. The [MVP Scope](MVP_Scope_and_Core_Loop.md)
> (SCRUM-1) turns it into a build plan; the [Economy](Economy_and_Progression.md)
> (SCRUM-2) turns it into numbers.

Our game combines mining, minion collection, autobattling, expeditions and
equipment progression into one connected economy.

The main goal is to make every session worthwhile, reward players for
progressing, and give them reasons to return without punishing them for being
offline.

---

## 1. Core gameplay loop

```
 MINING ───────────────► Earn Gold and ores
   │
   ▼
 SMITH & MINIONS ──────► Upgrade equipment and strengthen Minions
   │
   ▼
 MINE TIER BOSS ───────► Guaranteed progression material + Expedition unlock
   │
   ▼
 EXPEDITION FIRST CLEAR► Defeat enemies, earn bonus rewards, unlock automation
   │
   ▼
 AUTOMATED EXPEDITION ─► Farm rare materials indefinitely, including offline
   │
   └──► Unlock breakthroughs and progress to the next mine tier ──► (repeat)
```

## 2. Mining and resource progression

**Confirmed:** Miner Minions automatically mine the **highest unlocked tier**.
Every new tier **keeps all previously available resources and increases their
production**.

| Tier | Gold | Iron | Silver | Mithril |
|---|---|---|---|---|
| T1 | 100 | 40 | 0 | 0 |
| T2 | 150 | 65 | 0 | 0 |
| T3 | 220 | 90 | 10 | 0 |
| T4 | 320 | 125 | 25 | 0 |
| T5 | 450 | 170 | 45 | 5 |
| T6 | 600 | 230 | 75 | 15 |

*Illustrative hourly production rates, not final balance.*

For example, Tier 1 produces Gold and Iron, Tier 2 produces more Gold and Iron,
and Tier 3 introduces a small amount of Silver while continuing to increase Gold
and Iron production.

**There is no ore conversion system.** Older resources remain obtainable automatically.

## 3. Miner Minions vs Adventure Minions

| | Miner Minions | Adventure Minions |
|---|---|---|
| Purpose | Resource production | Combat and Expeditions |
| Specialization | Fixed traits per character | Combat classes |
| Progression | Mining efficiency and yield | Combat stats and abilities |
| Acquisition | Summoning shards | Summoning shards |

- **Miner traits** may specialize in Gold, ore, or mining speed.
- **Adventure classes:** Tank, Fighter, Archer, Mage and Support.
- **Still undecided:** the number of active Minions in each team.

## 4. Bosses and Expeditions

Bosses follow a Valheim-inspired progression model: defeating a **Mine Tier
Boss** guarantees a **unique progression material** and **unlocks new Expeditions**.

Expeditions work differently before and after their first completion:

| First Expedition clear | After first clear |
|---|---|
| Autobattles can fail | Automation cannot fail |
| Requires a capable Adventure team | Generates resources over time |
| Gives bonus rewards | Regular rare-material income |
| Unlocks automation permanently | Can operate while the phone is locked |

- **Only one Expedition can be active at a time.**
- Players can **stop an automated Expedition whenever they want** and collect the accumulated rewards.
- Automated Expedition rewards have **no time cap and no diminishing returns**.
- **Miner Minions keep mining while an Expedition runs.**

## 5. Smith and equipment progression

Gold and mined ores pay for ordinary upgrades. **Rare Expedition materials are
required for major breakthroughs.**

| Pickaxe upgrade | Required resources |
|---|---|
| Level 1 → 4 | Gold + Iron |
| Level 4 → 5 | Gold + Iron + rare Expedition material |
| Level 5 onward | Additional normal levels and future breakthroughs |

The **boss's unique material** unlocks advanced progression or Smith
capabilities, while **Expedition materials** pay for breakthrough upgrades.

Proposed **Minion development tracks:** standard levels, rare-material
breakthroughs, and optional Essence-based Ascension.

## 6. Summoning and character collection

**Updated prototype decision: only three rarities.**

| Rarity | Description |
|---|---|
| Common | Basic, dependable Minions |
| Uncommon | Improved traits and abilities |
| Rare | Specialized, valuable Minions |

Minions are acquired through **shards**, not Gold or Iron. Players **choose
whether to summon from the Miner or the Adventure pool**.

**Proposed prototype shard probabilities** (examples for discussion):

| Shard | Common | Uncommon | Rare |
|---|---|---|---|
| Basic | 75% | 23% | 2% |
| Advanced | 0% | 85% | 15% |

- Premium shard types and Epic/Legendary rarities can come in a later development stage.
- **Duplicates automatically become shared Minion Essence**, used for optional
  improvements to existing Minions.
- **Named Miner Minions always have fixed traits.**
- For the prototype, keep summoning simple: no large character collection and no
  complicated pity system yet.

## 7. Economy and player retention

Guiding principles:

1. Every session contributes to meaningful progression.
2. Major boss victories and Expedition clears provide permanent unlocks.
3. Mining and Expedition production complement rather than replace each other.
4. Free players can obtain the Minions and resources required to complete every stage.
5. Returning after a long absence is rewarding, not punishing.
6. The shop provides optional monetization without mandatory paywalls.

Players should have **short-term goals** (upgrade a Minion) and **long-term
goals** (defeat a new boss and automate its Expedition).

The economy should be balanced around **time to the next meaningful upgrade**,
not simply how large resource numbers become.

## 8. Remaining design decisions

| Area | What we still need to decide | Ticket |
|---|---|---|
| Minion capacity | How many Miners work and Adventurers fight simultaneously | SCRUM-6, SCRUM-2 |
| Progression pacing | How long each tier should take | SCRUM-2 |
| Upgrade economy | Exact resource costs, production rates and scaling | SCRUM-2 |
| Summoning | Final shard distribution, free acquisition rates and pity | SCRUM-11, SCRUM-2 |
| Ascension | Whether it belongs in the initial prototype | SCRUM-10 |
| Tier progression | Exact prerequisites for unlocking the next mine tier | SCRUM-2, SCRUM-7 |
| Monetization | What the shop will sell | SCRUM-24 |

## 8a. Team decisions after v3.4 (2026-11-12)

| Question | Decision |
|---|---|
| Guardian Tickets (from the v1.1 design)? | **Scrapped.** Boss attempts are free and unlimited. |
| Mine stockpile cap? | **None.** Mining accumulates for as long as the player is away. |
| Is the Adventure team busy while an Expedition is automated? | **Yes.** Stop the Expedition (and collect) to use the team for a boss or a first clear. |

## 9. Prototype scope recommendation

For an initial playable economy prototype, focus on:

**Three mining tiers, three rarities, a small Miner and Adventure roster, one
boss per tier, a few Expeditions, and a functioning Smith.**

That should be enough to test whether the overall loop is enjoyable before
expanding the character roster or introducing more complex systems.

**Most important next step:** define **tier progression pacing** and the
**resource requirements for advancing**, because those values determine how
satisfying mining, upgrading, summoning and Expeditions feel together (SCRUM-2).
