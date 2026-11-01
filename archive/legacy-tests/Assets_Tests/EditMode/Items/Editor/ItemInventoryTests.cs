using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;

using IronRing.Items;

namespace IronRing.Items.Tests
{
    /// <summary>스토리 013 AC: 카탈로그 데이터, 인벤토리 수량, 제작 비용과 거부.</summary>
    public sealed class ItemInventoryTests
    {
        private FakeResources _resources;
        private ItemInventory _items;

        [SetUp]
        public void SetUp()
        {
            _resources = new FakeResources(("coal", 6), ("scrap", 6), ("water", 6), ("grain", 0));
            _items = new ItemInventory(ItemsFixtures.Catalog(), _resources);
        }

        [Test]
        public void Craft_SpendsExactCostAndAddsOne()
        {
            var changes = new List<(string, int)>();
            _items.Changed += (id, n) => changes.Add((id, n));
            Assert.IsTrue(_items.CanCraft("steam_pipe"));
            Assert.IsTrue(_items.TryCraft("steam_pipe"));
            Assert.AreEqual(1, _items.Count("steam_pipe"));
            Assert.AreEqual(4, _resources.Get("scrap"));
            Assert.AreEqual(4, _resources.Get("coal"));
            Assert.AreEqual(5, _resources.Get("water"));
            CollectionAssert.AreEqual(new[] { ("steam_pipe", 1) }, changes);
        }

        [Test]
        public void Craft_RefusedWhenShort_ChangesNothing()
        {
            var before = _resources.Snapshot().ToDictionary(e => e.Key, e => e.Value);
            int events = 0;
            _items.Changed += (id, n) => events++;
            Assert.IsFalse(_items.CanCraft("seedling"));
            Assert.IsFalse(_items.TryCraft("seedling"));
            Assert.AreEqual(0, _items.Count("seedling"));
            CollectionAssert.AreEquivalent(before, _resources.Snapshot());
            Assert.AreEqual(0, events);
        }

        [Test]
        public void Craft_UntilResourcesRunOut_ThenRefuses()
        {
            // 화로: 고철 1 + 석탄 2. 석탄 6이면 정확히 3번.
            for (int i = 0; i < 3; i++) Assert.IsTrue(_items.TryCraft("brazier"));
            Assert.IsFalse(_items.TryCraft("brazier"));
            Assert.AreEqual(3, _items.Count("brazier"));
            Assert.AreEqual(0, _resources.Get("coal"));
            Assert.AreEqual(3, _resources.Get("scrap"));
        }

        [Test]
        public void Craft_UnknownItem_IsRefused()
        {
            Assert.IsFalse(_items.CanCraft("nope"));
            Assert.IsFalse(_items.TryCraft("nope"));
        }

        [Test]
        public void AddRemove_TrackCountsAndSnapshot()
        {
            _items.Add("gear", 2);
            Assert.IsTrue(_items.TryRemove("gear", 1));
            Assert.IsFalse(_items.TryRemove("gear", 5));
            Assert.AreEqual(1, _items.Count("gear"));
            Assert.AreEqual(1, _items.Snapshot()["gear"]);
            Assert.Throws<ArgumentException>(() => _items.Add("nope", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _items.Add("gear", -1));
        }

        [Test]
        public void Data_CatalogHasTwelveKoreanItemsWithTagsAndCosts()
        {
            ItemCatalog catalog = ItemCatalog.Parse(File.ReadAllText("Assets/Scripts/Items/Resources/Items/ItemCatalog.json"));
            var names = catalog.Items.Select(i => i.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "가로등", "화로", "물통", "우물", "화분", "텃밭", "벤치", "천막", "창고 상자", "톱니 장식", "깃발", "증기관" }, names);
            CollectionAssert.AreEquivalent(new[] { "빛", "열", "물", "식물", "쉼터", "산업" }, catalog.TagIds.Select(catalog.TagName).ToArray());
            var resources = new[] { "coal", "scrap", "water", "grain" };
            foreach (ItemDefinition item in catalog.Items)
            {
                Assert.IsNotEmpty(item.Tags, item.Id);
                Assert.IsNotEmpty(item.Cost, item.Id);
                foreach (string r in item.Cost.Keys) CollectionAssert.Contains(resources, r, item.Id);
            }
        }

        [Test]
        public void Data_HubRequirementsMatchDesignDefaults()
        {
            var rules = HubTagRequirement.Parse(File.ReadAllText("Assets/Scripts/Items/Resources/Items/HabitatTags.json")).ToDictionary(r => r.HubId);
            Assert.AreEqual(2, rules["hub-1"].Required["heat"]); Assert.AreEqual(1, rules["hub-1"].Required["industry"]);
            Assert.AreEqual(2, rules["hub-2"].Required["plant"]); Assert.AreEqual(1, rules["hub-2"].Required["water"]);
            Assert.AreEqual(2, rules["hub-3"].Required["water"]); Assert.AreEqual(1, rules["hub-3"].Required["industry"]);
            Assert.AreEqual(2, rules["hub-4"].Required["shelter"]); Assert.AreEqual(1, rules["hub-4"].Required["light"]);
        }
    }
}
