using System;
using System.Collections.Generic;
using Deepshaft.Combat;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Mining;
using Deepshaft.Progression;
using Deepshaft.Roster;
using Deepshaft.State;
using Deepshaft.Time;

namespace Deepshaft.Expeditions
{
    /// <summary>
    /// Expeditions (Game Design Summary v3.4 §4, Scope §4.3, SCRUM-7).
    ///
    /// - An Expedition unlocks when its boss is beaten (<see cref="ExpeditionDefinition.unlockedByBoss"/>).
    /// - <b>First clear:</b> an autobattle with the Adventure team that can fail. A win gives a
    ///   one-time bonus and unlocks automation for that Expedition for good.
    /// - <b>Automated:</b> can't fail. Earns its <see cref="ExpeditionDefinition.automatedPerHour"/>
    ///   from the clock, so it keeps going while the app is closed. No time cap, no diminishing returns.
    /// - Only one runs at a time. The player can stop it any time and collect what it earned.
    /// - While it runs the Adventure team is busy (<see cref="ITeamBusySource"/>): no team changes,
    ///   boss fights or first clears until it's stopped. Miners keep mining.
    /// </summary>
    public class ExpeditionService : ITeamBusySource
    {
        readonly GameState state;
        readonly GameDatabase database;
        readonly IClock clock;
        readonly TierProgressionService tiers;

        /// <param name="tiers">Optional: lets a first clear unlock a tier when the tier rules require it.</param>
        public ExpeditionService(GameState state, GameDatabase database, IClock clock,
            TierProgressionService tiers = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.tiers = tiers;
            state.EnsureValid();
            RosterLogic.EnsureInitialized(state, database);

            // Content removed after the save was made: don't leave the team busy forever.
            if (!string.IsNullOrEmpty(Data.activeId) && database.FindExpedition(Data.activeId) == null)
            {
                UnityEngine.Debug.LogWarning("Running Expedition '" + Data.activeId +
                                             "' no longer exists; it was stopped.");
                Data.activeId = "";
            }
        }

        ExpeditionData Data => state.expeditions;
        long NowMs => UnixTime.ToUnixMs(clock.UtcNow);

        /// <summary>Raised after an Expedition is cleared, started or stopped.</summary>
        public event Action Changed;

        /// <summary>True while an automated Expedition runs.</summary>
        public bool IsAdventureTeamBusy => IsRunning;

        public bool IsRunning => !string.IsNullOrEmpty(Data.activeId);

        /// <summary>The running Expedition, or null.</summary>
        public ExpeditionDefinition Active => IsRunning ? database.FindExpedition(Data.activeId) : null;

        public bool IsUnlocked(string id)
        {
            ExpeditionDefinition e = database.FindExpedition(id);
            return e != null && (string.IsNullOrEmpty(e.unlockedByBoss) || state.bosses.defeated.Contains(e.unlockedByBoss));
        }

        public bool IsCleared(string id) => !string.IsNullOrEmpty(id) && Data.cleared.Contains(id);

        public ExpeditionStatus GetStatus(string id)
        {
            if (!IsUnlocked(id)) return ExpeditionStatus.Locked;
            if (Data.activeId == id) return ExpeditionStatus.Running;
            return IsCleared(id) ? ExpeditionStatus.ReadyToAutomate : ExpeditionStatus.FirstClearAvailable;
        }

        /// <summary>Unlocked Expeditions in database order.</summary>
        public List<ExpeditionDefinition> GetUnlocked()
        {
            var result = new List<ExpeditionDefinition>();
            foreach (ExpeditionDefinition e in database.Expeditions)
            {
                if (e != null && IsUnlocked(e.id)) result.Add(e);
            }
            return result;
        }

        /// <summary>The team's power, to show next to the recommended power.</summary>
        public float GetTeamPower() => AdventureTeam.GetPower(state, database);

        // ---------- First clear ----------

        public ExpeditionCheck CanFirstClear(string id)
        {
            if (database.FindExpedition(id) == null) return ExpeditionCheck.Unknown;
            if (!IsUnlocked(id)) return ExpeditionCheck.Locked;
            if (IsCleared(id)) return ExpeditionCheck.AlreadyCleared;
            if (IsRunning) return ExpeditionCheck.TeamBusy;
            if (AdventureTeam.Build(state, database).Count == 0) return ExpeditionCheck.NoTeam;
            return ExpeditionCheck.Ok;
        }

        /// <summary>
        /// Fights the first clear. On a win: the one-time bonus goes into <paramref name="wallet"/>
        /// and automation is unlocked for good. On a loss nothing changes; try again any time.
        /// </summary>
        public FirstClearResult TryFirstClear(string id, Wallet wallet, Random rng)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var rewards = new List<ResourceAmount>();
            var unlockedTiers = new List<int>();

