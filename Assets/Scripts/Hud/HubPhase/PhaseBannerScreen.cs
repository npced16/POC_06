using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Presentation.HubPhase;

using DevPhase = IronRing.Contracts.HubPhase;

namespace IronRing.Hud.HubPhase
{
    /// <summary>
    /// 스토리 022(PhaseHud의 UGUI 포팅). 항상 켜진 화면 "PhaseBanner": 위 가운데 "거점 · 단계 · 번영 N · 서식지 N" 띠, 정착 기한 상자,
    /// IRunLossInfo가 없을 때만 보이는 패배 사유 줄. 지도 위 단계 라벨은 HubBuilding 프리팹의 월드 TMP(MapLabel)로 옮겼다.
    /// 칸 클릭 건설 메뉴(hub_phase_view.json의 _buildMenuOnCellClick, 현재 false)는 포팅하지 않았다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class PhaseBannerScreen : MonoBehaviour
    {
        private static PhaseBannerScreen _instance;

        [Header("마을 등급")]
        [Tooltip("평소 거점·목표·기한 패널을 보여줄지. POC 기본값은 숨김.")]
        [SerializeField] private bool _showPersistentStatus;
        [SerializeField] private GameObject _progressPanel;
        [Tooltip("스토리 045: 목표 줄 \"목표: 도시 (주민 a/b · 번영 c/d) · 바퀴 e/f\".")]
        [SerializeField] private TMP_Text _goal;
        [Tooltip("스토리 045: 배너와 겹치는 로직 등급 토스트(town_tier_up)를 삼키는 시간(초, 실시간).")]
        [SerializeField] private float _tierToastSwallowSeconds = 1f;
        [SerializeField] private GameObject _tierBanner;
        [SerializeField] private TMP_Text _tierBannerText;
        [SerializeField] private float _tierBannerSeconds = 2f;
        [Header("거점 상태")]
        [SerializeField] private TMP_Text _hubLabel;
        [Tooltip("정착 기한 상자(라벨의 부모). 기한이 없으면 끈다.")]
        [SerializeField] private GameObject _deadlineBox;
        [SerializeField] private TMP_Text _deadline;
        [SerializeField] private TMP_Text _loss;
        [Tooltip("단계별 띠 글자색: 폐허, 복구 중, 정착, 붕괴.")]
        [SerializeField] private Color[] _phaseColors =
        {
            new Color32(196, 140, 108, 255), new Color32(255, 207, 106, 255), new Color32(150, 205, 140, 255), new Color32(214, 96, 80, 255),
        };

        private ITownTier _town;
        private IPopulation _population;
        private readonly System.Collections.Generic.Queue<string> _tierMessages = new System.Collections.Generic.Queue<string>();
        private readonly System.Collections.Generic.Queue<(string text, float seconds)> _bannerMessages = new System.Collections.Generic.Queue<(string text, float seconds)>();
        private float _tierBannerUntil;
        private IHubStateSystem _hubs;
        private IHubDevelopment _development;
        private ITrackRuntime _track;
        private IHabitatClusters _clusters;
        private IViewMode _view;
        private ILineOpener _lineOpener;
        private string _lastHub;
        private string _bannerKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>거점 상태·발전·선로 서비스와 MainCanvas의 화면이 있으면 한 번 바인딩한다. 없으면 예시 문구를 지우고 조용히 남는다.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            UiScreens screens = UiScreens.Instance;
            PhaseBannerScreen placed = screens != null ? screens.Find<PhaseBannerScreen>() : null;
            if (placed == null || !placed.enabled || placed._hubLabel == null) return;
            if (GameServices.Get<IHubStateSystem>() == null || GameServices.Get<IHubDevelopment>() == null || GameServices.Get<ITrackRuntime>() == null)
            {
                placed.ClearSample();
                return;
            }
            _instance = placed;
            placed.Initialize();
        }

        void Awake() { ClearSample(); }

        /// <summary>프리팹의 편집 모드 예시 문구를 숨긴다(서비스가 채우기 전).</summary>
        private void ClearSample()
        {
            if (_instance == this) return;
            if (_tierBanner != null) _tierBanner.SetActive(false);
            if (_progressPanel != null) _progressPanel.SetActive(false);
            if (_goal != null) _goal.gameObject.SetActive(false);
            if (_hubLabel != null) _hubLabel.transform.parent.gameObject.SetActive(false);
            if (_deadlineBox != null) _deadlineBox.SetActive(false);
            if (_loss != null) _loss.gameObject.SetActive(false);
        }

