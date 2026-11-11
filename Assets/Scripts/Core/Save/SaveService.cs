using System;
using Deepshaft.State;
using Deepshaft.Time;
using UnityEngine;

namespace Deepshaft.Save
{
    public enum LoadStatus
    {
        /// <summary>No save existed; a new game was started.</summary>
        NewGame,
        /// <summary>The save loaded normally.</summary>
        Loaded,
        /// <summary>The latest save was damaged; the backup was loaded instead.</summary>
        LoadedFromBackup,
        /// <summary>Both the save and the backup were damaged; a new game was started.</summary>
        Corrupt,
        /// <summary>
        /// The save is from a newer version of the game. A new game was started in memory,
        /// and saving is blocked so the newer save isn't overwritten.
        /// </summary>
        NewerVersion,
    }

    public readonly struct LoadResult
    {
        public LoadResult(GameState state, LoadStatus status)
        {
            State = state;
            Status = status;
        }

        public GameState State { get; }
        public LoadStatus Status { get; }
    }

    /// <summary>
    /// Turns <see cref="GameState"/> into JSON and back, with a backup and version checks.
    /// </summary>
    public class SaveService
    {
        readonly ISaveStorage storage;
        readonly IClock clock;

        public SaveService(ISaveStorage storage, IClock clock)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>False after loading a save from a newer game version.</summary>
        public bool SavingAllowed { get; private set; } = true;

        public LoadResult Load()
        {
            bool hasPrimary = storage.TryReadPrimary(out string primaryText);
            bool hasBackup = storage.TryReadBackup(out string backupText);

            if (!hasPrimary && !hasBackup)
            {
                return new LoadResult(GameState.CreateNew(clock), LoadStatus.NewGame);
            }

            GameState primary = hasPrimary ? Parse(primaryText) : null;
            if (primary != null) return Finish(primary, LoadStatus.Loaded);

            GameState backup = hasBackup ? Parse(backupText) : null;
            if (backup != null)
            {
                Debug.LogWarning("Save was damaged or missing; loaded the backup.");
                return Finish(backup, LoadStatus.LoadedFromBackup);
            }

            Debug.LogError("Save and backup are both damaged; starting a new game.");
            return new LoadResult(GameState.CreateNew(clock), LoadStatus.Corrupt);
        }

        /// <summary>Writes the state. Returns false if saving is blocked.</summary>
        public bool Save(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!SavingAllowed)
            {
                Debug.LogWarning("Not saving: the save on this device is from a newer version of the game.");
                return false;
            }

            state.version = GameState.CurrentVersion;
            state.lastSavedAtUnixMs = UnixTime.ToUnixMs(clock.UtcNow);
            storage.Write(JsonUtility.ToJson(state));
            return true;
        }

        LoadResult Finish(GameState state, LoadStatus status)
        {
            if (state.version > GameState.CurrentVersion)
            {
                SavingAllowed = false;
                Debug.LogError("Save version " + state.version + " is newer than this build (" +
                               GameState.CurrentVersion + "). Saving is disabled.");
                return new LoadResult(GameState.CreateNew(clock), LoadStatus.NewerVersion);
            }

            if (state.version < GameState.CurrentVersion) SaveMigrations.Migrate(state);
            state.EnsureValid();
            return new LoadResult(state, status);
        }

        static GameState Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                GameState state = JsonUtility.FromJson<GameState>(text);
                // Valid JSON that isn't a save (e.g. "{}") has no version.
                return state != null && state.version > 0 ? state : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not parse save: " + e.Message);
                return null;
            }
        }
    }
}
