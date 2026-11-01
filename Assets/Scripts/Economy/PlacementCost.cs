using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Economy
{
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
