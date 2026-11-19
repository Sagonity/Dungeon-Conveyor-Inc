# Expedition: Deepshaft — Technical Architecture & Tooling

**Jira:** SCRUM-4 · **Status:** Final, v1.1 (2026-11-17)
**Engine:** Unity 6 (6000.6), URP 2D · **Platform:** Android first, iOS later
**Related:** [MVP Scope](MVP_Scope_and_Core_Loop.md) · [UI Wireframes](UI_Wireframes_and_Readability.md)

This doc says how the game's code is organised, so every system ticket
(SCRUM-5, 6, 7, 9, 10, 11, 17) is built the same way.

---

## 1. Goals

1. **Game rules are testable without the scene.** Mining output, Expedition results,
   costs and save/load are plain C# that runs in EditMode tests in milliseconds.
2. **Time keeps running.** Mining and automated Expeditions progress while menus
   are open, while the app is closed and while the phone is locked, with no time cap
   on Expedition rewards (scope doc §4.1, §4.3; Game Design Summary v3.4 §4).
3. **Content is data.** Minions, tiers, bosses and items are assets designers can
   edit in the Inspector, not prefabs with hard-coded numbers.
4. **Saves never get lost.** Atomic writes, a backup, versioning and migrations from day one.
5. **Small and boring.** No frameworks we don't need (no DI container, no ECS).

---

## 2. Layers

```
┌───────────────────────────────────────────────────────────────┐
│ Presentation  (Assembly-CSharp, MonoBehaviours)               │
│  UIManager, views, HUD, animations, input                     │
│  Reads state, calls systems, listens to their events          │
├───────────────────────────────────────────────────────────────┤
│ Session glue  (Assembly-CSharp)                               │
│  GameSession: loads the save, owns the systems, saves on      │
│  pause/quit. The only place that knows about everything.      │
├───────────────────────────────────────────────────────────────┤
│ Systems  (Deepshaft.Core, plain C#)                           │
│  Wallet, Mining, Smith, Expeditions, Roster, Summon, Boss     │
│  Rules only. Get IClock, GameState and content passed in.     │
├───────────────────────────────────────────────────────────────┤
│ State  (Deepshaft.Core, [Serializable] data)                  │
│  GameState: everything that is saved                          │
├───────────────────────────────────────────────────────────────┤
│ Content  (Deepshaft.Core, ScriptableObjects)                  │
│  MinionDefinition, GameDatabase (+ tiers, bosses, items later)│
└───────────────────────────────────────────────────────────────┘
        Dependencies only point downwards.
```

| Rule | Why |
|---|---|
| Systems never use `UnityEngine.Time`, `FindObjectOfType`, scenes or prefabs | So they can be tested and keep working while paused |
| Systems change state only through their own methods and raise C# events | The UI updates from events, not by polling |
| The UI never changes `GameState` directly | All rules live in one place |
| Content is read-only at runtime | Saves store only ids and player progress |

---

## 3. Folders and assemblies

```
Assets/
  Scripts/
    Core/                         Deepshaft.Core.asmdef  (namespace Deepshaft.*)
      Time/       IClock, SystemClock, ManualClock, UnixTime
      Economy/    ResourceId, ResourceAmount, WalletData, Wallet
      Minions/    MinionKind/Role/Rarity, MinionDefinition, OwnedMinion
      Content/    GameDatabase
      Mining/     MiningRules, MiningMath, MiningService, MiningData (SCRUM-5)
      Roster/     RosterService, RosterRules, RosterData, ITeamBusySource (SCRUM-6)
      Combat/     CombatRules, CombatSimulator, Combatant, BattleResult, AdventureTeam (SCRUM-7)
      Bosses/     BossDefinition, BossService, BossData (SCRUM-7)
      Expeditions/ ExpeditionDefinition, ExpeditionService, ExpeditionData (SCRUM-7)
      Progression/ TierRequirement, TierProgressionService (SCRUM-7)
      State/      GameState
      Save/       ISaveStorage, FileSaveStorage, MemorySaveStorage,
                  SaveService, SaveMigrations
      (Smith/, Summon/ … added by their tickets)
    UI/                           Assembly-CSharp (views, UIManager, SafeAreaFitter)
    Editor/                       Assembly-CSharp-Editor (GameUIBuilder, tools)
    *.cs                          Assembly-CSharp (GameController, legacy Minion scripts)
  Tests/
    EditMode/                     Deepshaft.Core.Tests.asmdef
  Content/                        ScriptableObject assets: Minions/, Tiers/, … (added with the first content)
Docs/                             Design and tech docs
```