            ExpeditionCheck check = CanFirstClear(id);
            if (check != ExpeditionCheck.Ok) return new FirstClearResult(check, null, rewards, unlockedTiers);

            ExpeditionDefinition expedition = database.FindExpedition(id);
            var enemies = new List<Combatant>();
            if (expedition.enemies != null)
            {
                foreach (EnemyDefinition e in expedition.enemies) if (e != null) enemies.Add(Combatant.ForEnemy(e));
            }
            BattleResult battle = CombatSimulator.Simulate(database.Combat, AdventureTeam.Build(state, database),
                enemies, expedition.recommendedPower, rng);

            if (battle.Won)
            {
                Data.cleared.Add(id);
                if (expedition.firstClearRewards != null)
                {
                    foreach (ResourceAmount r in expedition.firstClearRewards)
                    {
                        if (r.amount <= 0) continue;
                        wallet.Add(r.id, r.amount);
                        rewards.Add(r);
                    }
                }
                if (tiers != null) unlockedTiers = tiers.TryAutoUnlock();
                Changed?.Invoke();
            }
            return new FirstClearResult(check, battle, rewards, unlockedTiers);
        }

        // ---------- Automation ----------

        public ExpeditionCheck CanStart(string id)
        {
            if (database.FindExpedition(id) == null) return ExpeditionCheck.Unknown;
            if (!IsUnlocked(id)) return ExpeditionCheck.Locked;
            if (!IsCleared(id)) return ExpeditionCheck.NotCleared;
            if (Data.activeId == id) return ExpeditionCheck.AlreadyRunning;
            if (IsRunning) return ExpeditionCheck.AnotherRunning;
            if (AdventureTeam.Build(state, database).Count == 0) return ExpeditionCheck.NoTeam;
            return ExpeditionCheck.Ok;
        }

        /// <summary>Starts an automated Expedition. The Adventure team is busy until it's stopped.</summary>
        public ExpeditionCheck Start(string id)
        {
            ExpeditionCheck check = CanStart(id);
            if (check != ExpeditionCheck.Ok) return check;
            long now = NowMs;
            Data.activeId = id;
            Data.startedAtUnixMs = now;
            Data.lastBankedAtUnixMs = now;
            Changed?.Invoke();
            return ExpeditionCheck.Ok;
        }

        /// <summary>Earned per hour by the running Expedition (empty if none).</summary>
        public List<MinedAmount> GetHourlyRates()
        {
            ExpeditionDefinition active = Active;
            return active != null ? MiningMath.ToRates(active.automatedPerHour) : new List<MinedAmount>();
        }

        /// <summary>Earned and not collected yet, up to now (includes fractions from earlier runs). Doesn't change the save.</summary>
        public List<MinedAmount> GetPending()
        {
            var result = new List<MinedAmount>(Data.pending);
            if (IsRunning)
            {
                MiningMath.AddProduction(result, GetHourlyRates(),
                    MiningMath.HoursBetween(Data.lastBankedAtUnixMs, NowMs));
            }
            return result;
        }

        public double GetPending(ResourceId id) => MiningMath.Get(GetPending(), id);

        /// <summary>How long the running Expedition has been out. 0 if none (or the clock went backwards).</summary>
        public double HoursRunning => IsRunning ? MiningMath.HoursBetween(Data.startedAtUnixMs, NowMs) : 0.0;

        /// <summary>
        /// Moves what was earned since the last bank into the saved pending pile. Rates never change
        /// mid-run, so this is only needed before stopping; it's public for the save timing (SCRUM-17).
        /// If the clock went backwards, nothing is earned until it passes the last bank again.
        /// </summary>
        public void Bank()
        {
            if (!IsRunning) return;
            long now = NowMs;
            if (now <= Data.lastBankedAtUnixMs) return;
            MiningMath.AddProduction(Data.pending, GetHourlyRates(), MiningMath.HoursBetween(Data.lastBankedAtUnixMs, now));
            Data.lastBankedAtUnixMs = now;
        }

        /// <summary>
        /// Stops the running Expedition and moves every whole unit it earned into <paramref name="wallet"/>.
        /// The Adventure team is free again.
        /// </summary>
        public ExpeditionCollectResult StopAndCollect(Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (!IsRunning)
            {
                return new ExpeditionCollectResult(ExpeditionCheck.NotRunning, "", 0.0, new List<ResourceAmount>());
            }

            string id = Data.activeId;
            double hours = HoursRunning;
            Bank();
            List<ResourceAmount> collected = MiningMath.CollectWholeUnits(Data.pending, wallet);
            Data.activeId = "";
            Data.startedAtUnixMs = 0;
            Data.lastBankedAtUnixMs = 0;
            Changed?.Invoke();
            return new ExpeditionCollectResult(ExpeditionCheck.Ok, id, hours, collected);
        }
    }
}
