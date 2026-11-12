# Expedition: Deepshaft — MVP Scope & Core Loop

*Working title. Previously "Dungeon Conveyor Inc."*

**Jira:** SCRUM-1 · **Sprint:** Discovery · **Status:** In review, v2.1 (2026-11-12), updated to Game Design Summary v3.4
**Platform:** Android first (iOS later), all four orientations (portrait and landscape) · **Engine:** Unity 6 (6000.6)

> **Design source of truth:** [Game Design Summary v3.4](Game_Design_Summary.md).
> This doc turns it into a build plan. All numbers are starting points;
> the economy and pacing are SCRUM-2 ([Economy, Progression & Rewards](Economy_and_Progression.md)),
> final tuning is SCRUM-19.

---

## 1. Pitch

**Short version:** Mine to earn Gold and ores. Upgrade your gear at the Smith and
strengthen your Minions. Defeat each mine tier's boss, clear its Expeditions, then
automate them to farm rare materials forever, even while your phone is locked.
Break through and dig deeper. Repeat.

**Long version:** You run a logistics and mining corporation in an underground
fantasy world. **Miner Minions** automatically mine the deepest tier you have
unlocked, while **Adventure Minions** fight. Beat a **Mine Tier Boss** to get its
unique material and open new **Expeditions**. The first clear of an Expedition is
a real autobattle that can fail; after that it runs **automatically** and can't
fail, earning rare materials for as long as you let it. Those materials pay for
the **breakthroughs** that take your gear, your Minions and your mine to the next tier.

## 2. The most important design principle

> **Every session is worthwhile, and being offline is never punished.**

Boss fights and Expedition first clears are the progression gates and give
**permanent unlocks**. Mining and automated Expeditions are the repeatable
production that keeps running in the background, including offline.

### Supporting pillars (from v3.4 §7)

1. **Every session contributes to meaningful progression.** There is always a
   short-term goal (upgrade a Minion) and a long-term goal (beat a new boss and automate its Expedition).
2. **Mining and Expeditions complement each other.** Mining pays Gold and ores for
   ordinary upgrades; Expeditions pay the rare materials for breakthroughs. Neither replaces the other.
3. **Permanent unlocks.** A boss win and an Expedition first clear are never lost.
4. **Returning after a long absence is rewarding.** Automated Expeditions have no
   time cap and no diminishing returns.
5. **Fair to free players.** Every Minion and resource needed to finish every stage
   can be earned for free. The shop is optional; there are no paywalls.
6. **Balanced on time to the next meaningful upgrade**, not on how big the numbers get.
7. **Short sessions, readable on a phone, in any orientation** (SCRUM-3, SCRUM-12).
   Screen layouts: [UI Wireframes & Readability](UI_Wireframes_and_Readability.md).
   Code structure: [Technical Architecture](Technical_Architecture.md) (SCRUM-4).

## 3. Core loop

```
   ┌──────────────────────────────────────────────────────────────────┐
   ▼                                                                  │
┌──────────────────────────────┐                                      │
│ 1. MINING (always running)   │  Miners auto-mine the highest tier:  │
│    Gold + ores               │  Gold, Iron, then Silver, Mithril    │
└──────────────┬───────────────┘                                      │
               ▼                                                      │
┌──────────────────────────────┐                                      │
│ 2. SMITH & MINIONS           │  Upgrade equipment, level Minions    │
└──────────────┬───────────────┘                                      │
               ▼                                                      │
┌──────────────────────────────┐   lose → keep mining and upgrading   │
│ 3. MINE TIER BOSS            │  ──────────────────────────────────► │
│    win: unique boss material │                                      │
│    + new Expeditions unlock  │                                      │
└──────────────┬───────────────┘                                      │
               ▼                                                      │
┌──────────────────────────────┐   fail → upgrade, try again          │
│ 4. EXPEDITION FIRST CLEAR    │  ──────────────────────────────────► │
│    autobattle, bonus rewards │                                      │
│    unlocks automation        │                                      │
└──────────────┬───────────────┘                                      │
               ▼                                                      │
┌──────────────────────────────┐                                      │
│ 5. AUTOMATED EXPEDITION      │  Can't fail, no time cap, offline    │
│    rare materials over time  │                                      │
└──────────────┬───────────────┘                                      │
               ▼                                                      │
   Breakthroughs (Smith + Minions) → next mine tier ──────────────────┘
```

