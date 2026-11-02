using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Hub
{
    /// <summary>
    /// Story 014. Builds a facility on a FacilityPlot interior cell. IHubInterior offers no setter, so this wrapper keeps
    /// its own per-hub map of built cells and raises its own event. Story 012's cell state should eventually reflect it.
    /// </summary>
    public interface IInteriorFacilityPlots
    {
        /// <summary>True when the cell is a FacilityPlot or (Story 016) an Empty cell not yet built on.</summary>
        bool IsPlot(string hubId, int cellIndex);
        /// <summary>True when IsPlot and IFacilityBuilder.CanBuild agree.</summary>
        bool CanBuildOnCell(string hubId, int cellIndex, string facilityId);
        /// <summary>Builds through IFacilityBuilder.TryBuild and records the cell; false and no change if refused.</summary>
        bool TryBuildOnCell(string hubId, int cellIndex, string facilityId);
        /// <summary>Facility id on the cell (own map first, then interior FacilityBuilt state), or null.</summary>
        string FacilityAt(string hubId, int cellIndex);
        /// <summary>hubId, cellIndex, facilityId.</summary>
        event Action<string, int, string> FacilityPlaced;
    }

    /// <summary>Pure C# implementation of IInteriorFacilityPlots reusing Story 006's FacilityBuilder rules.</summary>
    public sealed class InteriorFacilityPlots : IInteriorFacilityPlots
    {
        private readonly IFacilityBuilder _builder;
        private readonly Func<IHubInterior> _interior;
        private readonly Func<IInteriorFacilityCells> _cells;
        private readonly Dictionary<string, Dictionary<int, string>> _built = new Dictionary<string, Dictionary<int, string>>();

        /// <summary>hubId, cellIndex, facilityId after a successful build.</summary>
        public event Action<string, int, string> FacilityPlaced;

        /// <summary>
        /// Takes the facility builder, a lazy interior provider and an optional lazy Story 012 cell writer
        /// (each may return null until Story 012 registers; without a writer the own map is the record).
        /// </summary>
        public InteriorFacilityPlots(IFacilityBuilder builder, Func<IHubInterior> interior, Func<IInteriorFacilityCells> cells = null)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _interior = interior ?? throw new ArgumentNullException(nameof(interior));
            _cells = cells;
        }

        /// <summary>
        /// Reads the interior cell and returns true for an unbuilt FacilityPlot or, since Story 016 (no facility plots,
        /// facilities go on any empty cell), an Empty cell.
        /// </summary>
        public bool IsPlot(string hubId, int cellIndex)
        {
            IInteriorCell cell = Cell(hubId, cellIndex);
            return cell != null && (cell.State == InteriorCellState.FacilityPlot || cell.State == InteriorCellState.Empty)
                && Recorded(hubId, cellIndex) == null;
        }

        /// <summary>True when the cell is a free plot and the builder allows facilityId at hubId.</summary>
        public bool CanBuildOnCell(string hubId, int cellIndex, string facilityId) =>
            IsPlot(hubId, cellIndex) && _builder.CanBuild(hubId, facilityId);

        /// <summary>
        /// Builds facilityId on a free plot through the builder, then asks Story 012's IInteriorFacilityCells to mark the
        /// cell FacilityBuilt; records the cell in the own map only when that writer is missing or refuses. Raises FacilityPlaced.
        /// </summary>
        public bool TryBuildOnCell(string hubId, int cellIndex, string facilityId)
        {
            if (!IsPlot(hubId, cellIndex) || !_builder.TryBuild(hubId, facilityId)) return false;
            IInteriorFacilityCells writer = _cells?.Invoke();
            if (writer == null || !writer.TryBuildFacility(hubId, cellIndex, facilityId))
            {
                if (!_built.TryGetValue(hubId, out var cells)) _built.Add(hubId, cells = new Dictionary<int, string>());
                cells[cellIndex] = facilityId;
            }
            FacilityPlaced?.Invoke(hubId, cellIndex, facilityId);
            return true;
        }

        /// <summary>Returns the recorded facility, else the interior's FacilityBuilt id, else null.</summary>
        public string FacilityAt(string hubId, int cellIndex)
        {
            string recorded = Recorded(hubId, cellIndex);
            if (recorded != null) return recorded;
            IInteriorCell cell = Cell(hubId, cellIndex);
            return cell != null && cell.State == InteriorCellState.FacilityBuilt ? cell.FacilityId : null;
        }

        /// <summary>Own record for the cell or null.</summary>
        private string Recorded(string hubId, int cellIndex) =>
            hubId != null && _built.TryGetValue(hubId, out var cells) && cells.TryGetValue(cellIndex, out string id) ? id : null;

        /// <summary>Interior cell at cellIndex or null when the interior, hub or index is unavailable.</summary>
        private IInteriorCell Cell(string hubId, int cellIndex)
        {
            IHubInterior interior = _interior();
            if (interior == null || hubId == null) return null;
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hubId);
            return cells != null && cellIndex >= 0 && cellIndex < cells.Count ? cells[cellIndex] : null;
        }
    }
}