- `Deepshaft.Core` is **auto-referenced**, so existing scripts in Assembly-CSharp can
  use it without changes. Core can't reference Assembly-CSharp; that's on purpose.
- New gameplay rules go in Core. New MonoBehaviours go in `Scripts/` (Assembly-CSharp)
  until there are enough to deserve their own assembly.
- The legacy `MinionStats` / `MinionHealth` scene scripts and the summon placeholder were removed in
  SCRUM-10. Minions are save data (`MinionDefinition` + `OwnedMinion`); combat is the SCRUM-7 autobattle.

---

## 4. Time and background progress

**Decision:** everything that runs in the background stores **when it started**
as a UTC timestamp and works out progress from the clock. Nothing counts down
with `Time.deltaTime`.

```
progress = clamp((clock.UtcNow - startedAt) / duration, 0, 1)
```

| System | Stored | Worked out on demand |
|---|---|---|
| Mining | `lastCollectedAtUnixMs`, production rate at that moment | Earned = rate × elapsed, **no cap** |
| Automated Expedition | which Expedition, `startedAtUnixMs` (or last collect), rate | Rare materials earned = rate × elapsed, **no cap**, no diminishing returns. The Adventure team is busy while it runs |
| Expedition first clear | which Expeditions are cleared (permanent) | Whether automation is unlocked |

- **`IClock`** is passed into every system. The game uses `SystemClock.Instance`;
  tests and debug tools use `ManualClock` to jump ahead hours in one line.
- Because nothing depends on `Time.timeScale`, `GameController` can keep pausing
  visuals in menus without stopping the economy.
- **Changes to a production rate** (assigning a Miner, a Smith upgrade, a new mine tier,
  stopping an Expedition): first bank the
  production earned so far at the old rate, then apply the change. Each system does
  this in one method, so the math is only written once.
