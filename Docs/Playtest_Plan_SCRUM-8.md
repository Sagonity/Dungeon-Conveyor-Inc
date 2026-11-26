# Expedition: Deepshaft — Core Loop Playtest Plan

**Jira:** SCRUM-8 (Step A milestone) · **Status:** Run 2026-11-25 → 26 with **v0.0.6** (web playtest). Results in §8
**Build:** grey-box (IMGUI) · **Testers:** 5+ people who haven't seen the game
**Success criteria from:** [MVP Scope](MVP_Scope_and_Core_Loop.md) §8 · **Feeds:** SCRUM-2 (economy), SCRUM-19 (balancing)

---

## 1. What we want to learn

| # | Success criterion | How we check it | Pass if |
|---|---|---|---|
| C1 | Testers can explain "mine, upgrade, beat the boss, clear and automate the Expedition, break through" after 10 minutes | Ask question Q1 at 10 min (§5); write the answer down word for word | 4 of 5 name at least 4 of the 5 steps |
| C2 | After losing a boss or first clear, testers go back to upgrading without being told | Watch; check the log: after a `boss_fight … lost` or `first_clear … lost`, is the next action a `level_up(_max)`, `gear_upgrade(_max)`, breakthrough or `swap_team`? A `view loss_hint …` means they used the "Lost to …" card's buttons (counts, but note it) | 4 of 5 |
| C3 | Testers understand that Expedition materials are needed for breakthroughs | Q3 in the interview; check the log for a `minion_breakthrough` or `gear_breakthrough` | 4 of 5 answer correctly |
| C4 | Every session ends with at least one meaningful upgrade | Log: at least one `level_up` / `gear_upgrade` / breakthrough / `smith_rank` with status `Ok`, or a `level_up_max` / `gear_upgrade_max` with `levels` ≥ 1, per session | Every session of every tester |
| C5 | Testers who come back after a long break feel rewarded, not punished | Session 2 (§4); Q6 and the reaction to the "Welcome back" message | 4 of 5 say rewarded or neutral; nobody says punished |
| C6 | All 3 tiers can be cleared without paying | Already checked by the simulated player (§3); confirm nobody hits a dead end | No dead end seen |
| C7 | Is stopping an automated Expedition to fight a fair choice or a chore? | Watch the moment it happens; Q5 | Notes only (decision input) |

**Decide after the playtest:** team sizes (3 + 3), tier pacing, Ascension, plus the findings in §3.

---

## 2. Readiness checklist

Do these before the playtest day. Playtest build: **v0.0.5** (everything up to SCRUM-34).

- [x] All code for the playtest merged to `main` (SCRUM-9, 10, 11, 32, 33, 34)
- [x] EditMode tests passed in Unity before SCRUM-34 (187 tests, 2026-11-24)
- [ ] Run the EditMode tests in Unity once more on 0.0.5 (expect 190)
- [x] `APK/Expedition Deepshaft v0.0.5.apk` built; internal phone tests of 0.0.3–0.0.5 done (findings on SCRUM-8)
- [x] Pacing: run the playtest with the current numbers and tune afterwards (team decision 2026-11-25; §3)
- [ ] Build the iOS version of 0.0.5 and share it through TestFlight (for testers on their own iPhones)
- [ ] Install 0.0.5 on every test phone. On Android, uninstall older builds first, or reset the game (below)
- [ ] On each phone: play 2 minutes, then Settings → Debug → **Copy log** and paste it somewhere. You should see `new_game`, `view tab …` and your actions
- [ ] On each phone: Settings → **Reset game** so every tester starts fresh (copy the log first; reset deletes it). A new game shows **300 Gold and 120 Iron**
- [ ] On iPhones, export logs with **Copy log**: **Save log file** writes into the app's own folder, which the Files app doesn't show
- [ ] Tell testers not to open Settings → Debug (the facilitator uses it for the break and the log)
- [ ] Charge the phones, turn on Do Not Disturb, set the screen timeout to 10 minutes or more
- [ ] Print the observation sheet (§6), one per tester

### What testers will see in 0.0.5

