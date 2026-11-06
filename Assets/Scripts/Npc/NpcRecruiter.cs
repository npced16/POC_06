using System;
using System.Collections.Generic;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Npc
{
    /// <summary>
    /// Run-scoped unique recruitment; invoke once per arrival, never each frame or flag update.
    /// Story 023: when a passenger system sets VisitHandler, a hub visit is handed to it and NPCs join ONLY through
    /// TryJoin (a passenger disembarking). Without a handler (standalone Story 003 demo) the legacy single roll applies.
    /// An NPC joins at most once per run either way.
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class NpcRecruiter : INpcRecruiter, ISaveParticipant
    {
        private readonly List<NpcDefinition> _pool;
        private readonly float _joinChance;
        private readonly IHabitatSystem _habitats;
        private readonly IRandomSource _random;
        private readonly Func<IHabitatClusters> _clusters;
        private readonly HashSet<string> _joined = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _joinOrder = new List<string>();
        private readonly Dictionary<string, List<string>> _members = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private bool _processingVisit;

        /// <summary>Publishes hub and NPC ids after membership has been committed.</summary>
        public event Action<string, string> NpcJoined;
        /// <summary>Story 023: npcId, hubId — raised right after NpcJoined (homes are assigned by then); presentation plays the arrival walk.</summary>
        public event Action<string, string> NpcArrived;
        /// <summary>스펙 005. hubId, npcId. 합류 기록이 지워진 뒤.</summary>
        public event Action<string, string> NpcWithdrawn;

        /// <summary>Story 023: receives every hub visit instead of the legacy roll (set by Passengers). Null keeps the legacy roll.</summary>
        public Action<string> VisitHandler { get; set; }

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

        /// <summary>True once npcId has joined any hub this run.</summary>
        public bool IsJoined(string npcId) => npcId != null && _joined.Contains(npcId);

        /// <summary>Processes one hub arrival: hands it to VisitHandler when set, otherwise one legacy roll. Ignores reentrant calls.</summary>
        public void OnHubVisited(string hubId)
        {
            if (_processingVisit || string.IsNullOrEmpty(hubId)) return;
            _processingVisit = true;
            try
            {
                if (VisitHandler != null) { VisitHandler(hubId); return; }
                if (!_habitats.IsEventFlagSet(hubId)) return;
                var candidates = new List<NpcDefinition>();
                foreach (var npc in _pool)
                {
                    if (_joined.Contains(npc.Id)) continue;
                    if (IsEligible(npc, hubId)) candidates.Add(npc);
                }
                if (candidates.Count == 0 || _random.NextFloat() >= _joinChance) return;
                var selected = candidates[candidates.Count == 1 ? 0 : _random.NextInt(0, candidates.Count)];
                Commit(selected.Id, hubId);
            }
            finally { _processingVisit = false; }
        }

        /// <summary>Story 023: commits npcId to hubId (a disembarking passenger). False when unknown or already joined.</summary>
        public bool TryJoin(string npcId, string hubId)
        {
            if (string.IsNullOrEmpty(hubId) || IsJoined(npcId) || _pool.Find(n => n.Id == npcId) == null) return false;
            Commit(npcId, hubId);
            return true;
        }

        /// <summary>
        /// 스펙 005. npcId의 합류 기록을 _joined, _joinOrder, _members에서 지우고 NpcWithdrawn을 낸다.
        /// 합류하지 않은 npcId면 아무것도 바꾸지 않고 false를 반환한다.
        /// </summary>
        public bool TryWithdraw(string npcId)
        {
            if (!IsJoined(npcId)) return false;
            string hubId = HubOf(npcId);
            _joined.Remove(npcId);
            _joinOrder.Remove(npcId);
            _members[hubId].Remove(npcId);
            NpcWithdrawn?.Invoke(hubId, npcId);
            return true;
        }

        /// <summary>npcId를 hubId에 합류시키고 도감 발견 기록을 저장한 뒤 합류 사건을 알린다.</summary>
        private void Commit(string npcId, string hubId)
        {
            _joined.Add(npcId);
            _joinOrder.Add(npcId);
            PassengerDiscovery.Remember(npcId);
            if (!_members.TryGetValue(hubId, out var members))
            {
                members = new List<string>();
                _members.Add(hubId, members);
            }
            members.Add(npcId);
            NpcJoined?.Invoke(hubId, npcId);
            NpcArrived?.Invoke(npcId, hubId);
        }

        /// <summary>
        /// Legacy eligibility. Hub list: the NPC's hubs, or any hub when it names a habitat type and lists none.
        /// Habitat: with a habitat type id and a cluster source, that type must be complete at hubId now;
        /// otherwise the Story 003 rule (RequiresCompleteHabitat → IsHabitatComplete).
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

        /// <summary>Story 023: every NPC id that wants habitatTypeId (hub-independent, pool order).</summary>
        public IReadOnlyList<string> CandidatesOf(string habitatTypeId)
        {
            var list = new List<string>();
            foreach (NpcDefinition npc in _pool) if (npc.HabitatTypeId != null && npc.HabitatTypeId == habitatTypeId) list.Add(npc.Id);
            return list.AsReadOnly();
        }

        /// <summary>Story 023: joined NPC ids that want habitatTypeId, in join order.</summary>
        public IReadOnlyList<string> JoinedOf(string habitatTypeId)
        {
            var list = new List<string>();
            foreach (string id in _joinOrder)
            {
                NpcDefinition npc = _pool.Find(n => n.Id == id);
                if (npc != null && npc.HabitatTypeId == habitatTypeId) list.Add(id);
            }
            return list.AsReadOnly();
        }

        // 스펙 003: 저장 슬롯용 합류한 NPC 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "npcRecruiter";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 60;

        /// <summary>
        /// 합류 순서 _joinOrder와 NPC마다 합류한 거점을 JSON으로 돌려준다. _joined와 거점별 _members는 이 둘에서 다시 만든다.
        /// 상태를 바꾸지 않는다.
        /// </summary>
        public string Capture()
        {
            JoinSave state = new JoinSave { NpcIds = _joinOrder.ToArray(), HubIds = new string[_joinOrder.Count] };
            for (int i = 0; i < _joinOrder.Count; i++) state.HubIds[i] = HubOf(_joinOrder[i]);
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 합류 기록을 바꾼다. 풀에 없는 NPC는 건너뛴다. 새로 합류 상태가 된 NPC마다 NpcJoined를 낸다
        /// (도착 걸음 연출 NpcArrived는 내지 않는다).
        /// </summary>
        public void Restore(string json)
        {
            JoinSave state = UnityEngine.JsonUtility.FromJson<JoinSave>(json);
            HashSet<string> before = new HashSet<string>(_joined, StringComparer.Ordinal);
            _joined.Clear();
            _joinOrder.Clear();
            _members.Clear();
            List<KeyValuePair<string, string>> joined = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < state.NpcIds.Length && i < state.HubIds.Length; i++)
            {
                string npcId = state.NpcIds[i];
                string hubId = state.HubIds[i];
                if (string.IsNullOrEmpty(hubId) || _joined.Contains(npcId) || _pool.Find(n => n.Id == npcId) == null) continue;
                _joined.Add(npcId);
                _joinOrder.Add(npcId);
                if (!_members.TryGetValue(hubId, out List<string> members))
                {
                    members = new List<string>();
                    _members.Add(hubId, members);
                }
                members.Add(npcId);
                joined.Add(new KeyValuePair<string, string>(hubId, npcId));
            }
            foreach (KeyValuePair<string, string> pair in joined)
            {
                if (!before.Contains(pair.Value)) NpcJoined?.Invoke(pair.Key, pair.Value);
            }
        }

        /// <summary>npcId가 합류한 거점 id를 _members에서 찾아 돌려준다. 없으면 빈 문자열.</summary>
        private string HubOf(string npcId)
        {
            foreach (KeyValuePair<string, List<string>> pair in _members)
            {
                if (pair.Value.Contains(npcId)) return pair.Key;
            }
            return string.Empty;
        }

        /// <summary>스펙 003. 저장 형식: 합류 순서의 NPC id와 합류 거점 id의 짝(같은 순번).</summary>
        [Serializable]
        private sealed class JoinSave
        {
            public string[] NpcIds = Array.Empty<string>();
            public string[] HubIds = Array.Empty<string>();
        }
    }
}
