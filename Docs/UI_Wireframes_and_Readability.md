# Expedition: Deepshaft — UI Wireframes & Mobile Readability

**Jira:** SCRUM-3 · **Status:** Ready for review · **Scope:** Step A (core loop playtest, SCRUM-8)
**Depends on:** [MVP Scope & Core Loop](MVP_Scope_and_Core_Loop.md) (SCRUM-1)
**Feeds:** SCRUM-9 (HQ), SCRUM-5/6/7/10/11 (screens), SCRUM-12 (visual design system)

These are **grey-box** wireframes: layout, hierarchy and flow only. Colors, fonts,
icons and art come later in SCRUM-12 and SCRUM-13. Every screen is drawn in
**portrait** and **landscape**, because the game supports all four rotations.

> ⚠️ **Needs an update for Game Design Summary v3.4 (2026-11-12).** The readability
> rules (§1), navigation pattern, safe area and device checklist still apply. These
> wireframes were drawn for the v1.1 design; change them as follows (SCRUM-3, SCRUM-12):
>
> | Area | Change |
> |---|---|
> | Resource bar (§3.1) | Gold, Iron, Silver (from Tier 3), shards (Basic/Advanced), rare materials on a tooltip. Remove Minerals, Upgrade Materials and Guardian Tickets (tickets are scrapped) |
> | Tabs (§2) | Add a **Smith** screen (upgrades, breakthroughs). HQ may merge into it (SCRUM-9) |
> | Mine (§4.3) | No mine choice: Miners auto-mine the highest tier. Show every resource the tier produces per hour. **No stockpile cap**: show the amount waiting to collect, no "full" state, no stockpile upgrade |
> | Expedition (§4.4) | Two states: **first clear** (pick team, autobattle, can fail, bonus rewards) and **automated** (running, earned so far, Stop & Collect). Only one active. No timers. While it runs, the Adventure team shows "Busy" and Boss / first clear offer "Stop Expedition & Collect" to free the team |
> | Minion card (§3.3) | Replace star rank with breakthroughs; show class (Tank/Fighter/Archer/Mage/Support) or Miner trait (Gold/ore/speed). Squad size is undecided |
> | Summon (§4.6) | Choose **Miner or Adventure pool** and **Basic or Advanced shard**; rates Common/Uncommon/Rare; no pity counter; show Essence gained from duplicates |
> | Boss (§4.7) | Win screen shows the unique boss material and the Expeditions it unlocked. **No ticket confirm or ticket counter**: attempts are free and unlimited |

---

## 1. Readability rules

All sizes are in **canvas units** at the reference resolution the project already
uses (`CanvasScaler`: 1080 × 1920, match width/height 0.5). With that setting,
1 unit is about 1 physical pixel on a typical 1080p phone in either orientation,
and **130 units ≈ 48 dp**.

### 1.1 Touch targets

| Rule | Value | Why |
|---|---|---|
| Minimum tappable size | **130 × 130** (≈ 48 dp) | Android accessibility guideline |
| Primary action buttons | **160–170** tall, ≥ 480 wide | One per screen, reachable with a thumb |
| Gap between tappable items | ≥ **24** | Stops mis-taps between neighbors |
| Tappable area of toggles/sliders | The **whole row**, not only the box/knob | Small boxes are hard to hit |
| Destructive or costly actions | Always confirm (e.g. "Spend 1 Guardian Ticket?") | No accidental spending |

### 1.2 Text

| Use | Min size | Current builder value |
|---|---|---|
| Screen title | 96 | 96–130 ✅ |
| Button label | 52 | 56 ✅ |
| Body text, list rows | **40** (≈ 15 sp) | 40–52 ✅ |
| Numbers in the resource bar | 44, bold | — (new) |
| Secondary labels (e.g. "per hour") | 32 (≈ 12 sp), never for key info | — |

- Use **auto-size** on any text that can grow (names, translated text later), with a
  floor no lower than the minimums above.
