using System;
using System.Collections.Generic;

using NUnit.Framework;

using IronRing.Contracts;

namespace IronRing.Hub.Tests
{
    /// <summary>Story 014: placed-item counting through IHubInterior and facility building on FacilityPlot cells.</summary>
    public sealed class HubSourcesTests
    {
        private sealed class Cell : IInteriorCell
        {
            public int Index { get; set; }
            public int Row => 0;
            public int Column => Index;
            public bool IsUpper => true;
            public InteriorCellState State { get; set; }
            public string ItemId { get; set; }
            public string FacilityId { get; set; }
        }

        private sealed class FakeInterior : IHubInterior
        {
            public readonly List<IInteriorCell> List = new List<IInteriorCell>();
            public FakeInterior(params InteriorCellState[] states)
            {
                for (int i = 0; i < states.Length; i++) List.Add(new Cell { Index = i, State = states[i], FacilityId = states[i] == InteriorCellState.FacilityBuilt ? "turret" : null });
            }
            public IReadOnlyList<IInteriorCell> Cells(string hubId) => List;
            public int InvestigationsLeft(string hubId) => 0;
            public bool CanInvestigate(string hubId, int cellIndex) => false;
            public InvestigationResult Investigate(string hubId, int cellIndex, out string foundId) { foundId = null; return InvestigationResult.Empty; }
            public void OnDocked(string hubId) { }
            public event Action<string, int> CellChanged { add { } remove { } }
            public event Action<string, int> InvestigationsChanged { add { } remove { } }
        }

        private sealed class FakeBuilder : IFacilityBuilder
        {
            public bool Allow = true;
            public readonly List<string> Built = new List<string>();
            public IReadOnlyList<string> FacilitiesAt(string hubId) => Built;
            public bool CanBuild(string hubId, string facilityId) => Allow;
            public bool TryBuild(string hubId, string facilityId) { if (!Allow) return false; Built.Add(facilityId); return true; }
            public float CombatBonus(string hubId) => 0;
            public event Action<string, string> FacilityBuilt { add { } remove { } }
        }

        private sealed class FakeHabitat : IHabitatSystem
        {
            public bool Complete;
            public bool TryPlace(string hubId, int slotIndex, string resourceId) => false;
            public bool TryRemove(string hubId, int slotIndex) => false;
            public string SlotContent(string hubId, int slotIndex) => null;
            public bool IsHabitatComplete(string hubId) => Complete;
            public bool IsEventFlagSet(string hubId) => Complete;
            public event Action<string> EventFlagRaised { add { } remove { } }
        }

        [Test]
        public void PlacedItemCount_CountsOnlyItemPlacedCells()
        {
            var interior = new FakeInterior(InteriorCellState.ItemPlaced, InteriorCellState.Empty, InteriorCellState.ItemPlaced, InteriorCellState.FacilityBuilt);
            var sources = new ServiceProsperitySources(() => interior, () => new FakeHabitat { Complete = true });

            Assert.That(sources.PlacedItemCount("hub-1"), Is.EqualTo(2));
            Assert.That(sources.IsHabitatComplete("hub-1"), Is.True);
        }

        [Test]
        public void MissingSystems_ReadAsZeroAndFalse()
        {
            var sources = new ServiceProsperitySources(() => null, () => null);

            Assert.That(sources.PlacedItemCount("hub-1"), Is.EqualTo(0));
            Assert.That(sources.IsHabitatComplete("hub-1"), Is.False);
        }

