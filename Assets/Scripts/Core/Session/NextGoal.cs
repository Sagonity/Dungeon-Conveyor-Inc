using System.Collections.Generic;
using Deepshaft.Bosses;
using Deepshaft.Content;
using Deepshaft.Equipment;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.State;
using Deepshaft.Summoning;

namespace Deepshaft.Session
{
    /// <summary>What the next goal asks the player to do.</summary>
    public enum GoalKind
    {
        /// <summary>Fight the next boss: the team is strong enough.</summary>
        BeatBoss,
        /// <summary>First-clear the next Expedition: the team is strong enough.</summary>
        ClearExpedition,
        /// <summary>The next fight is too hard yet: level up the team or upgrade its gear.</summary>
        GetStronger,
        /// <summary>The next fight is too hard and there are shards: summon Adventure Minions.</summary>
        Summon,
        /// <summary>The next fight is too hard and nothing is affordable: collect the Mine.</summary>
        CollectMine,
        /// <summary>The next fight is too hard and only the running Expedition has something waiting: stop it to collect.</summary>
        CollectExpedition,
        /// <summary>The next fight is too hard and nothing is affordable or waiting: let the Mine and Expedition work.</summary>
        Wait,
        /// <summary>A cleared Expedition isn't being farmed: automate it.</summary>
        Automate,
        /// <summary>The team is on an Expedition, but the next fight is within reach: stop it to fight.</summary>
        StopExpedition,
        /// <summary>A benched Adventure Minion is stronger than one in the team.</summary>
        SwapIn,
        /// <summary>Every boss and Expedition is done.</summary>
        AllDone,
    }

    /// <summary>Where the goal is done; the UI maps it to a tab.</summary>
    public enum GoalPlace { None, Boss, Expeditions, Minions, Smith, Mine }

    /// <summary>
    /// The single next thing to do (SCRUM-36, playtest finding 2: testers didn't know where to start or
    /// what to do next). Follows the prototype's path: Tier 1 boss → its Expedition's first clear →
    /// automate it → Tier 2 boss → … and, when the next fight is too hard, says how to get stronger.
    ///
    /// Pure: reads the session and changes nothing, so the UI can call it every frame.
    /// </summary>
    public sealed class NextGoal
    {
        /// <summary>
        /// A fight is "within reach" from this share of the recommended power: fights have some luck,
        /// and testers won Tier 1 at 66 vs 80. Below it the goal is to get stronger first.
        /// </summary>
        public const float WithinReach = 0.9f;

        /// <summary>
        /// Below this share of the recommended power the UI asks "Fight anyway?" (SCRUM-36, finding 3:
        /// testers fought the Tier 2 boss at 66 vs 135 straight after the Tier 1 win).
        /// </summary>
        public const float FarBelow = 0.75f;

        NextGoal(GoalKind kind, GoalPlace place, string title, string detail, string targetId = "",
            float power = 0f, float recommended = 0f, int swapOutId = 0, string shortTitle = null)
        {
            ShortTitle = shortTitle ?? title;
            Kind = kind;
            Place = place;
            Title = title;
            Detail = detail;
            TargetId = targetId;
            Power = power;
            Recommended = recommended;
            SwapOutId = swapOutId;
        }

        public GoalKind Kind { get; }
        public GoalPlace Place { get; }

        /// <summary>Short, for the header: "Beat Vein Warden".</summary>
        public string Title { get; }

        /// <summary>Even shorter, for the header pill: the fight itself ("Clear Spore Caverns") while getting stronger for it.</summary>
        public string ShortTitle { get; }

        /// <summary>One sentence: why, and how.</summary>
        public string Detail { get; }

        /// <summary>The boss or Expedition id of the next fight, or the incoming Minion's instance id for <see cref="GoalKind.SwapIn"/>.</summary>
        public string TargetId { get; }

        /// <summary>The team's power and the next fight's recommended power (0 when there's no fight).</summary>
        public float Power { get; }
        public float Recommended { get; }

        /// <summary>For <see cref="GoalKind.SwapIn"/>: the team member to swap out.</summary>
        public int SwapOutId { get; }

        public static bool IsFarBelow(float power, float recommended) => recommended > 0f && power < recommended * FarBelow;

