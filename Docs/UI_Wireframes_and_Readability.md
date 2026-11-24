# Expedition: Deepshaft — UI Wireframes & Mobile Readability

**Jira:** SCRUM-3 (rewritten in SCRUM-33) · **Status:** Describes the current grey-box build (0.0.3 + SCRUM-32/33), 2026-11-24
**Design source:** [Game Design Summary v3.4](Game_Design_Summary.md) · **Build plan:** [MVP Scope](MVP_Scope_and_Core_Loop.md)
**Code:** `Assets/Scripts/Game/GreyboxGameScreen.cs` · **Next:** the real UI and design system (SCRUM-12), art (SCRUM-13)

This doc shows the screens **as they are built now**: the grey-box game screen used for the core loop playtest
(SCRUM-8). It's drawn with IMGUI and generated textures, styled after the team's UI mock-up (dark background,
bronze-framed cards, gold buttons). Portraits, boss art and tier names are placeholders.

The readability rules in §1 are the target for the real UI (SCRUM-12). Where the grey-box doesn't meet them yet,
§1.5 says so.

---

## 1. Readability rules

### 1.1 Units

The grey-box screen scales everything to a **1080-unit shortest side**, so 1 unit is about 1 pixel on a 1080p
phone in either orientation, and **130 units ≈ 48 dp**. Content sits in one centred column, at most 1080 units
wide, inside the device safe area.

### 1.2 Touch targets

| Rule | Value | Why |
|---|---|---|
| Minimum tappable size | **130 × 130** (≈ 48 dp) | Android accessibility guideline |
| Primary action buttons | ≥ 130 tall, 300–440 wide | Reachable with a thumb |
| Gap between tappable items | ≥ 18–24 | Stops mis-taps between neighbours |
| Costly or destructive actions | Confirm, or make them free to retry | No accidental loss (Reset game needs a second tap) |

### 1.3 Text and colour

- Contrast at least **4.5 : 1** for body text and **3 : 1** for large text and icons. The palette passes
  (text `#EDE6D8`, muted `#A59C8C` on `#1A1620` cards and `#0E0C12` background).
- Never show information **only** with colour: rarity also has its name, locked things say what unlocks them,
  red numbers also have a "▲" or "short by" text.
- Big numbers are shortened in the resource bar: `1,250` → `1.2K`, `3,400,000` → `3.4M`. Elsewhere they use
  thousands separators (`12,241`).

### 1.4 Safe area and orientation

- Everything sits inside `Screen.safeArea` (notches, cut-outs, rounded corners). The background fills the screen.
- The same layout works in all four orientations: in landscape the column stays centred and the content scrolls.
- Rotating keeps the tab, the selected Minion and the scroll position.

### 1.5 Where the grey-box falls short (for SCRUM-12)

| Rule | Grey-box now | Target |
|---|---|---|
| Body text | 32 units (≈ 12 sp) | 40 (≈ 15 sp) |
| Secondary text | 26 units | 32, never for key information |
| Landscape | One centred column | Two columns or a side rail (wireframe idea from v1.1) |
| Icons | Coloured circles and diamonds with letters | Real icons (SCRUM-12, SCRUM-13) |

---

## 2. Navigation

```
 App start ───────▶ ┌──────────────────────────────────────────────┐  no Main menu: boots straight in (SCRUM-33)
                     │ Resource bar                    [≡ Settings] │  tap the bar → Resources panel
                     │ TIER 2 · IRON SEAMS        (Next: beat …)    │  header with the next goal
                     │                                              │
                     │            content of the tab                │  scrolls
                     │                                              │
                     │  HQ   Mine   Exped.  Minions  Smith   Boss   │  tab bar (red dot = something to do)
                     └──────────────────────────────────────────────┘
```

- **Six tabs**, always one tap away: HQ, Mine, Exped., Minions, Smith, Boss.
- **Red dots** on tabs: Mine has something to collect · an Expedition can be first-cleared or automated ·
  the current tier's boss isn't beaten · there's a free team slot and a benched Adventurer.
- **Header:** the current mine tier and a pill with the next goal ("Next: beat Slag Golem").
- **Messages** (results, errors) appear as a toast at the top of the content; tap to close.
- **Overlays:** the resource bar opens the Resources panel; the ≡ button opens Settings. Tapping outside closes them.

---

## 3. Shared parts

### 3.1 Resource bar

```
┌──────────────────────────────────────────────────────────────┐
│ ● 12.4K  ● 5.3K  ● 2.9K  ● 0  ◆ 30  ◆ 10                 [≡] │
│  Gold     Iron    Silver  Mithril  Basic  Advanced shards    │
└──────────────────────────────────────────────────────────────┘
```

Tap it for the **Resources panel**: every resource by group (Mine, Shards, Expedition materials, Boss materials,
Other). Each rare material has a line under it saying where it comes from, following the player's progress
(SCRUM-33). For example: "From: beat Spore Foreman to open Spore Caverns, clear it once, then automate it", or
"From: automate Spore Caverns (collected when you stop it)". There's deliberately no "earned so far" for rare
materials (team decision 2026-11-24).

