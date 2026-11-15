using System;
using System.Collections.Generic;
using Deepshaft.Economy;

namespace Deepshaft.Mining
{
    /// <summary>
    /// The production formulas, with no state and no clock, so they're easy to test.
    /// <see cref="MiningService"/> feeds them from the save.
    /// </summary>
    public static class MiningMath
    {
        public const double MsPerHour = 3_600_000.0;

        /// <summary>
        /// Total output per hour of all <paramref name="miners"/> at the given tier.
        /// One entry per resource the tier produces (even if the amount is 0).
        /// </summary>
        public static List<MinedAmount> GetHourlyRates(MiningRules rules, int highestUnlockedTier,
            IEnumerable<MinerInput> miners)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (miners == null) throw new ArgumentNullException(nameof(miners));

            var result = new List<MinedAmount>();
            MiningTierRates tier = rules.GetTierRates(highestUnlockedTier);
            if (tier == null || tier.perHour == null) return result;

            foreach (ResourceAmount baseRate in tier.perHour) Add(result, baseRate.id, 0);

            double reference = rules.referenceMiningPower > 0 ? rules.referenceMiningPower : 1.0;
            foreach (MinerInput miner in miners)
            {
                if (miner.MiningPower <= 0) continue;
                double strength = miner.MiningPower / reference * (1.0 + Math.Max(0f, miner.ToolBonus));
                foreach (ResourceAmount baseRate in tier.perHour)
                {
                    if (baseRate.amount <= 0) continue;
                    double amount = baseRate.amount * strength * rules.GetTraitMultiplier(miner.Trait, baseRate.id);
                    Add(result, baseRate.id, amount);
                }
            }
            return result;
        }

        /// <summary>
        /// Hours from <paramref name="fromUnixMs"/> to <paramref name="toUnixMs"/>.
        /// 0 if the clock went backwards. No upper limit: there is no stockpile cap.
        /// </summary>
        public static double HoursBetween(long fromUnixMs, long toUnixMs) =>
            toUnixMs > fromUnixMs ? (toUnixMs - fromUnixMs) / MsPerHour : 0.0;

        /// <summary>Adds <paramref name="ratesPerHour"/> × <paramref name="hours"/> to <paramref name="into"/>.</summary>
        public static void AddProduction(List<MinedAmount> into, IEnumerable<MinedAmount> ratesPerHour, double hours)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (ratesPerHour == null) throw new ArgumentNullException(nameof(ratesPerHour));
            if (hours <= 0) return;
            foreach (MinedAmount rate in ratesPerHour)
            {
                if (rate.amount > 0) Add(into, rate.id, rate.amount * hours);
            }
        }

        public static double Get(IEnumerable<MinedAmount> amounts, ResourceId id)
        {
            double sum = 0;
            foreach (MinedAmount a in amounts)
            {
                if (a.id == id) sum += a.amount;
            }
            return sum;
        }

        /// <summary>
        /// Moves every whole unit in <paramref name="pending"/> into <paramref name="wallet"/> and
        /// leaves the fractions. Returns what was added. Shared by the Mine and automated Expeditions.
        /// </summary>
        public static List<ResourceAmount> CollectWholeUnits(List<MinedAmount> pending, Wallet wallet)
        {
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));

            var collected = new List<ResourceAmount>();
            for (int i = 0; i < pending.Count; i++)
            {
                MinedAmount p = pending[i];
                // The epsilon stops 119.99999999999999 (float error in 1.2 × 100) from losing a unit.
                long whole = (long)Math.Floor(p.amount + 1e-6);
                if (whole <= 0) continue;
                wallet.Add(p.id, whole);
                collected.Add(new ResourceAmount(p.id, whole));
                pending[i] = new MinedAmount(p.id, Math.Max(0.0, p.amount - whole));
            }
            return collected;
        }

        /// <summary>Turns whole-unit rates (e.g. an Expedition's per-hour list) into <see cref="MinedAmount"/>s.</summary>
        public static List<MinedAmount> ToRates(IEnumerable<ResourceAmount> perHour)
        {
            var rates = new List<MinedAmount>();
            if (perHour == null) return rates;
            foreach (ResourceAmount r in perHour)
            {
                if (r.amount > 0) Add(rates, r.id, r.amount);
            }
            return rates;
        }

        internal static void Add(List<MinedAmount> list, ResourceId id, double amount)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id != id) continue;
                list[i] = new MinedAmount(id, list[i].amount + amount);
                return;
            }
            list.Add(new MinedAmount(id, amount));
        }
    }
}