### Loop timings

| Loop | Length | What the player does |
|---|---|---|
| **Moment** | 5–15 s | Collect production, do an upgrade, watch a fight |
| **Session** | 3–8 min | Collect, upgrade at the Smith, level or summon Minions, maybe try a boss or a first clear |
| **Day** | 2–4 sessions | Push toward the next boss or breakthrough; mining and automation earn between sessions |
| **Tier** | To be decided (SCRUM-2) | Pacing per tier is the most important open number (v3.4 §8) |

## 4. Systems

### System responsibilities

| System | Primary function | Rewards / output |
|---|---|---|
| **Mining** | Automatic resource production | Gold, Iron, Silver, Mithril |
| **Smith** | Equipment upgrades and breakthroughs | Stronger tools and gear |
| **Minions** | Mining (Miners) and combat (Adventurers) | Production and squad power |
| **Summoning** | Collect Minions with shards | Minions, Minion Essence (duplicates) |
| **Mine Tier Boss** | Progression gate | Unique boss material, new Expeditions |
| **Expeditions** | First clear, then automated farming | Bonus rewards, rare Expedition materials |

### 4.1 Mining

- **Miner Minions automatically mine the highest unlocked tier.** There is no
  choice of mine: when a deeper tier unlocks, they move there.
- **Every tier keeps all earlier resources and produces more of them**, plus new
  ones (v3.4 §2): Tier 1 Gold + Iron, Tier 2 more Gold + Iron, Tier 3 adds a little
  Silver, Tier 5 adds Mithril. Illustrative rates per hour (not final):

| Tier | Gold | Iron | Silver | Mithril |
|---|---|---|---|---|
| 1 | 100 | 40 | — | — |
| 2 | 150 | 65 | — | — |
| 3 | 220 | 90 | 10 | — |
| 4–6 | post-MVP | | | |

- **No ore conversion.** Older ores stay available automatically.
- Mining keeps running while an Expedition runs and while the app is closed.
- Output depends on the Miners' mining stats and fixed traits (Gold, ore or speed) and their equipment.
- **No stockpile cap (decided).** Mining keeps adding up for as long as the player is
  away, the same as automated Expeditions. Returning after a long absence is always rewarding.

### 4.2 Bosses

- **One Mine Tier Boss per tier.** Bosses follow a Valheim-like model:
  beating one **guarantees a unique progression material** and **unlocks new Expeditions**.
