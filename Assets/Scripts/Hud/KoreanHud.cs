using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Localization;

namespace IronRing.Hud
{
    /// <summary>일곱 시스템의 읽기와 명령을 연결하는 한국어 HUD. 게임 상태의 소유권은 각 시스템에 있다.</summary>
    public sealed class KoreanHud : MonoBehaviour
    {
        private static KoreanHud _instance;
        [Header("서비스 연결")]
        private ITrackRuntime _track;
        private IResourceInventory _inventory;
        private IHabitatSystem _habitat;
        private INpcRecruiter _npcs;
        private IHubStateSystem _hubs;
        private IWaypointResolver _waypoints;
        private IFacilityBuilder _facilities;
        private IHubClickSource _clicks;
        private ICombatRoundCommand _rounds;
        private IEncounterPresentationSource _encounter;
        private IViewMode _viewMode;
        [Header("화면 참조")]
        private HudCatalog _catalog;
        private VisualElement _root;
        private VisualElement _panel;
        private VisualElement _slots;
        private VisualElement _buildings;
        private Label _location;
        private Label _lap;
        private Label _hubTitle;
        private Label _hubState;
        private Label _habitatState;
        private Label _message;
        private Button _depart;
        private Button _next;
        private PanelSettings _settings;
        [Header("선택 상태")]
        private string _selectedHub;
        private string _selectedResource;
        private readonly Dictionary<string, Label> _stock = new Dictionary<string, Label>();
        private readonly Dictionary<string, Button> _choices = new Dictionary<string, Button>();

        /// <summary>매 실행 정적 참조를 비우며 이전 런의 HUD 참조를 남기지 않는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        /// <summary>씬 로드 후 준비 이벤트를 구독하고 등록된 서비스로 HUD 생성을 예약한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>준비된 GameServices를 입력으로 한 번만 HUD 오브젝트와 문서를 만든다.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            // 다른 스토리의 독립 데모는 HUD 계약 전체를 등록하지 않는다.
            if (GameServices.Get<ITrackRuntime>() == null || GameServices.Get<IResourceInventory>() == null
                || GameServices.Get<IHabitatSystem>() == null || GameServices.Get<INpcRecruiter>() == null
                || GameServices.Get<IHubStateSystem>() == null || GameServices.Get<IWaypointResolver>() == null
                || GameServices.Get<IFacilityBuilder>() == null || GameServices.Get<IHubClickSource>() == null) return;
            _instance = new GameObject(nameof(KoreanHud)).AddComponent<KoreanHud>();
            _instance.Initialize();
        }

