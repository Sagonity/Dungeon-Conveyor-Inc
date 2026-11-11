using Deepshaft.Content;
using Deepshaft.Minions;
using Deepshaft.State;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    public class MinionTests
    {
        MinionDefinition miner;
        MinionDefinition fighter;
        GameDatabase database;

        [SetUp]
        public void SetUp()
        {
            miner = ScriptableObject.CreateInstance<MinionDefinition>();
            miner.EditorSetup("test_miner", MinionKind.Miner, MinionRole.Fighter, Rarity.Common);
            fighter = ScriptableObject.CreateInstance<MinionDefinition>();
            fighter.EditorSetup("test_fighter", MinionKind.Adventure, MinionRole.Fighter, Rarity.Rare);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(new[] { miner, fighter });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(miner);
            Object.DestroyImmediate(fighter);
            Object.DestroyImmediate(database);
        }

        [Test]
        public void Miner_HasNoRole_AndNoCombatStats()
        {
            Assert.AreEqual(MinionRole.None, miner.Role);
            Assert.AreEqual(0f, miner.GetHp(5));
            Assert.Greater(miner.GetMiningPower(5), 0f);
        }

        [Test]
        public void Adventurer_HasNoMiningPower()
        {
            Assert.AreEqual(0f, fighter.GetMiningPower(5));
            Assert.Greater(fighter.GetAtk(5), 0f);
        }

        [Test]
        public void Stats_GrowWithLevel()
        {
            Assert.Greater(fighter.GetHp(10), fighter.GetHp(1));
            Assert.Greater(miner.GetMiningPower(10), miner.GetMiningPower(1));
        }

        [Test]
        public void Stars_RaiseTheLevelCap()
        {
            Assert.Greater(fighter.GetMaxLevel(2), fighter.GetMaxLevel(0));
        }

        [Test]
        public void Database_FindsMinionsById()
        {
            Assert.AreSame(fighter, database.FindMinion("test_fighter"));
            Assert.IsNull(database.FindMinion("does_not_exist"));
            Assert.IsNull(database.FindMinion(null));
        }

        [Test]
        public void GameState_AddMinion_GivesUniqueIds()
        {
            GameState state = GameState.CreateNew(new ManualClock(System.DateTime.UtcNow));

            OwnedMinion a = state.AddMinion("test_miner");
            OwnedMinion b = state.AddMinion("test_fighter");

            Assert.AreNotEqual(a.instanceId, b.instanceId);
            Assert.AreEqual(1, a.level);
            Assert.AreSame(b, state.FindMinion(b.instanceId));
        }
    }
}
