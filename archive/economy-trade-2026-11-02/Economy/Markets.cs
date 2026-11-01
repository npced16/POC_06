using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 040 (design/game-brief.md "휴식지는 시장이다"). IMarkets 구현. 휴식지·품목마다 변동 계수(factor, 시작 1)·재고·수요를 갖는다.
    ///   살 때 가격 = max(1, round(기준가 × 배율 × factor)), 팔 때 가격 = max(1, round(살 때 가격 × _sellRatio)) (반올림은 0에서 먼 쪽).
    ///   1개 살 때마다 factor += _buyPriceStep, 1개 팔 때마다 factor −= _sellPriceStep, [_minPriceFactor, _maxPriceFactor]로 자른다.
    ///   n개 거래는 한 개씩 가격을 매겨 합한다(원자적: 돈·칸·재고/수요가 모두 될 때만).
    ///   바퀴마다(RecoverLap) factor = 1 + (factor − 1) × (1 − _recoverRatio), 재고·수요는 데이터 값으로 복귀.
    /// 시장이 없는 노드(거점 등)는 모든 조회가 0/-1이다.
    /// </summary>
    public sealed class Markets : IMarkets
    {
        private sealed class Slot
        {
            public MarketGoodsData Data;
            public int BasePrice;
            public float Factor = 1f;
            public int Stock;
            public int Demand;
        }

        private readonly Dictionary<string, Dictionary<string, Slot>> _stops = new Dictionary<string, Dictionary<string, Slot>>();
        private readonly List<string> _stopIds = new List<string>();
        private readonly GoodsCatalog _catalog;
        private readonly EconomySettings _settings;
        private readonly IMoney _money;
        private readonly ICargo _cargo;
        private readonly Ledger _ledger;

        /// <summary>품목 id(카탈로그 순서).</summary>
        public IReadOnlyList<string> GoodsIds => _catalog.Ids;
        /// <summary>시장이 있는 정차지 id(데이터 순서).</summary>
        public IReadOnlyList<string> StopIds => _stopIds;
        /// <summary>가격·재고·수요가 바뀔 때마다.</summary>
        public event Action Changed;

        /// <summary>데이터와 돈·화물칸·장부를 주입한다. 카탈로그에 없는 품목 줄은 경고 후 건너뛴다.</summary>
        public Markets(IReadOnlyList<MarketStopData> stops, GoodsCatalog catalog, EconomySettings settings, IMoney money, ICargo cargo, Ledger ledger)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _money = money ?? throw new ArgumentNullException(nameof(money));
            _cargo = cargo ?? throw new ArgumentNullException(nameof(cargo));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            foreach (MarketStopData stop in stops)
            {
                var slots = new Dictionary<string, Slot>();
                foreach (MarketGoodsData g in stop.Goods)
                {
                    GoodsDefinition def = catalog.Get(g.Id);
                    if (def == null) { UnityEngine.Debug.LogWarning("Markets.json: unknown goods " + g.Id + " at " + stop.StopId); continue; }
                    slots[g.Id] = new Slot { Data = g, BasePrice = def.BasePrice, Stock = Math.Max(0, g.Stock), Demand = Math.Max(0, g.Demand) };
                }
                _stops[stop.StopId] = slots;
                _stopIds.Add(stop.StopId);
            }
        }

        /// <summary>시장이 있는 정차지인지.</summary>
        public bool HasMarket(string stopId) => stopId != null && _stops.ContainsKey(stopId);

        /// <summary>1개 살 때 가격(모르면 0).</summary>
        public int Price(string stopId, string goodsId) { Slot s = Find(stopId, goodsId); return s == null ? 0 : BuyPriceAt(s, s.Factor); }

        /// <summary>1개 팔 때 가격(모르면 0).</summary>
        public int SellPrice(string stopId, string goodsId) { Slot s = Find(stopId, goodsId); return s == null ? 0 : SellPriceAt(s, s.Factor); }

        /// <summary>시장 재고(모르면 0).</summary>
        public int Stock(string stopId, string goodsId) => Find(stopId, goodsId)?.Stock ?? 0;

        /// <summary>시장 수요(모르면 0).</summary>
        public int Demand(string stopId, string goodsId) => Find(stopId, goodsId)?.Demand ?? 0;

        /// <summary>n개 전체 구매가. 재고가 모자라거나 n &lt;= 0이면 -1(돈·칸은 보지 않는다).</summary>
        public int BuyCost(string stopId, string goodsId, int n)
        {
            Slot s = Find(stopId, goodsId);
            if (s == null || n <= 0 || s.Stock < n) return -1;
            int sum = 0;
            for (int i = 0; i < n; i++) sum += BuyPriceAt(s, Clamp(s.Factor + i * _settings.BuyPriceStep));
            return sum;
        }

        /// <summary>n개 전체 판매액. 수요가 모자라거나 n &lt;= 0이면 -1(화물은 보지 않는다).</summary>
        public int SellValue(string stopId, string goodsId, int n)
        {
            Slot s = Find(stopId, goodsId);
            if (s == null || n <= 0 || s.Demand < n) return -1;
            int sum = 0;
            for (int i = 0; i < n; i++) sum += SellPriceAt(s, Clamp(s.Factor - i * _settings.SellPriceStep));
            return sum;
        }

        /// <summary>n개를 산다(재고·빈 칸·잔고가 모두 될 때만). 장부에 구매(음수)로 적는다.</summary>
        public bool TryBuy(string stopId, string goodsId, int n)
        {
            int cost = BuyCost(stopId, goodsId, n);
            if (cost < 0 || _cargo.Used + n > _cargo.Capacity || _money.Balance < cost) return false;
            if (!_money.TrySpend(cost, "buy:" + goodsId)) return false;
            if (!_cargo.TryLoad(goodsId, n)) { _money.TryGain(cost, "buy-refund:" + goodsId); return false; }
            Slot s = Find(stopId, goodsId);
            s.Stock -= n;
            s.Factor = Clamp(s.Factor + n * _settings.BuyPriceStep);
            _ledger.Record(LedgerKind.Purchase, -cost, stopId + " " + goodsId + " x" + n);
            UnityEngine.Debug.Log($"ECON: buy {stopId} {goodsId} x{n} cost={cost} balance={_money.Balance} cargo={_cargo.Used}/{_cargo.Capacity}");
            Changed?.Invoke();
            return true;
        }

        /// <summary>n개를 판다(화물·수요가 모두 될 때만). 장부에 판매(양수)로 적는다.</summary>
        public bool TrySell(string stopId, string goodsId, int n)
        {
            int value = SellValue(stopId, goodsId, n);
            if (value < 0 || _cargo.CountOf(goodsId) < n) return false;
            if (!_cargo.TryUnload(goodsId, n)) return false;
            _money.TryGain(value, "sell:" + goodsId);
            Slot s = Find(stopId, goodsId);
            s.Demand -= n;
            s.Factor = Clamp(s.Factor - n * _settings.SellPriceStep);
            _ledger.Record(LedgerKind.Sale, value, stopId + " " + goodsId + " x" + n);
            UnityEngine.Debug.Log($"ECON: sell {stopId} {goodsId} x{n} value={value} balance={_money.Balance} cargo={_cargo.Used}/{_cargo.Capacity}");
            Changed?.Invoke();
            return true;
        }

        /// <summary>바퀴 복귀: 변동 계수를 1 쪽으로 _recoverRatio만큼 되돌리고 재고·수요를 데이터 값으로 채운다.</summary>
        public void RecoverLap()
        {
            float keep = 1f - Math.Max(0f, Math.Min(1f, _settings.RecoverRatio));
            foreach (Dictionary<string, Slot> slots in _stops.Values)
                foreach (Slot s in slots.Values)
                {
                    s.Factor = Clamp(1f + (s.Factor - 1f) * keep);
                    s.Stock = Math.Max(0, s.Data.Stock);
                    s.Demand = Math.Max(0, s.Data.Demand);
                }
            Changed?.Invoke();
        }

        private Slot Find(string stopId, string goodsId)
        {
            if (stopId == null || goodsId == null || !_stops.TryGetValue(stopId, out Dictionary<string, Slot> slots)) return null;
            return slots.TryGetValue(goodsId, out Slot s) ? s : null;
        }

        private float Clamp(float factor) => Math.Max(_settings.MinPriceFactor, Math.Min(_settings.MaxPriceFactor, factor));

        private static int BuyPriceAt(Slot s, float factor) =>
            Math.Max(1, (int)Math.Round(s.BasePrice * s.Data.Multiplier * factor, MidpointRounding.AwayFromZero));

        private int SellPriceAt(Slot s, float factor) =>
            Math.Max(1, (int)Math.Round(BuyPriceAt(s, factor) * _settings.SellRatio, MidpointRounding.AwayFromZero));
    }
}
