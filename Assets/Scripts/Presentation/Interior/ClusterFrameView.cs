using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 015. 완성된 서식지(IHabitatClusters, 스토리 016)의 칸마다 InteriorCatalog의 ClusterFrame 프리팹을 칸 앵커 아래에 인스턴스화해
    /// 묶음을 테두리로 보인다. 색은 서식지 종류별 카탈로그 색이다. HabitatCompleted/HabitatBroken과 내부 화면 열림 때 그 거점만 다시 그린다.
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

        /// <summary>hubId의 테두리를 모두 지우고 지금 완성된 서식지의 칸마다 다시 놓는다.</summary>
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
                if (cluster?.Cells == null) continue;
                Color color = _catalog.ClusterColor(cluster.TypeId);
                foreach (int cellIndex in cluster.Cells)
                {
                    Transform anchor = _anchors.CellAnchor(hubId, cellIndex);
                    if (anchor == null) continue;
                    GameObject frame = Instantiate(prefab, anchor, false);
                    frame.name = "ClusterFrame_" + cluster.TypeId;
                    SpriteRenderer renderer = frame.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer != null) renderer.color = color;
                    made.Add(frame);
                }
            }
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
