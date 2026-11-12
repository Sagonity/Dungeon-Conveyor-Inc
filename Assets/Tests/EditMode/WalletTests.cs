using System.Collections.Generic;
using Deepshaft.Economy;
using NUnit.Framework;

namespace Deepshaft.Tests
{
    public class WalletTests
    {
        static ResourceAmount R(ResourceId id, long amount) => new ResourceAmount(id, amount);

        [Test]
        public void NewWallet_IsEmpty()
        {
            var wallet = new Wallet(new WalletData());
            Assert.AreEqual(0, wallet.Get(ResourceId.Gold));
        }

        [Test]
        public void Add_IncreasesBalance_AndRaisesChanged()
        {
            var wallet = new Wallet(new WalletData());
            var changes = new List<(ResourceId, long)>();
            wallet.Changed += (id, balance) => changes.Add((id, balance));

            wallet.Add(ResourceId.Gold, 100);
            wallet.Add(ResourceId.Gold, 50);

            Assert.AreEqual(150, wallet.Get(ResourceId.Gold));
            CollectionAssert.AreEqual(new[] { (ResourceId.Gold, 100L), (ResourceId.Gold, 150L) }, changes);
        }

        [Test]
        public void Add_Negative_Throws()
        {
            var wallet = new Wallet(new WalletData());
            Assert.Throws<System.ArgumentOutOfRangeException>(() => wallet.Add(ResourceId.Gold, -1));
        }

        [Test]
        public void TrySpend_SpendsEverything_WhenAffordable()
        {
            var wallet = new Wallet(new WalletData());
            wallet.Add(ResourceId.Gold, 100);
            wallet.Add(ResourceId.Iron, 20);

            bool spent = wallet.TrySpend(new[] { R(ResourceId.Gold, 60), R(ResourceId.Iron, 20) });

            Assert.IsTrue(spent);
            Assert.AreEqual(40, wallet.Get(ResourceId.Gold));
            Assert.AreEqual(0, wallet.Get(ResourceId.Iron));
        }

        [Test]
        public void TrySpend_SpendsNothing_WhenAnyPartIsMissing()
        {
            var wallet = new Wallet(new WalletData());
            wallet.Add(ResourceId.Gold, 100);
            wallet.Add(ResourceId.Iron, 5);

            bool spent = wallet.TrySpend(new[] { R(ResourceId.Gold, 60), R(ResourceId.Iron, 20) });

            Assert.IsFalse(spent);
            Assert.AreEqual(100, wallet.Get(ResourceId.Gold));
            Assert.AreEqual(5, wallet.Get(ResourceId.Iron));
        }

        [Test]
        public void TrySpend_SumsRepeatedResources()
        {
            var wallet = new Wallet(new WalletData());
            wallet.Add(ResourceId.Gold, 100);

            Assert.IsFalse(wallet.TrySpend(new[] { R(ResourceId.Gold, 60), R(ResourceId.Gold, 60) }));
            Assert.AreEqual(100, wallet.Get(ResourceId.Gold));
        }

        [Test]
        public void GetShortfall_ListsOnlyWhatIsMissing()
        {
            var wallet = new Wallet(new WalletData());
            wallet.Add(ResourceId.Gold, 100);
            wallet.Add(ResourceId.Iron, 5);

            List<ResourceAmount> missing = wallet.GetShortfall(new[] { R(ResourceId.Gold, 60), R(ResourceId.Iron, 20) });

            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual(ResourceId.Iron, missing[0].id);
            Assert.AreEqual(15, missing[0].amount);
        }

        [Test]
        public void Wallet_WritesThroughToItsData()
        {
            var data = new WalletData();
            new Wallet(data).Add(ResourceId.BasicShard, 7);

            Assert.AreEqual(7, new Wallet(data).Get(ResourceId.BasicShard));
        }
    }
}
