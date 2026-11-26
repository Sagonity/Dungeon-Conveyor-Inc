# Expedition: Deepshaft — Economy, Progression & Rewards

**Jira:** SCRUM-2 (rewritten for the current build in SCRUM-33) · **Status:** Describes the numbers in the build, 2026-11-24.
Tuning comes after the playtest (SCRUM-8, then SCRUM-2 / SCRUM-19).
**Design source:** [Game Design Summary v3.4](Game_Design_Summary.md) · **Build plan:** [MVP Scope](MVP_Scope_and_Core_Loop.md)
**Pacing check:** `Assets/Tests/EditMode/PlaythroughTests.cs` (replaces the old v1.1 Python simulator)

Every number here is a **first pass**. They all live on the `GameDatabase` asset (`Assets/Resources/GameDatabase.asset`)
or a Minion asset (`Assets/Content/Minions`), so they can be tuned in the Inspector without code. After any change,
run `PlaythroughTests` (§9) to see the effect on pacing.

---

## 1. The loop in numbers

```
 Mine (Gold, Iron, Silver…) ──▶ Minion levels + gear levels ──▶ team power ──▶ beat the tier boss
      ▲                                                                            │
      │                                  boss material ──▶ Smith rank              ▼
      │                                                              Expedition opens ──▶ first clear
      │                                                                                     │
      └── next mine tier (richer ore) ◀── breakthroughs ◀── rare material ◀── automate ◀────┘
```

