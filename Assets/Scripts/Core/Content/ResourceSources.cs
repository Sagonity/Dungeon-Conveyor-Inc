using System.Collections.Generic;
using Deepshaft.Bosses;
using Deepshaft.Economy;
using Deepshaft.Expeditions;
using Deepshaft.Mining;

namespace Deepshaft.Content
{
    /// <summary>Where a resource comes from, so the Smith and Minion screens can say where to get what's missing (SCRUM-9).</summary>
    public enum ResourceSourceKind
    {
        Unknown,
        Mine,
        AutomatedExpedition,
        Boss,
    }

    public readonly struct ResourceSource
    {
        public ResourceSource(ResourceSourceKind kind, int tier, string id, string displayName)
        {
            Kind = kind;
            Tier = tier;
            Id = id ?? "";
            DisplayName = displayName ?? "";
        }

        public ResourceSourceKind Kind { get; }

        /// <summary>The mine tier, or the tier of the Expedition or boss.</summary>
        public int Tier { get; }

        /// <summary>Expedition or boss id; empty for the Mine.</summary>
        public string Id { get; }
        public string DisplayName { get; }
    }

    /// <summary>
    /// Finds the first (shallowest) place in the content that gives a resource: the Mine tier that
    /// first produces it, the Expedition that farms it when automated, or the boss that drops it.
    /// Shards and Essence come from Summoning and first clears; they report what the content shows.
    /// </summary>
    public static class ResourceSources
    {
        public static ResourceSource Find(GameDatabase database, ResourceId id)
        {
            if (database == null) return default;

            MiningTierRates bestTier = null;
            if (database.Mining.tiers != null)
            {
                foreach (MiningTierRates t in database.Mining.tiers)
                {
                    if (t == null || !Contains(t.perHour, id)) continue;
                    if (bestTier == null || t.tier < bestTier.tier) bestTier = t;
                }
            }
            if (bestTier != null) return new ResourceSource(ResourceSourceKind.Mine, bestTier.tier, "", "Mine");

            ExpeditionDefinition bestExpedition = null;
            foreach (ExpeditionDefinition e in database.Expeditions)
            {
                if (e == null || !Contains(e.automatedPerHour, id)) continue;
                if (bestExpedition == null || e.tier < bestExpedition.tier) bestExpedition = e;
            }
            if (bestExpedition != null)
            {
                return new ResourceSource(ResourceSourceKind.AutomatedExpedition, bestExpedition.tier,
                    bestExpedition.id, bestExpedition.displayName);
            }

            BossDefinition bestBoss = null;
            foreach (BossDefinition b in database.Bosses)
            {
                if (b == null || !(Contains(b.firstWinRewards, id) || Contains(b.repeatWinRewards, id))) continue;
                if (bestBoss == null || b.tier < bestBoss.tier) bestBoss = b;
            }
            if (bestBoss != null) return new ResourceSource(ResourceSourceKind.Boss, bestBoss.tier, bestBoss.id, bestBoss.displayName);

            return new ResourceSource(ResourceSourceKind.Unknown, 0, "", "");
        }

        static bool Contains(List<ResourceAmount> amounts, ResourceId id)
        {
            if (amounts == null) return false;
            foreach (ResourceAmount a in amounts) if (a.id == id && a.amount > 0) return true;
            return false;
        }
    }
}
