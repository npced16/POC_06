using System;

using IronRing.Contracts;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 040·045(가벼운 경제, design/game-brief.md "바퀴마다 장부에 정산"). 바퀴 정산. 순수 C#.
    /// 운임: IPassengers.Disembarked(거점 하차) 1명마다 _fare를 벌고 장부에 운임으로 적는다.
    /// 정산 시점: RingTrack은 LapCompleted를 Arrived보다 먼저 내고, 승객 하차는 Arrived(GameSession, 먼저 구독)에서 일어난다.
    /// 그래서 LapCompleted로 바퀴 번호를 기억해 두고 같은 도착의 Arrived(거점)에서 정산한다 = 그 도착의 운임이 그 바퀴에 들어간다.
    /// 정산: 건물 수입(Income, 완성 건물 _income 합)을 벌고 장부에 LedgerKind.Income → 열차 유지비 강제 지출(잔고 0 아래 허용)
    /// (048: 유지비 0이면 생략) → 장부 마감 → 토스트 한 줄(Settled) → AfterSettle(마을 등급·주민) → 파산 판정.
    /// 파산: _bankruptLaps &gt; 0일 때만, 정산 직후 잔고 &lt; 0인 바퀴가 그만큼 연속이면 declareBankrupt 한 번. 0이면 판정하지 않는다(045 기본).
    /// </summary>
    public sealed class Settlement : IDisposable
    {
        /// <summary>장부 메모(건물 수입).</summary>
        public const string INCOME_NOTE = "건물 수입";

        private readonly EconomySettings _settings;
        private readonly MoneyStore _money;
        private readonly Ledger _ledger;
        private readonly ITrackRuntime _track;
        private readonly string _toastTemplate;
        private readonly string _bankruptText;
        private readonly Action<string> _declareBankrupt;
        private readonly Func<bool> _isRunOver;
        private IPassengers _passengers;
        private int _pendingLap;
        private int _settledLap;
        private bool _bankrupt;

        /// <summary>정산 직후 잔고가 0 아래였던 연속 바퀴 수.</summary>
        public int NegativeLaps { get; private set; }
        /// <summary>마지막 정산 토스트 문구(없으면 null).</summary>
        public string LastLine { get; private set; }
        /// <summary>이미 정산한 마지막 바퀴 번호.</summary>
        public int SettledLap => _settledLap;
        /// <summary>스토리 045. 이번 바퀴 건물 수입(마르크). null이면 0.</summary>
        public Func<int> Income { get; set; }
        /// <summary>스토리 042. 토스트 뒤, 파산 판정 앞에서 부른다(마을 등급·주민 증감). 인자는 바퀴 번호.</summary>
        public Action<int> AfterSettle { get; set; }
        /// <summary>
        /// 스토리 048. 항목별 토스트 틀(ko.json econ_lap_head·econ_part_buildings·econ_part_fare·econ_part_upkeep·econ_lap_total).
        /// 다섯이 모두 있으면 0인 항목을 빼고 "바퀴 N 정산: +건물 a +운임 b = +c"로 잇는다. 없으면 생성자 toastTemplate(옛 형식).
        /// </summary>
        public string[] PartFormats { get; set; }
        /// <summary>정산 토스트 문구가 만들어질 때(설치자가 ToastScreen에 띄운다).</summary>
        public event Action<string> Settled;

        /// <summary>
        /// toastTemplate은 string.Format 틀({0} 바퀴, {1} 건물 수입, {2} 운임, {3} 유지비, {4} 부호 붙은 순이익),
        /// bankruptText는 파산 사유 문구, declareBankrupt는 RunState.DeclareBankrupt, isRunOver는 런 종료 여부.
        /// </summary>
        public Settlement(EconomySettings settings, MoneyStore money, Ledger ledger, ITrackRuntime track,
            string toastTemplate, string bankruptText, Action<string> declareBankrupt, Func<bool> isRunOver)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _money = money ?? throw new ArgumentNullException(nameof(money));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _toastTemplate = toastTemplate; _bankruptText = bankruptText;
            _declareBankrupt = declareBankrupt; _isRunOver = isRunOver ?? (() => false);
            _settledLap = track.Lap;
            _pendingLap = track.Lap;
            _track.LapCompleted += OnLapCompleted;
            _track.Arrived += OnArrived;
        }

        /// <summary>승객 하차 운임을 연결한다(여러 번 불러도 한 번만).</summary>
        public void BindPassengers(IPassengers passengers)
        {
            if (passengers == null || passengers == _passengers) return;
            if (_passengers != null) _passengers.Disembarked -= OnDisembarked;
            _passengers = passengers;
            _passengers.Disembarked += OnDisembarked;
        }

        private void OnDisembarked(string npcId, string hubId)
        {
            if (_settings.Fare <= 0) return;
            _money.TryGain(_settings.Fare, "fare:" + npcId);
            _ledger.Record(LedgerKind.Fare, _settings.Fare, npcId + "@" + hubId);
            UnityEngine.Debug.Log($"ECON: fare {npcId} +{_settings.Fare} balance={_money.Balance}");
        }

        private void OnLapCompleted(int lap) { if (lap > _pendingLap) _pendingLap = lap; }

        private void OnArrived(ITrackNode node)
        {
            if (node.Kind == TrackNodeKind.Hub && _pendingLap > _settledLap) Settle(_pendingLap);
        }

        /// <summary>lap을 정산한다(이미 정산한 바퀴는 무시). 데모도 이 경로를 쓴다.</summary>
        public void Settle(int lap)
        {
            if (lap <= _settledLap) return;
            _settledLap = lap;
            int income = Math.Max(0, Income?.Invoke() ?? 0);
            if (income > 0)
            {
                _money.TryGain(income, "income:buildings");
                _ledger.Record(LedgerKind.Income, income, INCOME_NOTE);
            }
            // 스토리 048: 유지비 0(기본)이면 돈도 장부도 건드리지 않는다.
            if (_settings.TrainUpkeep > 0)
            {
                _money.Charge(_settings.TrainUpkeep, "upkeep:train");
                _ledger.Record(LedgerKind.Upkeep, -_settings.TrainUpkeep, "train");
            }
            int buildings = _ledger.Total(lap, LedgerKind.Income);
            int fare = _ledger.Total(lap, LedgerKind.Fare);
            int upkeep = -_ledger.Total(lap, LedgerKind.Upkeep);
            int net = _ledger.Settle(lap);
            LastLine = Line(lap, buildings, fare, upkeep, net);
            UnityEngine.Debug.Log($"ECON: settle lap={lap} buildings={buildings} fare={fare} upkeep={upkeep} net={net} balance={_money.Balance}");
            Settled?.Invoke(LastLine);
            AfterSettle?.Invoke(lap);
            JudgeBankruptcy(lap);
        }

        private void JudgeBankruptcy(int lap)
        {
            if (_settings.BankruptLaps <= 0) return;
            NegativeLaps = _money.Balance < 0 ? NegativeLaps + 1 : 0;
            if (_bankrupt || NegativeLaps < _settings.BankruptLaps || _isRunOver()) return;
            _bankrupt = true;
            UnityEngine.Debug.Log($"ECON: bankrupt lap={lap} balance={_money.Balance} negativeLaps={NegativeLaps}");
            _declareBankrupt?.Invoke(_bankruptText);
        }

        /// <summary>스토리 048. 토스트 한 줄. PartFormats가 있으면 0인 항목을 뺀다.</summary>
        public string Line(int lap, int buildings, int fare, int upkeep, int net)
        {
            string[] f = PartFormats;
            if (f == null || f.Length < 5) return string.Format(_toastTemplate ?? "{0}: {4}", lap, buildings, fare, upkeep, Signed(net));
            var parts = new System.Collections.Generic.List<string> { string.Format(f[0], lap) };
            if (buildings != 0) parts.Add(string.Format(f[1], buildings));
            if (fare != 0) parts.Add(string.Format(f[2], fare));
            if (upkeep != 0) parts.Add(string.Format(f[3], upkeep));
            parts.Add(string.Format(f[4], Signed(net)));
            return string.Join(" ", parts);
        }

        /// <summary>부호 붙은 금액(+24 / −18, 빼기는 U+2212).</summary>
        public static string Signed(int amount) => amount >= 0 ? "+" + amount : "−" + (-amount);

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            _track.LapCompleted -= OnLapCompleted;
            _track.Arrived -= OnArrived;
            if (_passengers != null) _passengers.Disembarked -= OnDisembarked;
            _passengers = null;
            Settled = null;
            Income = null;
            AfterSettle = null;
        }
    }
}
