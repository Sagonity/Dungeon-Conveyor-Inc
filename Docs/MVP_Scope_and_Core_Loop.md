# Dungeon Conveyor Inc. — MVP Scope & Core Loop

**Jira:** SCRUM-1 · **Sprint:** Discovery · **Status:** Draft for team review
**Platform:** Android first (iOS later), all four orientations (portrait and landscape) · **Engine:** Unity 6 (6000.6)

> All numbers in this doc are starting points so the prototype can be built.
> Final tuning belongs to SCRUM-2 (economy) and SCRUM-19 (balancing).

---

## 1. Pitch

**Short version:** Mine and explore to earn resources. Summon and upgrade your
Minions. Defeat powerful bosses to unlock deeper mines and new expeditions. Repeat.

**Long version:** You run a logistics and mining corporation in an underground
fantasy world. You summon magical helpers and monsters ("Minions"):
**Miner Minions** work the **Mines**, and **Adventure Minions** go on
**Expeditions** that run in the background while you do other things. When your
Adventure team is strong enough, you spend a **Guardian Ticket** to challenge the tier's
**Boss**. Beat it, and a deeper Mine and a new Expedition open up, with better rewards.

## 2. The most important design principle

> **Boss Battles are progression gates. Mining and Expeditions are repeatable farming.**

The player always has something useful to do, even if they can't beat the next
boss yet or are out of Guardian Tickets. A failed boss attempt sends you back to
farming, never to a dead end.

### Supporting pillars

1. **Each activity has its own rewards.** Mining and Expeditions never pay out
   the same thing, so neither becomes the one "efficient" option to farm.
2. **One boss, one clear unlock.** Each boss win unlocks the next Mining Tier
   *and* the next Expedition Tier together.
3. **Plan, then let them work.** The player's skill is in building and
   upgrading the team. Mining and Expeditions run in the background; the player
   is free to do other things in the game meanwhile. Boss fights play themselves.
4. **The belt is the signature.** Mined resources ride a conveyor up to HQ. This
   is the visual identity of the game and the payoff of Mining.
5. **Short sessions, readable on a phone, in any orientation.** Every screen
   works in all four rotations, portrait and landscape (SCRUM-3, SCRUM-12).

## 3. Core loop

```
   ┌───────────────────────────────────────────────────────────────┐
   │                                                               │
   ▼                                                               │
┌───────────────────────────┐                                      │
│ 1. MINING & EXPEDITIONS   │  Repeatable farming (auto-runs)      │
│    Mining  → Gold, Minerals, Upgrade Materials                   │
│    Expedition → Summon Shards, Rare Materials, Equipment         │
└─────────────┬─────────────┘                                      │
              ▼                                                    │
┌───────────────────────────┐                                      │
│ 2. SUMMON & UPGRADE       │  New Minions, better gear and stats  │
└─────────────┬─────────────┘                                      │
              ▼                                                    │
┌───────────────────────────┐   lose → keep farming, try again     │
│ 3. BOSS BATTLE            │  ─────────────────────────────────►  │
│    costs 1 Guardian Ticket│                                      │
└─────────────┬─────────────┘                                      │
              ▼ win                                                │
┌───────────────────────────┐                                      │
│ 4. UNLOCK NEW TIER        │  Next Mining Tier + next Expedition  │
│    + first-clear rewards  │  Tier, with better rewards           │
└─────────────┬─────────────┘                                      │
              └────────────────────────────────────────────────────┘
                     Repeat: stronger Minions, bigger challenges
```

### Loop timings

| Loop | Length | What the player does |
|---|---|---|
| **Moment** | 5–15 s | Watch ore ride the belt, collect an Expedition report, or watch a boss fight |
| **Session** | 3–8 min | Collect the Mine, collect and resend the Expedition, summon/upgrade, maybe try the boss |
| **Day** | 2–4 sessions | Use the regenerated Guardian Tickets, push toward the next tier |
| **Tier** | ~1–3 days (MVP pacing target) | Farm until the team beats the tier boss |

## 4. Systems

### System responsibilities

| System | Primary function | Rewards / output |
|---|---|---|
| **Mining** | Collect raw materials | Gold, Minerals, Upgrade Materials |
| **Expeditions** | Acquire special resources | Summon Shards, Rare Materials, Equipment |
| **Minions / Gacha** | Collect new Minions and build stronger teams | Minions (from Summon Shards) |
| **Upgrades** | Increase mining efficiency and combat power | Stronger Mines and Minions |
| **Boss Battles** | Test team strength and unlock new tiers | New tiers, first-clear rewards |
| **Guardian Tickets** | Limit boss attempts without stopping farming | — |