- The boss material unlocks advanced progression and new Smith capabilities (§4.5).
- The fight is an autobattle with the Adventure team, played out on screen (about 30–60 s).
- A loss costs nothing permanent. The game shows *why* (for example "Boss HP left 32%,
  your team is below the recommended power") so the player knows what to upgrade (SCRUM-19).
- **No Guardian Tickets (decided, scrapped).** Boss attempts are free and unlimited;
  the only gate is the team's power.

### 4.3 Expeditions

Expeditions work differently before and after their first completion:

| | First clear | After first clear (automated) |
|---|---|---|
| Can it fail? | Yes, it's a real autobattle | No |
| Needs | A capable Adventure team | — |
| Rewards | Bonus rewards (one time) | Rare Expedition materials over time |
| Result | **Unlocks automation permanently** | Keeps running, including while the phone is locked |

- **Only one Expedition can be active at a time.**
- The player can **stop an automated Expedition at any time** and collect what it has earned.
- Automated rewards have **no time cap and no diminishing returns**.
- Miners keep mining while an Expedition runs.
- **The Adventure team is busy while an Expedition is automated (decided).** To fight a
  boss or try another first clear, the player stops the automated Expedition (collecting its
  rewards) to free the team. Miners are never affected.

### 4.4 Minions

| | Miner Minions | Adventure Minions |
|---|---|---|
| Purpose | Resource production | Combat (bosses, Expedition first clears) |
| Specialization | **Fixed trait** per named character: Gold, ore or mining speed | **Combat class:** Tank, Fighter, Archer, Mage, Support |
| Progression | Mining efficiency and yield | Combat stats and abilities |
| Acquisition | Summoning shards (Miner pool) | Summoning shards (Adventure pool) |

**Development tracks** (v3.4 §5):

1. **Standard levels:** Gold + ores.
2. **Breakthroughs:** need rare Expedition materials; each raises the level cap.
3. **Ascension:** optional, paid with Minion Essence. **Open:** in the prototype or not (SCRUM-10).

**Open:** how many Miners work and how many Adventurers fight at the same time (SCRUM-6).

### 4.5 Smith and equipment

- **Ordinary upgrades** cost Gold + mined ores.
- **Breakthrough upgrades** also need a rare Expedition material.
- The **boss's unique material** unlocks advanced progression or new Smith capabilities.

| Pickaxe upgrade | Cost |
|---|---|
| Level 1 → 4 | Gold + Iron |
| Level 4 → 5 (breakthrough) | Gold + Iron + rare Expedition material |
| Level 5 onward | More normal levels and later breakthroughs |

Exact costs and scaling: SCRUM-2. Equipment: tools for Miners, weapons and armor for
Adventurers, with fixed stats (no random rolls).

### 4.6 Summoning

- **Three rarities in the prototype:** Common (basic, dependable), Uncommon
  (improved traits and abilities), Rare (specialized, valuable).
- Minions come from **shards**, never Gold or ores. The player **picks the Miner or the Adventure pool**.
- Proposed rates (for discussion, final in SCRUM-2/SCRUM-11):

| Shard | Common | Uncommon | Rare |
|---|---|---|---|
| Basic | 75% | 23% | 2% |
| Advanced | 0% | 85% | 15% |

- **Duplicates become shared Minion Essence**, used for optional improvements to owned Minions.
- Named Miner Minions always have the same trait.
- **Keep it simple for the prototype:** small roster, no complicated pity system.
  Premium shards and Epic/Legendary rarities come later.
- Rates are shown in the game (store compliance, SCRUM-25).
- **Open:** where shards come from and how many free players get (SCRUM-2).

### 4.7 Resources

| Resource | Source | Used for |
|---|---|---|
| Gold | Mining (all tiers) | Almost every upgrade |
| Iron | Mining (Tier 1+) | Smith and Minion upgrades |
| Silver | Mining (Tier 3+) | Higher-tier upgrades |
| Mithril | Mining (Tier 5+, post-MVP) | Higher-tier upgrades |
| Rare Expedition materials (one per tier) | Expeditions (first clear bonus, automation) | Breakthroughs |
| Unique boss materials (one per boss) | Mine Tier Boss win (guaranteed) | Advanced progression, Smith capabilities |
| Basic / Advanced Shards | To be decided (SCRUM-2) | Summoning |
| Minion Essence | Duplicate summons | Optional Minion improvements (Ascension) |

Removed in v2.0: Copper, Upgrade Materials, star ranks, Summon Shards as a single type.

### 4.8 Art direction

**Decided:** in the spirit of **Arcane** and **Borderlands**. These are style
references only; all characters, designs and assets are our own.

- **From Arcane:** painterly, hand-painted textures; moody, high-contrast
  lighting with glowing accents; a grimy industrial-fantasy world. The
  underground mines, pipes and furnaces suit this well.
- **From Borderlands:** cel-shaded look with thick, inked outlines; bold
  comic-book shapes; exaggerated characters; irreverent humor.
- **Tone:** gritty world, funny characters. The "mining corporation" setting
  fits the humor: Minions as overworked employees, safety posters, HR memos.
- **Mobile readability first:** strong silhouettes and outlines help Minions
  and loot read on a small screen, in both portrait and landscape (SCRUM-12).
- **Format (decided): 2D for now, built from 3D.** Characters and objects are
  modelled in 3D, then rendered out as 2D sprites with the cel-shaded, outlined
  look baked in. A few elements may be real-time 3D in the scene.
  This keeps the game light on phones while making a later move to more 3D easier.
- The art pipeline (3D model → sprite render), style guide and first 8 Minions are SCRUM-13.

### 4.9 Monetization (at launch)

**Decided:** in-app purchases, plus ads that are **never forced** (the player
always chooses to watch one for a reward). No interstitial or pop-up ads.

- **v3.4 rule:** the shop is optional monetization with **no mandatory paywalls**.
  Free players can get every Minion and resource needed to finish every stage.
- **Open:** what the shop sells (premium shard types are one candidate). SCRUM-24.
- **Compliance:** summon odds are shown in the game. Some countries restrict paid
  gacha (for example Belgium), which affects the soft-launch markets (SCRUM-23, SCRUM-25).

## 5. MVP plan

**MVP = the vertical slice (SCRUM-14).** It is built in two steps:

- **Step A: economy prototype / core loop playtest (SCRUM-8).** Grey-box. Scope from
  v3.4: **three mining tiers, three rarities, a small Miner and Adventure roster, one
  boss per tier, a few Expeditions and a working Smith.** Proves the loop is fun
  before the roster or systems grow.
- **Step B: vertical slice (SCRUM-14).** The same loop with real art, polished UI,
  in-app purchases and optional ads.

| System | Step A: prototype | Step B: vertical slice | Ticket |
|---|---|---|---|
| Mining (auto, highest tier, Gold/Iron/Silver) | ✅ grey-box | ✅ animated | SCRUM-5 |
| Assigning Miners and the Adventure team | ✅ simple lists | ✅ polished | SCRUM-6 |
| Mine Tier Bosses (autobattle) | ✅ 3 bosses | ✅ loss feedback | SCRUM-7 |
| Expeditions: first clear + automation | ✅ a few Expeditions | ✅ | SCRUM-7 |
| Smith: upgrades + breakthroughs | ✅ pickaxe + a few items | ✅ | SCRUM-9, SCRUM-10 |
| Minion levels + breakthroughs | ✅ | ✅ | SCRUM-10 |
| Ascension (Essence) | ⚪ only if decided | ⚪ decide after playtest | SCRUM-10 |
| Summoning (2 pools, 3 rarities, Essence) | ✅ simple rates, no pity | ✅ rates shown | SCRUM-11 |
| Tier progression | ✅ | ✅ | SCRUM-2, SCRUM-7 |
| Save / load, offline progress | ⚪ local | ✅ local | SCRUM-16, SCRUM-17 |
| In-app purchases + optional ads | — | ✅ test purchases only | SCRUM-24 (pulled forward) |
| UI (all four orientations) | Grey-box screens | Design system, readable HUD | SCRUM-3, SCRUM-12 |

✅ = in scope · ⚪ = minimal / to decide

## 6. Out of scope for MVP

- Mine tiers 4–6 (Mithril), more than one active Expedition
- Epic/Legendary rarities, premium shard types, a large roster, a complex pity system
- Quests and achievements (SCRUM-16); events and live ops (SCRUM-27)
- Cloud save, accounts, analytics (SCRUM-17)
- PvP, guilds, any social features
- Manual skills during combat, random gear stats, gear sets
- Ore conversion (not planned at all)
- Localization (SCRUM-25)

## 7. Where the current build stands

- **Done:** main menu, settings, UI state machine, safe-area UI for all four rotations,
  wireframes for the v1.1 screens (SCRUM-3; need updating for v3.4, see that doc).
- **Done (SCRUM-4), updated for v3.4:** the `Deepshaft.Core` assembly:
  - `ResourceId`: Gold, Iron, Silver, Mithril, Basic/Advanced Shards, Minion Essence,
    rare Expedition materials and unique boss materials. Copper and Upgrade Materials are retired.
  - `MinionDefinition`: Miner/Adventure kind, combat class (Tank, Fighter, Archer, Mage, Support),
    fixed Miner trait (Gold, ore, speed), rarity (Common, Uncommon, Rare), level cap per breakthrough.
  - `OwnedMinion`: level and breakthroughs (star rank removed).
  - Save format version 2, with a migration that drops the retired resources from version 1 saves.
  - `Wallet`, `IClock` (UTC timestamps for offline progress), `GameState`, `SaveService`, EditMode tests.
- **Still to replace:** `MinionStats` / `MinionHealth` and `GameController.SummonMinion()`
  (placeholder Minion), moving to `MinionDefinition` / `OwnedMinion` (SCRUM-10, SCRUM-11).
- **Still to change:** `GameController` sets `Time.timeScale = 0` while a menu is open.
  New systems must use `IClock`, not game time.

**Missing for Step A:** auto-mining, Smith, Mine Tier Bosses and autobattle, Expedition
first clear + automation, tier progression, summoning with two pools, resource counters
in the HUD, and connecting the save to the game (`GameSession`, SCRUM-17).

## 8. Success criteria

### Step A: economy prototype / playtest (SCRUM-8)

Run with 5+ testers who haven't seen the game:

- [ ] Testers can explain "mine, upgrade, beat the boss, clear and automate the Expedition, break through" after 10 minutes
- [ ] After losing a boss or first clear, at least 4 of 5 go back to upgrading without being told
- [ ] Testers understand that Expedition materials are needed for breakthroughs
- [ ] Every session ends with at least one meaningful upgrade
- [ ] Testers who return after a long break feel rewarded, not punished
- [ ] All 3 tiers can be cleared without paying

### Step B: vertical slice (SCRUM-14)

- [ ] All 3 tiers can be cleared with a free roster
- [ ] Full loop with real art for the prototype roster
- [ ] Holds 60 fps on a mid-range Android device (SCRUM-20)
- [ ] A test purchase and an optional ad both complete and give the right reward; no ad ever plays without the player choosing it
- [ ] Mining and automated Expeditions progress correctly while the app is closed or the phone is locked
- [ ] Every screen is usable in all four orientations, and rotating mid-session loses nothing

## 9. Decisions

| # | Question | Decision |
|---|---|---|
| 1 | Design source of truth? | **Game Design Summary v3.4** ([doc](Game_Design_Summary.md)). |
| 2 | Can a Minion both mine and fight? | **No.** Miner Minions mine; Adventure Minions fight. |
| 3 | Which tier do Miners mine? | **Always the highest unlocked tier**, which keeps all earlier resources. |
| 4 | Ore conversion? | **No.** Older ores stay available automatically. |
| 5 | What does a boss win give? | **A guaranteed unique material + new Expeditions.** |
| 6 | Can Expeditions fail? | **Only the first clear.** Automation can't fail. |
| 7 | Expeditions at once? | **One.** It can be stopped any time; rewards have no cap or diminishing returns. |
| 7a | Guardian Tickets? | **Scrapped.** Boss attempts are free and unlimited. |
| 7b | Mine stockpile cap? | **None.** Mining accumulates for as long as the player is away. |
| 7c | Is the Adventure team busy while an Expedition is automated? | **Yes.** Stop the Expedition to use the team for a boss or a first clear. |
| 8 | Rarities? | **Common, Uncommon, Rare** in the prototype. Epic/Legendary later. |
| 9 | Summon pools? | **Player picks Miner or Adventure.** Paid with shards, not Gold or ores. |
| 10 | Duplicates? | **Become shared Minion Essence.** (Replaces star rank.) |
| 11 | Adventure classes? | **Tank, Fighter, Archer, Mage, Support.** |
| 12 | Monetization? | **In-app purchases + optional ads**, no paywalls. What the shop sells is open. |
| 13 | Screen orientation? | **All four rotations**, portrait and landscape. |
| 14 | Art? | **Arcane + Borderlands inspired**, 2D sprites made from 3D. See 4.8. |
| 15 | Conveyor belt? | **No.** |
| 16 | Game name? | **Expedition: Deepshaft** (working title). Check stores and trademarks (SCRUM-25). |

## 10. Open questions (v3.4 §8)

| Area | Question | Ticket |
|---|---|---|
| Minion capacity | How many Miners work and Adventurers fight at the same time? | SCRUM-6 |
| Progression pacing | How long should each tier take? **Most important next step.** | SCRUM-2 |
| Upgrade economy | Exact costs, production rates and scaling | SCRUM-2 |
| Summoning | Final shard rates, free shard income, pity | SCRUM-11, SCRUM-2 |
| Ascension | In the initial prototype or not? | SCRUM-10 |
| Tier progression | Exact prerequisites for the next mine tier (boss win + breakthrough?) | SCRUM-2, SCRUM-7 |
| Monetization | What does the shop sell? | SCRUM-24 |

## 11. Change log

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-11-11 | Scope final. Linked the UI wireframes (SCRUM-3) and updated "Where the current build stands" after the cleanup. |
| 1.1 | 2026-11-12 | Updated "Where the current build stands" after the core architecture (SCRUM-4). Linked the Technical Architecture doc. |
| 2.0 | 2026-11-12 | Rewritten for Game Design Summary v3.4: auto-mining of the highest tier (Gold, Iron, Silver, Mithril), Smith and breakthroughs, boss materials, Expedition first clear + automation, 3 rarities (Common/Uncommon/Rare), Miner/Adventure summon pools, Minion Essence, 5 combat classes, new prototype scope. Guardian Tickets and the Mine stockpile cap moved to open questions. |
| 2.1 | 2026-11-12 | Team decisions: Guardian Tickets scrapped, no Mine stockpile cap, the Adventure team is busy while an Expedition is automated. |
