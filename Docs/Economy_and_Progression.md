# Expedition: Deepshaft — Economy, Progression & Rewards

**Jira:** SCRUM-2 · **Status:** First pass, simulated. Needs team review (owner: Victor)
**Numbers:** [`Tools/EconomySim/economy.json`](../Tools/EconomySim/economy.json) ·
**Results:** [Economy Simulation Report](Economy_Simulation_Report.md)
**Builds on:** [MVP Scope](MVP_Scope_and_Core_Loop.md) §3–4 · **Final tuning:** SCRUM-19 after the playtest

> ⚠️ **Out of date since Game Design Summary v3.4 (2026-11-12).** This first pass
> models the v1.1 design. The [Game Design Summary](Game_Design_Summary.md) changed
> the economy, so the numbers below and the simulator need a second pass (SCRUM-2):
>
> | v1.1 (this doc) | v3.4 |
> |---|---|
> | Minerals per tier (Copper → Iron → Mithril) + Upgrade Materials | Gold, Iron, Silver, Mithril; each deeper tier keeps all earlier ores and produces more. No Upgrade Materials, no ore conversion |
> | Expeditions on 10/20/30-min timers with partial loot | First clear is an autobattle that can fail; afterwards automated, can't fail, no time cap, no diminishing returns, one at a time |
> | Boss win unlocks next Mine + Expedition, costs a Guardian Ticket | Boss win guarantees a unique material and unlocks Expeditions; next mine tier needs prerequisites (TBD). **Guardian Tickets scrapped**; attempts are free |
> | Gear upgrades: Gold + Rare Materials | Smith: Gold + ores for normal levels, rare Expedition materials for breakthroughs, boss materials unlock Smith capabilities |
> | Common/Rare/Epic, one banner, pity at 50, duplicates → stars | Common/Uncommon/Rare, Miner or Adventure pool, Basic/Advanced shards, duplicates → Minion Essence, simple (no complex pity) |
> | Roles: Fighter, Tank, Support | Classes: Tank, Fighter, Archer, Mage, Support; Miners have fixed traits (Gold, ore, speed) |
> | Up to 3 Miners, squad of 4 | Team sizes undecided; the Adventure team is busy while an Expedition is automated |
> | Stockpile 2 h, up to 8 h | **No stockpile cap**: mining accumulates for as long as the player is away |
>
> **Next step (v3.4 §9):** define tier progression pacing and the resource
> requirements for advancing, balanced on *time to the next meaningful upgrade*.
> Then rebuild `economy.json` and `simulate.py` for the new loop.

This doc explains *how* the economy works and *why*. The actual numbers live in
one JSON file, and a simulator plays the game with bot players to check the
pacing. When a number changes, change the JSON, run the simulator, and look at
the report. Don't copy numbers into other docs.

---

## 1. Pacing targets