- Contrast at least **4.5 : 1** for body text, **3 : 1** for large text and icons.
  The current palette (off-white `#F2EDE0` on near-black `#120F17`) passes.
- Never show information **only** with color. Rarity, for example, gets a color *and*
  a letter (C / R / E). Locked items get a lock icon *and* grey-out.
- Big numbers are shortened: `1,250` → `1.2K`, `3,400,000` → `3.4M`. Timers use
  `9:41` under an hour and `1h 05m` above it.

### 1.3 Safe area and orientation

- Every screen puts its content inside a **SafeArea** child (`SafeAreaFitter`), so
  nothing sits under a notch, camera cutout or rounded corner. Backgrounds still
  fill the whole screen.
- Layout switches on **aspect ratio**, not on device type:
  - **Portrait** (height > width): navigation as a **bottom tab bar**, content in one column.
  - **Landscape** (width ≥ height): navigation as a **left side rail**, content in two columns.
- Rotating never resets the screen: the same screen, scroll position, open dialog
  and selected tab stay after rotation (scope doc §8, Step B).
- The **resource bar** is always at the top in both orientations.
- Design for the tightest case: a 20:9 phone in landscape gives a canvas of about
  **2150 × 966**. Anything taller than ~900 must scroll.

### 1.4 Thumb zones (portrait)

```
┌─────────────────┐
│  hard to reach  │  ← status / info only (resource bar, titles)
│                 │
│   okay reach    │  ← lists, cards, secondary buttons
│                 │
│   easy reach    │  ← primary actions (Collect, Send, Fight, Summon)
│ [ tab bar ]     │  ← navigation
└─────────────────┘
```

In landscape, primary actions go **bottom right** (right thumb) and navigation
sits on the **left rail** (left thumb).

---

## 2. Navigation map

```
                     ┌────────────┐
                     │ Main Menu  │──── Settings
                     └─────┬──────┘
                           │ Play
                           ▼
   ┌───────────────────────────────────────────────────────┐
   │        Resource bar (always visible, top)             │
   ├───────────────────────────────────────────────────────┤
   │  HQ  │  Mine  │  Expedition  │  Minions  │  Boss      │  ← tabs / rail
   └──┬───────┬──────────┬────────────┬──────────┬─────────┘
      │       │          │            │          │
      │       │          ├─ Report    ├─ Minion  ├─ Confirm ticket
      │       │          │  (modal)   │  detail  ├─ Fight (auto)
      │       │          │            │          └─ Result: Win / Lose
      │       │          │            └─ Summon ── Rates (modal)
      │       │          │                     └─ Summon result
      │       │          └─ Squad picker (shared with Boss)
      │       └─ Assign Miners
      └─ shortcuts into every tab
```

- **5 tabs**, always one tap away. Summon lives under **Minions** (it creates
  Minions) and is also a shortcut card on HQ.
- **Badges** on tabs pull the player back: Mine full, Expedition report ready,
  Guardian Ticket available, enough Shards to summon.
- The Settings button (`@`) in the resource bar opens Settings; "Back" returns to where you were.
- Android **back button**: closes the top modal, otherwise goes to HQ, otherwise
  asks "Return to main menu?".

---

## 3. Shared parts

### 3.1 Resource bar

```
Portrait
┌──────────────────────────────────────────┐
│ $ 1.2K  M 340  U 85  ✦ 12  K 2/3      @  │
└──────────────────────────────────────────┘
$ Gold · M Minerals · U Upgrade Materials · ✦ Summon Shards
K Guardian Tickets · @ Settings
```

- Tapping a resource shows a small tooltip: name, amount, **where to get more**
  ("Shards come from Expeditions").
- Tickets show `2/3` and, when not full, a countdown under it (`+1 in 2h 14m`).
- Minerals show the **current tier's** mineral. All minerals are listed in the tooltip.
- **Rare Materials** (Spore Silk, Ember Core, Prism Dust) have no slot of their own
  on the bar. They are listed in the Minerals tooltip under their own heading, with
  "Found on Expeditions". Locked tiers are hidden until the player reaches them.
