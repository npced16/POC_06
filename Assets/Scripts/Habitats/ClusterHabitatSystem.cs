using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Habitats
{
    /// <summary>
    /// 스토리 016 (design/game-brief.md MVP 3). 칸 배치가 바뀔 때마다 거점을 다시 판정해 완성 서식지 목록(HabitatMatcher)을 갱신한다.
    /// IHabitatClusters: 종류별 완성 수가 늘면 HabitatCompleted, 줄면 HabitatBroken(늘거나 준 수만큼, 종류 id 단위).
    /// IHabitatClusterChanges(스토리 028, QA S3-1): 수는 같은데 그 종류 완성 묶음들의 칸 구성이 바뀌면 HabitatChanged(종류당 1회).
    /// IHabitatSystem: IsHabitatComplete = 완성 서식지 1개 이상, EventFlagRaised = 거점마다 처음 완성될 때 1회(sticky, 깨져도 유지).
    /// 002의 슬롯 메서드는 비활성(TryPlace/TryRemove false, SlotContent null). 013의 TagHabitatSystem을 대신해 등록된다.
    /// 스토리 035(카탈로그 LockCompleted): 완성되는 순간의 칸 묶음을 거점별 고정 목록에 넣고, 이후 판정에서 고정 칸은 처음부터 차지된
    /// 것으로 본다(옆에 타일을 놓아도 커지지 않음). 자기 칸의 타일이 빠지거나 바뀔 때만 고정이 풀리고, 풀린 칸은 같은 판정에서 다시 본다.
    /// </summary>
    public sealed class ClusterHabitatSystem : IHabitatClusters, IHabitatSystem, IHabitatClusterChanges, IDisposable
    {
        private static readonly IReadOnlyList<HabitatCluster> None = Array.Empty<HabitatCluster>();

        private readonly HabitatCatalog _catalog;
        private readonly IHubInterior _interior;
        private readonly IItemPlacement _placement;
        private readonly Dictionary<string, IReadOnlyList<HabitatCluster>> _completed = new Dictionary<string, IReadOnlyList<HabitatCluster>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<LockedCluster>> _locked = new Dictionary<string, List<LockedCluster>>(StringComparer.Ordinal);
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);
        private bool _disposed;

        /// <summary>hubId, 서식지 종류 id. 새로 완성될 때 1회.</summary>
        public event Action<string, string> HabitatCompleted;
        /// <summary>hubId, 서식지 종류 id. 타일이 빠져 더는 완성이 아닐 때.</summary>
        public event Action<string, string> HabitatBroken;
        /// <summary>스토리 028. hubId, 서식지 종류 id. 완성 수는 그대로인데 칸 구성이 바뀔 때(Completed/Broken 뒤에 낸다).</summary>
        public event Action<string, string> HabitatChanged;
        /// <summary>hubId: 플래그가 처음 켜질 때 한 번.</summary>
        public event Action<string> EventFlagRaised;

        /// <summary>
        /// 카탈로그, 칸을 읽을 내부, (선택) 배치 이벤트를 주입받아 구독한다. hubIds가 주어지면 지금 칸 상태로 조용히(이벤트 없이) 초기화한다.
        /// </summary>
        public ClusterHabitatSystem(HabitatCatalog catalog, IHubInterior interior, IItemPlacement placement = null, IEnumerable<string> hubIds = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _interior = interior ?? throw new ArgumentNullException(nameof(interior));
            _placement = placement;
            if (hubIds != null)
                foreach (string hubId in hubIds)
                    if (hubId != null) _completed[hubId] = Compute(hubId);
            _interior.CellChanged += OnCellChanged;
            if (_placement != null)
            {
                _placement.ItemPlaced += OnItemChanged;
                _placement.ItemRemoved += OnItemChanged;
            }
        }

        /// <summary>이 시스템이 따르는 카탈로그.</summary>
        public HabitatCatalog Catalog => _catalog;

        /// <summary>서식지 카탈로그의 모든 종류 id.</summary>
        public IReadOnlyList<string> HabitatTypeIds => _catalog.TypeIds;

        /// <summary>해당 거점에서 지금 완성된 서식지들(판정 전이거나 모르는 거점이면 빈 목록).</summary>
        public IReadOnlyList<HabitatCluster> CompletedAt(string hubId) =>
            hubId != null && _completed.TryGetValue(hubId, out var list) ? list : None;

        /// <summary>해당 거점에 typeId 서식지가 지금 완성되어 있는지.</summary>
        public bool IsTypeComplete(string hubId, string typeId)
        {
            foreach (HabitatCluster cluster in CompletedAt(hubId)) if (cluster.TypeId == typeId) return true;
            return false;
        }

        /// <summary>비활성(002 자원 슬롯 방식). 항상 false.</summary>
        public bool TryPlace(string hubId, int slotIndex, string resourceId) => false;
        /// <summary>비활성(002 자원 슬롯 방식). 항상 false.</summary>
        public bool TryRemove(string hubId, int slotIndex) => false;
        /// <summary>비활성(002 자원 슬롯 방식). 항상 null.</summary>
        public string SlotContent(string hubId, int slotIndex) => null;

        /// <summary>완성된 서식지가 하나라도 있으면 true(타일을 치우면 false로 돌아갈 수 있다).</summary>
        public bool IsHabitatComplete(string hubId) => CompletedAt(hubId).Count > 0;

        /// <summary>한 번이라도 완성된 적이 있는지(sticky).</summary>
        public bool IsEventFlagSet(string hubId) => hubId != null && _flags.Contains(hubId);

        /// <summary>거점을 다시 판정해 목록을 바꾸고 종류별 증감만큼 Broken → Completed 순으로 알린 뒤, 처음 완성이면 플래그를 켠다.</summary>
        public void Evaluate(string hubId)
        {
            if (_disposed || hubId == null) return;
            IReadOnlyList<HabitatCluster> before = CompletedAt(hubId);
            IReadOnlyList<HabitatCluster> after = Compute(hubId);
            _completed[hubId] = after;
            Dictionary<string, int> oldCounts = Count(before), newCounts = Count(after);
            foreach (string typeId in _catalog.TypeIds)
            {
                int delta = (newCounts.TryGetValue(typeId, out int n) ? n : 0) - (oldCounts.TryGetValue(typeId, out int o) ? o : 0);
                for (int i = 0; i < -delta; i++) HabitatBroken?.Invoke(hubId, typeId);
            }
            foreach (string typeId in _catalog.TypeIds)
            {
                int delta = (newCounts.TryGetValue(typeId, out int n) ? n : 0) - (oldCounts.TryGetValue(typeId, out int o) ? o : 0);
                for (int i = 0; i < delta; i++) HabitatCompleted?.Invoke(hubId, typeId);
            }
            foreach (string typeId in _catalog.TypeIds)
            {
                int n = newCounts.TryGetValue(typeId, out int nv) ? nv : 0, o = oldCounts.TryGetValue(typeId, out int ov) ? ov : 0;
                if (n == o && n > 0 && CellKey(before, typeId) != CellKey(after, typeId)) HabitatChanged?.Invoke(hubId, typeId);
            }
            if (after.Count > 0 && _flags.Add(hubId)) EventFlagRaised?.Invoke(hubId);
        }

        /// <summary>구독을 해제한다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _interior.CellChanged -= OnCellChanged;
            if (_placement != null)
            {
                _placement.ItemPlaced -= OnItemChanged;
                _placement.ItemRemoved -= OnItemChanged;
            }
        }

        private IReadOnlyList<HabitatCluster> Compute(string hubId)
        {
            IReadOnlyList<IInteriorCell> cells;
            try { cells = _interior.Cells(hubId); }
            catch (ArgumentException) { return None; }
            if (!_catalog.LockCompleted)
            {
                List<HabitatCluster> all = HabitatMatcher.Evaluate(cells, _catalog);
                return all.Count == 0 ? None : all.AsReadOnly();
            }
            var tileAt = new Dictionary<int, string>();
            foreach (IInteriorCell cell in cells)
                if (cell != null) tileAt[cell.Index] = cell.State == InteriorCellState.ItemPlaced && !string.IsNullOrEmpty(cell.ItemId) ? cell.ItemId : null;
            if (!_locked.TryGetValue(hubId, out List<LockedCluster> locked)) _locked[hubId] = locked = new List<LockedCluster>();
            locked.RemoveAll(entry => !entry.Intact(tileAt));
            var reserved = new HashSet<int>();
            foreach (LockedCluster entry in locked) foreach (int index in entry.Cluster.Cells) reserved.Add(index);
            foreach (HabitatCluster fresh in HabitatMatcher.Evaluate(cells, _catalog, reserved))
                locked.Add(LockedCluster.Snapshot(fresh, tileAt));
            if (locked.Count == 0) return None;
            var result = new List<HabitatCluster>(locked.Count);
            foreach (LockedCluster entry in locked) result.Add(entry.Cluster);
            return result.AsReadOnly();
        }

        /// <summary>스토리 035. 고정된 서식지 하나: 완성 순간의 클러스터와 칸마다 놓여 있던 타일.</summary>
        private sealed class LockedCluster
        {
            public HabitatCluster Cluster;
            private Dictionary<int, string> _tiles;

            public static LockedCluster Snapshot(HabitatCluster cluster, Dictionary<int, string> tileAt)
            {
                var tiles = new Dictionary<int, string>();
                foreach (int index in cluster.Cells ?? Array.Empty<int>()) tiles[index] = tileAt.TryGetValue(index, out string tile) ? tile : null;
                return new LockedCluster { Cluster = cluster, _tiles = tiles };
            }

            /// <summary>모든 칸의 타일이 고정 당시와 같으면 true.</summary>
            public bool Intact(Dictionary<int, string> tileAt)
            {
                foreach (KeyValuePair<int, string> pair in _tiles)
                {
                    string now = tileAt.TryGetValue(pair.Key, out string tile) ? tile : null;
                    if (!string.Equals(now, pair.Value, StringComparison.Ordinal)) return false;
                }
                return true;
            }
        }

        private static Dictionary<string, int> Count(IReadOnlyList<HabitatCluster> clusters)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (HabitatCluster cluster in clusters) counts[cluster.TypeId] = (counts.TryGetValue(cluster.TypeId, out int n) ? n : 0) + 1;
            return counts;
        }

        /// <summary>Order-independent key of every cell set of typeId's clusters (each cluster's cells sorted, clusters sorted).</summary>
        private static string CellKey(IReadOnlyList<HabitatCluster> clusters, string typeId)
        {
            var parts = new List<string>();
            foreach (HabitatCluster cluster in clusters)
            {
                if (cluster.TypeId != typeId) continue;
                var cells = new List<int>(cluster.Cells ?? Array.Empty<int>());
                cells.Sort();
                parts.Add(string.Join(",", cells));
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private void OnCellChanged(string hubId, int cellIndex) => Evaluate(hubId);

        private void OnItemChanged(string hubId, int cellIndex, string itemId) => Evaluate(hubId);
    }
}
