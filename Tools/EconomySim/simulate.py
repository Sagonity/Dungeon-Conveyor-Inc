#!/usr/bin/env python3
"""
Economy pacing simulator for Expedition: Deepshaft (SCRUM-2).

Plays the MVP economy with simple bot players and checks the pacing targets
from Docs/Economy_and_Progression.md. All numbers come from economy.json.

    python3 Tools/EconomySim/simulate.py              # print summary
    python3 Tools/EconomySim/simulate.py --report     # also write Docs/Economy_Simulation_Report.md

Standard library only. Deterministic for a given --seed.

NOTE: this models the v1.1 economy. Game Design Summary v3.4 (Docs/Game_Design_Summary.md)
changed resources, Expeditions, summoning and the Smith, so the model needs rebuilding (SCRUM-2).
"""
import argparse
import json
import math
import os
import random
import statistics
from dataclasses import dataclass, field

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))


# ----------------------------------------------------------------------------- data

def load_config(path):
    with open(path) as f:
        return json.load(f)


@dataclass
class Gear:
    slot: str      # weapon / armor / tool
    rarity: str
    level: int = 1


@dataclass
class Minion:
    defn: dict
    level: int = 1
    stars: int = 0
    gear: dict = field(default_factory=dict)

    @property
    def kind(self):
        return self.defn["kind"]


class Economy:
    """Formulas only. Everything is read from the config."""

    def __init__(self, cfg):
        self.cfg = cfg
        self.minion_defs = {m["id"]: m for m in cfg["minions"]}
        self.tiers = {t["tier"]: t for t in cfg["tiers"]}

    # --- stats
    def level_cap(self, m):
        lv = self.cfg["levels"]
        return lv["capAtZeroStars"] + lv["capPerStar"] * m.stars

    def _growth(self, base, per, level):
        return base * (1 + per * (level - 1))

    def gear_value(self, gear):
        spec = self.cfg["equipment"]["byRarity"][gear.rarity][gear.slot]
        return spec[0] + spec[1] * (gear.level - 1)

    def hp_atk(self, m):
        base = self.cfg["adventureBase"]
        role = self.cfg["roles"][m.defn["role"]]
        mult = self.cfg["rarity"][m.defn["rarity"]]["statMultiplier"]
        hp = self._growth(base["hp"] * role["hp"] * mult, base["growthPerLevel"], m.level)
        atk = self._growth(base["atk"] * role["atk"] * mult, base["growthPerLevel"], m.level)
        if "weapon" in m.gear:
            atk += self.gear_value(m.gear["weapon"])
        if "armor" in m.gear:
            hp += self.gear_value(m.gear["armor"])
        return hp, atk

    def minion_power(self, m):
        hp, atk = self.hp_atk(m)
        return atk + hp / self.cfg["power"]["hpPerPower"]

    def squad_power(self, squad):
        total = sum(self.minion_power(m) for m in squad)
        if any(m.defn["role"] == "Support" for m in squad):
            total *= 1 + self.cfg["power"]["supportBonus"]
        return total

    def best_squad(self, adventurers):
        size = self.cfg["power"]["squadSize"]
        ranked = sorted(adventurers, key=self.minion_power, reverse=True)
        best = ranked[:size]
        # A Support may be worth a slot even if it's not top-4 on its own.
        supports = [m for m in ranked if m.defn["role"] == "Support" and m not in best]
        if supports and len(best) == size:
            alt = best[:-1] + [supports[0]]
            if self.squad_power(alt) > self.squad_power(best):
                best = alt
        return best

    def mining_power(self, m):
        base = self.cfg["minerBase"]
        mult = self.cfg["rarity"][m.defn["rarity"]]["statMultiplier"]
        mp = self._growth(base["miningPower"] * mult, base["growthPerLevel"], m.level)
        if "tool" in m.gear:
            mp += self.gear_value(m.gear["tool"])
        return mp

    # --- costs
    def level_cost(self, m):
        lv = self.cfg["levels"]
        L = m.level
        return {
            "Gold": round(lv["costGoldBase"] * lv["costGoldGrowth"] ** (L - 1)),
            "UpgradeMaterials": lv["costUpgradeMaterialsBase"] + (L - 1) // lv["costUpgradeMaterialsEveryNLevels"],
        }

    def mine_upgrade_cost(self, which, level, depth):
        spec = self.cfg["mine"][which]
        mineral = self.tiers[depth]["mineral"]
        return {
            "Gold": round(spec["costGoldBase"] * spec["costGoldGrowth"] ** level),
            mineral: round(spec["costMineralBase"] * spec["costMineralGrowth"] ** level),
        }

    def gear_upgrade_cost(self, gear):
        eq = self.cfg["equipment"]
        tier = eq["byRarity"][gear.rarity]["tier"]
        e = gear.level
        return {
            "Gold": round(eq["costGoldBase"] * eq["costGoldGrowth"] ** (e - 1)),
            self.tiers[tier]["rareMaterial"]: eq["costRareMaterialBase"] + (e - 1) // eq["costRareMaterialEveryNLevels"],
        }


