using System;
using System.Collections.Generic;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Npc
{
    /// <summary>Run-scoped unique recruitment; invoke once per arrival, never each frame or flag update.</summary>
    public sealed class NpcRecruiter : INpcRecruiter
    {
        private readonly List<NpcDefinition> _pool;
        private readonly float _joinChance;
        private readonly IHabitatSystem _habitats;
        private readonly IRandomSource _random;
        private readonly Func<IHabitatClusters> _clusters;
        private readonly HashSet<string> _joined = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _members = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private bool _processingVisit;

        /// <summary>Publishes hub and NPC ids after membership has been committed.</summary>
        public event Action<string, string> NpcJoined;

        /// <summary>Copies validated pool and chance inputs and stores injected habitat and randomness dependencies for a new run.</summary>
        /// <param name="clusters">Story 016: completed-habitat source queried at visit time; null reads GameServices.Get&lt;IHabitatClusters&gt;().</param>
        public NpcRecruiter(IEnumerable<NpcDefinition> pool, float joinChance, IHabitatSystem habitats, IRandomSource random,
            Func<IHabitatClusters> clusters = null)
        {
            _clusters = clusters ?? GameServices.Get<IHabitatClusters>;
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (float.IsNaN(joinChance) || joinChance < 0f || joinChance > 1f) throw new ArgumentOutOfRangeException(nameof(joinChance));
            _habitats = habitats ?? throw new ArgumentNullException(nameof(habitats));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _pool = new List<NpcDefinition>(pool);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var npc in _pool)
                if (npc == null || !ids.Add(npc.Id)) throw new ArgumentException("NPC definitions must have unique ids.", nameof(pool));
            _joinChance = joinChance;
        }

        /// <summary>Processes one hubId arrival with at most one chance roll and one join; ignores reentrant calls during this visit.</summary>
        public void OnHubVisited(string hubId)
        {
            if (_processingVisit || string.IsNullOrEmpty(hubId)) return;
            _processingVisit = true;
            try
            {
                if (!_habitats.IsEventFlagSet(hubId)) return;
                var candidates = new List<NpcDefinition>();
                foreach (var npc in _pool)
                {
                    if (_joined.Contains(npc.Id)) continue;
                    if (IsEligible(npc, hubId)) candidates.Add(npc);
                }
                if (candidates.Count == 0 || _random.NextFloat() >= _joinChance) return;
                var selected = candidates[candidates.Count == 1 ? 0 : _random.NextInt(0, candidates.Count)];
                _joined.Add(selected.Id);
                if (!_members.TryGetValue(hubId, out var members))
                {
                    members = new List<string>();
                    _members.Add(hubId, members);
                }
                members.Add(selected.Id);
                NpcJoined?.Invoke(hubId, selected.Id);
            }
            finally { _processingVisit = false; }
        }

        /// <summary>
        /// Eligibility (the only rule Story 016 changes). Hub list: the NPC's hubs, or any hub when it names a habitat type and lists none.
        /// Habitat: with a habitat type id and a cluster source, that type must be complete at hubId now (each completed type's NPCs
        /// become candidates); otherwise the Story 003 rule (RequiresCompleteHabitat → IsHabitatComplete).
        /// </summary>
        private bool IsEligible(NpcDefinition npc, string hubId)
        {
            bool allowed = npc.HabitatTypeId != null && npc.EligibleHubIds.Count == 0;
            foreach (string eligibleHub in npc.EligibleHubIds)
                if (eligibleHub == hubId) { allowed = true; break; }
            if (!allowed) return false;
            IHabitatClusters clusters = npc.HabitatTypeId != null ? _clusters() : null;
            if (clusters == null) return !npc.RequiresCompleteHabitat || _habitats.IsHabitatComplete(hubId);
            foreach (HabitatCluster cluster in clusters.CompletedAt(hubId))
                if (cluster != null && cluster.TypeId == npc.HabitatTypeId) return true;
            return false;
        }

        /// <summary>Returns a read-only snapshot of joined ids for hubId; unknown hubs return an empty list.</summary>
        public IReadOnlyList<string> NpcsAt(string hubId) => hubId != null && _members.TryGetValue(hubId, out var members)
            ? Array.AsReadOnly(members.ToArray()) : Array.Empty<string>();
    }
}