- Landscape: same bar, items spread wider.

### 3.2 Tab bar (portrait) / side rail (landscape)

```
Portrait (bottom)                           Landscape (left)
┌──────────────────────────────────────┐    ┌──────┐
│  H       M•      E•      R       B•  │    │ H HQ │
│ HQ     Mine   Exped. Minions  Boss   │    │ M •  │
└──────────────────────────────────────┘    │ E •  │
                                            │ R    │
 • = badge (something to collect or do)     │ B •  │
                                            └──────┘
```

Each tab is ≥ 130 tall. Icons and short labels are both shown, because icons
alone are hard to learn.

### 3.3 Minion card (used in lists and squad slots)

```
┌────────────────┐
│  [ portrait ]  │   R = rarity letter + color border
│           R ★2 │   ★2 = star rank
│ Spore Bruiser  │   name (auto-size)
│ Lv 7  Fighter  │   level + role (Adventure) or "Miner"
│ ATK 120 HP 900 │   key stats; Miners show mining power
│ [On Exped.]    │   status tag, if busy
└────────────────┘
min size 220 × 300
```

---

## 4. Screens

Legend (single letters keep the boxes aligned; real icons come in SCRUM-12):

| In the boxes | Means |
|---|---|
| `$` `M` `U` `✦` `K` | Gold, Minerals, Upgrade Materials, Summon Shards, Guardian Tickets |
| `@` | Settings |
| `H` `M` `E` `R` `B` (tabs) | HQ, Mine, Expedition, Minions (roster), Boss |
| `F` `T` `S` (squad) | Fighter, Tank, Support |
| `[ Button ]` | Button |
| `▓▓▓░░` | Progress bar |
| `#` | Locked |
| `•` | Badge: something to collect or do |
| `!` | Warning (e.g. squad below recommended power) |
| `~` `/` (loot) | Material, equipment |

### 4.1 Main Menu (built)

```
Portrait                                   Landscape
┌──────────────────────────┐   ┌──────────────────────────────────────────┐
│                          │   │                                          │
│      EXPEDITION:         │   │        EXPEDITION: DEEPSHAFT             │
│       DEEPSHAFT          │   │                                          │
│                          │   │           [      Play      ]             │
│    [      Play      ]    │   │           [    Settings    ]             │
│    [    Settings    ]    │   │                                          │
│                          │   └──────────────────────────────────────────┘
└──────────────────────────┘
```

Already built by `Tools → Dungeon Conveyor → Build Game UI`. The title
auto-sizes (60–130), so it fits on one or two lines.

### 4.2 HQ (home) — SCRUM-9

The HQ is a dashboard: **one card per activity, each with its next action.**
A session should be possible from HQ alone (collect → resend → summon → fight).

```
Portrait
┌──────────────────────────────┐
│ $1.2K M340 U85 ✦12 K2/3    @ │
├──────────────────────────────┤
│ Tier 1 · Fungal Mines        │
│ ┌──────────────────────────┐ │
│ │ M MINE            FULL • │ │
│ │ ▓▓▓▓▓▓▓▓▓▓▓▓ 500/500     │ │
│ │ +120 Gold/h  [ Collect ] │ │
│ └──────────────────────────┘ │
│ ┌──────────────────────────┐ │
│ │ E EXPEDITION     READY • │ │
│ │ Spore Caverns, 4 Minions │ │
│ │            [Open Report] │ │
│ └──────────────────────────┘ │
│ ┌──────────────────────────┐ │
│ │ B BOSS: Spore Foreman    │ │
│ │ Team 1,850 / Rec. 2,000  │ │
│ │ K 2/3        [Challenge] │ │
│ └──────────────────────────┘ │
│ ┌──────────────────────────┐ │
│ │ ✦ SUMMON    12 Shards    │ │
│ │ ✦10 each      [ Summon ] │ │
│ └──────────────────────────┘ │
├──────────────────────────────┤
│  H     M•    E•    R     B   │
└──────────────────────────────┘
```