| Target | Value | From |
|---|---|---|
| Session length | 3–8 min | Scope §3 |
| Sessions per day | 2–4 (simulated: 3, 4 and 5) | Scope §3 |
| Tier 1 boss | Beaten **in the first session** (it's the tutorial payoff) | Scope §8: 3 of 5 testers |
| Tier 2 boss | **1–2 days** after install | Scope §3: ~1–3 days per tier |
| Tier 3 boss | **2–3 days after Tier 2** (end of MVP content) | Scope §3 |
| Every session | Has at least one useful thing to spend on | Pillar: always something to do |
| Mine | Sometimes full when the player returns, not always | Scope §4.1: the reason to come back |

**Simulated result (casual player, 4 sessions/day):** Tier 1 at 10 min, Tier 2 at
41.5 h, Tier 3 at 4.7 days (3.0 days after Tier 2). No session without
something to do. The Mine is full at 33% of session starts. Full results for
engaged, casual and light players are in the [report](Economy_Simulation_Report.md).

---

## 2. Where resources come from and where they go

Each activity pays out something the other can't, and every upgrade needs
**both** farming systems (Scope §2, pillar 1).

```
   MINING ──────────► Gold ─────────────┬──► Minion levels  ◄── Upgrade Materials ◄── MINING
     │                                  ├──► Mine upgrades  ◄── Minerals ◄─────────── MINING
     │                                  └──► Gear upgrades  ◄── Rare Materials ◄───── EXPEDITIONS
     ├──► Minerals (Copper/Iron/Mithril)
     └──► Upgrade Materials
                                             Summons ◄── Summon Shards ◄── EXPEDITIONS, boss first clear
   EXPEDITIONS ──► Summon Shards, Rare Materials, Equipment, a little Gold
   BOSS (1 ticket) ──► next tier + first-clear rewards (Shards, gear, +1 ticket)
```

| Resource | Sources | Spent on |
|---|---|---|
| Gold | Mining (most), Expeditions (a little) | Minion levels, Mine upgrades, gear upgrades |
| Upgrade Materials | Mining | Minion levels |
| Minerals (Copper → Iron → Mithril) | Mining; a deeper Mine still yields some of the previous mineral | Mine upgrades |
| Summon Shards | Expeditions, boss first clear | Summons |
| Rare Materials (Spore Silk → Ember Core → Prism Dust) | Expeditions; deeper Expeditions also drop the previous tier's material | Gear upgrades |
| Equipment | Expeditions (chance), boss first clear | Equipping Minions |
| Guardian Tickets | Regenerate (1 per 4 h, max 3), boss first clear (+1) | Boss attempts |

**What limits progress, by phase** (from the simulation):

| Phase | Limited by | So the player… |
|---|---|---|
| Tier 1 (first session) | Starter resources and the first Expedition | Learns collect → summon → level → boss in ~10 min |
| Tier 2 (day 1–2) | **Gold and Upgrade Materials** | Keeps the Mine running and collects it often |
| Tier 3 (day 2–5) | Gold early on, then **level caps** (stars from duplicates) and **gear** | Keeps sending Expeditions for Shards and Rare Materials |

---

## 3. Power

One number tells the player whether the squad is ready: **squad power**.

```
Minion power = ATK + HP / 10
Squad power  = sum of the 4 best Adventure Minions' power
               × 1.10 if the squad has a Support
```

- Bosses and Expeditions show a **recommended power**. At or above it, a boss fight
  is a sure win. Below 85% it's a sure loss. In between, the chance rises in a
  straight line (SCRUM-7 builds the real auto-combat to match this curve).
- Miners have **mining power (MP)** instead. They never fight.

### Stats

```
stat at level L = stat at level 1 × (1 + 0.15 × (L − 1))
```

| Minion | Kind | Role | Rarity | Lv 1 | Lv 10 | Lv 20 |
|---|---|---|---|---|---|---|
| Pebble Grub (`miner_c`) | Miner | — | Common | 10 MP | 23 MP | 38 MP |
| Spore Bruiser (`fighter_c`) | Adventure | Fighter | Common | 22 | 52 | 85 |
| Cart Shield (`tank_c`) | Adventure | Tank | Common | 22 | 52 | 85 |
| Drill Sergeant (`miner_r`) | Miner | — | Rare | 12 MP | 29 MP | 48 MP |
| Lamp Chaplain (`support_r`) | Adventure | Support | Rare | 22 (+10% squad) | 53 | 87 |
| Slag Knight (`fighter_r`) | Adventure | Fighter | Rare | 28 | 65 | 106 |
| Gilded Golem (`miner_e`) | Miner | — | Epic | 16 MP | 38 MP | 62 MP |
| Prism Duelist (`fighter_e`) | Adventure | Fighter | Epic | 35 | 83 | 136 |

Names are placeholders until SCRUM-13. Rarity multiplies base stats: Common ×1.0,
Rare ×1.25, Epic ×1.6. Roles split the same budget differently (Fighter: high ATK,
Tank: high HP, Support: lower stats + squad bonus), so all roles are worth about
the same power. The roles matter for the real combat in SCRUM-7.

### Levels and stars

| Rule | Value |
|---|---|
| Level-up cost (L → L+1) | Gold = 30 × 1.12^(L−1), Upgrade Materials = 1 + ⌊(L−1)/2⌋ |
| Level cap | 20, **+5 per star** (max 5 stars → 45) |
| Stars | +1 per duplicate summon |

Examples: level 1 → 2 costs 30 Gold + 1 Upgrade Material; level 19 → 20 costs
231 Gold + 10. The full table is in the report.

---

## 4. Mining

| Depth (= highest unlocked tier) | Per MP per hour |
|---|---|
| 1 · Fungal Mines | 14 Gold, 1.5 Copper, 0.30 Upgrade Materials |
| 2 · Magma Works | 16 Gold, 0.8 Copper, 1.5 Iron, 0.30 Upgrade Materials |
| 3 · Crystal Depths | 20 Gold, 0.8 Iron, 1.5 Mithril, 0.35 Upgrade Materials |

- **One Mine that goes deeper** when a tier unlocks (proposal, see §11). Up to
  **3 Miners** work it; total output = their MP × the table above × dig-speed bonus.
- **Dig speed:** +15% output per level, 10 levels. Costs Gold + the current mineral.
- **Stockpile:** holds **2 hours** of output, +1 hour per level, up to **8 hours**
  (a night's sleep). Storing it in hours, not amounts, means the cap never needs
  re-tuning when output grows.
- A new player (one Common Miner) makes 140 Gold, 15 Copper and 3 Upgrade
  Materials an hour.

---

## 5. Expeditions

| Tier | Expedition | Time | Recommended power | Full loot | Gear chance |
|---|---|---|---|---|---|
| 1 | Spore Caverns | 10 min | 60 | 5 Shards, 3 Spore Silk, 40 Gold | 25% (Common) |
| 2 | Magma Works | 20 min | 200 | 9 Shards, 3 Ember Core, 2 Spore Silk, 120 Gold | 25% (Rare) |
| 3 | Crystal Depths | 30 min | 520 | 14 Shards, 3 Prism Dust, 2 Ember Core, 300 Gold | 20% (Epic) |

- **Loot = full loot × (squad power ÷ recommended)**, between 25% and 100%. A weak
  squad always brings back something, never nothing (Scope §4.2).
- Gear chance is scaled the same way. Gear type is random: weapon, armor or tool.
- An Expedition's recommended power is half to two-thirds of the tier boss's. The
  squad can farm a tier well before it can beat that tier's boss.

---

## 6. Summons

| Rule | Value |
|---|---|
| Cost | 10 Summon Shards |
| Rates | Common 75%, Rare 22%, Epic 3% (shown in the game, SCRUM-25) |
| Pity | Guaranteed Epic within 50 summons |
| Duplicates | +1 star (higher level cap). They never turn into Shards (Scope §4.3) |
| Pool | All 8 Minions; Miners and Adventurers come from the same banner (Scope decision 5) |
| Tutorial | The very first summon is always Lamp Chaplain (Rare Support), so every new squad has a Support |

A casual player summons about 43 times by the end of Tier 3: about one Epic on
average, and a few stars on the Common Minions.

---

## 7. Equipment

| Rarity | Comes from | Max level | Weapon (ATK) | Armor (HP) | Tool (MP) |
|---|---|---|---|---|---|
| Common | Tier 1 Expedition | 10 | 4, +1.0/level | 40, +10/level | 3, +0.6/level |
| Rare | Tier 2 Expedition, Tier 1 boss | 15 | 8, +1.6/level | 80, +16/level | 6, +1.0/level |
| Epic | Tier 3 Expedition, Tier 3 boss | 20 | 14, +2.5/level | 140, +25/level | 10, +1.6/level |

- Slots: Adventure Minions have **Weapon + Armor**; Miners have a **Tool**.
- Upgrade cost: Gold = 40 × 1.15^(level−1), plus the gear tier's Rare Material:
  1 + ⌊(level−1)/3⌋. Fixed stats, no random rolls (Scope §6).

---

## 8. Bosses and Guardian Tickets

| Tier | Boss | Recommended power | First clear |
|---|---|---|---|
| 1 | Spore Foreman | 110 | 40 Shards, Rare Weapon, +1 ticket |
| 2 | Slag Golem | 380 | 60 Shards, Rare Armor, +1 ticket |
| 3 | Prism Wyrm | 800 | 100 Shards, Epic Weapon, +1 ticket |

- A fight costs **1 Guardian Ticket**, win or lose (Scope decision 1).
- Tickets: start with 3, max 3, **+1 every 4 hours**. First-clear tickets can go
  above the max.
- Win chance: 0% below 85% of recommended power, 100% at recommended power,
  a straight line in between. A loss shows how far off the squad was (Scope §4.5).
- Minions on an Expedition can't fight (Scope §4.2).

---

## 9. Starting state and the first session

| Start with | Why |
|---|---|
| 3 Minions: Pebble Grub (Miner), Spore Bruiser (Fighter), Cart Shield (Tank) | One of each job, so every system works on minute 1 |
| 300 Gold, 20 Upgrade Materials | Enough for a few level-ups in the tutorial |
| 20 Summon Shards | Two summons; the first is always Lamp Chaplain (Support) |
| 3 Guardian Tickets | The boss is available as soon as the squad is ready |

**Intended first session (~10–20 min):** collect the Mine → summon twice → send
the first Expedition (10 min) → level up the squad while it runs → collect the
report → summon again → beat the Spore Foreman → Tier 2 unlocks and the Mine goes
deeper. In the simulation this takes **about 10 minutes**, and every bot manages it.

---

## 10. Step A playtest build (SCRUM-8)

Step A uses a subset (Scope §5):

| System | Step A |
|---|---|
| Minions | 4 grey-box: Pebble Grub, Spore Bruiser, Cart Shield, Lamp Chaplain. Flat rates |
| Gear | 2 fixed items (one Weapon, one Tool), levels only |
| Tiers | 1 and 2 |
| Timers | **10× faster** (`playtest.timeScale`): Expeditions 1 and 2 min, tickets every 24 min, Mine 10× output |

In a 45-minute playtest at 10× speed, every bot beats Tier 1 and 14% also beat
Tier 2. That's a good stretch goal for the most engaged testers. The full
simulation uses the 8-Minion roster, so the Step A subset should be re-checked
once its content is final.

---

## 11. Decisions for the team

| # | Proposal | Why |
|---|---|---|
| 1 | **One Mine that goes deeper** when a tier unlocks (not one Mine per tier) | Simpler, and there are only 3 Miner slots anyway. The ◀ ▶ switcher in wireframe §4.3 is dropped |
| 2 | **Stockpile measured in hours** (2 h, up to 8 h) | The cap never needs re-tuning when output grows |
| 3 | **Starter pack** (300 Gold, 20 Upgrade Materials, 20 Shards) and a **guaranteed Support** first summon | Makes the Tier 1 boss reachable in the first session |
| 4 | **Recommended power = a sure win**; below 85% = sure loss | Players can trust the number, so a loss never feels random |
| 5 | **Deeper Expeditions and Mines also drop the previous tier's material** | Old gear stays upgradable without farming old content |
| 6 | **Stars add +5 to the level cap** (not +10) | +10 let Commons with many duplicates outgrow everything |

## 12. Known issues (for SCRUM-19)

- **Late Tier 3 surplus:** once the squad reaches its level caps (~day 3.5), Gold and
  Upgrade Materials pile up. Step B's monetization and cosmetics (Scope §4.8), or
  more sinks, can absorb this. For the MVP it only affects the last day or so.
- **Bots never lose a boss fight**, because they only try at 95% of recommended
  power. Real players will try earlier and lose sometimes; the playtest will show how often.
- **Light players** (3 sessions a day) beat Tier 3 at about 6.5 days, and 4% don't
  in 10 days. That's acceptable for a first pass.
- The bots spend cheapest-first. A player who saves up or focuses on one Minion
  will be somewhat faster.

## 13. How to change the numbers

1. Edit `Tools/EconomySim/economy.json`.
2. Run `python3 Tools/EconomySim/simulate.py` for a quick summary, or add
   `--report` to rewrite `Docs/Economy_Simulation_Report.md`.
3. Check the targets table at the top of the report.
4. When a system is built in Unity (SCRUM-5, 7, 10, 11), its data assets take their
   values from this JSON. Later, an editor import step can read the JSON directly,
   so there's one source of truth.
