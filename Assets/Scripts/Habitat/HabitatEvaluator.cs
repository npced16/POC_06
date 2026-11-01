using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Habitat
{
    /// <summary>Pure C# placement and deterministic ordered combo evaluation, consuming only inventory and track contracts.</summary>
    public sealed class HabitatEvaluator : IHabitatSystem
    {
        private readonly Dictionary<string, HubState> _hubs = new Dictionary<string, HubState>(StringComparer.Ordinal);
        private readonly IResourceInventory _inventory;
        private readonly ITrackRuntime _track;
        private bool _editing;

        /// <summary>Emits a hub id only when that hub's flag transitions from false to true.</summary>
        public event Action<string> EventFlagRaised;

        /// <summary>Initializes empty hub slots from definitions and injects inventory and track dependencies.</summary>
        public HabitatEvaluator(IEnumerable<HabitatDefinition> definitions, IResourceInventory inventory, ITrackRuntime track)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _track = track ?? throw new ArgumentNullException(nameof(track));
            foreach (var definition in definitions)
                _hubs.Add(definition.HubId, new HubState(definition));
            if (_hubs.Count == 0) throw new ArgumentException("At least one habitat is required.", nameof(definitions));
        }

        /// <summary>Places resourceId into the empty slotIndex at the docked hubId, spending one unit; returns success.</summary>
        public bool TryPlace(string hubId, int slotIndex, string resourceId)
        {
            if (_editing || !TryGetSlot(hubId, slotIndex, out var hub) || !CanEdit(hubId) || hub.Slots[slotIndex] != null) return false;
            if (resourceId == null || !Contains(hub.Definition.AllowedResources, resourceId) || _inventory.Get(resourceId) < 1) return false;
            _editing = true;
            try
            {
                if (!_inventory.TrySpend(resourceId, 1)) return false;
                hub.Slots[slotIndex] = resourceId;
                Evaluate(hub);
                return true;
            }
            finally { _editing = false; }
        }

        /// <summary>Removes slotIndex at the docked hubId, refunds its resource and updates completion; returns success.</summary>
        public bool TryRemove(string hubId, int slotIndex)
        {
            if (_editing || !TryGetSlot(hubId, slotIndex, out var hub) || !CanEdit(hubId)) return false;
            string resourceId = hub.Slots[slotIndex];
            if (resourceId == null || _inventory.Get(resourceId) == int.MaxValue) return false;
            _editing = true;
            try
            {
                _inventory.Add(resourceId, 1);
                hub.Slots[slotIndex] = null;
                Evaluate(hub);
                return true;
            }
            finally { _editing = false; }
        }

        /// <summary>Returns the resource at hubId and slotIndex, or null for an empty or unknown slot.</summary>
        public string SlotContent(string hubId, int slotIndex)
        {
            return TryGetSlot(hubId, slotIndex, out var hub) ? hub.Slots[slotIndex] : null;
        }

        /// <summary>Returns the cached deterministic completion state of hubId, or false for an unknown hub.</summary>
        public bool IsHabitatComplete(string hubId)
        {
            return hubId != null && _hubs.TryGetValue(hubId, out var hub) && hub.Complete;
        }

        /// <summary>Returns the event flag state of hubId, or false for an unknown hub.</summary>
        public bool IsEventFlagSet(string hubId)
        {
            return hubId != null && _hubs.TryGetValue(hubId, out var hub) && hub.Flag;
        }

        /// <summary>Checks track docking against hubId; returns true only at that held hub node.</summary>
        private bool CanEdit(string hubId)
        {
            var node = _track.CurrentNode;
            return _track.IsHeld && node != null && node.Kind == TrackNodeKind.Hub && string.Equals(node.HubId, hubId, StringComparison.Ordinal);
        }

        /// <summary>Looks up hubId and validates slotIndex, returning its owned state when valid.</summary>
        private bool TryGetSlot(string hubId, int slotIndex, out HubState hub)
        {
            hub = null;
            return hubId != null && _hubs.TryGetValue(hubId, out hub) && slotIndex >= 0 && slotIndex < hub.Slots.Length;
        }

        /// <summary>Checks values for an ordinal match with target; returns whether it is present.</summary>
        private static bool Contains(IReadOnlyList<string> values, string target)
        {
            for (int i = 0; i < values.Count; i++) if (string.Equals(values[i], target, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Compares every slot with hub's ordered data pattern, updates completion and emits only flag rising edges.</summary>
        private void Evaluate(HubState hub)
        {
            bool complete = true;
            for (int i = 0; i < hub.Slots.Length; i++)
                if (!string.Equals(hub.Slots[i], hub.Definition.Pattern[i], StringComparison.Ordinal)) { complete = false; break; }
            bool previousFlag = hub.Flag;
            hub.Complete = complete;
            hub.Flag = complete || (hub.Definition.FlagSticky && previousFlag);
            if (hub.Flag && !previousFlag) EventFlagRaised?.Invoke(hub.Definition.HubId);
        }

        private sealed class HubState
        {
            public HabitatDefinition Definition { get; }
            public string[] Slots { get; }
            public bool Complete { get; set; }
            public bool Flag { get; set; }

            /// <summary>Creates empty slots sized by definition and initializes unset completion and flag state.</summary>
            public HubState(HabitatDefinition definition)
            {
                Definition = definition;
                Slots = new string[definition.SlotCount];
            }
        }
    }
}