```
Landscape
┌────┬────────────────────────────────────────────────────────────────┐
│    │ $1.2K   M340   U85   ✦12   K2/3 (+1 in 2h 14m)               @ │
│ H  ├────────────────────────────────────────────────────────────────┤
│    │ Tier 1 · Fungal Mines                                          │
│ M• │ ┌────────────────────────────┐  ┌────────────────────────────┐ │
│    │ │ M MINE             FULL •  │  │ E EXPEDITION      READY •  │ │
│ E• │ │ ▓▓▓▓▓▓▓▓▓▓ 500/500         │  │ Spore Caverns · 4 Minions  │ │
│    │ │ +120 Gold/h   [ Collect ]  │  │             [Open Report]  │ │
│ R  │ └────────────────────────────┘  └────────────────────────────┘ │
│    │ ┌────────────────────────────┐  ┌────────────────────────────┐ │
│ B  │ │ B BOSS: Spore Foreman      │  │ ✦ SUMMON    12 Shards      │ │
│    │ │ Team 1,850 / Rec. 2,000    │  │ 1 summon = ✦10 [ Summon ]  │ │
│    │ │ K 2/3         [Challenge]  │  └────────────────────────────┘ │
│    │ └────────────────────────────┘                                 │
└────┴────────────────────────────────────────────────────────────────┘
```

- Cards are ordered by the core loop: farm (Mine, Expedition) → grow (Summon) → gate (Boss).
  In landscape the two farming cards sit side by side on the top row.
- Card states: Mine `Digging 63%` / `FULL`; Expedition `Send a squad` / `2:41 left` /
  `READY`; Boss `Challenge` / `No tickets (+1 in 2h)`.
- Tapping a card body opens its tab; the button does the action directly.

### 4.3 Mine — SCRUM-5, SCRUM-6

```
Portrait
┌──────────────────────────────┐
│ $1.2K M340 U85 ✦12 K2/3    @ │
├──────────────────────────────┤
│ FUNGAL MINES (Depth 1)       │
│ ┌──────────────────────────┐ │
│ │     [ Mine art /         │ │
│ │    grey-box diggers ]    │ │
│ └──────────────────────────┘ │
│ Stockpile                    │
│ ▓▓▓▓▓▓▓▓▓▓░░░░ 340/500       │
│ Gold +120/h · Copper +30/h   │
│ Upgrade Mat. +6/h            │
│ Full in 1h 20m               │
│                              │
│ MINERS (2/3)                 │
│ ┌──────┐ ┌──────┐ ┌──────┐   │
│ │Miner │ │Miner │ │  +   │   │
│ │Lv 4  │ │Lv 2  │ │      │   │
│ └──────┘ └──────┘ └──────┘   │
│ tap + to assign a Miner      │
│                              │
│ [Upgrade Mine]  [ Collect ]  │
├──────────────────────────────┤
│  H     M     E•    R     B   │
└──────────────────────────────┘
```

```
Landscape
┌────┬────────────────────────────────────────────────────────────────┐
│    │ $1.2K   M340   U85   ✦12   K2/3                              @ │
│ H  ├────────────────────────────────┬───────────────────────────────┤
│    │ FUNGAL MINES (Depth 1)         │ Stockpile                     │
│ M  │ ┌────────────────────────────┐ │ ▓▓▓▓▓▓▓▓▓▓░░░░ 340/500        │
│    │ │    [ Mine art /            │ │ Gold +120/h · Copper +30/h    │
│ E• │ │   grey-box diggers ]       │ │ Upgrade Mat. +6/h             │
│    │ │                            │ │ Full in 1h 20m                │
│ R  │ └────────────────────────────┘ │                               │
│    │ MINERS (2/3)                   │                               │
│ B  │ ┌──────┐ ┌──────┐ ┌──────┐     │                               │
│    │ │Lv 4  │ │Lv 2  │ │  +   │     │ [Upgrade Mine]  [ Collect ]   │
│    │ └──────┘ └──────┘ └──────┘     │                               │
└────┴────────────────────────────────┴───────────────────────────────┘
```

