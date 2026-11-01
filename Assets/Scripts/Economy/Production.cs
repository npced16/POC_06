using System;
using System.Collections.Generic;
using System.Linq;

using IronRing.Contracts;
using IronRing.Habitats;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 041 (design/game-brief.md "생산 건물"). 완성된 서식지(건물)의 바퀴 생산. 순수 C#(카탈로그·서비스 주입).
    /// Settlement.Producing(열차 유지비 앞)에서 Tick(lap)을 부른다.
    /// 판정 순서(건물 하나): 노동자(_workers, AvailableWorkers가 null이면 언제나 충분) → 증기(_needsSteam: 이번 바퀴 가동 중인 증기 공급 건물)
    /// → 물(_needsWater: 가동 중인 물 공급 건물) → 입력(_consumes가 창고에 모두 있음). 모두 통과하면 Running: 입력을 빼고 출력을 넣고(창고 상한 초과분은 버리고 로그)
    /// _upkeep을 더한다. 가동한 건물의 유지비 합은 장부에 LedgerKind.Upkeep "건물 유지비"로 한 줄 적고 강제 지출한다.
    /// 건물 순서(결정적): ① 입력 없는 생산 건물 → ② 증기·물 공급 건물 → ③ 나머지, 같은 묶음 안에서는 CompletedAt 순서. 앞 건물의 출력은 뒤 건물이 같은 바퀴에 쓸 수 있다.
    /// Status(i)는 같은 규칙을 창고 사본으로 미리 돌린 결과(다음 정산 예측)다.
    /// </summary>
    public sealed class Production : IProductionStatus, IDisposable
    {
        /// <summary>장부 메모.</summary>
        public const string UPKEEP_NOTE = "건물 유지비";

        private readonly HabitatCatalog _catalog;
        private readonly IHabitatClusters _clusters;
        private readonly Warehouse _warehouse;
        private readonly IMoney _money;
        private readonly Ledger _ledger;
        private readonly IItemPlacement _placement;
        private BuildingStatus[] _preview = Array.Empty<BuildingStatus>();
        private bool _dirty = true;

        /// <summary>생산 거점 id.</summary>
        public string HubId { get; }
        /// <summary>042: 쓸 수 있는 노동자 수. null이면 언제나 충분.</summary>
        public Func<int> AvailableWorkers { get; set; }
        /// <summary>상태가 바뀌었을 수 있을 때.</summary>
        public event Action Changed;
        /// <summary>정산 생산이 끝났을 때(바퀴 번호).</summary>
        public event Action<int> Produced;

        /// <summary>카탈로그·완성 서식지·창고·돈·장부를 주입한다. placement는 상태 갱신 알림용(null 허용).</summary>
        public Production(string hubId, HabitatCatalog catalog, IHabitatClusters clusters, Warehouse warehouse, IMoney money, Ledger ledger, IItemPlacement placement)
        {
            HubId = hubId;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _clusters = clusters ?? throw new ArgumentNullException(nameof(clusters));
            _warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
            _money = money ?? throw new ArgumentNullException(nameof(money));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _placement = placement;
            _warehouse.Changed += MarkDirty;
            _clusters.HabitatCompleted += OnClusterChanged;
            _clusters.HabitatBroken += OnClusterChanged;
            if (_placement != null) { _placement.ItemPlaced += OnItemChanged; _placement.ItemRemoved += OnItemChanged; }
        }

        private void OnClusterChanged(string hubId, string typeId) => MarkDirty();
        private void OnItemChanged(string hubId, int cell, string itemId) { if (hubId == HubId) MarkDirty(); }
        private void MarkDirty() { _dirty = true; Changed?.Invoke(); }

        /// <summary>clusterIndex 건물의 예측 상태.</summary>
        public BuildingStatus Status(int clusterIndex)
        {
            if (_dirty) { _preview = Evaluate(false, out _); _dirty = false; }
            return clusterIndex >= 0 && clusterIndex < _preview.Length ? _preview[clusterIndex] : BuildingStatus.Running;
        }

        /// <summary>상태 아이콘 id.</summary>
        public string IconId(BuildingStatus status)
        {
            switch (status)
            {
                case BuildingStatus.NoWorkers: return "status_no_workers";
                case BuildingStatus.NoInput: return "status_no_input";
                case BuildingStatus.NoSteam: return "status_no_steam";
                case BuildingStatus.NoWater: return "status_no_water";
                default: return "status_running";
            }
        }

        /// <summary>바퀴 생산을 실제로 적용한다(Settlement.Producing).</summary>
        public void Tick(int lap)
        {
            Evaluate(true, out int upkeep);
            if (upkeep > 0)
            {
                _money.Charge(upkeep, "upkeep:buildings");
                _ledger.Record(LedgerKind.Upkeep, -upkeep, UPKEEP_NOTE);
            }
            UnityEngine.Debug.Log($"PROD: lap={lap} upkeep={upkeep} warehouse {Describe()}");
            _dirty = true;
            Changed?.Invoke();
            Produced?.Invoke(lap);
        }

        /// <summary>창고 내용 한 줄(로그용, 품목 id 정렬).</summary>
        public string Describe()
            => $"{_warehouse.Total}/{_warehouse.Capacity} [" + string.Join(" ", AllGoods().Where(g => _warehouse.CountOf(g) > 0).Select(g => g + "=" + _warehouse.CountOf(g))) + "]";

        private IEnumerable<string> AllGoods()
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (HabitatType t in _catalog.Types)
            {
                foreach (HabitatGoodsAmount a in t.Produces) ids.Add(a.GoodsId);
                foreach (HabitatGoodsAmount a in t.Consumes) ids.Add(a.GoodsId);
            }
            return ids;
        }

        /// <summary>규칙을 돌린다. commit이면 창고를 실제로 바꾸고 로그를 남긴다. 결과는 CompletedAt 순서의 상태.</summary>
        private BuildingStatus[] Evaluate(bool commit, out int upkeep)
        {
            upkeep = 0;
            IReadOnlyList<HabitatCluster> clusters = _clusters.CompletedAt(HubId) ?? Array.Empty<HabitatCluster>();
            var result = new BuildingStatus[clusters.Count];
            var stock = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string g in AllGoods()) stock[g] = _warehouse.CountOf(g);
            int total = _warehouse.Total;
            int workers = AvailableWorkers != null ? Math.Max(0, AvailableWorkers()) : int.MaxValue;
            bool steam = false, water = false;
            foreach (int i in Order(clusters))
            {
                HabitatType type = _catalog.Get(clusters[i]?.TypeId);
                if (type == null) { result[i] = BuildingStatus.Running; continue; }
                BuildingStatus status;
                if (type.Workers > workers) status = BuildingStatus.NoWorkers;
                else if (type.NeedsSteam && !steam) status = BuildingStatus.NoSteam;
                else if (type.NeedsWater && !water) status = BuildingStatus.NoWater;
                else if (type.Consumes.Any(c => (stock.TryGetValue(c.GoodsId, out int n) ? n : 0) < c.Amount)) status = BuildingStatus.NoInput;
                else status = BuildingStatus.Running;
                result[i] = status;
                if (status != BuildingStatus.Running)
                {
                    if (commit) UnityEngine.Debug.Log($"PROD: {type.Id} {status}");
                    continue;
                }
                if (workers != int.MaxValue) workers -= type.Workers;
                steam |= type.ProvidesSteam;
                water |= type.ProvidesWater;
                upkeep += type.Upkeep;
                foreach (HabitatGoodsAmount c in type.Consumes)
                {
                    stock[c.GoodsId] -= c.Amount; total -= c.Amount;
                    if (commit) _warehouse.TryTake(c.GoodsId, c.Amount);
                }
                var made = new List<string>();
                foreach (HabitatGoodsAmount p in type.Produces)
                {
                    int room = Math.Max(0, _warehouse.Capacity - total);
                    int add = Math.Min(room, p.Amount);
                    if (add > 0)
                    {
                        stock[p.GoodsId] = (stock.TryGetValue(p.GoodsId, out int n) ? n : 0) + add; total += add;
                        if (commit) _warehouse.TryAdd(p.GoodsId, add);
                    }
                    if (commit && add < p.Amount) UnityEngine.Debug.Log($"PROD: {type.Id} warehouse full, lost {p.GoodsId} x{p.Amount - add}");
                    made.Add("+" + p.GoodsId + " " + add);
                }
                if (commit)
                {
                    string ins = string.Join(" ", type.Consumes.Select(c => "-" + c.GoodsId + " " + c.Amount));
                    string extras = (type.ProvidesSteam ? " steam" : "") + (type.ProvidesWater ? " water" : "");
                    UnityEngine.Debug.Log($"PROD: {type.Id} Running {string.Join(" ", made)} {ins}{extras} upkeep {type.Upkeep}".Replace("  ", " "));
                }
            }
            return result;
        }

        /// <summary>결정적 건물 순서: 입력 없는 생산 → 증기·물 공급 → 나머지.</summary>
        private IEnumerable<int> Order(IReadOnlyList<HabitatCluster> clusters)
        {
            int Rank(int i)
            {
                HabitatType t = _catalog.Get(clusters[i]?.TypeId);
                if (t == null) return 3;
                if (t.Consumes.Count == 0 && !t.NeedsSteam && !t.NeedsWater && !t.ProvidesSteam) return 0;
                if (t.ProvidesSteam || t.ProvidesWater) return 1;
                return 2;
            }
            return Enumerable.Range(0, clusters.Count).OrderBy(Rank).ThenBy(i => i).ToList();
        }

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            _warehouse.Changed -= MarkDirty;
            _clusters.HabitatCompleted -= OnClusterChanged;
            _clusters.HabitatBroken -= OnClusterChanged;
            if (_placement != null) { _placement.ItemPlaced -= OnItemChanged; _placement.ItemRemoved -= OnItemChanged; }
            Changed = null; Produced = null;
        }
    }
}