- **Clock cheating** (setting the phone's date forward) is accepted for the MVP: it's
  single-player and offline. If it matters later, use server time (post-MVP, SCRUM-17).
  The save already records `lastSavedAtUnixMs`, so going **backwards** in time can be
  detected and treated as "no time passed".

---

## 5. Content as data

- One **`MinionDefinition`** asset per Minion type: id, name, kind (Miner/Adventure),
  combat class (Adventure) or fixed trait (Miner), rarity (Common/Uncommon/Rare),
  stat growth, level cap per breakthrough.
- All definitions are listed in one **`GameDatabase`** asset. Systems look content up
  by **id**. Tiers, bosses, items and Expeditions get added to the database by their tickets.
- **Ids are permanent.** Save files store ids such as `"spore_bruiser"`. Renaming the
  asset or the display name is fine; changing the id breaks saves.
- If a save refers to an id that no longer exists, the system skips it and logs a
  warning. It never crashes.
- **Enums stored in saves** (`ResourceId`, `MinionKind`, …) have explicit numbers.
  Never reorder or reuse them.

---

## 6. Saving

| Topic | Decision |
|---|---|
| Format | JSON via Unity's `JsonUtility` (no extra package, fast, enough for plain data) |
| Where | `Application.persistentDataPath/save.json` (+ `.bak`) |
| What | One `GameState` object: progress only, no content |
| When | On `OnApplicationPause(true)` and `OnApplicationQuit`, after every action, and every 30 s while playing. Wired up in SCRUM-30 (`GameSession`, `GameSessionRunner`) |
| Safety | Write to `.tmp`, then swap; the previous save becomes `.bak`. A damaged save falls back to the backup |
| Versions | `GameState.version`. Older saves are upgraded in `SaveMigrations`. A save from a **newer** build is never overwritten (saving is turned off and the player is told) |
| Limits of JsonUtility | No dictionaries, no `DateTime`, no polymorphism. Use lists of `[Serializable]` classes and Unix-ms `long`s. If this ever hurts, switch to Newtonsoft JSON inside `SaveService` only. It's only in the project indirectly today (pulled in by `com.unity.ai.assistant` and `com.unity.purchasing`), so add `com.unity.nuget.newtonsoft-json` to `Packages/manifest.json` first |
| Cloud save | Post-MVP (SCRUM-17). It's another `ISaveStorage` |

---

## 7. Randomness

Summons, loot and combat take a **`System.Random` passed in**, never
`UnityEngine.Random` or a new `Random()` inside the system. Tests pass a seeded
`Random` and get the same results every run. Drop-rate tests run many seeded
summons and check the rates and pity guarantee (SCRUM-11).

---

## 8. Testing

- **EditMode tests** in `Assets/Tests/EditMode` cover every Core system. Run them in
  *Window → General → Test Runner → EditMode → Run All*.
- There is no CI, so **run the EditMode tests in Unity before merging a PR** that
  changes Core. Cloud (Claude) sessions can't run Unity; their PRs must say the tests
  still need a run.
- Each system ticket adds tests for its rules (e.g. "mining keeps adding up with no cap",
  "an automated Expedition can't fail", "the team can't fight while an Expedition runs").
- PlayMode tests only where a MonoBehaviour does real work. The UI is checked by
  hand with the device checklist in the wireframes doc.
- Tests must not leave errors in the Console: expected warnings/errors use `LogAssert.Expect`.

---

## 9. Coding conventions

- English for all names, comments, logs and commit messages.
- Namespaces: `Deepshaft.<Area>` in Core (`Deepshaft.Economy`, `Deepshaft.Save`, …).
- `[SerializeField] private` fields + read-only properties for Inspector data. Public
  fields only in `[Serializable]` save-data classes (JsonUtility needs them).
- Formatting comes from `.editorconfig` (4 spaces, Allman braces, `_` not used for
  private fields to match the existing code).
- No `Debug.Log` in hot paths or per-frame code. Warnings for recoverable problems,
  errors for bugs and data loss.

---

## 10. Tooling and workflow

| Area | Decision |
|---|---|
| Unity | 6000.6.x. Everyone uses the version in `ProjectSettings/ProjectVersion.txt` |
| IDE | VS Code or Rider. Installed packages: `com.unity.ide.rider` and `com.unity.ide.visualstudio` (also used by VS Code). Don't commit machine-specific settings such as `dotnet.defaultSolution` |
| Branches | One branch per ticket, named `SCRUM-<n>-<topic>`, e.g. `SCRUM-5-mining`. PR into `main`, then delete the branch |
| Commits | Start with the ticket key: `SCRUM-5: Add automatic mining` |
| PRs | Title starts with the ticket key. Describe what changed and how to test it. EditMode tests must pass in the Test Runner before merging |
| Builds | APKs are committed to `APK/`, named `Expedition Deepshaft v<version>.apk`. Other build output (`/Builds/`, `/IOS/`, `*.aab`) stays out of git |
| Large files | Turn on **Git LFS** before the first art lands (SCRUM-13): `*.psd`, `*.fbx`, `*.blend`, `*.png`, `*.wav`, `*.mp3`. Not enabled yet, because everyone needs `git lfs install` first. Worth adding `*.apk` to LFS then too, so each build stops growing every clone |
| CI | Not planned. Tests are run by hand in Unity (see §8) and builds are made locally |

---

## 11. What's built for this ticket

- `Deepshaft.Core` assembly: clock, wallet and resources, Minion definitions and owned
  Minions, game database, game state, save system with backup and versioning.
- `Deepshaft.Core.Tests`: EditMode tests for all of the above.
- `.editorconfig` for consistent formatting.

**Not built yet (later tickets):** HQ and Smith (SCRUM-9), Summon (SCRUM-11).
Minion levels, breakthroughs and gear: SCRUM-10, below.

**Updated for Game Design Summary v3.4 (save version 2):** `ResourceId` now has Gold,
Iron, Silver, Mithril, Basic/Advanced Shards, Minion Essence, rare Expedition materials and
unique boss materials (Copper and Upgrade Materials retired; the v1→v2 migration drops
them). `Rarity` is Common/Uncommon/Rare, `MinionRole` adds Archer and Mage, Miners get a
fixed `MinerTrait`, and `OwnedMinion.stars` became `breakthroughs`.

**Mining (SCRUM-5):** `MiningService` runs automatic mining on top of `GameState.mining`
(last bank time + uncollected output, fractions kept).
- The **assigned** Miners (SCRUM-6, below) mine the **highest unlocked tier**
  (`MiningService.GetWorkingMiners`).
- Output per hour = Σ Miners: tier rate × (mining power ÷ 10) × (1 + tool bonus) × trait bonus.
  Gold trait +25% Gold, Ore trait +25% Iron/Silver/Mithril, Speed trait +15% everything.
  Rates and bonuses live in `GameDatabase` → *Mining* (defaults = the v3.4 table; final
  numbers from SCRUM-2).
- Tools come in through `IMinerToolBonus`; the Smith (SCRUM-9) implements it. Until then no tools.
- No cap, works from `IClock`, so it runs in menus, during Expeditions and while the app is closed.
  A clock set backwards earns nothing until it passes the last bank again.
- **Rate changes:** call `MiningService.ChangeRate(() => …)` (or `AddMinion` / `UnlockTier`)
  so the old rate is banked first.
- `Collect(wallet)` moves whole units into the Wallet.
- Not yet: on-screen grey-box. The HUD needs the `GameSession` that owns `GameState` (SCRUM-17).

**Assignment (SCRUM-6):** `RosterService` decides who works. There is no movement or
placement: the player only picks Minions for slots.
- `GameState.roster` saves the Miner slots and the Adventure team as lists of Minion
  instance ids, in slot order.
- Miner Minions only go in Miner slots; Adventure Minions only go in the team
  (`AssignResult.WrongKind`). Only assigned Miners mine.
- Slot counts live in `GameDatabase` → *Roster* (`minerSlots`, `adventureSlots`).
  **Placeholders: 3 and 3** until the playtest (v3.4 §8). If a count is lowered, only the
  first slots work.
- Changing the Miners goes through `MiningService.ChangeRate`, so the old rate is banked first.
- New Minions take a free slot of their kind automatically (`RosterService.AddMinion`); the
  player can swap them with `AssignMiner` / `UnassignMiner` / `AssignToTeam` / `RemoveFromTeam`.
  Every call returns an `AssignResult` the UI can explain (`NoFreeSlot`, `TeamBusy`, ...).
- **Busy team:** while an automated Expedition runs, the team can't be edited
  (`AssignResult.TeamBusy`). The Expedition system (SCRUM-7) implements `ITeamBusySource`;
  until then the team is never busy. SCRUM-7 also adds the "Stop Expedition & Collect" action.
- **Old saves:** the first time a save without a roster is used, empty slots are filled from
  owned Minions in roster order (`RosterData.initialized`), so they keep mining. No version bump.
- Not yet: the two Step A lists on screen (they need `GameSession`, SCRUM-17), and team power
  for the "Team / Rec." line (SCRUM-7, SCRUM-10).

**Bosses, Expeditions and tier progression (SCRUM-7):**
- **Autobattle** (`CombatSimulator`): plain C# with a passed-in `System.Random`, so the same team,
  enemies and seed always give the same fight. The whole fight is worked out at once and returned as
  a `BattleResult` with a timed event list (attack, area attack, heal, knocked out); rewards are given
  straight away and the UI replays the events over the fight's 30–60 s.
  - Everyone acts on their own attack timer. Team Minions hit the front enemy; **Mages** hit every
    enemy; **Supports** heal the most hurt ally and give the team +10% damage; enemies hit a standing
    **Tank** first (it also takes 25% less), otherwise a random Minion; some enemies hit the whole
    team every Nth attack. Not won within 60 s = lost.
  - **Team power** = Σ (ATK + HP ÷ 10), × 1.1 with a Support. Bosses and Expeditions show a
    recommended power; the default content is tuned so most teams at that power win and teams
    15% below it usually lose (teams with a Tank and a Support do better than that).
  - `BattleResult.DescribeLoss()` explains a loss: "Out of time. Boss HP left: 32%. Team power 54 is
    below the recommended 70. …" Grey-box English until SCRUM-25.
  - Numbers live in `GameDatabase` → *Combat* (per-class timers and multipliers, heal, Support bonus,
    time limit). Class behaviour comes from `MinionDefinition.Role`; stats from its HP/ATK at the
    Minion's level. Gear bonuses come with SCRUM-9/10.
- **Mine Tier Bosses** (`BossService`): one per tier, in `GameDatabase` → *Bosses*. A boss can be
  fought once its tier is unlocked; attempts are free and unlimited. A win gives the boss's unique
  material (every win; the first win's list can add a one-time bonus), and the first win unlocks
  the Expeditions whose `unlockedByBoss` is that boss, permanently. Beaten bosses are saved in
  `GameState.bosses`.
- **Expeditions** (`ExpeditionService`), in `GameDatabase` → *Expeditions*:
  - **First clear:** an autobattle that can fail. A win gives the one-time bonus and unlocks
    automation for good (`GameState.expeditions.cleared`).
  - **Automated:** can't fail. Earns `automatedPerHour` from the clock (start time + last bank,
    like the Mine), so it runs while the app is closed. No time cap, no diminishing returns.
    Only one at a time. `StopAndCollect` moves whole units to the Wallet; fractions carry over
    to the next run.
  - **Busy team:** `ExpeditionService` is the `ITeamBusySource`. While it runs, the team can't be
    edited and can't fight a boss or a first clear (`TeamBusy`). Miners keep mining.
  - A save whose running Expedition was removed from the content stops it (with a warning) so the
    team isn't busy forever.
- **Tier progression** (`TierProgressionService`): each tier after the first has a
  `TierRequirement` in `GameDatabase` → *Tier Requirements*: bosses beaten, Expeditions
  first-cleared, and an optional cost. Without a cost the tier unlocks by itself as soon as it's met
  (checked after every boss win and first clear); with a cost the player pays with `TryUnlockNext`.
  Unlocking goes through `MiningService.UnlockTier`, so the old rate is banked first. A tier with
  no row is the end of the content. **Placeholder until SCRUM-2:** tier 2 needs the tier 1 boss,
  tier 3 the tier 2 boss.
- **Prototype content** (`DefaultAdventureContent`, also on the GameDatabase asset): 3 bosses
  (recommended power 80 / 135 / 240) and 3 Expeditions (100 / 175 / 300) with the v3.4 placeholder
  materials (Foreman Sigil, Golem Heart, Wyrm Scale; Spore Silk, Ember Core, Prism Dust). Names
  and numbers are placeholders (SCRUM-13, SCRUM-2/19).
- **Wiring** (done in `GameSession`, SCRUM-30): `mining` → `tiers = new TierProgressionService(state, db, mining)`
  → `expeditions = new ExpeditionService(state, db, clock, tiers)` → `roster = new RosterService(state, db, mining, expeditions)`
  → `bosses = new BossService(state, db, expeditions, tiers)`.
- Save format stays version 2: the new `bosses` and `expeditions` fields load as empty from older saves.
- Not yet: the fight replay on screen. The grey-box screens come with SCRUM-30 (below).

### SCRUM-30: GameSession and the grey-box screen

- **`GameSession`** (`Deepshaft.Session`, pure C#): loads the save, builds the services in the
  wiring order above, gives a new game the `GameDatabase` starter Minions, and reports what was earned
  while away (`OfflineReport`). The UI changes the game only through its action methods
  (`CollectMine`, `FightBoss`, `FirstClearExpedition`, `StartExpedition`, `StopExpedition`, roster changes),
  which save and raise `Changed`. Tests: `GameSessionTests`.
- **`GameSessionRunner`** (MonoBehaviour, created before the first scene loads): loads
  `Resources/GameDatabase`, saves every 30 s and on pause, focus loss and quit.
- **`GreyboxGameScreen`**: the game screen, styled after the UI mock-up: resource bar (tap for every
  material), tier header with the next goal, bronze-framed cards with gold buttons, and a bottom tab bar
  (HQ, Mine, Exped., Minions, Smith, Boss). Settings (top right) holds Main menu and the debug tools.
  Drawn with IMGUI and generated textures, so no art or scene setup. Portraits, boss art and tier names
  are placeholders (SCRUM-13); Smith and Summon show as "coming soon" (SCRUM-9, SCRUM-11).
  Throwaway until the real UI (SCRUM-12).
- **Debug:** `OffsetClock` skips time ahead (kept in PlayerPrefs). The "level everyone up" debug action
  was removed in SCRUM-10: Minions now level up for real in the Minions tab.
- **Time** still comes from the device clock. The team decided on a server clock (Scope decision 20);
  it plugs in as another `IClock`.

### SCRUM-10: Minion levels, breakthroughs and gear

- **`MinionProgressionService`** (`Deepshaft.Minions`): `CheckLevelUp` / `TryLevelUp` and
  `CheckBreakthrough` / `TryBreakthrough` return a `MinionUpgradeCheck` (status, cost, shortfall, level,
  cap) so the UI can explain what's missing. Changes go through `MiningService.ChangeRate`, so a Miner's
  new level counts only from now on. Allowed while the team is on an automated Expedition (automation
  can't fail and doesn't use team power). `GameSession.LevelUpMinion` / `BreakthroughMinion` save and
  raise `Changed`.
- **`MinionProgressionRules`** (on the GameDatabase): level-up cost per resource
  = base × growth^(level − 1), default 25 Gold + 10 Iron, ×1.12 per level, the same for Miners and
  Adventurers. Breakthroughs need the Minion at its cap; the 1st costs Spore Silk (Tier 1 Expedition),
  the 2nd Ember Core (Tier 2), the 3rd Prism Dust (Tier 3), plus Gold and Iron. The number of rows is
  the number of breakthroughs (3: level cap 20 → 30 → 40 → 50). First-pass numbers until SCRUM-2/19.
- **Gear, per Minion** (`Deepshaft.Equipment`): Miners have a Tool, Adventurers a Weapon and Armor
  (`EquipmentRules.SlotsFor`). Saved as `OwnedMinion.equipment` (slot, level, breakthroughs); a slot
  with no entry is the basic level-1 item. Fixed stats: each level above 1 adds Tool +10% mining output,
  Weapon +8% ATK, Armor +8% HP; cap 4, +4 per breakthrough (`EquipmentRules` on the GameDatabase).
  The Tool feeds mining through `EquipmentToolBonus` (`IMinerToolBonus`); Weapon and Armor feed
  `Combatant.ForMinion`. **Upgrading gear is the Smith (SCRUM-9).**
- **`MinionStatCalculator`**: a Minion's HP, ATK and mining power with level and gear, so the screen,
  combat and mining use the same numbers.
- **Ascension is not built** (team decision 2026-11-19). Minion Essence just accumulates.
- Save format stays version 2: `equipment` loads as empty from older saves (`GameState.EnsureValid`).
- **Grey-box screen:** tapping a Minion card selects it; the detail card shows Lv / cap, stats now →
  next level, Level Up, Breakthrough (with its cost shown before the cap), gear, and the Mine / team
  button (assigning moved here from the card tap).
- Tests: `MinionProgressionTests`, plus paid level-ups in `GameSessionTests`.

## 12. Team decisions (2026-11-17)

1. **Branch naming:** `SCRUM-<n>-<topic>`, as in §10.
2. **CI:** not planned. EditMode tests are run by hand in Unity before merging (§8).
3. **Git LFS:** turned on when SCRUM-13 art starts. **APKs stay in git** in `APK/`.
