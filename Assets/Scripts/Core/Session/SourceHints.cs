using Deepshaft.Bosses;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Expeditions;

namespace Deepshaft.Session
{
    /// <summary>
    /// "Where do I get this?" for rare materials (SCRUM-33), following the player's progress:
    /// what to beat or clear first when the source isn't open yet, and that automated Expedition
    /// loot arrives when the Expedition is stopped. There's deliberately no "earned so far" for
    /// rare materials (team decision 2026-11-24).
    /// </summary>
    public static class SourceHints
    {
        /// <summary>
        /// A short hint for a rare Expedition material or a boss material, or null for anything else
        /// (Mine resources, shards, Essence), which the UI words itself.
        /// </summary>
        public static string ForRareMaterial(GameSession game, ResourceId id)
        {
            if (game == null) return null;
            ResourceSource source = ResourceSources.Find(game.Database, id);
            switch (source.Kind)
            {
                case ResourceSourceKind.AutomatedExpedition:
                    return ForExpedition(game, game.Database.FindExpedition(source.Id));
                case ResourceSourceKind.Boss:
                    BossDefinition boss = game.Database.FindBoss(source.Id);
                    // Only a boss's unique material (given on every win), not first-win extras like shards.
                    return boss != null && boss.repeatWinRewards != null && boss.repeatWinRewards.Exists(r => r.id == id && r.amount > 0)
                        ? ForBoss(game, boss) : null;
                default:
                    return null;
            }
        }

        static string ForExpedition(GameSession game, ExpeditionDefinition e)
        {
            if (e == null) return null;
            switch (game.Expeditions.GetStatus(e.id))
            {
                case ExpeditionStatus.Locked:
                    BossDefinition boss = game.Database.FindBoss(e.unlockedByBoss);
                    string bossName = boss != null ? boss.displayName : e.unlockedByBoss;
                    return "beat " + bossName + " to open " + e.displayName + ", clear it once, then automate it";
                case ExpeditionStatus.FirstClearAvailable:
                    return "first-clear " + e.displayName + ", then automate it";
                case ExpeditionStatus.ReadyToAutomate:
                    return "automate " + e.displayName + " (collected when you stop it)";
                default:
                    return e.displayName + " is farming it; stop the Expedition to collect";
            }
        }

        static string ForBoss(GameSession game, BossDefinition boss)
        {
            if (boss == null) return null;
            if (game.Tiers.HighestUnlockedTier < boss.tier) return "reach Tier " + boss.tier + " and beat " + boss.displayName;
            return game.Bosses.IsDefeated(boss.id)
                ? "beat " + boss.displayName + " again (every win gives one)"
                : "beat " + boss.displayName;
        }
    }
}