        public static NextGoal For(GameSession game)
        {
            float power = game.Bosses.GetTeamPower();

            // Free power first: a benched Minion that's stronger than a team member.
            NextGoal swap = BestSwap(game);
            if (swap != null) return swap;

            if (!NextFight(game, out BossDefinition boss, out ExpeditionDefinition expedition))
            {
                return new NextGoal(GoalKind.AllDone, GoalPlace.Minions, "Every tier cleared",
                    "Keep your Expedition farming and break through to make your Minions even stronger.");
            }

            string name = boss != null ? boss.displayName : expedition.displayName;
            string id = boss != null ? boss.id : expedition.id;
            float recommended = boss != null ? boss.recommendedPower : expedition.recommendedPower;
            string fight = boss != null ? "Beat " + name : "Clear " + name;
            string versus = "Team power " + Num(power) + " / " + Num(recommended) + ".";
            // Lost this fight already? Then it's only in reach again once the team got stronger than it was.
            bool lostAtThisPower = game.LastLossId == id && power <= game.LastLossPower + 0.5f;
            bool inReach = power >= recommended * WithinReach && !lostAtThisPower;
            ExpeditionDefinition running = game.Expeditions.Active;

            if (inReach)
            {
                if (running != null)
                {
                    return new NextGoal(GoalKind.StopExpedition, GoalPlace.Expeditions, "Stop " + running.displayName + " to fight",
                        versus + " Your team is strong enough for " + name + ", but it's on the Expedition. Stop it to collect and fight.",
                        id, power, recommended);
                }
                return boss != null
                    ? new NextGoal(GoalKind.BeatBoss, GoalPlace.Boss, fight, versus + " Your team is ready: fight " + name + " on the Boss tab.", id, power, recommended)
                    : new NextGoal(GoalKind.ClearExpedition, GoalPlace.Expeditions, fight,
                        versus + " Your team is ready: first-clear it on the Expeditions tab. After that you can automate it.", id, power, recommended);
            }

            // Too hard for now. Farming a cleared Expedition is always worth it while getting stronger…
            ExpeditionDefinition idle = DeepestClearedIdle(game);
            if (running == null && idle != null)
            {
                return new NextGoal(GoalKind.Automate, GoalPlace.Expeditions, "Automate " + idle.displayName,
                    "It farms rare materials for breakthroughs, even while the game is closed. Stop it any time to fight.",
                    idle.id, power, recommended);
            }

            // …then spend what you have (new Minions, levels and gear, breakthroughs, the Smith), then collect, then wait.
            if (CanSummon(game))
            {
                return new NextGoal(GoalKind.Summon, GoalPlace.Minions, fight + ": summon Minions",
                    versus + " You have shards: summon on the Minions tab. A new Minion only helps once it's in the team.",
                    id, power, recommended, shortTitle: fight);
            }
            if (CanAffordTeamUpgrade(game))
            {
                return new NextGoal(GoalKind.GetStronger, GoalPlace.Minions, fight + ": get stronger",
                    versus + " Level up your team and upgrade their gear. Max buys several levels in one tap.", id, power, recommended,
                    shortTitle: fight);
            }
            if (CanBreakThroughTeam(game))
            {
                return new NextGoal(GoalKind.GetStronger, GoalPlace.Minions, fight + ": break through",
                    versus + " A team Minion is at its level cap: tap it on the Minions tab and break through to level it further.",
                    id, power, recommended, shortTitle: fight);
            }
            if (CanImproveAtSmith(game))
            {
                return new NextGoal(GoalKind.GetStronger, GoalPlace.Smith, fight + ": visit the Smith",
                    versus + " The Smith can learn a new rank or break through your team's gear.", id, power, recommended, shortTitle: fight);
            }
            if (HasMineWaiting(game))
            {
                return new NextGoal(GoalKind.CollectMine, GoalPlace.Mine, fight + ": collect the Mine",
                    versus + " Collect the Mine, then level up your team.", id, power, recommended, shortTitle: fight);
            }
            if (running != null && HasExpeditionWaiting(game))
            {
                return new NextGoal(GoalKind.CollectExpedition, GoalPlace.Expeditions, "Collect " + running.displayName,
                    versus + " Its materials only reach you when you stop it. Stop & collect, then start it again.",
                    running.id, power, recommended, shortTitle: fight);
            }
            return new NextGoal(GoalKind.Wait, GoalPlace.Mine, fight + ": come back later",
                versus + " Your Miners " + (running != null ? "and the Expedition " : "") +
                "keep working while you're away. Come back to collect and upgrade.", id, power, recommended, shortTitle: fight);
        }

        /// <summary>The next undone step in order: Tier 1 boss, Tier 1 Expedition, Tier 2 boss, …</summary>
        static bool NextFight(GameSession game, out BossDefinition boss, out ExpeditionDefinition expedition)
        {
            boss = null;
            expedition = null;
            GameDatabase db = game.Database;
            for (int tier = 1; tier <= 99; tier++)
            {
                BossDefinition b = db.FindBossForTier(tier);
                var tierExpeditions = new List<ExpeditionDefinition>();
                foreach (ExpeditionDefinition e in db.Expeditions) if (e != null && e.tier == tier) tierExpeditions.Add(e);
                if (b == null && tierExpeditions.Count == 0) return false;

                if (b != null && !game.Bosses.IsDefeated(b.id))
                {
                    boss = b;
                    return true;
                }
                foreach (ExpeditionDefinition e in tierExpeditions)
                {
                    if (game.Expeditions.IsCleared(e.id) || !game.Expeditions.IsUnlocked(e.id)) continue;
                    expedition = e;
                    return true;
                }
            }
            return false;
        }