- The game opens straight into the game screen (no Main menu). Settings (top right) has **Sound** and the debug tools.
- A new game starts with 300 Gold and 120 Iron, so there's something to buy straight away; the Tier 1 boss's first win also gives 200 Gold.
- **Max ×N** buttons on a Minion's card and on gear rows at the Smith buy several levels in one tap.
- After a lost fight, a **"Lost to …"** card shows the power gap with **Level up** / **Upgrade gear** buttons.
- After a first clear, a **"… cleared"** card offers **Start automation**. A running Expedition shows its rate, not
  "earned so far": its loot arrives when it's stopped.
- When the team or the Mine is full, a benched Minion's card offers **Swap with …**.
- Wherever something can't be afforded, the screen says what's missing and where to get it.

---

## 3. What the simulated player already shows

`PlaythroughTests` plays the whole prototype as a simple free player with the default numbers and the
prototype roster (7 starter Commons, plus the Uncommon and Rare Minions from summons). It runs three
play rhythms with 5 seeds each.
Each check-in it collects (stopping the Expedition to get its loot), summons, puts its best Minions to
work, does every breakthrough it can, buys the cheapest upgrade until nothing is affordable, and fights
every next step it's within 90% of. It plays perfectly, so real players will be slower.
Run it again after any tuning change; the table is in the test output.

**Hour of game time each step was first cleared** (2026-11-25, with the SCRUM-32 fixes; hour 0 = first launch):

| Check-in | T1 boss | T1 Exped. | T2 boss | T2 Exped. | T3 boss | **T3 Exped. (end)** | Lost fights | Minion breakthroughs |
|---|---|---|---|---|---|---|---|---|
| Every 1 h | 0 | 1–2 | 2–4 | 3–5 | 5–8 | **7–14 h** | 1–7 | 0 |
| Every 8 h | 0 | 8 | 8 | 8–16 | 16 | **24–32 h** | 1–2 | 1–3 |
| Once a day | 0 | 24 | 24 | 24 | 24–48 | **48–72 h** | 1 | 3–8 |

The first launch always has something to buy; summons are about half duplicates. Before the fixes, the first launch
had nothing to buy, and daily players never lost a fight or needed a Minion breakthrough (economy doc §9).

**Internal phone tests (Marcus):**
- **0.0.3, real time:** first launch empty; levelling found only on day 2; 26 retries in a row on Crystal Depths.
- **0.0.4, hourly debug skips:** the loss card's buttons were used at once; Max used 25 times; everything cleared at
  ~11 h of game time, inside the simulated 7–14 h.
- **0.0.5:** Minions levelled in the first minute; Swap used 3 times.
- Details on SCRUM-8.

**Findings for SCRUM-2 (economy owner), before or after the playtest:**

1. **C6 passes:** every run clears all 3 tiers without paying; there's no dead end.
2. **The prototype lasts 2–3 return visits:** a once-a-day player finishes on day 2, an 8-hourly player on day 1–2.
3. **Being away beats playing.** The daily player clears Tier 1 and Tier 2 together on its first return and
   never loses; the hourly player loses fights for the same progress. Mining has no cap, so a long break
   earns more than active play can spend.
4. **Losing is rare** unless the player checks in very often, so the "lose, upgrade, win" moment (C2) may
   barely happen. A level-1 starting team can even beat the Tier 1 boss (power 66 vs. 80 recommended).
   (Since SCRUM-32, every rhythm loses at least once.)
5. **Minion breakthroughs are almost never needed:** gear upgrades carry the team. "Break through" is one of
   the 5 loop steps testers should learn (C1, C3).
6. **The first session has nothing to buy:** a new game starts with 0 Gold and earns about 3 Gold a minute.
7. **Expedition loot is invisible until the Expedition is stopped,** and automation was started late in the phone test.
8. **About half of all summons are duplicates,** and Essence has no use in the prototype (no Ascension).
9. **One tap per level** makes levelling tedious (about 480 taps in the phone test).

**Fixed in SCRUM-32 (2026-11-24):** findings 6, 7 and 9, and the retry loop after a loss. New games start with
300 Gold + 120 Iron, and the Tier 1 boss's first win gives 200 Gold. "Max ×N" buttons buy many levels in one tap.
A card after a lost fight shows the power gap and links to the Minions tab and the Smith. A card after a first
clear offers "Start automation". With these, the simulated player always has something to buy on the first
launch and beats the Tier 1 boss in the first session. Overall pacing (findings 2–5) is unchanged and stays with SCRUM-2.

