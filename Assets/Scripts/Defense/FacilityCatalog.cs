using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Defense
{
    /// <summary>JSON adapter for facility rules; core construction remains independent of Unity.</summary>
    [Serializable]
    public sealed class FacilityCatalog
    {
        [Header("Construction Rules")]
        [SerializeField] private int _perHubLimit;
        [SerializeField] private bool _allowBuildInRuins;
        [SerializeField] private Entry[] _facilities;

        /// <summary>Maximum number of facilities allowed independently at each hub.</summary>
        public int PerHubLimit => _perHubLimit;
        /// <summary>Whether ruined hubs accept new construction.</summary>
        public bool AllowBuildInRuins => _allowBuildInRuins;

        /// <summary>Parses json text and returns a validated catalog with immutable facility definitions.</summary>
        public static FacilityCatalog Parse(string json)
        {
            var catalog = JsonUtility.FromJson<FacilityCatalog>(json);
            if (catalog == null || catalog._perHubLimit < 0 || catalog._facilities == null) throw new ArgumentException("Invalid facility catalog.");
            catalog.CreateDefinitions();
            return catalog;
        }

        /// <summary>Converts serialized entries into validated immutable definitions and returns a read-only list.</summary>
        public IReadOnlyList<FacilityDefinition> CreateDefinitions()
        {
            var result = new List<FacilityDefinition>();
            var ids = new HashSet<string>();
            foreach (var entry in _facilities)
            {
                var costs = new Dictionary<string, int>();
                foreach (var cost in entry.Costs) costs.Add(cost.ResourceId, cost.Amount);
                if (!ids.Add(entry.Id)) throw new ArgumentException("Duplicate facility id.");
                result.Add(new FacilityDefinition(entry.Id, costs, entry.CombatBonus, entry.ProsperityDelta, entry.IsProsperityFacility));
            }
            return result.AsReadOnly();
        }

        [Serializable]
        private sealed class Entry
        {
            [Header("Facility")]
            [SerializeField] private string _id;
            [SerializeField] private Cost[] _costs;
            [SerializeField] private float _combatBonus;
            [SerializeField] private float _prosperityDelta;
            [SerializeField] private bool _isProsperityFacility;
            public string Id => _id;
            public Cost[] Costs => _costs;
            public float CombatBonus => _combatBonus;
            public float ProsperityDelta => _prosperityDelta;
            public bool IsProsperityFacility => _isProsperityFacility;
        }

        [Serializable]
        private sealed class Cost
        {
            [Header("Resource Cost")]
            [SerializeField] private string _resourceId;
            [SerializeField] private int _amount;
            public string ResourceId => _resourceId;
            public int Amount => _amount;
        }
    }
}
