using System.Collections.Generic;

using NUnit.Framework;

using IronRing.Contracts;
using IronRing.Items;

namespace IronRing.Items.Tests
{
    /// <summary>스토리 013 AC: 태그 합산, 완성 판정, 플래그 1회·sticky, 슬롯 메서드 비활성, 아이템 보상.</summary>
    public sealed class TagHabitatSystemTests
    {
        private FakeInterior _interior;
        private ItemInventory _items;
        private ItemPlacement _placement;
        private TagHabitatSystem _habitat;
        private readonly List<string> _raised = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _raised.Clear();
            _interior = new FakeInterior(6, "hub-1", "hub-2");
            ItemCatalog catalog = ItemsFixtures.Catalog();
            _items = new ItemInventory(catalog, new FakeResources());
            _items.Add("brazier", 3); _items.Add("steam_pipe", 2); _items.Add("gear", 2); _items.Add("seedling", 2);
            _placement = new ItemPlacement(_items, _interior, _interior);
            _habitat = new TagHabitatSystem(catalog, ItemsFixtures.Rules(), _interior);
            _habitat.EventFlagRaised += hub => _raised.Add(hub);
        }

        [TearDown]
        public void TearDown() => _habitat.Dispose();

        [Test]
        public void TagTotals_SumEveryTagOfPlacedItems()
        {
            _placement.TryPlace("hub-1", 0, "steam_pipe");
            _placement.TryPlace("hub-1", 1, "brazier");
            _placement.TryPlace("hub-1", 2, "gear");
            IReadOnlyDictionary<string, int> totals = _habitat.TagTotals("hub-1");
            Assert.AreEqual(2, totals["heat"]);
            Assert.AreEqual(2, totals["industry"]);
            Assert.IsFalse(totals.ContainsKey("plant"));
        }

        [Test]
        public void Completion_RequiresEveryThreshold()
        {
            _placement.TryPlace("hub-1", 0, "brazier");
            _placement.TryPlace("hub-1", 1, "gear");
            Assert.IsFalse(_habitat.IsHabitatComplete("hub-1"), "열 1/2");
            _placement.TryPlace("hub-1", 2, "brazier");
            Assert.IsTrue(_habitat.IsHabitatComplete("hub-1"), "열 2/2 + 산업 1/1");
        }

        [Test]
        public void Completion_HeatAloneIsNotEnough()
        {
            _placement.TryPlace("hub-1", 0, "brazier");
            _placement.TryPlace("hub-1", 1, "brazier");
            _placement.TryPlace("hub-1", 2, "brazier");
            Assert.IsFalse(_habitat.IsHabitatComplete("hub-1"));
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void MultiTagItem_CountsTowardEachTag()
        {
            _placement.TryPlace("hub-1", 0, "steam_pipe");
            _placement.TryPlace("hub-1", 1, "brazier");
            Assert.IsTrue(_habitat.IsHabitatComplete("hub-1"));
        }

        [Test]
        public void Flag_RaisedExactlyOnce_AndSticky()
        {
            _placement.TryPlace("hub-1", 0, "steam_pipe");
            _placement.TryPlace("hub-1", 1, "brazier");
            _placement.TryPlace("hub-1", 2, "gear");
            CollectionAssert.AreEqual(new[] { "hub-1" }, _raised);
            Assert.IsTrue(_placement.TryPickUp("hub-1", 1));
            Assert.IsFalse(_habitat.IsHabitatComplete("hub-1"));
            Assert.IsTrue(_habitat.IsEventFlagSet("hub-1"), "sticky");
            Assert.IsTrue(_placement.TryPlace("hub-1", 1, "brazier"));
            Assert.IsTrue(_habitat.IsHabitatComplete("hub-1"));
            CollectionAssert.AreEqual(new[] { "hub-1" }, _raised, "다시 완성해도 한 번만");
        }

        [Test]
        public void Hubs_AreEvaluatedIndependently()
        {
            _placement.TryPlace("hub-1", 0, "steam_pipe");
            _placement.TryPlace("hub-1", 1, "brazier");
            Assert.IsFalse(_habitat.IsHabitatComplete("hub-2"));
            Assert.IsFalse(_habitat.IsEventFlagSet("hub-2"));
            Assert.IsFalse(_habitat.IsHabitatComplete("unknown"));
            Assert.IsFalse(_habitat.IsEventFlagSet(null));
        }

        [Test]
        public void Progress_ReportsCurrentAndRequired()
        {
            _placement.TryPlace("hub-1", 0, "brazier");
            IReadOnlyList<TagProgress> progress = _habitat.Progress("hub-1");
            Assert.AreEqual(2, progress.Count);
            Assert.AreEqual("heat", progress[0].TagId); Assert.AreEqual(1, progress[0].Current); Assert.AreEqual(2, progress[0].Required);
            Assert.AreEqual("industry", progress[1].TagId); Assert.AreEqual(0, progress[1].Current); Assert.IsFalse(progress[1].Met);
        }

        [Test]
        public void SlotMethods_AreDisabled()
        {
            Assert.IsFalse(_habitat.TryPlace("hub-1", 0, "coal"));
            Assert.IsFalse(_habitat.TryRemove("hub-1", 0));
            Assert.IsNull(_habitat.SlotContent("hub-1", 0));
        }

        [Test]
        public void Dispose_StopsListeningToCells()
        {
            _habitat.Dispose();
            _placement.TryPlace("hub-1", 0, "steam_pipe");
            _placement.TryPlace("hub-1", 1, "brazier");
            Assert.IsFalse(_habitat.IsHabitatComplete("hub-1"));
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void EncounterRewards_GrantByOutcomeAndEventTitle()
        {
            var rewards = new EncounterItemRewards(_items, "gear", 1,
                new Dictionary<string, KeyValuePair<string, int>> { { "Coal cache", new KeyValuePair<string, int>("brazier", 1) } });
            int gear = _items.Count("gear"), brazier = _items.Count("brazier");
            Assert.AreEqual("gear", rewards.Grant(EncounterOutcome.Victory, null));
            Assert.AreEqual("brazier", rewards.Grant(EncounterOutcome.EventResolved, "Coal cache"));
            Assert.IsNull(rewards.Grant(EncounterOutcome.EventResolved, "Leaking water tank"));
            Assert.IsNull(rewards.Grant(EncounterOutcome.Defeat, null));
            Assert.AreEqual(gear + 1, _items.Count("gear"));
            Assert.AreEqual(brazier + 1, _items.Count("brazier"));
        }

        [Test]
        public void EncounterRewards_ParseOptionalFieldsFromCombatData()
        {
            var catalog = ItemCatalog.Parse(System.IO.File.ReadAllText("Assets/Scripts/Items/Resources/Items/ItemCatalog.json"));
            var items = new ItemInventory(catalog, new FakeResources());
            var rewards = EncounterItemRewards.Parse(System.IO.File.ReadAllText("Assets/Scripts/Combat/Resources/Story005Encounters.json"), items);
            Assert.IsNotNull(rewards.Grant(EncounterOutcome.Victory, null));
            Assert.AreEqual(1, items.Snapshot().Count);
        }
    }
}
