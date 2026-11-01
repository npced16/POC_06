using System;
using System.Linq;

namespace IronRing.Economy.Demo
{
    /// <summary>
    /// 스토리 040·045 데모(UI 없이 로그 "ECON: …"). 인자: --economy-demo --economy-stage ledger (단계 없으면 ledger).
    ///   ledger: 시작 1초 뒤 Settlement.Settle(다음 바퀴)를 한 번 불러 정산 로그(건물 수입·운임·유지비·순이익)를 남긴다.
    /// 045에서 trade·bankrupt 단계는 시장·파산과 함께 빠졌다.
    /// </summary>
    public sealed class EconomyDemoDriver
    {
        /// <summary>단계 이름.</summary>
        public const string LEDGER = "ledger";
        private const float START_DELAY = 1f;

        private readonly string _stage;
        private readonly Settlement _settlement;
        private float _wait;
        private bool _done;

        /// <summary>실행 인자에서 단계를 읽는다. --economy-demo가 없으면 null.</summary>
        public static string ReadStage()
        {
            string[] args = DemoArguments();
            if (!args.Contains("--economy-demo")) return null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--economy-stage") return args[i + 1];
            return LEDGER;
        }

        /// <summary>단계와 정산을 주입한다.</summary>
        public EconomyDemoDriver(string stage, Settlement settlement)
        {
            _stage = stage;
            _settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            UnityEngine.Debug.Log($"ECON: demo stage={stage}");
        }

        /// <summary>매 프레임.</summary>
        public void Tick(float dt)
        {
            if (_done) return;
            _wait += Math.Max(0f, dt);
            if (_wait < START_DELAY) return;
            _done = true;
            if (_stage != LEDGER) { UnityEngine.Debug.LogWarning($"ECON: unknown demo stage {_stage}"); return; }
            _settlement.Settle(_settlement.SettledLap + 1);
            UnityEngine.Debug.Log($"ECON: demo ledger done line=\"{_settlement.LastLine}\"");
        }
    
        /// <summary>실행 인자와 진행 중인 브리지 캡처의 extraArgs를 합친다(캡처가 끝난 뒤의 값은 무시).</summary>
        private static string[] DemoArguments()
        {
            string extra = "";
#if UNITY_EDITOR
            string phase = UnityEditor.SessionState.GetString("IronRing.Bridge.Phase", "");
            if (phase == "enter-play" || phase == "capture")
                extra = UnityEditor.SessionState.GetString("IronRing.Bridge.Capture.ExtraArgs", "");
#endif
            return Environment.GetCommandLineArgs().Concat(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }
    }
}