- There is **one Mine** that goes deeper when a tier unlocks, so there is no Mine
  switcher ([Economy](Economy_and_Progression.md) §4, decision 1).
- **Collect** is disabled (greyed, still readable) when the stockpile is empty.
- **Upgrade Mine** opens a panel with two rows: *Dig speed* and *Stockpile cap*,
  each with current → next value and cost (Gold + Minerals). Unaffordable costs are
  shown in red **and** with a "need 120 more" note.
- **Assign Miners** is a list of Miner Minions only (Adventure Minions can't mine,
  scope doc §4.3). Tap to add/remove. Step A: simple list, no drag.

### 4.4 Expedition — SCRUM-7, SCRUM-6

Three states: **pick → running → report.**

```
Portrait — pick
┌──────────────────────────────┐
│ $1.2K M340 U85 ✦12 K2/3    @ │
├──────────────────────────────┤
│ EXPEDITIONS                  │
│ ┌──────────────────────────┐ │
│ │ ● Spore Caverns  (T1)    │ │
│ │   10 min · Rec. 1,200    │ │
│ │   Shards, Spore Silk,    │ │
│ │   Common gear            │ │
│ └──────────────────────────┘ │
│ ┌──────────────────────────┐ │
│ │ # Magma Works (T2)       │ │
│ │   Beat Spore Foreman     │ │
│ └──────────────────────────┘ │
│ SQUAD            Power 1,850 │
│ ┌────┐┌────┐┌────┐┌────┐     │
│ │ F  ││ T  ││ S  ││ +  │     │
│ └────┘└────┘└────┘└────┘     │
│ Strong enough ✓              │
│ [       Send Squad       ]   │
├──────────────────────────────┤
│  H     M•    E     R     B   │
└──────────────────────────────┘
  ● selected   # locked   F/T/S = Fighter/Tank/Support
```

```
Landscape — pick
┌────┬────────────────────────────────────────────────────────────────┐
│    │ $1.2K   M340   U85   ✦12   K2/3                              @ │
│ H  ├──────────────────────────────┬─────────────────────────────────┤
│    │ EXPEDITIONS                  │ SQUAD               Power 1,850 │
│ M• │ ┌──────────────────────────┐ │ ┌─────┐┌─────┐┌─────┐┌─────┐    │
│    │ │ ● Spore Caverns (T1)     │ │ │  F  ││  T  ││  S  ││  +  │    │
│ E  │ │   10 min · Rec. 1,200    │ │ └─────┘└─────┘└─────┘└─────┘    │
│    │ └──────────────────────────┘ │ Strong enough ✓                 │
│ R  │ ┌──────────────────────────┐ │ Loot: Shards, Spore Silk,       │
│    │ │ # Magma Works (T2)       │ │       Common gear               │
│ B  │ │   Beat Spore Foreman     │ │          [   Send Squad   ]     │
│    │ └──────────────────────────┘ │                                 │
└────┴──────────────────────────────┴─────────────────────────────────┘
```

```
Running                             Report (modal, both orientations)
┌──────────────────────────────┐    ┌────────────────────────────────┐
│ Spore Caverns                │    │ EXPEDITION REPORT              │
│ ▓▓▓▓▓▓▓▓░░░░░░  6:12 left    │    │ Spore Caverns — Success ✓      │
│ Squad: F T S S               │    │                                │
│ "Your squad is exploring.    │    │ ✦ Summon Shards       +14      │
│  You can leave this screen." │    │ ~ Spore Silk          +6       │
│                              │    │ / Rusty Pick (Common)  NEW     │
│ Squad Minions are busy:      │    │                                │
│ not available for the Boss.  │    │ Squad power 1,850 vs           │
└──────────────────────────────┘    │ Spore Foreman rec. 2,000       │
                                    │ → "Almost ready for the boss"  │
                                    │                                │
                                    │ [Collect] [Collect & Resend]   │
                                    └────────────────────────────────┘
```

