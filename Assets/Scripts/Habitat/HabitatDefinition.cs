using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IronRing.Habitat
{
    /// <summary>Immutable data for one hub; pattern order maps directly to slot indices and null requires an empty slot.</summary>
    public sealed class HabitatDefinition
    {
        private readonly ReadOnlyCollection<string> _allowedResources;
        private readonly ReadOnlyCollection<string> _pattern;

        /// <summary>Stable configured hub identifier.</summary>
        public string HubId { get; }
        /// <summary>Number of editable resource slots.</summary>
        public int SlotCount { get; }
        /// <summary>Whether a raised flag survives breaking the combo.</summary>
        public bool FlagSticky { get; }
        /// <summary>Resource identifiers permitted in this hub's slots.</summary>
        public IReadOnlyList<string> AllowedResources => _allowedResources;
        /// <summary>Required resource for each slot; null means the slot must remain empty.</summary>
        public IReadOnlyList<string> Pattern => _pattern;

        /// <summary>Copies hubId, slotCount, allowedResources, pattern and flagSticky into validated immutable configuration.</summary>
        public HabitatDefinition(string hubId, int slotCount, IEnumerable<string> allowedResources, IEnumerable<string> pattern, bool flagSticky)
        {
            if (string.IsNullOrWhiteSpace(hubId)) throw new ArgumentException("Hub id is required.", nameof(hubId));
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
            if (allowedResources == null) throw new ArgumentNullException(nameof(allowedResources));
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            var allowed = new List<string>(allowedResources);
            var required = new List<string>(pattern);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in allowed)
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new ArgumentException("Allowed resource ids must be unique and nonempty.");
            if (required.Count != slotCount) throw new ArgumentException("Pattern must define every slot.");
            bool hasResource = false;
            for (int i = 0; i < required.Count; i++)
            {
                if (string.IsNullOrEmpty(required[i])) { required[i] = null; continue; }
                if (!ids.Contains(required[i])) throw new ArgumentException("Pattern resource is not allowed.");
                hasResource = true;
            }
            if (!hasResource) throw new ArgumentException("Pattern must require a resource.");
            HubId = hubId;
            SlotCount = slotCount;
            FlagSticky = flagSticky;
            _allowedResources = allowed.AsReadOnly();
            _pattern = required.AsReadOnly();
        }
    }
}
