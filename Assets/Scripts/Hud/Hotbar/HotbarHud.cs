using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

using IronRing.Hud.Ui;
using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Items;
using IronRing.Localization;
using IronRing.Presentation.Interior;

namespace IronRing.Hud.Hotbar
{
    /// <summary>
    /// 스토리 015. 거점 내부 화면의 핫바(하단 9칸, 키 1~9)와 팔레트(카탈로그 전체: 아이템·타일·시설). 수량은 표시하지 않는다(무제한).
    /// 슬롯 선택은 PlacementGhost.Select로 고스트 배치를 시작하고, 슬롯·팔레트 항목을 끌면(UI Toolkit PointerDown/Move/Up) 같은 고스트로 드래그 배치한다.
    /// 팔레트 항목을 누른 뒤 슬롯을 누르면 그 슬롯의 배정이 바뀐다. 기본 배정은 Resources/Hotbar/Hotbar.json.
    /// 서식지 완성(IHabitatClusters.HabitatCompleted, 스토리 016)이면 "이름 완성!" 문구를 잠깐 띄운다. 게임 상태는 바꾸지 않고 명령만 보낸다.
    /// </summary>
    public sealed class HotbarHud : MonoBehaviour, IHotbarDemoTarget
    {
        public const int SLOT_COUNT = 9;
        private static HotbarHud _instance;
        private bool _bound;
        /// <summary>현재 핫바(데모 드라이버용). 없으면 null.</summary>
        public static HotbarHud Instance => _instance;

        private IViewMode _view;
        private IHabitatClusters _clusters;
        private PlacementGhost _ghost;
        private HotbarData _data;
        private ItemCatalog _items;

        private VisualElement _root;
        private VisualElement _hotbarRoot;
        private VisualElement _palette;
        private VisualElement _paletteList;
        private VisualElement _drag;
        private VisualElement _dragIcon;
        private Label _toast;
        private Label _notice;
        private readonly VisualElement[] _slotViews = new VisualElement[SLOT_COUNT];
        private readonly PlacementEntry[] _slots = new PlacementEntry[SLOT_COUNT];
        private readonly List<(PlacementEntry entry, VisualElement view)> _paletteViews = new List<(PlacementEntry, VisualElement)>();

        private int _selectedSlot = -1;
        private PlacementEntry _pendingAssign;
        private float _toastUntil;
        /// <summary>스토리 021: 도감 토스트는 내부 화면이 닫혀 있어도 보인다.</summary>
        private bool _toastAnyView;
        private bool _uguiInteriorToast;
        private float _noticeUntil;
        private bool _paletteOpen;

        // 포인터 누름 상태(슬롯 또는 팔레트 항목). 이동이 임계값을 넘으면 드래그로 바뀐다.
        private VisualElement _pressElement;
        private PlacementEntry _pressEntry;
        private Vector2 _pressPosition;
        private int _pressPointer = -1;
        private bool _pressDragging;
        private Action _pressClick;
        private Vector2? _dragScreenOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        /// <summary>MainRoot의 GameServices.Reset()(SubsystemRegistration) 뒤에 Ready를 구독한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>스토리 018: 씬(MainUI)에 놓인 문서는 서비스 준비(Initialize) 전까지 꺼 두어 UXML 예시 문구가 Play 화면에 비치지 않게 한다.</summary>
        void Awake() { if (!_bound && TryGetComponent(out UIDocument document)) document.enabled = false; }

        /// <summary>내부 화면 서비스와 아이템 카탈로그가 있는 씬에서만 한 번 만든다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IViewMode>() == null || ItemsInstaller.Catalog == null) return;
            if (UiScreens.Instance != null) return; // 스토리 022: UGUI HotbarScreen이 대신한다.
            PlacementGhost ghost = PlacementGhost.Ensure();
            if (ghost == null) return;
            HotbarHud placed = FindAnyObjectByType<HotbarHud>(FindObjectsInactive.Include);
            // 스토리 018: 씬(MainUI)에 놓인 HUD에 바인딩한다. 제작자가 꺼 둔 화면이면 대비책도 만들지 않는다.
            if (placed != null && !(placed.enabled && placed.gameObject.activeInHierarchy)) return;
            _instance = placed != null ? placed : new GameObject(nameof(HotbarHud)).AddComponent<HotbarHud>();
            _instance.Initialize(ghost);
        }