# ----------------------------------------------------------------------------- player state

class Player:
    def __init__(self, eco: Economy, rng: random.Random, time_scale: float):
        self.eco = eco
        self.cfg = eco.cfg
        self.rng = rng
        self.ts = time_scale          # >1 = playtest build with faster timers
        self.res = {}
        for k, v in self.cfg["start"]["resources"].items():
            self.res[k] = float(v)
        self.minions = [Minion(eco.minion_defs[i]) for i in self.cfg["start"]["minions"]]
        self.spare_gear = []
        self.tier = 1
        self.first_summon_done = False
        self.pity = 0
        # Mine
        self.depth = 1
        self.dig = 0
        self.cap = 0
        self.stock = {}
        self.mine_t = 0.0
        # Expedition
        self.exp = None   # dict(tier, end, share, squad)
        # Tickets
        self.tickets = self.cfg["start"].get("guardianTickets", self.cfg["guardianTickets"]["max"])
        self.ticket_t = 0.0
        # Log
        self.events = []
        self.boss_wins = {}
        self.boss_losses = 0
        self.summons = 0

    # --- helpers
    def add(self, rewards, mult=1.0):
        for k, v in rewards.items():
            self.res[k] = self.res.get(k, 0.0) + v * mult

    def can_afford(self, cost):
        return all(self.res.get(k, 0.0) >= v for k, v in cost.items())

    def pay(self, cost):
        for k, v in cost.items():
            self.res[k] -= v

    def adventurers(self, include_busy=True):
        busy = self.exp["squad"] if (self.exp and not include_busy) else []
        return [m for m in self.minions if m.kind == "Adventure" and m not in busy]

    def miners(self):
        ranked = sorted([m for m in self.minions if m.kind == "Miner"], key=self.eco.mining_power, reverse=True)
        return ranked[: self.cfg["mine"]["minerSlots"]]

    def squad(self):
        return self.eco.best_squad(self.adventurers())

    def power(self):
        return self.eco.squad_power(self.squad())

    # --- mine (lazy: banked whenever the rate is about to change)
    def mine_rates(self):
        mp = sum(self.eco.mining_power(m) for m in self.miners())
        bonus = 1 + self.cfg["mine"]["digSpeed"]["bonusPerLevel"] * self.dig
        out = self.cfg["mine"]["outputPerPowerPerHour"][str(self.depth)]
        return {k: v * mp * bonus * self.ts for k, v in out.items()}

    def cap_hours(self):
        s = self.cfg["mine"]["stockpile"]
        return s["baseHours"] + s["hoursPerLevel"] * self.cap

    def mine_update(self, t):
        dt_h = (t - self.mine_t) / 60.0
        self.mine_t = t
        if dt_h <= 0:
            return
        cap_h = self.cap_hours() / self.ts
        for k, rate in self.mine_rates().items():
            cap = rate * cap_h
            cur = self.stock.get(k, 0.0)
            self.stock[k] = max(cur, min(cur + rate * dt_h, cap))

    def mine_full(self):
        cap_h = self.cap_hours() / self.ts
        rates = self.mine_rates()
        return all(self.stock.get(k, 0) >= r * cap_h - 1e-6 for k, r in rates.items())

    def collect_mine(self, t):
        self.mine_update(t)
        self.add(self.stock)
        self.stock = {}

    # --- tickets
    def tickets_update(self, t):
        g = self.cfg["guardianTickets"]
        regen = g["regenHours"] * 60.0 / self.ts
        if self.tickets >= g["max"]:
            self.ticket_t = t
            return
        n = int((t - self.ticket_t) // regen)
        if n > 0:
            self.tickets = min(g["max"], self.tickets + n)
            self.ticket_t += n * regen
            if self.tickets >= g["max"]:
                self.ticket_t = t

    # --- expeditions
    def expedition_choice(self):
        """Deepest unlocked Expedition the squad can mostly handle (what players do in practice)."""
        power = self.power()
        best = 1
        for tier in range(1, min(self.tier, len(self.eco.tiers)) + 1):
            if power >= 0.8 * self.eco.tiers[tier]["expedition"]["recommendedPower"]:
                best = tier
        return best

    def send_expedition(self, t):
        tier = self.expedition_choice()
        ex = self.eco.tiers[tier]["expedition"]
        squad = self.squad()
        share = min(1.0, max(self.cfg["expeditions"]["minLootShare"], self.eco.squad_power(squad) / ex["recommendedPower"]))
        self.exp = {"tier": tier, "end": t + ex["minutes"] / self.ts, "share": share, "squad": squad}

    def collect_expedition(self, t):
        ex = self.eco.tiers[self.exp["tier"]]["expedition"]
        share = self.exp["share"]
        loot = dict(ex["loot"])
        loot = {k: max(1.0, v * share) if k == "SummonShards" else v * share for k, v in loot.items()}
        self.add(loot)
        if self.rng.random() < ex["gearChance"] * share:
            self.spare_gear.append(Gear(self.rng.choice(["weapon", "armor", "tool"]), ex["gearRarity"]))
        self.exp = None

    # --- summons
    def summon(self):
        cost = self.cfg["summon"]["costShards"]
        self.res["SummonShards"] -= cost
        self.summons += 1
        if not self.first_summon_done and self.cfg["start"].get("firstSummonGuaranteed"):
            defn = self.eco.minion_defs[self.cfg["start"]["firstSummonGuaranteed"]]
            self.first_summon_done = True
        else:
            self.pity += 1
            if self.pity >= self.cfg["summon"]["pityEpicWithin"]:
                rarity = "Epic"
            else:
                roll, acc, rarity = self.rng.random(), 0.0, "Common"
                for r, spec in self.cfg["rarity"].items():
                    acc += spec["summonRate"]
                    if roll < acc:
                        rarity = r
                        break
            if rarity == "Epic":
                self.pity = 0
            defn = self.rng.choice([d for d in self.cfg["minions"] if d["rarity"] == rarity])
        owned = next((m for m in self.minions if m.defn["id"] == defn["id"]), None)
        if owned:
            owned.stars = min(self.cfg["levels"]["maxStars"], owned.stars + 1)
        else:
            self.minions.append(Minion(defn))

    # --- gear
    def equip_best(self):
        pool = self.spare_gear + [g for m in self.minions for g in m.gear.values()]
        for m in self.minions:
            m.gear = {}
        slots = self.cfg["equipment"]["slots"]
        # Strongest minions get the strongest gear.
        order = sorted(self.adventurers(), key=self.eco.minion_power, reverse=True) + self.miners()
        for m in order:
            for slot in slots[m.kind]:
                cands = [g for g in pool if g.slot == slot]
                if cands:
                    g = max(cands, key=self.eco.gear_value)
                    pool.remove(g)
                    m.gear[slot] = g
        self.spare_gear = pool


# ----------------------------------------------------------------------------- bot behaviour

class Bot:
    """A reasonable, not optimal, player: collect, resend, summon, spend cheapest-first, fight when ready."""

    BOSS_ATTEMPT_AT = 0.95  # attempts the boss at 95% of recommended power

    def __init__(self, player: Player, t_now_fn):
        self.p = player

    def spend(self, t):
        p, eco = self.p, self.p.eco
        actions = 0
        while p.res.get("SummonShards", 0) >= p.cfg["summon"]["costShards"]:
            p.mine_update(t)
            p.summon()
            actions += 1
        p.mine_update(t)
        p.equip_best()

        target_cap_hours = 8
        while True:
            options = []
            # Stockpile cap first while it's shorter than a night.
            if p.cap < p.cfg["mine"]["stockpile"]["maxLevel"] and p.cap_hours() < target_cap_hours:
                options.append(("cap", eco.mine_upgrade_cost("stockpile", p.cap, p.depth), 0))
            if p.dig < p.cfg["mine"]["digSpeed"]["maxLevel"]:
                options.append(("dig", eco.mine_upgrade_cost("digSpeed", p.dig, p.depth), 1))
            squad = p.squad()
            avg_squad = sum(m.level for m in squad) / max(1, len(squad))
            for m in squad:
                if m.level < eco.level_cap(m):
                    options.append((("lvl", m), eco.level_cost(m), 1))
            for m in p.miners():
                if m.level < eco.level_cap(m) and m.level < avg_squad:
                    options.append((("lvl", m), eco.level_cost(m), 2))
            for m in p.minions:
                for g in m.gear.values():
                    if g.level < p.cfg["equipment"]["byRarity"][g.rarity]["maxLevel"]:
                        options.append((("gear", g), eco.gear_upgrade_cost(g), 2))
            affordable = [o for o in options if p.can_afford(o[1])]
            if not affordable:
                break
            # Lowest priority number first, then cheapest in Gold.
            kind, cost, _ = min(affordable, key=lambda o: (o[2], o[1].get("Gold", 0)))
            p.mine_update(t)
            p.pay(cost)
            if kind == "cap":
                p.cap += 1
            elif kind == "dig":
                p.dig += 1
            elif kind[0] == "lvl":
                kind[1].level += 1
            elif kind[0] == "gear":
                kind[1].level += 1
            actions += 1
        return actions

    def try_boss(self, t):
        p, eco = self.p, self.p.eco
        if p.tier > len(eco.tiers) or p.tier in p.boss_wins:
            return False
        p.tickets_update(t)
        if p.tickets < 1 or p.exp is not None:
            return False
        boss = eco.tiers[p.tier]["boss"]
        ratio = p.power() / boss["recommendedPower"]
        if ratio < self.BOSS_ATTEMPT_AT:
            return False
        if p.tickets >= p.cfg["guardianTickets"]["max"]:
            p.ticket_t = t
        p.tickets -= 1
        bc = p.cfg["bossCombat"]
        win_chance = min(1.0, max(0.0, (ratio - bc["loseBelow"]) / (bc["winAbove"] - bc["loseBelow"])))
        if p.rng.random() < win_chance:
            p.boss_wins[p.tier] = t
            fc = boss["firstClear"]
            p.add({"SummonShards": fc["SummonShards"]})
            p.tickets += fc["GuardianTickets"]
            gid = fc["gear"]
            p.spare_gear.append(Gear({"weapon": "weapon", "armor": "armor", "tool": "tool"}[gid.split("_")[0]],
                                     {"c": "Common", "r": "Rare", "e": "Epic"}[gid.split("_")[1]]))
            if p.tier < len(eco.tiers):
                p.mine_update(t)
                p.tier += 1
                p.depth = p.tier
            else:
                p.tier += 1
            p.events.append((t, "boss_win", p.tier - 1, ratio))
            return True
        p.boss_losses += 1
        p.events.append((t, "boss_loss", p.tier, ratio))
        return False

    def session(self, start, minutes):
        p = self.p
        actions = 0
        was_full = None
        for minute in range(int(minutes) + 1):
            t = start + minute
            p.mine_update(t)
            if minute == 0:
                was_full = p.mine_full()
                p.collect_mine(t)
            if p.exp and t >= p.exp["end"]:
                p.collect_expedition(t)  # collecting isn't counted: "actions" = things bought or summoned
            actions += self.spend(t)
            self.try_boss(t)
            if p.exp is None:
                p.send_expedition(t)
            if minute % 5 == 0 and minute > 0:
                p.collect_mine(t)
        p.collect_mine(start + minutes)
        actions += self.spend(start + minutes)
        return actions, was_full


# ----------------------------------------------------------------------------- schedules

def schedule(kind, days):
    """Sessions as (start_minute_since_install, length_minutes). Install is at 19:00 on day 0."""
    out = [(0, 20)]  # first session: onboarding
    daily = {
        "engaged": [(8, 0, 6), (12, 30, 6), (17, 0, 6), (20, 0, 8), (22, 30, 5)],
        "casual":  [(8, 0, 5), (12, 30, 5), (19, 0, 8), (22, 30, 5)],
        "light":   [(8, 0, 5), (18, 0, 6), (22, 30, 5)],
    }[kind]
    for d in range(1, days + 1):
        for h, m, length in daily:
            start = (d * 24 + h - 19) * 60 + m
            if start > 0:
                out.append((start, length))
    out.sort()
    return out


# ----------------------------------------------------------------------------- runs

def run_once(eco, seed, sessions, time_scale=1.0):
    rng = random.Random(seed)
    p = Player(eco, rng, time_scale)
    bot = Bot(p, None)
    per_session = []
    for i, (start, length) in enumerate(sessions):
        actions, was_full = bot.session(start, length)
        per_session.append({"start": start, "actions": actions, "mine_full": was_full, "tier": p.tier,
                            "power": p.power()})
        if p.tier > len(eco.tiers):
            break
    return p, per_session


def pct(values, q):
    if not values:
        return None
    s = sorted(values)
    k = (len(s) - 1) * q
    lo, hi = math.floor(k), math.ceil(k)
    return s[lo] + (s[hi] - s[lo]) * (k - lo)


def fmt_minutes(m):
    if m is None:
        return "not reached"
    if m < 60:
        return "%d min" % round(m)
    h = m / 60.0
    if h < 48:
        return "%.1f h" % h
    return "%.1f days" % (h / 24)


def simulate(cfg, runs, seed, days):
    eco = Economy(cfg)
    results = {}
    for kind in ["engaged", "casual", "light"]:
        sess = schedule(kind, days)
        wins = {1: [], 2: [], 3: []}
        not_reached = {1: 0, 2: 0, 3: 0}
        idle_sessions = 0
        total_sessions = 0
        full_mines = 0
        losses = []
        summons = []
        first_session_t1 = 0
        for r in range(runs):
            p, per = run_once(eco, seed + r, sess)
            for tier in (1, 2, 3):
                if tier in p.boss_wins:
                    wins[tier].append(p.boss_wins[tier])
                else:
                    not_reached[tier] += 1
            if 1 in p.boss_wins and p.boss_wins[1] <= sess[0][1]:
                first_session_t1 += 1
            for s in per[1:]:
                total_sessions += 1
                if s["actions"] == 0:
                    idle_sessions += 1
                if s["mine_full"]:
                    full_mines += 1
            losses.append(p.boss_losses)
            summons.append(p.summons)
        results[kind] = {
            "wins": wins, "not_reached": not_reached, "runs": runs,
            "idle_share": idle_sessions / max(1, total_sessions),
            "full_mine_share": full_mines / max(1, total_sessions),
            "losses_median": statistics.median(losses),
            "summons_median": statistics.median(summons),
            "first_session_t1": first_session_t1 / runs,
            "sessions_per_day": len([s for s in sess if s[0] >= 24 * 60 and s[0] < 48 * 60]),
        }
    # Playtest build: one 45-minute sitting with timers sped up.
    play = {"wins": {1: [], 2: [], 3: []}, "runs": runs}
    scale = cfg.get("playtest", {}).get("timeScale", 10)
    minutes = cfg.get("playtest", {}).get("sessionMinutes", 45)
    for r in range(runs):
        p, _ = run_once(eco, seed + r, [(0, minutes)], time_scale=scale)
        for tier in (1, 2, 3):
            if tier in p.boss_wins:
                play["wins"][tier].append(p.boss_wins[tier])
    play["scale"] = scale
    play["minutes"] = minutes
    results["playtest"] = play
    return eco, results


def sample_log(eco, seed, days):
    sess = schedule("casual", days)
    rng = random.Random(seed)
    p = Player(eco, rng, 1.0)
    bot = Bot(p, None)
    rows = []
    for start, length in sess:
        before_tier = p.tier
        actions, was_full = bot.session(start, length)
        squad = p.squad()
        rows.append({
            "when": start, "len": length, "actions": actions, "mine_full": was_full,
            "tier": before_tier, "power": p.power(),
            "boss": eco.tiers[before_tier]["boss"]["recommendedPower"],
            "levels": "/".join(str(m.level) for m in squad),
            "gold": p.res.get("Gold", 0), "um": p.res.get("UpgradeMaterials", 0),
            "event": ", ".join("beat T%d boss" % e[2] for e in p.events if e[1] == "boss_win" and start <= e[0] <= start + length)
                     + ", ".join("lost to boss" for e in p.events if e[1] == "boss_loss" and start <= e[0] <= start + length),
        })
        if p.tier > 3:
            break
    return rows


# ----------------------------------------------------------------------------- output

TARGETS = [
    ("T1 boss beaten in the first session (casual)", "≥ 60% of players (3 of 5 testers)"),
    ("T2 boss (casual, median)", "1–2 days after install"),
    ("T3 boss (casual, median)", "2–3 days after T2"),
    ("Sessions with nothing to do", "≤ 10%"),
    ("Mine already full when a session starts", "some, not most (it's the reason to come back)"),
]


def summary_lines(results):
    c = results["casual"]
    lines = []
    t2 = pct(c["wins"][2], 0.5)
    t3 = pct(c["wins"][3], 0.5)
    lines.append(("T1 boss beaten in the first session (casual)", "%d%%" % round(100 * c["first_session_t1"])))
    lines.append(("T2 boss (casual, median)", fmt_minutes(t2)))
    lines.append(("T3 boss (casual, median)", fmt_minutes(t3) + ("" if t2 is None or t3 is None else " (%s after T2)" % fmt_minutes(t3 - t2))))
    lines.append(("Sessions with nothing to do", "%d%%" % round(100 * c["idle_share"])))
    lines.append(("Mine already full when a session starts", "%d%%" % round(100 * c["full_mine_share"])))
    return lines


def write_report(cfg, eco, results, log_rows, path, runs, seed):
    out = []
    w = out.append
    w("# Economy Simulation Report")
    w("")
    w("> ⚠️ **Out of date since Game Design Summary v3.4 (2026-11-12).** This report")
    w("> simulates the v1.1 economy. See the note at the top of")
    w("> [Economy, Progression & Rewards](Economy_and_Progression.md); the simulator is")
    w("> rebuilt for v3.4 in SCRUM-2.")
    w("")
    w("*Generated by `Tools/EconomySim/simulate.py --report` from `Tools/EconomySim/economy.json`. "
      "Don't edit by hand: change the JSON and run the script again.*")
    w("")
    w("Runs per player type: %d (seeds %d–%d). Bots are reasonable, not optimal: they collect, resend, "
      "summon whenever they can, spend cheapest-first, and fight the boss at 95%% of its recommended power." % (runs, seed, seed + runs - 1))
    w("")
    w("## Targets")
    w("")
    w("| Check | Target | Result |")
    w("|---|---|---|")
    res = dict(summary_lines(results))
    for name, target in TARGETS:
        w("| %s | %s | **%s** |" % (name, target, res[name]))
    w("")
    w("## When each boss falls (time since install)")
    w("")
    w("| Player type | Sessions/day | T1 boss (median) | T2 boss (p10 / median / p90) | T3 boss (p10 / median / p90) | Didn't beat T3 | Boss losses (median) | Summons (median) |")
    w("|---|---|---|---|---|---|---|---|")
    for kind in ["engaged", "casual", "light"]:
        r = results[kind]
        def trio(tier):
            v = r["wins"][tier]
            return " / ".join(fmt_minutes(pct(v, q)) for q in (0.1, 0.5, 0.9))
        w("| %s | %d | %s | %s | %s | %d%% | %s | %s |" % (
            kind.capitalize(), r["sessions_per_day"], fmt_minutes(pct(r["wins"][1], 0.5)), trio(2), trio(3),
            round(100 * r["not_reached"][3] / r["runs"]), r["losses_median"], r["summons_median"]))
    w("")
    pt = results["playtest"]
    w("## Playtest build (SCRUM-8)")
    w("")
    w("One %d-minute sitting with every timer %d× faster (Mining, Expeditions, Guardian Tickets)." % (pt["minutes"], pt["scale"]))
    w("")
    w("| Boss | Beaten by | Median time |")
    w("|---|---|---|")
    for tier in (1, 2, 3):
        v = pt["wins"][tier]
        w("| T%d | %d%% | %s |" % (tier, round(100 * len(v) / pt["runs"]), fmt_minutes(pct(v, 0.5)) if v else "—"))
    w("")
    w("## Example: one casual player, session by session")
    w("")
    w("Squad power and resources are at the end of each session. *Boss needs* is the boss of the tier the session started in.")
    w("")
    w("| Time since install | Length | Actions | Mine was full | Tier | Squad power | Boss needs | Squad levels | Gold left | Upgr. Mats left | Event |")
    w("|---|---|---|---|---|---|---|---|---|---|---|")
    for row in log_rows[:22]:
        w("| %s | %d min | %d | %s | %d | %d | %s | %s | %d | %d | %s |" % (
            fmt_minutes(row["when"]) if row["when"] else "0 (install)", row["len"], row["actions"],
            "yes" if row["mine_full"] else "", row["tier"], round(row["power"]),
            row["boss"], row["levels"], row["gold"], row["um"], row["event"]))
    w("")
    w("## Cost tables")
    w("")
    w("### Minion level-up (from level L to L+1)")
    w("")
    w("| L | Gold | Upgrade Materials | Common Fighter power at L |")
    w("|---|---|---|---|")
    fighter = Minion(eco.minion_defs["fighter_c"])
    for L in [1, 2, 3, 5, 8, 10, 12, 15, 19, 20, 25, 29]:
        fighter.level = L
        c = eco.level_cost(fighter)
        w("| %d | %d | %d | %.0f |" % (L, c["Gold"], c["UpgradeMaterials"], eco.minion_power(fighter)))
    w("")
    w("### Mine upgrades (cost at depth 1; deeper Mines charge their own mineral)")
    w("")
    w("| Level | Dig speed: Gold / Copper | Output | Stockpile: Gold / Copper | Holds |")
    w("|---|---|---|---|---|")
    m = cfg["mine"]
    for k in range(0, 10):
        dig = eco.mine_upgrade_cost("digSpeed", k, 1)
        cap = eco.mine_upgrade_cost("stockpile", k, 1) if k < m["stockpile"]["maxLevel"] else None
        w("| %d → %d | %d / %d | +%d%% | %s | %s |" % (
            k, k + 1, dig["Gold"], dig["Copper"], round(100 * m["digSpeed"]["bonusPerLevel"] * (k + 1)),
            "%d / %d" % (cap["Gold"], cap["Copper"]) if cap else "max",
            ("%d h" % (m["stockpile"]["baseHours"] + m["stockpile"]["hoursPerLevel"] * (k + 1))) if cap else ""))
    w("")
    with open(path, "w") as f:
        f.write("\n".join(out))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", default=os.path.join(HERE, "economy.json"))
    ap.add_argument("--runs", type=int, default=200)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--days", type=int, default=10)
    ap.add_argument("--report", action="store_true")
    args = ap.parse_args()

    cfg = load_config(args.config)
    eco, results = simulate(cfg, args.runs, args.seed, args.days)
    for name, value in summary_lines(results):
        print("%-48s %s" % (name, value))
    for kind in ["engaged", "casual", "light"]:
        r = results[kind]
        print("%-8s T1 %-10s T2 %-10s T3 %-10s idle %d%%  full %d%%  losses %s  summons %s" % (
            kind, fmt_minutes(pct(r["wins"][1], 0.5)), fmt_minutes(pct(r["wins"][2], 0.5)),
            fmt_minutes(pct(r["wins"][3], 0.5)), round(100 * r["idle_share"]), round(100 * r["full_mine_share"]),
            r["losses_median"], r["summons_median"]))
    pt = results["playtest"]
    print("playtest T1 %d%%  T2 %d%%  (x%d, %d min)" % (100 * len(pt["wins"][1]) / pt["runs"], 100 * len(pt["wins"][2]) / pt["runs"], pt["scale"], pt["minutes"]))
    if args.report:
        path = os.path.join(REPO, "Docs", "Economy_Simulation_Report.md")
        write_report(cfg, eco, results, sample_log(eco, args.seed, args.days), path, args.runs, args.seed)
        print("Wrote", os.path.relpath(path, REPO))


if __name__ == "__main__":
    main()
