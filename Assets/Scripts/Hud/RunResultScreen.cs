using System;
using System.Reflection;
using System.Text;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;
using DevPhase = IronRing.Contracts.HubPhase;

namespace IronRing.Hud
{
    /// <summary>
    /// 스토리 028 (design/game-brief.md Player goal &amp; fail state). 상황 화면 "RunResult": IRunResult.RunEnded에 전체 화면을 덮고
    /// 결과 제목(승리 / 부분 성공 (정착 N/4) / 패배), 사유 본문, 거점별 줄(이름 · 단계 · 번영 · 합류 NPC 수), 총 바퀴, "다시 시작" 버튼을 보인다.
    /// 옛 Defeat 화면을 대신한다(Defeat 프리팹은 남지만 IRunResult가 있으면 열리지 않는다).
    /// 다시 시작: 새 플레이 진입과 같은 순서를 흉내 낸다. Unity는 RuntimeInitializeOnLoadMethod 훅을 플레이 진입 때 한 번만 부르므로,
    /// (1) GameServices.Reset() 뒤 프로젝트의 SubsystemRegistration 훅(static 초기화, 설치자 _installed 해제)을 부르고 씬을 다시 읽은 다음,
    /// (2) 새 씬의 Awake 뒤·Start 전(sceneLoaded)에 BeforeSceneLoad 훅(설치자의 Composing 구독)과 AfterSceneLoad 훅(화면의 Ready 구독)을 부른다.
    /// 캡처 도구(전역 네임스페이스, IronRing.Tools)는 DontDestroyOnLoad로 살아 있으므로 건너뛴다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class RunResultScreen : MonoBehaviour
    {
        private static RunResultScreen _instance;

        [Header("결과 표시")]
        [SerializeField] private TMP_Text _finance;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private TMP_Text _hubs;
        [SerializeField] private TMP_Text _laps;
        [SerializeField] private Button _restart;

        private IRunResult _result;
        private UiScreens _screens;
        private UiScreen _screen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; SceneManager.sceneLoaded -= OnReloaded; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        private static void Create()
        {
            if (_instance != null || GameServices.Get<IRunResult>() == null) return;
            UiScreens screens = UiScreens.Instance;
            RunResultScreen placed = screens != null ? screens.Find<RunResultScreen>() : null;
            if (placed == null || !placed.enabled || placed._title == null || placed._hubs == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screen = GetComponent<UiScreen>();
            _result = GameServices.Get<IRunResult>();
            _result.RunEnded += Refresh;
            if (_restart != null) _restart.onClick.AddListener(Restart);
            Refresh();
        }

        /// <summary>런이 끝났으면 결과를 채우고 화면을 켠다. 아니면 끈다.</summary>
        private void Refresh()
        {
            if (_screens == null || _result == null) return;
            if (!_result.IsOver) { _screens.Hide(_screen.Id); return; }
            // 스토리 045: 마을 등급이 있으면 "정착 n/n" 대신 최종 등급으로 제목·본문을 쓴다.
            ITownTier town = GameServices.Get<ITownTier>();
            string townHub = TownUiText.DEFAULT_HUB;
            string townName = Strings.Get(townHub.Replace('-', '_'));
            if (town != null) FillTown(town, townName);
            else switch (_result.Outcome)
            {
                case RunOutcome.Victory:
                    // Story 032: 거점 수가 데이터(선로의 역 수)에 따라 바뀌므로 "정착 n/n"을 함께 보인다.
                    _title.text = string.Format(Strings.Get("result_victory_count"), _result.SettledCount, _result.HubCount);
                    _body.text = Strings.Get("result_body_victory_all");
                    break;
                case RunOutcome.Partial:
                    _title.text = string.Format(Strings.Get("result_partial"), _result.SettledCount, _result.HubCount);
                    _body.text = Strings.Get("result_body_partial");
                    break;
                default:
                    RunLossReason reason = GameServices.Get<IRunLossInfo>()?.LastLossReason ?? RunLossReason.None;
                    _title.text = Strings.Get("result_defeat");
                    _body.text = Strings.Get(reason == RunLossReason.Bankrupt ? "econ_bankrupt" : reason == RunLossReason.AllSettledCollapsed ? "result_body_collapse" : "result_body_deadline");
                    break;
            }
            var lines = new StringBuilder();
            foreach (RunHubSummary hub in _result.Hubs)
            {
                if (lines.Length > 0) lines.Append('\n');
                string phase = town != null ? town.TierNameAt(hub.HubId) : PhaseText(hub.Phase);
                IPopulation population = GameServices.Get<IPopulation>();
                int residents = population != null ? population.ResidentsAt(hub.HubId) : 0;
                lines.Append(string.Format(Strings.Get("ui_result_hub_line"), Strings.Get(hub.HubId.Replace('-', '_')), phase, residents));
            }
            _hubs.text = lines.ToString();
            if (_laps != null)
            {
                _laps.text = string.Format(Strings.Get("result_laps"), _result.Laps);
            }
            ILedger ledger = GameServices.Get<ILedger>();
            if (_finance != null) _finance.gameObject.SetActive(ledger != null);
            if (ledger != null && _finance != null)
            {
                _finance.text = string.Format(Strings.Get("ui_result_finance"), ledger.TotalIncome, ledger.TotalExpense);
                if (town != null) _finance.text += " · " + string.Format(Strings.Get("ui_tier_result"), town.TierName);
            }
            _screens.Show(_screen.Id);
        }

        /// <summary>스토리 045: 결과별 제목·본문을 최종 등급으로 채운다(승리 = 승리 등급 도달, 부분 성공 = 기한 종료 때 정착촌 이상).</summary>
        private void FillTown(ITownTier town, string townName)
        {
            switch (_result.Outcome)
            {
                case RunOutcome.Victory:
                    _title.text = string.Format(Strings.Get("ui_result_victory_tier"), town.TierName);
                    _body.text = string.Format(Strings.Get("ui_result_body_victory_town"), townName, town.TierName);
                    break;
                case RunOutcome.Partial:
                    _title.text = string.Format(Strings.Get("ui_result_partial_tier"), town.TierName);
                    _body.text = string.Format(Strings.Get("ui_result_body_partial_town"), townName, town.TierName);
                    break;
                default:
                    RunLossReason reason = GameServices.Get<IRunLossInfo>()?.LastLossReason ?? RunLossReason.None;
                    _title.text = Strings.Get("result_defeat");
                    _body.text = reason == RunLossReason.Bankrupt ? Strings.Get("econ_bankrupt")
                        : reason == RunLossReason.AllSettledCollapsed ? string.Format(Strings.Get("ui_result_body_collapse_town"), townName)
                        : string.Format(Strings.Get("ui_result_body_ruin_town"), townName, town.TierName);
                    break;
            }
        }

        private static string PhaseText(DevPhase phase)
        {
            switch (phase)
            {
                case DevPhase.Recovering: return Strings.Get("phase_recovering");
                case DevPhase.Settled: return Strings.Get("phase_settled");
                case DevPhase.Collapsed: return Strings.Get("phase_collapsed");
                default: return Strings.Get("phase_ruined");
            }
        }

        /// <summary>RunEnded를 놓친 경우(구독 전에 끝남)에도 화면을 맞춘다.</summary>
        void LateUpdate()
        {
            if (_result != null && _screen != null && _result.IsOver != _screen.gameObject.activeSelf) Refresh();
        }

        /// <summary>등록·static을 비우고 활성 씬을 다시 읽는다. 새 씬이 읽히면 BeforeSceneLoad·AfterSceneLoad 부트 훅을 다시 부른다.</summary>
        public static void Restart()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameServices.Reset();
            int reset = InvokeHooks(RuntimeInitializeLoadType.SubsystemRegistration);
            Debug.Log("RUN RESTART: reload " + scene.path + " (SubsystemRegistration hooks " + reset + ")");
            SceneManager.sceneLoaded -= OnReloaded;
            SceneManager.sceneLoaded += OnReloaded;
            if (scene.buildIndex >= 0) { SceneManager.LoadScene(scene.buildIndex); return; }
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name);
#endif
        }

