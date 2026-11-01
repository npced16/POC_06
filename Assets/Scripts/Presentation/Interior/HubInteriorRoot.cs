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
        [SerializeField] private Transform _trainAnchor;

        /// <summary>이 내부가 속한 거점 id(hub-1..hub-4).</summary>
        public string HubId => _hubId;
        /// <summary>타일·조각 카탈로그.</summary>
        public InteriorCatalog Catalog => _catalog;
        /// <summary>이 내부를 비출 때의 직교 카메라 크기.</summary>
        public float CameraSize => _cameraSize;
        /// <summary>열차(기관차) 앵커. 014 NPC가 주변을 돌아다니는 기준.</summary>
        public Transform TrainAnchor => _trainAnchor != null ? _trainAnchor : transform;

        /// <summary>자식 칸 전부(비활성 포함).</summary>
        public InteriorCellView[] Cells() => GetComponentsInChildren<InteriorCellView>(true);

        /// <summary>프리팹 제작 도구가 카탈로그, 카메라 크기, 열차 앵커를 연결한다.</summary>
        public void Configure(InteriorCatalog catalog, float cameraSize, Transform trainAnchor)
        {
            _catalog = catalog;
            _cameraSize = cameraSize;
            _trainAnchor = trainAnchor;
        }

        /// <summary>씬 인스턴스의 거점 id를 루트와 모든 칸에 덮어쓴다.</summary>
        public void ApplyHubId(string hubId)
        {
            _hubId = hubId;
            foreach (InteriorCellView cell in Cells()) cell.SetHubId(hubId);
        }
    }
}
