# Expedition: Deepshaft — Core Loop Playtest Plan

**Jira:** SCRUM-8 (Step A milestone) · **Status:** Ready to run once PR #21 is merged and a new APK is built
**Build:** grey-box (IMGUI) · **Testers:** 5+ people who haven't seen the game
**Success criteria from:** [MVP Scope](MVP_Scope_and_Core_Loop.md) §8 · **Feeds:** SCRUM-2 (economy), SCRUM-19 (balancing)

---

## 1. What we want to learn

| # | Success criterion | How we check it | Pass if |
|---|---|---|---|
| C1 | Testers can explain "mine, upgrade, beat the boss, clear and automate the Expedition, break through" after 10 minutes | Ask question Q1 at 10 min (§5); write the answer down word for word | 4 of 5 name at least 4 of the 5 steps |
| C2 | After losing a boss or first clear, testers go back to upgrading without being told | Watch; check the log: after a `boss_fight … lost` or `first_clear … lost`, is the next action a `level_up`, `gear_upgrade` or breakthrough? | 4 of 5 |
| C3 | Testers understand that Expedition materials are needed for breakthroughs | Q3 in the interview; check the log for a `minion_breakthrough` or `gear_breakthrough` | 4 of 5 answer correctly |
| C4 | Every session ends with at least one meaningful upgrade | Log: at least one `level_up`, `gear_upgrade`, breakthrough or `smith_rank` with status `Ok` per session | Every session of every tester |
| C5 | Testers who come back after a long break feel rewarded, not punished | Session 2 (§4); Q6 and the reaction to the "Welcome back" message | 4 of 5 say rewarded or neutral; nobody says punished |
| C6 | All 3 tiers can be cleared without paying | Already checked by the simulated player (§3); confirm nobody hits a dead end | No dead end seen |
| C7 | Is stopping an automated Expedition to fight a fair choice or a chore? | Watch the moment it happens; Q5 | Notes only (decision input) |

**Decide after the playtest:** team sizes (3 + 3), tier pacing, Ascension, plus the findings in §3.

---

## 2. Readiness checklist

Do these before the playtest day.

- [ ] Review and merge PR #21 (SCRUM-10, 9, 11, and this plan)
- [x] Run the EditMode tests in Unity (Test Runner → EditMode → Run All). **Passed in Unity, 2026-11-19**
- [ ] Open the project and check that the 13 new Minion assets in `Assets/Content/Minions` import without warnings
- [ ] Build `APK/Expedition Deepshaft v0.0.3.apk` and the iOS build of 0.0.3, and install them on the test phones
- [ ] On iPhones, use **Copy log** to export the playtest log: **Save log file** writes into the app's own folder,
  which the Files app doesn't show unless file sharing is turned on in the Xcode project
- [ ] On each phone: play 2 minutes, then Settings → Debug → **Copy log** and paste it somewhere. You should see `new_game`, `tab …` and your actions
- [ ] On each phone: Settings → **Reset game** so every tester starts fresh (copy the log first; reset deletes it)
- [ ] Charge the phones, turn on Do Not Disturb, set the screen timeout to 10 minutes or more
- [ ] Print the observation sheet (§6), one per tester
- [ ] Decide whether to change the pacing first (§3). If not, change the session lengths below to match

---

## 3. What the simulated player already shows

`PlaythroughTests` plays the whole prototype as a simple free player with the default numbers.
It checks in every hour (an engaged player) or every 8 hours (about three sessions a day).
Each check-in it collects, buys the cheapest upgrade it can afford until it can't, summons, and fights
when the team is within 90% of the recommended power.

| Milestone | Check-in every 1 h | Check-in every 8 h |
|---|---|---|
| Beat Tier 1 boss (Spore Foreman) | hour 1 | hour 8 |
| First clear Spore Caverns | hour 3 | hour 16 |
| Beat Tier 2 boss (Slag Golem) | hour 4 | hour 24 |
| First clear Magma Works | hour 6 | hour 32 |
| Beat Tier 3 boss (Prism Wyrm) | hour 8 | hour 40 |
| First clear Crystal Depths (end) | **hour 11** | **hour 48 (day 3)** |
| Minion breakthroughs used | **none** (all Minions stay at the level-20 cap) | 2 per Minion |
| Team power at the end (needed: 300) | 286 | **522** |

