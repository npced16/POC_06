using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Npc
{
    /// <summary>
    /// 스토리 017 집 배정(story-017-npc-homes-dialogue.md, AC "집 배정"·"집이 깨지면").
    /// 합류한 NPC는 합류한 거점의 완성 서식지 가운데 자기 종류(NpcPool.json `_habitatTypeId`) 묶음 하나를 집으로 잡는다.
    /// 같은 종류가 여럿이면 아무도 살지 않는 묶음을 먼저, 모두 차 있으면 함께 산다. 서식지가 깨지면 칸 목록을 비우고(거점은 그대로),
    /// 같은 종류가 다시 완성되면 마지막 집과 가장 많이 겹치는 묶음(없으면 빈 묶음)을 되찾는다. 묶음이 자라거나 줄면 칸 목록을 따라 갱신한다.
    /// IHabitatClusters는 Composing 순서에 기대지 않도록 늦게 얻는다(첫 사용 또는 Bind 호출 때 구독).
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class NpcHomes : INpcHomes, IDisposable, ISaveParticipant
    {
        private readonly INpcRecruiter _recruiter;
        private readonly NpcPoolData _pool;
        private readonly Func<IHabitatClusters> _clustersSource;
        private readonly Func<IItemPlacement> _placementSource;
        private readonly Dictionary<string, Record> _records = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly List<string> _joinOrder = new List<string>();
        private IHabitatClusters _clusters;
        private IItemPlacement _placement;
        private bool _disposed;

        /// <summary>npcId. 집이 생기거나 칸이 바뀌거나 비었을 때.</summary>
        public event Action<string> HomeChanged;

        private sealed class Record
        {
            public string NpcId;
            public string HubId;
            public string TypeId;
            public int[] Cells = Array.Empty<int>();
            /// <summary>마지막으로 비어 있지 않던 칸들. 서식지가 다시 완성될 때 같은 자리를 되찾는 데 쓴다.</summary>
            public int[] LastCells = Array.Empty<int>();
        }

        /// <summary>클러스터 시스템을 바로 받는 생성자(테스트·단독 사용).</summary>
        public NpcHomes(INpcRecruiter recruiter, IHabitatClusters clusters, NpcPoolData pool, IEnumerable<string> hubIds, IItemPlacement placement = null)
            : this(recruiter, () => clusters, pool, hubIds, () => placement)
        {
            Bind();
        }

        /// <summary>
        /// 조립용 생성자. clusters/placement는 처음 필요할 때 얻는다(Composing 안에서 다른 설치자가 아직 등록하지 않았을 수 있다).
        /// hubIds의 거점마다 이미 합류한 NPC(이 시스템보다 먼저 합류)를 기록한다.
        /// </summary>
        public NpcHomes(INpcRecruiter recruiter, Func<IHabitatClusters> clusters, NpcPoolData pool, IEnumerable<string> hubIds, Func<IItemPlacement> placement = null)
        {
            _recruiter = recruiter ?? throw new ArgumentNullException(nameof(recruiter));
            _clustersSource = clusters ?? throw new ArgumentNullException(nameof(clusters));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _placementSource = placement;
            _recruiter.NpcJoined += OnNpcJoined;
            if (hubIds != null)
                foreach (string hub in hubIds)
                    foreach (string npc in _recruiter.NpcsAt(hub))
                        AddRecord(hub, npc);
        }

        /// <summary>서식지·배치 시스템이 등록되었으면 구독하고 이미 합류한 NPC의 집을 잡는다. 여러 번 불러도 된다.</summary>
        public void Bind()
        {
            if (_disposed) return;
            if (_clusters == null)
            {
                _clusters = _clustersSource();
                if (_clusters != null)
                {
                    _clusters.HabitatCompleted += OnHabitatChanged;
                    _clusters.HabitatBroken += OnHabitatChanged;
                    foreach (string hub in HubsWithRecords()) Refresh(hub, null);
                }
            }
            if (_placement == null && _placementSource != null)
            {
                _placement = _placementSource();
                if (_placement != null)
                {
                    _placement.ItemPlaced += OnItemChanged;
                    _placement.ItemRemoved += OnItemChanged;
                }
            }
        }

        /// <summary>npcId의 집(복사본). 합류하지 않은 NPC면 null.</summary>
        public NpcHome HomeOf(string npcId)
        {
            Bind();
            return npcId != null && _records.TryGetValue(npcId, out Record r) ? ToHome(r) : null;
        }

        /// <summary>hubId에 사는 NPC들의 집(합류 순, 복사본).</summary>
        public IReadOnlyList<NpcHome> HomesAt(string hubId)
        {
            Bind();
            var list = new List<NpcHome>();
            if (hubId == null) return list.AsReadOnly();
            foreach (string npc in _joinOrder)
            {
                Record r = _records[npc];
                if (r.HubId == hubId) list.Add(ToHome(r));
            }
            return list.AsReadOnly();
        }

        /// <summary>구독을 모두 끊는다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _recruiter.NpcJoined -= OnNpcJoined;
            if (_clusters != null)
            {
                _clusters.HabitatCompleted -= OnHabitatChanged;
                _clusters.HabitatBroken -= OnHabitatChanged;
            }
            if (_placement != null)
            {
                _placement.ItemPlaced -= OnItemChanged;
                _placement.ItemRemoved -= OnItemChanged;
            }
            HomeChanged = null;
        }

        private void OnNpcJoined(string hubId, string npcId)
        {
            if (!AddRecord(hubId, npcId)) return;
            Bind();
            Refresh(hubId, npcId);
        }

        private void OnHabitatChanged(string hubId, string typeId) => Refresh(hubId, null);

        private void OnItemChanged(string hubId, int cellIndex, string itemId)
        {
            if (HasRecordsAt(hubId)) Refresh(hubId, null);
        }

        private bool AddRecord(string hubId, string npcId)
        {
            if (hubId == null || npcId == null || _records.ContainsKey(npcId)) return false;
            _records.Add(npcId, new Record { NpcId = npcId, HubId = hubId, TypeId = _pool.Find(npcId)?.HabitatTypeId });
            _joinOrder.Add(npcId);
            return true;
        }

        /// <summary>
        /// hubId의 집들을 지금의 완성 서식지에 맞춘다. 1) 칸이 같은 종류 묶음과 정확히 같으면 유지. 2) 나머지는 합류 순으로
        /// 아무도 안 사는 묶음(마지막 집과 많이 겹치는 것 우선) → 점유된 묶음(같은 기준) → 없음(빈 목록).
        /// 칸이 바뀐 NPC와 forceNotify(새로 합류한 NPC)에 HomeChanged를 낸다.
        /// </summary>
        private void Refresh(string hubId, string forceNotify)
        {
            if (hubId == null) return;
            IReadOnlyList<HabitatCluster> completed = _clusters != null ? _clusters.CompletedAt(hubId) : null;
            var clusters = new List<int[]>();
            var clusterTypes = new List<string>();
            if (completed != null)
                foreach (HabitatCluster c in completed)
                {
                    if (c?.Cells == null || c.Cells.Count == 0) continue;
                    var cells = new List<int>(c.Cells);
                    cells.Sort();
                    clusters.Add(cells.ToArray());
                    clusterTypes.Add(c.TypeId);
                }

            var claimed = new HashSet<int>();
            var pending = new List<Record>();
            foreach (string npc in _joinOrder)
            {
                Record r = _records[npc];
                if (r.HubId != hubId) continue;
                int exact = -1;
                for (int i = 0; i < clusters.Count && r.Cells.Length > 0; i++)
                    if (clusterTypes[i] == r.TypeId && !claimed.Contains(i) && SameCells(clusters[i], r.Cells)) { exact = i; break; }
                if (exact >= 0) claimed.Add(exact);
                else pending.Add(r);
            }

            var changed = new List<string>();
            foreach (Record r in pending)
            {
                int pick = Pick(r, clusters, clusterTypes, claimed, true);
                if (pick < 0) pick = Pick(r, clusters, clusterTypes, claimed, false);
                int[] next = pick >= 0 ? clusters[pick] : Array.Empty<int>();
                if (pick >= 0) claimed.Add(pick);
                if (SameCells(next, r.Cells)) continue;
                r.Cells = next;
                if (next.Length > 0) r.LastCells = next;
                changed.Add(r.NpcId);
            }

            if (forceNotify != null && !changed.Contains(forceNotify)) changed.Add(forceNotify);
            foreach (string npc in changed) HomeChanged?.Invoke(npc);
        }

        /// <summary>r의 종류인 묶음 가운데 마지막 집과 가장 많이 겹치는 것(같으면 앞선 것). onlyFree면 아무도 안 사는 묶음만.</summary>
        private static int Pick(Record r, List<int[]> clusters, List<string> types, HashSet<int> claimed, bool onlyFree)
        {
            if (r.TypeId == null) return -1;
            int best = -1, bestOverlap = -1;
            for (int i = 0; i < clusters.Count; i++)
            {
                if (types[i] != r.TypeId || (onlyFree && claimed.Contains(i))) continue;
                int overlap = Overlap(clusters[i], r.LastCells);
                if (overlap > bestOverlap) { best = i; bestOverlap = overlap; }
            }
            return best;
        }

        private static int Overlap(int[] a, int[] b)
        {
            int n = 0;
            foreach (int x in a)
                if (Array.IndexOf(b, x) >= 0) n++;
            return n;
        }

        private static bool SameCells(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private bool HasRecordsAt(string hubId)
        {
            foreach (Record r in _records.Values)
                if (r.HubId == hubId) return true;
            return false;
        }

        private List<string> HubsWithRecords()
        {
            var hubs = new List<string>();
            foreach (string npc in _joinOrder)
                if (!hubs.Contains(_records[npc].HubId)) hubs.Add(_records[npc].HubId);
            return hubs;
        }

        private static NpcHome ToHome(Record r) => new NpcHome
        {
            NpcId = r.NpcId,
            HubId = r.HubId,
            HabitatTypeId = r.TypeId,
            Cells = Array.AsReadOnly((int[])r.Cells.Clone())
        };

        // 스펙 003: 저장 슬롯용 숙소 기록·합류 순서 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "npcHomes";

        /// <summary>스펙 003. 복원 순서(합류 기록 다음).</summary>
        public int Order => 61;

        /// <summary>
        /// 합류 순서 _joinOrder대로 NPC마다 거점·집 칸·마지막 집 칸을 JSON으로 돌려준다. 서식지 종류는 풀 데이터에서 다시 읽으므로 저장하지 않는다.
        /// 상태를 바꾸지 않는다.
        /// </summary>
        public string Capture()
        {
            HomesSave state = new HomesSave();
            foreach (string npcId in _joinOrder)
            {
                Record r = _records[npcId];
                state.Records.Add(new RecordSave { NpcId = r.NpcId, HubId = r.HubId, Cells = r.Cells, LastCells = r.LastCells });
            }
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>Capture가 만든 json으로 숙소 기록과 합류 순서를 바꾸고, 기록된 NPC마다 HomeChanged를 낸다.</summary>
        public void Restore(string json)
        {
            HomesSave state = UnityEngine.JsonUtility.FromJson<HomesSave>(json);
            _records.Clear();
            _joinOrder.Clear();
            foreach (RecordSave saved in state.Records)
            {
                if (saved == null || saved.NpcId == null || saved.HubId == null || _records.ContainsKey(saved.NpcId)) continue;
                _records.Add(saved.NpcId, new Record
                {
                    NpcId = saved.NpcId,
                    HubId = saved.HubId,
                    TypeId = _pool.Find(saved.NpcId)?.HabitatTypeId,
                    Cells = saved.Cells ?? Array.Empty<int>(),
                    LastCells = saved.LastCells ?? Array.Empty<int>(),
                });
                _joinOrder.Add(saved.NpcId);
            }
            foreach (string npcId in _joinOrder) HomeChanged?.Invoke(npcId);
        }

        /// <summary>스펙 003. 저장 형식: 합류 순서의 숙소 기록 목록.</summary>
        [Serializable]
        private sealed class HomesSave
        {
            public List<RecordSave> Records = new List<RecordSave>();
        }

        /// <summary>스펙 003. 저장 형식: NPC 하나의 거점·집 칸·마지막 집 칸.</summary>
        [Serializable]
        private sealed class RecordSave
        {
            public string NpcId;
            public string HubId;
            public int[] Cells = Array.Empty<int>();
            public int[] LastCells = Array.Empty<int>();
        }
    }
}
