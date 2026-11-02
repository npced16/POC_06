using System;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Habitats.Demo
{
    /// <summary>
    /// 스토리 050 데모(--unlock-demo, 단계 --unlock-stage start|first|all, 기본 all). 실행 인자 또는 진행 중인 브리지 캡처의 extraArgs에서 읽는다.
    /// 데모 거점(Resources/Habitats/UnlockDemo.json _hubId) 내부를 열어 핫바가 보이게 한 뒤, _chain의 건물을 _stepSeconds 간격으로 차례로
    /// "처음 완성"시킨다. 완성은 HabitatCodex.MarkBuilt(IHabitatClusters.HabitatCompleted가 부르는 같은 경로)로 흉내 내며 타일은 놓지 않는다.
    /// 로그: TileUnlockNotices가 "UNLOCK: tile … by …"·"UNLOCK: recipe … by …"를 내고, 여기서 "UNLOCK: build …"(새 설계도가 없으면
    /// "UNLOCK: recipe none by …"), 끝에 같은 건물 재완성 "UNLOCK: rebuild … changed=False"와 "UNLOCK: all tiles n/m".
    /// </summary>
    public sealed class UnlockDemoDriver : MonoBehaviour
    {
        private const string FLAG = "--unlock-demo";
        private const string STAGE_FLAG = "--unlock-stage";
        private const string SETTINGS_PATH = "Habitats/UnlockDemo";
        /// <summary>시작 상태(타일 5종) 그대로.</summary>
        public const string STAGE_START = "start";
        /// <summary>첫 건물(_chain[0])만 짓고 멈춘다(해금 직후 캡처용).</summary>
        public const string STAGE_FIRST = "first";
        /// <summary>사슬 끝까지.</summary>
        public const string STAGE_ALL = "all";

        private static UnlockDemoDriver _instance;
        private Settings _settings;
        private int _waited;
        private bool _opened;
        private bool _started;
        private bool _done;
        private int _step;
        private float _nextAt;

        /// <summary>데모 단계. --unlock-demo가 없으면 null.</summary>
        public static string Stage { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Ready -= Create;
            _instance = null;
            Stage = null;
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
            Stage = at >= 0 && at + 1 < args.Length ? args[at + 1] : STAGE_ALL;
        }

        private static void Create()
        {
            if (Stage == null) ReadStage();
            if (_instance != null || Stage == null) return;
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>(SETTINGS_PATH);
            if (asset == null) { UnityEngine.Debug.LogError("UNLOCK: Resources/" + SETTINGS_PATH + ".json 없음"); return; }
            _instance = new GameObject(nameof(UnlockDemoDriver)).AddComponent<UnlockDemoDriver>();
            _instance._settings = JsonUtility.FromJson<Settings>(asset.text) ?? new Settings();
            Application.runInBackground = true;
            UnityEngine.Debug.Log("UNLOCK: demo stage=" + Stage);
        }

        void Update()
        {
            if (_done || _settings == null) return;
            IViewMode view = GameServices.Get<IViewMode>();
            if (view != null && (!view.InteriorOpen || view.InteriorHubId != _settings._hubId))
            {
                if (!_opened) { _opened = true; GameServices.Get<IInteriorNavigation>()?.Open(_settings._hubId); }
                return;
            }
            if (_waited++ < _settings._waitFrames) return;
            HabitatCodex codex = HabitatsInstaller.Codex;
            if (codex == null) { UnityEngine.Debug.LogError("UNLOCK: HabitatCodex가 설치되지 않았습니다."); _done = true; return; }
            if (!_started)
            {
                _started = true;
                UnityEngine.Debug.Log($"UNLOCK: start tiles {string.Join(",", codex.UnlockedTileIds)} ({codex.UnlockedTileIds.Count}/{codex.ManagedTileIds.Count}) recipes {string.Join(",", codex.UnlockedTypeIds)}");
                if (Stage == STAGE_START) { _done = true; return; }
                _nextAt = Time.realtimeSinceStartup;
            }
            if (Time.realtimeSinceStartup < _nextAt) return;
            string[] chain = _settings._chain ?? Array.Empty<string>();
            if (_step < chain.Length && !(Stage == STAGE_FIRST && _step >= 1))
            {
                Build(codex, chain[_step++]);
                _nextAt = Time.realtimeSinceStartup + Mathf.Max(0f, _settings._stepSeconds);
                return;
            }
            Finish(codex, chain);
        }

        private static void Build(HabitatCodex codex, string typeId)
        {
            int recipesBefore = codex.UnlockedTypeIds.Count;
            bool first = codex.MarkBuilt(typeId);
            UnityEngine.Debug.Log($"UNLOCK: build {typeId} first={first} tiles {codex.UnlockedTileIds.Count}/{codex.ManagedTileIds.Count} recipes {codex.UnlockedTypeIds.Count}/{codex.TotalCount}");
            if (first && codex.UnlockedTypeIds.Count == recipesBefore) UnityEngine.Debug.Log($"UNLOCK: recipe none by {typeId}");
        }

        private void Finish(HabitatCodex codex, string[] chain)
        {
            _done = true;
            if (Stage == STAGE_ALL && chain.Length > 0)
            {
                int tiles = codex.UnlockedTileIds.Count, recipes = codex.UnlockedTypeIds.Count;
                bool again = codex.MarkBuilt(chain[0]);
                bool changed = again || tiles != codex.UnlockedTileIds.Count || recipes != codex.UnlockedTypeIds.Count;
                UnityEngine.Debug.Log($"UNLOCK: rebuild {chain[0]} changed={changed}");
            }
            string unknown = string.Join(",", HabitatsInstaller.Catalog.Types.Select(t => t.Id).Where(id => !codex.IsUnlocked(id)));
            UnityEngine.Debug.Log($"UNLOCK: recipes known {codex.UnlockedTypeIds.Count}/{codex.TotalCount} unknown=[{unknown}]");
            UnityEngine.Debug.Log($"UNLOCK: all tiles {codex.UnlockedTileIds.Count}/{codex.ManagedTileIds.Count}");
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
            public int _waitFrames = 30;
            public float _stepSeconds = 2.5f;
            public string[] _chain;
        }
#pragma warning restore 0649
    }
}
