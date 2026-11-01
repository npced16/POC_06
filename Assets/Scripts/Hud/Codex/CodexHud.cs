using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;
using IronRing.Habitats.Demo;
using IronRing.Hud.Hotbar;
using IronRing.Localization;
using IronRing.Presentation.Interior;
using IronRing.Presentation.Items;

namespace IronRing.Hud.Codex
{
    /// <summary>
    /// 스토리 021. 서식지 레시피 도감 패널(UI Toolkit). 상단 바의 "도감" 버튼(KoreanHud → Toggle) 또는 C 키로 열고 닫는다(Esc·닫기 버튼도 닫는다).
    /// 카탈로그의 모든 종류를 격자로 보인다: 미해금은 "???"와 어두운 실루엣, 해금은 이름·종류·칸 수·타일 배열 그림·번영 보너스·찾아오는 NPC 직업,
    /// 현재 거점에서 완성된 종류에는 "완성" 표시. 상태: 닫힘(display none, 클릭 판정 없음) / 열림.
    /// IHabitatCodex.RecipeUnlocked마다 핫바 토스트 자리에 "새 레시피: 이름"(발견이면 "발견: 이름")을 한 프레임 늦게, 겹치면 차례로 띄운다
    /// (같은 프레임의 "완성!" 토스트가 덮어쓰지 않게). 게임 상태는 읽기만 한다.
    /// </summary>
    public sealed class CodexHud : MonoBehaviour
    {
        /// <summary>NPC 대화(125) 위에 그린다.</summary>
        private const int SORTING_ORDER = 130;

        private static CodexHud _instance;
        private bool _bound;

        /// <summary>현재 패널(데모용). 없으면 null.</summary>
        public static CodexHud Instance => _instance;

        private IHabitatCodex _codex;
        private IHabitatClusters _clusters;
        private IViewMode _view;
        private ITrackRuntime _track;
        private IHubStateSystem _hubs;
        private HabitatCatalog _catalog;
        private ItemSpriteSet _sprites;

        private VisualElement _root;
        private VisualElement _panel;
        private Label _title;
        private ScrollView _grid;
        private bool _open;
        private string _lastHub;
        private readonly Queue<string> _toasts = new Queue<string>();
        private float _nextToastAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Ready -= Create;
            _instance = null;
        }

        /// <summary>MainRoot의 GameServices.Reset()(SubsystemRegistration) 뒤에 Ready를 구독한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>스토리 018: 씬(MainUI)에 놓인 문서는 Initialize 전까지 꺼 두어 UXML 예시가 Play 화면에 비치지 않게 한다.</summary>
        void Awake() { if (!_bound && TryGetComponent(out UIDocument document)) document.enabled = false; }

