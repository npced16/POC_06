using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.UIElements;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Items;
using IronRing.Localization;
using IronRing.Presentation.Items;

namespace IronRing.Hud.Items
{
    /// <summary>
    /// 스토리 013. 거점 내부 화면 전용 한국어 HUD: 아래 인벤토리 바(아이콘+수량), 제작 메뉴, 칸 메뉴(빈 터 → 배치, 배치된 칸 → 회수),
    /// 서식지 태그 진행도와 완성 문구. 게임 상태는 IItemInventory/IItemPlacement/IHabitatSystem이 소유하고 HUD는 명령과 표시만 한다.
    /// 스토리 015: 자원·제작이 없어지고 타일·아이템이 무제한이 되어 인벤토리 바(수량), 제작 메뉴, 칸 클릭 메뉴, 완성 배너를 숨긴다(코드는 보존,
    /// LegacyMenus로 다시 켤 수 있다). 배치·회수는 핫바(HotbarHud)와 PlacementGhost가, 완성 문구는 핫바 토스트가 맡는다. 서식지 상태 패널은 유지한다.
    /// </summary>
    public sealed class InventoryHud : MonoBehaviour
    {
        private static InventoryHud _instance;
        private bool _bound;
        /// <summary>스토리 013의 인벤토리 바·제작 메뉴·칸 메뉴·배너를 보일지(015부터 false: 숨김, 코드 보존).</summary>
        private static readonly bool LegacyMenus = false;
        /// <summary>현재 HUD(데모 드라이버가 메뉴를 열 때 쓴다). 없으면 null.</summary>
        public static InventoryHud Instance => _instance;

        private IItemInventory _items;
        private IItemPlacement _placement;
        private IHubInterior _interior;
        private IViewMode _view;
        private IInteriorCellClickSource _clicks;
        private IHabitatSystem _habitat;
        private IHabitatClusters _clusters;
        private IResourceInventory _resources;
        private ItemCatalog _catalog;
        private ItemSpriteSet _sprites;
        private ItemsViewSettings _settings;
        private PanelSettings _panel;

        private VisualElement _root;
        private VisualElement _bar;
        private VisualElement _itemsRow;
        private VisualElement _craftMenu;
        private ScrollView _craftList;
        private VisualElement _cellMenu;
        private VisualElement _cellList;
        private Label _cellTitle;
        private Label _cellSubtitle;
        private Label _banner;
        private Label _message;

        private string _cellHub;
        private int _cellIndex = -1;
        private float _bannerUntil;
        private bool _bannerPinned;
        private bool _craftOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>스토리 018: 씬(MainUI)에 놓인 문서는 서비스 준비(Initialize) 전까지 꺼 두어 UXML 예시 문구가 Play 화면에 비치지 않게 한다.</summary>
        void Awake() { if (!_bound && TryGetComponent(out UIDocument document)) document.enabled = false; }

        /// <summary>필요한 서비스가 모두 등록되어 있을 때만 HUD를 만든다(다른 스토리 단독 데모에서는 만들지 않는다).</summary>
        private static void Create()
        {
            if (_instance != null) return;
            if (UiScreens.Instance != null) return; // 스토리 022: 남은 기능이 없어 UGUI로 옮기지 않는다(3단계에서 삭제).
            if (GameServices.Get<IItemInventory>() == null || GameServices.Get<IItemPlacement>() == null
                || GameServices.Get<IHubInterior>() == null || GameServices.Get<IViewMode>() == null
                || GameServices.Get<IResourceInventory>() == null || ItemsInstaller.Catalog == null) return;
            InventoryHud placed = FindAnyObjectByType<InventoryHud>(FindObjectsInactive.Include);
            // 스토리 018: 씬(MainUI)에 놓인 HUD에 바인딩한다. 제작자가 꺼 둔 화면이면 대비책도 만들지 않는다.
            if (placed != null && !(placed.enabled && placed.gameObject.activeInHierarchy)) return;
            _instance = placed != null ? placed : new GameObject(nameof(InventoryHud)).AddComponent<InventoryHud>();
            _instance.Initialize();
        }