        /// <summary>새 씬의 Awake 뒤, Start 전에 한 번: BeforeSceneLoad와 AfterSceneLoad 훅을 다시 불러 Composing/Ready 구독을 되살린다.</summary>
        private static void OnReloaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnReloaded;
            int before = InvokeHooks(RuntimeInitializeLoadType.BeforeSceneLoad);
            int after = InvokeHooks(RuntimeInitializeLoadType.AfterSceneLoad);
            Debug.Log("RUN RESTART: re-ran " + before + " BeforeSceneLoad + " + after + " AfterSceneLoad hooks");
        }

        /// <summary>프로젝트 어셈블리에서 loadType 훅(인자 없는 static)을 모두 부르고 수를 돌려준다. 캡처 도구는 건너뛴다.</summary>
        private static int InvokeHooks(RuntimeInitializeLoadType loadType)
        {
            int count = 0;
            foreach (Type type in typeof(RunResultScreen).Assembly.GetTypes())
            {
                if (type.Namespace == null || type.Namespace.StartsWith("IronRing.Tools", StringComparison.Ordinal)) continue;
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var hook = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    if (hook == null || hook.loadType != loadType || method.GetParameters().Length != 0) continue;
                    try { method.Invoke(null, null); count++; }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            return count;
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_result != null) _result.RunEnded -= Refresh;
            if (_restart != null) _restart.onClick.RemoveListener(Restart);
            if (_instance == this) _instance = null;
        }
    }
}
