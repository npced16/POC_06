using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Economy
{
    /// <summary>스토리 040. IMoney 구현(회사 잔고, 마르크). 순수 C#.</summary>
    public sealed class MoneyStore : IMoney
    {
        /// <summary>현재 잔고.</summary>
        public int Balance { get; private set; }
        /// <summary>런에서 가장 높았던 잔고(결과 재무 요약용).</summary>
        public int PeakBalance { get; private set; }
        /// <summary>잔고가 바뀔 때마다.</summary>
        public event Action Changed;

        /// <summary>시작 잔고로 만든다.</summary>
        public MoneyStore(int startBalance) { Balance = startBalance; PeakBalance = startBalance; }

        /// <summary>amount를 더한다(음수 false, 0은 true지만 알림 없음).</summary>
        public bool TryGain(int amount, string reason)
        {
            if (amount < 0) return false;
            if (amount > 0) Apply(amount);
            return true;
        }

        /// <summary>잔고가 충분할 때만 뺀다.</summary>
        public bool TrySpend(int amount, string reason)
        {
            if (amount < 0 || amount > Balance) return false;
            if (amount > 0) Apply(-amount);
            return true;
        }

        /// <summary>강제 지출(잔고 0 아래 허용). 음수는 무시.</summary>
        public void Charge(int amount, string reason) { if (amount > 0) Apply(-amount); }

        private void Apply(int delta)
        {
            Balance += delta;
            if (Balance > PeakBalance) PeakBalance = Balance;
            Changed?.Invoke();
        }
    }

    /// <summary>스토리 040. ICargo 구현(열차 화물칸: 칸 수 = 수량 합, 카탈로그에 있는 품목만).</summary>
    public sealed class Cargo : ICargo
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly GoodsCatalog _catalog;

        /// <summary>전체 칸 수.</summary>
        public int Capacity { get; }
        /// <summary>쓰인 칸 수.</summary>
        public int Used { get; private set; }
        /// <summary>적재가 바뀔 때마다.</summary>
        public event Action Changed;

        /// <summary>카탈로그와 칸 수(데이터)로 만든다.</summary>
        public Cargo(GoodsCatalog catalog, int capacity)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Capacity = Math.Max(0, capacity);
        }

        /// <summary>품목 수량(모르면 0).</summary>
        public int CountOf(string goodsId) => goodsId != null && _counts.TryGetValue(goodsId, out int n) ? n : 0;

        /// <summary>빈 칸이 n 이상이고 품목을 알 때만 싣는다.</summary>
        public bool TryLoad(string goodsId, int n)
        {
            if (n <= 0 || _catalog.Get(goodsId) == null || Used + n > Capacity) return false;
            _counts[goodsId] = CountOf(goodsId) + n;
            Used += n;
            Changed?.Invoke();
            return true;
        }

        /// <summary>수량이 n 이상일 때만 내린다.</summary>
        public bool TryUnload(string goodsId, int n)
        {
            int have = CountOf(goodsId);
            if (n <= 0 || have < n) return false;
            _counts[goodsId] = have - n;
            Used -= n;
            Changed?.Invoke();
            return true;
        }
    }

    /// <summary>스토리 040. ILedger 구현. 거래·운임은 즉시 적고, Settle에서 유지비를 적은 뒤 바퀴를 닫는다.</summary>
    public sealed class Ledger : ILedger
    {
        private readonly List<LedgerEntry> _entries = new List<LedgerEntry>();

        /// <summary>모든 항목(적은 순서).</summary>
        public IReadOnlyList<LedgerEntry> Entries => _entries;
        /// <summary>지금 적히는 바퀴(완료 바퀴 + 1).</summary>
        public int CurrentLap { get; private set; }
        /// <summary>런 전체 수입(양수).</summary>
        public int TotalIncome { get; private set; }
        /// <summary>런 전체 지출(양수).</summary>
        public int TotalExpense { get; private set; }
        /// <summary>바퀴 번호, 순이익.</summary>
        public event Action<int, int> LapSettled;

        /// <summary>완료된 바퀴 수로 만든다(첫 항목은 completedLaps + 1 바퀴에 적힌다).</summary>
        public Ledger(int completedLaps) { CurrentLap = completedLaps + 1; }

        /// <summary>지금 바퀴에 한 줄 적는다. 0원은 적지 않는다.</summary>
        public void Record(LedgerKind kind, int amount, string note)
        {
            if (amount == 0) return;
            _entries.Add(new LedgerEntry { Lap = CurrentLap, Kind = kind, Amount = amount, Note = note });
            if (amount > 0) TotalIncome += amount; else TotalExpense -= amount;
        }

        /// <summary>lap 바퀴 kind 합계(부호 있음).</summary>
        public int Total(int lap, LedgerKind kind)
        {
            int sum = 0;
            foreach (LedgerEntry e in _entries) if (e.Lap == lap && e.Kind == kind) sum += e.Amount;
            return sum;
        }

        /// <summary>lap 바퀴 순이익.</summary>
        public int Net(int lap)
        {
            int sum = 0;
            foreach (LedgerEntry e in _entries) if (e.Lap == lap) sum += e.Amount;
            return sum;
        }

        /// <summary>lap(완료된 바퀴)을 닫고 LapSettled를 낸다. 이후 항목은 lap + 1에 적힌다. 지난 바퀴는 무시하고 0을 돌려준다.</summary>
        public int Settle(int lap)
        {
            if (lap < CurrentLap - 1) return 0;
            int net = Net(lap);
            CurrentLap = lap + 1;
            LapSettled?.Invoke(lap, net);
            return net;
        }
    }
}
