using System;
using System.Collections.Generic;

using IronRing.Contracts;

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
    /// 스토리 013. 조사된 빈 터에만 아이템을 놓는다. 놓으면 인벤토리 1 감소, 회수하면 1 증가해 개수가 보존된다.
    /// 칸 상태 전이(Empty ↔ ItemPlaced)는 IInteriorItemSlots가 하고, 성공한 뒤에만 이벤트를 낸다.
    /// </summary>
    public sealed class ItemPlacement : IItemPlacement
    {
        private readonly IItemInventory _inventory;
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
        /// <summary>스토리 050. 아이템이 해금됐는지(IHabitatCodex.IsTileUnlocked). null이면 모두 해금. 잠긴 타일은 놓을 수 없다(회수는 언제나 가능).</summary>
        public Func<string, bool> IsUnlocked { get; set; }
        /// <summary>
        /// 스토리 051. 타일 재고(중간역 사건 보상). 놓을 때 재고가 있으면 재고 1을 쓰고 결제하지 않는다(잔고 검사도 건너뜀), 없으면 기존처럼 돈.
        /// 회수: 재고로 놓은 칸은 재고로 1 되돌리고 환급 없음, 돈으로 놓은 칸은 기존 환급. null이면 재고 없음.
        /// </summary>
        public ITileStock Stock { get; set; }
        private readonly HashSet<string> _stockPaid = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>스토리 051. 그 칸의 타일이 재고로 놓였는지.</summary>
        public bool IsStockPaid(string hubId, int cellIndex) => _stockPaid.Contains(Key(hubId, cellIndex));

        private bool HasStock(string itemId) => Stock != null && Stock.CountOf(itemId) > 0;
        private static string Key(string hubId, int cellIndex) => hubId + "#" + cellIndex;

        /// <summary>canEdit가 null이면 언제나 편집 가능(테스트). 통합은 내부 화면이 그 거점일 때만 true를 준다.</summary>
        public ItemPlacement(IItemInventory inventory, IHubInterior interior, IInteriorItemSlots slots, Func<string, bool> canEdit = null)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _interior = interior ?? throw new ArgumentNullException(nameof(interior));
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
            _canEdit = canEdit;
        }

        /// <summary>편집 가능하고 (스토리 050) 해금된 아이템이며 칸이 Empty이고 아이템을 1개 이상 가졌으면 true.</summary>
        public bool CanPlace(string hubId, int cellIndex, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !CanEdit(hubId)) return false;
            if (IsUnlocked != null && !IsUnlocked(itemId)) return false;
            IInteriorCell cell = Cell(hubId, cellIndex);
            return cell != null && cell.State == InteriorCellState.Empty && _inventory.Count(itemId) > 0 && (HasStock(itemId) || CanAfford == null || CanAfford(itemId));
        }

        /// <summary>인벤토리에서 1개를 빼 칸에 놓는다. 칸 쓰기가 거부되면 되돌리고 false.</summary>
        public bool TryPlace(string hubId, int cellIndex, string itemId)
        {
            if (!CanPlace(hubId, cellIndex, itemId)) return false;
            if (!_inventory.TryRemove(itemId, 1)) return false;
            if (!_slots.TryPlaceItem(hubId, cellIndex, itemId)) { _inventory.Add(itemId, 1); return false; }
            bool fromStock = Stock != null && Stock.TryTake(itemId, 1);
            if (!fromStock && TryCharge != null && !TryCharge(itemId))
            {
                _slots.TryRemoveItem(hubId, cellIndex);
                _inventory.Add(itemId, 1);
                return false;
            }
            if (fromStock) _stockPaid.Add(Key(hubId, cellIndex));
            else _stockPaid.Remove(Key(hubId, cellIndex));
            ItemPlaced?.Invoke(hubId, cellIndex, itemId);
            return true;
        }

        /// <summary>ItemPlaced 칸의 아이템을 인벤토리로 1개 돌려주고 칸을 Empty로 만든다.</summary>
        public bool TryPickUp(string hubId, int cellIndex)
        {
            if (!CanEdit(hubId)) return false;
            IInteriorCell cell = Cell(hubId, cellIndex);
            if (cell == null || cell.State != InteriorCellState.ItemPlaced || string.IsNullOrEmpty(cell.ItemId)) return false;
            string itemId = cell.ItemId;
            if (!_slots.TryRemoveItem(hubId, cellIndex)) return false;
            _inventory.Add(itemId, 1);
            if (_stockPaid.Remove(Key(hubId, cellIndex)) && Stock != null) Stock.Add(itemId, 1); // 스토리 051: 재고로 놓은 칸은 재고로
            else Refund?.Invoke(itemId);
            ItemRemoved?.Invoke(hubId, cellIndex, itemId);
            return true;
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
    }
}