        private void Initialize()
        {
            _items = GameServices.Get<IItemInventory>();
            _placement = GameServices.Get<IItemPlacement>();
            _interior = GameServices.Get<IHubInterior>();
            _view = GameServices.Get<IViewMode>();
            _clicks = GameServices.Get<IInteriorCellClickSource>();
            _habitat = GameServices.Get<IHabitatSystem>();
            _clusters = GameServices.Get<IHabitatClusters>();
            _resources = GameServices.Get<IResourceInventory>();
            _catalog = ItemsInstaller.Catalog;
            _sprites = ItemSpriteSet.Load();
            _settings = ItemsViewSettings.Load();

            _bound = true;
            // 스토리 018: 씬에 놓인 UIDocument가 있으면 비어 있는 값만 채운다(Sort Order·PanelSettings·UXML은 프리팹 값 우선).
            UIDocument document = HudDocument.Bind(gameObject, () => _panel = Instantiate(KoreanUi.DefaultPanel), _settings.HudSortingOrder, "Hud/InventoryHud");
            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            foreach (TemplateContainer container in _root.Query<TemplateContainer>().ToList()) container.pickingMode = PickingMode.Ignore;
            VisualElement invRoot = _root.Q("inv-root");
            invRoot.pickingMode = PickingMode.Ignore;
            // KoreanHud와 같은 이유로 USS는 UXML과 다른 이름으로 두고 직접 붙인다.
            StyleSheet style = UnityEngine.Resources.Load<StyleSheet>("Hud/InventoryHudStyle");
            if (style == null) UnityEngine.Debug.LogError("InventoryHud: Resources/Hud/InventoryHudStyle.uss를 불러오지 못했습니다.");
            else if (!_root.styleSheets.Contains(style)) _root.styleSheets.Add(style);
            KoreanUi.Apply(_root);

            _bar = _root.Q("inv-bar");
            _itemsRow = _root.Q("inv-items");
            _craftMenu = _root.Q("inv-craft-menu");
            _craftList = _root.Q<ScrollView>("inv-craft-list");
            _cellMenu = _root.Q("inv-cell-menu");
            _cellList = _root.Q("inv-cell-list");
            _cellTitle = _root.Q<Label>("inv-cell-title");
            _cellSubtitle = _root.Q<Label>("inv-cell-subtitle");
            _banner = _root.Q<Label>("inv-banner");
            _message = _root.Q<Label>("inv-message");
            _banner.pickingMode = PickingMode.Ignore;
            _root.Q<Label>("inv-bar-title").text = Strings.Get("inv_bar_title");
            _root.Q<Label>("inv-craft-title").text = Strings.Get("inv_craft_title");
            Button toggle = _root.Q<Button>("inv-craft-toggle");
            toggle.text = Strings.Get("inv_craft");
            toggle.clicked += ToggleCraftMenu;
            Button craftClose = _root.Q<Button>("inv-craft-close");
            craftClose.text = Strings.Get("inv_close");
            craftClose.clicked += () => SetCraftOpen(false);
            Button cellClose = _root.Q<Button>("inv-cell-close");
            cellClose.text = Strings.Get("inv_close");
            cellClose.clicked += CloseCellMenu;
            _banner.text = Strings.Get("inv_banner_complete");
            _message.text = "";
            // UXML은 옛 바를 display:none으로 둔다(편집 모드 미리보기도 Play와 같게). LegacyMenus면 다시 켠다.
            _bar.style.display = LegacyMenus ? DisplayStyle.Flex : DisplayStyle.None;

            _items.Changed += OnCount;
            _resources.Changed += OnCount;
            _placement.ItemPlaced += OnPlacement;
            _placement.ItemRemoved += OnPlacement;
            _interior.CellChanged += OnCell;
            _view.InteriorChanged += OnInterior;
            if (_habitat != null) _habitat.EventFlagRaised += OnFlag;
            if (_clusters != null) { _clusters.HabitatCompleted += OnCluster; _clusters.HabitatBroken += OnCluster; }
            // 015: 칸 클릭은 핫바 고스트 배치가 쓴다. 칸 메뉴는 LegacyMenus일 때만 연결한다.
            if (_clicks != null && LegacyMenus) _clicks.CellClicked += OpenCellMenu;
            Refresh();
        }

        /// <summary>제작 메뉴를 연다(데모·캡처용).</summary>
        public void ShowCraftMenu() { SetCraftOpen(true); }

        /// <summary>서식지 완성 배너를 계속 보이게 한다(데모·캡처용).</summary>
        public void PinBanner() { _bannerPinned = LegacyMenus; Refresh(); }

        /// <summary>칸 상태를 보고 빈 터면 배치 메뉴, 배치된 칸이면 회수 메뉴를 연다. 다른 상태면 닫는다(012/014가 처리).</summary>
        public void OpenCellMenu(string hubId, int cellIndex)
        {
            if (!LegacyMenus || !_view.InteriorOpen || hubId != _view.InteriorHubId) return;
            IInteriorCell cell = Cell(hubId, cellIndex);
            if (cell == null || (cell.State != InteriorCellState.Empty && cell.State != InteriorCellState.ItemPlaced)) { CloseCellMenu(); return; }
            _cellHub = hubId;
            _cellIndex = cellIndex;
            SetVisible(_cellMenu, true);
            Refresh();
        }