**Decided (2026-11-25):** run the playtest with the numbers as they are and tune afterwards (SCRUM-2). Also
fixed since: team swaps in one tap (SCRUM-34) and "where to get it" hints for rare materials (SCRUM-33).

---

## 4. Session format

| Part | Length | What happens |
|---|---|---|
| Intro | 3 min | Read the script (§5). Consent to notes. No tutorial |
| Session 1 | 30 min | Free play, thinking aloud. Q1 at 10 min. The facilitator only helps after 2 minutes stuck |
| Short interview | 5 min | Q2–Q5 |
| Break | 1 day (best) or a debug skip | **Best:** the tester keeps the phone or app until the next day. **Same-day option:** Settings → Debug → **+8 h**, and tell them "it's now 8 hours later" |
| Session 2 | 10–15 min | "Welcome back" message, then free play. Watch the first reaction |
| Final interview | 5 min | Q6–Q8 |
| Export | 1 min | Settings → Debug → **Copy log** (or **Save log file**), paste it into the results folder as `tester-N.csv` |

Mix of testers: at least 2 who play idle or gacha mobile games and at least 1 who doesn't.

**Remote testers:** instead of a phone, they can play in a browser at `/playtest` on the website with a tester
code; their log is sent automatically and downloaded from `/playtest/admin.html` (no Copy log needed). Setup:
`Website/playtest/README.md` (SCRUM-35). Run the interview by video call.

---

## 5. Facilitator script

**Intro (read out):**
> "Thanks for helping. This is an early grey-box version of a mobile game; the graphics are placeholders.
> We're testing the game, not you, so there are no wrong answers. Please think aloud: say what you're
> looking at, what you expect and what surprises you. I'll mostly stay quiet. If you get really stuck
> I'll help, but try first. You can stop at any time."

**During play:**
- Don't explain anything. If asked "what does this do?", answer "What do you think it does?"
- Help only after **2 minutes stuck**, and write down what they were stuck on.
- Write down quotes, especially surprise, confusion or delight.

**Questions:**

| When | # | Question |
|---|---|---|
| 10 min in | Q1 | "In your own words, what do you do in this game?" (C1, write it down word for word) |
| After session 1 | Q2 | "What were you trying to do just now? What would you do next?" |
| | Q3 | "What are Spore Silk, Ember Core and Prism Dust for?" (C3) |
| | Q4 | "Was there a moment you didn't know what to do?" |
| | Q5 | "How did it feel to stop the Expedition to fight the boss?" (C7) |
| After session 2 | Q6 | "How did it feel to come back? Did you feel rewarded, or that you'd missed out?" (C5) |
| | Q7 | "What would make you open the game again tomorrow?" |
| | Q8 | "One thing you'd change?" |

---

## 6. Observation sheet (one per tester)

```
Tester #___   Date ______   Plays mobile games: none / casual / idle-gacha   Phone ______

Session 1 start __:__
 [ ] Found Collect without help          time __:__
 [ ] First level-up                      time __:__   used Max ×N? Y / N
 [ ] First Smith upgrade                 time __:__
 [ ] First summon                        time __:__   result _________
 [ ] First boss fight                    time __:__   won / lost
 [ ] First LOSS (boss or first clear)    time __:__   what next? ______________________  (C2)
     Used the "Lost to …" card's buttons?  Y / N
 [ ] First first clear                   time __:__
 [ ] Started automation                  time __:__   from the "… cleared" card? Y / N
 [ ] Stopped Expedition to fight         time __:__   reaction __________________     (C7)
 [ ] Breakthrough (Minion or gear)       time __:__                                    (C3)
 Q1 answer (10 min): ____________________________________________________________ (C1)
 Stuck moments (>2 min): ________________________________________________________
 Session 1 ended with an upgrade? Y / N                                                (C4)

Session 2 start __:__   hours away ___ (real / debug)
 First reaction to "Welcome back": ______________________________________________ (C5)
 Session 2 ended with an upgrade? Y / N                                                (C4)

Quotes:
```

---

## 7. Reading the playtest log

