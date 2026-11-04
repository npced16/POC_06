using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 완성된 서식지의 바깥 둘레에만 카탈로그 색 선을 그리고 묶음 중심에 이름을 놓는다.
    /// HabitatCompleted, HabitatBroken 및 내부 화면 열림 때 해당 거점의 표시를 다시 그린다.
    /// IHabitatClusters가 등록되지 않았으면(016 이전) 아무것도 만들지 않는다.
    /// </summary>
    public sealed class ClusterFrameView : MonoBehaviour
    {
        private static ClusterFrameView _instance;
        private IHabitatClusters _clusters;
        private IInteriorAnchors _anchors;
        private IViewMode _view;
        private InteriorCatalog _catalog;
        private bool _warned;
        private readonly Dictionary<string, List<GameObject>> _frames = new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        private static void Create()
        {
            if (_instance != null || GameServices.Get<IInteriorAnchors>() == null || GameServices.Get<IViewMode>() == null) return;
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            if (clusters == null) { UnityEngine.Debug.Log("ClusterFrameView: IHabitatClusters 미등록(스토리 016 이전) - 서식지 묶음 표시를 건너뜁니다."); return; }
            _instance = new GameObject(nameof(ClusterFrameView)).AddComponent<ClusterFrameView>();
            _instance.Initialize(clusters);
        }

        private void Initialize(IHabitatClusters clusters)
        {
            _clusters = clusters;
            _anchors = GameServices.Get<IInteriorAnchors>();
            _view = GameServices.Get<IViewMode>();
            foreach (HubInteriorRoot root in UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (root.Catalog != null) { _catalog = root.Catalog; break; }
            _clusters.HabitatCompleted += OnClusterChanged;
            _clusters.HabitatBroken += OnClusterChanged;
            _view.InteriorChanged += OnInteriorChanged;
            if (_view.InteriorOpen) Rebuild(_view.InteriorHubId);
        }

        private void OnClusterChanged(string hubId, string typeId) => Rebuild(hubId);

        private void OnInteriorChanged(bool open) { if (open) Rebuild(_view.InteriorHubId); }

        /// <summary>
        /// hubId의 완성 묶음과 칸 앵커를 읽어 외곽 변과 중심 이름을 다시 놓는다.
        /// 생성한 표시 오브젝트를 _frames에 저장한다.
        /// </summary>
        public void Rebuild(string hubId)
        {
            if (hubId == null) return;
            if (_frames.TryGetValue(hubId, out List<GameObject> old))
                foreach (GameObject frame in old) if (frame != null) Destroy(frame);
            var made = new List<GameObject>();
            _frames[hubId] = made;
            GameObject prefab = _catalog != null ? _catalog.ClusterFramePrefab : null;
            if (prefab == null) { Warn("ClusterFrameView: InteriorCatalog에 ClusterFrame 프리팹이 없습니다. Story012Build.Generate를 실행하세요."); return; }
            IReadOnlyList<HabitatCluster> clusters = _clusters.CompletedAt(hubId);
            if (clusters == null) return;
            foreach (HabitatCluster cluster in clusters)
            {
                if (cluster?.Cells == null || cluster.Cells.Count == 0) continue;
                Color color = _catalog.ClusterColor(cluster.TypeId);
                color.a = 1f;
                Vector3 center = Vector3.zero;
                int visibleCells = 0;
                Transform nameParent = null;
                var anchors = new Dictionary<Vector2Int, Transform>();
                foreach (int cellIndex in cluster.Cells)
                {
                    Transform anchor = _anchors.CellAnchor(hubId, cellIndex);
                    if (anchor == null) continue;
                    if (nameParent == null) nameParent = anchor;
                    center += anchor.position;
                    visibleCells++;
                    Vector3 position = anchor.position - nameParent.position;
                    anchors[new Vector2Int(Mathf.RoundToInt(position.x / _catalog.TileSize), Mathf.RoundToInt(position.y / _catalog.TileSize))] = anchor;
                }
                foreach (KeyValuePair<Vector2Int, Transform> cell in anchors)
                {
                    if (!anchors.ContainsKey(cell.Key + Vector2Int.up)) AddEdge(prefab, cell.Value, Vector2Int.up, color, made);
                    if (!anchors.ContainsKey(cell.Key + Vector2Int.right)) AddEdge(prefab, cell.Value, Vector2Int.right, color, made);
                    if (!anchors.ContainsKey(cell.Key + Vector2Int.down)) AddEdge(prefab, cell.Value, Vector2Int.down, color, made);
                    if (!anchors.ContainsKey(cell.Key + Vector2Int.left)) AddEdge(prefab, cell.Value, Vector2Int.left, color, made);
                }
                if (visibleCells > 0) AddName(cluster, center / visibleCells, color, nameParent, made);
            }
        }

        /// <summary>
        /// anchor의 direction 쪽 바깥 변에 프레임 재질로 선을 놓는다.
        /// color를 적용하고 생성한 선 오브젝트를 made에 추가한다.
        /// </summary>
        private void AddEdge(GameObject prefab, Transform anchor, Vector2Int direction, Color color, List<GameObject> made)
        {
            GameObject edge = Instantiate(prefab, anchor, false);
            edge.name = "ClusterEdge_" + direction.x + "_" + direction.y;
            edge.transform.localScale = Vector3.one;
            SpriteRenderer sprite = edge.GetComponentInChildren<SpriteRenderer>(true);
            var line = edge.AddComponent<LineRenderer>();
            if (sprite != null)
            {
                line.sharedMaterial = sprite.sharedMaterial;
                sprite.enabled = false;
            }
            float half = _catalog.TileSize * 0.5f;
            float depth = -(_catalog.BuildingDepth + 0.02f);
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = _catalog.TileSize * 3f / 32f;
            line.endWidth = line.startWidth;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = _catalog.BuildingSignOrder + 1;
            line.SetPosition(0, direction.x == 0
                ? new Vector3(-half, direction.y * half, depth)
                : new Vector3(direction.x * half, -half, depth));
            line.SetPosition(1, direction.x == 0
                ? new Vector3(half, direction.y * half, depth)
                : new Vector3(direction.x * half, half, depth));
            made.Add(edge);
        }

        /// <summary>
        /// cluster의 이름을 item 프리팹의 월드 글자로 만들어 parent 아래 center에 배치한다.
        /// color를 적용하고 생성한 글자 오브젝트를 made에 추가한다.
        /// </summary>
        private void AddName(HabitatCluster cluster, Vector3 center, Color color, Transform parent, List<GameObject> made)
        {
            if (_catalog.ItemPiecePrefab == null) return;
            GameObject labelRoot = Instantiate(_catalog.ItemPiecePrefab, parent);
            labelRoot.name = "ClusterName_" + cluster.TypeId;
            labelRoot.transform.position = center + Vector3.back * (_catalog.BuildingDepth + 0.03f);
            SpriteRenderer sprite = labelRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null) sprite.enabled = false;
            TextMeshPro label = labelRoot.GetComponentInChildren<TextMeshPro>(true);
            if (label == null) { Destroy(labelRoot); return; }
            label.gameObject.SetActive(true);
            label.text = HabitatsInstaller.Catalog?.Get(cluster.TypeId)?.Name ?? cluster.TypeId;
            label.color = color;
            label.enableAutoSizing = false;
            label.fontSize = 34f;
            label.transform.localScale *= Mathf.Clamp(Mathf.Sqrt(cluster.Cells.Count) * 0.9f, 1.35f, 2f);
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = _catalog.BuildingSignOrder + 2;
            label.ForceMeshUpdate();
            made.Add(labelRoot);
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
            if (_clusters != null) { _clusters.HabitatCompleted -= OnClusterChanged; _clusters.HabitatBroken -= OnClusterChanged; }
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_instance == this) _instance = null;
        }
    }
}
