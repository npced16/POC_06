using System;
using System.Collections.Generic;

using NUnit.Framework;

using IronRing.Inventory;

namespace IronRing.Inventory.Tests
{
    public sealed class ResourceInventoryTests
    {
        /// <summary>Creates stock from a fixture dictionary and verifies spending, adding and change notifications.</summary>
        [Test]
        public void StockTransfersAndNotificationsAreExact()
        {
            var inventory = new ResourceInventory(new Dictionary<string, int> { { "coal", 6 } });
            var events = new List<int>();
            inventory.Changed += (id, amount) => events.Add(amount);
            Assert.That(inventory.TrySpend("coal", 1), Is.True);
            inventory.Add("coal", 1);
            Assert.That(inventory.Get("coal"), Is.EqualTo(6));
            Assert.That(events, Is.EqualTo(new[] { 5, 6 }));
        }

        /// <summary>Uses insufficient and unknown stock requests and verifies no mutation or notification occurs.</summary>
        [Test]
        public void RejectedSpendDoesNotChangeStock()
        {
            var inventory = new ResourceInventory(new Dictionary<string, int> { { "coal", 1 } });
            int events = 0;
            inventory.Changed += (id, amount) => events++;
            Assert.That(inventory.TrySpend("coal", 2), Is.False);
            Assert.That(inventory.TrySpend("missing", 1), Is.False);
            Assert.That(inventory.Get("missing"), Is.Zero);
            Assert.That(inventory.Get("coal"), Is.EqualTo(1));
            Assert.That(events, Is.Zero);
        }

        /// <summary>Mutates the constructor input and later inventory; verifies earlier snapshots remain independent.</summary>
        [Test]
        public void SnapshotAndSeedAreDefensiveCopies()
        {
            var seed = new Dictionary<string, int> { { "coal", 6 } };
            var inventory = new ResourceInventory(seed);
            var snapshot = inventory.Snapshot();
            seed["coal"] = 0;
            inventory.TrySpend("coal", 1);
            Assert.That(snapshot["coal"], Is.EqualTo(6));
            Assert.That(inventory.Get("coal"), Is.EqualTo(5));
        }

        /// <summary>Supplies negative and overflowing quantities and verifies invalid operations preserve stock.</summary>
        [Test]
        public void InvalidAmountsCannotCorruptStock()
        {
            var inventory = new ResourceInventory(new Dictionary<string, int> { { "coal", int.MaxValue } });
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Add("coal", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TrySpend("coal", -1));
            Assert.Throws<OverflowException>(() => inventory.Add("coal", 1));
            Assert.That(inventory.Get("coal"), Is.EqualTo(int.MaxValue));
        }
    }
}
