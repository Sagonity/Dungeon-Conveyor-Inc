using System;
using System.Collections.Generic;
using Deepshaft.Economy;
using UnityEngine;

namespace Deepshaft.Minions
{
    /// <summary>
    /// One resource in the cost of a standard level-up:
    /// amount = <see cref="atLevelOne"/> × <see cref="growth"/>^(current level − 1), rounded.
    /// </summary>
    [Serializable]
    public class LevelCostTerm
    {
        public ResourceId resource = ResourceId.Gold;
        [Tooltip("Cost of going from level 1 to 2.")]
        [Min(0)] public float atLevelOne = 25f;
        [Tooltip("Each level costs this much more than the one before. 1.12 = +12%.")]
        [Min(1)] public float growth = 1.12f;

        public LevelCostTerm() { }

        public LevelCostTerm(ResourceId resource, float atLevelOne, float growth)
        {
            this.resource = resource;
            this.atLevelOne = atLevelOne;
            this.growth = growth;
        }

        public long GetAmount(int currentLevel) =>
            (long)Math.Round(atLevelOne * Math.Pow(growth, Math.Max(1, currentLevel) - 1));
    }

    /// <summary>The cost of one Minion breakthrough.</summary>
    [Serializable]
    public class BreakthroughCost
    {
        public List<ResourceAmount> cost = new List<ResourceAmount>();

        public BreakthroughCost() { }

        public BreakthroughCost(params ResourceAmount[] cost) => this.cost = new List<ResourceAmount>(cost);
    }

    /// <summary>
    /// Minion development numbers (Game Design Summary v3.4 §5; Scope §4.4), on the GameDatabase
    /// asset so they can be tuned in the Inspector. First pass until SCRUM-2 / SCRUM-19.
    ///
    /// - <b>Standard levels</b> cost Gold + Iron and grow by a fixed factor per level. The same table
    ///   is used for Miners and Adventure Minions.
    /// - <b>Breakthroughs</b> need the Minion at its level cap and cost a rare Expedition material:
    ///   the 1st uses Tier 1's, the 2nd Tier 2's, the 3rd Tier 3's (team decision 2026-11-19).
    ///   Each one raises the cap (<see cref="MinionDefinition.GetMaxLevel"/>). The number of rows
    ///   is the number of breakthroughs a Minion can have.
    /// - Ascension (Minion Essence) is not in the prototype (team decision 2026-11-19).
    /// </summary>
    [Serializable]
    public class MinionProgressionRules
    {
        public List<LevelCostTerm> levelCost = new List<LevelCostTerm>();
        public List<BreakthroughCost> breakthroughs = new List<BreakthroughCost>();

        public static MinionProgressionRules CreateDefault() => new MinionProgressionRules
        {
            levelCost = new List<LevelCostTerm>
            {
                new LevelCostTerm(ResourceId.Gold, 25f, 1.12f),
                new LevelCostTerm(ResourceId.Iron, 10f, 1.12f),
            },
            breakthroughs = new List<BreakthroughCost>
            {
                new BreakthroughCost(R(ResourceId.Gold, 1000), R(ResourceId.Iron, 400), R(ResourceId.SporeSilk, 5)),
                new BreakthroughCost(R(ResourceId.Gold, 3000), R(ResourceId.Iron, 1200), R(ResourceId.EmberCore, 5)),
                new BreakthroughCost(R(ResourceId.Gold, 8000), R(ResourceId.Iron, 3000), R(ResourceId.PrismDust, 5)),
            },
        };

        public int MaxBreakthroughs => breakthroughs?.Count ?? 0;

        /// <summary>Cost of going from <paramref name="currentLevel"/> to the next level.</summary>
        public List<ResourceAmount> GetLevelUpCost(int currentLevel)
        {
            var cost = new List<ResourceAmount>();
            if (levelCost == null) return cost;
            foreach (LevelCostTerm term in levelCost)
            {
                if (term == null) continue;
                long amount = term.GetAmount(currentLevel);
                if (amount > 0) cost.Add(new ResourceAmount(term.resource, amount));
            }
            return cost;
        }

        /// <summary>
        /// Cost of the breakthrough after <paramref name="breakthroughsDone"/>, or null if there are no more.
        /// </summary>
        public List<ResourceAmount> GetBreakthroughCost(int breakthroughsDone)
        {
            if (breakthroughs == null || breakthroughsDone < 0 || breakthroughsDone >= breakthroughs.Count) return null;
            BreakthroughCost row = breakthroughs[breakthroughsDone];
            return row?.cost != null ? new List<ResourceAmount>(row.cost) : new List<ResourceAmount>();
        }

        static ResourceAmount R(ResourceId id, long amount) => new ResourceAmount(id, amount);
    }
}
