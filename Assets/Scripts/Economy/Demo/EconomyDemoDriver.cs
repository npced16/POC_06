using System;
using System.Linq;

using IronRing.Contracts;
using IronRing.Hub;

namespace IronRing.Economy.Demo
{
    /// <summary>
    /// 스토리 040 데모(UI 없이 로그 "ECON: …"로 확인). 인자: --economy-demo --economy-stage trade|ledger|bankrupt (단계 없으면 trade).
    ///   trade:    거점에서 자동 출발 → 데이터 휴식지(_demoTradeStop)에서 세션이 열리면 품목(_demoTradeGoods)을 _demoTradeCount개 사고 세션을 연 채 멈춘다.
    ///   ledger:   trade처럼 사고 바로 출발, 판매 휴식지(_demoSellStop)에서 전부 팔고, 다른 휴식지는 바로 출발 → 거점 복귀 정산 로그 후 끝.
    ///   bankrupt: 시작 잔고 _demoBankruptStart, 유지비 _demoBankruptUpkeep으로 덮어써(10 − 50) 세 번째 정산에서 파산(RunState Defeat, Bankrupt).
    /// 거점 정차는 _demoHubDwellSeconds 뒤 ITrackRuntime.Depart로 출발한다. 모든 수치는 Economy.json.
    /// </summary>
    public sealed class EconomyDemoDriver : IDisposable
    {
        /// <summary>단계 이름.</summary>
        public const string TRADE = "trade", LEDGER = "ledger", BANKRUPT = "bankrupt";

        private readonly string _stage;
        private readonly EconomySettings _settings;
        private readonly ITrackRuntime _track;
        private readonly IMarkets _markets;
        private readonly ICargo _cargo;
        private readonly IMoney _money;
        private readonly ILedger _ledger;
        private readonly TradeSession _session;
        private readonly RunState _run;
        private float _dwell;
        private bool _done;

        /// <summary>실행 인자에서 단계를 읽는다. --economy-demo가 없으면 null.</summary>
        public static string ReadStage()
        {
            string[] args = Arguments();
            if (!args.Contains("--economy-demo")) return null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--economy-stage") return args[i + 1];
            return TRADE;
        }

        /// <summary>bankrupt 단계면 시작 잔고·유지비를 데모 값으로 덮어쓴다.</summary>
        public static void ApplyOverrides(string stage, EconomySettings settings)
        {
            if (stage != BANKRUPT || settings == null) return;
            settings.StartBalance = settings.DemoBankruptStart;
            settings.TrainUpkeep = settings.DemoBankruptUpkeep;
        }

        /// <summary>설치자가 만든 서비스를 주입하고 이벤트를 구독한다.</summary>
        public EconomyDemoDriver(string stage, EconomySettings settings, ITrackRuntime track, IMarkets markets, ICargo cargo, IMoney money,
            ILedger ledger, TradeSession session, RunState run)
        {
            _stage = stage; _settings = settings; _track = track; _markets = markets; _cargo = cargo; _money = money;
            _ledger = ledger; _session = session; _run = run;
            _session.Opened += OnOpened;
            _ledger.LapSettled += OnSettled;
            if (_run != null) _run.RunEnded += OnRunEnded;
            UnityEngine.Debug.Log($"ECON: demo stage={_stage} balance={_money.Balance} upkeep={_settings.TrainUpkeep}");
        }

        /// <summary>매 프레임: 거점에 서 있으면 잠시 뒤 출발.</summary>
        public void Tick(float dt)
        {
            if (_done || (_run != null && _run.IsRunLost)) return;
            ITrackNode node = _track.CurrentNode;
            if (node == null || node.Kind != TrackNodeKind.Hub || !_track.IsHeld) { _dwell = 0f; return; }
            _dwell += dt;
            if (_dwell < _settings.DemoHubDwellSeconds) return;
            _dwell = 0f;
            _track.Depart();
        }

        private void OnOpened()
        {
            string stop = _session.StopId;
            string goods = _settings.DemoTradeGoods;
            if (_stage == BANKRUPT) { _session.Depart(); return; }
            if (stop == _settings.DemoTradeStop && _cargo.CountOf(goods) == 0)
            {
                int cost = _markets.BuyCost(stop, goods, _settings.DemoTradeCount);
                bool ok = _markets.TryBuy(stop, goods, _settings.DemoTradeCount);
                UnityEngine.Debug.Log($"ECON: demo trade {stop} buy {goods} x{_settings.DemoTradeCount} ok={ok} cost={cost} balance={_money.Balance} cargo={_cargo.Used}/{_cargo.Capacity} nextPrice={_markets.Price(stop, goods)}");
                if (_stage == TRADE) { _session.AutoDepartPaused = true; _done = true; UnityEngine.Debug.Log("ECON: demo trade checkpoint (session held open)"); return; }
            }
            else if (_stage == LEDGER && stop == _settings.DemoSellStop && _cargo.CountOf(goods) > 0)
            {
                int n = Math.Min(_cargo.CountOf(goods), _markets.Demand(stop, goods));
                bool ok = n > 0 && _markets.TrySell(stop, goods, n);
                UnityEngine.Debug.Log($"ECON: demo sell {stop} {goods} x{n} ok={ok} balance={_money.Balance}");
            }
            _session.Depart();
        }

        private void OnSettled(int lap, int net)
        {
            UnityEngine.Debug.Log($"ECON: demo settled lap={lap} net={net} balance={_money.Balance} income={_ledger.TotalIncome} expense={_ledger.TotalExpense}");
            if (_stage == LEDGER) { _done = true; UnityEngine.Debug.Log("ECON: demo ledger checkpoint"); }
        }

        private void OnRunEnded()
        {
            UnityEngine.Debug.Log($"ECON: demo run ended outcome={_run.Outcome} reason={_run.LastLossReason} text={_run.LossReasonText} laps={_run.Laps} balance={_money.Balance}");
            _done = true;
        }

        /// <summary>실행 인자 + 편집기 브리지 캡처 인자(MainRoot와 같은 규칙).</summary>
        private static string[] Arguments()
        {
            string extra = "";
#if UNITY_EDITOR
            string phase = UnityEditor.SessionState.GetString("IronRing.Bridge.Phase", "");
            if (phase == "enter-play" || phase == "capture")
                extra = UnityEditor.SessionState.GetString("IronRing.Bridge.Capture.ExtraArgs", "");
#endif
            return Environment.GetCommandLineArgs().Concat(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            _session.Opened -= OnOpened;
            _ledger.LapSettled -= OnSettled;
            if (_run != null) _run.RunEnded -= OnRunEnded;
        }
    }
}
