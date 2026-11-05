using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Presentation.Interior;

namespace IronRing.Presentation.HubPhase
{
    /// <summary>
    /// Story 014 (pure presentation): while the interior view is open, shows one sprite per joined NPC of that hub,
    /// wandering slowly between cell anchors (falls back to the interior train anchor when no cell is usable).
    /// Story 017: each NPC wanders among its home habitat cells (INpcHomes) plus the Empty cells 4-adjacent to them;
    /// without a home (unassigned or broken) it wanders over all Empty cells as before. Targets are re-picked on
    /// INpcHomes.HomeChanged and IHubInterior.CellChanged. Each sprite gets an NpcBody (the prefab carries a trigger
    /// BoxCollider2D); an empty-handed left click on it (no hotbar selection or drag, not over UI) raises
    /// INpcClickSource.NpcClicked, which this walker registers. Game state is never changed here.
    /// </summary>
    public sealed class InteriorNpcWalker : MonoBehaviour, INpcClickSource
    {
        private static InteriorNpcWalker _instance;
        private static bool _loggedMissing;
        private static bool _loggedNoHomes;
        private HubPhaseViewSettings _settings;
        private HubPhasePrefabCatalog _catalog;
        private INpcRecruiter _npcs;
        private IViewMode _view;
        private IInteriorAnchors _anchors;
        private IHubInterior _interior;
        private INpcHomes _homes;
        private IMapAnchors _map;
        private System.Random _random;
        private string _hub;
        private bool _targetsDirty = true;
        private readonly List<Transform> _targets = new List<Transform>();
        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private string _highlighted;
        private string _talking;
        private string _hovered;
        private readonly List<string> _arriving = new List<string>();

        /// <summary>Story 023 demo override of the arrival walk speed (0 = data value). Cleared on play start.</summary>
        public static float DemoArrivalSpeed;

        private sealed class Walker
        {
            public string NpcId;
            public SpriteRenderer Renderer;
            public NpcHoverOutline Outline;
            public Color BaseColor;
            public Transform Target;
            public float Pause;
            /// <summary>Story 023: walking from the train car to its home.</summary>
            public bool Arriving;
            /// <summary>Story 029: fixed standing post (door slot, home-cell centre or train side) while _npcWanders is false.</summary>
            public Vector3 Post;
            public bool HasPost;
            /// <summary>Story 029: post row (0 = front of the door, 1+ = rows behind).</summary>
            public int Row;
            /// <summary>Home-based targets; empty means "use the shared all-Empty-cells list".</summary>
            public readonly List<Transform> Targets = new List<Transform>();
        }

        /// <summary>hubId, npcId. Empty-handed left click on an interior NPC.</summary>
        public event Action<string, string> NpcClicked;

        /// <summary>Clears statics for a new play session (domain reload disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; _loggedMissing = false; _loggedNoHomes = false; DemoArrivalSpeed = 0f; }

        /// <summary>Waits for GameServices.Ready after the scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>Creates the walker once when NPCs, the view mode and interior anchors are registered, and registers it as INpcClickSource.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            INpcRecruiter npcs = GameServices.Get<INpcRecruiter>();
            IViewMode view = GameServices.Get<IViewMode>();
            IInteriorAnchors anchors = GameServices.Get<IInteriorAnchors>();
            if (npcs == null || view == null || anchors == null)
            {
                if (!_loggedMissing) UnityEngine.Debug.Log("InteriorNpcWalker: INpcRecruiter/IViewMode/IInteriorAnchors 미등록 - 내부 NPC 표시를 건너뜁니다.");
                _loggedMissing = true;
                return;
            }
            _instance = new GameObject(nameof(InteriorNpcWalker)).AddComponent<InteriorNpcWalker>();
            _instance.Initialize(npcs, view, anchors, GameServices.Get<IHubInterior>());
            GameServices.Register<INpcClickSource>(_instance);
        }

        /// <summary>Tints the sprite of npcId with the data highlight colour (null clears). Used by the dialog HUD while its panel is open.</summary>
        public static void SetHighlighted(string npcId)
        {
            if (_instance == null) return;
            _instance._highlighted = npcId;
            _instance.ApplyHighlight();
        }

