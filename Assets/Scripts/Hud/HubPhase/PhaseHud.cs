using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hub;
using IronRing.Localization;
using IronRing.Presentation.HubPhase;

using DevPhase = IronRing.Contracts.HubPhase;

namespace IronRing.Hud.HubPhase
{
    /// <summary>
    /// Story 014 Korean HUD: the current hub's phase in both views (banner + ring-map labels), the first-settlement deadline,
    /// the loss reason, and the 건설 menu opened by clicking a FacilityPlot interior cell (builds via IInteriorFacilityPlots).
    /// </summary>
    public sealed class PhaseHud : MonoBehaviour
    {
        private static PhaseHud _instance;
        private bool _bound;
        private HubPhaseViewSettings _settings;
        private IHubStateSystem _hubs;
        private IHubDevelopment _development;
        private ITrackRuntime _track;
        private IViewMode _view;
        private IMapAnchors _map;
        private IInteriorFacilityPlots _plots;
        private IInteriorCellClickSource _clicks;
        private VisualElement _root;
        private VisualElement _mapLabels;
        private VisualElement _menu;
        private VisualElement _options;
        private Label _hubLabel;
        private Label _deadline;
        private Label _menuTitle;
        private Label _menuMessage;
        private Label _loss;
        private string _lastHub;
        private string _menuHub;
        private int _menuCell = -1;
        private string _bannerKey;
        private VisualTreeAsset _labelTemplate;
        private VisualTreeAsset _optionTemplate;
        private readonly Dictionary<string, Label> _labels = new Dictionary<string, Label>();

        /// <summary>Clears statics for a new play session (domain reload disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        /// <summary>Waits for GameServices.Ready after the scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>스토리 018: 씬(MainUI)에 놓인 문서는 서비스 준비(Initialize) 전까지 꺼 두어 UXML 예시 문구가 Play 화면에 비치지 않게 한다.</summary>
        void Awake() { if (!_bound && TryGetComponent(out UIDocument document)) document.enabled = false; }

