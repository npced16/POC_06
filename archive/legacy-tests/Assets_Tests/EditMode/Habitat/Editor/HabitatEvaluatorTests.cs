using System;
using System.Collections.Generic;

using NUnit.Framework;

using IronRing.Habitat.Demo;
using IronRing.Inventory;

namespace IronRing.Habitat.Tests
{
    public sealed class HabitatEvaluatorTests
    {
        private ResourceInventory _inventory;
        private HabitatDemoTrack _track;
        private HabitatEvaluator _habitat;

        /// <summary>Creates two data-defined hubs and fresh inventory for each independent test.</summary>
        [SetUp]
        public void SetUp()
        {
            var definitions = new[]
            {
                new HabitatDefinition("hub-a", 3, new[] { "coal", "water" }, new[] { "coal", "water", null }, true),
                new HabitatDefinition("hub-b", 2, new[] { "coal", "water" }, new[] { "water", "coal" }, true)
            };
            _inventory = new ResourceInventory(new Dictionary<string, int> { { "coal", 6 }, { "water", 6 } });
            _track = new HabitatDemoTrack(new[] { "hub-a", "hub-b" });
            _habitat = new HabitatEvaluator(definitions, _inventory, _track);
        }

        /// <summary>Places and removes at the docked hub and verifies slots, completion, refund and sticky event semantics.</summary>
        [Test]
        public void CompletionAndStickyFlagFollowSuccessfulPlacement()
        {
            var raised = new List<string>();
            _habitat.EventFlagRaised += raised.Add;
            Assert.That(_habitat.TryPlace("hub-a", 0, "coal"), Is.True);
            Assert.That(_habitat.IsHabitatComplete("hub-a"), Is.False);
            Assert.That(_habitat.TryPlace("hub-a", 1, "water"), Is.True);
            Assert.That(_habitat.IsHabitatComplete("hub-a"), Is.True);
            Assert.That(_habitat.IsEventFlagSet("hub-a"), Is.True);
            Assert.That(_inventory.Get("water"), Is.EqualTo(5));
            Assert.That(_habitat.TryRemove("hub-a", 1), Is.True);
            Assert.That(_inventory.Get("water"), Is.EqualTo(6));
            Assert.That(_habitat.SlotContent("hub-a", 1), Is.Null);
            Assert.That(_habitat.IsHabitatComplete("hub-a"), Is.False);
            Assert.That(_habitat.IsEventFlagSet("hub-a"), Is.True);
            Assert.That(_habitat.TryPlace("hub-a", 1, "water"), Is.True);
            Assert.That(raised, Is.EqualTo(new[] { "hub-a" }));
        }

        /// <summary>Queries the same layout repeatedly; verifies deterministic results and no repeated flag event.</summary>
        [Test]
        public void QueriesAreDeterministicAndSideEffectFree()
        {
            int events = 0;
            _habitat.EventFlagRaised += id => events++;
            _habitat.TryPlace("hub-a", 0, "coal");
            _habitat.TryPlace("hub-a", 1, "water");
            for (int i = 0; i < 100; i++)
            {
                Assert.That(_habitat.IsHabitatComplete("hub-a"), Is.True);
                Assert.That(_habitat.IsEventFlagSet("hub-a"), Is.True);
            }
            Assert.That(events, Is.EqualTo(1));
        }

        /// <summary>Exhausts one resource before placement and verifies slots, flags and inventory remain unchanged.</summary>
        [Test]
        public void InsufficientStockIsAtomic()
        {
            _inventory.TrySpend("coal", 6);
            Assert.That(_habitat.TryPlace("hub-a", 0, "coal"), Is.False);
            Assert.That(_habitat.SlotContent("hub-a", 0), Is.Null);
            Assert.That(_habitat.IsHabitatComplete("hub-a"), Is.False);
            Assert.That(_habitat.IsEventFlagSet("hub-a"), Is.False);
            Assert.That(_inventory.Get("coal"), Is.Zero);
        }