        private void CloseCellMenu()
        {
            _cellHub = null;
            _cellIndex = -1;
            SetVisible(_cellMenu, false);
        }

        private void ToggleCraftMenu() => SetCraftOpen(!_craftOpen);

        private void SetCraftOpen(bool open) { _craftOpen = open && LegacyMenus; SetVisible(_craftMenu, _craftOpen); Refresh(); }

        void Update()
        {
            if (_banner == null) return;
            bool show = LegacyMenus && _view.InteriorOpen && (_bannerPinned || Time.realtimeSinceStartup < _bannerUntil);
            SetVisible(_banner, show);
        }

        /// <summary>현재 상태로 바·메뉴를 다시 그린다(스토리 021: 서식지 조건 패널은 도감으로 옮겼다).</summary>
        private void Refresh()
        {
            if (_root == null) return;
            bool open = _view.InteriorOpen;
            _root.Q("inv-root").style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) return;
            RefreshBar();
            RefreshCraft();
            RefreshCell();
        }

        private void RefreshBar()
        {
            if (!LegacyMenus) return;
            _itemsRow.Clear();
            bool any = false;
            foreach (ItemDefinition item in _catalog.Items)
            {
                int count = _items.Count(item.Id);
                if (count <= 0) continue;
                any = true;
                var cell = new VisualElement();
                cell.AddToClassList("inv-item");
                cell.Add(Icon(item.Id, "inv-icon"));
                cell.Add(TextLabel(item.Name, "inv-name"));
                cell.Add(TextLabel("×" + count, "inv-count"));
                _itemsRow.Add(cell);
            }
            if (!any) _itemsRow.Add(TextLabel(Strings.Get("inv_bar_empty"), "inv-detail"));
        }

        private void RefreshCraft()
        {
            if (!_craftOpen) return;
            _craftList.Clear();
            foreach (ItemDefinition item in _catalog.Items)
            {
                string id = item.Id;
                var row = new VisualElement();
                row.AddToClassList("inv-row");
                var left = new VisualElement();
                left.AddToClassList("inv-row-left");
                left.Add(Icon(id, "inv-icon-small"));
                var text = new VisualElement();
                text.AddToClassList("inv-row-text");
                text.Add(TextLabel(item.Name + "  ×" + _items.Count(id), "inv-name"));
                text.Add(TextLabel(Strings.Get("inv_tags") + " " + TagText(item) + "   " + Strings.Get("inv_cost") + " " + CostText(item), "inv-detail"));
                left.Add(text);
                row.Add(left);
                var craft = new Button(() => Craft(id)) { text = Strings.Get("inv_craft") };
                craft.SetEnabled(_items.CanCraft(id));
                row.Add(craft);
                _craftList.Add(row);
            }
        }

        private void RefreshCell()
        {
            if (_cellHub == null) return;
            IInteriorCell cell = Cell(_cellHub, _cellIndex);
            if (cell == null || _cellHub != _view.InteriorHubId
                || (cell.State != InteriorCellState.Empty && cell.State != InteriorCellState.ItemPlaced)) { CloseCellMenu(); return; }
            _cellTitle.text = Strings.Get("inv_cell_title") + " " + (_cellIndex + 1);
            _cellList.Clear();
            string hub = _cellHub;
            int index = _cellIndex;
            if (cell.State == InteriorCellState.ItemPlaced)
            {
                _cellSubtitle.text = Strings.Get("inv_pickup_title");
                ItemDefinition item = _catalog.Get(cell.ItemId);
                var row = new VisualElement();
                row.AddToClassList("inv-row");
                var left = new VisualElement();
                left.AddToClassList("inv-row-left");
                left.Add(Icon(cell.ItemId, "inv-icon-small"));
                left.Add(TextLabel(item != null ? item.Name : cell.ItemId, "inv-name"));
                row.Add(left);
                row.Add(new Button(() => PickUp(hub, index)) { text = Strings.Get("inv_pickup") });
                _cellList.Add(row);
                return;
            }
            _cellSubtitle.text = Strings.Get("inv_place_title");
            bool any = false;
            foreach (ItemDefinition item in _catalog.Items)
            {
                int count = _items.Count(item.Id);
                if (count <= 0) continue;
                any = true;
                string id = item.Id;
                var row = new VisualElement();
                row.AddToClassList("inv-row");
                var left = new VisualElement();
                left.AddToClassList("inv-row-left");
                left.Add(Icon(id, "inv-icon-small"));
                left.Add(TextLabel(item.Name + "  ×" + count + "   " + TagText(item), "inv-name"));
                row.Add(left);
                var place = new Button(() => Place(hub, index, id)) { text = Strings.Get("inv_place_title") };
                place.SetEnabled(_placement.CanPlace(hub, index, id));
                row.Add(place);
                _cellList.Add(row);
            }
            if (!any) _cellList.Add(TextLabel(Strings.Get("inv_place_none"), "inv-detail"));
        }

