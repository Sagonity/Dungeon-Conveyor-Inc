# Dungeon Conveyor Inc. — MVP Scope & Core Loop

**Jira:** SCRUM-1 · **Sprint:** Discovery · **Status:** Draft for team review
**Platform:** Android first (iOS later), portrait, 1080×1920 reference · **Engine:** Unity 6 (6000.6)

> All numbers in this doc are starting points so the prototype can be built.
> Final tuning belongs to SCRUM-2 (economy) and SCRUM-19 (balancing).

---

## 1. Pitch

You run a logistics and mining corporation in an underground fantasy world.
You recruit magical helpers and monsters ("Minions") through a gacha, give them
tools or weapons, and send them on auto-run **Expeditions** to clear floors,
mine resources and fight bosses. Everything they mine rides a **conveyor belt**
back up to your HQ, where you turn it into upgrades.

## 2. Design pillars

Use these to settle scope arguments. If a feature doesn't support at least one, cut it.

1. **The belt is the reward.** Seeing loot ride the conveyor home is the main
   payoff moment, and the one visual that makes this game ours. It always runs on screen.
2. **Plan, then watch.** The player's skill is in squad building and gear
   choices *before* a run. During a run the game plays itself; the player can
   speed it up or retreat, nothing more.
3. **Short sessions, steady progress.** One expedition is 1–3 minutes. Every
   session ends with something to spend.
4. **Readable on a phone.** One-thumb portrait play, big numbers, clear icons (SCRUM-3, SCRUM-12).

## 3. Core loop

```
        ┌──────────────┐
        │  1. RECRUIT  │  Spend Gems / tickets on summons
        └──────┬───────┘
               ▼
        ┌──────────────┐
        │  2. EQUIP &  │  Give each minion a Tool or Weapon,
        │   FORMATION  │  place them on the 3×3 squad grid
        └──────┬───────┘
               ▼
        ┌──────────────┐
        │ 3. EXPEDITION│  Auto-run floors: fight monsters,
        │   (auto-run) │  mine ore nodes, boss every 5th floor
        └──────┬───────┘
               ▼
        ┌──────────────┐
        │ 4. CONVEYOR  │  Loot rides the belt back to HQ
        │   DELIVERY   │  (belt speed = Hauler power + belt level)
        └──────┬───────┘
               ▼
        ┌──────────────┐
        │ 5. INVEST    │  Level minions, forge gear,
        │   AT HQ      │  upgrade the belt
        └──────┬───────┘
               └──────► back to 2, reaching deeper floors
```

### Loop timings

| Loop | Length | What the player does | What they get |
|---|---|---|---|
| **Moment** | 5–15 s | Watch a floor: a fight or a mining node | Ore and gold land on the belt |
| **Session** | 3–8 min | 1–3 expeditions, then spend at HQ | A level-up, new gear or a belt upgrade |
| **Day** | 2–4 sessions | Collect the HQ belt stockpile, do daily summons | Gems for summons, a new floor record |
| **Week** (post-MVP) | — | Events, bosses, quests | Limited minions (SCRUM-16 and later) |

### Expedition flow

1. Pick a biome and a start floor (any floor already cleared, or the next one).
2. The squad auto-advances floor by floor. Each floor is a **fight** (enemy
   wave), a **mine** (ore nodes, faster with Tools and Miners) or, every 5th floor, a **boss**.
3. The run ends when the squad wipes, the player taps **Retreat**, or the
   biome's last floor is cleared.
4. **Nothing is lost on a wipe.** Everything already on the belt is kept. This
   keeps failure cheap and pushes the player to try deeper floors.

### Conveyor (the "logistics" layer)

- Loot from an expedition is placed on the belt and moves up to HQ visibly.
- **Belt level** sets throughput (items per second) and **HQ stockpile cap**.
- Between expeditions the belt keeps bringing up a small passive trickle of ore
  from cleared floors, up to the cap. This is the MVP version of idle income.
  Real offline rewards come in SCRUM-16.
