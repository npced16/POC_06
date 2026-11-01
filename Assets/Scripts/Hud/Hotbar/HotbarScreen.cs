using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Items;
using IronRing.Localization;
using IronRing.Presentation.Interior;

namespace IronRing.Hud.Hotbar
{
    /// <summary>스토리 022. PlacementDemoDriver가 핫바를 조작하는 공통 면(HotbarScreen이 구현).</summary>
    public interface IHotbarDemoTarget
    {
        /// <summary>slot(0..8)의 배정.</summary>
        PlacementEntry Entry(int slot);
        /// <summary>slot을 선택해 고스트를 시작한다. 음수면 해제.</summary>
        void SelectSlot(int slot);
        /// <summary>slot 항목을 끌기 시작한 상태로 만들고 드래그 칩을 screen에 고정한다.</summary>
        void BeginDemoDrag(int slot, Vector2 screen);
    }

    /// <summary>
    /// 스토리 022(HotbarHud의 UGUI 포팅). 항상 켜진 화면 "Hotbar": 거점 내부에서만 내용(_content)을 보인다. 하단 9칸(키 1~9), 안내 줄,
    /// "놓을 수 없다" 알림, 팔레트(카탈로그 타일 + 시설; 항목을 누른 뒤 슬롯을 누르면 배정), 드래그 칩. 슬롯 선택은 PlacementGhost.Select,
    /// 끌어 놓기는 PlacementGhost.BeginDrag/EndDrag(true)(UI 위에서 놓으면 고스트가 EventSystem 판정으로 거른다). 토스트는 ToastScreen.
    /// 게임 상태는 바꾸지 않고 고스트에 명령만 보낸다. 참조는 인스펙터로 꽂는다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class HotbarScreen : MonoBehaviour, IHotbarDemoTarget
    {
        /// <summary>슬롯 수.</summary>
        public const int SLOT_COUNT = 9;
        private static HotbarScreen _instance;
        /// <summary>현재 핫바 화면(데모 드라이버용). 초기화 전·캔버스가 없으면 null.</summary>
        public static HotbarScreen Instance => _instance;

        [Tooltip("거점 내부에서만 켜는 모든 내용(핫바·팔레트·알림·드래그 칩).")]
        [SerializeField] private GameObject _content;
        [SerializeField] private HotbarCellView[] _slotViews = new HotbarCellView[SLOT_COUNT];
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private GameObject _noticeBox;
        [SerializeField] private TMP_Text _notice;
        [SerializeField] private Button _paletteToggle;
        [Tooltip("스토리 034. 열차 장비 모드 전환 버튼(T 키와 같음).")]
        [SerializeField] private GameObject _palette;
        [SerializeField] private TMP_Text _paletteTitle;
        [SerializeField] private TMP_Text _paletteHint;
        [SerializeField] private Button _paletteClose;
        [SerializeField] private RectTransform _paletteContent;
        [Tooltip("팔레트 항목 부품(Parts/PaletteItem.prefab).")]
        [SerializeField] private HotbarCellView _paletteItemPrefab;
        [SerializeField] private RectTransform _dragChip;
        [SerializeField] private Image _dragIcon;
        [Tooltip("드래그 칩을 포인터에서 띄우는 거리(캔버스 단위).")]
        [SerializeField] private Vector2 _dragChipOffset = new Vector2(34, 34);

        private IMoney _money;
        private IPlacementCost _cost;
        private IViewMode _view;
        private IHabitatClusters _clusters;
        private PlacementGhost _ghost;
        private HotbarData _data;
        private ItemCatalog _items;
        private UiTheme _theme;

        private readonly PlacementEntry[] _slots = new PlacementEntry[SLOT_COUNT];
        private readonly List<(PlacementEntry entry, HotbarCellView view)> _paletteViews = new List<(PlacementEntry, HotbarCellView)>();
        private int _selectedSlot = -1;
        private PlacementEntry _pendingAssign;
        private bool _paletteOpen;
        private bool _interiorToast;
        private float _noticeUntil;
        private Vector2? _dragScreenOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>내부 화면 서비스·아이템 카탈로그·고스트가 있고 캔버스에 켜진 화면이 있으면 한 번 바인딩한다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IViewMode>() == null || ItemsInstaller.Catalog == null) return;
            UiScreens screens = UiScreens.Instance;
            HotbarScreen placed = screens != null ? screens.Find<HotbarScreen>() : null;
            if (placed == null || !placed.enabled || placed._content == null || placed._paletteItemPrefab == null) return;
            PlacementGhost ghost = PlacementGhost.Ensure();
            if (ghost == null) return;
            _instance = placed;
            placed.Initialize(screens, ghost);
        }

        /// <summary>screens와 ghost 및 경제 서비스를 저장하고 핫바 입력·가격 갱신 이벤트를 연결한다.</summary>
        private void Initialize(UiScreens screens, PlacementGhost ghost)
        {
            _theme = screens.Theme;
            _ghost = ghost;
            _view = GameServices.Get<IViewMode>();
            _clusters = GameServices.Get<IHabitatClusters>();
            _items = ItemsInstaller.Catalog;
            _money = GameServices.Get<IMoney>();
            _cost = GameServices.Get<IPlacementCost>();
            if (_money != null) _money.Changed += Refresh;
            _data = HotbarData.Load();

            _hint.text = Strings.Get("hotbar_hint");
            _paletteTitle.text = Strings.Get("hotbar_palette_title");
            _paletteHint.text = Strings.Get("hotbar_palette_hint");
            SetButtonText(_paletteToggle, Strings.Get("hotbar_palette"));
            SetButtonText(_paletteClose, Strings.Get("inv_close"));
            _paletteToggle.onClick.AddListener(() => SetPaletteOpen(!_paletteOpen));
            _paletteClose.onClick.AddListener(() => SetPaletteOpen(false));
            _noticeBox.SetActive(false);
            _dragChip.gameObject.SetActive(false);

            AssignDefaults();
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                int slot = i;
                if (_slotViews[i] == null) continue;
                _slotViews[i].SetKey((i + 1).ToString());
                _slotViews[i].Bind(this, () => _slots[slot], () => OnSlotClicked(slot));
            }
            BuildPalette();

            _ghost.SelectionChanged += OnSelectionChanged;
            _ghost.Notice += OnNotice;
            _ghost.Demolished += OnDemolished;
            _ghost.Undone += OnUndone;
            _view.InteriorChanged += OnInteriorChanged;
            if (_clusters != null) _clusters.HabitatCompleted += OnHabitatCompleted;
            Refresh();
        }

        private static void SetButtonText(Button button, string text)
        {
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = text;
        }

        // ----- 데이터 배정 (HotbarHud와 같은 규칙) -----

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
            return entry.Kind == PlacementKind.Item && _items.Contains(entry.Id);
        }

        /// <summary>팔레트 목록: Hotbar.json _paletteCategories(기본 ["tile"]) 분류의 아이템(데이터 순서), 다음 시설.</summary>
        private List<PlacementEntry> PaletteEntries()
        {
            var list = new List<PlacementEntry>();
            string[] categories = PaletteCategories();
            foreach (ItemDefinition item in _items.Items)
                if (categories.Length == 0 || Array.IndexOf(categories, item.Category) >= 0)
                    list.Add(new PlacementEntry(PlacementKind.Item, item.Id));
            return list;
        }

        private static string[] PaletteCategories()
        {
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>("Hotbar/Hotbar");
            PaletteFilter filter = asset != null ? JsonUtility.FromJson<PaletteFilter>(asset.text) : null;
            return filter?._paletteCategories ?? new[] { ItemDefinition.TileCategory };
        }