**Findings for SCRUM-2 (economy owner), before or after the playtest:**

1. **C6 passes:** both players clear all 3 tiers without paying; there's no dead end.
2. **The whole prototype takes about 11 hours of play,** or 2 days for someone who plays three times a day.
   Pacing per tier is still undecided (SCRUM-2); this is the first real number for that discussion.
3. **Minion breakthroughs aren't needed** by an engaged player: gear upgrades alone carry the team
   to the end. "Break through" is one of the 5 loop steps testers should learn (C1, C3). Testers may
   never do one in a 30-minute session.
4. **Long absences beat active play by a lot.** The 8-hour player ends with almost twice the needed
   power, because mining has no cap and the Mine keeps running. SCRUM-2 has an open item to check exactly this.
5. **A level-1 starting team can already beat the Tier 1 boss** (power 66 vs. 80 recommended). The first
   "lose, then upgrade" moment (C2) may not happen until Tier 2. Note at what point testers first lose.
6. **Expedition loot is invisible until the player stops the Expedition.** Rare materials are earned
   all the time but only reach the wallet on "Stop & Collect". Watch whether testers realise this (C3, C7).
7. **Most Basic summons are duplicates.** New players already own 7 of the 8 Common Minions, so 75% of
   Basic summons give Essence, and Essence has no use in the prototype (no Ascension).

**Suggestion:** run the playtest with the numbers as they are, since the findings above are what we
want to see in people. If the team would rather tune first, the quickest single change is to raise the
boss and Expedition recommended power (and enemy stats) on the GameDatabase asset.

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
 [ ] First level-up                      time __:__
 [ ] First Smith upgrade                 time __:__
 [ ] First summon                        time __:__   result _________
 [ ] First boss fight                    time __:__   won / lost
 [ ] First LOSS (boss or first clear)    time __:__   what next? ______________________  (C2)
 [ ] First first clear                   time __:__
 [ ] Started automation                  time __:__
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
| `view` | `tab Minions` | Where the tester went, e.g. after a loss (C2) |
| `collect_mine` | `412 Gold + 160 Iron` | |
| `level_up` / `minion_breakthrough` | `Minion_Tank:C#3 Ok level=5` / `CantAfford` | Upgrades (C4); failed taps show confusion |
| `gear_upgrade` / `gear_breakthrough` / `smith_rank` | `… Tool Ok level=3` | Upgrades (C3, C4) |
| `summon` | `Adventure Basic new Minion_Mage:U Uncommon` / `duplicate … essence=5` | |
| `boss_fight` / `first_clear` | `spore_foreman lost power=72 rec=80` | Losses (C2), power vs. recommended |
| `expedition_start` / `expedition_stop` | `spore_caverns hours=3.2 6 SporeSilk + …` | Automation (C7) |
| `assign_*` / `remove_team` | `Minion_Archer:C#7 Ok` | |
| `debug_skip` | `hours=8` | Same-day break (session 2) |

The `minutes` column counts from the first event and includes debug skips, so a skipped break shows up as a jump.

---

## 8. Results template

Fill in after all testers; attach the logs and sheets to SCRUM-8.

| Criterion | T1 | T2 | T3 | T4 | T5 | Result | Notes |
|---|---|---|---|---|---|---|---|
| C1 explains the loop at 10 min | | | | | | _/5 | |
| C2 back to upgrading after a loss | | | | | | _/5 | |
| C3 knows Expedition materials → breakthroughs | | | | | | _/5 | |
| C4 every session ends with an upgrade | | | | | | | |
| C5 return feels rewarding | | | | | | _/5 | |
| C6 no dead end | | | | | | | |
| C7 stopping the Expedition: fair or chore | | | | | | | |
| Time to first boss win (min) | | | | | | | |
| Tier reached in session 1 | | | | | | | |

**Decisions to take afterwards** (record them in the scope doc §9): team sizes, tier pacing (§3 findings),
Ascension (what Essence is for), whether Expedition loot should reach the wallet without stopping.