- Haulers in the squad add belt speed. This is why a non-fighter is worth a slot.

## 4. MVP systems

**MVP = the vertical slice (SCRUM-14).** It is built in two steps:

- **Step A: core loop playtest (SCRUM-8).** Grey-box. Proves that "plan → watch → collect → upgrade" is fun.
- **Step B: vertical slice (SCRUM-14).** The same loop with real art, gacha, HQ and equipment.

| System | Step A: playtest | Step B: vertical slice | Ticket |
|---|---|---|---|
| Squad grid (3×3, drag to place) | ✅ (already in the build) | ✅ polished | SCRUM-6 |
| Auto-run expedition + combat | ✅ 1 biome, 10 floors, 1 boss | ✅ 3 biomes × 10 floors, 3 bosses | SCRUM-7 |
| Mining nodes + conveyor delivery | ✅ grey-box belt | ✅ animated belt, upgrade levels | SCRUM-5 |
| Minion roles & stats | ✅ 3 roles, 4 grey-box minions | ✅ 8 minions | SCRUM-10, SCRUM-13 |
| Equipment (Tool / Weapon) | ⚪ 1 slot, hard-coded items | ✅ 2 slots, 3 tiers, forge at HQ | SCRUM-10 |
| HQ upgrades | ⚪ belt level only | ✅ belt, forge, barracks (minion level cap) | SCRUM-9 |
| Gacha / recruitment | ⚪ "Summon" button (already in the build) | ✅ banner, rarities, pity, duplicates | SCRUM-11 |
| Currencies | Gold, Ore | Gold, Ore, Gems | SCRUM-2 |
| Save / load | ⚪ local PlayerPrefs/JSON | ✅ local save | SCRUM-17 |
| UI | Grey-box HUD (already in the build) | Design system, readable HUD | SCRUM-3, SCRUM-12 |

✅ = in scope · ⚪ = minimal / stubbed

### 4.1 Minions

Each minion has:

| Field | Purpose | Exists in code? |
|---|---|---|
| `minionName` | Display name | ✅ `MinionStats` |
| `maxHP` | Survives fights | ✅ `MinionStats` |
| `damage` (ATK) | Fight speed | ✅ `MinionStats` |
| `role` | Fighter / Miner / Hauler | ❌ add |
| `rarity` | Common / Rare / Epic | ❌ add |
| `miningPower` | Ore node clear speed | ❌ add |
| `haulPower` | Belt speed bonus | ❌ add |
| `level`, `xp` | Progression | ❌ add |
| Equipment slots | 1 Tool + 1 Weapon | ❌ add |

**Formation rule:** the front row takes hits first. Fighters belong in front,
Miners and Haulers further back. That gives the 3×3 grid a real decision.

**MVP roster (8):** 3 Common, 3 Rare, 2 Epic, spread so each role has at least
one minion in every rarity. Names and art come from SCRUM-13.

### 4.2 Equipment

- 2 slots: **Tool** (boosts mining or hauling) and **Weapon** (boosts ATK).
- 3 tiers (Copper / Iron / Mithril), forged at HQ from Ore + Gold, and
  sometimes dropped by bosses.
- MVP has about 6 items (one Tool line and one Weapon line × 3 tiers). No random stats in MVP.

### 4.3 Gacha (MVP version)

- One standard banner. Costs Gems (earned in game only for MVP).
- Starting rates: Common 75% / Rare 22% / Epic 3%. **Pity:** guaranteed Epic within 50 pulls.
- Duplicates become **shards** that raise a minion's level cap.
- Drop rates are shown in the game from day one (store compliance, SCRUM-25).

### 4.4 Currencies

| Currency | Source | Sink |
|---|---|---|
| **Gold** (soft) | Fights, the belt | Minion level-ups, forging |
| **Ore** (material) | Mining nodes, passive belt | Forging, HQ upgrades |
| **Gems** (summon) | First-time floor clears, bosses, daily login | Summons |

