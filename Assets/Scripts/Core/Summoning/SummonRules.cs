using System;
using System.Collections.Generic;
using Deepshaft.Economy;
using Deepshaft.Minions;
using UnityEngine;

namespace Deepshaft.Summoning
{
    /// <summary>The shard a summon is paid with (Game Design Summary v3.4 §6). Stored in no save; safe to extend.</summary>
    public enum ShardType
    {
        Basic = 0,
        Advanced = 1,
    }

    /// <summary>What one shard type costs per summon and its rarity rates, in percent.</summary>
    [Serializable]
    public class ShardRates
    {
        public ShardType shard;
        [Tooltip("The wallet resource paid for a summon.")]
        public ResourceId currency = ResourceId.BasicShard;
        [Min(1)] public long costPerSummon = 10;

        [Tooltip("Chance in percent. The three don't need to add up to 100; they're used as weights.")]
        [Min(0)] public float common = 75f;
        [Min(0)] public float uncommon = 23f;
        [Min(0)] public float rare = 2f;

        public ShardRates() { }

        public ShardRates(ShardType shard, ResourceId currency, long costPerSummon, float common, float uncommon, float rare)
        {
            this.shard = shard;
            this.currency = currency;
            this.costPerSummon = costPerSummon;
            this.common = common;
            this.uncommon = uncommon;
            this.rare = rare;
        }

        public float GetWeight(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return Math.Max(0f, common);
                case Rarity.Uncommon: return Math.Max(0f, uncommon);
                case Rarity.Rare: return Math.Max(0f, rare);
                default: return 0f;
            }
        }

        public ResourceAmount Cost => new ResourceAmount(currency, costPerSummon);
    }

    /// <summary>
    /// Summoning numbers (SCRUM-11; Game Design Summary v3.4 §6), on the GameDatabase asset.
    /// Proposed rates from the design until SCRUM-2: Basic 75 / 23 / 2, Advanced 0 / 85 / 15, 10 shards a summon.
    /// Duplicates give Minion Essence by rarity (team decision 2026-11-19): 5 / 15 / 50.
    /// No pity in the prototype.
    /// </summary>
    [Serializable]
    public class SummonRules
    {
        public List<ShardRates> shards = new List<ShardRates>();

        [Header("Minion Essence per duplicate")]
        [Min(0)] public long essenceCommon = 5;
        [Min(0)] public long essenceUncommon = 15;
        [Min(0)] public long essenceRare = 50;

        public static SummonRules CreateDefault() => new SummonRules
        {
            shards = new List<ShardRates>
            {
                new ShardRates(ShardType.Basic, ResourceId.BasicShard, 10, 75f, 23f, 2f),
                new ShardRates(ShardType.Advanced, ResourceId.AdvancedShard, 10, 0f, 85f, 15f),
            },
        };

        /// <summary>The row for a shard type, or null if the content has none.</summary>
        public ShardRates Get(ShardType shard)
        {
            if (shards == null) return null;
            foreach (ShardRates r in shards) if (r != null && r.shard == shard) return r;
            return null;
        }

        public long GetEssence(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return essenceUncommon;
                case Rarity.Rare: return essenceRare;
                default: return essenceCommon;
            }
        }
    }
}
