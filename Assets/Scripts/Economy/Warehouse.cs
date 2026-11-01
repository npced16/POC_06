using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 041 (design/game-brief.md "생산 건물"). 거점 창고. 품목별 수량을 세고 전체 합을 상한(Economy.json _warehouseCapacity) 안에 둔다. 순수 C#.
    /// 생산(Production)이 바퀴 정산 때 넣고 빼며, UI 창고 패널은 ITransfer로 화물칸과 주고받는다.
    /// </summary>
    public sealed class Warehouse : IWarehouse
    {
        private readonly Func<string, bool> _isKnown;
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>전체 상한.</summary>
        public int Capacity { get; }
        /// <summary>모든 품목 수량 합.</summary>
        public int Total { get; private set; }
        /// <summary>수량이 바뀔 때마다.</summary>
        public event Action Changed;

        /// <summary>isKnown은 품목 id 검사(GoodsCatalog), capacity는 상한(0 이상).</summary>
        public Warehouse(Func<string, bool> isKnown, int capacity)
        {
            _isKnown = isKnown ?? (_ => true);
            Capacity = Math.Max(0, capacity);
        }

        /// <summary>남은 자리.</summary>
        public int Free => Capacity - Total;

        /// <summary>품목 수량.</summary>
        public int CountOf(string goodsId) => goodsId != null && _counts.TryGetValue(goodsId, out int n) ? n : 0;

        /// <summary>n개 넣는다(상한 초과면 아무것도 바꾸지 않고 false).</summary>
        public bool TryAdd(string goodsId, int n)
        {
            if (n <= 0 || goodsId == null || !_isKnown(goodsId) || n > Free) return false;
            _counts[goodsId] = CountOf(goodsId) + n;
            Total += n;
            Changed?.Invoke();
            return true;
        }

        /// <summary>n개 꺼낸다.</summary>
        public bool TryTake(string goodsId, int n)
        {
            if (n <= 0 || CountOf(goodsId) < n) return false;
            _counts[goodsId] = CountOf(goodsId) - n;
            Total -= n;
            Changed?.Invoke();
            return true;
        }
    }

    /// <summary>
    /// 스토리 041. ITransfer 구현. canTransfer(내부 화면이 창고 거점에서 열려 있음)일 때만 창고 ↔ 화물칸을 원자적으로 옮긴다.
    /// 돈·장부는 움직이지 않는다(자기 물건을 옮길 뿐).
    /// </summary>
    public sealed class Transfer : ITransfer
    {
        private readonly IWarehouse _warehouse;
        private readonly ICargo _cargo;
        private readonly Func<bool> _canTransfer;

        /// <summary>창고·화물칸과 이동 가능 조건을 주입한다(null이면 언제나 가능).</summary>
        public Transfer(IWarehouse warehouse, ICargo cargo, Func<bool> canTransfer)
        {
            _warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
            _cargo = cargo ?? throw new ArgumentNullException(nameof(cargo));
            _canTransfer = canTransfer;
        }

        /// <summary>지금 옮길 수 있는지.</summary>
        public bool CanTransfer => _canTransfer == null || _canTransfer();

        /// <summary>창고 → 화물칸.</summary>
        public bool TryLoad(string goodsId, int n)
        {
            if (!CanTransfer || n <= 0 || _warehouse.CountOf(goodsId) < n || _cargo.Capacity - _cargo.Used < n) return false;
            if (!_warehouse.TryTake(goodsId, n)) return false;
            if (_cargo.TryLoad(goodsId, n)) { UnityEngine.Debug.Log($"PROD: load {goodsId} x{n} warehouse={_warehouse.CountOf(goodsId)} cargo={_cargo.CountOf(goodsId)}"); return true; }
            _warehouse.TryAdd(goodsId, n);
            return false;
        }

        /// <summary>화물칸 → 창고.</summary>
        public bool TryUnload(string goodsId, int n)
        {
            if (!CanTransfer || n <= 0 || _cargo.CountOf(goodsId) < n || _warehouse.Capacity - _warehouse.Total < n) return false;
            if (!_cargo.TryUnload(goodsId, n)) return false;
            if (_warehouse.TryAdd(goodsId, n)) { UnityEngine.Debug.Log($"PROD: unload {goodsId} x{n} warehouse={_warehouse.CountOf(goodsId)} cargo={_cargo.CountOf(goodsId)}"); return true; }
            _cargo.TryLoad(goodsId, n);
            return false;
        }
    }

    /// <summary>
    /// 스토리 041. IPlacementCost 구현이자 ItemPlacement 결제 창구. 가격은 ItemCatalog.json _price, 환급은 round(가격 × _refundRatio).
    /// 결제는 IMoney.TrySpend(모자라면 거절), 환급은 IMoney.TryGain. 장부에는 적지 않는다(건설 투자는 바퀴 손익과 따로 본다).
    /// </summary>
    public sealed class PlacementCost : IPlacementCost
    {
        private readonly Func<string, int> _price;
        private readonly IMoney _money;
        private readonly float _refundRatio;

        /// <summary>price는 아이템 id → 가격(모르면 0).</summary>
        public PlacementCost(Func<string, int> price, IMoney money, float refundRatio)
        {
            _price = price ?? (_ => 0);
            _money = money ?? throw new ArgumentNullException(nameof(money));
            _refundRatio = Math.Max(0f, refundRatio);
        }

        /// <summary>배치 가격.</summary>
        public int PriceOf(string itemId) => itemId == null ? 0 : Math.Max(0, _price(itemId));
        /// <summary>환급액.</summary>
        public int RefundOf(string itemId) => (int)Math.Round(PriceOf(itemId) * _refundRatio, MidpointRounding.AwayFromZero);
        /// <summary>잔고로 놓을 수 있는지.</summary>
        public bool CanAfford(string itemId) => _money.Balance >= PriceOf(itemId);

        /// <summary>결제한다(가격 0이면 언제나 true).</summary>
        public bool TryCharge(string itemId)
        {
            int price = PriceOf(itemId);
            return price <= 0 || _money.TrySpend(price, "build:" + itemId);
        }

        /// <summary>환급한다.</summary>
        public void Refund(string itemId)
        {
            int refund = RefundOf(itemId);
            if (refund > 0) _money.TryGain(refund, "refund:" + itemId);
        }
    }
}
