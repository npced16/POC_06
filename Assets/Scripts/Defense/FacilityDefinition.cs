using System;
using System.Collections.Generic;

namespace IronRing.Defense
{
    /// <summary>Immutable facility costs and effects loaded by the data adapter.</summary>
    public sealed class FacilityDefinition
    {
        /// <summary>Stable facility identifier from the catalog.</summary>
        public string Id { get; }
        /// <summary>Resource amounts charged once when the facility is built.</summary>
        public IReadOnlyDictionary<string, int> Costs { get; }
        /// <summary>Persistent combat strength added at the facility's hub.</summary>
        public float CombatBonus { get; }
        /// <summary>Prosperity change applied once upon construction.</summary>
        public float ProsperityDelta { get; }
        /// <summary>Whether presentation identifies this as a prosperity facility.</summary>
        public bool IsProsperityFacility { get; }

        /// <summary>Copies validated id, costs and effects into immutable facility data.</summary>
        public FacilityDefinition(string id, IDictionary<string, int> costs, float combatBonus, float prosperityDelta, bool isProsperityFacility)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Facility id is required.");
            var copy = new Dictionary<string, int>(costs);
            foreach (var cost in copy)
                if (string.IsNullOrWhiteSpace(cost.Key) || cost.Value < 0) throw new ArgumentException("Invalid facility cost.");
            if (float.IsNaN(combatBonus) || float.IsInfinity(combatBonus) || float.IsNaN(prosperityDelta) || float.IsInfinity(prosperityDelta))
                throw new ArgumentException("Effects must be finite.");
            Id = id;
            Costs = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(copy);
            CombatBonus = combatBonus;
            ProsperityDelta = prosperityDelta;
            IsProsperityFacility = isProsperityFacility;
        }
    }
}