        private void Craft(string itemId)
        {
            bool ok = _items.TryCraft(itemId);
            Say(ok ? ItemName(itemId) + " " + Strings.Get("inv_crafted") : Strings.Get("inv_craft_refused"));
            Refresh();
        }

        private void Place(string hubId, int cellIndex, string itemId)
        {
            bool ok = _placement.TryPlace(hubId, cellIndex, itemId);
            Say(ok ? ItemName(itemId) + " " + Strings.Get("inv_placed") : Strings.Get("inv_place_refused"));
            Refresh();
        }

        private void PickUp(string hubId, int cellIndex)
        {
            IInteriorCell cell = Cell(hubId, cellIndex);
            string itemId = cell?.ItemId;
            bool ok = _placement.TryPickUp(hubId, cellIndex);
            Say(ok ? ItemName(itemId) + " " + Strings.Get("inv_picked") : Strings.Get("inv_place_refused"));
            Refresh();
        }

        private void Say(string text) { if (_message != null) _message.text = text; }

        private string ItemName(string itemId) => _catalog.Get(itemId)?.Name ?? itemId;

        private string TagText(ItemDefinition item)
        {
            var sb = new StringBuilder();
            foreach (string tag in item.Tags) { if (sb.Length > 0) sb.Append('·'); sb.Append(_catalog.TagName(tag)); }
            return sb.ToString();
        }

        private static string CostText(ItemDefinition item)
        {
            var sb = new StringBuilder();
            foreach (var cost in item.Cost)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Strings.Get("resource_" + cost.Key)).Append(' ').Append(cost.Value);
            }
            return sb.ToString();
        }

        private static string HubName(string hubId) => string.IsNullOrEmpty(hubId) ? "" : Strings.Get(hubId.Replace('-', '_'));

        private VisualElement Icon(string itemId, string cssClass)
        {
            var icon = new VisualElement();
            icon.AddToClassList(cssClass);
            Sprite sprite = _sprites != null ? _sprites.Get(itemId) : null;
            if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
            return icon;
        }

        private static Label TextLabel(string text, string cssClass)
        {
            var label = new Label(text);
            label.AddToClassList(cssClass);
            return label;
        }

        private IInteriorCell Cell(string hubId, int cellIndex)
        {
            if (hubId == null) return null;
            IReadOnlyList<IInteriorCell> cells;
            try { cells = _interior.Cells(hubId); }
            catch (System.ArgumentException) { return null; }
            return cells != null && cellIndex >= 0 && cellIndex < cells.Count ? cells[cellIndex] : null;
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnCount(string id, int value) => Refresh();
        private void OnPlacement(string hubId, int cellIndex, string itemId) => Refresh();
        private void OnCell(string hubId, int cellIndex) => Refresh();

        private void OnInterior(bool open)
        {
            if (!open) { CloseCellMenu(); _craftOpen = false; SetVisible(_craftMenu, false); _bannerPinned = false; }
            Refresh();
        }

        /// <summary>서식지 완성·해제 때 목록만 갱신한다(완성 문구 배너는 Story 015 토스트가 맡는다).</summary>
        private void OnCluster(string hubId, string typeId) { Refresh(); }

        private void OnFlag(string hubId)
        {
            if (hubId == _view.InteriorHubId) _bannerUntil = Time.realtimeSinceStartup + _settings.BannerSeconds;
            Refresh();
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_items != null)
            {
                _items.Changed -= OnCount;
                _resources.Changed -= OnCount;
                _placement.ItemPlaced -= OnPlacement;
                _placement.ItemRemoved -= OnPlacement;
                _interior.CellChanged -= OnCell;
                _view.InteriorChanged -= OnInterior;
                if (_habitat != null) _habitat.EventFlagRaised -= OnFlag;
                if (_clusters != null) { _clusters.HabitatCompleted -= OnCluster; _clusters.HabitatBroken -= OnCluster; }
                if (_clicks != null) _clicks.CellClicked -= OpenCellMenu;
            }
            if (_panel != null) Destroy(_panel);
            if (_instance == this) _instance = null;
        }
    }
}