        /// <summary>등록 서비스와 제작된 카탈로그를 읽고 한국어 문서, 표시 상태 및 이벤트 구독을 초기화한다.</summary>
        private void Initialize()
        {
            _track = GameServices.Get<ITrackRuntime>();
            _inventory = GameServices.Get<IResourceInventory>();
            _habitat = GameServices.Get<IHabitatSystem>();
            _npcs = GameServices.Get<INpcRecruiter>();
            _hubs = GameServices.Get<IHubStateSystem>();
            _waypoints = GameServices.Get<IWaypointResolver>();
            _facilities = GameServices.Get<IFacilityBuilder>();
            _clicks = GameServices.Get<IHubClickSource>();
            _rounds = GameServices.Get<ICombatRoundCommand>();
            _encounter = GameServices.Get<IEncounterPresentationSource>();
            _viewMode = GameServices.Get<IViewMode>();
            _catalog = JsonUtility.FromJson<HudCatalog>(Resources.Load<TextAsset>("Hud/HudCatalog").text);
            _selectedResource = _catalog.Resources[0];
            _settings = Instantiate(Resources.Load<PanelSettings>("KoreanPanelSettings"));
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = _settings;
            document.sortingOrder = 100;
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("Hud/KoreanHud");
            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            foreach (TemplateContainer container in _root.Query<TemplateContainer>().ToList()) container.pickingMode = PickingMode.Ignore;
            _root.Q("hud").pickingMode = PickingMode.Ignore;
            // UXML과 이름이 같은 USS는 Resources 로드가 모호했으므로 별도 이름(KoreanHudStyle)으로 로드하고 누락 시 즉시 알린다.
            StyleSheet hudStyle = Resources.Load<StyleSheet>("Hud/KoreanHudStyle");
            if (hudStyle == null) Debug.LogError("KoreanHud: Resources/Hud/KoreanHudStyle.uss를 불러오지 못했습니다.");
            else if (!_root.styleSheets.Contains(hudStyle)) _root.styleSheets.Add(hudStyle);
            KoreanUi.Apply(_root);
            _location = _root.Q<Label>("location");
            _lap = _root.Q<Label>("lap");
            _panel = _root.Q("panel");
            _slots = _root.Q("slots");
            _buildings = _root.Q("buildings");
            _hubTitle = _root.Q<Label>("hub-title");
            _hubState = _root.Q<Label>("hub-state");
            _habitatState = _root.Q<Label>("habitat-state");
            _message = _root.Q<Label>("message");
            _depart = _root.Q<Button>("depart");
            _next = _root.Q<Button>("next-round");
            foreach (Label label in _root.Query<Label>(className: "localized").ToList()) label.text = Strings.Get(label.name);
            _depart.text = Strings.Get("hud_depart");
            _next.text = Strings.Get("hud_next_round");
            _depart.clicked += Depart;
            _next.clicked += AdvanceRound;
            Button close = _root.Q<Button>("close");
            close.text = Strings.Get("hud_close");
            close.clicked += ClosePanel;
            foreach (string resource in _catalog.Resources)
            {
                var cell = new VisualElement(); cell.AddToClassList("resource");
                var icon = new Image { image = Resources.Load<Texture2D>("Hud/Icons/" + resource) };
                icon.AddToClassList("icon"); cell.Add(icon);
                cell.Add(new Label(Strings.Get("resource_" + resource)));
                var count = new Label(); count.AddToClassList("quantity"); cell.Add(count);
                _stock.Add(resource, count); _root.Q("resources").Add(cell);
                string id = resource;
                var choice = new Button(() => SelectResource(id)) { text = Strings.Get("resource_" + id) };
                _choices.Add(id, choice); _root.Q("resource-choices").Add(choice);
            }
            _track.Arrived += OnArrived; _track.LapCompleted += OnLap;
            _inventory.Changed += OnResource; _habitat.EventFlagRaised += OnHub;
            _npcs.NpcJoined += OnPair; _hubs.ProsperityChanged += OnProsperity;
            _hubs.HubRuined += OnHub; _hubs.RunLost += Refresh;
            _waypoints.EncounterStarted += OnArrived; _waypoints.EncounterEnded += OnEncounterEnded;
            _facilities.FacilityBuilt += OnPair; _clicks.HubClicked += OpenHub;
            if (_viewMode != null) _viewMode.InteriorChanged += OnInteriorChanged;
            Refresh();
        }

        /// <summary>선택 거점과 현재 정차 상태를 읽어 관리 명령을 사용할 수 있는지 반환한다.</summary>
        private bool CanManage() => !_hubs.IsRunLost && _selectedHub != null && !_hubs.IsRuined(_selectedHub)
            && _track.IsHeld && !_waypoints.IsEncounterActive && _track.CurrentNode?.Kind == TrackNodeKind.Hub
            && _track.CurrentNode.HubId == _selectedHub;

