# Dungeon Conveyor Inc. — MVP Scope & Core Loop

**Jira:** SCRUM-1 · **Sprint:** Discovery · **Status:** Draft for team review
**Platform:** Android first (iOS later), portrait, 1080×1920 reference · **Engine:** Unity 6 (6000.6)

> All numbers in this doc are starting points so the prototype can be built.
> Final tuning belongs to SCRUM-2 (economy) and SCRUM-19 (balancing).

---

## 1. Pitch

**Short version:** Mine and explore to earn resources. Summon and upgrade your
Minions. Defeat powerful bosses to unlock deeper mines and new expeditions. Repeat.

**Long version:** You run a logistics and mining corporation in an underground
fantasy world. You summon magical helpers and monsters ("Minions"), equip them,
and put them to work in the **Mines** and on auto-run **Expeditions**. When the
team is strong enough, you spend a **Guardian Ticket** to challenge the tier's
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
3. **Plan, then watch.** The player's skill is in building and upgrading the
   team. Runs and fights play themselves.
4. **The belt is the signature.** Mined resources ride a conveyor up to HQ. This
   is the visual identity of the game and the payoff of Mining.
5. **Short sessions, readable on a phone.** One-thumb portrait play (SCRUM-3, SCRUM-12).

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
| **Moment** | 5–15 s | Watch ore ride the belt, or a fight during an Expedition |
| **Session** | 3–8 min | Collect the Mine, run 1–3 Expeditions, summon/upgrade, maybe try the boss |
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

- Minions assigned to the Mine dig continuously. Output rides the **conveyor
  belt** up to HQ and fills a **stockpile**, which the player collects.
- Runs while the player does other things (Expeditions, menus). Stops when the
  stockpile is full, which is what brings the player back.
- **Mining power** comes from the assigned Minions' mining stat and their Tool.
- **Mine upgrades** (paid in Gold + Minerals): dig speed, belt speed, stockpile cap.
- Each tier's Mine yields more and adds a new Mineral type.
- Offline accumulation beyond the stockpile cap is post-MVP (SCRUM-16).

### 4.2 Expeditions

- The player picks a squad (up to 4 Minions) and an Expedition from an unlocked
  tier. The squad auto-runs a short series of encounters (fights and loot rooms)
  that plays out on screen in 1–3 minutes.
- **No energy cost.** Expeditions can always be run. This is where the player
  goes when out of Guardian Tickets.
- Rewards: **Summon Shards, Rare Materials and Equipment drops.**
- A squad that wipes keeps what it collected before the wipe.
- Expeditions also show the player how strong the team is before they spend a ticket on the boss.

### 4.3 Minions & Gacha

- Summons cost **Summon Shards**, which come only from Expeditions. This keeps
  Expeditions essential for growing the roster.
- Starting rates: Common 75% / Rare 22% / Epic 3%. **Pity:** a guaranteed Epic within 50 summons.
- Duplicates raise a Minion's **star rank** (higher stat cap). They don't
  become Summon Shards, to avoid mixing up the two.
- Drop rates are shown in the game from day one (store compliance, SCRUM-25).
- Each Minion has: name, rarity, role, HP, ATK, mining power, level, star rank,
  and 2 equipment slots (Tool, Weapon).
- **Roles:** *Fighter* (strong on Expeditions and vs bosses), *Miner* (strong
  in the Mine), *Support* (buffs the squad). A Minion works either in the Mine
  or on an Expedition, not both at once. This gives the player a real decision about who goes where.
- **MVP roster:** 8 Minions (3 Common, 3 Rare, 2 Epic). Art and names come from SCRUM-13.

### 4.4 Upgrades

Each upgrade costs rewards from **both** farming systems, so neither one can be skipped:

| Upgrade | Cost | Effect |
|---|---|---|
| Minion level | Gold + Upgrade Materials (Mining) | HP, ATK, mining power |
| Equipment upgrade | Gold (Mining) + Rare Materials (Expeditions) | Bigger gear bonuses |
| Mine upgrade | Gold + Minerals (Mining) | Dig speed, belt speed, stockpile cap |
| Star rank | Duplicate Minions (Gacha) | Higher level cap |

Equipment: 2 slots (Tool for mining, Weapon for combat), 3 rarities. About 6–8 items in MVP, with fixed stats (no random stats).

### 4.5 Boss Battles & Guardian Tickets

