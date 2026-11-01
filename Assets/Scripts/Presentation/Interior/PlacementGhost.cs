using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Presentation.Items;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 015. 거점 내부 화면의 배치 고스트와 마우스 배치 입력. 핫바가 고른 대상(Select) 또는 드래그 중인 대상(BeginDrag)의
    /// 반투명 GhostPiece 프리팹(InteriorCatalog)을 마우스 아래 가장 가까운 칸 앵커(IInteriorAnchors)에 스냅해 보이고, 놓을 수 있으면 초록·없으면 붉게 칠한다.
    /// 좌클릭 = 배치, 우클릭·Esc = 선택/드래그 취소, 선택이 없을 때 우클릭 = 놓인 아이템 회수. UI 위의 클릭과 닫힌 내부 화면에서는 아무것도 하지 않는다.
    /// 게임 상태는 IItemPlacement, IFacilityBuilder, IInteriorFacilityCells가 소유하며 이 뷰는 명령만 보낸다.
    /// </summary>
    public sealed class PlacementGhost : MonoBehaviour
    {
        private static PlacementGhost _instance;
        /// <summary>현재 고스트(내부 화면 서비스가 없는 씬에서는 null).</summary>
        public static PlacementGhost Instance => _instance;

        /// <summary>HUD가 등록하는 추가 UI 판정(화면 좌표 → 그 위에 자기 패널이 있는지). EventSystem 판정과 함께 쓴다.</summary>
        public static readonly List<Func<Vector2, bool>> UiHitTests = new List<Func<Vector2, bool>>();

        private IViewMode _view;
        private IInteriorAnchors _anchors;
        private IMapAnchors _map;
        private IHubInterior _interior;
        private IItemPlacement _placement;
        private IFacilityBuilder _facilities;
        private IInteriorFacilityCells _facilityCells;
        private InteriorCatalog _catalog;
        private ItemSpriteSet _sprites;
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        private PlacementEntry _selected;
        private PlacementEntry _dragEntry;
        private bool _dragging;
        private int _parkedCell = -1;
        private GameObject _ghost;
        private SpriteRenderer _ghostRenderer;
        private PlacementEntry _ghostShows;

        /// <summary>선택이 바뀔 때(해제는 IsValid가 false인 값).</summary>
        public event Action<PlacementEntry> SelectionChanged;
        /// <summary>배치·회수 결과 안내(한국어 문자열 키, 대상 이름에 쓸 id).</summary>
        public event Action<string, PlacementEntry> Notice;

        /// <summary>현재 선택(없으면 IsValid false).</summary>
        public PlacementEntry Selected => _selected;
        /// <summary>핫바·팔레트에서 끌고 있는지.</summary>
        public bool IsDragging => _dragging;
        /// <summary>드래그 중인 대상(드래그 중이 아니면 IsValid false).</summary>
        public PlacementEntry DragEntry => _dragging ? _dragEntry : default;
        /// <summary>마지막 프레임에 고스트가 스냅한 칸(없으면 -1).</summary>
        public int HoverCell { get; private set; } = -1;
        /// <summary>마지막 프레임의 고스트가 초록(배치 가능)이었는지.</summary>
        public bool HoverValid { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= OnReady; _instance = null; UiHitTests.Clear(); }

        /// <summary>MainRoot의 GameServices.Reset()(SubsystemRegistration) 뒤에 Ready를 구독한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { GameServices.Ready -= OnReady; GameServices.Ready += OnReady; }

        private static void OnReady() => Ensure();

        /// <summary>내부 화면 서비스가 등록되어 있으면 고스트를 한 번 만들어 돌려준다. 없으면 null(다른 스토리 데모 씬).</summary>
        public static PlacementGhost Ensure()
        {
            if (_instance != null) return _instance;
            if (GameServices.Get<IViewMode>() == null || GameServices.Get<IInteriorAnchors>() == null
                || GameServices.Get<IHubInterior>() == null || GameServices.Get<IMapAnchors>() == null) return null;
            _instance = new GameObject(nameof(PlacementGhost)).AddComponent<PlacementGhost>();
            _instance.Initialize();
            return _instance;
        }

        private void Initialize()
        {
            _view = GameServices.Get<IViewMode>();
            _anchors = GameServices.Get<IInteriorAnchors>();
            _map = GameServices.Get<IMapAnchors>();
            _interior = GameServices.Get<IHubInterior>();
            _placement = GameServices.Get<IItemPlacement>();
            _facilities = GameServices.Get<IFacilityBuilder>();
            _facilityCells = GameServices.Get<IInteriorFacilityCells>();
            _sprites = ItemSpriteSet.Load();
            foreach (HubInteriorRoot root in UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (root.Catalog != null) { _catalog = root.Catalog; break; }
            if (_catalog == null || _catalog.GhostPiecePrefab == null)
                UnityEngine.Debug.LogError("PlacementGhost: InteriorCatalog에 GhostPiece 프리팹이 없습니다. IronRing.Presentation.Interior.Editor.Story012Build.Generate를 실행하세요.");
            _view.InteriorChanged += OnInteriorChanged;
        }

        /// <summary>entry를 선택한다(핫바 슬롯·키 1~9). 잘못된 값이면 선택을 지운다.</summary>
        public void Select(PlacementEntry entry)
        {
            if (!entry.IsValid) { ClearSelection(); return; }
            if (_selected.Equals(entry)) return;
            _selected = entry;
            SelectionChanged?.Invoke(_selected);
        }

        /// <summary>선택을 지운다(Esc·우클릭·화면 닫힘).</summary>
        public void ClearSelection()
        {
            if (!_selected.IsValid) return;
            _selected = default;
            SelectionChanged?.Invoke(_selected);
        }

        /// <summary>핫바·팔레트 항목 드래그를 시작한다. 드래그 동안 같은 고스트가 entry로 보인다.</summary>
        public void BeginDrag(PlacementEntry entry)
        {
            if (!entry.IsValid || _view == null || !_view.InteriorOpen) return;
            _dragEntry = entry;
            _dragging = true;
        }

        /// <summary>드래그를 끝낸다. place면 지금 스냅한 칸이 초록이고 포인터가 UI 위가 아닐 때 놓는다. 드래그 중이 아니면 false.</summary>
        public bool EndDrag(bool place)
        {
            if (!_dragging) return false;
            PlacementEntry entry = _dragEntry;
            _dragging = false;
            _dragEntry = default;
            if (!place) return false;
            Vector2 pointer = Pointer();
            if (_parkedCell < 0 && IsPointerOverUi(pointer)) return false;
            int cell = _parkedCell >= 0 ? _parkedCell : NearestCell(pointer, out _);
            return cell >= 0 && TryPlace(_view.InteriorHubId, cell, entry);
        }

        /// <summary>데모·캡처용: 마우스 대신 cellIndex 칸 위에 고스트를 고정한다(마우스 입력은 무시). 음수면 해제.</summary>
        public void Park(int cellIndex) => _parkedCell = cellIndex;

        /// <summary>entry를 hubId의 cellIndex에 놓을 수 있는지. 아이템은 IItemPlacement.CanPlace, 시설은 빈 칸(또는 부지) + IFacilityBuilder.CanBuild.</summary>
        public bool CanPlace(string hubId, int cellIndex, PlacementEntry entry)
        {
            if (!entry.IsValid || hubId == null) return false;
            if (entry.Kind == PlacementKind.Item) return _placement != null && _placement.CanPlace(hubId, cellIndex, entry.Id);
            IInteriorCell cell = Cell(hubId, cellIndex);
            if (cell == null || _facilities == null || _facilityCells == null) return false;
            return (cell.State == InteriorCellState.Empty || cell.State == InteriorCellState.FacilityPlot) && _facilities.CanBuild(hubId, entry.Id);
        }

        /// <summary>
        /// entry를 놓는다. 아이템: IItemPlacement.TryPlace. 시설: 칸을 IInteriorFacilityCells.TryBuildFacility로 먼저 FacilityBuilt로 바꾼 뒤
        /// IFacilityBuilder.TryBuild로 거점 기록(한도·폐허 규칙)을 남긴다. 칸 쓰기가 거부되면 아무것도 소모하지 않는다.
        /// </summary>
        public bool TryPlace(string hubId, int cellIndex, PlacementEntry entry)
        {
            if (!CanPlace(hubId, cellIndex, entry)) { Notice?.Invoke("placement_refused", entry); return false; }
            bool ok;
            if (entry.Kind == PlacementKind.Item) ok = _placement.TryPlace(hubId, cellIndex, entry.Id);
            else
            {
                ok = _facilityCells.TryBuildFacility(hubId, cellIndex, entry.Id);
                if (!ok) UnityEngine.Debug.LogWarning($"PlacementGhost: {hubId} 칸 {cellIndex}({Cell(hubId, cellIndex)?.State})에 시설 {entry.Id}를 칸 상태로 기록하지 못했습니다(IInteriorFacilityCells가 거부).");
                else if (!_facilities.TryBuild(hubId, entry.Id)) UnityEngine.Debug.LogWarning($"PlacementGhost: {hubId} 시설 {entry.Id} 건설이 칸 기록 뒤 거부되었습니다.");
            }
            Notice?.Invoke(ok ? "placement_placed" : "placement_refused", entry);
            return ok;
        }

        /// <summary>hubId의 cellIndex에 놓인 아이템을 회수한다(IItemPlacement.TryPickUp).</summary>
        public bool TryPickUp(string hubId, int cellIndex)
        {
            IInteriorCell cell = Cell(hubId, cellIndex);
            if (cell == null || cell.State != InteriorCellState.ItemPlaced || _placement == null) return false;
            var entry = new PlacementEntry(PlacementKind.Item, cell.ItemId);
            bool ok = _placement.TryPickUp(hubId, cellIndex);
            Notice?.Invoke(ok ? "placement_picked" : "placement_refused", entry);
            return ok;
        }

        /// <summary>entry의 아이콘·고스트 스프라이트(아이템은 ItemSpriteSet, 시설은 InteriorCatalog 시설 아이콘). 없으면 null.</summary>
        public Sprite IconFor(PlacementEntry entry)
        {
            if (!entry.IsValid) return null;
            if (entry.Kind == PlacementKind.Facility) return _catalog != null ? _catalog.FacilityIcon(entry.Id) : null;
            return _sprites != null ? _sprites.Get(entry.Id) : null;
        }

        /// <summary>hubId의 cellIndex 칸 중심 화면 좌표(Input System 기준, 왼쪽 아래 원점). 없으면 null.</summary>
        public Vector2? CellScreenPosition(string hubId, int cellIndex)
        {
            Transform anchor = _anchors.CellAnchor(hubId, cellIndex);
            Camera camera = _map.MapCamera;
            if (anchor == null || camera == null) return null;
            return camera.WorldToScreenPoint(anchor.position);
        }

        void Update()
        {
            if (_view == null) return;
            if (!_view.InteriorOpen) { SetGhost(default, Vector3.zero, false); return; }
            string hub = _view.InteriorHubId;
            PlacementEntry active = _dragging ? _dragEntry : _selected;
            Vector2 pointer = Pointer();
            bool parked = _parkedCell >= 0;
            bool overUi = !parked && IsPointerOverUi(pointer);
            Vector3 world;
            int cell;
            if (parked)
            {
                cell = _parkedCell;
                Transform parkedAnchor = _anchors.CellAnchor(hub, cell);
                world = parkedAnchor != null ? parkedAnchor.position : Vector3.zero;
            }
            else
            {
                cell = NearestCell(pointer, out world);
                if (cell >= 0) world = _anchors.CellAnchor(hub, cell).position;
            }
            HoverCell = cell;
            HoverValid = active.IsValid && cell >= 0 && CanPlace(hub, cell, active);
            SetGhost(active, world, active.IsValid && (!overUi || parked));
            if (!parked) HandleInput(hub, active, cell, overUi);
        }

        /// <summary>마우스·키보드 입력: Esc/우클릭 취소, 선택 없는 우클릭 회수, 좌클릭 배치, 드래그 놓기(UI 밖에서 버튼을 뗐을 때).</summary>
        private void HandleInput(string hub, PlacementEntry active, int cell, bool overUi)
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool right = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if (_dragging)
            {
                if (escape || right) { EndDrag(false); return; }
                if (mouse != null && mouse.leftButton.wasReleasedThisFrame) EndDrag(true);
                return;
            }
            if (escape && _selected.IsValid) { ClearSelection(); return; }
            if (right)
            {
                if (_selected.IsValid) ClearSelection();
                else if (!overUi && cell >= 0) TryPickUp(hub, cell);
                return;
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && active.IsValid && !overUi && cell >= 0) TryPlace(hub, cell, active);
        }

        /// <summary>entry의 고스트를 world에 보이거나(visible) 숨긴다. 스프라이트는 대상이 바뀔 때만 바꾸고 색은 HoverValid로 정한다.</summary>
        private void SetGhost(PlacementEntry entry, Vector3 world, bool visible)
        {
            if (!visible || !entry.IsValid)
            {
                if (_ghost != null && _ghost.activeSelf) _ghost.SetActive(false);
                return;
            }
            if (_ghost == null)
            {
                if (_catalog == null || _catalog.GhostPiecePrefab == null) return;
                _ghost = Instantiate(_catalog.GhostPiecePrefab, transform, false);
                _ghost.name = "GhostPiece";
                _ghostRenderer = _ghost.GetComponentInChildren<SpriteRenderer>(true);
                _ghostShows = default;
            }
            if (!_ghostShows.Equals(entry) && _ghostRenderer != null)
            {
                _ghostRenderer.sprite = IconFor(entry);
                _ghostShows = entry;
            }
            if (_ghostRenderer != null) _ghostRenderer.color = HoverValid ? _catalog.GhostValidColor : _catalog.GhostInvalidColor;
            _ghost.transform.position = new Vector3(world.x, world.y, 0);
            if (!_ghost.activeSelf) _ghost.SetActive(true);
        }

        /// <summary>화면 좌표 pointer 아래의 월드 위치와 스냅 반경 안에서 가장 가까운 열린 거점의 칸(없으면 -1).</summary>
        private int NearestCell(Vector2 pointer, out Vector3 world)
        {
            world = Vector3.zero;
            Camera camera = _map.MapCamera;
            string hub = _view.InteriorHubId;
            if (camera == null || hub == null) return -1;
            world = camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -camera.transform.position.z));
            world.z = 0;
            float tile = _catalog != null ? _catalog.TileSize : 1f;
            float radius = (_catalog != null ? _catalog.SnapRadius : 0.75f) * tile;
            float best = radius * radius;
            int found = -1;
            int count = _interior.Cells(hub).Count;
            for (int i = 0; i < count; i++)
            {
                Transform anchor = _anchors.CellAnchor(hub, i);
                if (anchor == null) continue;
                Vector2 delta = (Vector2)(anchor.position - world);
                float distance = delta.sqrMagnitude;
                if (distance <= best) { best = distance; found = i; }
            }
            return found;
        }

        /// <summary>pointer가 HUD 위인지: EventSystem 레이캐스트(UI Toolkit 패널 포함)와 HUD가 등록한 패널 판정.</summary>
        private bool IsPointerOverUi(Vector2 pointer)
        {
            foreach (Func<Vector2, bool> test in UiHitTests) if (test != null && test(pointer)) return true;
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _uiHits);
            return _uiHits.Count > 0;
        }

        private static Vector2 Pointer() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        private IInteriorCell Cell(string hubId, int cellIndex)
        {
            if (hubId == null) return null;
            IReadOnlyList<IInteriorCell> cells;
            try { cells = _interior.Cells(hubId); }
            catch (ArgumentException) { return null; }
            return cells != null && cellIndex >= 0 && cellIndex < cells.Count ? cells[cellIndex] : null;
        }

        /// <summary>내부 화면이 닫히면 선택·드래그·고정을 모두 지운다.</summary>
        private void OnInteriorChanged(bool open)
        {
            if (open) return;
            EndDrag(false);
            _parkedCell = -1;
            ClearSelection();
        }

        void OnDestroy()
        {
            GameServices.Ready -= OnReady;
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_instance == this) _instance = null;
        }
    }
}