- **One squad out at a time** (scope §4.2). While running, Send is replaced by the timer.
- **Squad picker** (shared with Boss): Adventure Minions only, 4 slots, sorted by
  power; Minions on an Expedition are shown with a "Busy" tag and can't be picked.
- **Collect & Resend** sends the same squad to the same Expedition. It saves taps,
  but isn't auto-repeat (which is out of scope).
- A weak squad shows "Too weak: partial loot" **before** sending, and the report
  says "Returned early" — never an empty report.

### 4.5 Minions (roster) — SCRUM-10, SCRUM-11

```
Portrait                            Landscape
┌──────────────────────────────┐    ┌────┬────────────────────────────────────────────────────────────────┐
│ $1.2K M340 U85 ✦12 K2/3    @ │    │    │ $1.2K   M340   U85   ✦12   K2/3                              @ │
├──────────────────────────────┤    │ H  ├────────────────────────────────────────────────────────────────┤
│ [Adventure (5)] [Miners (3)] │    │    │ [Adventure (5)] [Miners (3)]  [✦ Summon]                       │
│ Sort: Power ▾     [✦ Summon] │    │ M  │ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐                      │
│ ┌──────┐ ┌──────┐ ┌──────┐   │    │    │ │card│ │card│ │card│ │card│ │card│ │card│                      │
│ │card  │ │card  │ │card  │   │    │ E  │ └────┘ └────┘ └────┘ └────┘ └────┘ └────┘                      │
│ └──────┘ └──────┘ └──────┘   │    │    │ ┌────┐ ┌────┐                                                  │
│ ┌──────┐ ┌──────┐            │    │ R  │ │card│ │card│                                                  │
│ │card  │ │card  │            │    │    │ └────┘ └────┘                                                  │
│ └──────┘ └──────┘            │    │ B  │                                                                │
├──────────────────────────────┤    └────┴────────────────────────────────────────────────────────────────┘
│  H     M     E     R     B   │
└──────────────────────────────┘
```

- Grid is **3 columns in portrait, 5–6 in landscape** (as many 220-wide cards as fit).
- Two tabs because the two kinds never share a job (scope §4.3).

```
Minion detail — portrait            Minion detail — landscape
┌──────────────────────────────┐    ┌────────────────────┬──────────────────────────────────┐
│ ◀ Back                       │    │ ◀ Back             │ Lv 7 / 20                        │
│ ┌──────────────────────────┐ │    │ ┌────────────────┐ │ ATK 120 → 132                    │
│ │      [ portrait ]        │ │    │ │  [ portrait ]  │ │ HP  900 → 990                    │
│ │                          │ │    │ │                │ │ [ Level Up  $200  U10 ]          │
│ └──────────────────────────┘ │    │ └────────────────┘ │ GEAR                             │
│ Spore Bruiser   R ★2         │    │ Spore Bruiser      │ [Weapon: Rusty Pick] [Armor: +]  │
│ Fighter · Adventure          │    │ R ★2 · Fighter     │                                  │
│ Lv 7 / 20                    │    └────────────────────┴──────────────────────────────────┘
│ ATK 120 → 132 (next level)   │
│ HP  900 → 990                │
│ [ Level Up  $200  U10 ]      │
│ GEAR                         │
│ [Weapon: Rusty Pick]         │
│ [Armor: +]                   │
└──────────────────────────────┘
```

- Step A: **levels only + 2 fixed items** (scope §5). Star rank is shown but can't be
  raised yet.
- Level Up shows **before → after** for each stat, so the player sees what they buy.

