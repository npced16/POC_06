using UnityEngine;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 씬에 놓인 거점 내부 프리팹(Assets/Prefabs/Interior/HubInterior.prefab) 인스턴스의 루트. 거점 id, 카탈로그, 카메라 크기,
    /// 열차 앵커를 가지며 자식의 InteriorCellView 36개가 칸이다. 위치·크기는 Hierarchy/Inspector에서 자유롭게 편집한다.
    /// </summary>
    public sealed class HubInteriorRoot : MonoBehaviour
    {
        [Header("거점 (인스턴스마다 덮어쓴다)")]
        [SerializeField] private string _hubId;
        [Header("연출")]
        [SerializeField] private InteriorCatalog _catalog;
        [SerializeField] private float _cameraSize = 5.5f;
        [SerializeField] private float _cameraMinSize = 3.5f;
        [SerializeField] private float _cameraMaxSize = 8f;
        [SerializeField] private float _cameraZoomStep = 0.5f;
        [SerializeField] private Transform _cameraAnchor;
        [SerializeField] private Transform _trainAnchor;

        /// <summary>이 내부가 속한 거점 id(hub-1..hub-4).</summary>
        public string HubId => _hubId;
        /// <summary>타일·조각 카탈로그.</summary>
        public InteriorCatalog Catalog => _catalog;
        /// <summary>이 내부를 비출 때의 직교 카메라 크기.</summary>
        public float CameraSize => _cameraSize;
        /// <summary>이 마을에서 휠로 확대할 수 있는 최소 직교 카메라 크기를 반환한다.</summary>
        public float CameraMinSize => _cameraMinSize;
        /// <summary>이 마을에서 휠로 축소할 수 있는 최대 직교 카메라 크기를 반환한다.</summary>
        public float CameraMaxSize => _cameraMaxSize;
        /// <summary>이 마을의 휠 한 번에 적용할 직교 카메라 크기 변화량을 반환한다.</summary>
        public float CameraZoomStep => _cameraZoomStep;
        /// <summary>마을을 비출 카메라의 중심 기준점이며, 지정하지 않으면 마을 루트를 반환한다.</summary>
        public Transform CameraAnchor => _cameraAnchor != null ? _cameraAnchor : transform;
        /// <summary>열차(기관차) 앵커. 014 NPC가 주변을 돌아다니는 기준.</summary>
        public Transform TrainAnchor => _trainAnchor != null ? _trainAnchor : transform;

        /// <summary>자식 칸 전부(비활성 포함).</summary>
        public InteriorCellView[] Cells() => GetComponentsInChildren<InteriorCellView>(true);

        /// <summary>프리팹 제작 도구가 catalog, cameraSize, trainAnchor, cameraAnchor를 받아 마을 표시 설정과 두 기준점을 저장한다.</summary>
        public void Configure(InteriorCatalog catalog, float cameraSize, Transform trainAnchor, Transform cameraAnchor)
        {
            _catalog = catalog;
            _cameraSize = cameraSize;
            _trainAnchor = trainAnchor;
            _cameraAnchor = cameraAnchor;
        }

        /// <summary>씬 인스턴스의 거점 id를 루트와 모든 칸에 덮어쓴다.</summary>
        public void ApplyHubId(string hubId)
        {
            _hubId = hubId;
            foreach (InteriorCellView cell in Cells()) cell.SetHubId(hubId);
        }

        void OnValidate()
        {
            _cameraMinSize = Mathf.Max(0.01f, _cameraMinSize);
            _cameraMaxSize = Mathf.Max(_cameraMinSize, _cameraMaxSize);
            _cameraZoomStep = Mathf.Max(0.01f, _cameraZoomStep);
        }
    }
}
