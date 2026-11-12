using System;
using System.Collections.Generic;
using Deepshaft.Economy;
using Deepshaft.Minions;
using Deepshaft.Time;

namespace Deepshaft.State
{
    /// <summary>
    /// Everything that is saved: the whole player progress as plain data.
    /// Systems (Mining, Expeditions, ...) read and change it; <see cref="Save.SaveService"/>
    /// writes it to disk as JSON.
    ///
    /// Rules for changing this class:
    /// - Only [Serializable] types, Lists and primitives (JsonUtility: no dictionaries, no DateTime).
    /// - Times are Unix milliseconds in UTC (<see cref="UnixTime"/>).
    /// - Adding a field is safe: old saves load with the default value.
    /// - Renaming or changing the meaning of a field needs a version bump and a migration
    ///   in <see cref="Save.SaveMigrations"/>.
    /// </summary>
    [Serializable]
    public class GameState
    {
        /// <summary>Save format version. Bump it when a migration is needed.</summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// 0 means "not a save" (e.g. empty JSON). Set by <see cref="CreateNew"/> and on every save.
        /// </summary>
        public int version;
        public long createdAtUnixMs;
        public long lastSavedAtUnixMs;

        public WalletData wallet = new WalletData();

        public List<OwnedMinion> minions = new List<OwnedMinion>();
        public int nextMinionInstanceId = 1;

        /// <summary>Highest tier the player can play (1 at start; a boss win raises it).</summary>
        public int highestUnlockedTier = 1;

        public static GameState CreateNew(IClock clock)
        {
            long now = UnixTime.ToUnixMs(clock.UtcNow);
            return new GameState
            {
                version = CurrentVersion,
                createdAtUnixMs = now,
                lastSavedAtUnixMs = now,
            };
        }

        /// <summary>Adds a new level-1 Minion and returns it.</summary>
        public OwnedMinion AddMinion(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId)) throw new ArgumentException("Definition id is required.", nameof(definitionId));
            var minion = new OwnedMinion
            {
                instanceId = nextMinionInstanceId++,
                definitionId = definitionId,
                level = 1,
                breakthroughs = 0,
            };
            minions.Add(minion);
            return minion;
        }

        public OwnedMinion FindMinion(int instanceId)
        {
            foreach (OwnedMinion m in minions)
            {
                if (m.instanceId == instanceId) return m;
            }
            return null;
        }

        /// <summary>
        /// Fixes up data that JsonUtility may leave null (e.g. fields missing from older saves).
        /// Called after loading.
        /// </summary>
        public void EnsureValid()
        {
            if (wallet == null) wallet = new WalletData();
            if (wallet.balances == null) wallet.balances = new List<ResourceAmount>();
            if (minions == null) minions = new List<OwnedMinion>();
            if (highestUnlockedTier < 1) highestUnlockedTier = 1;

            int maxId = 0;
            foreach (OwnedMinion m in minions)
            {
                if (m.level < 1) m.level = 1;
                if (m.breakthroughs < 0) m.breakthroughs = 0;
                if (m.instanceId > maxId) maxId = m.instanceId;
            }
            if (nextMinionInstanceId <= maxId) nextMinionInstanceId = maxId + 1;
        }
    }
}
