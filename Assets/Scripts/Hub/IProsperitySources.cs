using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Hub
{
    /// <summary>Story 014. Per-lap inputs RunState cannot get through its constructor (Bootstrap is not edited).</summary>
    public interface IProsperitySources
    {
        /// <summary>Items currently placed in hubId's interior; 0 when unknown.</summary>
        int PlacedItemCount(string hubId);
        /// <summary>True when hubId's habitat is complete.</summary>
        bool IsHabitatComplete(string hubId);
        /// <summary>
        /// Story 016. Per-lap habitat prosperity of hubId: for every completed habitat cluster, its catalog bonus when that
        /// is positive, otherwise perHabitat (the hub's data HabitatBonus). Without a cluster system this is the Story 014
        /// rule: perHabitat when the habitat is complete, else 0.
        /// </summary>
        float HabitatBonus(string hubId, float perHabitat);
    }

    /// <summary>Default sources before the installer runs: no items, no habitat.</summary>
    internal sealed class NullProsperitySources : IProsperitySources
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly NullProsperitySources Instance = new NullProsperitySources();
        /// <summary>Always 0.</summary>
        public int PlacedItemCount(string hubId) => 0;
        /// <summary>Always false.</summary>
        public bool IsHabitatComplete(string hubId) => false;
        /// <summary>Always 0.</summary>
        public float HabitatBonus(string hubId, float perHabitat) => 0f;
    }

    /// <summary>
    /// Reads systems lazily on every query so late registration (Story 012/013) is picked up.
    /// Placed items are counted as IHubInterior cells in the ItemPlaced state (IItemPlacement has no count query).
    /// A null provider or a null system reads as 0/false.
    /// </summary>
    public sealed class ServiceProsperitySources : IProsperitySources
    {
        private readonly Func<IHubInterior> _interior;
        private readonly Func<IHabitatSystem> _habitat;
        private readonly Func<IHabitatClusters> _clusters;
        private readonly Func<string, float> _typeBonus;

        /// <summary>
        /// Stores the providers; any may be null or return null. clusters (Story 016) lists completed habitats per hub and
        /// typeBonus returns the catalog prosperity bonus of a habitat type id (0 or less means "use the hub default").
        /// </summary>
        public ServiceProsperitySources(Func<IHubInterior> interior, Func<IHabitatSystem> habitat,
            Func<IHabitatClusters> clusters = null, Func<string, float> typeBonus = null)
        {
            _interior = interior;
            _habitat = habitat;
            _clusters = clusters;
            _typeBonus = typeBonus;
        }

        /// <summary>Sums the bonus of every completed cluster (catalog value when positive, else perHabitat); falls back to the single-flag rule.</summary>
        public float HabitatBonus(string hubId, float perHabitat)
        {
            IHabitatClusters clusters = _clusters?.Invoke();
            if (clusters == null) return IsHabitatComplete(hubId) ? perHabitat : 0f;
            IReadOnlyList<HabitatCluster> completed = clusters.CompletedAt(hubId);
            if (completed == null) return 0f;
            float total = 0f;
            foreach (HabitatCluster cluster in completed)
            {
                if (cluster == null) continue;
                float bonus = _typeBonus?.Invoke(cluster.TypeId) ?? 0f;
                total += bonus > 0f ? bonus : perHabitat;
            }
            return total;
        }

        /// <summary>Counts ItemPlaced cells of hubId, or 0 when no interior is registered.</summary>
        public int PlacedItemCount(string hubId)
        {
            IHubInterior interior = _interior?.Invoke();
            if (interior == null) return 0;
            var cells = interior.Cells(hubId);
            if (cells == null) return 0;
            int count = 0;
            foreach (IInteriorCell cell in cells) if (cell != null && cell.State == InteriorCellState.ItemPlaced) count++;
            return count;
        }

        /// <summary>Asks the current habitat system, or false when none is registered.</summary>
        public bool IsHabitatComplete(string hubId)
        {
            IHabitatSystem habitat = _habitat?.Invoke();
            return habitat != null && habitat.IsHabitatComplete(hubId);
        }
    }
}
