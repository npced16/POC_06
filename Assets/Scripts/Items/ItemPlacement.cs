using System;
using System.Collections.Generic;
using System.Linq;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Items
{
    /// <summary>
    /// 칸 상태를 실제로 바꾸는 쓰기 창구. IHubInterior 계약에는 쓰기 메서드가 없으므로 012의 HubInterior
    /// (TryPlaceItem/TryRemoveItem)를 어댑터로 감싸 주입한다. 테스트는 가짜로 구현한다.
    /// </summary>
    public interface IInteriorItemSlots
    {
        /// <summary>Empty 칸을 ItemPlaced(itemId)로 바꾼다. 다른 상태면 false.</summary>
        bool TryPlaceItem(string hubId, int cellIndex, string itemId);
        /// <summary>ItemPlaced 칸을 Empty로 바꾼다. 다른 상태면 false.</summary>
        bool TryRemoveItem(string hubId, int cellIndex);
    }

    /// <summary>
    /// 조사된 빈 터에 아이템을 놓는다. 땅은 개간 비용을 내고 그 외 재료는 재고에서 차감하고 회수한다.
    /// 칸 상태 전이(Empty ↔ ItemPlaced)는 IInteriorItemSlots가 하고, 성공한 뒤에만 이벤트를 낸다.
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class ItemPlacement : IItemPlacement, ISaveParticipant
    {
        private readonly IHubInterior _interior;
        private readonly IInteriorItemSlots _slots;
        private readonly Func<string, bool> _canEdit;

        /// <summary>hubId, cellIndex, itemId.</summary>
        public event Action<string, int, string> ItemPlaced;
        /// <summary>hubId, cellIndex, itemId.</summary>
        public event Action<string, int, string> ItemRemoved;

        /// <summary>스토리 041. 배치 비용 결제(아이템 id → 잔고에서 빼기, 모자라면 false). null이면 무료.</summary>
        public Func<string, bool> TryCharge { get; set; }
        /// <summary>스토리 041. 배치 가능 여부의 잔고 검사(아이템 id). null이면 언제나 가능.</summary>
        public Func<string, bool> CanAfford { get; set; }
        /// <summary>스토리 041. 회수 환급(아이템 id). null이면 환급 없음.</summary>
        public Action<string> Refund { get; set; }
        /// <summary>기존 호출부와의 호환용 해금 판정. 재고 배치에는 사용하지 않는다.</summary>
        public Func<string, bool> IsUnlocked { get; set; }
        /// <summary>
        /// 땅 이외의 재료를 배치하거나 회수할 때 사용하는 재고. null이면 재료를 배치할 수 없다.
        /// </summary>
        public ITileStock Stock { get; set; }
        private readonly HashSet<string> _stockPaid = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>스토리 051. 그 칸의 타일이 재고로 놓였는지.</summary>
        public bool IsStockPaid(string hubId, int cellIndex) => _stockPaid.Contains(Key(hubId, cellIndex));

        /// <summary>itemId의 배치 재고가 하나 이상이면 true를 반환한다.</summary>
        private bool HasStock(string itemId) => Stock != null && Stock.CountOf(itemId) > 0;
        /// <summary>hubId와 cellIndex를 결합해 저장용 칸 키를 반환한다.</summary>
        private static string Key(string hubId, int cellIndex) => hubId + "#" + cellIndex;

        /// <summary>canEdit가 null이면 언제나 편집 가능(테스트). 통합은 내부 화면이 그 거점일 때만 true를 준다.</summary>
        public ItemPlacement(IItemInventory inventory, IHubInterior interior, IInteriorItemSlots slots, Func<string, bool> canEdit = null)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            _interior = interior ?? throw new ArgumentNullException(nameof(interior));
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
            _canEdit = canEdit;
        }

        // 스펙 001: 온보딩 안내 배치가 칸·재료를 제한할 수 있도록 놓기 허용 판정을 추가(null이면 제한 없음).
        /// <summary>(거점 id, 칸 번호, 아이템 id)를 받아 놓아도 되면 true를 돌려주는 추가 판정. null이면 제한 없음.</summary>
        public Func<string, int, string, bool> PlacementGate { get; set; }

        /// <summary>거점과 칸, 아이템을 받아 빈 칸에 땅 비용 또는 재료 재고가 충분하면 true를 반환한다.</summary>
        public bool CanPlace(string hubId, int cellIndex, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !CanEdit(hubId)) return false;
            if (PlacementGate != null && !PlacementGate(hubId, cellIndex, itemId)) return false; // 스펙 001: 안내 배치 제한 확인 추가
            IInteriorCell cell = Cell(hubId, cellIndex);
            return cell != null && cell.State == InteriorCellState.Empty &&
                (itemId == "tile_cobble" ? CanAfford == null || CanAfford(itemId) : HasStock(itemId));
        }

        /// <summary>거점의 칸에 아이템을 놓고 땅은 비용, 다른 재료는 재고를 차감한다. 칸 쓰기가 실패하면 차감분을 되돌린다.</summary>
        public bool TryPlace(string hubId, int cellIndex, string itemId)
        {
            if (!CanPlace(hubId, cellIndex, itemId)) return false;
            bool fromStock = itemId != "tile_cobble";
            if (fromStock && !Stock.TryTake(itemId, 1)) return false;
            int groundPrice = !fromStock ? GameServices.Get<IPlacementCost>()?.PriceOf(itemId) ?? 0 : 0;
            bool charged = !fromStock && TryCharge != null;
            if (charged && !TryCharge(itemId)) return false;
            if (!_slots.TryPlaceItem(hubId, cellIndex, itemId))
            {
                if (fromStock) Stock.Add(itemId, 1);
                else if (charged && groundPrice > 0) GameServices.Get<IMoney>()?.TryGain(groundPrice, "build_rollback:" + itemId);
                return false;
            }
            if (fromStock) _stockPaid.Add(Key(hubId, cellIndex));
            else _stockPaid.Remove(Key(hubId, cellIndex));
            ItemPlaced?.Invoke(hubId, cellIndex, itemId);
            return true;
        }

        /// <summary>거점의 칸에서 아이템을 회수하고 땅은 설정된 비용을 환급하며 다른 재료는 재고에 하나를 돌려준다.</summary>
        public bool TryPickUp(string hubId, int cellIndex)
        {
            if (!CanEdit(hubId)) return false;
            IInteriorCell cell = Cell(hubId, cellIndex);
            if (cell == null || cell.State != InteriorCellState.ItemPlaced || string.IsNullOrEmpty(cell.ItemId)) return false;
            string itemId = cell.ItemId;
            if (!_slots.TryRemoveItem(hubId, cellIndex)) return false;
            _stockPaid.Remove(Key(hubId, cellIndex));
            if (itemId != "tile_cobble" && Stock != null) Stock.Add(itemId, 1);
            else if (itemId == "tile_cobble") Refund?.Invoke(itemId);
            ItemRemoved?.Invoke(hubId, cellIndex, itemId);
            return true;
        }

        /// <summary>
        /// hubId의 완성 cluster를 destination으로 비용과 재고 변경 없이 옮긴다.
        /// 모든 칸이 유효하고 최종 묶음 종류가 유지되면 true를 반환한다.
        /// </summary>
        public bool TryMoveHabitat(string hubId, HabitatCluster cluster, IReadOnlyList<int> destination)
        {
            if (!CanMoveHabitat(hubId, cluster, destination) || !(_interior is IronRing.Interior.HubInterior interior)) return false;
            var source = new List<int>(cluster.Cells);
            var stock = new bool[source.Count];
            for (int i = 0; i < source.Count; i++) stock[i] = _stockPaid.Contains(Key(hubId, source[i]));
            var clusters = GameServices.Get<IHabitatClusters>() as IronRing.Habitats.ClusterHabitatSystem;
            bool moved = interior.TryMoveItems(hubId, source, destination, () =>
            {
                foreach (int index in source) _stockPaid.Remove(Key(hubId, index));
                for (int i = 0; i < destination.Count; i++) if (stock[i]) _stockPaid.Add(Key(hubId, destination[i]));
                clusters.MoveLocked(hubId, cluster, destination);
            });
            if (!moved) return false;
            for (int i = 0; i < source.Count; i++) ItemRemoved?.Invoke(hubId, source[i], Cell(hubId, destination[i]).ItemId);
            for (int i = 0; i < destination.Count; i++) ItemPlaced?.Invoke(hubId, destination[i], Cell(hubId, destination[i]).ItemId);
            return true;
        }

        /// <summary>
        /// hubId의 cluster가 destination에 놓일 수 있는지 확인한다.
        /// 점유 칸과 이동 후 조합을 검사하여 가능하면 true를 반환한다.
        /// </summary>
        public bool CanMoveHabitat(string hubId, HabitatCluster cluster, IReadOnlyList<int> destination)
        {
            var system = GameServices.Get<IHabitatClusters>() as IronRing.Habitats.ClusterHabitatSystem;
            if (!CanEdit(hubId) || cluster?.Cells == null || destination == null || cluster.Cells.Count != destination.Count || system == null || !system.CompletedAt(hubId).Contains(cluster)) return false;
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(hubId);
            var from = new HashSet<int>(cluster.Cells);
            var to = new HashSet<int>();
            var simulated = new List<IInteriorCell>();
            foreach (IInteriorCell cell in cells) simulated.Add(new MoveCell(cell, from.Contains(cell.Index) ? null : cell.ItemId));
            for (int i = 0; i < destination.Count; i++)
            {
                int index = destination[i];
                if (index < 0 || index >= cells.Count || !to.Add(index) || (!from.Contains(index) && cells[index].State != InteriorCellState.Empty)) return false;
                simulated[index] = new MoveCell(cells[index], cells[cluster.Cells[i]].ItemId);
            }
            var moved = new HabitatCluster { TypeId = cluster.TypeId, Cells = destination };
            return IronRing.Habitats.HabitatMatcher.MatchesCluster(simulated, system.Catalog.Get(cluster.TypeId), moved);
        }

        /// <summary>이동 후보를 판정할 때 원본 칸을 변경하지 않고 재료 상태를 나타낸다.</summary>
        private sealed class MoveCell : IInteriorCell
        {
            private readonly IInteriorCell _cell;
            public int Index => _cell.Index;
            public int Row => _cell.Row;
            public int Column => _cell.Column;
            public bool IsUpper => _cell.IsUpper;
            public InteriorCellState State { get; }
            public string ItemId { get; }
            /// <summary>cell의 좌표와 itemId를 복사하여 후보 칸을 만든다.</summary>
            public MoveCell(IInteriorCell cell, string itemId) { _cell = cell; ItemId = itemId; State = itemId != null ? InteriorCellState.ItemPlaced : cell.State == InteriorCellState.ItemPlaced ? InteriorCellState.Empty : cell.State; }
        }

        private bool CanEdit(string hubId) => hubId != null && (_canEdit == null || _canEdit(hubId));

        /// <summary>거점·인덱스가 유효하면 칸, 아니면 null(모르는 거점의 예외도 null로 바꾼다).</summary>
        private IInteriorCell Cell(string hubId, int cellIndex)
        {
            IReadOnlyList<IInteriorCell> cells;
            try { cells = _interior.Cells(hubId); }
            catch (ArgumentException) { return null; }
            if (cells == null || cellIndex < 0 || cellIndex >= cells.Count) return null;
            return cells[cellIndex];
        }

        // 스펙 003: 저장 슬롯용 재고 결제 칸 _stockPaid 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "placement";

        /// <summary>스펙 003. 복원 순서(칸 상태 HubInterior 다음).</summary>
        public int Order => 30;

        /// <summary>재고로 놓은 칸 키 _stockPaid를 정렬해 JSON으로 돌려준다. 놓인 아이템 자체는 HubInterior가 저장한다.</summary>
        public string Capture()
        {
            List<string> keys = new List<string>(_stockPaid);
            keys.Sort(StringComparer.Ordinal);
            return UnityEngine.JsonUtility.ToJson(new PlacementSave { StockPaid = keys.ToArray() });
        }

        /// <summary>
        /// Capture가 만든 json으로 _stockPaid를 바꾸고, 복원된 칸 가운데 아이템이 놓인 칸마다 ItemPlaced를 내서
        /// 칸 위 아이템 표시가 다시 그려지게 한다. 거점 목록은 HubInterior에서 읽는다.
        /// </summary>
        public void Restore(string json)
        {
            PlacementSave state = UnityEngine.JsonUtility.FromJson<PlacementSave>(json);
            _stockPaid.Clear();
            foreach (string key in state.StockPaid) if (!string.IsNullOrEmpty(key)) _stockPaid.Add(key);
            if (!(_interior is IronRing.Interior.HubInterior hubInterior)) return;
            foreach (string hubId in hubInterior.HubIds)
            {
                IReadOnlyList<IInteriorCell> cells = _interior.Cells(hubId);
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].State == InteriorCellState.ItemPlaced && !string.IsNullOrEmpty(cells[i].ItemId)) ItemPlaced?.Invoke(hubId, i, cells[i].ItemId);
                }
            }
        }

        /// <summary>스펙 003. 저장 형식: 재고로 놓은 칸 키("거점#칸") 목록.</summary>
        [Serializable]
        private sealed class PlacementSave
        {
            public string[] StockPaid = Array.Empty<string>();
        }
    }
}
