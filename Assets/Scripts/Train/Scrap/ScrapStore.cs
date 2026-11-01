using System;

using IronRing.Contracts;

namespace IronRing.Train.Scrap
{
    /// <summary>스토리 038. IScrap 구현: 런 동안 고철 양을 쥔다(순수 C#). 늘 때 "SCRAP: +n" 로그를 남긴다.</summary>
    public sealed class ScrapStore : IScrap
    {
        /// <summary>시작 양으로 만든다.</summary>
        public ScrapStore(int start) { Amount = Math.Max(0, start); }

        public int Amount { get; private set; }
        public event Action Changed;

        public bool TryGain(int amount)
        {
            if (amount < 0) return false;
            if (amount == 0) return true;
            Amount += amount;
            UnityEngine.Debug.Log($"SCRAP: +{amount} total={Amount}");
            Changed?.Invoke();
            return true;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Amount) return false;
            if (amount == 0) return true;
            Amount -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
