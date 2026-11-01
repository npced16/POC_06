using UnityEngine;

namespace IronRing.Presentation.Map
{
    /// <summary>
    /// 선로 화면 아트·프리팹 카탈로그(Assets/Prefabs/Map/MapCatalog.asset). 생성된 스프라이트 전체, 스프라이트 재질, 한글 라벨 폰트,
    /// 씬에 놓는 프리팹(TrackTile, HubBuilding, WaypointMarker, Train)을 Inspector에서 편집한다. BuildTopDownMain이 인스턴스를 놓을 때 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/Map Catalog")]
    public sealed class MapCatalog : ScriptableObject
    {
        [Header("스프라이트 (Art/Generated 전체, 이름으로 찾는다)")]
        [SerializeField] private Sprite[] _sprites;
        [SerializeField] private Material _spriteMaterial;
        [SerializeField] private Font _labelFont;
        [Header("프리팹 (Assets/Prefabs/Map)")]
        [SerializeField] private GameObject _trackTilePrefab;
        [SerializeField] private GameObject _hubBuildingPrefab;
        [SerializeField] private GameObject _waypointMarkerPrefab;
        [SerializeField] private GameObject _trainPrefab;

        public Material SpriteMaterial => _spriteMaterial;
        public Font LabelFont => _labelFont;
        public GameObject TrackTilePrefab => _trackTilePrefab;
        public GameObject HubBuildingPrefab => _hubBuildingPrefab;
        public GameObject WaypointMarkerPrefab => _waypointMarkerPrefab;
        public GameObject TrainPrefab => _trainPrefab;

        /// <summary>name과 일치하는 등록 스프라이트를 반환하며 없으면 null을 반환한다.</summary>
        public Sprite Sprite(string name)
        {
            if (_sprites == null) return null;
            foreach (Sprite sprite in _sprites) if (sprite != null && sprite.name == name) return sprite;
            return null;
        }
    }
}
