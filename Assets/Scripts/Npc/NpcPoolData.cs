using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Npc
{
    /// <summary>
    /// JSON adapter separating Unity serialization from the pure C# recruitment model.
    /// Story 023 adds the passenger settings (`_maxPassengers`, `_patienceLaps`, `_maxDisembarkPerVisit`, `_passengerSeed`, toast formats)
    /// and per-NPC passenger fields (`_hubIds`, `_origin`, `_originName`, `_chance`, `_boardConditions`, `_conditions`, `_proposal`, `_hint`).
    /// </summary>
    [Serializable]
    public sealed class NpcPoolData
    {
        [Header("Recruitment data")]
        [SerializeField] private float _joinChance;
        [Header("Passengers (Story 023)")]
        [SerializeField] private int _maxPassengers = 3;
        [SerializeField] private int _patienceLaps = 3;
        [SerializeField] private int _maxDisembarkPerVisit;
        [SerializeField] private int _passengerSeed = 2311;
        [SerializeField] private float _toastSeconds = 4f;
        [SerializeField] private string _boardToastFormat = "{0} {1}에서 탔다: \"{2}\" {3}";
        [SerializeField] private string _disembarkToastFormat = "열차에서 {0} 내렸다 · {1} 출신";
        [SerializeField] private string _leaveToastFormat = "{0} 열차에서 내려 떠났다";
        [SerializeField] private Entry[] _npcs;
        private IReadOnlyList<NpcDefinition> _definitions;

        /// <summary>Probability configured in the external pool JSON (legacy roll used only when no passenger system is attached).</summary>
        public float JoinChance => _joinChance;
        /// <summary>Seats on the train.</summary>
        public int MaxPassengers => Mathf.Max(0, _maxPassengers);
        /// <summary>Laps a passenger rides without finding a home before leaving at the next waypoint; 0 = forever.</summary>
        public int PatienceLaps => Mathf.Max(0, _patienceLaps);
        /// <summary>Most passengers that disembark per hub visit; 0 = unlimited.</summary>
        public int MaxDisembarkPerVisit => Mathf.Max(0, _maxDisembarkPerVisit);
        /// <summary>Seed for the boarding rolls (deterministic).</summary>
        public int PassengerSeed => _passengerSeed;
        /// <summary>Seconds each passenger toast stays up.</summary>
        public float ToastSeconds => _toastSeconds > 0f ? _toastSeconds : 4f;
        /// <summary>{0} job name, {1} origin name, {2} proposal, {3} hint.</summary>
        public string BoardToastFormat => _boardToastFormat;
        /// <summary>{0} job name, {1} origin name.</summary>
        public string DisembarkToastFormat => _disembarkToastFormat;
        /// <summary>{0} job name.</summary>
        public string LeaveToastFormat => _leaveToastFormat;
        /// <summary>Validated immutable NPC definitions loaded from the JSON.</summary>
        public IReadOnlyList<NpcDefinition> Definitions => _definitions;

        /// <summary>Story 017: definition for npcId, or null when the id is not in the pool.</summary>
        public NpcDefinition Find(string npcId)
        {
            if (npcId == null || _definitions == null) return null;
            foreach (NpcDefinition npc in _definitions)
                if (npc.Id == npcId) return npc;
            return null;
        }

        /// <summary>Deserializes JSON input and validates all pool values, returning immutable definitions and their configured chance.</summary>
        public static NpcPoolData FromJson(string json)
        {
            var data = JsonUtility.FromJson<NpcPoolData>(json);
            if (data == null || data._npcs == null || float.IsNaN(data._joinChance) || data._joinChance < 0f || data._joinChance > 1f)
                throw new ArgumentException("NPC JSON must contain a pool and chance in [0,1].", nameof(json));
            var definitions = new List<NpcDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in data._npcs)
            {
                var npc = entry.ToDefinition();
                if (!ids.Add(npc.Id)) throw new ArgumentException("Duplicate NPC id in JSON.", nameof(json));
                definitions.Add(npc);
            }
            data._definitions = definitions.AsReadOnly();
            return data;
        }

        [Serializable]
        private sealed class ConditionEntry
        {
            [SerializeField] private string _type;
            [SerializeField] private string _value;

            /// <summary>Copies the serialized pair.</summary>
            public NpcCondition ToCondition() => new NpcCondition(_type, _value);
        }

        [Serializable]
        private sealed class Entry
        {
            [Header("NPC identity and condition")]
            [SerializeField] private string _id;
            [SerializeField] private string _name;
            [SerializeField] private string _quote;
            [Tooltip("Story 023: hubs this NPC settles in (hidden). Overrides _eligibleHubIds when non-empty.")]
            [SerializeField] private string[] _hubIds;
            [SerializeField] private string[] _eligibleHubIds;
            [SerializeField] private bool _requiresCompleteHabitat;
            [Tooltip("Story 016: habitat type id that must be complete at the hub (empty = Story 003 rule).")]
            [SerializeField] private string _habitatTypeId;
            [Tooltip("Story 017: extra dialogue lines after _quote (Korean, 2 recommended).")]
            [SerializeField] private string[] _lines;
            [Header("Passenger (Story 023)")]
            [SerializeField] private string _origin;
            [SerializeField] private string _originName;
            [SerializeField] private float _chance = -1f;
            [SerializeField] private ConditionEntry[] _boardConditions;
            [SerializeField] private ConditionEntry[] _conditions;
            [SerializeField] private string _proposal;
            [SerializeField] private string _hint;

            /// <summary>Copies serialized identity, text and condition fields into a validated immutable NPC definition.</summary>
            public NpcDefinition ToDefinition()
            {
                string[] hubs = _hubIds != null && _hubIds.Length > 0 ? _hubIds : _eligibleHubIds ?? Array.Empty<string>();
                NpcPassengerProfile passenger = _chance >= 0f
                    ? new NpcPassengerProfile(_origin, _originName, _chance, Convert(_boardConditions), Convert(_conditions), _proposal, _hint)
                    : null;
                return new NpcDefinition(_id, _name, _quote, hubs, _requiresCompleteHabitat, _habitatTypeId, _lines, passenger);
            }

            private static List<NpcCondition> Convert(ConditionEntry[] entries)
            {
                var list = new List<NpcCondition>();
                foreach (ConditionEntry e in entries ?? Array.Empty<ConditionEntry>()) if (e != null) list.Add(e.ToCondition());
                return list;
            }
        }
    }
}