### 3.2 Card

Every block is a bronze-framed card: a title on the left, a status pill on the right (green = running/ok,
purple = busy/automated, red = problem, gold = Smith), the body, and the main action as a gold button
bottom right.

### 3.3 Minion card (grid of 3 per row)

```
┌──────────────┐
│ ▬▬▬▬▬▬▬▬▬▬▬▬ │  gold bar = selected
│ ┌──────────┐ │
│ │    T     │ │  portrait placeholder: class colour + initial
│ └──────────┘ │
│ Common Tank  │
│ Tank · Lv 12/20
│ ◆ ◇ ◇        │  breakthroughs done (of 3)
│ Team         │  Team / Bench (Adventurers), Mining / Resting (Miners), Busy (on an Expedition)
└──────────────┘
```

### 3.4 "Where to get it"

Whenever something can't be afforded, the screen says **"Need 50 Iron more"** in red, followed by where each
missing resource comes from:

- **Mine resources:** "Iron: Mine", or "Silver: Mine from Silver Veins" if the tier isn't reached yet.
- **Rare Expedition materials:** the same progress-aware hint as §3.1.
- **Boss materials:** "beat Slag Golem", "beat Spore Foreman again (every win gives one)", or "reach Tier 3 and beat Prism Wyrm".

---

## 4. Screens

### 4.1 HQ (home)

```
┌ LOST TO SLAG GOLEM ─────────────── Free retries ┐   only after a lost fight (SCRUM-32)
│ Your team has 120 power; Slag Golem recommends  │
│ 135. You need about 15 more.                    │
│ You can afford upgrades right now.              │
│ [Level up] [Upgrade gear]               [Close] │
└─────────────────────────────────────────────────┘
┌ SPORE CAVERNS CLEARED ────────────── Automate it ┐  only after a won first clear (SCRUM-32)
│ Automate it now: it earns 2 Spore Silk per hour, │
│ never fails and keeps going while locked.        │
│ [Start automation]                   [Not now]   │
└──────────────────────────────────────────────────┘
┌ HQ · PRODUCTION ─────────────────────── Running ┐
│ Per hour, in total   +440 ● +180 ● +2 ◆         │
│ Mine: 440 Gold · 180 Iron                       │
│ Expedition Spore Caverns: 2 Spore Silk (collect │
│ when you stop it)                               │
│ Waiting in the Mine  +312 ● +128 ●              │
│                                      [Collect]  │
└─────────────────────────────────────────────────┘
┌ EXPEDITION ───────────────── Automated / Idle ──┐   running: [Stop & Collect]; idle: next step + [Go]
┌ SMITH ──────────────────────────── Rank 1/3 ────┐   [Go]
┌ BOSS · SLAG GOLEM ──────────────────────────────┐   team vs. recommended, [Go]
```

### 4.2 Mine

- **MINE** card: what's waiting to collect, the rates per hour ("no limit": there's no stockpile cap), **Collect**.
- **MINERS (2/3)**: the Minion grid of Miners. Tapping one selects it; its detail card (level up, Mine button) is on the Minions tab.
- **NEXT TIER**: what unlocks the next tier ("Iron Seams: beat Spore Foreman"), or "the deepest tier in the prototype".

### 4.3 Exped.

```
┌ LOST TO … / … CLEARED ┐  the SCRUM-32 cards, as on HQ
┌ EXPEDITION (running) ─────────────── Automated ┐
│ Spore Caverns · running for 3 h 10 min         │
│ Farms 2 Spore Silk per hour. Collected when    │
│ you stop it.                                   │
│ Never fails · no time limit · keeps running    │
│                              [Stop & Collect]  │
└────────────────────────────────────────────────┘
┌ ADVENTURE TEAM ─────────────────── Ready/Busy ─┐  3 slots; "BUSY" while an Expedition runs
┌ EXPEDITIONS ────────────────────── Power 120 ──┐
│ ● Spore Caverns · cleared · 2 Spore Silk/h  [Automate]
│ ● Magma Works · first clear needed · Rec. 175 [First clear]
│ ● Crystal Depths · beat Slag Golem             │
└────────────────────────────────────────────────┘
```

The running card shows the rate, not a running total: there's no "earned so far" for rare materials (team
decision 2026-11-24). The rewards reach the wallet when the Expedition is stopped.

- Only **one** Expedition runs at a time, and the Adventure team is **busy** while it runs: no team changes,
  no boss fights or first clears until it's stopped.
- A **first clear** is a real autobattle that can fail. Once cleared, an Expedition can be **automated** forever:
  it never fails, has no time limit and keeps running while the app is closed.

### 4.4 Minions