        private void Initialize(PlacementGhost ghost)
        {
            _ghost = ghost;
            _view = GameServices.Get<IViewMode>();
            _clusters = GameServices.Get<IHabitatClusters>();
            _items = ItemsInstaller.Catalog;
            _data = HotbarData.Load();

            _bound = true;
            // 스토리 018: 씬에 놓인 UIDocument가 있으면 비어 있는 값만 채운다(Sort Order·PanelSettings·UXML은 프리팹 값 우선).
            UIDocument document = HudDocument.Bind(gameObject, () => KoreanUi.DefaultPanel, _data.SortingOrder, "Hud/Hotbar");
            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            foreach (TemplateContainer container in _root.Query<TemplateContainer>().ToList()) container.pickingMode = PickingMode.Ignore;
            // 다른 HUD와 같은 이유로 USS를 UXML과 다른 이름으로 두고 직접 붙인다.
            StyleSheet style = UnityEngine.Resources.Load<StyleSheet>("Hud/HotbarStyle");
            if (style == null) UnityEngine.Debug.LogError("HotbarHud: Resources/Hud/HotbarStyle.uss를 불러오지 못했습니다.");
            else if (!_root.styleSheets.Contains(style)) _root.styleSheets.Add(style);
            KoreanUi.Apply(_root);

            _hotbarRoot = _root.Q("hotbar-root");
            _palette = _root.Q("hotbar-palette");
            _paletteList = _root.Q("hotbar-palette-list");
            _drag = _root.Q("hotbar-drag");
            _dragIcon = _root.Q("hotbar-drag-icon");
            _toast = _root.Q<Label>("hotbar-toast");
            _notice = _root.Q<Label>("hotbar-notice");
            // 토스트는 UXML 미리보기에서 보이도록 USS 숨김을 뺐으므로 여기서 바로 숨긴다(Update가 이어서 관리).
            _toast.style.display = DisplayStyle.None;
            // 스토리 021: 토스트를 핫바 루트(내부 화면에서만 보임) 밖 문서 루트로 옮겨 지도 화면에서도 같은 자리에 띄울 수 있게 한다.
            _root.Add(_toast);
            foreach (string name in new[] { "hotbar-root", "hotbar-dock", "hotbar-toast", "hotbar-drag", "hotbar-drag-icon", "hotbar-notice", "hotbar-hint" })
                _root.Q(name).pickingMode = PickingMode.Ignore;
            _root.Q<Label>("hotbar-hint").text = Strings.Get("hotbar_hint");
            _root.Q<Label>("hotbar-palette-title").text = Strings.Get("hotbar_palette_title");
            _root.Q<Label>("hotbar-palette-hint").text = Strings.Get("hotbar_palette_hint");
            Button toggle = _root.Q<Button>("hotbar-palette-toggle");
            toggle.text = Strings.Get("hotbar_palette");
            toggle.clicked += () => SetPaletteOpen(!_paletteOpen);
            Button close = _root.Q<Button>("hotbar-palette-close");
            close.text = Strings.Get("inv_close");
            close.clicked += () => SetPaletteOpen(false);

            AssignDefaults();
            BuildSlots();
            BuildPalette();

            PlacementGhost.UiHitTests.Add(IsOverHud);
            _ghost.SelectionChanged += OnSelectionChanged;
            _ghost.Notice += OnNotice;
            _view.InteriorChanged += OnInteriorChanged;
            if (_clusters != null) _clusters.HabitatCompleted += OnHabitatCompleted;
            Refresh();
        }

        // ----- 데이터 배정 -----

        /// <summary>Hotbar.json의 배정을 쓰되 카탈로그에 없는 id는(설정 시) 아직 쓰지 않은 카탈로그 아이템으로 채운다.</summary>
        private void AssignDefaults()
        {
            HotbarData.SlotData[] data = _data.Slots;
            var used = new HashSet<PlacementEntry>();
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                PlacementEntry entry = i < data.Length && data[i] != null ? data[i].ToEntry() : default;
                _slots[i] = Exists(entry) ? entry : default;
                if (_slots[i].IsValid) used.Add(_slots[i]);
            }
            if (!_data.FillMissingFromCatalog) return;
            int next = 0;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (_slots[i].IsValid) continue;
                while (next < _items.Items.Count && used.Contains(new PlacementEntry(PlacementKind.Item, _items.Items[next].Id))) next++;
                if (next >= _items.Items.Count) break;
                _slots[i] = new PlacementEntry(PlacementKind.Item, _items.Items[next].Id);
                used.Add(_slots[i]);
            }
        }

        private bool Exists(PlacementEntry entry)
        {
            if (!entry.IsValid) return false;
            if (entry.Kind == PlacementKind.Item) return _items.Contains(entry.Id);
            return Array.IndexOf(_data.Facilities, entry.Id) >= 0;
        }

        /// <summary>
        /// 팔레트 목록: 아이템 카탈로그 중 Hotbar.json의 _paletteCategories(스토리 016, 기본 ["tile"])에 든 분류만(데이터 순서), 다음 시설.
        /// 옛 장식 아이템은 카탈로그에 남지만 팔레트에는 나오지 않는다. 빈 배열이면 전체.
        /// </summary>
        private List<PlacementEntry> PaletteEntries()
        {
            var list = new List<PlacementEntry>();
            string[] categories = PaletteCategories();
            foreach (ItemDefinition item in _items.Items)
                if (categories.Length == 0 || Array.IndexOf(categories, item.Category) >= 0)
                    list.Add(new PlacementEntry(PlacementKind.Item, item.Id));
            foreach (string facility in _data.Facilities) list.Add(new PlacementEntry(PlacementKind.Facility, facility));
            return list;
        }

        /// <summary>Hotbar.json의 _paletteCategories를 읽는다(HotbarData에 필드가 없어 같은 JSON을 따로 읽는다). 없으면 ["tile"].</summary>
        private static string[] PaletteCategories()
        {
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>("Hotbar/Hotbar");
            PaletteFilter filter = asset != null ? JsonUtility.FromJson<PaletteFilter>(asset.text) : null;
            return filter?._paletteCategories ?? new[] { ItemDefinition.TileCategory };
        }