        /// <summary>현재 거점·선로·발전 서비스를 저장하고 상태줄의 초기 표시를 갱신한다.</summary>
        private void Initialize()
        {
            HubPhaseViewSettings settings = HubPhaseViewSettings.Load();
            if (settings.BuildMenuOnCellClick)
                UnityEngine.Debug.LogWarning("PhaseBannerScreen: _buildMenuOnCellClick=true지만 칸 클릭 건설 메뉴는 UGUI로 옮기지 않았습니다(시설은 핫바로 놓는다).");
            _hubs = GameServices.Get<IHubStateSystem>();
            _development = GameServices.Get<IHubDevelopment>();
            _track = GameServices.Get<ITrackRuntime>();
            _clusters = GameServices.Get<IHabitatClusters>();
            _view = GameServices.Get<IViewMode>();
            _lineOpener = GameServices.Get<ILineOpener>();
            if (_lineOpener != null) _lineOpener.HubOpened += OnHubOpened;
            _lastHub = _hubs.HubIds.Count > 0 ? _hubs.HubIds[0] : null;
            Refresh(true);
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

        /// <summary>띠가 보여 줄 거점: 열린 내부, 아니면 정차한 거점, 아니면 마지막으로 보인 거점.</summary>
        private string CurrentHub()
        {
            if (_town != null && !string.IsNullOrEmpty(_town.CurrentHubId)) return _lastHub = _town.CurrentHubId;
            if (_population != null && !string.IsNullOrEmpty(_population.CurrentHubId)) return _lastHub = _population.CurrentHubId;
            if (_view != null && _view.InteriorOpen && _view.InteriorHubId != null) return _lastHub = _view.InteriorHubId;
            ITrackNode node = _track.CurrentNode;
            if (node != null && node.Kind == TrackNodeKind.Hub) return _lastHub = node.HubId;
            return _lastHub;
        }

        /// <summary>띠·기한·패배 사유를 갱신한다. 값이 그대로면(force가 아니면) 문자열 작업을 건너뛴다.</summary>
        private void Refresh(bool force)
        {
            string hub = CurrentHub();
            if (hub == null) return;
            DevPhase phase = _development.GetPhase(hub);
            int prosperity = Mathf.RoundToInt(_hubs.GetProsperity(hub));
            bool anySettled = false;
            foreach (string id in _hubs.HubIds) anySettled |= _development.HasEverSettled(id);
            int laps = _development.LapsUntilDeadline;
            int habitats = _clusters != null ? _clusters.CompletedAt(hub).Count : 0;
            // 스토리 028: 정착 기준과 다음 바퀴 예상 증감(경유지 결과 제외)을 함께 보인다.
            IProsperityForecast forecast = GameServices.Get<IProsperityForecast>();
            int threshold = forecast != null ? Mathf.RoundToInt(forecast.SettledThreshold(hub)) : 0;
            int next = forecast != null ? Mathf.RoundToInt(forecast.NextLapDelta(hub)) : 0;
            string townHub = TownUiText.DEFAULT_HUB;
            int townProsperity = _town != null ? Mathf.RoundToInt(_hubs.GetProsperity(townHub)) : 0;
            int townResidents = _population != null ? _population.ResidentsAt(townHub) : 0;
            int lap = _track.Lap;
            string key = (_town != null ? _town.TierAt(hub).ToString() : "") + "|" + townResidents + "|" + townProsperity + "|" + lap + "|" + hub + "|" + phase + "|" + prosperity + "|" + laps + "|" + anySettled + "|" + _hubs.IsRunLost + "|" + habitats + "|" + threshold + "|" + next;
            if (!force && key == _bannerKey) return;
            _bannerKey = key;
            _hubLabel.transform.parent.gameObject.SetActive(_showPersistentStatus);
            string prosperityText = Strings.Get("phase_prosperity") + " " + prosperity + (forecast != null ? "/" + threshold : "");
            if (forecast != null) prosperityText += "  ·  " + Strings.Get("phase_next_lap") + " " + next.ToString("+0;-0;+0");
            _hubLabel.text = Strings.Get(hub.Replace('-', '_')) + "  ·  " + PhaseText(phase) + "  ·  " + prosperityText
                + "  ·  " + Strings.Get("phase_habitats") + " " + habitats;
            // 스토리 045: 등급 거점(hub-1)은 단계 대신 등급 이름(붕괴만 기존 문구). 다른 거점은 기존 띠 그대로.
            if (_town != null && phase != DevPhase.Collapsed)
                _hubLabel.text = Strings.Get(hub.Replace('-', '_')) + "  ·  " + _town.TierNameAt(hub) + "  ·  " + Strings.Get("phase_prosperity") + " " + prosperity;
            string goal = _town != null && !_hubs.IsRunLost ? TownUiText.Goal(_town, townResidents, townProsperity, lap, laps != int.MaxValue ? lap + laps : 0) : null;
            if (_progressPanel != null) _progressPanel.SetActive(_showPersistentStatus && goal != null);
            if (_goal != null)
            {
                _goal.gameObject.SetActive(_showPersistentStatus && goal != null);
                if (goal != null) _goal.text = goal;
            }
            int index = (int)phase;
            if (index >= 0 && index < _phaseColors.Length) _hubLabel.color = _phaseColors[index];
            // 스토리 028: 기한은 정착해도 꺼지지 않고 런을 끝낸다(IRunResult가 없으면 옛 규칙: 첫 정착까지만).
            bool runEnds = GameServices.Get<IRunResult>() != null;
            bool showDeadline = _showPersistentStatus && (runEnds || !anySettled) && laps != int.MaxValue && !_hubs.IsRunLost;
            if (_deadlineBox != null) _deadlineBox.SetActive(showDeadline);
            if (showDeadline && _deadline != null)
                _deadline.text = Strings.Get(runEnds ? "phase_run_deadline" : "phase_deadline") + ": " + laps + Strings.Get("phase_laps_left");
            // 사유별 패배 제목은 Defeat 화면이 IRunLossInfo로 보인다. 이 줄은 그 서비스가 없을 때의 대비책이라 둘이 겹치지 않는다.
            bool showLoss = _hubs.IsRunLost && GameServices.Get<IRunLossInfo>() == null;
            if (_loss != null)
            {
                _loss.gameObject.SetActive(showLoss);
                if (showLoss) _loss.text = Strings.Get("phase_loss_collapse");
            }
        }

        /// <summary>늦게 준비된 등급·주민 서비스를 연결하고 등급 상승 이벤트를 구독한다.</summary>
        private void ResolveTown()
        {
            if (_population == null) _population = GameServices.Get<IPopulation>();
            if (_town != null) return;
            _town = GameServices.Get<ITownTier>();
            if (_town != null) _town.HubTierChanged += OnHubTierChanged;
        }

        /// <summary>number 등급의 상승 문구를 큐에 넣어 각 상승을 Inspector 지정 시간 동안 표시한다.</summary>
        /// <summary>hubId 마을 등급 상승을 큐에 넣고 해당 로직 토스트를 삼킨 뒤 배너 내용을 갱신한다.</summary>
        private void OnHubTierChanged(string hubId, int number)
        {
            string tierName = _town.TierNameAt(hubId);
            IronRing.Hud.Ui.ToastScreen.Swallow(TownUiText.TierToastMatcher(tierName), _tierToastSwallowSeconds);
            _tierMessages.Enqueue(string.Format(Strings.Get("ui_tier_promoted"), Strings.Get(hubId.Replace('-', '_')), tierName));
            Refresh(true);
        }

        /// <summary>새로 열린 hubId 마을의 개통 배너를 표시하고 같은 로직 토스트의 중복을 막는다.</summary>
        private void OnHubOpened(string hubId)
        {
            string lineMessage = string.Format(Strings.Get("line_opened"), Strings.Get(hubId.Replace('-', '_')));
            IronRing.Hud.Ui.ToastScreen.Swallow(text => text == lineMessage, 2.5f);
            _bannerMessages.Enqueue((string.Format(Strings.Get("ui_line_opened"), Strings.Get(hubId.Replace('-', '_'))), 2.5f));
        }

        /// <summary>스펙 001 FR-035: 가운데 배너가 떠 있거나 띄울 문구가 남아 있으면 true를 돌려준다.</summary>
        public static bool IsBusy => _instance != null && _instance._tierBanner != null
            && (_instance._tierBanner.activeSelf || _instance._bannerMessages.Count > 0 || _instance._tierMessages.Count > 0);

        void LateUpdate()
        {
            if (_instance != this || _development == null) return;
            ResolveTown(); Refresh(false);
            if (_tierBanner == null || _tierBannerText == null) { _tierMessages.Clear(); return; }
            if (_tierBanner.activeSelf && Time.unscaledTime >= _tierBannerUntil) _tierBanner.SetActive(false);
            if (!_tierBanner.activeSelf && (_bannerMessages.Count > 0 || _tierMessages.Count > 0))
            {
                if (_bannerMessages.Count > 0)
                {
                    var message = _bannerMessages.Dequeue();
                    _tierBannerText.text = message.text;
                    _tierBannerUntil = Time.unscaledTime + message.seconds;
                }
                else
                {
                    _tierBannerText.text = _tierMessages.Dequeue();
                    _tierBannerUntil = Time.unscaledTime + _tierBannerSeconds;
                }
                _tierBanner.SetActive(true);
            }
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_town != null) _town.HubTierChanged -= OnHubTierChanged;
            if (_lineOpener != null) _lineOpener.HubOpened -= OnHubOpened;
            if (_instance == this) _instance = null;
        }
    }
}
