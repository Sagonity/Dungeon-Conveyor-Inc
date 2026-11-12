# Expedition: Deepshaft — Technical Architecture & Tooling

**Jira:** SCRUM-4 · **Status:** Final, v1.0 (2026-11-12)
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
      State/      GameState
      Save/       ISaveStorage, FileSaveStorage, MemorySaveStorage,
                  SaveService, SaveMigrations
      (Expeditions/, Boss/, Summon/ … added by their tickets)
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
- `MinionStats` and `MinionHealth` are legacy. They get replaced by
  `MinionDefinition` + `OwnedMinion` (SCRUM-10) and the boss auto-combat (SCRUM-7).

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
| When | On `OnApplicationPause(true)` and `OnApplicationQuit`, after important actions (summon, boss result, upgrade), and every 30 s while playing. Wired up in SCRUM-17 |
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
| Branches | One branch per ticket, e.g. `SCRUM-5-mining`. PR into `main`, then delete the branch |
| Commits | Start with the ticket key: `SCRUM-5: Add automatic mining` |
| PRs | Describe what changed and how to test it. Tests must pass in the Test Runner |
| Builds | APKs go on GitHub Releases, never into git (`.gitignore` blocks them) |
| Large files | Turn on **Git LFS** before the first art lands (SCRUM-13): `*.psd`, `*.fbx`, `*.blend`, `*.png`, `*.wav`, `*.mp3`. Not enabled yet, because everyone needs `git lfs install` first |
| CI | Later: GameCI on GitHub Actions to run tests and build an APK on each PR. Needs a Unity license secret set up by the repo owner |

---

## 11. What's built for this ticket

- `Deepshaft.Core` assembly: clock, wallet and resources, Minion definitions and owned
  Minions, game database, game state, save system with backup and versioning.
- `Deepshaft.Core.Tests`: EditMode tests for all of the above.
- `.editorconfig` for consistent formatting.

**Not built yet (later tickets):** `GameSession` and save timing (SCRUM-17), roster and assignment (SCRUM-6), Mine Tier Bosses and Expeditions (first clear
+ automation) (SCRUM-7), HQ and Smith (SCRUM-9), Minion levels, breakthroughs and gear
(SCRUM-10), Summon (SCRUM-11).

**Updated for Game Design Summary v3.4 (save version 2):** `ResourceId` now has Gold,
Iron, Silver, Mithril, Basic/Advanced Shards, Minion Essence, rare Expedition materials and
unique boss materials (Copper and Upgrade Materials retired; the v1→v2 migration drops
them). `Rarity` is Common/Uncommon/Rare, `MinionRole` adds Archer and Mage, Miners get a
fixed `MinerTrait`, and `OwnedMinion.stars` became `breakthroughs`.

**Mining (SCRUM-5):** `MiningService` runs automatic mining on top of `GameState.mining`
(last bank time + uncollected output, fractions kept).
- Every owned Miner mines the **highest unlocked tier**. Assignment and slot count come
  with SCRUM-6 (`MiningService.GetWorkingMiners`).
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

## 12. Open questions for the team

1. Branch naming `SCRUM-<n>-<topic>`: OK for everyone?
2. Who owns the Unity license for CI, and do we want CI before the playtest (SCRUM-8)?
3. Turn on Git LFS now, or when SCRUM-13 art starts?