#pragma warning disable 0649
        [Serializable] private sealed class PaletteFilter { public string[] _paletteCategories; }
#pragma warning restore 0649

        // ----- 그리기 -----

        /// <summary>프리팹의 예시 항목을 지우고 팔레트 부품을 목록 수만큼 만든다(동적 목록).</summary>
        private void BuildPalette()
        {
            for (int i = _paletteContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = _paletteContent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            _paletteViews.Clear();
            foreach (PlacementEntry entry in PaletteEntries())
            {
                PlacementEntry captured = entry;
                HotbarCellView view = Instantiate(_paletteItemPrefab, _paletteContent);
                view.name = "Entry_" + entry.Id;
                view.Show(_ghost.IconFor(entry), DisplayName(entry));
                view.Bind(this, () => captured, () => OnPaletteClicked(captured));
                _paletteViews.Add((entry, view));
            }
        }

        private bool _suppressed;

        /// <summary>
        /// 스토리 029: 대화 장면처럼 화면 아래를 덮는 상황 화면이 열려 있는 동안 핫바 내용을 숨긴다(true). 거점 내부 열림·닫힘 규칙은 그대로이며,
        /// false로 풀면 지금 내부가 열려 있을 때만 다시 보인다.
        /// </summary>
        public void SetSuppressed(bool suppressed)
        {
            if (_suppressed == suppressed) return;
            _suppressed = suppressed;
            Refresh();
        }

        /// <summary>현재 상태로 내용 표시·슬롯 아이콘·이름·선택 틀·팔레트를 다시 그린다.</summary>
        private void Refresh()
        {
            if (_view == null) return;
            bool open = _view.InteriorOpen && !_suppressed;
            _content.SetActive(open);
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                HotbarCellView view = _slotViews[i];
                if (view == null) continue;
                view.Show(_slots[i].IsValid ? _ghost.IconFor(_slots[i]) : null, DisplayName(_slots[i]));
                view.SetState(i == _selectedSlot, _pendingAssign.IsValid, _theme);
                RefreshPrice(view, _slots[i]);
            }
            _palette.SetActive(open && _paletteOpen);
            foreach (var (entry, view) in _paletteViews)
            {
                view.SetState(entry.Equals(_pendingAssign), false, _theme);
                RefreshPrice(view, entry);
            }
        }

        /// <summary>entry의 데이터 가격과 현재 결제 가능 상태를 읽어 view의 가격·부족 표시를 갱신한다.</summary>
        private void RefreshPrice(HotbarCellView view, PlacementEntry entry)
        {
            ItemDefinition item = entry.Kind == PlacementKind.Item ? _items.Get(entry.Id) : null;
            bool affordable = item == null || _money == null || (_cost != null ? _cost.CanAfford(entry.Id) : _money.Balance >= item.Price);
            view.SetPrice(item != null ? (_cost != null ? _cost.PriceOf(entry.Id) : item.Price) : (int?)null, affordable);
        }

        /// <summary>entry에 해당하는 시설 또는 아이템의 표시 이름을 반환한다.</summary>
        private string DisplayName(PlacementEntry entry)
        {
            if (!entry.IsValid) return "";
            ItemDefinition item = _items.Get(entry.Id);
            return item != null ? item.Name : entry.Id;
        }

        /// <summary>open에 맞춰 팔레트를 표시하고 닫을 때 배정 대기를 해제한다.</summary>
        private void SetPaletteOpen(bool open)
        {
            _paletteOpen = open;
            if (!open) _pendingAssign = default;
            Refresh();
        }

        // ----- 선택·배정 -----

        /// <summary>slot 클릭 시 대기 중 항목을 배정하거나 해당 슬롯의 선택을 전환한다.</summary>
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

        private void OnPaletteClicked(PlacementEntry entry)
        {
            _pendingAssign = _pendingAssign.Equals(entry) ? default : entry;
            Refresh();
        }

        /// <summary>slot(0..8)을 선택해 고스트를 시작한다. 음수·빈 슬롯이면 해제.</summary>
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

        /// <summary>데모·캡처용: slot 항목을 끌기 시작한 상태로 만들고 드래그 칩을 screen(Input System 좌표)에 고정한다.</summary>
        public void BeginDemoDrag(int slot, Vector2 screen)
        {
            if (!Entry(slot).IsValid) return;
            _dragScreenOverride = screen;
            _ghost.BeginDrag(Entry(slot));
        }

        /// <summary>드래그 시작 임계값(픽셀, Hotbar.json).</summary>
        public float DragThreshold => _data != null ? _data.DragThresholdPixels : 8f;

        /// <summary>슬롯·팔레트 부품이 포인터 드래그를 시작할 때 부른다.</summary>
        public void BeginPointerDrag(PlacementEntry entry)
        {
            if (_ghost == null) return;
            _dragScreenOverride = null;
            _ghost.BeginDrag(entry);
        }

        /// <summary>슬롯·팔레트 부품이 드래그를 놓을 때 부른다. 월드 위면 고스트가 스냅한 칸에 놓고, UI 위면 고스트가 거른다.</summary>
        public void EndPointerDrag()
        {
            if (_ghost != null) _ghost.EndDrag(true);
        }

        private void OnSelectionChanged(PlacementEntry entry)
        {
            if (!entry.IsValid) _selectedSlot = -1;
            Refresh();
        }

        void Update()
        {
            if (_view == null) return;
            if (_view.InteriorOpen) ReadDigitKeys();
            UpdateDragChip();
            bool notice = _view.InteriorOpen && Time.realtimeSinceStartup < _noticeUntil;
            if (_noticeBox.activeSelf != notice) _noticeBox.SetActive(notice);
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

        /// <summary>드래그 중이면 끄는 항목의 아이콘 칩을 포인터(또는 데모 고정 위치) 옆에 보인다.</summary>
        private void UpdateDragChip()
        {
            bool dragging = _ghost.IsDragging && _view.InteriorOpen;
            if (!dragging)
            {
                _dragScreenOverride = null;
                if (_dragChip.gameObject.activeSelf) _dragChip.gameObject.SetActive(false);
                return;
            }
            if (!_dragChip.gameObject.activeSelf) _dragChip.gameObject.SetActive(true);
            Sprite sprite = _ghost.IconFor(_ghost.DragEntry);
            _dragIcon.sprite = sprite;
            _dragIcon.enabled = sprite != null;
            Vector2 screen = _dragScreenOverride ?? (Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
            var parent = (RectTransform)_dragChip.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local))
                _dragChip.anchoredPosition = local + _dragChipOffset;
        }

        // ----- 문구 -----

        private void OnNotice(string key, PlacementEntry entry)
        {
            if (key == "placement_placed") return; // 배치 성공은 칸 위 조각으로 보인다.
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
            if (ToastScreen.Show(text, _data.ToastSeconds)) _interiorToast = true;
        }

        /// <summary>스토리 031. 건물 철거 토스트 "&lt;종류 이름&gt; 철거".</summary>
        private void OnDemolished(string hubId, string typeId)
        {
            string text = string.Format(Strings.Get("placement_demolished"), HabitatName(typeId));
            if (ToastScreen.Show(text, _data.ToastSeconds)) _interiorToast = true;
        }

        /// <summary>스토리 031. 되돌림 토스트 "되돌림: &lt;동작&gt;"(InteriorCatalog._undoToast가 꺼져 있으면 띄우지 않는다).</summary>
        private void OnUndone(PlacementAction action)
        {
            if (action == null || _ghost == null || !_ghost.UndoToastEnabled) return;
            string what;
            if (action.Kind == PlacementActionKind.Demolish) what = string.Format(Strings.Get("placement_demolished"), HabitatName(action.TypeId));
            else
            {
                string item = action.Tiles.Count > 0 ? DisplayName(new PlacementEntry(PlacementKind.Item, action.Tiles[0].Value)) : "";
                what = Strings.Get(action.Kind == PlacementActionKind.Place ? "placement_placed" : "placement_picked") + " " + item;
            }
            if (ToastScreen.Show(string.Format(Strings.Get("placement_undone"), what), _data.ToastSeconds)) _interiorToast = true;
        }

        /// <summary>토스트 자리에 문구를 띄운다(지도·내부 화면 모두, ToastScreen).</summary>
        public void ShowToast(string text)
        {
            if (ToastScreen.Show(text, ToastSeconds)) _interiorToast = false;
        }

        /// <summary>토스트 표시 시간(초, Hotbar.json).</summary>
        public float ToastSeconds => (_data ?? HotbarData.Load()).ToastSeconds;

        /// <summary>서식지 종류 이름: habitat_type_&lt;id&gt; 또는 habitat_&lt;id&gt; 키, 없으면 클러스터 서비스의 이름 메서드, 그것도 없으면 id.</summary>
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
                if (_interiorToast) { ToastScreen.Hide(); _interiorToast = false; }
            }
            Refresh();
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_money != null) _money.Changed -= Refresh;
            if (_ghost != null)
            {
                _ghost.SelectionChanged -= OnSelectionChanged;
                _ghost.Notice -= OnNotice;
                _ghost.Demolished -= OnDemolished;
                _ghost.Undone -= OnUndone;
            }
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_clusters != null) _clusters.HabitatCompleted -= OnHabitatCompleted;
            if (_instance == this) _instance = null;
        }
    }
}