| Resource | Comes from | Pays for |
|---|---|---|
| Gold, Iron | Mine (all tiers), first-clear bonuses, Tier 1 boss first win (Gold) | Minion levels, gear levels, breakthroughs |
| Silver | Mine from Tier 3 | Gear levels from 9, the 3rd gear breakthrough |
| Mithril | Mine from Tier 5 (after the prototype) | Nothing yet |
| Spore Silk / Ember Core / Prism Dust | Automated Expeditions (Tier 1 / 2 / 3), first clears | Minion and gear breakthroughs 1 / 2 / 3 |
| Foreman Sigil / Golem Heart / Wyrm Scale | Every win against the Tier 1 / 2 / 3 boss | Smith ranks 1 / 2 / 3 |
| Basic Shards | Boss first wins, first clears | Summons (Basic) |
| Advanced Shards | Boss first wins (10 each) | Summons (Advanced) |
| Minion Essence | Duplicate summons | Nothing yet (Ascension isn't in the prototype) |

---

## 2. New game

| | Value | Where |
|---|---|---|
| Starting resources | 400 Gold, 150 Iron (once, new games only; was 300 + 120 before SCRUM-36) | GameDatabase → New Game Resources |
| Starter Minions | Common Miner (Gold trait), Common Ore Miner; Common Tank, Fighter, Support, Mage, Archer | GameDatabase → Starter Minions |
| Slots | 3 Miners, 3 in the Adventure team | GameDatabase → Roster |
| Starting tier | Tier 1 (Gold Shallows) | |

---

## 3. Mining

Output per hour = Σ working Miners: **tier rate × (mining power ÷ 10) × (1 + tool bonus) × (1 + trait bonus)**.
Miners always mine the highest unlocked tier; there's **no stockpile cap**.

| Tier (per reference Miner, per hour) | Gold | Iron | Silver | Mithril |
|---|---|---|---|---|
| 1 Gold Shallows | 100 | 40 | – | – |
| 2 Iron Seams | 150 | 65 | – | – |
| 3 Silver Veins | 220 | 90 | 10 | – |
| 4 (post-prototype) | 320 | 125 | 25 | – |
| 5 (post-prototype) | 450 | 170 | 45 | 5 |
| 6 (post-prototype) | 600 | 230 | 75 | 15 |

- **Mining power:** 10 at level 1, +2 per level (Common). Uncommon ×1.25, Rare ×1.6.
- **Traits:** Gold +25% Gold · Ore +25% on everything except Gold · Speed +15% on everything.
- **Tool (Pickaxe):** +10% output per level above 1.

---

## 4. Minions

| | Common | Uncommon | Rare |
|---|---|---|---|
| HP at level 1, per level | 100, +10 | 125, +12.5 | 160, +16 |
| ATK at level 1, per level | 10, +1 | 12.5, +1.25 | 16, +1.6 |
| Mining power at level 1, per level | 10, +2 | 12.5, +2.5 | 16, +3.2 |

**Levels** (same table for Miners and Adventurers; GameDatabase → Minion Progression):

- Cost from level L to L+1: **25 × 1.12^(L−1) Gold** and **10 × 1.12^(L−1) Iron**, rounded.
  Examples: level 1→2 = 25 Gold + 10 Iron; 10→11 = 69 + 28; 19→20 = 192 + 77.
- **Level cap 20**, raised by 10 per breakthrough: 20 → 30 → 40 → 50.

**Breakthroughs** (at the level cap):

| # | Cost | New cap |
|---|---|---|
| 1 | 1,000 Gold + 400 Iron + 5 Spore Silk | 30 |
| 2 | 3,000 Gold + 1,200 Iron + 5 Ember Core | 40 |
| 3 | 8,000 Gold + 3,000 Iron + 5 Prism Dust | 50 |

**Ascension** (Minion Essence) is not in the prototype (team decision 2026-11-19).

---

## 5. Gear and the Smith

Every Minion has its **own** gear (team decision 2026-11-19): a Pickaxe for Miners, a Weapon and Armor for
Adventurers. All start at level 1 with no bonus.

| Item | Bonus per level above 1 |
|---|---|
| Pickaxe | +10% mining output |
| Weapon | +8% ATK |
| Armor | +8% HP |

- **Gear level cost** from level L: **40 × 1.25^(L−1) Gold + 20 × 1.25^(L−1) Iron**, plus **10 × 1.25^(L−9) Silver from level 9**.
- **Gear cap 4**, +4 per gear breakthrough (pickaxe 4 → 5 is the first breakthrough, as in v3.4 §5).

| Gear breakthrough | Cost | Needs Smith rank |
|---|---|---|
| 1 | 300 Gold + 150 Iron + 3 Spore Silk | 1 (Sigil Forging: 1 Foreman Sigil) |
| 2 | 900 Gold + 450 Iron + 3 Ember Core | 2 (Golem Tempering: 1 Golem Heart) |
| 3 | 2,500 Gold + 1,200 Iron + 150 Silver + 3 Prism Dust | 3 (Wyrmscale Smithing: 1 Wyrm Scale) |

Smith ranks are learned **once each** (team decision 2026-11-19).

---

## 6. Combat, bosses and Expeditions

**Team power** = Σ team (ATK + HP ÷ 10), ×1.1 if the team has a Support. Bosses and Expeditions show a
**recommended power**; fights are a short autobattle (60 s limit, ±10% damage) and can be lost.

| Class | Behaviour |
|---|---|
| Tank | Enemies hit it first; takes 25% less damage; deals 70% |
| Fighter | Plain attacker |
| Archer | Attacks faster (every 0.8 s), deals 85% |
| Mage | Hits every enemy, slower (1.25 s), deals 80% |
| Support | Heals the most hurt ally below 80% HP; team +10% damage |

**Bosses** (free, unlimited tries):

| Tier | Boss | Recommended | Every win | First win also |
|---|---|---|---|---|
| 1 | Vein Warden | 80 | 1 Foreman Sigil | 20 Basic + 10 Advanced Shards, **200 Gold** |
| 2 | The Rooted Elder | 135 | 1 Golem Heart | 30 Basic + 10 Advanced Shards |
| 3 | Crystal Burrower | 240 | 1 Wyrm Scale | 40 Basic + 10 Advanced Shards |

Beating a tier's boss **unlocks the next mine tier** (team decision 17) and that tier's Expedition.

**Expeditions** (one runs at a time; the Adventure team is busy while it runs):

| Tier | Expedition | Recommended | First clear (once) | Automated, per hour (never fails, no cap) |
|---|---|---|---|---|
| 1 | Spore Caverns | 85 (was 100 before SCRUM-36) | 500 Gold, 10 Basic Shards, 5 Spore Silk | 2 Spore Silk |
| 2 | Magma Works | 175 | 1,200 Gold, 15 Basic Shards, 5 Ember Core | 2 Ember Core + 1 Spore Silk |
| 3 | Crystal Depths | 300 | 2,500 Gold, 20 Basic Shards, 5 Prism Dust | 2 Prism Dust + 1 Ember Core |

Automated loot reaches the wallet when the Expedition is **stopped**.

---

## 7. Summoning

- **10 shards** a summon; the player picks the **Miner or Adventure pool** and the shard.
- **Basic:** Common 75% · Uncommon 23% · Rare 2%. **Advanced:** Uncommon 85% · Rare 15%. If a pool has no
  Minion of a rarity, that rarity is left out and the others keep their proportions. No pity.
- One of each Minion: a **duplicate** gives Minion Essence: Common 5, Uncommon 15, Rare 50.
- Prototype roster: 20 Minions (7 Miners, 13 Adventurers): every class as Common and Uncommon, Rare Tank,
  Archer and Mage, and Gold/Ore/Speed Miners across the rarities.

---

## 8. Where the numbers are

| What | Where in Unity |
|---|---|
| Mining rates, traits | GameDatabase → Mining |
| Slots | GameDatabase → Roster |
| Combat rules, classes | GameDatabase → Combat |
| Bosses, Expeditions, tier unlocks | GameDatabase → Bosses / Expeditions / Tier Requirements |
| Minion level and breakthrough costs | GameDatabase → Minion Progression |
| Gear bonuses, gear costs, Smith ranks | GameDatabase → Equipment |
| Summon costs, rates, Essence | GameDatabase → Summoning |
| Starting resources, starter Minions | GameDatabase → New Game |
| Minion base stats, rarity, trait | Each asset in `Assets/Content/Minions` |

---

## 9. Pacing with these numbers

`PlaythroughTests` plays the whole prototype as a free player with the defaults above. It runs check-ins every
1 h, every 8 h and once a day, with 5 seeds each, and prints the hour each step was first cleared. It plays
perfectly, so real players are slower. Results on 2026-11-26 (after the SCRUM-36 Spore Caverns fix):

| Check-in | T1 boss | T1 Exped. | T2 boss | T2 Exped. | T3 boss | **T3 Exped. (end)** |
|---|---|---|---|---|---|---|
| Every 1 h | 0 | 1 | 2–3 | 3–5 | 4–6 | **7–11 h** |
| Every 8 h | 0 | 8 | 8 | 8 | 16 | **16–32 h** |
| Once a day | 0 | 24 | 24 | 24 | 24–48 | **48–72 h** |

**The Spore Caverns wall (SCRUM-36).** In the SCRUM-8 playtest every tester who got that far stalled at the Tier 1
Expedition's first clear (power ~66–90 vs 100); one quit there. A scratch simulation of a real first session
(spend the starting resources on the team and Miners, collect every minute) showed why: spending well gets the
team to ~86 power, and the first clear was only won at ~97. With **85 recommended** and **400 Gold + 150 Iron**
it's won about 1 minute after the Tier 1 boss; the first attempt at 66 power still loses, so the "lose, upgrade,
win" moment stays. The rest of the pacing barely moved (table above).

`PlaythroughTests` also has a **player who only follows the next goal** (SCRUM-36): it does exactly what the
NEXT GOAL card says, never levels Miners, and stops a visit after a lost fight. It must clear all 3 tiers; with
hourly check-ins it finishes in 12–25 h, with 8-hourly check-ins in 24–192 h.

**Before → after the SCRUM-32 playtest fixes** (starting resources, Tier 1 first-win Gold; rerun 2026-11-25):

| Measure | Every 1 h | Every 8 h | Once a day |
|---|---|---|---|
| Finished at | 10–15 h → **7–14 h** | 24–32 h → **24–32 h** | 48 h → **48–72 h** |
| Visits with nothing to buy | 1–3 → **0–2** | 1 → **0** | 1 → **0** |
| Lost fights | 2–6 → **1–7** | 0–1 → **1–2** | 0 → **1** |
| Minion breakthroughs | 0 → **0** | 0 or 7 → **1–3** | 0 → **3–8** |

The first launch now always has something to buy, every rhythm loses at least one fight, and Minion
breakthroughs appear for 8-hourly and daily players. A phone test of 0.0.4 with hourly debug skips finished
at ~11 h of game time, inside the simulated 7–14 h, so the simulation is a fair predictor.

**What the economy pass (SCRUM-2) needs to fix, after the playtest:**

1. The whole prototype lasts only 2–4 return visits (an hourly player: 7–14 h; a daily player: 2–3 days).
2. Being away beats playing: uncapped mining makes long breaks earn more than active play can spend.
3. Fights are rarely lost unless the player checks in very often.
4. Minion breakthroughs are barely needed by active players: gear upgrades carry the team.
5. About half of summons are duplicates, and Essence has no use yet.

Decide first: **how long each tier should take** for a once-a-day player. Then tune costs, recommended power and
Expedition rates, rerun `PlaythroughTests`, and repeat.

---

## History

| Version | Date | Changes |
|---|---|---|
| 1.0 | 2026-11-11 | First pass for the v1.1 design, with a Python simulator. |
| 2.0 | 2026-11-24 | Rewritten to describe the numbers in the current build (v3.4 design, SCRUM-5 to SCRUM-32). The v1.1 simulator and its report are replaced by `PlaythroughTests`. |
| 2.1 | 2026-11-26 | SCRUM-36: Spore Caverns 100 → 85, starting resources 400 Gold + 150 Iron, pacing rerun, next-goal follower. |
| 2.2 | 2026-11-26 | SCRUM-37: bosses renamed (Vein Warden, The Rooted Elder, Crystal Burrower); ids, materials and numbers unchanged. |
