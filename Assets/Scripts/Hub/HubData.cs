using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Hub
{
    /// <summary>
    /// Unity JSON wire-format boundary (HubProfiles.json) that converts authorable profiles into pure immutable definitions.
    /// Schema: _deadlineLaps (laps before the first settlement, &gt;= 1); per hub: bounds (_minimum, _maximum, _initial,
    /// _ruinThreshold, _settledThreshold), per-lap contributions (_basePerLap, _perNpc, _perFacility, _perPlacedItem,
    /// _perHabitatCell (Story 028), _habitatBonus) and Story 004 profile multipliers (_decayScale, _npcScale, _facilityScale).
    /// </summary>
    [Serializable]
    public sealed class HubData
    {
        [Header("Run rules (Story 014)")]
        [SerializeField] private int _deadlineLaps;
        [Header("Hub profiles")]
        [SerializeField] private HubProfile[] _hubs;

        /// <summary>Parses json and returns validated definitions with all multipliers and the shared run rules resolved from data.</summary>
        public static IReadOnlyList<HubDefinition> Parse(string json)
        {
            HubData data = JsonUtility.FromJson<HubData>(json);
            if (data == null || data._hubs == null || data._hubs.Length == 0)
                throw new ArgumentException("Hub data is empty.", nameof(json));
            var rules = new HubRunRules(data._deadlineLaps);
            var definitions = new List<HubDefinition>();
            foreach (HubProfile profile in data._hubs) definitions.Add(profile.ToDefinition(rules));
            return definitions.AsReadOnly();
        }

        [Serializable]
        private sealed class HubProfile
        {
            [Header("Identity and bounds")]
            [SerializeField] private string _id;
            [SerializeField] private float _minimum;
            [SerializeField] private float _maximum;
            [SerializeField] private float _initial;
            [SerializeField] private float _ruinThreshold;
            [SerializeField] private float _settledThreshold;
            [Header("Growth contributions")]
            [SerializeField] private float _basePerLap;
            [SerializeField] private float _perNpc;
            [SerializeField] private float _perFacility;
            [SerializeField] private float _perPlacedItem;
            [Tooltip("Story 028: prosperity per lap for each cell inside a completed habitat cluster.")]
            [SerializeField] private float _perHabitatCell = 1f;
            [SerializeField] private float _habitatBonus;
            [Header("Profile multipliers")]
            [SerializeField] private float _decayScale;
            [SerializeField] private float _npcScale;
            [SerializeField] private float _facilityScale;
            [Header("Settlement (Story 023)")]
            [Tooltip("true: a completed habitat also settles the hub. false: settle only at _settledThreshold.")]
            [SerializeField] private bool _settleByHabitat;

            /// <summary>Resolves serialized growth multipliers, bounds and phase threshold into a validated immutable hub definition.</summary>
            public HubDefinition ToDefinition(HubRunRules rules) => new HubDefinition(_id, _minimum, _maximum, _initial,
                _ruinThreshold, _basePerLap * _decayScale, _perNpc * _npcScale, _perFacility * _facilityScale,
                _perPlacedItem, _habitatBonus, _settledThreshold, rules, _settleByHabitat, _perHabitatCell);
        }
    }
}
