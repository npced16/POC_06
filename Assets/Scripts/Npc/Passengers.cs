using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Npc
{
    /// <summary>
    /// Story 023 (story-023-npc-rosters-conditions-arrival.md, AC "탑승"·"하차"·"제안과 힌트"). NPCs are train passengers.
    /// Boarding: when a waypoint encounter ends with EventResolved or Victory (IWaypointResolver.EncounterEnded, the signal the
    /// Story 021 codex uses), every NPC not joined and not aboard whose origin is that waypoint (or "*") and whose boarding
    /// conditions hold rolls its own chance in pool order; winners take free seats (NpcPoolData.MaxPassengers). Seeded.
    /// Leaving: at every ended encounter (defeat included) a passenger that rode PatienceLaps laps (ITrackRuntime.Lap) leaves first
    /// and returns to the pool. Disembarking: NpcRecruiter forwards each hub visit here; a passenger whose hubs include the hub,
    /// whose habitat type is complete there and whose hidden conditions all hold disembarks and joins through NpcRecruiter.TryJoin.
    /// Story 032 (POC 1 station): NpcPoolData.IgnoreHubRoster drops the roster check on disembarking and NpcPoolData.IgnoreOrigin
    /// drops the origin check on boarding; boarding conditions are judged at the NPC's first hub when it is in the run, else the first run hub.
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class Passengers : IPassengers, IDisposable, ISaveParticipant
    {
        /// <summary>Waypoint id used for boards made by demo helpers.</summary>
        public const string DemoWaypoint = "demo";

        private sealed class Rider
        {
            public string Id;
            public string WaypointId;
            public int BoardLap;
        }

        private readonly NpcPoolData _pool;
        private readonly NpcRecruiter _recruiter;
        private readonly ITrackRuntime _track;
        private readonly IWaypointResolver _waypoints;
        private readonly Func<IHabitatClusters> _clusters;
        private readonly IHubStateSystem _hubs;
        private readonly Func<IHubDevelopment> _development;
        private readonly Random _random;
        private readonly List<Rider> _riders = new List<Rider>();
        private readonly Dictionary<string, int> _visits = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _disposed;
        private int _boardRolls;

        /// <summary>npcId, waypointId.</summary>
        public event Action<string, string> Boarded;
        /// <summary>npcId, hubId.</summary>
        public event Action<string, string> Disembarked;
        /// <summary>npcId that left the train without a home.</summary>
        public event Action<string> Left;

        /// <summary>Subscribes to waypoint results and takes over the recruiter's hub visits. Lazy sources may return null.</summary>
        public Passengers(NpcPoolData pool, NpcRecruiter recruiter, ITrackRuntime track, IWaypointResolver waypoints,
            Func<IHabitatClusters> clusters, IHubStateSystem hubs, Func<IHubDevelopment> development)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _recruiter = recruiter ?? throw new ArgumentNullException(nameof(recruiter));
            _track = track;
            _waypoints = waypoints;
            _clusters = clusters ?? (() => null);
            _hubs = hubs;
            _development = development ?? (() => null);
            _random = new Random(pool.PassengerSeed);
            if (_waypoints != null) _waypoints.EncounterEnded += OnEncounterEnded;
            _recruiter.VisitHandler = OnHubVisited;
        }

        /// <summary>
        /// 스토리 051. false면 휴식지 이벤트 종료 때의 확률 탑승(RollBoarding)을 하지 않는다 — 탑승은 중간역 사건의 passenger 효과(TryBoardAny/TryBoard)로만.
        /// 인내 바퀴가 지난 승객이 떠나는 규칙은 그대로. LoopInstaller가 RestEvents.json _disableAutoBoarding으로 끈다.
        /// </summary>
        public bool AutoBoarding { get; set; } = true;

        /// <summary>
        /// 스토리 051. 중간역 사건 "합류": 자리가 있으면 아직 합류·탑승하지 않은 NPC 하나를 태운다. 출신지·탑승 조건을 채우는 후보를 먼저,
        /// 없으면 나머지에서(풀의 시드 난수로) 고른다. 태운 id, 자리나 후보가 없으면 null.
        /// </summary>
        public string TryBoardAny(string waypointId)
        {
            if (_disposed || _riders.Count >= _pool.MaxPassengers) return null;
            NpcDefinition pick = _pool.Find(PickCandidate(waypointId));
            if (pick == null) return null;
            Board(pick, waypointId);
            return pick.Id;
        }

        /// <summary>
        /// 중간역 승객 사건 후보를 waypointId 기준으로 고른다. 출신지·탑승 조건을 채우는 NPC를 먼저, 없으면 나머지에서 시드 난수로 고른다.
        /// 좌석 수는 보지 않으며 태우지도 않는다. 고른 id, 후보가 없으면 null을 반환한다.
        /// </summary>
        public string PickCandidate(string waypointId)
        {
            if (_disposed) return null;
            var preferred = new List<NpcDefinition>();
            var rest = new List<NpcDefinition>();
            foreach (NpcDefinition npc in _pool.Definitions)
            {
                if (npc.Passenger == null || _recruiter.IsJoined(npc.Id) || Find(npc.Id) != null) continue;
                (IsBoardCandidate(npc, waypointId) ? preferred : rest).Add(npc);
            }
            List<NpcDefinition> from = preferred.Count > 0 ? preferred : rest;
            if (from.Count == 0) return null;
            return from[_random.Next(from.Count)].Id;
        }

        /// <summary>좌석(NpcPoolData.MaxPassengers)이 모두 찼으면 true.</summary>
        public bool IsFull => _riders.Count >= _pool.MaxPassengers;

        /// <summary>열차 최대 승객 수(NpcPoolData.MaxPassengers)를 반환한다.</summary>
        public int MaxPassengers => _pool.MaxPassengers;

        /// <summary>
        /// 플레이어가 고른 승객 boardId를 waypointId에서 태운다. leaveId가 있으면 그 승객을 먼저 떠나게 하고(Left), 없으면 빈 좌석을 쓴다.
        /// 탑승 조건은 보지 않는다. 대상이 없거나 이미 합류·탑승했거나 자리가 없으면 false를 반환하고 아무것도 바꾸지 않는다.
        /// </summary>
        public bool TrySwap(string leaveId, string boardId, string waypointId)
        {
            NpcDefinition npc = _pool.Find(boardId);
            if (_disposed || npc?.Passenger == null || _recruiter.IsJoined(boardId) || Find(boardId) != null) return false;
            Rider leaving = leaveId != null ? Find(leaveId) : null;
            if (leaveId != null && leaving == null) return false;
            if (leaving == null && IsFull) return false;
            if (leaving != null)
            {
                _riders.Remove(leaving);
                UnityEngine.Debug.Log($"PASSENGER: leave {leaving.Id} (교체: {boardId})");
                Left?.Invoke(leaving.Id);
            }
            Board(npc, waypointId);
            return true;
        }

        /// <summary>npcId의 표시 이름(직업명)을 반환한다. 모르면 id를 반환한다.</summary>
        public string Name(string npcId) => _pool.Find(npcId)?.Name ?? npcId;

        /// <summary>npcId의 「건물 완성 → 인물 합류 · 직무 +값」 안내를 반환한다. 모르면 빈 문자열.</summary>
        public string RequestLabel(string npcId) => PassengerRequest.LabelWithWork(_pool.Find(npcId));

        /// <summary>Pool settings and texts (toast formats).</summary>
        public NpcPoolData Pool => _pool;

        /// <summary>Passenger ids in boarding order.</summary>
        public IReadOnlyList<string> PassengerIds
        {
            get
            {
                var ids = new List<string>(_riders.Count);
                foreach (Rider r in _riders) ids.Add(r.Id);
                return ids.AsReadOnly();
            }
        }

        /// <summary>Proposal line of npcId ("" if unknown).</summary>
        public string Proposal(string npcId) => _pool.Find(npcId)?.Passenger?.Proposal ?? "";
        /// <summary>Hint line of npcId ("" if unknown).</summary>
        public string Hint(string npcId) => _pool.Find(npcId)?.Passenger?.Hint ?? "";
        /// <summary>Korean origin name of npcId ("" if unknown).</summary>
        public string OriginName(string npcId) => _pool.Find(npcId)?.Passenger?.OriginName ?? "";

        /// <summary>Laps completed since npcId boarded; 0 when not aboard.</summary>
        public int LapsAboard(string npcId)
        {
            Rider rider = Find(npcId);
            return rider == null ? 0 : Math.Max(0, Lap - rider.BoardLap);
        }

        /// <summary>Hub visits counted so far (including the current docking).</summary>
        public int VisitsAt(string hubId) => hubId != null && _visits.TryGetValue(hubId, out int n) ? n : 0;

        /// <summary>
        /// Boards npcId at waypointId. Without force the normal rules apply (free seat, origin, boarding conditions; no roll).
        /// force skips them (demo/test hook) and logs that it did. False when unknown, already joined or already aboard.
        /// </summary>
        public bool TryBoard(string npcId, string waypointId, bool force)
        {
            NpcDefinition npc = _pool.Find(npcId);
            if (_disposed || npc?.Passenger == null || _recruiter.IsJoined(npcId) || Find(npcId) != null) return false;
            bool normal = _riders.Count < _pool.MaxPassengers && IsBoardCandidate(npc, waypointId);
            if (!normal && !force) return false;
            if (!normal) UnityEngine.Debug.Log($"PASSENGER: force board {npcId} at {waypointId} (규칙 무시)");
            Board(npc, waypointId);
            return true;
        }

        /// <summary>
        /// Demo helper: boards (forced) the first NPC that would disembark at hubId right now (hub allowed, habitat type complete,
        /// hidden conditions met), preferring preferredId. Returns its id, or null when nobody qualifies.
        /// </summary>
        public string BoardForHub(string hubId, string preferredId = null)
        {
            var order = new List<NpcDefinition>();
            NpcDefinition preferred = _pool.Find(preferredId);
            if (preferred != null) order.Add(preferred);
            foreach (NpcDefinition npc in _pool.Definitions) if (npc != preferred) order.Add(npc);
            foreach (NpcDefinition npc in order)
            {
                if (npc.Passenger == null || _recruiter.IsJoined(npc.Id)) continue;
                if (Find(npc.Id) != null) return npc.Id;
                if (!CanDisembark(npc, hubId, VisitsAt(hubId) + 1)) continue;
                if (TryBoard(npc.Id, DemoWaypoint, true)) return npc.Id;
            }
            UnityEngine.Debug.LogWarning($"PASSENGER: {hubId}에서 내릴 수 있는 승객 후보가 없습니다.");
            return null;
        }

        /// <summary>Unsubscribes and gives hub visits back to the recruiter's legacy rule.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_waypoints != null) _waypoints.EncounterEnded -= OnEncounterEnded;
            if (_recruiter.VisitHandler == (Action<string>)OnHubVisited) _recruiter.VisitHandler = null;
            Boarded = null; Disembarked = null; Left = null;
        }

        private int Lap => _track?.Lap ?? 0;

        private NpcConditionContext Context() => new NpcConditionContext
        {
            Hubs = _hubs, Track = _track, Clusters = _clusters(), Recruiter = _recruiter, Development = _development(), Visits = VisitsAt
        };

        private void OnEncounterEnded(ITrackNode node, EncounterOutcome outcome)
        {
            if (_disposed || node == null) return;
            DropImpatient();
            if (AutoBoarding) RollBoarding(node.Id);
        }

        private void DropImpatient()
        {
            int patience = _pool.PatienceLaps;
            if (patience <= 0) return;
            foreach (Rider rider in _riders.ToArray())
            {
                if (Lap - rider.BoardLap < patience) continue;
                _riders.Remove(rider);
                UnityEngine.Debug.Log($"PASSENGER: leave {rider.Id} ({Lap - rider.BoardLap}바퀴 탑승)");
                Left?.Invoke(rider.Id);
            }
        }

        /// <summary>
        /// Story 028 (QA S2-2). Collects every eligible candidate, shuffles them with the pool's seeded random, then moves the
        /// roster of this roll's priority hub to the front (k-th boarding roll prefers hub k, cycling through the hub ids) and
        /// rolls each candidate's chance in that order until the seats are full. Deterministic for a given seed and history.
        /// </summary>
        private void RollBoarding(string waypointId)
        {
            if (_riders.Count >= _pool.MaxPassengers) return;
            var candidates = new List<NpcDefinition>();
            foreach (NpcDefinition npc in _pool.Definitions)
                if (npc.Passenger != null && !_recruiter.IsJoined(npc.Id) && Find(npc.Id) == null && IsBoardCandidate(npc, waypointId)) candidates.Add(npc);
            string priority = PriorityHub(_boardRolls++);
            List<NpcDefinition> order = BoardingOrder(candidates, priority, _random);
            UnityEngine.Debug.Log($"PASSENGER: roll at {waypointId} priority={priority ?? "-"} candidates={order.Count}");
            foreach (NpcDefinition npc in order)
            {
                if (_riders.Count >= _pool.MaxPassengers) return;
                // 스토리 039: 휴식지 "승객 합승" 이벤트가 이 정차의 탑승 확률을 올린다(IWaypointResolver.BoardChanceBonus, 데이터).
                float bonus = IronRing.Contracts.Presentation.GameServices.Get<IronRing.Contracts.IWaypointResolver>()?.BoardChanceBonus ?? 0f;
                if (_random.NextDouble() < npc.Passenger.Chance + bonus) Board(npc, waypointId);
            }
        }

        /// <summary>스토리 048. 열린 거점 id(ILineOpener가 잠갔다고 하는 역은 뺀다). 거점이 없으면 null.</summary>
        private IReadOnlyList<string> OpenHubs()
        {
            IReadOnlyList<string> all = _hubs?.HubIds;
            ILineOpener opener = IronRing.Contracts.Presentation.GameServices.Get<ILineOpener>();
            if (all == null || opener == null || opener.LockedHubIds.Count == 0) return all;
            var open = new List<string>();
            foreach (string id in all) if (opener.IsOpen(id)) open.Add(id);
            return open;
        }

        /// <summary>Hub whose roster goes first on the given roll: hub ids in data order, cycling; null without hubs.</summary>
        private string PriorityHub(int roll)
        {
            IReadOnlyList<string> ids = OpenHubs();
            if (ids == null || ids.Count == 0) return null;
            return ids[((roll % ids.Count) + ids.Count) % ids.Count];
        }

        /// <summary>
        /// Story 028. Fisher-Yates shuffle of candidates with random, then a stable partition that puts NPCs whose first eligible
        /// hub (home roster) is priorityHub first. Returns a new list; the input is not changed.
        /// </summary>
        public static List<NpcDefinition> BoardingOrder(IReadOnlyList<NpcDefinition> candidates, string priorityHub, Random random)
        {
            var shuffled = new List<NpcDefinition>(candidates);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            if (priorityHub == null) return shuffled;
            var first = new List<NpcDefinition>();
            var rest = new List<NpcDefinition>();
            foreach (NpcDefinition npc in shuffled)
                (npc.EligibleHubIds.Count > 0 && npc.EligibleHubIds[0] == priorityHub ? first : rest).Add(npc);
            first.AddRange(rest);
            return first;
        }

        /// <summary>Origin matches (or "*") and boarding conditions hold, judged against the NPC's first hub.</summary>
        private bool IsBoardCandidate(NpcDefinition npc, string waypointId)
        {
            NpcPassengerProfile p = npc.Passenger;
            if (!_pool.IgnoreOrigin && p.Origin != "*" && p.Origin != waypointId) return false;
            return NpcConditions.AllMet(p.BoardConditions, ConditionHub(npc), Context());
        }

        /// <summary>
        /// Hub the boarding conditions are judged at: the NPC's first hub; with IgnoreHubRoster, when that hub is not in the run
        /// (not a track station) the first run hub instead, so conditions never read a hub the run does not have.
        /// </summary>
        private string ConditionHub(NpcDefinition npc)
        {
            string hub = npc.EligibleHubIds.Count > 0 ? npc.EligibleHubIds[0] : null;
            if (!_pool.IgnoreHubRoster) return hub;
            IReadOnlyList<string> ids = OpenHubs();
            if (ids == null || ids.Count == 0) return hub;
            foreach (string id in ids) if (id == hub) return hub;
            return ids[0];
        }

        /// <summary>npc를 waypointId에서 탑승시키고 도감 발견 기록에 남긴 뒤 탑승 사건을 알린다.</summary>
        private void Board(NpcDefinition npc, string waypointId)
        {
            _riders.Add(new Rider { Id = npc.Id, WaypointId = waypointId, BoardLap = Lap });
            PassengerDiscovery.Remember(npc.Id);
            UnityEngine.Debug.Log($"PASSENGER: board {npc.Id} at {waypointId} \"{npc.Passenger.Proposal}\" {npc.Passenger.Hint}");
            Boarded?.Invoke(npc.Id, waypointId);
        }

        /// <summary>NpcRecruiter.VisitHandler: counts the visit, then lets every qualifying passenger off in boarding order.</summary>
        private void OnHubVisited(string hubId)
        {
            if (_disposed || hubId == null) return;
            _visits[hubId] = VisitsAt(hubId) + 1;
            int limit = _pool.MaxDisembarkPerVisit;
            int count = 0;
            foreach (Rider rider in _riders.ToArray())
            {
                if (limit > 0 && count >= limit) break;
                NpcDefinition npc = _pool.Find(rider.Id);
                if (npc == null || !CanDisembark(npc, hubId, VisitsAt(hubId))) continue;
                _riders.Remove(rider);
                UnityEngine.Debug.Log($"PASSENGER: disembark {npc.Id} at {hubId}");
                if (_recruiter.TryJoin(npc.Id, hubId)) count++;
                Disembarked?.Invoke(npc.Id, hubId);
            }
        }

        private bool CanDisembark(NpcDefinition npc, string hubId, int visits)
        {
            if (!_pool.IgnoreHubRoster && !npc.AllowsHub(hubId)) return false;
            NpcConditionContext context = Context();
            context.Visits = h => h == hubId ? visits : VisitsAt(h);
            if (npc.HabitatTypeId != null && !NpcConditions.IsTypeComplete(context.Clusters, hubId, npc.HabitatTypeId)) return false;
            return NpcConditions.AllMet(npc.Passenger?.Conditions, hubId, context);
        }

        private Rider Find(string npcId)
        {
            foreach (Rider r in _riders) if (r.Id == npcId) return r;
            return null;
        }

        // 스펙 003: 저장 슬롯용 거점 방문 횟수와 탑승 중 승객 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "passengers";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 62;

        /// <summary>거점 방문 횟수 _visits를 거점 id 순으로, 탑승 중 승객 _riders를 탑승 순서로 JSON에 담아 돌려준다. 상태를 바꾸지 않는다.</summary>
        public string Capture()
        {
            List<string> hubs = new List<string>(_visits.Keys);
            hubs.Sort(StringComparer.Ordinal);
            VisitSave state = new VisitSave { HubIds = hubs.ToArray(), Counts = new int[hubs.Count] };
            for (int i = 0; i < hubs.Count; i++) state.Counts[i] = _visits[hubs[i]];
            // 스펙 003: 정차 중 탑승 승객(id·탄 곳·탄 바퀴) 저장 추가
            state.RiderIds = new string[_riders.Count];
            state.RiderWaypoints = new string[_riders.Count];
            state.RiderLaps = new int[_riders.Count];
            for (int i = 0; i < _riders.Count; i++)
            {
                state.RiderIds[i] = _riders[i].Id;
                state.RiderWaypoints[i] = _riders[i].WaypointId;
                state.RiderLaps[i] = _riders[i].BoardLap;
            }
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 _visits와 _riders를 바꾼다. 풀에 없거나 이미 합류했거나 중복이거나 좌석을 넘는 승객은 버린다.
        /// 해금 토스트·온보딩이 다시 뜨지 않도록 Boarded 등 알림을 내지 않는다.
        /// </summary>
        public void Restore(string json)
        {
            VisitSave state = UnityEngine.JsonUtility.FromJson<VisitSave>(json);
            _visits.Clear();
            for (int i = 0; i < state.HubIds.Length && i < state.Counts.Length; i++)
            {
                if (!string.IsNullOrEmpty(state.HubIds[i])) _visits[state.HubIds[i]] = state.Counts[i];
            }
            // 스펙 003: 정차 중 탑승 승객 복원 추가(합류 기록은 NpcRecruiter가 먼저 복원됨)
            _riders.Clear();
            int count = Math.Min(state.RiderIds.Length, Math.Min(state.RiderWaypoints.Length, state.RiderLaps.Length));
            for (int i = 0; i < count && _riders.Count < _pool.MaxPassengers; i++)
            {
                string id = state.RiderIds[i];
                if (_pool.Find(id)?.Passenger == null || _recruiter.IsJoined(id) || Find(id) != null) continue;
                _riders.Add(new Rider { Id = id, WaypointId = state.RiderWaypoints[i], BoardLap = state.RiderLaps[i] });
            }
        }

        /// <summary>스펙 003. 저장 형식: 거점 id와 방문 횟수의 짝(같은 순번).</summary>
        [Serializable]
        private sealed class VisitSave
        {
            public string[] HubIds = Array.Empty<string>();
            public int[] Counts = Array.Empty<int>();
            // 스펙 003: 탑승 승객 저장 배열 추가(같은 순번이 한 승객, 옛 파일에는 없어 빈 배열)
            public string[] RiderIds = Array.Empty<string>();
            public string[] RiderWaypoints = Array.Empty<string>();
            public int[] RiderLaps = Array.Empty<int>();
        }
    }
}
