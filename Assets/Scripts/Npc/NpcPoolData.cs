using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Npc
{
    /// <summary>JSON adapter separating Unity serialization from the pure C# recruitment model.</summary>
    [Serializable]
    public sealed class NpcPoolData
    {
        [Header("Recruitment data")]
        [SerializeField] private float _joinChance;
        [SerializeField] private Entry[] _npcs;
        private IReadOnlyList<NpcDefinition> _definitions;

        /// <summary>Probability configured in the external pool JSON.</summary>
        public float JoinChance => _joinChance;
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
        private sealed class Entry
        {
            [Header("NPC identity and condition")]
            [SerializeField] private string _id;
            [SerializeField] private string _name;
            [SerializeField] private string _quote;
            [SerializeField] private string[] _eligibleHubIds;
            [SerializeField] private bool _requiresCompleteHabitat;
            [Tooltip("Story 016: habitat type id that must be complete at the hub (empty = Story 003 rule).")]
            [SerializeField] private string _habitatTypeId;
            [Tooltip("Story 017: extra dialogue lines after _quote (Korean, 2 recommended).")]
            [SerializeField] private string[] _lines;

            /// <summary>Copies serialized identity, text and condition fields into a validated immutable NPC definition.</summary>
            public NpcDefinition ToDefinition() => new NpcDefinition(_id, _name, _quote, _eligibleHubIds ?? Array.Empty<string>(), _requiresCompleteHabitat, _habitatTypeId, _lines);
        }
    }
}
