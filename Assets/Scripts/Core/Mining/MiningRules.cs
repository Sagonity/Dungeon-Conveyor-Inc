using System;
using System.Collections.Generic;
using Deepshaft.Economy;
using Deepshaft.Minions;
using UnityEngine;

namespace Deepshaft.Mining
{
    /// <summary>Base output of one mine tier, per hour, for one reference Miner.</summary>
    [Serializable]
    public class MiningTierRates
    {
        [Min(1)] public int tier = 1;
        public List<ResourceAmount> perHour = new List<ResourceAmount>();

        public MiningTierRates() { }

        public MiningTierRates(int tier, params ResourceAmount[] perHour)
        {
            this.tier = tier;
            this.perHour = new List<ResourceAmount>(perHour);
        }
    }

    /// <summary>
    /// Mining numbers (Game Design Summary v3.4 §2). Lives on the GameDatabase asset so
    /// it can be tuned in the Inspector. The defaults are the v3.4 illustrative table;
    /// final numbers come from SCRUM-2.
    ///
    /// Output per hour = Σ over working Miners of
    ///   tier rate × (mining power ÷ <see cref="referenceMiningPower"/>) × (1 + tool bonus)
    ///   × (1 + trait bonus for that resource).
    /// So one level-1 Common Miner (10 MP, no trait, no tool) mines exactly the table.
    /// </summary>
    [Serializable]
    public class MiningRules
    {
        [Tooltip("Mining power that produces exactly the tier table. A level-1 Common Miner has 10.")]
        [Min(0.01f)] public float referenceMiningPower = 10f;

        [Header("Miner traits (fixed per named Miner)")]
        [Tooltip("Extra Gold for Gold-trait Miners. 0.25 = +25%.")]
        [Min(0)] public float goldTraitBonus = 0.25f;
        [Tooltip("Extra ore (everything except Gold) for Ore-trait Miners.")]
        [Min(0)] public float oreTraitBonus = 0.25f;
        [Tooltip("Extra output of every resource for Speed-trait Miners.")]
        [Min(0)] public float speedTraitBonus = 0.15f;

        [Tooltip("Miners always mine the highest unlocked tier. A tier with no row uses the deepest row below it.")]
        public List<MiningTierRates> tiers = new List<MiningTierRates>();

        /// <summary>The v3.4 starting numbers. Tiers 4–6 are post-MVP but listed so the table is complete.</summary>
        public static MiningRules CreateDefault() => new MiningRules
        {
            tiers = new List<MiningTierRates>
            {
                Tier(1, 100, 40, 0, 0),
                Tier(2, 150, 65, 0, 0),
                Tier(3, 220, 90, 10, 0),
                Tier(4, 320, 125, 25, 0),
                Tier(5, 450, 170, 45, 5),
                Tier(6, 600, 230, 75, 15),
            },
        };

        /// <summary>
        /// The rates of the deepest configured tier at or below
        /// <paramref name="highestUnlockedTier"/>. Null if no tier fits.
        /// </summary>
        public MiningTierRates GetTierRates(int highestUnlockedTier)
        {
            MiningTierRates best = null;
            if (tiers == null) return null;
            foreach (MiningTierRates t in tiers)
            {
                if (t == null || t.tier > highestUnlockedTier) continue;
                if (best == null || t.tier > best.tier) best = t;
            }
            return best;
        }

        /// <summary>Multiplier for one resource from a Miner's fixed trait.</summary>
        public double GetTraitMultiplier(MinerTrait trait, ResourceId resource)
        {
            switch (trait)
            {
                case MinerTrait.Gold: return resource == ResourceId.Gold ? 1.0 + goldTraitBonus : 1.0;
                case MinerTrait.Ore: return resource != ResourceId.Gold ? 1.0 + oreTraitBonus : 1.0;
                case MinerTrait.Speed: return 1.0 + speedTraitBonus;
                default: return 1.0;
            }
        }

        static MiningTierRates Tier(int tier, long gold, long iron, long silver, long mithril)
        {
            var rates = new MiningTierRates { tier = tier };
            rates.perHour.Add(new ResourceAmount(ResourceId.Gold, gold));
            rates.perHour.Add(new ResourceAmount(ResourceId.Iron, iron));
            if (silver > 0) rates.perHour.Add(new ResourceAmount(ResourceId.Silver, silver));
            if (mithril > 0) rates.perHour.Add(new ResourceAmount(ResourceId.Mithril, mithril));
            return rates;
        }
    }
}