        /// <summary>도감 서비스가 있는 씬에서 한 번 만든다. 씬(MainUI)에 놓인 컴포넌트가 있으면 그것에 바인딩한다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IHabitatCodex>() == null || HabitatsInstaller.Catalog == null) return;
            CodexHud placed = FindAnyObjectByType<CodexHud>(FindObjectsInactive.Include);
            if (placed != null && !(placed.enabled && placed.gameObject.activeInHierarchy)) return;
            _instance = placed != null ? placed : new GameObject(nameof(CodexHud)).AddComponent<CodexHud>();
            _instance.Initialize();
        }

        /// <summary>도감을 열거나 닫는다(상단 바 버튼). 패널이 없으면 무시한다.</summary>
        public static void Toggle() { if (_instance != null) _instance.SetOpen(!_instance._open); }

        /// <summary>도감을 연다(데모용).</summary>
        public static void Open() { if (_instance != null) _instance.SetOpen(true); }

        private void Initialize()
        {
            _codex = GameServices.Get<IHabitatCodex>();
            _clusters = GameServices.Get<IHabitatClusters>();
            _view = GameServices.Get<IViewMode>();
            _track = GameServices.Get<ITrackRuntime>();
            _hubs = GameServices.Get<IHubStateSystem>();
            _catalog = HabitatsInstaller.Catalog;
            _sprites = ItemSpriteSet.Load();
            _bound = true;
            UIDocument document = HudDocument.Bind(gameObject, () => KoreanUi.DefaultPanel, SORTING_ORDER, "Hud/Codex");
            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            foreach (TemplateContainer container in _root.Query<TemplateContainer>().ToList()) container.pickingMode = PickingMode.Ignore;
            StyleSheet style = UnityEngine.Resources.Load<StyleSheet>("Hud/CodexStyle");
            if (style == null) UnityEngine.Debug.LogError("CodexHud: Resources/Hud/CodexStyle.uss를 불러오지 못했습니다.");
            else if (!_root.styleSheets.Contains(style)) _root.styleSheets.Add(style);
            KoreanUi.Apply(_root);

            _root.Q("codex-root").pickingMode = PickingMode.Ignore;
            _panel = _root.Q("codex-panel");
            _title = _root.Q<Label>("codex-title");
            _grid = _root.Q<ScrollView>("codex-grid");
            _root.Q<Label>("codex-hint").text = Text("codex_hint");
            Button close = _root.Q<Button>("codex-close");
            close.text = Text("codex_close");
            close.clicked += () => SetOpen(false);

            PlacementGhost.UiHitTests.Add(IsOverPanel);
            _codex.RecipeUnlocked += OnRecipeUnlocked;
            if (_clusters != null) { _clusters.HabitatCompleted += OnClustersChanged; _clusters.HabitatBroken += OnClustersChanged; }
            if (_view != null) _view.InteriorChanged += OnInteriorChanged;
            SetOpen(false);
            if (CodexDemoDriver.Stage != null) CodexDemoDriver.StageReady += Open;
        }

        // ----- 상태 -----

        private void SetOpen(bool open)
        {
            _open = open;
            if (_panel == null) return;
            _panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) Rebuild();
        }

        private void OnRecipeUnlocked(string typeId, string source)
        {
            string key = source == HabitatCodex.SOURCE_DISCOVER ? "codex_toast_discover" : "codex_toast_new";
            _toasts.Enqueue(string.Format(Text(key), HabitatName(typeId)));
            if (_open) Rebuild();
        }

        private void OnClustersChanged(string hubId, string typeId) { if (_open) Rebuild(); }
        private void OnInteriorChanged(bool open) { if (_open) Rebuild(); }

        void Update()
        {
            if (_panel == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.cKey.wasPressedThisFrame) SetOpen(!_open);
                else if (_open && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
            }
            // 큐에 넣은 다음 프레임 이후에 띄운다(같은 프레임에 핫바의 "완성!" 토스트가 나중에 덮어쓰지 않게).
            float now = Time.realtimeSinceStartup;
            if (_toasts.Count > 0 && now >= _nextToastAt)
            {
                HotbarHud hotbar = HotbarHud.Instance;
                string text = _toasts.Dequeue();
                if (hotbar != null)
                {
                    hotbar.ShowToast(text);
                    _nextToastAt = now + hotbar.ToastSeconds;
                }
            }
            if (_open && CurrentHub() != _lastHub) Rebuild();
        }

        private bool IsOverPanel(Vector2 screen)
        {
            if (!_open || _panel?.panel == null) return false;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(_panel.panel, new Vector2(screen.x, Screen.height - screen.y));
            return _panel.worldBound.Contains(point);
        }

        /// <summary>완성 표시 기준 거점: 열린 내부 화면, 아니면 정박한 거점, 아니면 마지막에 본 거점(처음엔 첫 거점).</summary>
        private string CurrentHub()
        {
            if (_view != null && _view.InteriorOpen && _view.InteriorHubId != null) return _view.InteriorHubId;
            ITrackNode node = _track?.CurrentNode;
            if (node != null && node.Kind == TrackNodeKind.Hub) return node.HubId;
            if (_lastHub != null) return _lastHub;
            return _hubs != null && _hubs.HubIds.Count > 0 ? _hubs.HubIds[0] : null;
        }

        // ----- 그리기 -----

        private void Rebuild()
        {
            string hub = CurrentHub();
            _lastHub = hub;
            var completed = new HashSet<string>();
            if (_clusters != null && hub != null)
                foreach (HabitatCluster cluster in _clusters.CompletedAt(hub)) completed.Add(cluster.TypeId);
            _title.text = string.Format(Text("codex_title"), _codex.UnlockedTypeIds.Count, _catalog.Types.Count);
            _grid.Clear();
            foreach (HabitatType type in _catalog.Types)
                _grid.Add(_codex.IsUnlocked(type.Id) ? UnlockedEntry(type, completed.Contains(type.Id)) : LockedEntry());
        }

        private VisualElement LockedEntry()
        {
            VisualElement entry = Element("codex-entry", "codex-locked");
            entry.Add(Element("codex-silhouette"));
            entry.Add(TextLabel(Text("codex_locked"), "codex-entry-name"));
            return entry;
        }

        private VisualElement UnlockedEntry(HabitatType type, bool complete)
        {
            VisualElement entry = Element("codex-entry");
            entry.EnableInClassList("codex-complete", complete);
            VisualElement header = Element("codex-entry-header");
            header.Add(TextLabel(HabitatName(type.Id), "codex-entry-name"));
            if (complete) header.Add(TextLabel(Text("codex_complete"), "codex-badge"));
            entry.Add(header);
            entry.Add(TextLabel(KindName(type.Kind) + "  ·  " + string.Format(Text("codex_cells"), CellCount(type)), "codex-entry-meta"));
            entry.Add(Diagram(type));
            VisualElement footer = Element("codex-entry-footer");
            footer.Add(TextLabel(string.Format(Text("codex_bonus"), type.ProsperityBonus.ToString("0")), "codex-bonus"));
            footer.Add(TextLabel(string.Format(Text("codex_npc"), Lookup("npc_" + type.NpcTypeId) ?? type.NpcTypeId), "codex-npc"));
            entry.Add(footer);
            return entry;
        }

        /// <summary>같은 타일: 아이콘×개수. 혼합: 타일별 아이콘×최소 수 + "+k 아무거나". 패턴: 템플릿 격자(빈 칸은 '.').</summary>
        private VisualElement Diagram(HabitatType type)
        {
            VisualElement diagram = Element("codex-diagram");
            if (type.Kind == HabitatKind.Pattern)
            {
                diagram.AddToClassList("codex-diagram-pattern");
                foreach (IReadOnlyList<string> row in type.Pattern)
                {
                    VisualElement line = Element("codex-pattern-row");
                    foreach (string tile in row)
                        line.Add(tile == HabitatType.DontCare ? Element("codex-icon", "codex-icon-empty") : Icon(tile));
                    diagram.Add(line);
                }
                return diagram;
            }
            if (type.Kind == HabitatKind.Set)
            {
                int fixedCount = 0;
                foreach (HabitatTileRequirement tile in type.SetTiles)
                {
                    for (int i = 0; i < tile.MinCount; i++) diagram.Add(Icon(tile.TileItemId));
                    fixedCount += tile.MinCount;
                }
                int rest = type.RequiredCount - fixedCount;
                if (rest > 0) diagram.Add(TextLabel(string.Format(Text("codex_any"), rest), "codex-any"));
                return diagram;
            }
            for (int i = 0; i < type.RequiredCount; i++) diagram.Add(Icon(type.TileItemId));
            return diagram;
        }

        private VisualElement Icon(string tileItemId)
        {
            VisualElement icon = Element("codex-icon");
            Sprite sprite = _sprites != null ? _sprites.Get(tileItemId) : null;
            if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
            else
            {
                string name = Lookup("item_" + tileItemId) ?? tileItemId;
                icon.Add(TextLabel(name.Substring(0, 1), "codex-icon-letter"));
            }
            icon.tooltip = Lookup("item_" + tileItemId) ?? tileItemId;
            return icon;
        }

        private static int CellCount(HabitatType type)
        {
            if (type.Kind != HabitatKind.Pattern) return type.RequiredCount;
            int count = 0;
            foreach (IReadOnlyList<string> row in type.Pattern)
                foreach (string tile in row) if (tile != HabitatType.DontCare) count++;
            return count;
        }

        private static string KindName(HabitatKind kind) => Text(kind == HabitatKind.Pattern ? "codex_kind_pattern" : kind == HabitatKind.Set ? "codex_kind_set" : "codex_kind_cluster");

        private static string HabitatName(string typeId) => Lookup("habitat_type_" + typeId) ?? typeId;

        private static string Text(string key) => Lookup(key) ?? key;

        /// <summary>문자열 표의 값. 키가 없으면 null.</summary>
        private static string Lookup(string key)
        {
            try { return Strings.Get(key); }
            catch (KeyNotFoundException) { return null; }
        }

        private static VisualElement Element(params string[] classes)
        {
            var element = new VisualElement();
            foreach (string c in classes) element.AddToClassList(c);
            return element;
        }

        private static Label TextLabel(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            CodexDemoDriver.StageReady -= Open;
            PlacementGhost.UiHitTests.Remove(IsOverPanel);
            if (_codex != null) _codex.RecipeUnlocked -= OnRecipeUnlocked;
            if (_clusters != null) { _clusters.HabitatCompleted -= OnClustersChanged; _clusters.HabitatBroken -= OnClustersChanged; }
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_instance == this) _instance = null;
        }
    }
}
