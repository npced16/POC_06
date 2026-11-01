using System;

using IronRing.Contracts;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 040 (design/game-brief.md "바퀴마다 장부에 정산"·"파산"). 바퀴 정산과 파산 규칙. 순수 C#.
    /// 운임: IPassengers.Disembarked(거점 하차) 1명마다 _fare를 벌고 장부에 운임으로 적는다.
    /// 정산 시점: RingTrack은 LapCompleted를 Arrived보다 먼저 내고, 승객 하차는 Arrived(GameSession, 먼저 구독)에서 일어난다.
    /// 그래서 LapCompleted로 바퀴 번호를 기억해 두고 같은 도착의 Arrived(거점)에서 정산한다 = 그 도착의 운임이 그 바퀴에 들어간다.
    /// 정산: 열차 유지비 강제 지출(잔고 0 아래 허용) → 장부 마감(LapSettled) → 토스트 한 줄(Settled) → 시장 복귀 → 파산 판정.
    /// 파산: 정산 직후 잔고 &lt; 0인 바퀴가 _bankruptLaps 연속이면 declareBankrupt(사유 문구) 한 번. 0 이상이면 연속 수를 0으로.
    /// </summary>
    public sealed class Settlement : IDisposable
    {
        private readonly EconomySettings _settings;
        private readonly MoneyStore _money;
        private readonly Ledger _ledger;
        private readonly Markets _markets;
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
        /// <summary>스토리 041. 정산 첫 단계(열차 유지비 앞)에서 부른다: 건물 생산·건물 유지비. 인자는 바퀴 번호.</summary>
        public Action<int> Producing { get; set; }
        /// <summary>정산 토스트 문구가 만들어질 때(로직 토스트; 설치자가 ToastScreen에 띄운다).</summary>
        public event Action<string> Settled;

        /// <summary>
        /// toastTemplate은 string.Format 틀({0} 바퀴, {1} 운임, {2} 판매, {3} 구매, {4} 유지비, {5} 부호 붙은 순이익),
        /// bankruptText는 파산 사유 문구, declareBankrupt는 RunState.DeclareBankrupt, isRunOver는 런 종료 여부.
        /// </summary>
        public Settlement(EconomySettings settings, MoneyStore money, Ledger ledger, Markets markets, ITrackRuntime track,
            string toastTemplate, string bankruptText, Action<string> declareBankrupt, Func<bool> isRunOver)
        {
            _settings = settings; _money = money; _ledger = ledger; _markets = markets;
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

        /// <summary>lap을 정산한다(이미 정산한 바퀴는 무시). 데모·시험도 이 경로를 쓴다.</summary>
        public void Settle(int lap)
        {
            if (lap <= _settledLap) return;
            _settledLap = lap;
            Producing?.Invoke(lap);
            _money.Charge(_settings.TrainUpkeep, "upkeep:train");
            _ledger.Record(LedgerKind.Upkeep, -_settings.TrainUpkeep, "train");
            int fare = _ledger.Total(lap, LedgerKind.Fare);
            int sale = _ledger.Total(lap, LedgerKind.Sale);
            int buy = -_ledger.Total(lap, LedgerKind.Purchase);
            int upkeep = -_ledger.Total(lap, LedgerKind.Upkeep);
            int net = _ledger.Settle(lap);
            LastLine = string.Format(_toastTemplate ?? "{0}: {5}", lap, fare, sale, buy, upkeep, Signed(net));
            UnityEngine.Debug.Log($"ECON: settle lap={lap} fare={fare} sale={sale} buy={buy} upkeep={upkeep} net={net} balance={_money.Balance}");
            Settled?.Invoke(LastLine);
            _markets.RecoverLap();
            JudgeBankruptcy(lap);
        }

        private void JudgeBankruptcy(int lap)
        {
            NegativeLaps = _money.Balance < 0 ? NegativeLaps + 1 : 0;
            if (_bankrupt || NegativeLaps < _settings.BankruptLaps || _isRunOver()) return;
            _bankrupt = true;
            UnityEngine.Debug.Log($"ECON: bankrupt lap={lap} balance={_money.Balance} negativeLaps={NegativeLaps}");
            _declareBankrupt?.Invoke(_bankruptText);
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
            Producing = null;
        }
    }

    /// <summary>
    /// 스토리 040. ITradeSession 구현. 시장이 있는 휴식지에 도착하면 열리며 선로에 정차 유지("trade-market")를 건다.
    /// 휴식지 이벤트(WaypointResolver)의 정차 유지와 독립이라, 이벤트가 끝나도 세션이 열려 있으면 열차는 서 있다.
    /// Depart() 또는 _autoDepartSeconds가 지나면(Tick) 닫고 유지를 푼다 → TrainController가 휴식지에서 자동 출발한다.
    /// 런이 끝나면 열지 않는다.
    /// </summary>
    public sealed class TradeSession : ITradeSession, IDisposable
    {
        /// <summary>선로 정차 유지 사유.</summary>
        public const string HOLD_REASON = "trade-market";

        private readonly ITrackRuntime _track;
        private readonly IMarkets _markets;
        private readonly float _autoSeconds;
        private readonly Func<bool> _isRunOver;

        /// <summary>열려 있는지.</summary>
        public bool IsOpen { get; private set; }
        /// <summary>열린(또는 마지막) 정차지 id.</summary>
        public string StopId { get; private set; }
        /// <summary>자동 출발까지 남은 초.</summary>
        public float SecondsLeft { get; private set; }
        /// <summary>true면 자동 출발 시계가 멈춘다(데모 trade 단계).</summary>
        public bool AutoDepartPaused { get; set; }
        /// <summary>열릴 때.</summary>
        public event Action Opened;
        /// <summary>닫힐 때.</summary>
        public event Action Closed;

        /// <summary>선로·시장·자동 출발 초(데이터)를 주입하고 도착을 구독한다.</summary>
        public TradeSession(ITrackRuntime track, IMarkets markets, float autoDepartSeconds, Func<bool> isRunOver)
        {
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _markets = markets ?? throw new ArgumentNullException(nameof(markets));
            _autoSeconds = Math.Max(0f, autoDepartSeconds);
            _isRunOver = isRunOver ?? (() => false);
            _track.Arrived += OnArrived;
        }

        private void OnArrived(ITrackNode node)
        {
            if (IsOpen || node.Kind != TrackNodeKind.Waypoint || !_markets.HasMarket(node.Id) || _isRunOver()) return;
            StopId = node.Id;
            SecondsLeft = _autoSeconds;
            IsOpen = true;
            _track.RequestHold(HOLD_REASON);
            UnityEngine.Debug.Log($"ECON: trade open {node.Id} auto={_autoSeconds}s");
            Opened?.Invoke();
        }

        /// <summary>매 프레임: 자동 출발 시계를 줄이고 0이 되면 Depart.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!IsOpen) return;
            if (_isRunOver()) { Depart(); return; }
            if (AutoDepartPaused) return;
            SecondsLeft = Math.Max(0f, SecondsLeft - Math.Max(0f, deltaSeconds));
            if (SecondsLeft <= 0f) Depart();
        }

        /// <summary>거래를 끝내고 정차 유지를 푼다(닫혀 있으면 무시).</summary>
        public void Depart()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SecondsLeft = 0f;
            _track.ReleaseHold(HOLD_REASON);
            UnityEngine.Debug.Log($"ECON: trade close {StopId}");
            Closed?.Invoke();
        }

        /// <summary>열린 유지를 풀고 구독을 해제한다.</summary>
        public void Dispose()
        {
            if (IsOpen) _track.ReleaseHold(HOLD_REASON);
            IsOpen = false;
            _track.Arrived -= OnArrived;
            Opened = null; Closed = null;
        }
    }
}