### 4.6 Summon — SCRUM-11

```
Portrait                            Landscape
┌──────────────────────────────┐    ┌────────────────────────────┬──────────────────────────┐
│ ◀ Back                  ✦ 12 │    │ ◀ Back                     │                    ✦ 12  │
│ ┌──────────────────────────┐ │    │ ┌────────────────────────┐ │ Common 75% · Rare 22%    │
│ │     [ portal art ]       │ │    │ │   [ portal art ]       │ │ Epic 3%      [ Rates i ] │
│ │                          │ │    │ │                        │ │                          │
│ └──────────────────────────┘ │    │ └────────────────────────┘ │ [ Summon ×1  ✦10 ]       │
│ Common 75% Rare 22% Epic 3%  │    └────────────────────────────┴──────────────────────────┘
│ [ Rates i ]                  │
│                              │
│ [   Summon ×1   ✦ 10   ]     │
│ Not enough Shards? Run an    │
│ Expedition → [ Go ]          │
└──────────────────────────────┘
```

- **Drop rates visible on the screen itself**, plus a full Rates modal (store
  compliance, SCRUM-25). Step A uses flat rates; pity counter appears in Step B.
- When Shards are short, the button is disabled and the screen **points to where
  Shards come from** (anti-frustration, SCRUM-19).
- Result: a card flips over, rarity color + letter, "NEW" or "★ up". One tap to skip.

### 4.7 Boss — SCRUM-7

```
Portrait — before the fight
┌──────────────────────────────┐
│ $1.2K M340 U85 ✦12 K2/3    @ │
├──────────────────────────────┤
│ TIER 1 BOSS                  │
│ ┌──────────────────────────┐ │
│ │  [ Spore Foreman art ]   │ │
│ │                          │ │
│ └──────────────────────────┘ │
│ Spore Foreman                │
│ Recommended power   2,000    │
│ Your squad          1,850 !  │
│ ┌────┐┌────┐┌────┐┌────┐     │
│ │ F  ││ T  ││ S  ││ S  │     │
│ └────┘└────┘└────┘└────┘     │
│ First clear: ✦50 + Rare gear │
│ Unlocks: Tier 2 Mine + Exp.  │
│ K 2/3  (+1 in 2h 14m)        │
│ [   Challenge  (K 1)   ]     │
├──────────────────────────────┤
│  H     M•    E•    R     B   │
└──────────────────────────────┘
  ! = squad below recommended power
```

```
Landscape — before the fight
┌────┬────────────────────────────────────────────────────────────────┐
│    │ $1.2K   M340   U85   ✦12   K2/3 (+1 in 2h 14m)               @ │
│ H  ├───────────────────────────────┬────────────────────────────────┤
│    │ ┌───────────────────────────┐ │ Spore Foreman                  │
│ M• │ │ [ Spore Foreman art ]     │ │ Recommended 2,000              │
│    │ │                           │ │ Your squad  1,850 !            │
│ E• │ │                           │ │ ┌─────┐┌─────┐┌─────┐┌─────┐   │
│    │ └───────────────────────────┘ │ │  F  ││  T  ││  S  ││  S  │   │
│ R  │ First clear: ✦50 + Rare gear  │ └─────┘└─────┘└─────┘└─────┘   │
│    │ Unlocks: Tier 2               │                                │
│ B  │                               │     [  Challenge  (K 1)  ]     │
└────┴───────────────────────────────┴────────────────────────────────┘
```

