using System;
using System.Linq;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hub;

namespace IronRing.Economy.Demo
{
    /// <summary>
    /// 스토리 048 데모. 실행 인자 `--line-demo --line-stage locked|opened`(브리지 캡처 extraArgs도 읽는다).
    /// locked: 역에 서면 출발시킨다 → 잠긴 잿골을 지날 때 LineOpener가 "LINE: pass hub-2 locked"를 남긴다. 황동골로 돌아오면 "LINE: lap done".
    /// opened: 황동골 주민·번영을 정착촌 조건(TownTiers.json _demoTierResidents/_demoTierProsperity)으로 시드하고 정산 → "LINE: opened hub-2"
    ///         → 출발 → 잿골 도착 "LINE: arrived hub-2 interior=true|false"(한 프레임 뒤 IViewMode로 판정).
    /// </summary>
    public sealed class LineDemoDriver
    {
        /// <summary>잠긴 역 통과 단계.</summary>
        public const string LOCKED = "locked";
        /// <summary>개통 후 정차 단계.</summary>
        public const string OPENED = "opened";
        private const float START_DELAY = 1.5f;

        private readonly string _stage;
        private readonly TownSettings _settings;
        private readonly Settlement _settlement;
        private readonly Population _population;
        private readonly TownTiers _tiers;
        private readonly RunState _run;
        private readonly ITrackRuntime _track;
        private float _wait;
        private bool _seeded;
        private bool _done;
        private int _checkArrival = -1;
        private string _arrivedHub;

        /// <summary>실행 인자에서 단계를 읽는다. --line-demo가 없으면 null.</summary>
        public static string ReadStage()
        {
            string[] args = DemoArguments();
            if (!args.Contains("--line-demo")) return null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--line-stage") return args[i + 1];
            return LOCKED;
        }

        /// <summary>설치자가 만든 서비스를 주입한다.</summary>
        public LineDemoDriver(string stage, TownSettings settings, Settlement settlement, Population population, TownTiers tiers, RunState run, ITrackRuntime track)
        {
            _stage = stage;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            _population = population ?? throw new ArgumentNullException(nameof(population));
            _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            _run = run;
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _track.Arrived += OnArrived;
            ILineOpener line = GameServices.Get<ILineOpener>();
            UnityEngine.Debug.Log($"LINE: demo stage={stage} locked=[{(line != null ? string.Join(",", line.LockedHubIds) : "?")}] hubs={_run?.HubCount ?? 0}");
        }

        private void OnArrived(ITrackNode node)
        {
            if (_done) return;
            if (node.Kind == TrackNodeKind.Hub && node.HubId != _settings.HubId) { _arrivedHub = node.HubId; _checkArrival = 2; }
            if (node.Index == 0 && _stage == LOCKED && _track.Lap >= 1)
            {
                UnityEngine.Debug.Log($"LINE: lap done lap={_track.Lap} hubs={_run?.HubCount ?? 0}");
                Finish();
            }
        }

        /// <summary>매 프레임.</summary>
        public void Tick(float dt)
        {
            if (_done) return;
            _wait += Math.Max(0f, dt);
            if (_wait < START_DELAY) return;
            if (_stage == OPENED && !_seeded) Seed();
            if (_checkArrival > 0 && --_checkArrival == 0)
            {
                IViewMode view = GameServices.Get<IViewMode>();
                bool interior = view != null && view.InteriorOpen && view.InteriorHubId == _arrivedHub;
                UnityEngine.Debug.Log($"LINE: arrived {_arrivedHub} interior={interior} residents={_population.ResidentsAt(_arrivedHub)} tier={_tiers.TierNameAt(_arrivedHub)} current={_tiers.CurrentHubId} hubs={_run?.HubCount ?? 0}");
                Finish();
                return;
            }
            // 역에 서 있으면 출발(내부 화면이 열려 있어도 선로 출발은 같다).
            ITrackNode node = _track.CurrentNode;
            if (_track.IsHeld && node != null && node.Kind == TrackNodeKind.Hub && _checkArrival < 0) _track.Depart();
        }

        private void Seed()
        {
            _seeded = true;
            string hub = _settings.HubId;
            _population.SetResidentsAt(hub, _settings.DemoTierResidents);
            if (_run != null) _run.ApplyDelta(hub, _settings.DemoTierProsperity - _run.GetProsperity(hub), "line-demo");
            _settlement.Settle(_settlement.SettledLap + 1);
            ILineOpener line = GameServices.Get<ILineOpener>();
            UnityEngine.Debug.Log($"LINE: seeded tier={_tiers.TierNameAt(hub)} open={(line != null && line.IsOpen("hub-2"))} hubs={_run?.HubCount ?? 0}");
        }

        private void Finish()
        {
            _done = true;
            _track.Arrived -= OnArrived;
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