```
┌ ADVENTURE (5) ──────────────────── [Miners (2)] ┐  toggle Adventure / Miners
│ [card] [card] [card]                            │
│ [card] [card]                                   │
│ Tap a Minion to level it up or put it in the team (3 slots).
└─────────────────────────────────────────────────┘
┌ COMMON TANK ──────────────── Common · Tank ─────┐  detail card of the selected Minion
│ Lv 12 / 20 · breakthroughs 0/3                  │
│ ATK 21 → 22   HP 210 → 220                      │  now → next level
│ Power 42 → 44                                   │
│ Level Up: 82 Gold, 33 Iron   [Max ×4] [Level Up]│  Max = as many as affordable (SCRUM-32)
│ Max buys 4 levels for 380 Gold, 152 Iron.       │
│ Breakthrough 1: 1,000 Gold, 400 Iron, 5 Spore Silk     [Break]
│ Need 5 Spore Silk more                          │  red, then where to get it (§3.4)
│ Reach level 20 first. Raises the cap to 30.     │
│ GEAR                                            │
│ Weapon Lv 1 · +0% ATK   Armor Lv 1 · +0% HP     │  (Miners: Pickaxe · +mining output)
│ [Upgrade gear at the Smith]                     │
│ [Bench] / [Add to team] / [Put in Mine] / [Take out of Mine]
└─────────────────────────────────────────────────┘
┌ SUMMON ─────────────────────── ◆ 30   ◆ 10 ─────┐
│ [Miner] [Adventure]   [Basic] [Advanced]        │  pool follows the toggle above
│ Common 75% · Uncommon 23% · Rare 2%             │  the real chances for this pool
│ You own one of each Minion: duplicates become   │
│ Minion Essence (5 / 15 / 50 by rarity). No pity.│
│ Need 10 Basic Shards more                       │
│ Basic Shards: boss wins and Expedition first clears.
│                              [Summon x1 · 10]   │
└─────────────────────────────────────────────────┘
```

- Levels cost Gold + Iron. At the level cap (20, then 30, 40, 50) a **breakthrough** is needed: Spore Silk,
  then Ember Core, then Prism Dust, plus Gold and Iron.
- A summon result shows "NEW! Rare Mage joined the bench" and selects the new Minion, or
  "Common Tank again: +5 Minion Essence".

### 4.5 Smith

```
┌ SMITH ──────────────────────────── Rank 1/3 ────┐
│ Golem Tempering · opens gear breakthrough 2     │
│ Costs 1 Golem Heart (once)                      │
│ Need 1 Golem Heart more · Golem Heart: beat Slag Golem
│                                        [Learn]  │
└─────────────────────────────────────────────────┘
┌ WEAPONS & ARMOR ──────────────── [Miner tools] ─┐  toggle
│ Common Tank · Team                              │
│  Weapon Lv 4/4 · +24% ATK                       │
│  Breakthrough: 300 Gold, 150 Iron, 3 Spore Silk        [Break]
│  Armor Lv 2/4 · +8% → +16% HP                   │
│  Upgrade: 50 Gold, 25 Iron            [Max ×2] [Upgrade]
│ Common Fighter · Team …                         │
└─────────────────────────────────────────────────┘
```

- Every Minion has its own gear: a Pickaxe for Miners, a Weapon and Armor for Adventurers.
- Gear levels cost Gold + Iron (Silver from level 9). At the cap (4, then 8, 12, 16) a gear breakthrough costs
  Gold + Iron + that tier's Expedition material, **and** the Smith must know the matching rank.
- Smith ranks are learned once each with a boss material: Foreman Sigil, Golem Heart, Wyrm Scale.

### 4.6 Boss

```
┌ LOST TO … ┐  the SCRUM-32 card, after a loss
┌─────────────────────────────────────────────────┐
│              [ Slag Golem art ]                 │  placeholder (SCRUM-13)
└─────────────────────────────────────────────────┘
┌ BOSS · SLAG GOLEM ─────── Free · unlimited tries ┐
│ Team 120 / Rec. 135                              │
│ Win: unique boss material + new Expeditions      │
│ Ready                          [Fight]           │  "Stop Exp. & Fight" while the team is busy
└──────────────────────────────────────────────────┘
┌ YOUR TEAM ──────────────────────── Ready ───────┐  3 slots, power
┌ Recommended 135 · Your team 120 ▲ · Unlocks … ──┐
┌ OTHER BOSSES ───────────────────────────────────┐  earlier bosses: [Fight again] for more boss material
```

### 4.7 Settings

- **Sound:** opens the volume and mute screen; its Back button returns to the game.
- **Debug (playtest only):** skip the clock +1 h / +8 h / +24 h; **Copy log** / **Save log file** for the
  playtest log (SCRUM-8); **Reset game** (asks twice, and deletes the save and its log).

---

## 5. Device check (each new build)

- [ ] Portrait and landscape on a small phone (≈ 5.5") and a 20:9 phone: nothing under a notch, no text cut off
- [ ] Every tab scrolls to its last card; buttons near the bottom aren't hidden by the tab bar
- [ ] Rotate mid-session: same tab, same selected Minion
- [ ] Tap targets: no mis-taps on the Minion grid, the Max / Upgrade pair or the tab bar
- [ ] Copy log works and pastes into a message (Android and iPhone)

## 6. Next (SCRUM-12)

Real UI with Unity UI or UI Toolkit, a design system (type scale meeting §1, icons, colours), two-column
landscape, animations and real art. The grey-box screen stays until then.
