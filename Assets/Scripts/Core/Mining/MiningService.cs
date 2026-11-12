using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Minions;
using Deepshaft.State;
using Deepshaft.Time;

namespace Deepshaft.Mining
{
    /// <summary>
    /// Automatic mining (Game Design Summary v3.4 §2, Scope §4.1).
    ///
    /// - Every owned Miner Minion mines the highest unlocked tier. There's no mine choice.
    /// - Output is worked out from the clock, so it keeps running in menus, during an
    ///   Expedition and while the app is closed. Nothing here reads UnityEngine.Time.
    /// - No stockpile cap: output adds up for as long as the player is away.
    /// - Anything that changes the rate (a new Miner, a level-up, a tool, a new tier) must go
    ///   through <see cref="ChangeRate"/> (or a helper that uses it), so what was earned at the
    ///   old rate is banked first.
    ///
    /// Which Miners work: all owned Miners for now. Assignment and the number of slots
    /// are SCRUM-6; when that lands, change <see cref="GetWorkingMiners"/>.
    /// </summary>
    public class MiningService
    {
        readonly GameState state;
        readonly GameDatabase database;
        readonly IClock clock;
        readonly IMinerToolBonus tools;

        public MiningService(GameState state, GameDatabase database, IClock clock, IMinerToolBonus tools = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.tools = tools ?? NoMinerTools.Instance;

            if (state.mining == null) state.mining = new MiningData();
            if (state.mining.pending == null) state.mining.pending = new List<MinedAmount>();
            // A save from before mining existed: start counting now, don't pay for the past.
            if (state.mining.lastBankedAtUnixMs <= 0) state.mining.lastBankedAtUnixMs = NowMs;
        }

        MiningData Data => state.mining;
        MiningRules Rules => database.Mining;
        long NowMs => UnixTime.ToUnixMs(clock.UtcNow);

        /// <summary>Raised after <see cref="Collect"/> moved resources into the wallet.</summary>
        public event Action<IReadOnlyList<ResourceAmount>> Collected;

        /// <summary>The tier the Miners are working: always the highest unlocked one.</summary>
        public int CurrentTier => Math.Max(1, state.highestUnlockedTier);

        /// <summary>Current output per hour of every working Miner together.</summary>
        public List<MinedAmount> GetHourlyRates()
        {
            var inputs = new List<MinerInput>();
            foreach (OwnedMinion miner in GetWorkingMiners())
            {
                MinionDefinition definition = database.FindMinion(miner.definitionId);
                inputs.Add(new MinerInput(definition.GetMiningPower(miner.level), definition.MinerTrait,
                    tools.GetToolBonus(miner)));
            }
            return MiningMath.GetHourlyRates(Rules, CurrentTier, inputs);
        }

        /// <summary>Owned Minions that are Miners and whose definition still exists.</summary>
        public List<OwnedMinion> GetWorkingMiners()
        {
            var result = new List<OwnedMinion>();
            foreach (OwnedMinion m in state.minions)
            {
                MinionDefinition definition = database.FindMinion(m.definitionId);
                if (definition != null && definition.Kind == MinionKind.Miner) result.Add(m);
            }
            return result;
        }

        /// <summary>Everything mined and not collected yet, up to now. Doesn't change the save.</summary>
        public List<MinedAmount> GetPending()
        {
            var result = new List<MinedAmount>(Data.pending);
            MiningMath.AddProduction(result, GetHourlyRates(), MiningMath.HoursBetween(Data.lastBankedAtUnixMs, NowMs));
            return result;
        }

        public double GetPending(ResourceId id) => MiningMath.Get(GetPending(), id);

        /// <summary>
        /// Moves output earned since the last bank into the saved pending pile, at the current rate.
        /// If the clock went backwards, nothing is earned until it passes the last bank again.
        /// </summary>
        public void Bank()
        {
            long now = NowMs;
            if (now <= Data.lastBankedAtUnixMs) return;
            MiningMath.AddProduction(Data.pending, GetHourlyRates(), MiningMath.HoursBetween(Data.lastBankedAtUnixMs, now));
            Data.lastBankedAtUnixMs = now;
        }

        /// <summary>
        /// Banks what was earned at the old rate, then runs <paramref name="change"/>
        /// (e.g. a Miner level-up or a Smith tool upgrade). Output from now on uses the new rate.
        /// </summary>
        public void ChangeRate(Action change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            Bank();
            change();
        }

        /// <summary>Adds a new Minion to the roster; if it's a Miner it starts mining now.</summary>
        public OwnedMinion AddMinion(string definitionId)
        {
            OwnedMinion added = null;
            ChangeRate(() => added = state.AddMinion(definitionId));
            return added;
        }

        /// <summary>Raises the highest unlocked tier. Miners move there at once. Never lowers it.</summary>
        public void UnlockTier(int tier)
        {
            if (tier <= state.highestUnlockedTier) return;
            ChangeRate(() => state.highestUnlockedTier = tier);
        }

        /// <summary>
        /// Banks, then moves every whole unit mined so far into <paramref name="wallet"/>.
        /// Fractions stay in the Mine for next time. Returns what was added.
        /// </summary>
        public List<ResourceAmount> Collect(Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            Bank();

            var collected = new List<ResourceAmount>();
            for (int i = 0; i < Data.pending.Count; i++)
            {
                MinedAmount p = Data.pending[i];
                // The epsilon stops 119.99999999999999 (float error in 1.2 × 100) from losing a unit.
                long whole = (long)Math.Floor(p.amount + 1e-6);
                if (whole <= 0) continue;
                wallet.Add(p.id, whole);
                collected.Add(new ResourceAmount(p.id, whole));
                Data.pending[i] = new MinedAmount(p.id, Math.Max(0.0, p.amount - whole));
            }

            if (collected.Count > 0) Collected?.Invoke(collected);
            return collected;
        }
    }
}