#pragma warning disable 0649
        [Serializable] private sealed class PaletteFilter { public string[] _paletteCategories; }
#pragma warning restore 0649

        // ----- 화면 구성 -----

        private void BuildSlots()
        {
            VisualElement row = _root.Q("hotbar-slots");
            row.Clear();
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                int slot = i;
                var view = new VisualElement();
                view.AddToClassList("hotbar-slot");
                var icon = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore };
                icon.AddToClassList("hotbar-icon");
                var nameLabel = new Label { name = "name", pickingMode = PickingMode.Ignore };
                nameLabel.AddToClassList("hotbar-name");
                var key = new Label((i + 1).ToString()) { pickingMode = PickingMode.Ignore };
                key.AddToClassList("hotbar-key");
                view.Add(icon);
                view.Add(nameLabel);
                view.Add(key);
                RegisterPointer(view, () => _slots[slot], () => OnSlotClicked(slot));
                _slotViews[i] = view;
                row.Add(view);
            }
        }

        private void BuildPalette()
        {
            _paletteList.Clear();
            _paletteViews.Clear();
            foreach (PlacementEntry entry in PaletteEntries())
            {
                PlacementEntry captured = entry;
                var view = new VisualElement();
                view.AddToClassList("hotbar-entry");
                if (entry.Kind == PlacementKind.Facility) view.AddToClassList("hotbar-entry--facility");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("hotbar-icon");
                SetIcon(icon, entry);
                var label = new Label(DisplayName(entry)) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("hotbar-name");
                view.Add(icon);
                view.Add(label);
                RegisterPointer(view, () => captured, () => OnPaletteClicked(captured));
                _paletteViews.Add((entry, view));
                _paletteList.Add(view);
            }
        }

        /// <summary>현재 상태로 슬롯 아이콘·이름·선택 표시, 팔레트 표시를 다시 그린다.</summary>
        private void Refresh()
        {
            if (_root == null) return;
            bool open = _view.InteriorOpen;
            _hotbarRoot.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                VisualElement view = _slotViews[i];
                SetIcon(view.Q("icon"), _slots[i]);
                view.Q<Label>("name").text = _slots[i].IsValid ? DisplayName(_slots[i]) : "";
                view.EnableInClassList("hotbar-slot--selected", i == _selectedSlot);
                view.EnableInClassList("hotbar-slot--assignable", _pendingAssign.IsValid);
            }
            _palette.style.display = open && _paletteOpen ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var (entry, view) in _paletteViews) view.EnableInClassList("hotbar-entry--pending", entry.Equals(_pendingAssign));
        }

        private void SetIcon(VisualElement icon, PlacementEntry entry)
        {
            Sprite sprite = _ghost.IconFor(entry);
            icon.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
        }

        private string DisplayName(PlacementEntry entry)
        {
            if (!entry.IsValid) return "";
            if (entry.Kind == PlacementKind.Facility) return Strings.Get("facility_" + entry.Id);
            ItemDefinition item = _items.Get(entry.Id);
            return item != null ? item.Name : entry.Id;
        }

        private void SetPaletteOpen(bool open)
        {
            _paletteOpen = open;
            if (!open) _pendingAssign = default;
            Refresh();
        }

        // ----- 선택·배정 -----

        /// <summary>slot을 고른다(같은 슬롯이면 해제). 팔레트 항목이 대기 중이면 대신 그 항목을 slot에 배정한다.</summary>
        private void OnSlotClicked(int slot)
        {
            if (_pendingAssign.IsValid)
            {
                _slots[slot] = _pendingAssign;
                _pendingAssign = default;
                ShowNotice(Strings.Get("hotbar_assigned") + "  " + (slot + 1) + " · " + DisplayName(_slots[slot]));
                if (_selectedSlot == slot) _ghost.Select(_slots[slot]);
                Refresh();
                return;
            }
            SelectSlot(slot == _selectedSlot ? -1 : slot);
        }

        /// <summary>팔레트 항목을 배정 대기로 둔다(다시 누르면 해제). 다음 슬롯 클릭이 배정한다.</summary>
        private void OnPaletteClicked(PlacementEntry entry)
        {
            _pendingAssign = _pendingAssign.Equals(entry) ? default : entry;
            Refresh();
        }

        /// <summary>slot(0..8)을 선택해 고스트를 시작한다. 음수면 해제.</summary>
        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= SLOT_COUNT || !_slots[slot].IsValid)
            {
                _selectedSlot = -1;
                _ghost.ClearSelection();
            }
            else
            {
                _selectedSlot = slot;
                _ghost.Select(_slots[slot]);
            }
            Refresh();
        }

        /// <summary>slot의 배정(데모·검증용).</summary>
        public PlacementEntry Entry(int slot) => slot >= 0 && slot < SLOT_COUNT ? _slots[slot] : default;

        /// <summary>데모·캡처용: slot 항목을 끌기 시작한 상태로 만들고 드래그 표시를 screen(Input System 좌표)에 고정한다.</summary>
        public void BeginDemoDrag(int slot, Vector2 screen)
        {
            if (!Entry(slot).IsValid) return;
            _dragScreenOverride = screen;
            _ghost.BeginDrag(Entry(slot));
        }

        private void OnSelectionChanged(PlacementEntry entry)
        {
            if (!entry.IsValid) _selectedSlot = -1;
            Refresh();
        }

        // ----- 포인터: 클릭과 드래그 -----

        /// <summary>element에 PointerDown/Move/Up을 걸어 짧은 누름은 click, 임계값 이상 이동은 entry 드래그로 처리한다.</summary>
        private void RegisterPointer(VisualElement element, Func<PlacementEntry> entry, Action click)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _pressElement = element;
                _pressEntry = entry();
                _pressPosition = evt.position;
                _pressPointer = evt.pointerId;
                _pressDragging = false;
                _pressClick = click;
                element.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_pressElement != element || evt.pointerId != _pressPointer || _pressDragging) return;
                if (((Vector2)evt.position - _pressPosition).magnitude < _data.DragThresholdPixels || !_pressEntry.IsValid) return;
                _pressDragging = true;
                _dragScreenOverride = null;
                _ghost.BeginDrag(_pressEntry);
            });
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_pressElement != element || evt.pointerId != _pressPointer) return;
                if (element.HasPointerCapture(evt.pointerId)) element.ReleasePointer(evt.pointerId);
                bool dragged = _pressDragging;
                Action pressClick = _pressClick;
                ClearPress();
                if (dragged) _ghost.EndDrag(true);
                else pressClick?.Invoke();
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerCaptureOutEvent>(evt => { if (_pressElement == element && !_pressDragging) ClearPress(); });
        }

        private void ClearPress()
        {
            _pressElement = null;
            _pressEntry = default;
            _pressPointer = -1;
            _pressDragging = false;
            _pressClick = null;
        }

        /// <summary>screen(Input System 좌표, 왼쪽 아래 원점) 위에 이 HUD의 선택 가능한 요소가 있는지(PlacementGhost가 클릭을 무시하도록).</summary>
        private bool IsOverHud(Vector2 screen)
        {
            if (_root?.panel == null || !_view.InteriorOpen) return false;
            Vector2 panel = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            return _root.panel.Pick(panel) != null;
        }

        void Update()
        {
            if (_view == null) return;
            if (_view.InteriorOpen) ReadDigitKeys();
            UpdateDragChip();
            float now = Time.realtimeSinceStartup;
            if (_toast != null) _toast.style.display = (_view.InteriorOpen || _toastAnyView) && now < _toastUntil ? DisplayStyle.Flex : DisplayStyle.None;
            if (_notice != null) _notice.style.display = _view.InteriorOpen && now < _noticeUntil ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>키 1~9로 슬롯을 고른다(같은 키를 다시 누르면 해제).</summary>
        private void ReadDigitKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            KeyControl[] keys = { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key,
                keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key };
            for (int i = 0; i < SLOT_COUNT; i++)
                if (keys[i].wasPressedThisFrame) { SelectSlot(i == _selectedSlot ? -1 : i); return; }
        }

        /// <summary>드래그 중이면 끌고 있는 항목의 아이콘 칩을 포인터(또는 데모 고정 위치) 옆에 보인다.</summary>
        private void UpdateDragChip()
        {
            if (_drag == null) return;
            bool dragging = _ghost.IsDragging && _view.InteriorOpen;
            if (!dragging)
            {
                _dragScreenOverride = null;
                _drag.style.display = DisplayStyle.None;
                return;
            }
            _drag.style.display = DisplayStyle.Flex;
            SetIcon(_dragIcon, _ghost.DragEntry);
            Vector2 screen = _dragScreenOverride ?? (Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
            if (_root.panel == null) return;
            Vector2 panel = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            _drag.style.left = panel.x + 34;
            _drag.style.top = panel.y - 34;
        }

        // ----- 문구 -----

        private void OnNotice(string key, PlacementEntry entry)
        {
            if (key == "placement_placed") return; // 배치 성공은 칸 위 조각으로 보이므로 문구를 띄우지 않는다.
            ShowNotice(Strings.Get(key) + (entry.IsValid ? "  " + DisplayName(entry) : ""));
        }

        private void ShowNotice(string text)
        {
            _notice.text = text;
            _noticeUntil = Time.realtimeSinceStartup + _data.NoticeSeconds;
        }

        private void OnHabitatCompleted(string hubId, string typeId)
        {
            if (hubId != _view.InteriorHubId) return;
            string text = string.Format(Strings.Get("placement_habitat_complete"), HabitatName(typeId));
            _toastAnyView = false;
            // 스토리 022: UGUI 토스트가 있으면 넘긴다(내부 전용: 내부 화면을 닫으면 숨긴다).
            if (ToastScreen.Show(text, _data.ToastSeconds)) { _uguiInteriorToast = true; return; }
            _toast.text = text;
            _toastUntil = Time.realtimeSinceStartup + _data.ToastSeconds;
        }

        /// <summary>스토리 021: 같은 토스트 자리에 문구를 띄운다(지도·내부 화면 모두). 도감 해금 알림이 쓴다.</summary>
        public void ShowToast(string text)
        {
            // 스토리 022: UGUI 토스트(ToastScreen)가 있으면 그쪽에 띄운다.
            if (_data != null && ToastScreen.Show(text, _data.ToastSeconds)) { _uguiInteriorToast = false; return; }
            if (_toast == null) return;
            _toast.text = text;
            _toastUntil = Time.realtimeSinceStartup + _data.ToastSeconds;
            _toastAnyView = true;
        }

        /// <summary>토스트 표시 시간(초, 데이터).</summary>
        public float ToastSeconds => _data.ToastSeconds;

        /// <summary>
        /// 서식지 종류의 한국어 이름: 문자열 표의 habitat_type_&lt;id&gt; 또는 habitat_&lt;id&gt; 키, 없으면 016 구현이 공개하는
        /// string X(string) 형태의 이름 메서드(Name/DisplayName/NameOf/HabitatName), 그것도 없으면 id.
        /// </summary>
        private string HabitatName(string typeId)
        {
            foreach (string key in new[] { "habitat_type_" + typeId, "habitat_" + typeId })
            {
                try { return Strings.Get(key); }
                catch (KeyNotFoundException) { }
                catch (FormatException) { }
            }
            if (_clusters != null)
                foreach (string method in new[] { "Name", "DisplayName", "NameOf", "HabitatName" })
                {
                    MethodInfo info = _clusters.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                    if (info != null && info.ReturnType == typeof(string) && info.Invoke(_clusters, new object[] { typeId }) is string name && name.Length > 0) return name;
                }
            return typeId;
        }

        private void OnInteriorChanged(bool open)
        {
            if (!open)
            {
                _selectedSlot = -1;
                _pendingAssign = default;
                _paletteOpen = false;
                if (!_toastAnyView) _toastUntil = 0;
                if (_uguiInteriorToast) { ToastScreen.Hide(); _uguiInteriorToast = false; }
                ClearPress();
            }
            Refresh();
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            PlacementGhost.UiHitTests.Remove(IsOverHud);
            if (_ghost != null) { _ghost.SelectionChanged -= OnSelectionChanged; _ghost.Notice -= OnNotice; }
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_clusters != null) _clusters.HabitatCompleted -= OnHabitatCompleted;
            if (_instance == this) _instance = null;
        }
    }
}
