using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Hub
{
    /// <summary>
    /// Unity JSON wire-format boundary (HubProfiles.json) that converts authorable profiles into pure immutable definitions.
    /// Schema: _deadlineLaps (laps before the first settlement, &gt;= 1); per hub: bounds (_minimum, _maximum, _initial,
    /// _ruinThreshold, _settledThreshold), per-lap contributions (_basePerLap, _perNpc, _perPlacedItem,
    /// _perHabitatCell (Story 028), _habitatBonus) and Story 004 profile multipliers (_decayScale, _npcScale).
    /// </summary>
    [Serializable]
    public sealed class HubData
    {
        [Header("Run rules (Story 014)")]
        [SerializeField] private int _deadlineLaps;
        [Tooltip("Story 030: end the run as Victory the moment every hub is Settled.")]
        [SerializeField] private bool _earlyWin;
        [Tooltip("Story 042: the town hub phase and the run outcome follow the town tier (TownTiers.json). false = old prosperity thresholds.")]
        [SerializeField] private bool _useTownTiers;
        [Tooltip("Story 042/045: prosperity per lap for each resident of the town hub.")]
        [SerializeField] private float _perResident;
        [Tooltip("QA 2026-11-01: a Cluster/Set building counts at most ceil(RequiredCount x this) cells for prosperity. Absent or <= 0 = 1.5.")]
        [SerializeField] private float _cellCapRatio = 1.5f;
        [Header("Hub profiles")]
        [SerializeField] private HubProfile[] _hubs;

        /// <summary>Parses json and returns validated definitions with all multipliers and the shared run rules resolved from data.</summary>
        public static IReadOnlyList<HubDefinition> Parse(string json)
        {
            HubData data = JsonUtility.FromJson<HubData>(json);
            if (data == null || data._hubs == null || data._hubs.Length == 0)
                throw new ArgumentException("Hub data is empty.", nameof(json));
            var rules = new HubRunRules(data._deadlineLaps, data._earlyWin, data._useTownTiers, data._perResident, data._cellCapRatio);
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
            [SerializeField] private float _perPlacedItem;
            [Tooltip("Story 028: prosperity per lap for each cell inside a completed habitat cluster.")]
            [SerializeField] private float _perHabitatCell = 1f;
            [SerializeField] private float _habitatBonus;
            [Header("Profile multipliers")]
            [SerializeField] private float _decayScale;
            [SerializeField] private float _npcScale;
            [Header("Settlement (Story 023)")]
            [Tooltip("true: a completed habitat also settles the hub. false: settle only at _settledThreshold.")]
            [SerializeField] private bool _settleByHabitat;

            /// <summary>Resolves serialized growth multipliers, bounds and phase threshold into a validated immutable hub definition.</summary>
            public HubDefinition ToDefinition(HubRunRules rules) => new HubDefinition(_id, _minimum, _maximum, _initial,
                _ruinThreshold, _basePerLap * _decayScale, _perNpc * _npcScale,
                _perPlacedItem, _habitatBonus, _settledThreshold, rules, _settleByHabitat, _perHabitatCell);
        }
    }
}
