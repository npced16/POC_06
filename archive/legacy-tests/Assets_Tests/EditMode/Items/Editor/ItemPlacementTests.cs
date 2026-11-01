using System.Collections.Generic;

using NUnit.Framework;

using IronRing.Contracts;
using IronRing.Items;

namespace IronRing.Items.Tests
{
    /// <summary>스토리 013 AC: 빈 터에만 배치, 배치·회수 수량 보존, 칸 상태 갱신, 이벤트.</summary>
    public sealed class ItemPlacementTests
    {
        private FakeInterior _interior;
        private ItemInventory _items;
        private ItemPlacement _placement;
        private readonly List<string> _events = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _interior = new FakeInterior(6, "hub-1");
            _items = new ItemInventory(ItemsFixtures.Catalog(), new FakeResources());
            _items.Add("gear", 2);
            _placement = new ItemPlacement(_items, _interior, _interior);
            _placement.ItemPlaced += (h, i, id) => _events.Add($"+{h}:{i}:{id}");
            _placement.ItemRemoved += (h, i, id) => _events.Add($"-{h}:{i}:{id}");
        }

        [Test]
        public void Place_OnEmptyCell_TakesOneAndMarksCell()
        {
            Assert.IsTrue(_placement.TryPlace("hub-1", 2, "gear"));
            Assert.AreEqual(1, _items.Count("gear"));
            IInteriorCell cell = _interior.Cells("hub-1")[2];
            Assert.AreEqual(InteriorCellState.ItemPlaced, cell.State);
            Assert.AreEqual("gear", cell.ItemId);
            CollectionAssert.AreEqual(new[] { "+hub-1:2:gear" }, _events);
        }

        [TestCase(InteriorCellState.Rubble)]
        [TestCase(InteriorCellState.FacilityPlot)]
        [TestCase(InteriorCellState.FacilityBuilt)]
        public void Place_OnNonEmptyCell_IsRefusedWithoutChange(InteriorCellState state)
        {
            _interior.SetState("hub-1", 0, state);
            Assert.IsFalse(_placement.CanPlace("hub-1", 0, "gear"));
            Assert.IsFalse(_placement.TryPlace("hub-1", 0, "gear"));
            Assert.AreEqual(2, _items.Count("gear"));
            Assert.AreEqual(state, _interior.Cells("hub-1")[0].State);
            Assert.IsEmpty(_events);
        }

        [Test]
        public void Place_WithoutItem_OrOnOccupiedCell_OrBadIndex_IsRefused()
        {
            Assert.IsFalse(_placement.TryPlace("hub-1", 0, "brazier"));
            Assert.IsTrue(_placement.TryPlace("hub-1", 0, "gear"));
            Assert.IsFalse(_placement.TryPlace("hub-1", 0, "gear"));
            Assert.IsFalse(_placement.TryPlace("hub-1", 99, "gear"));
            Assert.IsFalse(_placement.TryPlace("hub-x", 0, "gear"));
            Assert.AreEqual(1, _items.Count("gear"));
        }

        [Test]
        public void PickUp_ReturnsOneAndEmptiesCell()
        {
            _placement.TryPlace("hub-1", 3, "gear");
            Assert.IsTrue(_placement.TryPickUp("hub-1", 3));
            Assert.AreEqual(2, _items.Count("gear"));
            Assert.AreEqual(InteriorCellState.Empty, _interior.Cells("hub-1")[3].State);
            Assert.IsNull(_interior.Cells("hub-1")[3].ItemId);
            Assert.IsFalse(_placement.TryPickUp("hub-1", 3));
            CollectionAssert.AreEqual(new[] { "+hub-1:3:gear", "-hub-1:3:gear" }, _events);
        }

        [Test]
        public void PlaceAndPickUp_ConserveTotalCount()
        {
            for (int round = 0; round < 3; round++)
            {
                Assert.IsTrue(_placement.TryPlace("hub-1", 0, "gear"));
                Assert.IsTrue(_placement.TryPlace("hub-1", 1, "gear"));
                Assert.AreEqual(0, _items.Count("gear"));
                Assert.IsTrue(_placement.TryPickUp("hub-1", 0));
                Assert.IsTrue(_placement.TryPickUp("hub-1", 1));
                Assert.AreEqual(2, _items.Count("gear"));
            }
        }

        [Test]
        public void Place_WhenCellWriteRefused_RefundsItem()
        {
            _interior.RefuseWrites = true;
            Assert.IsFalse(_placement.TryPlace("hub-1", 0, "gear"));
            Assert.AreEqual(2, _items.Count("gear"));
            Assert.IsEmpty(_events);
        }

        [Test]
        public void EditGate_BlocksPlacementAtOtherHub()
        {
            var gated = new ItemPlacement(_items, _interior, _interior, hub => hub == "hub-2");
            Assert.IsFalse(gated.TryPlace("hub-1", 0, "gear"));
            Assert.AreEqual(2, _items.Count("gear"));
        }
    }
}