        /// <summary>Creates the HUD once when hub state, development and the track are registered.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            if (GameServices.Get<IHubStateSystem>() == null || GameServices.Get<IHubDevelopment>() == null
                || GameServices.Get<ITrackRuntime>() == null) return;
            PhaseHud placed = FindAnyObjectByType<PhaseHud>(FindObjectsInactive.Include);
            // 스토리 018: 씬(MainUI)에 놓인 HUD에 바인딩한다. 제작자가 꺼 둔 화면이면 대비책도 만들지 않는다.
            if (placed != null && !(placed.enabled && placed.gameObject.activeInHierarchy)) return;
            _instance = placed != null ? placed : new GameObject(nameof(PhaseHud)).AddComponent<PhaseHud>();
            _instance.Initialize();
        }

        /// <summary>Reads services, builds the Korean document and subscribes to cell clicks and phase changes.</summary>
        private void Initialize()
        {
            _settings = HubPhaseViewSettings.Load();
            _hubs = GameServices.Get<IHubStateSystem>();
            _development = GameServices.Get<IHubDevelopment>();
            _track = GameServices.Get<ITrackRuntime>();
            _view = GameServices.Get<IViewMode>();
            _map = GameServices.Get<IMapAnchors>();
            _plots = GameServices.Get<IInteriorFacilityPlots>();
            _clicks = GameServices.Get<IInteriorCellClickSource>();
            _bound = true;
            // 스토리 018: 씬에 놓인 UIDocument가 있으면 비어 있는 값만 채운다(Sort Order·PanelSettings·UXML은 프리팹 값 우선).
            UIDocument document = HudDocument.Bind(gameObject, () => Instantiate(UnityEngine.Resources.Load<PanelSettings>("KoreanPanelSettings")), _settings.HudSortingOrder, "Hud/PhaseHud");
            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            foreach (TemplateContainer container in _root.Query<TemplateContainer>().ToList()) container.pickingMode = PickingMode.Ignore;
            StyleSheet style = UnityEngine.Resources.Load<StyleSheet>("Hud/PhaseHudStyle");
            if (style == null) UnityEngine.Debug.LogError("PhaseHud: Resources/Hud/PhaseHudStyle.uss를 불러오지 못했습니다.");
            else if (!_root.styleSheets.Contains(style)) _root.styleSheets.Add(style);
            KoreanUi.Apply(_root);
            _mapLabels = _root.Q("phase-map-labels");
            _hubLabel = _root.Q<Label>("phase-hub");
            _deadline = _root.Q<Label>("phase-deadline");
            _menu = _root.Q("phase-build-menu");
            _options = _root.Q("phase-build-options");
            _menuTitle = _root.Q<Label>("phase-build-title");
            _menuMessage = _root.Q<Label>("phase-build-message");
            _loss = _root.Q<Label>("phase-loss-reason");
            Button close = _root.Q<Button>("phase-build-close");
            close.text = Strings.Get("hud_close");
            close.clicked += CloseMenu;
            _labelTemplate = UnityEngine.Resources.Load<VisualTreeAsset>("Hud/PhaseMapLabel");
            _optionTemplate = UnityEngine.Resources.Load<VisualTreeAsset>("Hud/PhaseBuildOption");
            if (_labelTemplate == null || _optionTemplate == null) UnityEngine.Debug.LogError("PhaseHud: Resources/Hud/PhaseMapLabel.uxml 또는 PhaseBuildOption.uxml이 없습니다.");
            foreach (string hubId in _hubs.HubIds)
            {
                Label label = _labelTemplate?.Instantiate().Q<Label>("phase-map-label");
                if (label == null) continue;
                _labels.Add(hubId, label);
                _mapLabels.Add(label);
            }
            _lastHub = _hubs.HubIds.Count > 0 ? _hubs.HubIds[0] : null;
            // Story 015: 시설은 핫바(7~9)로 놓는다. 칸 클릭 건설 메뉴는 hub_phase_view.json의 _buildMenuOnCellClick이 true일 때만 쓴다.
            if (!_settings.BuildMenuOnCellClick) { }
            else if (_clicks != null) _clicks.CellClicked += OnCellClicked;
            else UnityEngine.Debug.Log("PhaseHud: IInteriorCellClickSource 미등록 - 부지 건설 메뉴를 쓸 수 없습니다.");
            if (_view != null) _view.InteriorChanged += OnInteriorChanged;
            _development.PhaseChanged += OnPhaseChanged;
            _hubs.RunLost += OnRunLost;
            Refresh(true);
        }

        private void OnPhaseChanged(string hubId, DevPhase phase) => Refresh(true);
        private void OnInteriorChanged(bool open) { if (!open) CloseMenu(); Refresh(true); }
        private void OnRunLost() { CloseMenu(); Refresh(true); }

        /// <summary>Korean phase name for phase.</summary>
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

        /// <summary>USS class for phase colour.</summary>
        private static string PhaseClass(DevPhase phase) => "phase-" + phase.ToString().ToLowerInvariant();

        /// <summary>Korean hub name from the shared table (hub-1 -> hub_1).</summary>
        private static string HubName(string hubId) => Strings.Get(hubId.Replace('-', '_'));

        /// <summary>Hub shown by the banner: open interior, else the docked hub, else the last hub shown.</summary>
        private string CurrentHub()
        {
            if (_view != null && _view.InteriorOpen && _view.InteriorHubId != null) return _lastHub = _view.InteriorHubId;
            ITrackNode node = _track.CurrentNode;
            if (node != null && node.Kind == TrackNodeKind.Hub) return _lastHub = node.HubId;
            return _lastHub;
        }

        /// <summary>Updates banner, deadline, ring labels and loss text; skips string work when nothing changed unless forced.</summary>
        private void Refresh(bool force)
        {
            string hub = CurrentHub();
            if (hub == null) return;
            DevPhase phase = _development.GetPhase(hub);
            int prosperity = Mathf.RoundToInt(_hubs.GetProsperity(hub));
            bool anySettled = false;
            foreach (string id in _hubs.HubIds) anySettled |= _development.HasEverSettled(id);
            int laps = _development.LapsUntilDeadline;
            string key = hub + "|" + phase + "|" + prosperity + "|" + laps + "|" + anySettled + "|" + _hubs.IsRunLost;
            if (!force && key == _bannerKey) return;
            _bannerKey = key;
            _hubLabel.text = HubName(hub) + "  ·  " + PhaseText(phase) + "  ·  " + Strings.Get("phase_prosperity") + " " + prosperity;
            foreach (DevPhase each in new[] { DevPhase.Ruined, DevPhase.Recovering, DevPhase.Settled, DevPhase.Collapsed })
                _hubLabel.EnableInClassList(PhaseClass(each), each == phase);
            bool showDeadline = !anySettled && laps != int.MaxValue && !_hubs.IsRunLost;
            _deadline.style.display = showDeadline ? DisplayStyle.Flex : DisplayStyle.None;
            if (showDeadline) _deadline.text = Strings.Get("phase_deadline") + ": " + laps + Strings.Get("phase_laps_left");
            foreach (KeyValuePair<string, Label> pair in _labels)
            {
                DevPhase hubPhase = _development.GetPhase(pair.Key);
                pair.Value.text = PhaseText(hubPhase);
                foreach (DevPhase each in new[] { DevPhase.Ruined, DevPhase.Recovering, DevPhase.Settled, DevPhase.Collapsed })
                    pair.Value.EnableInClassList(PhaseClass(each), each == hubPhase);
            }
            // The reason-specific defeat title is shown by Story 011's overlay through IRunLossInfo; this line is only a
            // fallback when that service is missing, so the two never repeat each other.
            IRunLossInfo lossInfo = GameServices.Get<IRunLossInfo>();
            bool showLoss = _hubs.IsRunLost && lossInfo == null;
            _loss.style.display = showLoss ? DisplayStyle.Flex : DisplayStyle.None;
            if (showLoss) _loss.text = Strings.Get("phase_loss_collapse");
        }

        /// <summary>Opens the 건설 menu when a free FacilityPlot cell of the open interior is clicked.</summary>
        private void OnCellClicked(string hubId, int cellIndex)
        {
            if (_hubs.IsRunLost || _plots == null || !_plots.IsPlot(hubId, cellIndex)) return;
            _menuHub = hubId; _menuCell = cellIndex;
            _menuTitle.text = Strings.Get("phase_build_title") + " · " + HubName(hubId);
            _menuMessage.text = "";
            _options.Clear();
            foreach (string facilityId in _settings.BuildFacilityIds)
            {
                string id = facilityId;
                Button button = _optionTemplate?.Instantiate().Q<Button>("phase-build-option");
                if (button == null) continue;
                button.text = Strings.Get("hud_build") + ": " + Strings.Get("facility_" + id);
                button.clicked += () => Build(id);
                button.SetEnabled(_plots.CanBuildOnCell(hubId, cellIndex, id));
                _options.Add(button);
            }
            _menu.style.display = DisplayStyle.Flex;
        }

        /// <summary>Builds facilityId on the menu's cell; keeps the menu open with a Korean reason when refused.</summary>
        private void Build(string facilityId)
        {
            if (_menuHub == null || _menuCell < 0) return;
            if (_plots.TryBuildOnCell(_menuHub, _menuCell, facilityId)) CloseMenu();
            else _menuMessage.text = Strings.Get("phase_build_refused");
        }

        private void CloseMenu()
        {
            _menuHub = null; _menuCell = -1;
            if (_menu != null) _menu.style.display = DisplayStyle.None;
        }

        void LateUpdate()
        {
            if (_root == null) return;
            Refresh(false);
            bool interior = _view != null && _view.InteriorOpen;
            _mapLabels.style.display = interior ? DisplayStyle.None : DisplayStyle.Flex;
            if (interior || _map == null || _map.MapCamera == null || _root.panel == null) return;
            foreach (KeyValuePair<string, Label> pair in _labels)
            {
                Transform anchor = _map.HubAnchor(pair.Key);
                if (anchor == null) { pair.Value.style.display = DisplayStyle.None; continue; }
                Vector2 point = LabelPoint(anchor, pair.Value);
                pair.Value.style.left = point.x;
                pair.Value.style.top = point.y;
            }
        }

        /// <summary>
        /// Panel centre for a hub's phase label: stacked next to the hub's Korean name label (TextMesh in the HubBuilding
        /// instance) on the side away from the building, with a data gap, so it never covers the name or the building.
        /// Falls back to the data offset below the anchor when the prefab has no name label.
        /// </summary>
        private Vector2 LabelPoint(Transform anchor, Label label)
        {
            IPanel panel = _root.panel;
            Camera camera = _map.MapCamera;
            TextMesh name = anchor.GetComponentInChildren<TextMesh>();
            Renderer nameRenderer = name != null ? name.GetComponent<Renderer>() : null;
            if (nameRenderer == null)
                return RuntimePanelUtils.CameraTransformWorldToPanel(panel, anchor.position + Vector3.up * _settings.MapLabelOffsetY, camera);
            Bounds bounds = nameRenderer.bounds;
            SpriteRenderer building = anchor.GetComponentInChildren<SpriteRenderer>();
            float buildingY = building != null ? building.bounds.center.y : anchor.position.y;
            bool nameBelowBuilding = bounds.center.y < buildingY;
            float half = float.IsNaN(label.resolvedStyle.height) || label.resolvedStyle.height <= 0 ? _settings.MapLabelGapPixels * 3 : label.resolvedStyle.height * 0.5f;
            Vector2 centre = RuntimePanelUtils.CameraTransformWorldToPanel(panel, bounds.center, camera);
            Vector2 bottom = RuntimePanelUtils.CameraTransformWorldToPanel(panel, new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), camera);
            Vector2 top = RuntimePanelUtils.CameraTransformWorldToPanel(panel, new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), camera);
            float y = nameBelowBuilding ? bottom.y + _settings.MapLabelGapPixels + half : top.y - _settings.MapLabelGapPixels - half;
            return new Vector2(centre.x, y);
        }

        void OnDestroy()
        {
            if (_clicks != null) _clicks.CellClicked -= OnCellClicked;
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_development != null) _development.PhaseChanged -= OnPhaseChanged;
            if (_hubs != null) _hubs.RunLost -= OnRunLost;
            if (_instance == this) _instance = null;
        }
    }
}