        static ExpeditionDefinition DeepestClearedIdle(GameSession game)
        {
            ExpeditionDefinition best = null;
            foreach (ExpeditionDefinition e in game.Database.Expeditions)
            {
                if (e == null || !game.Expeditions.IsCleared(e.id)) continue;
                if (best == null || e.tier > best.tier) best = e;
            }
            return best;
        }

        /// <summary>The strongest benched Adventurer that beats the weakest team member, while the team can change.</summary>
        static NextGoal BestSwap(GameSession game)
        {
            if (game.Roster.IsTeamBusy) return null;
            List<OwnedMinion> team = game.Roster.GetTeam();
            List<OwnedMinion> bench = game.Roster.GetUnassigned(MinionKind.Adventure);
            if (bench.Count == 0) return null;

            OwnedMinion best = null;
            float bestPower = 0f;
            foreach (OwnedMinion m in bench)
            {
                float p = MemberPower(game, m);
                if (best == null || p > bestPower) { best = m; bestPower = p; }
            }

            string name = DisplayName(game, best);
            if (team.Count < game.Roster.AdventureSlots)
            {
                return new NextGoal(GoalKind.SwapIn, GoalPlace.Minions, "Put " + name + " in the team",
                    "The team has a free slot. Minions on the bench don't fight.", best.instanceId.ToString());
            }

            OwnedMinion weakest = null;
            float weakestPower = 0f;
            foreach (OwnedMinion m in team)
            {
                float p = MemberPower(game, m);
                if (weakest == null || p < weakestPower) { weakest = m; weakestPower = p; }
            }
            if (weakest == null || bestPower <= weakestPower) return null;
            return new NextGoal(GoalKind.SwapIn, GoalPlace.Minions, "Swap " + name + " into the team",
                name + " (" + Num(bestPower) + " power) is on the bench and stronger than " + DisplayName(game, weakest) +
                " (" + Num(weakestPower) + "). Minions on the bench don't fight.",
                best.instanceId.ToString(), swapOutId: weakest.instanceId);
        }

        /// <summary>One Adventure Minion's power, as the team power counts it.</summary>
        public static float MemberPower(GameSession game, OwnedMinion m) =>
            game.Database.Combat.GetPower(MinionStatCalculator.GetHp(game.Database, m), MinionStatCalculator.GetAtk(game.Database, m));

        public static bool CanAffordTeamUpgrade(GameSession game)
        {
            foreach (OwnedMinion m in game.Roster.GetTeam())
            {
                if (game.Minions.CheckLevelUp(m.instanceId, game.Wallet).CanDo) return true;
                if (game.Smith.CheckUpgrade(m.instanceId, EquipmentSlot.Weapon, game.Wallet).CanDo) return true;
                if (game.Smith.CheckUpgrade(m.instanceId, EquipmentSlot.Armor, game.Wallet).CanDo) return true;
            }
            return false;
        }

        static bool CanSummon(GameSession game) =>
            game.Summoning.Check(MinionKind.Adventure, ShardType.Basic, game.Wallet).Ok ||
            game.Summoning.Check(MinionKind.Adventure, ShardType.Advanced, game.Wallet).Ok;

        static bool CanBreakThroughTeam(GameSession game)
        {
            foreach (OwnedMinion m in game.Roster.GetTeam())
            {
                if (game.Minions.CheckBreakthrough(m.instanceId, game.Wallet).CanDo) return true;
            }
            return false;
        }

        static bool CanImproveAtSmith(GameSession game)
        {
            if (game.Smith.CheckRankUp(game.Wallet).CanDo) return true;
            foreach (OwnedMinion m in game.Roster.GetTeam())
            {
                if (game.Smith.CheckBreakthrough(m.instanceId, EquipmentSlot.Weapon, game.Wallet).CanDo) return true;
                if (game.Smith.CheckBreakthrough(m.instanceId, EquipmentSlot.Armor, game.Wallet).CanDo) return true;
            }
            return false;
        }

        static bool HasExpeditionWaiting(GameSession game)
        {
            foreach (MinedAmount a in game.Expeditions.GetPending()) if (a.amount >= 1.0) return true;
            return false;
        }

        static bool HasMineWaiting(GameSession game)
        {
            foreach (MinedAmount a in game.Mining.GetPending()) if (a.amount >= 1.0) return true;
            return false;
        }

        static string DisplayName(GameSession game, OwnedMinion m) =>
            game.Database.FindMinion(m.definitionId)?.DisplayName ?? m.definitionId;

        static string Num(float value) => System.Math.Floor(value).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