        /// <summary>
        /// Story 029: npcId is (true) or is no longer (false) in an open dialog. The talking NPC holds still at its post facing the camera
        /// (no breathe bob, flipX off). Presentation only.
        /// </summary>
        public static void SetTalking(string npcId, bool talking)
        {
            if (_instance == null) return;
            if (talking) _instance._talking = npcId;
            else if (_instance._talking == npcId) _instance._talking = null;
        }

        /// <summary>Stores injected systems, subscribes to changes and draws the current state.</summary>
        private void Initialize(INpcRecruiter npcs, IViewMode view, IInteriorAnchors anchors, IHubInterior interior)
        {
            _settings = HubPhaseViewSettings.Load();
            _catalog = HubPhasePrefabCatalog.Load();
            _random = new System.Random(_settings.NpcSeed);
            _npcs = npcs; _view = view; _anchors = anchors; _interior = interior;
            _map = GameServices.Get<IMapAnchors>();
            _view.InteriorChanged += OnInteriorChanged;
            _npcs.NpcJoined += OnNpcJoined;
            _npcs.NpcArrived += OnNpcArrived;
            if (_interior != null) _interior.CellChanged += OnCellChanged;
            ResolveHomes();
            Rebuild();
        }

        /// <summary>Gets INpcHomes once it is registered (the logic half may land later); logs once while missing.</summary>
        private void ResolveHomes()
        {
            if (_homes != null) return;
            _homes = GameServices.Get<INpcHomes>();
            if (_homes != null) { _homes.HomeChanged += OnHomeChanged; return; }
            if (!_loggedNoHomes) UnityEngine.Debug.Log("InteriorNpcWalker: INpcHomes 미등록 - 거점 전체 배회로 대신합니다.");
            _loggedNoHomes = true;
        }

        private void OnInteriorChanged(bool open) => Rebuild();
        private void OnNpcJoined(string hubId, string npcId) { if (hubId == _view.InteriorHubId) Rebuild(); }
        /// <summary>Story 023: a passenger disembarked at the open hub — it appears at the train car and walks home.</summary>
        private void OnNpcArrived(string npcId, string hubId)
        {
            if (hubId == null || hubId != _hub || npcId == null) return;
            if (!_arriving.Contains(npcId)) _arriving.Add(npcId);
            ApplyArrivals();
        }

        /// <summary>Puts every still-arriving walker at the train anchor, aims it at its nearest home cell and staggers the starts.</summary>
        private void ApplyArrivals()
        {
            if (_hub == null || _arriving.Count == 0) return;
            Transform train = _anchors.TrainAnchor(_hub);
            if (train == null) { _arriving.Clear(); return; }
            if (_targetsDirty) RefreshTargets();
            int order = 0;
            foreach (string id in _arriving)
            {
                Walker walker = _walkers.Find(w => w.NpcId == id);
                if (walker == null || walker.Renderer == null) continue;
                Transform body = walker.Renderer.transform;
                body.position = new Vector3(train.position.x, train.position.y, body.position.z);
                walker.Arriving = true;
                walker.Target = NearestHome(walker, body.position);
                walker.Pause = order++ * _settings.ArrivalStagger;
                UnityEngine.Debug.Log($"NPC ARRIVAL: {id} 열차 칸 → {(walker.Target != null ? walker.Target.name : "?")} (속도 {ArrivalSpeed})");
            }
        }

        private float ArrivalSpeed => DemoArrivalSpeed > 0f ? DemoArrivalSpeed : _settings.ArrivalSpeed;