        /// <summary>서비스의 현재 스냅샷을 읽어 수량, 위치, 관리 패널 및 패배 화면만 갱신한다.</summary>
        private void Refresh()
        {
            foreach (var entry in _stock) entry.Value.text = _inventory.Get(entry.Key).ToString();
            _location.text = Strings.Get("hud_location") + "  " + Strings.Get(_catalog.NodeKey(_track.CurrentNode?.Id));
            _lap.text = Strings.Get("hud_turn") + "  " + _track.Lap;
            _depart.SetEnabled(!_hubs.IsRunLost && _track.IsHeld && _track.CurrentNode?.Kind == TrackNodeKind.Hub && !_waypoints.IsEncounterActive);
            _next.style.display = _waypoints.IsEncounterActive && _encounter != null && _encounter.IsCombat ? DisplayStyle.Flex : DisplayStyle.None;
            _next.SetEnabled(!_hubs.IsRunLost && _rounds != null && _rounds.CanAdvance);
            _root.Q("defeat").style.display = _hubs.IsRunLost ? DisplayStyle.Flex : DisplayStyle.None;
            // Story 014: 패배 사유별 제목(IRunLossInfo 미등록이면 기존 defeat_title 유지).
            RunLossReason lossReason = GameServices.Get<IRunLossInfo>()?.LastLossReason ?? RunLossReason.None;
            _root.Q<Label>("defeat_title").text = Strings.Get(lossReason == RunLossReason.DeadlineExpired ? "phase_loss_deadline"
                : lossReason == RunLossReason.AllSettledCollapsed ? "phase_loss_collapse" : "defeat_title");
            // Story 012: 거점 내부 화면이 열려 있으면 거점 관리 패널은 숨긴다(상단 자원 바와 출발 버튼은 유지).
            bool interiorOpen = _viewMode != null && _viewMode.InteriorOpen;
            _panel.style.display = _selectedHub != null && !_hubs.IsRunLost && !interiorOpen ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var entry in _choices) entry.Value.EnableInClassList("selected", entry.Key == _selectedResource);
            if (_selectedHub == null) return;
            bool canManage = CanManage();
            _hubTitle.text = Strings.Get(_catalog.NodeKey(_selectedHub));
            _hubState.text = Strings.Get(_hubs.IsRuined(_selectedHub) ? "hub_ruined" : "hud_prosperity") + "  " + _hubs.GetProsperity(_selectedHub).ToString("0")
                + "    " + Strings.Get("hud_npcs") + "  " + _npcs.NpcsAt(_selectedHub).Count;
            _habitatState.text = Strings.Get("hud_habitat") + "  " + Strings.Get(_habitat.IsHabitatComplete(_selectedHub) ? "hud_complete" : "hud_incomplete");
            _root.Q("resource-choices").SetEnabled(canManage);
            _message.text = canManage ? Strings.Get("train_stopped") : Strings.Get("hud_unavailable");
            _slots.Clear();
            for (int i = 0; i < _catalog.SlotCount(_selectedHub); i++)
            {
                int slot = i;
                string content = _habitat.SlotContent(_selectedHub, i);
                var row = new VisualElement(); row.AddToClassList("slot-row");
                row.Add(new Label((i + 1) + "  " + Strings.Get(string.IsNullOrEmpty(content) ? "hud_empty" : "resource_" + content)));
                var action = new Button(() => ChangeSlot(slot)) { text = Strings.Get(string.IsNullOrEmpty(content) ? "hud_donate" : "hud_remove") };
                action.SetEnabled(canManage && (!string.IsNullOrEmpty(content) || _inventory.Get(_selectedResource) > 0));
                row.Add(action); _slots.Add(row);
            }
            _buildings.Clear();
            foreach (string facility in _catalog.Facilities)
            {
                string id = facility;
                var row = new VisualElement(); row.AddToClassList("slot-row");
                int count = 0; foreach (string built in _facilities.FacilitiesAt(_selectedHub)) if (built == id) count++;
                row.Add(new Label(Strings.Get("facility_" + id) + "  " + count));
                var action = new Button(() => Build(id)) { text = Strings.Get("hud_build") };
                action.SetEnabled(canManage && _facilities.CanBuild(_selectedHub, id)); row.Add(action); _buildings.Add(row);
            }
        }

        /// <summary>resource를 현재 선택 자원으로 저장하고 버튼 선택 표시를 갱신한다.</summary>
        private void SelectResource(string resource) { _selectedResource = resource; Refresh(); }
        /// <summary>hubId가 등록 거점이면 로컬 선택을 바꾸고 정보 패널을 연다.</summary>
        private void OpenHub(string hubId)
        {
            bool known = false;
            foreach (string id in _hubs.HubIds) if (id == hubId) known = true;
            if (!known) return;
            _selectedHub = hubId; Refresh();
        }
        /// <summary>입력 없이 선택 거점을 해제하고 측면 패널을 닫는다.</summary>
        private void ClosePanel() { _selectedHub = null; Refresh(); }
        /// <summary>slot과 선택 자원으로 배치 또는 회수를 시스템에 요청하고 결과 표시를 갱신한다.</summary>
        private void ChangeSlot(int slot)
        {
            if (!CanManage()) return;
            bool accepted = string.IsNullOrEmpty(_habitat.SlotContent(_selectedHub, slot))
                ? _habitat.TryPlace(_selectedHub, slot, _selectedResource) : _habitat.TryRemove(_selectedHub, slot);
            Refresh(); if (!accepted) _message.text = Strings.Get("hud_unavailable");
        }
        /// <summary>facility를 시설 시스템에 건설 요청하고 결과 표시만 갱신한다.</summary>
        private void Build(string facility)
        {
            if (!CanManage()) return;
            bool accepted = _facilities.TryBuild(_selectedHub, facility);
            Refresh(); if (!accepted) _message.text = Strings.Get("hud_insufficient");
        }
        /// <summary>정차와 런 상태를 확인해 열차에 출발을 요청하고 표시를 갱신한다.</summary>
        private void Depart()
        {
            if (_hubs.IsRunLost || !_track.IsHeld || _waypoints.IsEncounterActive || _track.CurrentNode?.Kind != TrackNodeKind.Hub) return;
            _track.Depart(); Refresh();
        }
        /// <summary>활성 전투 여부를 확인해 라운드 명령 서비스에 다음 라운드를 요청한다.</summary>
        private void AdvanceRound()
        {
            if (_hubs.IsRunLost || !_waypoints.IsEncounterActive || _encounter == null || !_encounter.IsCombat || _rounds == null || !_rounds.CanAdvance) return;
            _rounds.AdvanceCombatRound(); Refresh();
        }
        /// <summary>node 도착 알림을 받아 HUD 표시를 최신 상태로 변경한다.</summary>
        private void OnArrived(ITrackNode node) { Refresh(); }
        /// <summary>lap 순환 알림을 받아 HUD 표시를 최신 상태로 변경한다.</summary>
        private void OnLap(int lap) { Refresh(); }
        /// <summary>id와 amount 자원 알림을 받아 HUD 표시를 최신 상태로 변경한다.</summary>
        private void OnResource(string id, int amount) { Refresh(); }
        /// <summary>hub 상태 알림을 받아 HUD 표시를 최신 상태로 변경한다.</summary>
        private void OnHub(string hub) { Refresh(); }
        /// <summary>hub와 id 개체 알림을 받아 HUD 표시를 최신 상태로 변경한다.</summary>
        private void OnPair(string hub, string id) { Refresh(); }
        /// <summary>hub와 value 번영 알림을 받아 HUD 표시를 최신 상태로 변경한다.</summary>
        private void OnProsperity(string hub, float value) { Refresh(); }
        /// <summary>node와 outcome 조우 종료 알림을 받아 HUD 표시를 최신 상태로 변경한다.</summary>
        private void OnEncounterEnded(ITrackNode node, EncounterOutcome outcome) { Refresh(); }
        /// <summary>내부 화면 open 전환 알림을 받아 거점 패널 표시를 갱신한다.</summary>
        private void OnInteriorChanged(bool open) { Refresh(); }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_track == null) return;
            _track.Arrived -= OnArrived; _track.LapCompleted -= OnLap;
            _inventory.Changed -= OnResource; _habitat.EventFlagRaised -= OnHub;
            _npcs.NpcJoined -= OnPair; _hubs.ProsperityChanged -= OnProsperity;
            _hubs.HubRuined -= OnHub; _hubs.RunLost -= Refresh;
            _waypoints.EncounterStarted -= OnArrived; _waypoints.EncounterEnded -= OnEncounterEnded;
            _facilities.FacilityBuilt -= OnPair; _clicks.HubClicked -= OpenHub;
            if (_viewMode != null) _viewMode.InteriorChanged -= OnInteriorChanged;
            Destroy(_settings);
        }
    }

}
