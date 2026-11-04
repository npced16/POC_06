using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 027. 열린 거점 내부에서 완성된 서식지(IHabitatClusters.CompletedAt)마다 칸 모양 그대로 건물을 세운다. 칸마다 InteriorCatalog의
    /// BuildingPiece 프리팹을 놓고 4방향 이웃 비트마스크(1=N, 2=E, 4=S, 8=W)로 지붕 조각을 고르며, 두 이웃은 있는데 대각이 빈 곳에는
    /// 오목 모서리 덧조각을 얹는다. 아래 가장자리 중 가로 중심에 가장 가까운 칸에 문, 맨 윗줄 오른쪽 칸에 굴뚝(+연기), 가운데에 가장 가까운
    /// 칸에 종류 상징물(HabitatTypes.json _buildingStyle·_signProp)을 놓는다. 건물 아래 타일 조각 렌더러는 끄고 묶음이 깨지면 다시 켠다.
    /// 완성·깨짐·배치·제거·내부 열림이 있으면 다음 LateUpdate에 묶음 목록을 다시 읽어(칸이 늘어난 묶음도 반영) 바뀐 건물만 다시 세운다.
    /// 스토리 031: 사라진 묶음의 칸을 새 묶음이 이어받지 않으면(철거·깨짐) 건물은 바로 지우지 않고 InteriorCatalog._buildingCollapseSeconds 동안
    /// _buildingCollapseDistance만큼 내려앉으며 투명해진 뒤 지운다(완성 연출의 반대). 무너지는 동안 클릭 충돌체는 끈다.
    /// QA 후속(건물 장식): 종류의 HabitatType.Decor(_decor)로 지붕 색조 계열, 문 칸을 뺀 아래 가장자리 칸의 창 무늬 덧조각,
    /// 굴뚝·상징물 칸을 피한 칸의 지붕 덧소품 1~2개(첫째는 왼쪽 위부터, 둘째는 오른쪽 아래부터 빈 칸)를 고른다. 모두 종류별로 결정적이다.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        private const string CLEARED_LOT_ID = "cleared_lot";

        private static readonly Vector2Int[] Diagonals = { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1) };
        private static BuildingView _instance;
        private IHabitatClusters _clusters;
        private IInteriorAnchors _anchors;
        private IViewMode _view;
        private IItemPlacement _placement;
        private InteriorCatalog _catalog;
        private string _hub;
        private bool _dirty = true;
        private bool _animateNext;
        private bool _warned;
        private readonly Dictionary<string, Building> _buildings = new Dictionary<string, Building>(StringComparer.Ordinal);
        private readonly List<SpriteRenderer> _hidden = new List<SpriteRenderer>();
        private readonly List<TextMeshPro> _hiddenLabels = new List<TextMeshPro>();
        private readonly List<Collapse> _collapsing = new List<Collapse>();

        /// <summary>스토리 031. 무너지는 중인 건물(내려앉기·투명해지기).</summary>
        private sealed class Collapse
        {
            public GameObject Root;
            public Vector3 Start;
            public SpriteRenderer[] Renderers;
            public Color[] Colors;
            public float Age;
        }

        private sealed class Building
        {
            public string TypeId;
            public List<int> Cells;
            public GameObject Root;
            public Transform Door;
            public Vector3 RestPosition;
            public float Age = -1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        private static void Create()
        {
            if (_instance != null || GameServices.Get<IInteriorAnchors>() == null || GameServices.Get<IViewMode>() == null) return;
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            if (clusters == null) return;
            _instance = new GameObject(nameof(BuildingView)).AddComponent<BuildingView>();
            _instance.Initialize(clusters);
        }

        private void Initialize(IHabitatClusters clusters)
        {
            _clusters = clusters;
            _anchors = GameServices.Get<IInteriorAnchors>();
            _view = GameServices.Get<IViewMode>();
            _placement = GameServices.Get<IItemPlacement>();
            foreach (HubInteriorRoot root in FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (root.Catalog != null) { _catalog = root.Catalog; break; }
            _clusters.HabitatCompleted += OnCompleted;
            _clusters.HabitatBroken += OnBroken;
            _view.InteriorChanged += OnInteriorChanged;
            if (_placement != null) { _placement.ItemPlaced += OnItemChanged; _placement.ItemRemoved += OnItemChanged; }
        }

        private void OnCompleted(string hubId, string typeId) { _dirty = true; _animateNext = true; }
        private void OnBroken(string hubId, string typeId) => _dirty = true;
        private void OnInteriorChanged(bool open) => _dirty = true;
        private void OnItemChanged(string hubId, int cellIndex, string itemId) => _dirty = true;

        /// <summary>
        /// hubId에 typeId 묶음(cells와 칸이 하나라도 겹침)의 건물이 서 있으면 그 문 위치를 돌려준다. NPC가 문 앞에 서는 데 쓴다.
        /// </summary>
        public static bool TryGetDoor(string hubId, string typeId, IReadOnlyList<int> cells, out Vector3 position)
        {
            position = Vector3.zero;
            Building building = _instance != null ? _instance.Find(hubId, typeId, cells) : null;
            if (building == null || building.Door == null) return false;
            position = building.Door.position;
            if (building.Root != null) position.y += building.RestPosition.y - building.Root.transform.position.y;
            return true;
        }

        private Building Find(string hubId, string typeId, IReadOnlyList<int> cells)
        {
            if (hubId == null || hubId != _hub || cells == null) return null;
            foreach (Building building in _buildings.Values)
                if (building.TypeId == typeId && building.Cells.Any(cells.Contains)) return building;
            return null;
        }

        void LateUpdate()
        {
            if (_dirty) { _dirty = false; Sync(); _animateNext = false; }
            float seconds = _catalog != null ? _catalog.BuildingRiseSeconds : 0f;
            foreach (Building building in _buildings.Values)
            {
                if (building.Age < 0f || building.Root == null) continue;
                building.Age += Time.deltaTime;
                float t = seconds > 0f ? Mathf.Clamp01(building.Age / seconds) : 1f;
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                building.Root.transform.position = building.RestPosition + Vector3.down * (_catalog.BuildingRiseDistance * (1f - ease));
                if (t >= 1f) building.Age = -1f;
            }
            AnimateCollapses();
        }

        /// <summary>스토리 031. 무너지는 건물을 ease-in으로 내려앉히며 알파를 줄이고, 끝나면 지운다.</summary>
        private void AnimateCollapses()
        {
            float seconds = _catalog != null ? _catalog.BuildingCollapseSeconds : 0f;
            float distance = _catalog != null ? _catalog.BuildingCollapseDistance : 0f;
            for (int i = _collapsing.Count - 1; i >= 0; i--)
            {
                Collapse collapse = _collapsing[i];
                if (collapse.Root == null) { _collapsing.RemoveAt(i); continue; }
                collapse.Age += Time.deltaTime;
                float t = seconds > 0f ? Mathf.Clamp01(collapse.Age / seconds) : 1f;
                if (t >= 1f) { Destroy(collapse.Root); _collapsing.RemoveAt(i); continue; }
                float ease = t * t * t;
                collapse.Root.transform.position = collapse.Start + Vector3.down * (distance * ease);
                for (int r = 0; r < collapse.Renderers.Length; r++)
                {
                    if (collapse.Renderers[r] == null) continue;
                    Color c = collapse.Colors[r];
                    c.a *= 1f - t;
                    collapse.Renderers[r].color = c;
                }
            }
        }

        /// <summary>스토리 031. building을 무너짐 목록으로 옮긴다(연출 시간이 0이면 바로 지운다).</summary>
        private void StartCollapse(Building building)
        {
            if (building.Root == null) return;
            if (_catalog == null || _catalog.BuildingCollapseSeconds <= 0f) { Destroy(building.Root); return; }
            foreach (Collider2D collider in building.Root.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (BuildingPiece piece in building.Root.GetComponentsInChildren<BuildingPiece>(true)) piece.enabled = false;
            building.Root.name += "_Collapsing";
            SpriteRenderer[] renderers = building.Root.GetComponentsInChildren<SpriteRenderer>(true);
            _collapsing.Add(new Collapse
            {
                Root = building.Root,
                Start = building.Age >= 0f ? building.RestPosition : building.Root.transform.position,
                Renderers = renderers,
                Colors = renderers.Select(r => r.color).ToArray()
            });
            UnityEngine.Debug.Log($"BUILDING COLLAPSE: {_hub} {building.TypeId} 칸 {string.Join(",", building.Cells)} {_catalog.BuildingCollapseSeconds:0.##}초");
        }

        /// <summary>열린 거점의 완성 서식지 목록과 서 있는 건물을 맞춘다(새 묶음은 세우고 사라진 묶음은 부순다).</summary>
        private void Sync()
        {
            string hub = _view.InteriorOpen ? _view.InteriorHubId : null;
            if (hub != _hub) { Clear(); _hub = hub; }
            RestoreHidden();
            if (_hub == null) return;
            if (_catalog == null || !_catalog.HasBuildings)
            {
                Warn("BuildingView: InteriorCatalog에 건물 조각이 없습니다. SpriteGenBuildings.Generate를 실행하세요.");
                return;
            }
            var wanted = new Dictionary<string, HabitatCluster>(StringComparer.Ordinal);
            foreach (HabitatCluster cluster in _clusters.CompletedAt(_hub) ?? Array.Empty<HabitatCluster>())
                if (cluster?.Cells != null && cluster.Cells.Count > 0) wanted[Key(cluster)] = cluster;
            foreach (string key in _buildings.Keys.Where(k => !wanted.ContainsKey(k)).ToList())
            {
                Building gone = _buildings[key];
                bool replaced = wanted.Values.Any(c => c.Cells.Any(gone.Cells.Contains));
                if (replaced) { if (gone.Root != null) Destroy(gone.Root); }
                else StartCollapse(gone);
                _buildings.Remove(key);
            }
            foreach (KeyValuePair<string, HabitatCluster> pair in wanted)
                if (!_buildings.ContainsKey(pair.Key))
                {
                    Building building = Build(pair.Value);
                    if (building != null) _buildings[pair.Key] = building;
                }
            foreach (Building building in _buildings.Values)
                if (building.TypeId != CLEARED_LOT_ID) HideTiles(building.Cells);
        }

        private static string Key(HabitatCluster cluster) => cluster.TypeId + ":" + string.Join(",", cluster.Cells.OrderBy(c => c));

        /// <summary>묶음 하나의 건물(지붕·모서리·문·굴뚝·상징물)을 세운다.</summary>
        private Building Build(HabitatCluster cluster)
        {
            Transform first = _anchors.CellAnchor(_hub, cluster.Cells[0]);
            HubInteriorRoot root = first != null ? first.GetComponentInParent<HubInteriorRoot>() : null;
            if (root == null) return null;
            float tile = _catalog.TileSize;
            var grid = new Dictionary<Vector2Int, int>();
            var positions = new Dictionary<int, Vector3>();
            foreach (int cell in cluster.Cells)
            {
                Transform anchor = _anchors.CellAnchor(_hub, cell);
                if (anchor == null) continue;
                Vector3 local = root.transform.InverseTransformPoint(anchor.position);
                // Mathf.RoundToInt는 .5를 짝수로 반올림한다(열 좌표가 ±0.5, ±1.5…라 이웃 열이 같은 칸으로 겹쳤음) → Floor(x + 0.5)
                grid[new Vector2Int(Mathf.FloorToInt(local.x / tile + 0.5f), Mathf.FloorToInt(local.y / tile + 0.5f))] = cell;
                positions[cell] = anchor.position;
            }
            if (grid.Count == 0) return null;
            HabitatType type = HabitatsInstaller.Catalog?.Get(cluster.TypeId);
            string style = type?.BuildingStyle;
            HabitatDecor decor = type?.Decor;
            string tint = decor?.RoofTint;
            var container = new GameObject("Building_" + cluster.TypeId);
            container.transform.SetParent(root.transform, false);
            container.transform.position = root.transform.position;
            var building = new Building { TypeId = cluster.TypeId, Cells = cluster.Cells.ToList(), Root = container, RestPosition = container.transform.position };
            List<int> cells = building.Cells;
            if (cluster.TypeId == CLEARED_LOT_ID)
            {
                Vector3 center = positions.Values.Aggregate(Vector3.zero, (sum, position) => sum + position) / positions.Count;
                GameObject marker = Prop(container, center + Vector3.up * (_catalog.SignLift * tile), _catalog.BuildingSign(type?.SignProp), _catalog.BuildingSignOrder, "ClearedLotMarker");
                marker.transform.localScale *= _catalog.BuildingSignScale;
                building.Door = marker.transform;
                HideBuildingVisuals(container);
                return building;
            }
            foreach (KeyValuePair<Vector2Int, int> pair in grid)
            {
                Vector2Int g = pair.Key;
                int mask = (grid.ContainsKey(g + Vector2Int.up) ? 1 : 0) | (grid.ContainsKey(g + Vector2Int.right) ? 2 : 0)
                    | (grid.ContainsKey(g + Vector2Int.down) ? 4 : 0) | (grid.ContainsKey(g + Vector2Int.left) ? 8 : 0);
                Vector3 at = positions[pair.Value] + Vector3.forward * _catalog.BuildingDepth;
                GameObject piece = Instantiate(_catalog.BuildingPiecePrefab, container.transform);
                piece.name = "Roof_" + mask;
                piece.transform.position = at;
                SpriteRenderer renderer = piece.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null) { renderer.sprite = _catalog.RoofSprite(style, tint, mask); renderer.sortingOrder = _catalog.BuildingOrder; }
                (piece.GetComponent<BuildingPiece>() ?? piece.AddComponent<BuildingPiece>()).Bind(_hub, cluster.TypeId, cells);
                for (int c = 0; c < Diagonals.Length; c++)
                {
                    Vector2Int d = Diagonals[c];
                    if (grid.ContainsKey(g + new Vector2Int(d.x, 0)) && grid.ContainsKey(g + new Vector2Int(0, d.y)) && !grid.ContainsKey(g + d))
                        Prop(container, at, _catalog.InnerCornerSprite(style, tint, c), _catalog.BuildingDoorOrder, "Corner_" + c);
                }
            }
            float centerX = grid.Keys.Average(g => (float)g.x);
            int bottom = grid.Keys.Where(g => !grid.ContainsKey(g + Vector2Int.down)).Min(g => g.y);
            Vector2Int doorCell = grid.Keys.Where(g => g.y == bottom && !grid.ContainsKey(g + Vector2Int.down))
                .OrderBy(g => Mathf.Abs(g.x - centerX)).ThenBy(g => g.x).First();
            int variant = (cluster.TypeId ?? "").Sum(ch => ch);
            building.Door = Prop(container, positions[grid[doorCell]], style == "yard" ? null : _catalog.BuildingDoor(variant), _catalog.BuildingDoorOrder, "Door").transform;
            int top = grid.Keys.Max(g => g.y);
            Vector2Int chimneyCell = grid.Keys.Where(g => g.y == top).OrderByDescending(g => g.x).First();
            Vector3 chimneyAt = positions[grid[chimneyCell]] + (Vector3)(_catalog.ChimneyOffset * tile);
            GameObject chimney = style == "yard" ? null : Prop(container, chimneyAt, _catalog.BuildingChimney, _catalog.BuildingSignOrder, "Chimney");
            if (chimney != null && _catalog.BuildingSmokePrefab != null)
            {
                GameObject smoke = Instantiate(_catalog.BuildingSmokePrefab, chimney.transform);
                smoke.transform.localPosition = _catalog.SmokeOffset;
                foreach (SpriteRenderer r in smoke.GetComponentsInChildren<SpriteRenderer>(true)) r.sortingOrder = _catalog.BuildingSignOrder + 1;
            }
            Vector3 centroid = positions.Values.Aggregate(Vector3.zero, (sum, p) => sum + p) / positions.Count;
            int signCell = positions.OrderBy(p => (p.Value - centroid).sqrMagnitude).First().Key;
            GameObject sign = Prop(container, positions[signCell] + Vector3.up * (_catalog.SignLift * tile), _catalog.BuildingSign(type?.SignProp), _catalog.BuildingSignOrder, "Sign_" + type?.SignProp);
            sign.transform.localScale *= _catalog.BuildingSignScale;
            Decorate(container, decor, grid, positions, doorCell, chimneyCell, signCell);
            if (_animateNext && _catalog.BuildingRiseSeconds > 0f)
            {
                building.Age = 0f;
                container.transform.position = building.RestPosition + Vector3.down * _catalog.BuildingRiseDistance;
            }
            HideBuildingVisuals(container);
            UnityEngine.Debug.Log($"BUILDING: {_hub} {cluster.TypeId} 칸 {string.Join(",", cells)} 지붕 {style ?? "?"}/{tint ?? "base"} 창 {decor?.WindowPattern ?? 0} 소품 {(decor != null ? string.Join(",", decor.Props) : "-")} 상징물 {type?.SignProp ?? "?"} 문 칸 {grid[doorCell]}");
            return building;
        }

        /// <summary>
        /// container 안의 지붕과 장식 렌더러를 숨겨 완성 묶음의 외곽선과 이름만 보이게 한다.
        /// BuildingPiece 충돌체와 문 Transform은 그대로 유지한다.
        /// </summary>
        private static void HideBuildingVisuals(GameObject container)
        {
            foreach (SpriteRenderer renderer in container.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
        }

        /// <summary>QA 후속(건물 장식). 창 무늬 덧조각(문 칸을 뺀 아래 가장자리 칸)과 지붕 덧소품(굴뚝·상징물 칸을 피한 빈 칸)을 놓는다.</summary>
        private void Decorate(GameObject container, HabitatDecor decor, Dictionary<Vector2Int, int> grid, Dictionary<int, Vector3> positions,
            Vector2Int doorCell, Vector2Int chimneyCell, int signCell)
        {
            if (decor == null) return;
            float tile = _catalog.TileSize;
            Sprite window = _catalog.BuildingWindow(decor.WindowPattern);
            if (window != null)
                foreach (Vector2Int g in grid.Keys.Where(g => !grid.ContainsKey(g + Vector2Int.down) && g != doorCell).OrderBy(g => g.x))
                    Prop(container, positions[grid[g]], window, _catalog.BuildingDoorOrder, "Window_" + decor.WindowPattern);
            var used = new HashSet<Vector2Int> { chimneyCell };
            foreach (KeyValuePair<Vector2Int, int> pair in grid) if (pair.Value == signCell) used.Add(pair.Key);
            for (int i = 0; i < decor.Props.Count; i++)
            {
                Sprite sprite = _catalog.BuildingDecor(decor.Props[i]);
                if (sprite == null) continue;
                IEnumerable<Vector2Int> order = i == 0
                    ? grid.Keys.OrderByDescending(g => g.y).ThenBy(g => g.x)
                    : grid.Keys.OrderBy(g => g.y).ThenByDescending(g => g.x);
                Vector2Int? free = null;
                foreach (Vector2Int g in order) if (!used.Contains(g)) { free = g; break; }
                if (free == null) continue; // 칸이 모자란 작은 건물은 남는 소품을 생략한다
                used.Add(free.Value);
                GameObject prop = Prop(container, positions[grid[free.Value]] + Vector3.up * (_catalog.DecorLift * tile), sprite, _catalog.BuildingSignOrder, "Decor_" + decor.Props[i]);
                prop.transform.localScale *= _catalog.BuildingDecorScale;
            }
        }

        private GameObject Prop(GameObject container, Vector3 at, Sprite sprite, int order, string name)
        {
            GameObject prop = Instantiate(_catalog.BuildingPropPrefab, container.transform);
            prop.name = name;
            prop.transform.position = at + Vector3.forward * _catalog.BuildingDepth;
            SpriteRenderer renderer = prop.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null) { renderer.sprite = sprite; renderer.sortingOrder = order; }
            return prop;
        }

        /// <summary>건물 칸의 타일 조각(ItemSlot 아래 렌더러)을 끈다.</summary>
        private void HideTiles(List<int> cells)
        {
            foreach (int cell in cells)
            {
                Transform anchor = _anchors.CellAnchor(_hub, cell);
                InteriorCellView view = anchor != null ? anchor.GetComponent<InteriorCellView>() : null;
                if (view == null || view.ItemSlot == view.transform) continue;
                foreach (SpriteRenderer renderer in view.ItemSlot.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.enabled) { renderer.enabled = false; _hidden.Add(renderer); }
                foreach (TextMeshPro label in view.ItemSlot.GetComponentsInChildren<TextMeshPro>(true))
                    if (label.enabled) { label.enabled = false; _hiddenLabels.Add(label); }
            }
        }

        private void RestoreHidden()
        {
            foreach (SpriteRenderer renderer in _hidden) if (renderer != null) renderer.enabled = true;
            _hidden.Clear();
            foreach (TextMeshPro label in _hiddenLabels) if (label != null) label.enabled = true;
            _hiddenLabels.Clear();
        }

        private void Clear()
        {
            RestoreHidden();
            foreach (Building building in _buildings.Values) if (building.Root != null) Destroy(building.Root);
            _buildings.Clear();
            foreach (Collapse collapse in _collapsing) if (collapse.Root != null) Destroy(collapse.Root);
            _collapsing.Clear();
        }

        private void Warn(string message)
        {
            if (_warned) return;
            _warned = true;
            UnityEngine.Debug.LogWarning(message);
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_clusters != null) { _clusters.HabitatCompleted -= OnCompleted; _clusters.HabitatBroken -= OnBroken; }
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_placement != null) { _placement.ItemPlaced -= OnItemChanged; _placement.ItemRemoved -= OnItemChanged; }
            if (_instance == this) _instance = null;
        }
    }
}
