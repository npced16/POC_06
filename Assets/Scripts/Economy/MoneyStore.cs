using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Economy
{
    /// <summary>스토리 040. IMoney 구현(회사 잔고, 마르크). 순수 C#.</summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class MoneyStore : IMoney, ISaveParticipant
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

        // 스펙 003: 저장 슬롯용 잔고·최고 잔고 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "money";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 80;

        /// <summary>잔고 Balance와 최고 잔고 PeakBalance를 JSON으로 돌려준다. 상태를 바꾸지 않는다.</summary>
        public string Capture()
        {
            return UnityEngine.JsonUtility.ToJson(new MoneySave { Balance = Balance, PeakBalance = PeakBalance });
        }

        /// <summary>Capture가 만든 json으로 잔고와 최고 잔고를 바꾸고 Changed를 낸다.</summary>
        public void Restore(string json)
        {
            MoneySave state = UnityEngine.JsonUtility.FromJson<MoneySave>(json);
            Balance = state.Balance;
            PeakBalance = Math.Max(state.PeakBalance, state.Balance);
            Changed?.Invoke();
        }

        /// <summary>스펙 003. 저장 형식: 잔고와 최고 잔고.</summary>
        [Serializable]
        private sealed class MoneySave
        {
            public int Balance;
            public int PeakBalance;
        }
    }

    /// <summary>스토리 040. ILedger 구현. 거래·운임은 즉시 적고, Settle에서 유지비를 적은 뒤 바퀴를 닫는다.</summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가(장부 _entries는 이 클래스가 가진다)
    public sealed class Ledger : ILedger, ISaveParticipant
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

        // 스펙 003: 저장 슬롯용 장부 항목·지금 바퀴 꺼내기·되돌리기 추가(수입·지출 합계는 항목에서 다시 계산)
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "ledger";

        /// <summary>스펙 003. 복원 순서(돈 다음).</summary>
        public int Order => 81;

        /// <summary>적은 순서의 장부 항목 _entries와 지금 바퀴 CurrentLap을 JSON으로 돌려준다. 상태를 바꾸지 않는다.</summary>
        public string Capture()
        {
            LedgerSave state = new LedgerSave { CurrentLap = CurrentLap };
            foreach (LedgerEntry e in _entries) state.Entries.Add(new EntrySave { Lap = e.Lap, Kind = (int)e.Kind, Amount = e.Amount, Note = e.Note ?? string.Empty });
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 장부 항목과 지금 바퀴를 바꾸고 TotalIncome·TotalExpense를 항목에서 다시 계산한다.
        /// 장부에는 변경 이벤트가 없어 알림을 내지 않는다.
        /// </summary>
        public void Restore(string json)
        {
            LedgerSave state = UnityEngine.JsonUtility.FromJson<LedgerSave>(json);
            _entries.Clear();
            TotalIncome = 0;
            TotalExpense = 0;
            foreach (EntrySave saved in state.Entries)
            {
                if (saved == null) continue;
                _entries.Add(new LedgerEntry { Lap = saved.Lap, Kind = (LedgerKind)saved.Kind, Amount = saved.Amount, Note = saved.Note });
                if (saved.Amount > 0) TotalIncome += saved.Amount; else TotalExpense -= saved.Amount;
            }
            CurrentLap = state.CurrentLap;
        }

        /// <summary>스펙 003. 저장 형식: 장부 항목 목록과 지금 바퀴.</summary>
        [Serializable]
        private sealed class LedgerSave
        {
            public List<EntrySave> Entries = new List<EntrySave>();
            public int CurrentLap;
        }

        /// <summary>스펙 003. 저장 형식: 장부 항목 하나(종류는 정수).</summary>
        [Serializable]
        private sealed class EntrySave
        {
            public int Lap;
            public int Kind;
            public int Amount;
            public string Note;
        }
    }
}