### 4.1 Mining

- **Miner Minions** assigned to the Mine dig continuously. Only Miner Minions can mine. Output rides the **conveyor
  belt** up to HQ and fills a **stockpile**, which the player collects.
- Runs while the player does other things (Expeditions, menus). Stops when the
  stockpile is full, which is what brings the player back.
- **Mining power** comes from the assigned Miner Minions' mining stat and their Tool.
- **Mine upgrades** (paid in Gold + Minerals): dig speed, belt speed, stockpile cap.
- Each tier's Mine yields more and adds a new Mineral type.
- Offline accumulation beyond the stockpile cap is post-MVP (SCRUM-16).

### 4.2 Expeditions

- The player picks a squad (up to 4 **Adventure Minions**) and an Expedition
  from an unlocked tier, and sends them off. The Expedition runs on a **timer in
  the background**; the player is free to mine, summon, upgrade or browse menus.
- The result is worked out from squad power vs. the Expedition's difficulty. When
  the timer ends the player collects a short **report** with the loot.
- Starting durations: 10 min (Tier 1), 20 min (Tier 2), 30 min (Tier 3).
  The playtest build uses shortened timers. The timer keeps running while the app is closed.
- MVP has **one squad out at a time**. More parallel squads can come later.
- Minions out on an Expedition can't be used in a boss fight until they return.
- **No energy cost.** Expeditions can always be run. This is where the player
  goes when out of Guardian Tickets.
- Rewards: **Summon Shards, Rare Materials and Equipment drops.**
- A squad that is too weak returns early with partial loot, never with nothing.
- The report also tells the player how strong the team is before they spend a ticket on the boss.

### 4.3 Minions & Gacha

- Summons cost **Summon Shards**, which come only from Expeditions. This keeps
  Expeditions essential for growing the roster.
- Starting rates: Common 75% / Rare 22% / Epic 3%. **Pity:** a guaranteed Epic within 50 summons.
- Duplicates raise a Minion's **star rank** (higher stat cap). They don't
  become Summon Shards, to avoid mixing up the two.
- Drop rates are shown in the game from day one (store compliance, SCRUM-25).
- **Two kinds of Minion, with separate jobs:**

| | Miner Minions | Adventure Minions |
|---|---|---|
| Work in | The Mine only | Expeditions and Boss Battles only |
| Key stats | Mining power, level, star rank | HP, ATK, level, star rank |
| Gear | Tool | Weapon, Armor |
| Roles | — | *Fighter* (damage), *Tank* (takes hits), *Support* (buffs the squad) |

- Because the two kinds never compete for the same job, there is no choice
  about who goes where. The choices are which Miners to upgrade, and which 4
  Adventure Minions make the best squad.
- **MVP roster:** 8 Minions: 3 Miner and 5 Adventure, spread across Common, Rare
  and Epic. Art and names come from SCRUM-13.

### 4.4 Upgrades

Each upgrade costs rewards from **both** farming systems, so neither one can be skipped:

| Upgrade | Cost | Effect |
|---|---|---|
| Minion level | Gold + Upgrade Materials (Mining) | Miner: mining power. Adventure: HP, ATK |
| Equipment upgrade | Gold (Mining) + Rare Materials (Expeditions) | Bigger gear bonuses |
| Mine upgrade | Gold + Minerals (Mining) | Dig speed, belt speed, stockpile cap |
| Star rank | Duplicate Minions (Gacha) | Higher level cap |

Equipment: Tools for Miner Minions; Weapons and Armor for Adventure Minions. 3 rarities. About 6–8 items in MVP, with fixed stats (no random stats).

### 4.5 Boss Battles & Guardian Tickets

- Each tier has one Boss. Fighting it costs **1 Guardian Ticket**.
- The fight is auto-combat with a squad of up to 4 Adventure Minions, played out on screen (about 30–60 s).
- **Win:** the next Mining Tier and Expedition Tier unlock, plus one-time
  first-clear rewards (a large batch of Summon Shards plus a guaranteed equipment piece).
- **Lose:** the ticket is spent. The player goes back to farming and upgrades.
- **Tickets:** max 3. One regenerates every 4 hours. A few are also given as
  first-clear rewards. Tickets are never needed for Mining or Expeditions.