```
Confirm (modal)                 Fight (auto, 30–60 s)
┌──────────────────────────┐    ┌──────────────────────────────┐
│ Spend 1 Guardian Ticket  │    │ Spore Foreman ▓▓▓▓▓░░░ 58%   │
│ to fight Spore Foreman?  │    │                              │
│                          │    │       [ boss ]               │
│ Your squad is below      │    │                              │
│ the recommended power.   │    │  [F] [T] [S] [S]             │
│                          │    │  ▓▓▓ ▓▓▓ ▓▓░ ▓▓▓  (HP bars)  │
│ [ Cancel ]  [ Fight T1 ] │    │                      [ x2 ]  │
└──────────────────────────┘    └──────────────────────────────┘

Result — Lose
┌────────────────────────────────┐
│ DEFEAT                         │
│ Boss HP left: 32%              │
│ Your power is 150 below the    │
│ recommended level.             │
│                                │
│ Try:                           │
│ • Level up your Fighters  →    │
│ • Run Spore Caverns for gear → │
│ [     Back to HQ     ]         │
└────────────────────────────────┘
```

- The warning ⚠ appears when squad power is below recommended. The fight is still
  allowed; the player decides.
- Minions out on an Expedition can't be picked (scope §4.2) and show "Busy".
- **Lose** always explains why and links to the fix (scope §4.5, SCRUM-19). The
  arrows open the matching screen.
- **Win** shows first-clear rewards and a big "Tier 2 unlocked: Magma Works" banner
  with buttons to the new Mine and Expedition.
- Fight speed toggle (×1 / ×2) is optional for Step A.

### 4.8 Settings (built)

```
Portrait                                   Landscape
┌──────────────────────────┐   ┌──────────────────────────────────────────┐
│        Settings          │   │                Settings                  │
│ Master Volume      100%  │   │  Master Volume                    100%   │
│ ═══════════●             │   │  ════════════════════●                   │
│ ☐ Mute Audio             │   │  ☐ Mute Audio                            │
│                          │   │              [  Back  ]                  │
│     [    Back    ]       │   └──────────────────────────────────────────┘
└──────────────────────────┘
```

Built. With this ticket the slider and the mute row are 130 tall and fully
tappable.

---

## 5. What changed in the build for this ticket

| Change | File |
|---|---|
| New `SafeAreaFitter` keeps content clear of notches/cutouts and updates on rotation | `Assets/Scripts/UI/SafeAreaFitter.cs` |
| Every view gets a `SafeArea` child for its content; backgrounds stay full-screen | `Assets/Scripts/Editor/GameUIBuilder.cs` |
| Minimum touch target constant (130 units ≈ 48 dp) | `GameUIBuilder.cs` |
| Volume slider: 50 → 130 tall, whole height tappable, 80 × 80 knob | `GameUIBuilder.cs` |
| Mute toggle: 80 → 130 tall, whole row tappable (was only the 70 × 70 box) | `GameUIBuilder.cs` |
| HUD Menu button: 120 → 130 tall | `GameUIBuilder.cs` |

To apply them to the scene, run **Tools → Dungeon Conveyor → Build Game UI** again
(choose *Replace*) and save the scene.

---

## 6. Device test checklist

Run on at least one tall phone with a notch or punch-hole camera (20:9) and one
16:9 phone or tablet. In the Editor, the Device Simulator covers most of this.

For **each** of: portrait, portrait upside-down, landscape left, landscape right:

- [ ] Main Menu, Settings and the in-game HUD: nothing under the notch or rounded corners
- [ ] All text readable at arm's length; nothing cut off or overlapping
- [ ] Every button, the slider and the mute row can be hit with a thumb on the first try
- [ ] Rotating while on each screen keeps the screen and its values (volume, mute)
- [ ] Main Menu and Settings fit without scrolling on a 20:9 phone in landscape

---

## 7. Next steps (not part of this ticket)

- **SCRUM-12:** turn these wireframes into a visual design system (colors, type
  scale, icons, button states) in the Arcane/Borderlands style from scope §4.9.
- **SCRUM-9 and the system tickets:** build the screens. Add `UIState`s for HQ, Mine,
  Expedition, Minions and Boss, plus the tab bar / side rail that switches layout
  on aspect ratio.
- Step B adds: Store and premium currency, optional-ad buttons (double loot, shorten
  Expedition), pity counter, star-rank upgrades, equipment upgrade screen.
