using System;
using System.IO;
using System.Text.RegularExpressions;
using Deepshaft.Economy;
using Deepshaft.Save;
using Deepshaft.State;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deepshaft.Tests
{
    public class SaveServiceTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        ManualClock clock;
        MemorySaveStorage storage;
        SaveService service;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock(Start);
            storage = new MemorySaveStorage();
            service = new SaveService(storage, clock);
        }

        static GameState MakeProgress(IClock clock)
        {
            GameState state = GameState.CreateNew(clock);
            new Wallet(state.wallet).Add(ResourceId.Gold, 1234);
            state.AddMinion("spore_bruiser").level = 7;
            state.highestUnlockedTier = 2;
            return state;
        }

        [Test]
        public void NoSave_StartsNewGame()
        {
            LoadResult result = service.Load();

            Assert.AreEqual(LoadStatus.NewGame, result.Status);
            Assert.AreEqual(GameState.CurrentVersion, result.State.version);
            Assert.AreEqual(UnixTime.ToUnixMs(Start), result.State.createdAtUnixMs);
        }

        [Test]
        public void SaveThenLoad_KeepsEverything()
        {
            service.Save(MakeProgress(clock));

            LoadResult result = new SaveService(storage, clock).Load();

            Assert.AreEqual(LoadStatus.Loaded, result.Status);
            Assert.AreEqual(1234, new Wallet(result.State.wallet).Get(ResourceId.Gold));
            Assert.AreEqual(1, result.State.minions.Count);
            Assert.AreEqual("spore_bruiser", result.State.minions[0].definitionId);
            Assert.AreEqual(7, result.State.minions[0].level);
            Assert.AreEqual(2, result.State.highestUnlockedTier);
            Assert.AreEqual(2, result.State.nextMinionInstanceId);
        }

        [Test]
        public void Save_RecordsTheTime()
        {
            GameState state = GameState.CreateNew(clock);
            clock.Advance(TimeSpan.FromHours(3));

            service.Save(state);

            Assert.AreEqual(UnixTime.ToUnixMs(Start.AddHours(3)), state.lastSavedAtUnixMs);
        }

        [Test]
        public void DamagedSave_FallsBackToBackup()
        {
            service.Save(MakeProgress(clock));   // becomes the backup
            service.Save(MakeProgress(clock));   // latest
            storage.Primary = "{ this is not json";

            LogAssert.Expect(LogType.Warning, new Regex("Could not parse save"));
            LogAssert.Expect(LogType.Warning, new Regex("loaded the backup"));
            LoadResult result = service.Load();

            Assert.AreEqual(LoadStatus.LoadedFromBackup, result.Status);
            Assert.AreEqual(1234, new Wallet(result.State.wallet).Get(ResourceId.Gold));
        }

        [Test]
        public void EmptyJsonObject_IsNotASave()
        {
            storage.Primary = "{}";

            LogAssert.Expect(LogType.Error, new Regex("both damaged"));
            LoadResult result = service.Load();

            Assert.AreEqual(LoadStatus.Corrupt, result.Status);
        }

        [Test]
        public void SaveAndBackupDamaged_StartsNewGame()
        {
            storage.Primary = "garbage";
            storage.Backup = "";

            LogAssert.Expect(LogType.Warning, new Regex("Could not parse save"));
            LogAssert.Expect(LogType.Error, new Regex("both damaged"));
            LoadResult result = service.Load();

            Assert.AreEqual(LoadStatus.Corrupt, result.Status);
            Assert.IsNotNull(result.State);
            Assert.IsTrue(service.SavingAllowed);
        }

        [Test]
        public void SaveFromNewerVersion_IsNeverOverwritten()
        {
            GameState future = MakeProgress(clock);
            future.version = GameState.CurrentVersion + 1;
            storage.Primary = JsonUtility.ToJson(future);
            string original = storage.Primary;

            LogAssert.Expect(LogType.Error, new Regex("newer than this build"));
            LoadResult result = service.Load();
            LogAssert.Expect(LogType.Warning, new Regex("Not saving"));
            bool saved = service.Save(result.State);

            Assert.AreEqual(LoadStatus.NewerVersion, result.Status);
            Assert.IsFalse(saved);
            Assert.AreEqual(original, storage.Primary);
        }

        [Test]
        public void MissingFields_AreRepairedOnLoad()
        {
            // Simulates an old save written before these fields existed.
            storage.Primary = "{\"version\":1,\"nextMinionInstanceId\":0,\"minions\":[{\"instanceId\":5,\"definitionId\":\"x\",\"level\":0}]}";

            LoadResult result = service.Load();

            Assert.AreEqual(LoadStatus.Loaded, result.Status);
            Assert.IsNotNull(result.State.wallet.balances);
            Assert.AreEqual(1, result.State.minions[0].level);
            Assert.AreEqual(1, result.State.highestUnlockedTier);
            Assert.AreEqual(6, result.State.nextMinionInstanceId);
        }

        [Test]
        public void FileStorage_KeepsPreviousSaveAsBackup()
        {
            string dir = Path.Combine(Path.GetTempPath(), "deepshaft-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var files = new FileSaveStorage(dir);
                files.Write("first");
                files.Write("second");

                Assert.IsTrue(files.TryReadPrimary(out string primary));
                Assert.IsTrue(files.TryReadBackup(out string backup));
                Assert.AreEqual("second", primary);
                Assert.AreEqual("first", backup);

                files.DeleteAll();
                Assert.IsFalse(files.TryReadPrimary(out _));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
