using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Minions;
using Deepshaft.Roster;
using Deepshaft.State;

namespace Deepshaft.Summoning
{
    public enum SummonStatus
    {
        /// <summary>Can summon now (or, after <see cref="SummonService.TrySummon"/>, did).</summary>
        Ok,
        /// <summary>The content has no rates for this shard type.</summary>
        UnknownShard,
        /// <summary>The pool has no Minion that this shard can give.</summary>
        EmptyPool,
        CantAfford,
    }

    /// <summary>The outcome of a summon, or why it can't be done.</summary>
    public sealed class SummonResult
    {
        public SummonResult(SummonStatus status, ResourceAmount cost, IReadOnlyList<ResourceAmount> shortfall = null,
            MinionDefinition definition = null, OwnedMinion added = null, long essence = 0)
        {
            Status = status;
            Cost = cost;
            Shortfall = shortfall ?? Array.Empty<ResourceAmount>();
            Definition = definition;
            Added = added;
            Essence = essence;
        }

        public SummonStatus Status { get; }
        public bool Ok => Status == SummonStatus.Ok;
        public ResourceAmount Cost { get; }
        public IReadOnlyList<ResourceAmount> Shortfall { get; }

        /// <summary>The Minion that came out. Null if the summon didn't happen.</summary>
        public MinionDefinition Definition { get; }

        /// <summary>The new owned Minion, or null for a duplicate.</summary>
        public OwnedMinion Added { get; }
        public bool IsDuplicate => Ok && Added == null;

        /// <summary>Minion Essence given for a duplicate. 0 for a new Minion.</summary>
        public long Essence { get; }
    }

    /// <summary>
    /// Summoning (SCRUM-11; Game Design Summary v3.4 §6, Scope §4.6).
    ///
    /// - Paid with shards only, never Gold or ores. The player picks the Miner or the Adventure pool
    ///   and a Basic or Advanced shard.
    /// - A rarity is rolled with the shard's rates, then a Minion of that rarity from the pool, each
    ///   equally likely. If the pool has no Minion of some rarity, that rarity is left out and the
    ///   others keep their proportions; <see cref="GetRates"/> shows exactly these chances.
    /// - The player owns one of each Minion: a duplicate becomes Minion Essence (by rarity).
    /// - A named Miner's trait comes from its definition, so it is always the same.
    /// - New Minions go through <see cref="RosterService.AddMinion"/>: they fill a free slot.
    /// - No pity in the prototype.
    /// </summary>
    public class SummonService
    {
        readonly GameState state;
        readonly GameDatabase database;
        readonly RosterService roster;

        public SummonService(GameState state, GameDatabase database, RosterService roster)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
        }

        /// <summary>Raised after a summon. Arg: the result.</summary>
        public event Action<SummonResult> Summoned;

        SummonRules Rules => database.Summoning;

        /// <summary>Every Minion in a pool, sorted by id so a seeded summon always gives the same Minion.</summary>
        public List<MinionDefinition> GetPool(MinionKind kind)
        {
            var pool = new List<MinionDefinition>();
            foreach (MinionDefinition d in database.Minions)
            {
                if (d != null && !string.IsNullOrEmpty(d.Id) && d.Kind == kind) pool.Add(d);
            }
            pool.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return pool;
        }

        public bool Owns(string definitionId)
        {
            foreach (OwnedMinion m in state.minions) if (m.definitionId == definitionId) return true;
            return false;
        }

        /// <summary>
        /// The real chance of each rarity for this pool and shard, as fractions that add up to 1
        /// (or all 0 if nothing can come out). Shown in the game (store compliance, SCRUM-25).
        /// </summary>
        public Dictionary<Rarity, double> GetRates(MinionKind kind, ShardType shard)
        {
            var rates = new Dictionary<Rarity, double>();
            ShardRates row = Rules.Get(shard);
            List<MinionDefinition> pool = GetPool(kind);
            double total = 0;
            foreach (Rarity r in AllRarities)
            {
                double w = row != null && HasRarity(pool, r) ? row.GetWeight(r) : 0.0;
                rates[r] = w;
                total += w;
            }
            foreach (Rarity r in AllRarities) rates[r] = total > 0 ? rates[r] / total : 0.0;
            return rates;
        }

        public SummonResult Check(MinionKind kind, ShardType shard, Wallet wallet = null)
        {
            ShardRates row = Rules.Get(shard);
            if (row == null) return new SummonResult(SummonStatus.UnknownShard, default);
            if (!CanGiveAnything(kind, shard)) return new SummonResult(SummonStatus.EmptyPool, row.Cost);
            if (wallet != null)
            {
                List<ResourceAmount> shortfall = wallet.GetShortfall(new[] { row.Cost });
                if (shortfall.Count > 0) return new SummonResult(SummonStatus.CantAfford, row.Cost, shortfall);
            }
            return new SummonResult(SummonStatus.Ok, row.Cost);
        }

        /// <summary>Pays the shards and summons one Minion, or does nothing and says why.</summary>
        public SummonResult TrySummon(MinionKind kind, ShardType shard, Wallet wallet, Random rng)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            SummonResult check = Check(kind, shard, wallet);
            if (!check.Ok || !wallet.TrySpend(new[] { check.Cost })) return check;

            MinionDefinition picked = Roll(kind, shard, rng);
            SummonResult result;
            if (Owns(picked.Id))
            {
                long essence = Rules.GetEssence(picked.Rarity);
                if (essence > 0) wallet.Add(ResourceId.MinionEssence, essence);
                result = new SummonResult(SummonStatus.Ok, check.Cost, null, picked, null, essence);
            }
            else
            {
                OwnedMinion added = roster.AddMinion(picked.Id);
                result = new SummonResult(SummonStatus.Ok, check.Cost, null, picked, added);
            }
            Summoned?.Invoke(result);
            return result;
        }

        MinionDefinition Roll(MinionKind kind, ShardType shard, Random rng)
        {
            Dictionary<Rarity, double> rates = GetRates(kind, shard);
            double roll = rng.NextDouble();
            Rarity rarity = Rarity.Common;
            double sum = 0;
            bool found = false;
            foreach (Rarity r in AllRarities)
            {
                if (rates[r] <= 0) continue;
                sum += rates[r];
                rarity = r;
                if (roll < sum) { found = true; break; }
            }
            if (!found) rarity = LastWithChance(rates); // rounding: the roll landed past the last bucket

            var candidates = GetPool(kind).FindAll(d => d.Rarity == rarity);
            return candidates[rng.Next(candidates.Count)];
        }

        bool CanGiveAnything(MinionKind kind, ShardType shard)
        {
            foreach (KeyValuePair<Rarity, double> r in GetRates(kind, shard)) if (r.Value > 0) return true;
            return false;
        }

        static Rarity LastWithChance(Dictionary<Rarity, double> rates)
        {
            Rarity last = Rarity.Common;
            foreach (Rarity r in AllRarities) if (rates[r] > 0) last = r;
            return last;
        }

        static bool HasRarity(List<MinionDefinition> pool, Rarity rarity)
        {
            foreach (MinionDefinition d in pool) if (d.Rarity == rarity) return true;
            return false;
        }

        static readonly Rarity[] AllRarities = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
    }
}
