using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Habitats.Demo
{
    /// <summary>
    /// 스토리 021 캡처용 스크립트 데모(--codex-demo, 단계 --codex-stage start|lap|discover, 기본 start). 실행 인자 또는 진행 중인 브리지 캡처의
    /// extraArgs에서 읽는다. start = 런 시작 상태(시작 레시피 3개) 그대로, lap = 데이터의 바퀴 수만큼 HabitatCodex.OnLapCompleted를 불러
    /// 바퀴 완료 신호를 흉내 낸다(PhaseDemoDriver가 RunState.OnLapCompleted를 직접 부르는 것과 같은 방식, 열차는 정박 그대로),
    /// discover = 데모 거점 내부를 열고 아직 모르는 레시피(데이터, 기본 전신선)의 타일을 IItemPlacement로 놓아 완성·발견시킨다.
    /// 끝나면 StageReady를 내고, CodexScreen이 그때 도감을 연다.
    /// </summary>
    public sealed class CodexDemoDriver : MonoBehaviour
    {
        private const string FLAG = "--codex-demo";
        private const string STAGE_FLAG = "--codex-stage";
        private const string SETTINGS_PATH = "Habitats/CodexDemo";
        public const string STAGE_START = "start";
        public const string STAGE_LAP = "lap";
        public const string STAGE_DISCOVER = "discover";

        private static CodexDemoDriver _instance;
        private static Action _stageReady;
        private Settings _settings;
        private int _waited;
        private bool _opened;
        private bool _done;

        /// <summary>데모 단계(start|lap|discover). --codex-demo가 없으면 null.</summary>
        public static string Stage { get; private set; }

        /// <summary>단계 준비가 끝나 StageReady를 낸 뒤 true.</summary>
        public static bool IsStageReady { get; private set; }

        /// <summary>단계 준비가 끝난 뒤 한 번 발생한다. 이미 끝났으면 구독 즉시 호출된다.</summary>
        public static event Action StageReady
        {
            add { _stageReady += value; if (IsStageReady) value?.Invoke(); }
            remove { _stageReady -= value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Ready -= Create;
            _instance = null;
            _stageReady = null;
            Stage = null;
            IsStageReady = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            ReadStage();
            GameServices.Ready -= Create;
            GameServices.Ready += Create;
        }

        private static void ReadStage()
        {
            string[] args = Arguments();
            if (!args.Contains(FLAG)) return;
            int at = Array.IndexOf(args, STAGE_FLAG);
            Stage = at >= 0 && at + 1 < args.Length ? args[at + 1] : STAGE_START;
        }

        private static void Create()
        {
            if (Stage == null) ReadStage();
            if (_instance != null || Stage == null) return;
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>(SETTINGS_PATH);
            if (asset == null) { UnityEngine.Debug.LogError("CODEX DEMO: Resources/" + SETTINGS_PATH + ".json 없음"); return; }
            _instance = new GameObject(nameof(CodexDemoDriver)).AddComponent<CodexDemoDriver>();
            _instance._settings = JsonUtility.FromJson<Settings>(asset.text);
            Application.runInBackground = true;
            UnityEngine.Debug.Log("CODEX DEMO: 단계 " + Stage);
        }

        void Update()
        {
            if (_done || _settings == null) return;
            if (Stage == STAGE_DISCOVER)
            {
                IViewMode view = GameServices.Get<IViewMode>();
                if (view == null) return;
                if (!view.InteriorOpen || view.InteriorHubId != _settings._hubId)
                {
                    if (!_opened) { _opened = true; GameServices.Get<IInteriorNavigation>()?.Open(_settings._hubId); }
                    return;
                }
            }
            if (_waited++ < _settings._waitFrames) return;
            _done = true;
            try { Run(); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
            IsStageReady = true;
            _stageReady?.Invoke();
        }

        private void Run()
        {
            HabitatCodex codex = HabitatsInstaller.Codex;
            if (codex == null) { UnityEngine.Debug.LogError("CODEX DEMO: HabitatCodex가 설치되지 않았습니다."); return; }
            if (Stage == STAGE_LAP)
            {
                ITrackRuntime track = GameServices.Get<ITrackRuntime>();
                int lap = track != null ? track.Lap : 0;
                for (int i = 1; i <= Math.Max(1, _settings._laps); i++) codex.OnLapCompleted(lap + i);
            }
            else if (Stage == STAGE_DISCOVER) PlaceDiscovery();
            UnityEngine.Debug.Log($"CODEX DEMO: 해금 {codex.UnlockedTypeIds.Count}/{codex.TotalCount} → {string.Join(", ", codex.UnlockedTypeIds)}");
        }

        private void PlaceDiscovery()
        {
            string hubId = _settings._hubId;
            IHubInterior interior = GameServices.Get<IHubInterior>();
            IItemPlacement placement = GameServices.Get<IItemPlacement>();
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            if (interior == null || placement == null || clusters == null)
            {
                UnityEngine.Debug.LogError("CODEX DEMO: 필요한 서비스가 등록되지 않았습니다(IHubInterior/IItemPlacement/IHabitatClusters).");
                return;
            }
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hubId);
            foreach (Placement p in _settings._discoverPlacements ?? Array.Empty<Placement>())
            {
                IInteriorCell cell = cells.FirstOrDefault(c => c.IsUpper == p._upper && c.Row == p._row && c.Column == p._column);
                bool ok = cell != null && placement.TryPlace(hubId, cell.Index, p._itemId);
                UnityEngine.Debug.Log($"CODEX DEMO: 배치 {p._itemId} → {(p._upper ? "위" : "아래")}({p._row},{p._column}) 칸 {cell?.Index} = {ok}");
            }
            UnityEngine.Debug.Log("CODEX DEMO " + hubId + ": 완성 서식지 " + string.Join(", ", clusters.CompletedAt(hubId).Select(c => c.TypeId)));
        }

        /// <summary>실행 인자와 진행 중인 브리지 캡처의 extraArgs를 합친다(캡처가 끝난 뒤의 값은 무시).</summary>
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

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_instance == this) _instance = null;
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class Settings
        {
            public string _hubId = "hub-1";
            public int _waitFrames = 20;
            public int _laps = 1;
            public Placement[] _discoverPlacements;
        }

        [Serializable]
        private sealed class Placement
        {
            public string _itemId;
            public bool _upper;
            public int _row;
            public int _column;
        }
#pragma warning restore 0649
    }
}