Settings → Debug → **Copy log** gives CSV: `utc,minutes,type,detail`. The log stays on the device and
is kept in the save, so it survives app restarts. **Reset game deletes it**, so copy it first.

| type | detail (example) | Use |
|---|---|---|
| `new_game` / `session_start` | `hours_away=8.0 tier=2 waiting_mine=… waiting_expedition=…` | Session boundaries (C4, C5) |
| `view` | `tab Minions` / `loss_hint Minions` / `loss_hint Smith` | Where the tester went, e.g. after a loss (C2); `loss_hint …` = a button on the "Lost to …" card |
| `collect_mine` | `412 Gold + 160 Iron` | |
| `level_up` / `minion_breakthrough` | `Minion_Tank:C#3 Ok level=5` / `CantAfford` | Upgrades (C4); failed taps show confusion |
| `level_up_max` / `gear_upgrade_max` | `Minion_Tank:C#3 levels=7 level=10` | Several levels in one tap (C4) |
| `gear_upgrade` / `gear_breakthrough` / `smith_rank` | `… Tool Ok level=3` | Upgrades (C3, C4) |
| `summon` | `Adventure Basic new Minion_Mage:U Uncommon` / `duplicate … essence=5` | |
| `boss_fight` / `first_clear` | `spore_foreman lost power=72 rec=80` | Losses (C2), power vs. recommended |
| `expedition_start` / `expedition_stop` | `spore_caverns hours=3.2 6 SporeSilk + …` | Automation (C7) |
| `assign_*` / `remove_team` / `unassign_miner` | `Minion_Archer:C#7 Ok` / `NoFreeSlot` | |
| `swap_team` / `swap_miner` | `Minion_Archer:R#9 for Minion_Fighter:C#4 Ok` | Team changes in one tap |
| `debug_skip` | `hours=8` | Same-day break (session 2) |
| `debug_give` | `100 BasicShard` | Resources added from Settings → Debug (SCRUM-38): progress was helped |

The `minutes` column counts from the first event and includes debug skips, so a skipped break shows up as a jump.

---

## 8. Results (2026-11-25 → 26, v0.0.6, web playtest)

**Testers:** 6 codes played. **T1–T4 are outside testers** and count for the criteria. 005-WSVO and 005-6HNU
are **internal** (developer and co-developer); their logs are used as extra evidence, their answers are not
counted. The plan expected 5 outside testers, so the targets below are read as "of 4" (4 of 5 = 80 %).
Logs: downloaded from the admin page and kept offline with the code list (not in the repo or Jira), because
the codes and device IDs link to the testers.

| Criterion | T1 005-Q39O | T2 005-Q8LP | T3 005-J4R9 | T4 005-OFV3 | Result | Notes |
|---|---|---|---|---|---|---|
| C1 explains the loop at 10 min | ✅ 4 of 5 (asked ~15 min) | ❌ 3 of 5 (10 min) | ❌ 2 of 5 (10 min; had played 7) | – no answer | **Fail** (1 of 3 answers; 4 of 4 needed) | Mine and upgrade always named. Expeditions missing 2 of 3, breakthroughs 3 of 3 (often not reached by 10 min). T3 had beaten a boss and didn't mention bosses |
| C2 back to upgrading after a loss | ✅ loss card → Level up | ✅ loss card → 7 level-ups | ✅ both sessions, via the loss card | ❌ quit 3 s after the loss | **3 of 4** (Fail, close) | Loss card used by everyone who continued (5 of 5 logs incl. internal) |
| C3 knows Expedition materials → breakthroughs | ✅ correct; gear breakthroughs in log | – | – (answers not received) | – | 1 of 1 answered | Too few answers to judge |
| C4 every session ends with an upgrade | ✅ | ✅ | ✅ both sessions | ❌ no upgrade at all | **Fail** (T4) | T4 played about 1 minute |
| C5 return feels rewarding | ✅ "felt rewarding for progression to take a break" | – never returned | – returned after 21 h, answer not received | – never returned | 1 of 1 answered | Internal 005-6HNU: "I did feel rewarded!" |
| C6 no dead end | ✅ Tier 3 reached | – quit at Tier 1 (Spore Caverns) | ✅ Tier 3 boss reached | – quit after 1 min | **Pass** (no dead end seen) | But 2 of 4 quit early, one at the Spore Caverns wall |
| C7 stopping the Expedition: fair or chore | "I did not try that" | – | Stopped after 23 s, for nothing, to fight; answer not received | – | No data | Internal 005-6HNU: "pretty alright, but it feels like a bit of a loss" |
| Time to first boss win (min) | 1.8 | 0.8 | 2.1 | 1.0 | | Everyone wins Tier 1 at once (66 vs 80) |
| Tier reached in session 1 | 2 (Spore Caverns cleared at 92 min) | 2 (stopped at Spore Caverns) | 2 (stopped at Spore Caverns) | 2 (stopped after the Slag Golem loss) | | |
| Real play time | ~1.5 h real + debug skips | ~4 min, 1 session | 7 min + 4 min next day | ~1 min | | |

