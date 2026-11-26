using System.Collections.Generic;
using Deepshaft.Bosses;
using Deepshaft.Combat;
using Deepshaft.Economy;
using Deepshaft.Expeditions;
using Deepshaft.Progression;

namespace Deepshaft.Content
{
    /// <summary>
    /// The prototype's bosses, Expeditions and tier requirements (SCRUM-7): 3 tiers, one boss
    /// and one Expedition each (Game Design Summary v3.4 §9). These are the GameDatabase defaults;
    /// tune them on the asset. Names are placeholders (SCRUM-13); numbers are a first pass until
    /// SCRUM-2 / SCRUM-19.
    ///
    /// Stats follow one formula so the recommended power means the same thing at every tier:
    /// - Boss: HP = 11 × recommended power, ATK ≈ 0.32 × recommended, attacks every 2 s,
    ///   every 4th attack hits the whole team. Fights take about 25–50 s.
    /// - Expedition: 4 enemies, each HP = 3 × recommended, ATK = 0.105 × recommended, every 1.6 s.
    /// A level-1 Adventure Minion has 20 power and gains 2 per level, so 3 Minions start at 60
    /// (66 with a Support).
    /// </summary>
    public static class DefaultAdventureContent
    {
        public const string Tier1Boss = "spore_foreman";
        public const string Tier2Boss = "slag_golem";
        public const string Tier3Boss = "prism_wyrm";

        public const string Tier1Expedition = "spore_caverns";
        public const string Tier2Expedition = "magma_works";
        public const string Tier3Expedition = "crystal_depths";

        public static List<BossDefinition> Bosses() => new List<BossDefinition>
        {
            // Display names from SCRUM-37 (were Spore Foreman, Slag Golem, Prism Wyrm); the ids stay.
            Boss(Tier1Boss, "Vein Warden", 1, 80, 26, ResourceId.ForemanSigil, 20, "Art/Bosses/vein_warden_sheet", firstWinGold: 200),
            Boss(Tier2Boss, "The Rooted Elder", 2, 135, 43, ResourceId.GolemHeart, 30, "Art/Bosses/rooted_elder_sheet"),
            Boss(Tier3Boss, "Crystal Burrower", 3, 240, 77, ResourceId.WyrmScale, 40, "Art/Bosses/crystal_burrower_sheet"),
        };

        public static List<ExpeditionDefinition> Expeditions() => new List<ExpeditionDefinition>
        {
            Expedition(Tier1Expedition, "Spore Caverns", 1, Tier1Boss, 85, "Spore Crawler", 10.5f,
                new[] { R(ResourceId.Gold, 500), R(ResourceId.BasicShard, 10), R(ResourceId.SporeSilk, 5) },
                new[] { R(ResourceId.SporeSilk, 2) }),
            Expedition(Tier2Expedition, "Magma Works", 2, Tier2Boss, 175, "Magma Imp", 18f,
                new[] { R(ResourceId.Gold, 1200), R(ResourceId.BasicShard, 15), R(ResourceId.EmberCore, 5) },
                new[] { R(ResourceId.EmberCore, 2), R(ResourceId.SporeSilk, 1) }),
            Expedition(Tier3Expedition, "Crystal Depths", 3, Tier3Boss, 300, "Crystal Wisp", 31.5f,
                new[] { R(ResourceId.Gold, 2500), R(ResourceId.BasicShard, 20), R(ResourceId.PrismDust, 5) },
                new[] { R(ResourceId.PrismDust, 2), R(ResourceId.EmberCore, 1) }),
        };

        /// <summary>
        /// Placeholder until SCRUM-2 decides (v3.4 §8): the next tier unlocks when the current
        /// tier's boss is beaten. Add first clears or a cost on the asset to make it stricter.
        /// </summary>
        public static List<TierRequirement> TierRequirements() => new List<TierRequirement>
        {
            new TierRequirement { tier = 2, requiredBosses = new List<string> { Tier1Boss } },
            new TierRequirement { tier = 3, requiredBosses = new List<string> { Tier2Boss } },
        };

        static BossDefinition Boss(string id, string name, int tier, float recommended, float atk,
            ResourceId material, long shards, string spriteSheet, long firstWinGold = 0) => new BossDefinition
        {
            id = id,
            displayName = name,
            tier = tier,
            recommendedPower = recommended,
            enemy = new EnemyDefinition(name, 11f * recommended, atk, 2f, 4),
            // Each boss's first win also gives one Advanced summon (team decision 2026-11-19, SCRUM-11).
            // The Tier 1 boss's first win also gives Gold, so the first session keeps going (SCRUM-32).
            firstWinRewards = FirstWin(material, shards, firstWinGold),
            repeatWinRewards = new List<ResourceAmount> { R(material, 1) },
            spriteSheet = spriteSheet,
        };

        static List<ResourceAmount> FirstWin(ResourceId material, long shards, long gold)
        {
            var rewards = new List<ResourceAmount> { R(material, 1), R(ResourceId.BasicShard, shards), R(ResourceId.AdvancedShard, 10) };
            if (gold > 0) rewards.Add(R(ResourceId.Gold, gold));
            return rewards;
        }

        static ExpeditionDefinition Expedition(string id, string name, int tier, string boss, float recommended,
            string enemyName, float enemyAtk, ResourceAmount[] firstClear, ResourceAmount[] perHour)
        {
            var enemies = new List<EnemyDefinition>();
            for (int i = 0; i < 4; i++) enemies.Add(new EnemyDefinition(enemyName, 3f * recommended, enemyAtk, 1.6f));
            return new ExpeditionDefinition
            {
                id = id,
                displayName = name,
                tier = tier,
                unlockedByBoss = boss,
                recommendedPower = recommended,
                enemies = enemies,
                firstClearRewards = new List<ResourceAmount>(firstClear),
                automatedPerHour = new List<ResourceAmount>(perHour),
            };
        }

        static ResourceAmount R(ResourceId id, long amount) => new ResourceAmount(id, amount);
    }
}