        /// <summary>Requests invalid ids, indices, occupied slots and non-docked edits; verifies all refuse without spending.</summary>
        [Test]
        public void PlacementRequiresAllowedResourceEmptySlotAndCurrentDock()
        {
            Assert.That(_habitat.TryPlace("hub-b", 0, "coal"), Is.False);
            Assert.That(_habitat.TryPlace("missing", 0, "coal"), Is.False);
            Assert.That(_habitat.TryPlace("hub-a", -1, "coal"), Is.False);
            Assert.That(_habitat.TryPlace("hub-a", 3, "coal"), Is.False);
            Assert.That(_habitat.TryPlace("hub-a", 0, "scrap"), Is.False);
            Assert.That(_habitat.TryPlace("hub-a", 0, "coal"), Is.True);
            Assert.That(_habitat.TryPlace("hub-a", 0, "water"), Is.False);
            _track.Depart();
            Assert.That(_habitat.TryRemove("hub-a", 0), Is.False);
            Assert.That(_habitat.TryPlace("hub-a", 1, "water"), Is.False);
            Assert.That(_inventory.Get("coal"), Is.EqualTo(5));
            Assert.That(_inventory.Get("water"), Is.EqualTo(6));
        }

        /// <summary>Uses the second hub's distinct pattern and verifies order matters and hub state stays isolated.</summary>
        [Test]
        public void EachHubUsesItsOwnOrderedPatternAndSlotCount()
        {
            _track.DockAt("hub-b");
            _habitat.TryPlace("hub-b", 0, "coal");
            _habitat.TryPlace("hub-b", 1, "water");
            Assert.That(_habitat.IsHabitatComplete("hub-b"), Is.False);
            Assert.That(_habitat.TryPlace("hub-b", 2, "water"), Is.False);
            _habitat.TryRemove("hub-b", 0);
            _habitat.TryRemove("hub-b", 1);
            _habitat.TryPlace("hub-b", 0, "water");
            _habitat.TryPlace("hub-b", 1, "coal");
            Assert.That(_habitat.IsHabitatComplete("hub-b"), Is.True);
            Assert.That(_habitat.IsEventFlagSet("hub-a"), Is.False);
        }

        /// <summary>Configures a non-sticky flag and verifies removing and restoring the pattern raises each rising edge.</summary>
        [Test]
        public void NonStickyPolicyIsControlledByDefinition()
        {
            var definition = new HabitatDefinition("hub-a", 1, new[] { "coal" }, new[] { "coal" }, false);
            var habitat = new HabitatEvaluator(new[] { definition }, _inventory, _track);
            int events = 0;
            habitat.EventFlagRaised += id => events++;
            habitat.TryPlace("hub-a", 0, "coal");
            habitat.TryRemove("hub-a", 0);
            Assert.That(habitat.IsEventFlagSet("hub-a"), Is.False);
            habitat.TryPlace("hub-a", 0, "coal");
            Assert.That(events, Is.EqualTo(2));
        }

        /// <summary>Fills an explicitly empty recipe slot, then clears it; verifies exact empty-slot pattern semantics.</summary>
        [Test]
        public void EmptyPatternPositionsMustRemainEmpty()
        {
            _habitat.TryPlace("hub-a", 0, "coal");
            _habitat.TryPlace("hub-a", 1, "water");
            _habitat.TryPlace("hub-a", 2, "coal");
            Assert.That(_habitat.IsHabitatComplete("hub-a"), Is.False);
            Assert.That(_habitat.TryRemove("hub-a", 2), Is.True);
            Assert.That(_habitat.IsHabitatComplete("hub-a"), Is.True);
            Assert.That(_habitat.TryRemove("hub-a", 2), Is.False);
        }

        /// <summary>Mutates source arrays after definition construction; verifies defensive copies and rejects invalid recipes.</summary>
        [Test]
        public void DefinitionsValidateAndOwnTheirInputs()
        {
            var allowed = new[] { "coal" };
            var pattern = new[] { "coal" };
            var definition = new HabitatDefinition("hub-a", 1, allowed, pattern, true);
            allowed[0] = "water";
            pattern[0] = "water";
            Assert.That(definition.AllowedResources[0], Is.EqualTo("coal"));
            Assert.That(definition.Pattern[0], Is.EqualTo("coal"));
            Assert.Throws<ArgumentException>(() => new HabitatDefinition("hub-a", 2, allowed, pattern, true));
            Assert.Throws<ArgumentException>(() => new HabitatDefinition("hub-a", 1, new[] { "coal" }, pattern, true));
            Assert.Throws<ArgumentException>(() => new HabitatDefinition("hub-a", 1, allowed, new string[] { null }, true));
        }
    }
}