### Findings (most important first)

1. **The Spore Caverns first clear is a wall** (power ~66–90 vs 100). Hit by **5 of 5** testers who got that far (T4 stopped before it). T2 quit there
   after ~4 minutes; T3 ended session 1 there and only passed after a 21-hour break; T1 waited 25 minutes and
   used the +1 h debug skip out of boredom; internal 005-6HNU retried 8 times in 3 seconds.
2. **Testers don't know what to do or where to go next.** Said outright by T1 ("didn't know where to start or
   what I should be focusing on") and T3 ("not always clear what you need to upgrade or where to go next");
   005-6HNU was confused about needing Expeditions for Spore Silk. T3 spent the first 2 minutes on the locked
   Expeditions tab. C1 failed.
3. **After the Tier 1 win, testers fight the next boss straight away and lose badly.** T1, T2 and T4 pressed
   Fight on Slag Golem 2–6 seconds after beating the Tier 1 boss (66 vs 135); T3 did the same with Prism Wyrm
   after Slag Golem (181 vs 240). For T4 it was the last thing they did. The Boss tab shows the next, much
   stronger boss with the same Fight button in the same place.
4. **Levelling Minions isn't obvious.** T3 opened the Minions tab 6 times through the loss card in session 1
   and never levelled anyone. Single taps instead of Max in 3 of 6 logs (~60 single taps for T1); gear Max
   never used.
5. **Benched Minions get levelled or forgotten.** 3 of 6 logs levelled a benched Minion; T3's Rare Mage sat on
   the bench through two losses until a swap took power from 82 to 181. New Minions from a summon go to the
   bench without a prompt.
6. **The loss card works as a pointer** (used in every log that continued after a loss), but it can't close a
   power gap the player can't afford yet (finding 1).
7. **Automation is understood:** everyone who cleared an Expedition started automating it within seconds.
8. **Testers use Settings → Debug** (T1 skipped time out of boredom).
9. **Wishes:** login bonuses (T1, added to SCRUM-16); music, animations, seeing the fight, rarity-specific
   animations and summon animations (T1); "the graphics" (005-6HNU).

### Playtest tooling notes

- Closing the tab right after the last minute-upload can make the website store the last lines twice (the
  game resends them at the next start). Seen once (T3). Duplicates are exact copies and can be dropped.
- If the send at close fails and the tester never returns, up to about a minute at the end is missing.

### Proposed fixes (findings 1–5 built in SCRUM-36; tooling not scheduled)

| Finding | Fix |
|---|---|
| 1 | Lower Spore Caverns' recommended power (100 → ~85) and/or raise the starting resources; re-run `PlaythroughTests` |
| 2 | A "next goal" line (e.g. "Clear Spore Caverns: power 84 / 100 → level up your team") and a short guided start |
| 3 | After a boss win, show the victory and the next boss's power before Fight is pressable again; "Too weak (66 vs 135). Fight anyway?" |
| 4 | Make Level up and Max ×N obvious on the Minions tab; gear Max more visible |
| 5 | Warn when levelling a benched Minion; "Swap into the team?" after a new summon |
| 8 + tooling | Hide Debug in web playtests; send the last lines with `sendBeacon` on close; drop duplicate lines in the admin CSV |

**Decisions to take afterwards** (record them in the scope doc §9): team sizes, tier pacing (§3 findings and
finding 1 above), Ascension (what Essence is for: duplicates give Essence that can't be spent yet), whether
Expedition loot should reach the wallet without stopping.