        [Test]
        public void TryBuildOnCell_BuildsOnlyOnFreePlotAndRaisesEvent()
        {
            var interior = new FakeInterior(InteriorCellState.Empty, InteriorCellState.FacilityPlot);
            var builder = new FakeBuilder();
            var plots = new InteriorFacilityPlots(builder, () => interior);
            var placed = new List<string>();
            plots.FacilityPlaced += (hub, cell, id) => placed.Add(hub + "/" + cell + "/" + id);

            Assert.That(plots.TryBuildOnCell("hub-1", 0, "wall"), Is.False);
            Assert.That(plots.TryBuildOnCell("hub-1", 1, "wall"), Is.True);
            Assert.That(plots.TryBuildOnCell("hub-1", 1, "turret"), Is.False, "A plot takes one facility.");

            Assert.That(plots.FacilityAt("hub-1", 1), Is.EqualTo("wall"));
            Assert.That(plots.IsPlot("hub-1", 1), Is.False);
            Assert.That(builder.Built, Is.EqualTo(new[] { "wall" }));
            Assert.That(placed, Is.EqualTo(new[] { "hub-1/1/wall" }));
        }

        [Test]
        public void TryBuildOnCell_RefusedByBuilder_ChangesNothing()
        {
            var interior = new FakeInterior(InteriorCellState.FacilityPlot);
            var builder = new FakeBuilder { Allow = false };
            var plots = new InteriorFacilityPlots(builder, () => interior);

            Assert.That(plots.CanBuildOnCell("hub-1", 0, "wall"), Is.False);
            Assert.That(plots.TryBuildOnCell("hub-1", 0, "wall"), Is.False);
            Assert.That(plots.FacilityAt("hub-1", 0), Is.Null);
            Assert.That(plots.IsPlot("hub-1", 0), Is.True);
        }

        private sealed class FakeCells : IInteriorFacilityCells
        {
            private readonly FakeInterior _interior;
            public bool Refuse;
            public FakeCells(FakeInterior interior) { _interior = interior; }
            public bool TryBuildFacility(string hubId, int cellIndex, string facilityId)
            {
                var cell = (Cell)_interior.List[cellIndex];
                if (Refuse || cell.State != InteriorCellState.FacilityPlot) return false;
                cell.State = InteriorCellState.FacilityBuilt; cell.FacilityId = facilityId;
                return true;
            }
        }

        [Test]
        public void TryBuildOnCell_WithCellWriter_MarksInteriorCellFacilityBuilt()
        {
            var interior = new FakeInterior(InteriorCellState.FacilityPlot);
            var plots = new InteriorFacilityPlots(new FakeBuilder(), () => interior, () => new FakeCells(interior));

            Assert.That(plots.TryBuildOnCell("hub-1", 0, "wall"), Is.True);

            Assert.That(interior.List[0].State, Is.EqualTo(InteriorCellState.FacilityBuilt));
            Assert.That(interior.List[0].FacilityId, Is.EqualTo("wall"));
            Assert.That(plots.FacilityAt("hub-1", 0), Is.EqualTo("wall"));
            Assert.That(plots.IsPlot("hub-1", 0), Is.False);
        }

        [Test]
        public void TryBuildOnCell_WriterRefuses_FallsBackToOwnRecord()
        {
            var interior = new FakeInterior(InteriorCellState.FacilityPlot);
            var plots = new InteriorFacilityPlots(new FakeBuilder(), () => interior, () => new FakeCells(interior) { Refuse = true });

            Assert.That(plots.TryBuildOnCell("hub-1", 0, "wall"), Is.True);

            Assert.That(interior.List[0].State, Is.EqualTo(InteriorCellState.FacilityPlot));
            Assert.That(plots.FacilityAt("hub-1", 0), Is.EqualTo("wall"));
            Assert.That(plots.IsPlot("hub-1", 0), Is.False);
        }

        [Test]
        public void FacilityAt_ReadsInteriorFacilityBuiltCells_AndNoInteriorMeansNoPlots()
        {
            var plots = new InteriorFacilityPlots(new FakeBuilder(), () => new FakeInterior(InteriorCellState.FacilityBuilt));
            var none = new InteriorFacilityPlots(new FakeBuilder(), () => null);

            Assert.That(plots.FacilityAt("hub-1", 0), Is.EqualTo("turret"));
            Assert.That(none.IsPlot("hub-1", 0), Is.False);
            Assert.That(none.TryBuildOnCell("hub-1", 0, "wall"), Is.False);
        }
    }
}