        /// <summary>Nearest home-cell anchor (falls back to the walker's wander list).</summary>
        private Transform NearestHome(Walker walker, Vector3 from)
        {
            var candidates = new List<Transform>();
            NpcHome home = _homes?.HomeOf(walker.NpcId);
            if (home != null && home.HubId == _hub && home.Cells != null)
                foreach (int index in home.Cells)
                {
                    Transform anchor = _anchors.CellAnchor(_hub, index);
                    if (anchor != null) candidates.Add(anchor);
                }
            if (candidates.Count == 0) candidates.AddRange(walker.Targets.Count > 0 ? walker.Targets : _targets);
            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (Transform t in candidates)
            {
                float d = (t.position - from).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = t; }
            }
            return best;
        }

        private void OnCellChanged(string hubId, int cellIndex) { if (hubId == _hub) _targetsDirty = true; }
        private void OnHomeChanged(string npcId) { if (_hub != null) _targetsDirty = true; }

        /// <summary>Destroys current walkers and spawns one per NPC of the open interior's hub.</summary>
        private void Rebuild()
        {
            foreach (Walker walker in _walkers) if (walker.Renderer != null) Destroy(walker.Renderer.gameObject);
            _walkers.Clear();
            _hovered = null;
            string previous = _hub;
            _hub = _view.InteriorOpen ? _view.InteriorHubId : null;
            if (_hub != previous) _arriving.Clear();
            _targetsDirty = true;
            if (_hub == null) return;
            ResolveHomes();
            foreach (string npcId in _npcs.NpcsAt(_hub)) _walkers.Add(new Walker { NpcId = npcId });
            RefreshTargets();
            if (_targets.Count == 0) { _walkers.Clear(); return; }
            float width = HubPhaseSprites.CellSpacing(_anchors, _hub) * _settings.NpcCellFraction;
            for (int i = _walkers.Count - 1; i >= 0; i--)
            {
                Walker walker = _walkers[i];
                Transform start = RandomTarget(walker);
                if (!_settings.NpcWanders && i == _walkers.Count - 1) ComputePosts();
                Vector3? spawnAt = !_settings.NpcWanders && walker.HasPost ? walker.Post : start != null ? start.position : (Vector3?)null;
                SpriteRenderer renderer = spawnAt.HasValue
                    ? HubPhaseSprites.Spawn(_catalog != null ? _catalog.Npc : null, transform, spawnAt.Value, "npc-" + walker.NpcId)
                    : null;
                if (renderer == null) { _walkers.RemoveAt(i); continue; }
                HubPhaseSprites.FitWidth(renderer, width);
                renderer.gameObject.AddComponent<NpcBody>().Bind(_hub, walker.NpcId);
                walker.Outline = renderer.gameObject.AddComponent<NpcHoverOutline>();
                walker.Outline.Bind(_settings.NpcHoverOutlineColor, _settings.NpcHoverOutlinePixels);
                walker.Renderer = renderer;
                walker.BaseColor = renderer.color;
                walker.Target = RandomTarget(walker);
                walker.Pause = NextPause();
            }
            ApplyArrivals();
            ApplyHighlight();
        }

        /// <summary>
        /// Shared list: anchors of Empty cells (or the interior train anchor). Per walker: home cells plus Empty cells
        /// 4-adjacent to them (same area). A walker whose current target left its list picks a new one.
        /// </summary>
        private void RefreshTargets()
        {
            _targets.Clear();
            _targetsDirty = false;
            if (_hub == null) return;
            ResolveHomes();
            IReadOnlyList<IInteriorCell> cells = _interior?.Cells(_hub);
            if (cells != null)
                foreach (IInteriorCell cell in cells)
                {
                    if (cell.State != InteriorCellState.Empty) continue;
                    Transform anchor = _anchors.CellAnchor(_hub, cell.Index);
                    if (anchor != null) _targets.Add(anchor);
                }
            if (_targets.Count == 0)
            {
                Transform train = _anchors.TrainAnchor(_hub);
                if (train != null) _targets.Add(train);
            }
            foreach (Walker walker in _walkers)
            {
                CollectHomeTargets(walker, cells);
                List<Transform> list = walker.Targets.Count > 0 ? walker.Targets : _targets;
                if (walker.Target != null && !list.Contains(walker.Target)) walker.Target = list.Count > 0 ? list[_random.Next(list.Count)] : null;
            }
        }

        /// <summary>Fills walker.Targets with its home cells' anchors and the Empty cells 4-adjacent to any of them.</summary>
        private void CollectHomeTargets(Walker walker, IReadOnlyList<IInteriorCell> cells)
        {
            walker.Targets.Clear();
            NpcHome home = _homes?.HomeOf(walker.NpcId);
            if (home == null || home.HubId != _hub || home.Cells == null || home.Cells.Count == 0 || cells == null) return;
            foreach (int index in home.Cells)
            {
                Transform anchor = _anchors.CellAnchor(_hub, index);
                if (anchor != null && !walker.Targets.Contains(anchor)) walker.Targets.Add(anchor);
            }
            foreach (IInteriorCell cell in cells)
            {
                if (cell.State != InteriorCellState.Empty || !IsAdjacentToHome(cell, home.Cells, cells)) continue;
                Transform anchor = _anchors.CellAnchor(_hub, cell.Index);
                if (anchor != null && !walker.Targets.Contains(anchor)) walker.Targets.Add(anchor);
            }
        }

        /// <summary>True when cell shares an edge (same area, Manhattan distance 1 in Row/Column) with any home cell.</summary>
        private static bool IsAdjacentToHome(IInteriorCell cell, IReadOnlyList<int> homeCells, IReadOnlyList<IInteriorCell> cells)
        {
            foreach (int index in homeCells)
            {
                if (index < 0 || index >= cells.Count) continue;
                IInteriorCell home = cells[index];
                if (home.IsUpper != cell.IsUpper) continue;
                if (Mathf.Abs(home.Row - cell.Row) + Mathf.Abs(home.Column - cell.Column) == 1) return true;
            }
            return false;
        }

        private Transform RandomTarget(Walker walker)
        {
            List<Transform> list = walker.Targets.Count > 0 ? walker.Targets : _targets;
            return list.Count > 0 ? list[_random.Next(list.Count)] : null;
        }

        private float NextPause() => _settings.NpcPauseMin + (float)_random.NextDouble() * (_settings.NpcPauseMax - _settings.NpcPauseMin);

        /// <summary>Tints the highlighted NPC's sprite and restores the others.</summary>
        private void ApplyHighlight()
        {
            foreach (Walker walker in _walkers)
                if (walker.Renderer != null)
                    walker.Renderer.color = walker.NpcId == _highlighted ? _settings.NpcHighlightColor : walker.BaseColor;
        }

        /// <summary>마우스 아래 NPC가 바뀌면 _hovered를 갱신하고 그 NPC의 윤곽선만 켠다.</summary>
        private void UpdateHover()
        {
            string hovered = HoveredNpc();
            if (hovered == _hovered) return;
            _hovered = hovered;
            foreach (Walker walker in _walkers)
                if (walker.Outline != null) walker.Outline.SetVisible(walker.NpcId == hovered);
        }

        /// <summary>
        /// 클릭과 같은 조건에서 마우스 아래 NPC의 id를 반환한다. 내부 화면이 열려 있고 빈손이며 UI 위가 아니어야 한다.
        /// 조건이 맞지 않거나 NPC가 없으면 null을 반환한다.
        /// </summary>
        private string HoveredNpc()
        {
            Mouse mouse = Mouse.current;
            if (_hub == null || _walkers.Count == 0 || mouse == null || !_view.InteriorOpen) return null;
            PlacementGhost ghost = PlacementGhost.Instance;
            if (ghost != null && (ghost.Selected.IsValid || ghost.DragEntry.IsValid)) return null;
            Vector2 pointer = mouse.position.ReadValue();
            if (IsPointerOverUi(pointer)) return null;
            Camera camera = _map != null && _map.MapCamera != null ? _map.MapCamera : Camera.main;
            if (camera == null) return null;
            foreach (RaycastHit2D hit in Physics2D.GetRayIntersectionAll(camera.ScreenPointToRay(pointer), Mathf.Infinity))
                if (hit.collider != null && hit.collider.TryGetComponent(out NpcBody npc) && npc.HubId == _hub) return npc.NpcId;
            return null;
        }

        void Update()
        {
            HandleClick();
            UpdateHover();
            if (_walkers.Count == 0) return;
            if (_targetsDirty) RefreshTargets();
            if (_targets.Count == 0) return;
            float step = _settings.NpcSpeed * Time.deltaTime;
            if (!_settings.NpcWanders) ComputePosts();
            foreach (Walker walker in _walkers)
            {
                if (walker.Renderer == null) continue;
                if (!_settings.NpcWanders) { StandAtPost(walker); continue; }
                if (walker.Target == null) walker.Target = RandomTarget(walker);
                if (walker.Target == null) continue;
                if (walker.Pause > 0f) { walker.Pause -= Time.deltaTime; continue; }
                Transform body = walker.Renderer.transform;
                Vector3 goal = walker.Target.position; goal.z = body.position.z;
                if (Mathf.Abs(goal.x - body.position.x) > 0.001f) walker.Renderer.flipX = goal.x < body.position.x;
                body.position = Vector3.MoveTowards(body.position, goal, walker.Arriving ? ArrivalSpeed * Time.deltaTime : step);
                if ((body.position - goal).sqrMagnitude > 0.0001f) continue;
                if (walker.Arriving)
                {
                    walker.Arriving = false;
                    _arriving.Remove(walker.NpcId);
                    walker.Pause = _settings.ArrivalPause;
                    walker.Target = RandomTarget(walker);
                    continue;
                }
                walker.Pause = NextPause();
                walker.Target = RandomTarget(walker);
            }
        }

        /// <summary>
        /// Empty-handed left click on an NPC body: only with the interior open, no hotbar selection or drag (those clicks
        /// are placement), and not over UI (EventSystem raycast).
        /// </summary>
        private void HandleClick()
        {
            Mouse mouse = Mouse.current;
            if (_hub == null || mouse == null || !mouse.leftButton.wasPressedThisFrame || !_view.InteriorOpen) return;
            PlacementGhost ghost = PlacementGhost.Instance;
            if (ghost != null && (ghost.Selected.IsValid || ghost.DragEntry.IsValid)) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (IsPointerOverUi(pointer)) return;
            Camera camera = _map != null && _map.MapCamera != null ? _map.MapCamera : Camera.main;
            if (camera == null) return;
            RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(camera.ScreenPointToRay(pointer), Mathf.Infinity);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || !hit.collider.TryGetComponent(out NpcBody npc) || npc.HubId != _hub) continue;
                NpcClicked?.Invoke(npc.HubId, npc.NpcId);
                return;
            }
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || !hit.collider.TryGetComponent(out BuildingPiece piece) || piece.HubId != _hub) continue;
                ClickBuilding(piece);
                return;
            }
        }

        /// <summary>
        /// Story 027: an empty-handed click on a building opens the dialog of the NPC whose home is that cluster (same type, overlapping
        /// cells); with nobody living there a short toast says the building is waiting for a passenger.
        /// </summary>
        private void ClickBuilding(BuildingPiece piece)
        {
            ResolveHomes();
            if (_homes != null)
                foreach (NpcHome home in _homes.HomesAt(_hub) ?? Array.Empty<NpcHome>())
                {
                    if (home == null || home.HabitatTypeId != piece.TypeId || home.Cells == null || piece.Cells == null) continue;
                    foreach (int cell in home.Cells)
                        if (ContainsCell(piece.Cells, cell)) { NpcClicked?.Invoke(_hub, home.NpcId); return; }
                }
            string name = HabitatsInstaller.Catalog?.Get(piece.TypeId)?.Name ?? piece.TypeId;
            string format;
            try { format = Strings.Get("building_empty"); }
            catch (KeyNotFoundException) { format = "{0}"; }
            UnityEngine.Debug.Log($"BUILDING CLICK: {_hub} {piece.TypeId} - 사는 NPC 없음");
            ToastScreen.Show(string.Format(format, name), _settings.EmptyBuildingToastSeconds);
        }

        private static bool ContainsCell(IReadOnlyList<int> cells, int cell)
        {
            foreach (int c in cells) if (c == cell) return true;
            return false;
        }

        /// <summary>
        /// Story 029 (_npcWanders false): assigns every walker a fixed post. Building homes: the door of the building
        /// (BuildingView.TryGetDoor + _doorOffsetY); home cells without a building: the centre of the home cell anchors; no home:
        /// the interior train anchor. NPCs sharing one spot (join order) spread sideways by _npcPostSpacing cells, at most
        /// _npcPostsPerRow per row, further rows sit _npcRowOffset cells behind (up). Positions only; game state is untouched.
        /// </summary>
        private void ComputePosts()
        {
            if (_hub == null) return;
            float cell = HubPhaseSprites.CellSpacing(_anchors, _hub);
            var groups = new Dictionary<string, List<Walker>>();
            var bases = new Dictionary<string, Vector3>();
            foreach (Walker walker in _walkers)
            {
                walker.HasPost = false;
                if (!TryPostBase(walker, out string key, out Vector3 at)) continue;
                if (!groups.TryGetValue(key, out List<Walker> list)) { groups[key] = list = new List<Walker>(); bases[key] = at; }
                list.Add(walker);
            }
            int perRow = _settings.NpcPostsPerRow;
            foreach (KeyValuePair<string, List<Walker>> group in groups)
            {
                Vector3 at = bases[group.Key];
                int count = group.Value.Count;
                for (int i = 0; i < count; i++)
                {
                    int row = i / perRow;
                    int inRow = Mathf.Min(perRow, count - row * perRow);
                    float slot = i % perRow - (inRow - 1) * 0.5f;
                    Walker walker = group.Value[i];
                    walker.Post = at + new Vector3(slot * _settings.NpcPostSpacing * cell, row * _settings.NpcRowOffset * cell, 0f);
                    walker.Row = row;
                    walker.HasPost = true;
                }
            }
        }

        /// <summary>Story 029: the shared spot (and its grouping key) the walker's post is spread around.</summary>
        private bool TryPostBase(Walker walker, out string key, out Vector3 at)
        {
            NpcHome home = _homes?.HomeOf(walker.NpcId);
            if (home != null && home.HubId == _hub && home.Cells != null && home.Cells.Count > 0)
            {
                if (BuildingView.TryGetDoor(_hub, home.HabitatTypeId, home.Cells, out Vector3 door))
                {
                    door.y += _settings.DoorOffsetY;
                    key = "door:" + Mathf.RoundToInt(door.x * 100f) + "," + Mathf.RoundToInt(door.y * 100f);
                    at = door;
                    return true;
                }
                Vector3 sum = Vector3.zero;
                int found = 0;
                foreach (int index in home.Cells)
                {
                    Transform anchor = _anchors.CellAnchor(_hub, index);
                    if (anchor == null) continue;
                    sum += anchor.position;
                    found++;
                }
                if (found > 0)
                {
                    at = sum / found;
                    key = "home:" + home.HabitatTypeId + ":" + Mathf.RoundToInt(at.x * 100f) + "," + Mathf.RoundToInt(at.y * 100f);
                    return true;
                }
            }
            Transform train = _anchors.TrainAnchor(_hub);
            key = "train";
            at = train != null ? train.position : Vector3.zero;
            return train != null;
        }

        /// <summary>
        /// Story 029: walks the NPC to its post (arrival speed while Arriving, which ends there), then stands facing the camera with the
        /// breathe bob (no bob while talking). Rows behind the door draw one sorting step lower.
        /// </summary>
        private void StandAtPost(Walker walker)
        {
            if (!walker.HasPost) return;
            if (walker.Pause > 0f) { walker.Pause -= Time.deltaTime; return; }
            Transform body = walker.Renderer.transform;
            Vector3 post = walker.Post;
            post.z = body.position.z;
            walker.Renderer.sortingOrder = _settings.NpcSortingOrder - walker.Row;
            float reach = _settings.BreatheAmplitude * 2f + 0.02f;
            if (walker.Arriving || (body.position - post).sqrMagnitude > reach * reach)
            {
                if (Mathf.Abs(post.x - body.position.x) > 0.001f) walker.Renderer.flipX = post.x < body.position.x;
                body.position = Vector3.MoveTowards(body.position, post, (walker.Arriving ? ArrivalSpeed : _settings.NpcSpeed) * Time.deltaTime);
                if (walker.Arriving && (body.position - post).sqrMagnitude <= 0.0001f) { walker.Arriving = false; _arriving.Remove(walker.NpcId); }
                return;
            }
            walker.Renderer.flipX = false;
            if (walker.NpcId == _talking) { body.position = post; return; }
            float phase = (Mathf.Abs(walker.NpcId?.GetHashCode() ?? 0) % 100) * 0.1f;
            body.position = post + Vector3.up * (Mathf.Sin((Time.time + phase) * _settings.BreatheSpeed) * _settings.BreatheAmplitude);
        }

        /// <summary>Story 027: the body sprite of npcId for the dialog portrait (the spawned body, else the NPC prefab's sprite).</summary>
        public static Sprite BodySprite(string npcId)
        {
            if (_instance != null)
                foreach (Walker walker in _instance._walkers)
                    if (walker.NpcId == npcId && walker.Renderer != null) return walker.Renderer.sprite;
            HubPhasePrefabCatalog catalog = _instance != null ? _instance._catalog : HubPhasePrefabCatalog.Load();
            GameObject prefab = catalog != null ? catalog.Npc : null;
            SpriteRenderer renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            return renderer != null ? renderer.sprite : null;
        }

        /// <summary>pointer is over a HUD panel: EventSystem raycast only (Story 025 removed the old registered tests).</summary>
        private bool IsPointerOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _uiHits);
            return _uiHits.Count > 0;
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_npcs != null) _npcs.NpcJoined -= OnNpcJoined;
            if (_npcs != null) _npcs.NpcArrived -= OnNpcArrived;
            if (_interior != null) _interior.CellChanged -= OnCellChanged;
            if (_homes != null) _homes.HomeChanged -= OnHomeChanged;
            if (_instance == this) _instance = null;
        }
    }
}
