using System;
using System.Collections.Generic;
using System.Linq;

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
    /// 반투명 GhostPiece 프리팹(InteriorCatalog)을 마우스 아래 Grid 칸 앵커에 스냅해 보이고, 놓을 수 있으면 초록·없으면 붉게 칠한다.
        /// 완성 서식지 좌클릭 = 선택, 좌클릭 = 배치, 우클릭 = 선택 취소 또는 타일 한 칸 회수,
    /// Ctrl+Z = 최근 배치·회수·철거 되돌리기(PlacementHistory, 스토리 031). UI 위의 클릭과 닫힌 내부 화면에서는 아무것도 하지 않는다.
    /// 게임 상태는 IItemPlacement가 소유하며 이 뷰는 명령만 보낸다.
    /// </summary>
    public sealed class PlacementGhost : MonoBehaviour
    {
        private static PlacementGhost _instance;
        /// <summary>현재 고스트(내부 화면 서비스가 없는 씬에서는 null).</summary>
        public static PlacementGhost Instance => _instance;

        private IViewMode _view;
        private IInteriorAnchors _anchors;
        private IMapAnchors _map;
        private IHubInterior _interior;
        private IItemPlacement _placement;
        private IHabitatClusters _clusters;
        private PlacementHistory _history;
        private InteriorCatalog _catalog;
        private ItemSpriteSet _sprites;

        private PlacementEntry _selected;
        private PlacementEntry _dragEntry;
        private bool _dragging;
        private int _parkedCell = -1;
        private GameObject _ghost;
        private SpriteRenderer _ghostRenderer;
        private PlacementEntry _ghostShows;
        private IPlacementSurface _surface;
        private Vector3 _ghostBaseScale = Vector3.one;
        private int _ghostBaseOrder;
        private HabitatCluster _movingCluster;
        private int _movingAnchor;
        private int _movingTurns;
        private readonly List<GameObject> _movingGhosts = new List<GameObject>();
        private List<int> _movingDestination;

        /// <summary>스토리 034. 거점 칸 대신 쓰는 배치 면(열차 장비 모드). null이면 거점 칸.</summary>
        public IPlacementSurface Surface => _surface;

        /// <summary>스토리 034. 배치 면을 바꾼다(드래그·선택은 지운다). null이면 거점 칸으로 돌아간다.</summary>
        public void SetSurface(IPlacementSurface surface)
        {
            if (ReferenceEquals(_surface, surface)) return;
            EndDrag(false);
            ClearSelection();
            _surface = surface;
            _ghostShows = default;
        }

        /// <summary>선택이 바뀔 때(해제는 IsValid가 false인 값).</summary>
        public event Action<PlacementEntry> SelectionChanged;
        /// <summary>배치·회수 결과 안내(한국어 문자열 키, 대상 이름에 쓸 id).</summary>
        public event Action<string, PlacementEntry> Notice;
        /// <summary>스토리 031. 건물을 철거했을 때(hubId, 서식지 종류 id).</summary>
        public event Action<string, string> Demolished;
        /// <summary>스토리 031. Ctrl+Z로 기록 하나를 되돌렸을 때.</summary>
        public event Action<PlacementAction> Undone;

        /// <summary>스토리 031. 되돌림 토스트를 띄울지(InteriorCatalog._undoToast).</summary>
        public bool UndoToastEnabled => _catalog == null || _catalog.UndoToast;
        /// <summary>스토리 031. 남은 되돌리기 기록 수.</summary>
        public int UndoCount => _history != null ? _history.Count : 0;

        /// <summary>현재 선택(없으면 IsValid false).</summary>
        public PlacementEntry Selected => _selected;
        /// <summary>핫바·팔레트에서 끌고 있는지.</summary>
        public bool IsDragging => _dragging;
        /// <summary>완성 서식지를 이동 중인지 반환한다.</summary>
        public bool IsMovingHabitat => _movingCluster != null;
        /// <summary>드래그 중인 대상(드래그 중이 아니면 IsValid false).</summary>
        public PlacementEntry DragEntry => _dragging ? _dragEntry : default;
        /// <summary>마지막 프레임에 고스트가 스냅한 칸(없으면 -1).</summary>
        public int HoverCell { get; private set; } = -1;
        /// <summary>마지막 프레임의 고스트가 초록(배치 가능)이었는지.</summary>
        public bool HoverValid { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= OnReady; _instance = null; }

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
            _clusters = GameServices.Get<IHabitatClusters>();
            _sprites = ItemSpriteSet.Load();
            foreach (HubInteriorRoot root in UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (root.Catalog != null) { _catalog = root.Catalog; break; }
            if (_catalog == null || _catalog.GhostPiecePrefab == null)
                UnityEngine.Debug.LogError("PlacementGhost: InteriorCatalog에 GhostPiece 프리팹이 없습니다. IronRing.Presentation.Interior.Editor.Story012Build.Generate를 실행하세요.");
            if (_placement != null) _history = new PlacementHistory(_placement, _interior, _catalog != null ? _catalog.UndoDepth : 5);
            _view.InteriorChanged += OnInteriorChanged;
        }

        /// <summary>entry를 선택한다(핫바 슬롯·키 1~9). 잘못된 값이면 선택을 지운다.</summary>
        public void Select(PlacementEntry entry)
        {
            CancelHabitatMove();
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
            CancelHabitatMove();
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
            int cell = _parkedCell >= 0 ? _parkedCell : _surface != null ? _surface.Nearest(PointerWorld(pointer), entry, out _) : NearestCell(pointer, out _);
            return cell >= 0 && TryPlace(_view.InteriorHubId, cell, entry);
        }

        /// <summary>데모·캡처용: 마우스 대신 cellIndex 칸 위에 고스트를 고정한다(마우스 입력은 무시). 음수면 해제.</summary>
        public void Park(int cellIndex) => _parkedCell = cellIndex;

        /// <summary>itemId가 땅이면 개간 비용을, 다른 재료면 재고를 확인하여 배치 가능 여부를 반환한다.</summary>
        public bool Affordable(string itemId) => itemId == "tile_cobble"
            ? GameServices.Get<IPlacementCost>()?.CanAfford(itemId) ?? true
            : (GameServices.Get<ITileStock>()?.CountOf(itemId) ?? 0) > 0;

        /// <summary>hubId와 cellIndex에 entry를 놓을 수 있는지 재고와 배치 조건을 확인해 반환한다.</summary>
        public bool CanPlace(string hubId, int cellIndex, PlacementEntry entry)
        {
            if (!entry.IsValid || hubId == null) return false;
            if (entry.Kind == PlacementKind.Item) return _placement != null && Affordable(entry.Id) && _placement.CanPlace(hubId, cellIndex, entry.Id);
            return false;
        }

        /// <summary>
        /// entry를 놓는다. 아이템: IItemPlacement.TryPlace.
        /// </summary>
        public bool TryPlace(string hubId, int cellIndex, PlacementEntry entry)
        {
            if (entry.Kind == PlacementKind.Item && !Affordable(entry.Id))
            {
                Notice?.Invoke("placement_refused", entry);
                return false;
            }
            if (!CanPlace(hubId, cellIndex, entry)) { Notice?.Invoke("placement_refused", entry); return false; }
            bool ok;
            if (entry.Kind == PlacementKind.Item)
            {
                ok = _placement.TryPlace(hubId, cellIndex, entry.Id);
                if (ok) Record(PlacementActionKind.Place, hubId, null, new[] { new KeyValuePair<int, string>(cellIndex, entry.Id) });
            }
            else ok = false;
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
            if (ok) Record(PlacementActionKind.PickUp, hubId, null, new[] { new KeyValuePair<int, string>(cellIndex, entry.Id) });
            Notice?.Invoke(ok ? "placement_picked" : "placement_refused", entry);
            return ok;
        }

        /// <summary>스토리 031. hubId의 cellIndex가 속한 완성 서식지 묶음(건물). 없으면 null.</summary>
        public HabitatCluster BuildingAt(string hubId, int cellIndex)
        {
            if (_clusters == null || hubId == null || cellIndex < 0) return null;
            foreach (HabitatCluster cluster in _clusters.CompletedAt(hubId) ?? Array.Empty<HabitatCluster>())
                if (cluster?.Cells != null && cluster.Cells.Contains(cellIndex)) return cluster;
            return null;
        }

        /// <summary>
        /// 스토리 031. cellIndex가 완성된 건물 칸이면 그 묶음의 타일을 모두 IItemPlacement.TryPickUp으로 회수한다(서식지가 깨지며
        /// BuildingView가 무너짐 연출, INpcHomes가 집을 거둬 NPC는 열차 옆 자리로). 하나라도 회수하면 기록·Demolished 후 true.
        /// </summary>
        public bool TryDemolish(string hubId, int cellIndex)
        {
            HabitatCluster cluster = BuildingAt(hubId, cellIndex);
            if (cluster == null || _placement == null) return false;
            var removed = new List<KeyValuePair<int, string>>();
            foreach (int cell in cluster.Cells.ToList())
            {
                IInteriorCell state = Cell(hubId, cell);
                if (state == null || state.State != InteriorCellState.ItemPlaced) continue;
                string item = state.ItemId;
                if (_placement.TryPickUp(hubId, cell)) removed.Add(new KeyValuePair<int, string>(cell, item));
            }
            UnityEngine.Debug.Log($"DEMOLISH: {cluster.TypeId} {hubId} cells {string.Join(",", removed.Select(t => t.Key + "=" + t.Value))} (묶음 {cluster.Cells.Count}칸)");
            if (removed.Count == 0) { Notice?.Invoke("placement_refused", default); return false; }
            Record(PlacementActionKind.Demolish, hubId, cluster.TypeId, removed);
            Demolished?.Invoke(hubId, cluster.TypeId);
            return true;
        }

        /// <summary>스토리 031. 최근 기록 하나를 되돌린다(Ctrl+Z). 되돌린 것이 없으면 false.</summary>
        public bool TryUndo()
        {
            if (_history == null || !_history.TryUndo(out PlacementAction action)) return false;
            Undone?.Invoke(action);
            return true;
        }

        private void Record(PlacementActionKind kind, string hubId, string typeId, IReadOnlyList<KeyValuePair<int, string>> tiles)
        {
            _history?.Record(new PlacementAction { Kind = kind, HubId = hubId, TypeId = typeId, Tiles = tiles });
        }

        /// <summary>entry의 아이콘·고스트 스프라이트(아이템은 ItemSpriteSet, 시설은 InteriorCatalog 시설 아이콘). 없으면 null.</summary>
        public Sprite IconFor(PlacementEntry entry)
        {
            if (!entry.IsValid) return null;
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
                Transform parkedAnchor = _surface != null ? null : _anchors.CellAnchor(hub, cell);
                world = _surface != null ? _surface.Anchor(cell, active) : parkedAnchor != null ? parkedAnchor.position : Vector3.zero;
            }
            else if (_surface != null) cell = _surface.Nearest(PointerWorld(pointer), active, out world);
            else
            {
                cell = NearestCell(pointer, out world);
                if (cell >= 0) world = _anchors.CellAnchor(hub, cell).position;
            }
            HoverCell = cell;
            if (_movingCluster != null)
            {
                SetGhost(default, Vector3.zero, false);
                UpdateHabitatMove(hub, cell, overUi);
                if (!parked) HandleHabitatInput(hub, cell, overUi);
                return;
            }
            HoverValid = active.IsValid && cell >= 0 && CanPlace(hub, cell, active);
            SetGhost(active, world, active.IsValid && (!overUi || parked));
            if (!parked) HandleInput(hub, active, cell, overUi);
        }

        /// <summary>hub의 cell과 UI 겹침 상태를 받아 서식지 선택, 배치, 회수와 취소 입력을 처리한다.</summary>
        private void HandleInput(string hub, PlacementEntry active, int cell, bool overUi)
        {
            // NPC를 끄는 동안의 우클릭은 끌기 취소이므로 회수·철거로 처리하지 않는다.
            if (IronRing.Presentation.HubPhase.NpcDragController.IsInputHeld) return;
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
            if (keyboard != null && keyboard.zKey.wasPressedThisFrame && keyboard.ctrlKey.isPressed && !IronRing.Hud.GameMenuScreen.IsOpen && !IronRing.Hud.Onboarding.DevSituationsScreen.IsOpen) { if (_surface != null) _surface.TryUndo(); else TryUndo(); return; } // 스펙 003: 게임 메뉴·개발자 메뉴가 열려 있으면 되돌리기 막음
            bool left = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (left && !overUi && _surface == null && TrySelectHabitat(hub, cell, PointerWorld(Pointer()))) return;
            if (right)
            {
                if (overUi) return;
                if (_selected.IsValid) { ClearSelection(); return; }
                if (cell >= 0)
                {
                    if (_surface != null) _surface.TryRemove(cell);
                    else TryPickUp(hub, cell);
                }
                return;
            }
            if (left && active.IsValid && !overUi && cell >= 0) TryPlace(hub, cell, active);
        }

        /// <summary>
        /// hub의 클릭 칸 또는 이름 중심 world에서 완성 서식지를 찾아 이동 대상으로 선택한다.
        /// 선택하면 기존 재료 선택을 해제하고 true를 반환한다.
        /// </summary>
        private bool TrySelectHabitat(string hub, int cell, Vector3 world)
        {
            HabitatCluster cluster = BuildingAt(hub, cell);
            int anchorCell = cell;
            IInteriorCell clicked = Cell(hub, cell);
            if (cluster == null && (clicked == null || clicked.State != InteriorCellState.ItemPlaced) && _clusters != null)
            {
                float radius = (_catalog != null ? _catalog.TileSize : 1f) * 0.5f;
                float nearestName = radius * radius;
                foreach (HabitatCluster candidate in _clusters.CompletedAt(hub))
                {
                    if (candidate.Cells == null || candidate.Cells.Count == 0) continue;
                    Vector3 center = Vector3.zero;
                    int count = 0;
                    int closest = -1;
                    float closestDistance = float.MaxValue;
                    foreach (int index in candidate.Cells)
                    {
                        Transform anchor = _anchors.CellAnchor(hub, index);
                        if (anchor == null) continue;
                        center += anchor.position;
                        count++;
                        float distance = ((Vector2)(anchor.position - world)).sqrMagnitude;
                        if (distance < closestDistance) { closestDistance = distance; closest = index; }
                    }
                    if (count == 0) continue;
                    float nameDistance = ((Vector2)(center / count - world)).sqrMagnitude;
                    if (nameDistance <= nearestName) { nearestName = nameDistance; cluster = candidate; anchorCell = closest; }
                }
            }
            if (cluster == null) return false;
            ClearSelection();
            _movingCluster = cluster;
            _movingAnchor = anchorCell;
            _movingTurns = 0;
            IronRing.Hud.Ui.ToastScreen.Show(IronRing.Localization.Strings.Get("habitat_move_controls"), 4f);
            return true;
        }

        /// <summary>선택한 묶음의 기준 칸을 cell로 옮기고 회전한 후보와 배치 가능 색을 표시한다.</summary>
        private void UpdateHabitatMove(string hub, int cell, bool overUi)
        {
            _movingDestination = HabitatDestination(hub, cell);
            var placement = _placement as IronRing.Items.ItemPlacement;
            bool valid = _movingDestination != null && placement != null && placement.CanMoveHabitat(hub, _movingCluster, _movingDestination);
            HoverValid = valid;
            while (_movingGhosts.Count < _movingCluster.Cells.Count && _catalog != null && _catalog.ClusterFramePrefab != null)
            {
                _movingGhosts.Add(CreateHabitatGhost());
            }
            float tile = _catalog != null ? _catalog.TileSize : 1f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, -90f * _movingTurns);
            Vector3 origin = _anchors.CellAnchor(hub, _movingAnchor).position;
            var positions = new List<Vector2Int>();
            var occupied = new HashSet<Vector2Int>();
            foreach (int index in _movingCluster.Cells)
            {
                Vector3 offset = rotation * (_anchors.CellAnchor(hub, index).position - origin);
                var position = new Vector2Int(Mathf.RoundToInt(offset.x / tile), Mathf.RoundToInt(offset.y / tile));
                positions.Add(position);
                occupied.Add(position);
            }
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            for (int i = 0; i < _movingGhosts.Count; i++)
            {
                GameObject ghost = _movingGhosts[i];
                bool show = !overUi && cell >= 0;
                ghost.SetActive(show);
                if (!show) continue;
                if (_movingDestination != null) ghost.transform.position = _anchors.CellAnchor(hub, _movingDestination[i]).position;
                else
                {
                    Vector3 offset = _anchors.CellAnchor(hub, _movingCluster.Cells[i]).position - _anchors.CellAnchor(hub, _movingAnchor).position;
                    ghost.transform.position = _anchors.CellAnchor(hub, cell).position + rotation * offset;
                }
                Color color = valid ? _catalog.GhostValidColor : _catalog.GhostInvalidColor;
                for (int edge = 0; edge < directions.Length; edge++)
                {
                    LineRenderer line = ghost.transform.GetChild(edge).GetComponent<LineRenderer>();
                    line.enabled = !occupied.Contains(positions[i] + directions[edge]);
                    line.startColor = color;
                    line.endColor = color;
                }
            }
        }

        /// <summary>카탈로그의 프레임 재질과 타일 크기로 서식지 고스트의 네 외곽선을 만들어 반환한다.</summary>
        private GameObject CreateHabitatGhost()
        {
            GameObject ghost = new GameObject("HabitatGhostCell");
            ghost.transform.SetParent(transform, false);
            SpriteRenderer source = _catalog.ClusterFramePrefab.GetComponentInChildren<SpriteRenderer>(true);
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            float half = _catalog.TileSize * 0.5f;
            float depth = -(_catalog.BuildingDepth + 0.04f);
            foreach (Vector2Int direction in directions)
            {
                GameObject edge = new GameObject("Edge");
                edge.transform.SetParent(ghost.transform, false);
                LineRenderer line = edge.AddComponent<LineRenderer>();
                if (source != null) line.sharedMaterial = source.sharedMaterial;
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.startWidth = _catalog.TileSize * 3f / 32f;
                line.endWidth = line.startWidth;
                line.sortingOrder = _catalog.BuildingSignOrder + 3;
                line.SetPosition(0, direction.x == 0 ? new Vector3(-half, direction.y * half, depth) : new Vector3(direction.x * half, -half, depth));
                line.SetPosition(1, direction.x == 0 ? new Vector3(half, direction.y * half, depth) : new Vector3(direction.x * half, half, depth));
            }
            return ghost;
        }

        /// <summary>hub와 기준 cell을 사용해 회전된 목적지 인덱스 목록을 반환하며 범위 밖이면 null을 반환한다.</summary>
        private List<int> HabitatDestination(string hub, int cell)
        {
            IInteriorCell origin = Cell(hub, _movingAnchor), target = Cell(hub, cell);
            if (origin == null || target == null) return null;
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(hub);
            int upperRows = cells.Where(c => c.IsUpper).Select(c => c.Row + 1).DefaultIfEmpty(0).Max();
            int originRow = origin.Row + (origin.IsUpper ? 0 : upperRows);
            int targetRow = target.Row + (target.IsUpper ? 0 : upperRows);
            var result = new List<int>();
            foreach (int index in _movingCluster.Cells)
            {
                IInteriorCell part = Cell(hub, index);
                int row = part.Row + (part.IsUpper ? 0 : upperRows) - originRow, column = part.Column - origin.Column;
                for (int turn = 0; turn < _movingTurns; turn++) { int previous = row; row = column; column = -previous; }
                IInteriorCell destination = cells.FirstOrDefault(c => c.Row + (c.IsUpper ? 0 : upperRows) == targetRow + row && c.Column == target.Column + column);
                if (destination == null) return null;
                result.Add(destination.Index);
            }
            return result;
        }

        /// <summary>선택 중 회전, 확정, 취소, 제거 입력을 처리하고 이동 기록을 보관한다.</summary>
        private void HandleHabitatInput(string hub, int cell, bool overUi)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { CancelHabitatMove(); return; }
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && !overUi) { CancelHabitatMove(); return; }
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) { _movingTurns = (_movingTurns + 1) % 4; UpdateHabitatMove(hub, cell, overUi); }
            if (keyboard != null && keyboard.deleteKey.wasPressedThisFrame) { int origin = _movingAnchor; CancelHabitatMove(); TryPickUp(hub, origin); return; }
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || overUi || !HoverValid || _movingDestination == null) return;
            var placement = _placement as IronRing.Items.ItemPlacement;
            var before = _movingCluster.Cells.Select(index => new KeyValuePair<int, string>(index, Cell(hub, index).ItemId)).ToArray();
            var after = _movingDestination.Select((index, i) => new KeyValuePair<int, string>(index, before[i].Value)).ToArray();
            string type = _movingCluster.TypeId;
            if (placement != null && placement.TryMoveHabitat(hub, _movingCluster, _movingDestination))
            {
                _history?.Record(new PlacementAction { Kind = PlacementActionKind.Move, HubId = hub, TypeId = type, Tiles = before, Destination = after });
                CancelHabitatMove();
            }
        }

        /// <summary>현재 선택된 묶음과 미리보기 오브젝트를 지워 원래 배치를 유지한다.</summary>
        private void CancelHabitatMove()
        {
            _movingCluster = null;
            _movingDestination = null;
            foreach (GameObject ghost in _movingGhosts) if (ghost != null) Destroy(ghost);
            _movingGhosts.Clear();
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
                _ghostBaseScale = _ghost.transform.localScale;
                _ghostBaseOrder = _ghostRenderer != null ? _ghostRenderer.sortingOrder : 0;
            }
            if (!_ghostShows.Equals(entry) && _ghostRenderer != null)
            {
                _ghostRenderer.sprite = IconFor(entry);
                _ghostShows = entry;
            }
            if (_ghostRenderer != null) _ghostRenderer.color = HoverValid ? _catalog.GhostValidColor : _catalog.GhostInvalidColor;
            bool onSurface = _surface != null;
            _ghost.transform.localScale = onSurface ? _ghostBaseScale * _surface.GhostScale(entry) : _ghostBaseScale;
            if (_ghostRenderer != null) _ghostRenderer.sortingOrder = onSurface ? _surface.GhostOrder : _ghostBaseOrder;
            _ghost.transform.position = new Vector3(world.x, world.y, 0);
            if (!_ghost.activeSelf) _ghost.SetActive(true);
        }

        /// <summary>스펙 005. 화면 좌표 pointer 아래의 칸 번호를 반환하고 world에 월드 좌표를 담는다. 칸이 없으면 -1을 반환한다.</summary>
        public int CellAt(Vector2 pointer, out Vector3 world) => NearestCell(pointer, out world);

        /// <summary>화면 좌표 pointer 아래의 월드 위치와 스냅 반경 안에서 가장 가까운 열린 거점의 칸(없으면 -1).</summary>
        private int NearestCell(Vector2 pointer, out Vector3 world)
        {
            world = Vector3.zero;
            Camera camera = _map.MapCamera;
            string hub = _view.InteriorHubId;
            if (camera == null || hub == null) return -1;
            world = camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -camera.transform.position.z));
            world.z = 0;
            return (_anchors as HubInteriorView)?.CellAtWorld(hub, world) ?? -1;
        }

        /// <summary>pointer가 HUD(UGUI MainCanvas) 위인지: EventSystem.IsPointerOverGameObject 판정만 쓴다(스토리 025에서 옛 등록 판정 제거).</summary>
        private bool IsPointerOverUi(Vector2 pointer)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>화면 좌표 pointer의 월드 위치(z = 0). 카메라가 없으면 원점.</summary>
        private Vector3 PointerWorld(Vector2 pointer)
        {
            Camera camera = _map.MapCamera;
            if (camera == null) return Vector3.zero;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -camera.transform.position.z));
            world.z = 0;
            return world;
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
            CancelHabitatMove();
            _surface = null;
            EndDrag(false);
            _parkedCell = -1;
            ClearSelection();
            _history?.Clear();
        }

        void OnDestroy()
        {
            GameServices.Ready -= OnReady;
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_instance == this) _instance = null;
        }
    }
}