### 4.5 Biomes (vertical slice)

| # | Biome | Floors | Boss | Mechanic it teaches |
|---|---|---|---|---|
| 1 | Fungal Mines | 1–10 | Spore Foreman | The basics: fight + mine |
| 2 | Magma Works | 11–20 | Slag Golem | High damage: needs tanky front row |
| 3 | Crystal Depths | 21–30 | Prism Wyrm | Rich ore: rewards Miners and Haulers |

Biome names are placeholders until SCRUM-13.

## 5. Out of scope for MVP

Listed so nobody builds them early:

- Real-money purchases / IAP (Unity Purchasing is in the project but stays unused until SCRUM-24)
- Ads
- Offline rewards beyond the belt stockpile cap (SCRUM-16)
- Quests, achievements, battle pass (SCRUM-16)
- Multiple banners, limited-time events, live ops (SCRUM-27)
- Cloud save, accounts, analytics (SCRUM-17)
- PvP, guilds, any social features
- Manual skill activation during combat
- Random gear stats, gear sets
- Localization (SCRUM-25)

## 6. Where the current build stands

The current build (v0.0.1 + main menu PR) already has:

- `GridManager`: 3×3 grid with snapping. **Keep it** as the squad formation board.
- `DraggableMinion`: drag to move. Dropping onto another minion currently
  *damages* it. **Change:** in the formation screen, dropping on a minion should
  **swap** them. Combat moves to the expedition scene.
- `MinionStats` / `MinionHealth`: base for minion data. Move stats to a
  `ScriptableObject` (`MinionDefinition`) so the 8 minions are data, not prefabs.
  How this is done is decided in SCRUM-4.
- `GameController.SummonMinion()` + HUD Summon button: the starting point for SCRUM-11.
- Main menu, settings (volume / mute) and the UI state machine are done.

**Missing for Step A:** expedition scene, floor generation, auto-combat tick,
ore nodes, the belt, Gold/Ore counters, and a "return to HQ" results screen.

## 7. Success criteria

### Step A: core loop playtest (SCRUM-8)

Run with 5+ testers who haven't seen the game:

- [ ] Testers can explain the loop back without help after 10 minutes
- [ ] At least 3 of 5 change their formation or gear on their own to beat a floor
- [ ] Median first session is 10 minutes or longer
- [ ] At least 3 of 5 say the belt delivery feels good (we ask about it directly)
- [ ] No tester gets stuck with nothing to spend for more than 2 expeditions in a row

If the belt moment doesn't land, revisit pillar 1 before starting Step B.

### Step B: vertical slice (SCRUM-14)

- [ ] All 3 biomes and 3 bosses can be cleared with a reachable roster
- [ ] Full loop including gacha, equip and HQ upgrade, with real art for the 8 minions
- [ ] Holds 60 fps on a mid-range Android device (SCRUM-20)
- [ ] Save survives closing and reopening the app

## 8. Open questions

These need a decision from the team. Proposed answers are in *italics*.

1. **Portrait only, or both orientations?** The project allows auto-rotate to
   all four. *Proposal: lock to portrait. Simpler UI, one-hand play.*
2. **Is the belt only a reward animation, or something the player builds and lays out?**
   *Proposal: MVP is a fixed belt with upgrade levels. A belt the player builds
   is a possible post-launch feature.*
3. **Monetization model at launch:** gacha IAP only, ads too, or a premium price?
   *Proposal: decide in SCRUM-2. Keep IAP out of the MVP.*
4. **Minion art style and theme:** cute, gritty or comedic-corporate?
   *Proposal: comedic-corporate (minions as "employees", HR jokes).*
   This affects SCRUM-13.
5. **Language of code comments:** the code is a mix of Norwegian and English.
   *Proposal: English from now on.*