- Each tier has one Boss. Fighting it costs **1 Guardian Ticket**.
- The fight is auto-combat with the player's chosen squad.
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

No premium currency in MVP (see Open questions).

## 5. MVP plan

**MVP = the vertical slice (SCRUM-14).** It is built in two steps:

- **Step A: core loop playtest (SCRUM-8).** Grey-box, Tier 1 + Tier 2.
  Proves that "farm → upgrade → boss → unlock" feels good.
- **Step B: vertical slice (SCRUM-14).** All 3 tiers with real art, full gacha and equipment.

| System | Step A: playtest | Step B: vertical slice | Ticket |
|---|---|---|---|
| Mining + conveyor + stockpile | ✅ grey-box belt | ✅ animated belt, Mine upgrades | SCRUM-5 |
| Assigning Minions to Mine / squad | ✅ simple lists | ✅ polished | SCRUM-6 |
| Expeditions (auto-run) | ✅ 2 tiers | ✅ 3 tiers | SCRUM-7 |
| Boss Battles + Guardian Tickets | ✅ 2 bosses | ✅ 3 bosses, loss feedback | SCRUM-7 |
| Tier unlocks | ✅ | ✅ | SCRUM-7, SCRUM-9 |
| Minion stats, levels, equipment | ⚪ levels only, 2 fixed items | ✅ full | SCRUM-10 |
| Summon (gacha) | ⚪ flat rates, 4 grey-box Minions | ✅ rarities, pity, star rank, 8 Minions | SCRUM-11, SCRUM-13 |
| HQ screen (collect, upgrade) | ⚪ basic | ✅ | SCRUM-9 |
| Save / load | ⚪ local | ✅ local | SCRUM-17 |
| UI | Grey-box | Design system, readable HUD | SCRUM-3, SCRUM-12 |

✅ = in scope · ⚪ = minimal / stubbed

## 6. Out of scope for MVP

- Real-money purchases, premium currency, ads (Unity Purchasing stays unused until SCRUM-24)
- Buying or refilling Guardian Tickets
- Offline rewards beyond the stockpile cap; quests, achievements (SCRUM-16)
- Special bosses, events, multiple banners, live ops (SCRUM-27)
- Cloud save, accounts, analytics (SCRUM-17)
- PvP, guilds, any social features
- Manual skills during combat, random gear stats, gear sets
- Auto-repeat or "sweep" of Expeditions
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

**Missing for Step A:** Mine + belt + stockpile, Expedition runner, auto-combat,
Boss Battle + Guardian Tickets, tier unlocks, resource counters, Minion roster
and assignment, save/load.

## 8. Success criteria

### Step A: core loop playtest (SCRUM-8)

Run with 5+ testers who haven't seen the game:

- [ ] Testers can explain "farm, upgrade, beat the boss, unlock" without help after 10 minutes
- [ ] After losing to a boss, at least 4 of 5 go back to farming without being told
- [ ] Both farming systems are used: each gets at least 30% of farming time
- [ ] At least 3 of 5 beat the Tier 1 boss in their first session
- [ ] No tester runs out of useful things to do (out of tickets and nothing worth farming)

### Step B: vertical slice (SCRUM-14)

- [ ] All 3 tiers can be cleared with a reachable roster
- [ ] Full loop with real art for the 8 Minions
- [ ] Holds 60 fps on a mid-range Android device (SCRUM-20)
- [ ] Save survives closing and reopening the app

## 9. Open questions

Proposed answers are in *italics*.

1. **Does a lost boss fight spend the Guardian Ticket?**
   *Proposal: yes, otherwise tickets don't limit anything. Show clear loss
   feedback so it doesn't feel wasted.*
2. **Can a Minion be in the Mine and on an Expedition at the same time?**
   *Proposal: no. Splitting the roster is a meaningful choice and a reason to summon more.*
3. **How long is an Expedition: watched live (1–3 min) or a timer you come back to (e.g. 30 min)?**
   *Proposal: watched live for MVP; it's more fun to playtest. Timers can come later.*
4. **Portrait only?** The project currently allows all four rotations. *Proposal: lock to portrait.*
5. **Monetization at launch:** gacha IAP, ads, or a premium price?
   *Proposal: decide in SCRUM-2. Nothing in the MVP.*
6. **Art tone:** cute, gritty or comedic-corporate? *Proposal: comedic-corporate.* Affects SCRUM-13.
