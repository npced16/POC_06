using UnityEngine;
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
    /// 스토리 022(KoreanHud 거점 패널의 UGUI 포팅). 상황 화면 "HubPanel": 거점 이름, 번영·주민 줄, 서식지 줄, 시설 목록(FacilityRow), 안내 문구, 닫기.
    /// 선로에서 거점을 클릭하면(IHubClickSource.HubClicked) 열리고, 닫기·거점 내부 열림·패배 때 닫힌다. 건설은 IFacilityBuilder 명령으로만 한다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class HubPanelScreen : MonoBehaviour
    {
        private static HubPanelScreen _instance;

        [Header("마을 상태")]
        [SerializeField] private TMP_Text _nextTier;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _state;
        [SerializeField] private TMP_Text _habitatLine;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Button _close;
        [Tooltip("시설 줄(HudCatalog의 시설 순서). 시설보다 많은 줄은 숨긴다.")]
        [SerializeField] private FacilityRow[] _rows = new FacilityRow[0];
        [Tooltip("노드 id → 문자열 키 표(HudCatalog.json).")]
        [SerializeField] private TextAsset _catalogJson;

        private ITownTier _town;
        private IPopulation _population;
        private ITrackRuntime _track;
        private IHabitatSystem _habitat;
        private INpcRecruiter _npcs;
        private IHubStateSystem _hubs;
        private IWaypointResolver _waypoints;
        private IFacilityBuilder _facilities;
        private IHubClickSource _clicks;
        private IViewMode _viewMode;
        private HudCatalog _catalog;
        private UiScreens _screens;
        private UiScreen _screen;
        private UiTheme _theme;
        private string _selectedHub;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>HUD 서비스와 MainCanvas의 화면이 있으면 한 번 바인딩한다(화면이 꺼져 있어도 구독은 여기서 한다).</summary>
        private static void Create()
        {
            if (_instance != null || !KoreanHudServices.Ready()) return;
            UiScreens screens = UiScreens.Instance;
            HubPanelScreen placed = screens != null ? screens.Find<HubPanelScreen>() : null;
            if (placed == null || !placed.enabled || placed._title == null || placed._state == null || placed._habitatLine == null
                || placed._message == null || placed._catalogJson == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        /// <summary>screens와 거점 서비스들을 저장하고 화면 및 마을 상태 갱신 이벤트를 연결한다.</summary>
        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screen = GetComponent<UiScreen>();
            _theme = screens.Theme;
            _track = GameServices.Get<ITrackRuntime>();
            _habitat = GameServices.Get<IHabitatSystem>();
            _npcs = GameServices.Get<INpcRecruiter>();
            _hubs = GameServices.Get<IHubStateSystem>();
            _waypoints = GameServices.Get<IWaypointResolver>();
            _facilities = GameServices.Get<IFacilityBuilder>();
            _clicks = GameServices.Get<IHubClickSource>();
            _viewMode = GameServices.Get<IViewMode>();
            _catalog = JsonUtility.FromJson<HudCatalog>(_catalogJson.text);
            if (_close != null) _close.onClick.AddListener(ClosePanel);
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] == null) continue;
                bool used = i < _catalog.Facilities.Length;
                _rows[i].gameObject.SetActive(used);
                if (!used || _rows[i].Build == null) continue;
                string id = _catalog.Facilities[i];
                _rows[i].Build.onClick.AddListener(() => Build(id));
            }
            _track.Arrived += OnArrived; _track.LapCompleted += OnLap;
            _habitat.EventFlagRaised += OnHub; _npcs.NpcJoined += OnPair;
            _hubs.ProsperityChanged += OnProsperity; _hubs.HubRuined += OnHub; _hubs.RunLost += Refresh;
            _waypoints.EncounterStarted += OnArrived; _waypoints.EncounterEnded += OnEncounterEnded;
            _facilities.FacilityBuilt += OnPair; _clicks.HubClicked += OpenHub;
            if (_viewMode != null) _viewMode.InteriorChanged += OnInteriorChanged;
            ScreenTicker.Add(ResolveTown); ResolveTown();
            Refresh();
        }

        private bool CanManage() => !_hubs.IsRunLost && _selectedHub != null && !_hubs.IsRuined(_selectedHub)
            && _track.IsHeld && !_waypoints.IsEncounterActive && _track.CurrentNode?.Kind == TrackNodeKind.Hub
            && _track.CurrentNode.HubId == _selectedHub;

        /// <summary>선택 거점이 있고 패배·거점 내부가 아니면 화면을 켜고 내용을 채운다. 아니면 끈다.</summary>
        private void Refresh()
        {
            if (_screens == null) return;
            bool interiorOpen = _viewMode != null && _viewMode.InteriorOpen;
            bool visible = _selectedHub != null && !_hubs.IsRunLost && !interiorOpen;
            if (visible) _screens.Show(_screen.Id); else _screens.Hide(_screen.Id);
            if (!visible) return;
            bool canManage = CanManage();
            _title.text = Strings.Get(_catalog.NodeKey(_selectedHub));
            _state.text = Strings.Get(_hubs.IsRuined(_selectedHub) ? "hub_ruined" : "hud_prosperity") + "  " + _hubs.GetProsperity(_selectedHub).ToString("0")
                + "    " + Strings.Get("hud_npcs") + "  " + _npcs.NpcsAt(_selectedHub).Count;
            _habitatLine.text = Strings.Get("hud_habitat") + "  " + Strings.Get(_habitat.IsHabitatComplete(_selectedHub) ? "hud_complete" : "hud_incomplete")
                + (_town == null ? SettleText(_selectedHub) : "");
            _nextTier.gameObject.SetActive(_town != null);
            if (_town != null)
            {
                int residents = _population?.Residents ?? 0;
                int prosperity = Mathf.RoundToInt(_hubs.GetProsperity(_selectedHub));
                _state.text = string.Format(Strings.Get("ui_tier_state"), _town.TierName, residents, prosperity);
                _nextTier.text = TownUiText.Next(_town, residents, prosperity);
            }
            _message.text = canManage ? Strings.Get("train_stopped") : Strings.Get("hud_unavailable");
            for (int i = 0; i < _rows.Length && i < _catalog.Facilities.Length; i++)
            {
                FacilityRow row = _rows[i];
                if (row == null) continue;
                string id = _catalog.Facilities[i];
                int count = 0;
                foreach (string built in _facilities.FacilitiesAt(_selectedHub)) if (built == id) count++;
                if (row.Label != null) row.Label.text = Strings.Get("facility_" + id) + "  " + count;
                if (row.Build == null) continue;
                bool on = canManage && _facilities.CanBuild(_selectedHub, id);
                row.Build.interactable = on;
                TMP_Text label = row.Build.GetComponentInChildren<TMP_Text>(true);
                if (label != null && _theme != null) label.color = on ? _theme.Text : _theme.Muted;
            }
        }

        /// <summary>늦게 준비되는 주민·등급 서비스를 찾아 변경 이벤트에 연결하고 준비 후 탐색을 끝낸다.</summary>
        private void ResolveTown()
        {
            bool changed = false;
            if (_town == null)
            {
                _town = GameServices.Get<ITownTier>();
                if (_town != null) { _town.TierChanged += OnTierChanged; changed = true; }
            }
            if (_population == null)
            {
                _population = GameServices.Get<IPopulation>();
                if (_population != null) { _population.Changed += Refresh; changed = true; }
            }
            if (changed) Refresh();
            if (_town != null && _population != null) ScreenTicker.Remove(ResolveTown);
        }

        /// <summary>number 등급 상승을 받아 거점 패널의 이름과 다음 조건을 갱신한다.</summary>
        private void OnTierChanged(int number) { Refresh(); }

        /// <summary>등록된 거점이면 선택하고 패널을 연다.</summary>
        private void OpenHub(string hubId)
        {
            bool known = false;
            foreach (string id in _hubs.HubIds) if (id == hubId) known = true;
            if (!known) return;
            _selectedHub = hubId; Refresh();
        }

        private void ClosePanel() { _selectedHub = null; Refresh(); }

        private void Build(string facility)
        {
            if (!CanManage()) return;
            bool accepted = _facilities.TryBuild(_selectedHub, facility);
            Refresh();
            if (!accepted) _message.text = Strings.Get("hud_insufficient");
        }

        private void OnArrived(ITrackNode node) { Refresh(); }
        private void OnLap(int lap) { Refresh(); }
        private void OnHub(string hub) { Refresh(); }
        private void OnPair(string hub, string id) { Refresh(); }
        private void OnProsperity(string hub, float value) { Refresh(); }
        private void OnEncounterEnded(ITrackNode node, EncounterOutcome outcome) { Refresh(); }
        private void OnInteriorChanged(bool open) { Refresh(); }

        /// <summary>스토리 028: "  ·  정착까지 번영 K"(정착 단계면 "  ·  정착"). 예보·단계 서비스가 없으면 빈 문자열.</summary>
        private string SettleText(string hubId)
        {
            IProsperityForecast forecast = GameServices.Get<IProsperityForecast>();
            IHubDevelopment development = GameServices.Get<IHubDevelopment>();
            if (forecast == null || development == null) return "";
            if (development.GetPhase(hubId) == DevPhase.Settled) return "  ·  " + Strings.Get("phase_settled");
            int left = Mathf.Max(0, Mathf.CeilToInt(forecast.SettledThreshold(hubId) - _hubs.GetProsperity(hubId)));
            return "  ·  " + Strings.Get("hub_to_settle") + " " + left;
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            ScreenTicker.Remove(ResolveTown);
            if (_town != null) _town.TierChanged -= OnTierChanged;
            if (_population != null) _population.Changed -= Refresh;
            if (_instance == this) _instance = null;
            if (_track == null) return;
            _track.Arrived -= OnArrived; _track.LapCompleted -= OnLap;
            _habitat.EventFlagRaised -= OnHub; _npcs.NpcJoined -= OnPair;
            _hubs.ProsperityChanged -= OnProsperity; _hubs.HubRuined -= OnHub; _hubs.RunLost -= Refresh;
            _waypoints.EncounterStarted -= OnArrived; _waypoints.EncounterEnded -= OnEncounterEnded;
            _facilities.FacilityBuilt -= OnPair; _clicks.HubClicked -= OpenHub;
            if (_viewMode != null) _viewMode.InteriorChanged -= OnInteriorChanged;
        }
    }
}