- After a loss, the game shows *why* (for example "Boss HP left: 32%, your
  team's power is below the recommended level") so the player knows what to farm (SCRUM-19).

### 4.6 Tiers (MVP content)

One boss unlocks both the next Mine and the next Expedition.

| Tier | Zone (placeholder) | Mine yields | Expedition yields | Boss | Win unlocks |
|---|---|---|---|---|---|
| 1 | Fungal Mines | Gold, Copper | Shards, Spore Silk, Common gear | Spore Foreman | Tier 2 |
| 2 | Magma Works | + Iron | + Ember Core, Rare gear | Slag Golem | Tier 3 |
| 3 | Crystal Depths | + Mithril | + Prism Dust, Epic gear | Prism Wyrm | End of MVP content |

Zone and item names are placeholders until SCRUM-13. Special bosses that unlock
unique areas, Minions or systems come after launch.

### 4.7 Currencies & resources

| Resource | Source | Used for |
|---|---|---|
| Gold | Mining | Almost every upgrade |
| Minerals (one per tier) | Mining | Mine upgrades, gear crafting |
| Upgrade Materials | Mining | Minion levels |
| Summon Shards | Expeditions, boss first-clear | Summons |
| Rare Materials (one per tier) | Expeditions | Equipment upgrades |
| Equipment | Expeditions, boss first-clear | Equipping Minions |
| Guardian Tickets | Regenerate over time, first-clear | Boss attempts |

No premium currency in the MVP. It arrives with in-app purchases (section 4.8).

### 4.8 Monetization (at launch)

**Decided:** in-app purchases, plus ads that are **never forced**: the player
always chooses to watch one in exchange for a reward. No interstitial or pop-up ads.

The ideas below are proposals for SCRUM-2 and SCRUM-24:

- **In-app purchases:** a premium currency (name TBD) that buys summons,
  starter packs and cosmetic items.
- **Optional (rewarded) ads:** watch an ad to double an Expedition report's
  loot, cut time off a running Expedition, or get 1 extra Guardian Ticket per day (capped).
- **Fairness rule:** everything that gates progression (tiers, bosses) can be
  reached without paying. Money and ads speed things up; they never unlock content.
- **Compliance:** summon odds are shown in game. Some countries restrict paid
  gacha (for example Belgium), which affects the soft-launch markets (SCRUM-23, SCRUM-25).

### 4.9 Art direction

**Decided:** in the spirit of **Arcane** and **Borderlands**. These are style
references only; all characters, designs and assets are our own.

- **From Arcane:** painterly, hand-painted textures; moody, high-contrast
  lighting with glowing accents; a grimy industrial-fantasy world. The
  underground mines, pipes, furnaces and conveyor belts suit this well.
- **From Borderlands:** cel-shaded look with thick, inked outlines; bold
  comic-book shapes; exaggerated characters; irreverent humor.
- **Tone:** gritty world, funny characters. The "mining corporation" setting
  fits the humor: Minions as overworked employees, safety posters, HR memos.
- **Mobile readability first:** strong silhouettes and outlines help Minions
  and loot read on a small screen, in both portrait and landscape (SCRUM-12).
- The art pipeline, style guide and first 8 Minions are SCRUM-13.

## 5. MVP plan

**MVP = the vertical slice (SCRUM-14).** It is built in two steps:

- **Step A: core loop playtest (SCRUM-8).** Grey-box, Tier 1 + Tier 2.
  Proves that "farm → upgrade → boss → unlock" feels good.
- **Step B: vertical slice (SCRUM-14).** All 3 tiers with real art, full gacha and equipment.

| System | Step A: playtest | Step B: vertical slice | Ticket |
|---|---|---|---|
| Mining + conveyor + stockpile | ✅ grey-box belt | ✅ animated belt, Mine upgrades | SCRUM-5 |
| Assigning Miners to the Mine, Adventurers to the squad | ✅ simple lists | ✅ polished | SCRUM-6 |
| Expeditions (background timer + report) | ✅ 2 tiers, short timers | ✅ 3 tiers | SCRUM-7 |
| Boss Battles + Guardian Tickets | ✅ 2 bosses | ✅ 3 bosses, loss feedback | SCRUM-7 |
| Tier unlocks | ✅ | ✅ | SCRUM-7, SCRUM-9 |
| Minion stats, levels, equipment | ⚪ levels only, 2 fixed items | ✅ full | SCRUM-10 |
| Summon (gacha) | ⚪ flat rates, 4 grey-box Minions | ✅ rarities, pity, star rank, 8 Minions | SCRUM-11, SCRUM-13 |
| HQ screen (collect, upgrade) | ⚪ basic | ✅ | SCRUM-9 |
| Save / load | ⚪ local | ✅ local | SCRUM-17 |
| UI (all four orientations) | Grey-box, works in portrait and landscape | Design system, readable HUD | SCRUM-3, SCRUM-12 |

✅ = in scope · ⚪ = minimal / stubbed

## 6. Out of scope for MVP

- Implementing in-app purchases, premium currency and optional ads. They are planned for launch (section 4.8), but built in SCRUM-24 before the soft launch. Unity Purchasing stays unused until then.
- Offline rewards beyond the stockpile cap; quests, achievements (SCRUM-16)
- Special bosses, events, multiple banners, live ops (SCRUM-27)
- Cloud save, accounts, analytics (SCRUM-17)
- PvP, guilds, any social features
- Manual skills during combat, random gear stats, gear sets
- Auto-repeat or "sweep" of Expeditions, more than one squad out at a time
- Localization (SCRUM-25)

## 7. Where the current build stands

The current build (v0.0.1 + main menu PR) has:

- `GridManager` and `DraggableMinion`: the 3×3 grid and drag-to-move. **Not
  part of the game. Remove them.** `GameController.SummonMinion()` currently
  places minions on the grid, so that needs to change too.
- `MinionStats` / `MinionHealth`: base for Minion data. Move stats to a
  `ScriptableObject` (`MinionDefinition`) so Minions are data, not prefabs.
  How this is done is decided in SCRUM-4.
- `GameController.SummonMinion()` + HUD Summon button: the starting point for SCRUM-11.
- Main menu, settings (volume / mute) and the UI state machine are done.
  `GameUIBuilder` already scales the UI for both portrait and landscape, and the
  project allows all four rotations.

**Missing for Step A:** Mine + belt + stockpile, Expedition timer + report, auto-combat for bosses,
Boss Battle + Guardian Tickets, tier unlocks, resource counters, Miner/Adventure
Minion roster and assignment, save/load.

## 8. Success criteria

### Step A: core loop playtest (SCRUM-8)

Run with 5+ testers who haven't seen the game:

- [ ] Testers can explain "farm, upgrade, beat the boss, unlock" without help after 10 minutes
- [ ] After losing to a boss, at least 4 of 5 go back to farming without being told
- [ ] Testers keep both farming systems busy: they resend Expeditions and collect the Mine without being reminded
- [ ] At least 3 of 5 beat the Tier 1 boss in their first session
- [ ] No tester runs out of useful things to do (out of tickets and nothing worth farming)

### Step B: vertical slice (SCRUM-14)

- [ ] All 3 tiers can be cleared with a reachable roster
- [ ] Full loop with real art for the 8 Minions
- [ ] Holds 60 fps on a mid-range Android device (SCRUM-20)
- [ ] Save survives closing and reopening the app, and Expedition timers finish correctly while it's closed
- [ ] Every screen is usable in all four orientations, and rotating mid-session loses nothing

## 9. Decisions

| # | Question | Decision |
|---|---|---|
| 1 | Does a lost boss fight spend the Guardian Ticket? | **Yes.** Show clear loss feedback so it doesn't feel wasted. |
| 2 | Can a Minion both mine and go on Expeditions? | **No.** Miner Minions mine; Adventure Minions do Expeditions and Boss Battles. |
| 3 | Are Expeditions watched live? | **No.** They run in the background on a timer so the player can do other things. |
| 4 | Screen orientation? | **All four rotations**, portrait and landscape. |
| 5 | Do Miner and Adventure Minions come from the same summon? | **Yes.** One banner that can give either kind, paid with Summon Shards. |
| 6 | Expedition length? | **10 / 20 / 30 min** for Tiers 1–3, tuned in SCRUM-2. Shorter in the playtest build. |
| 7 | Monetization? | **In-app purchases + optional ads.** No forced ads. See 4.8. |
| 8 | Art tone? | **Arcane + Borderlands inspired.** See 4.9. |

## 10. Open questions

Proposed answers are in *italics*.

1. **2D or 3D?** Both styles can be done either way. 3D with a cel-shading
   outline shader is closest to Borderlands but costs more to make and to run on
   phones. Hand-painted 2D with thick outlines is cheaper and still fits.
   *Proposal: 2D hand-painted sprites with outlines for MVP.* Affects SCRUM-4 and SCRUM-13.
2. **Should purchases and ads be in the vertical slice, or added just before soft launch?**
   *Proposal: before soft launch (SCRUM-24). The vertical slice proves the fun first.*
3. **Can money or ads buy Guardian Tickets?**
   *Proposal: 1 extra per day from an optional ad, none for money. Paying past the
   boss gate would weaken the main design principle.*
