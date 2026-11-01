using System;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 거점 내부 아트·프리팹 카탈로그(Assets/Prefabs/Interior/InteriorCatalog.asset). 칸 상태별 타일, 열차·선로·바닥 스프라이트,
    /// 동적 조각 프리팹(013 아이템, 014 시설·NPC)과 최초 배치 기본값을 Inspector에서 편집한다. 씬에 놓인 인스턴스의 위치·크기는 씬 값이 우선한다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/Interior Catalog")]
    public sealed class InteriorCatalog : ScriptableObject
    {
        [Header("칸 타일 (상태별)")]
        [SerializeField] private Sprite _rubbleSprite;
        [SerializeField] private Sprite _emptySprite;
        [SerializeField] private Sprite _plotSprite;
        [SerializeField] private Sprite _builtSprite;
        [Header("정적 조각 스프라이트와 재질")]
        [SerializeField] private Sprite _trainSprite;
        [SerializeField] private Sprite _trackSprite;
        [SerializeField] private Sprite _floorSprite;
        [SerializeField] private Material _spriteMaterial;
        [Header("동적 조각 프리팹 (ItemSlot/FacilitySlot 아래에 인스턴스화)")]
        [SerializeField] private GameObject _itemPiecePrefab;
        [SerializeField] private GameObject _facilityPiecePrefab;
        [SerializeField] private GameObject _npcPiecePrefab;
        [Header("배치 고스트·서식지 묶음 (스토리 015)")]
        [SerializeField] private GameObject _ghostPiecePrefab;
        [SerializeField] private GameObject _clusterFramePrefab;
        [SerializeField] private Color _ghostValidColor = new Color(0.45f, 1f, 0.5f, 0.6f);
        [SerializeField] private Color _ghostInvalidColor = new Color(1f, 0.35f, 0.3f, 0.6f);
        [Tooltip("마우스가 칸 중심에서 이 거리(타일 크기 배수) 안이면 그 칸에 스냅한다.")]
        [SerializeField] private float _snapRadius = 0.75f;
        [SerializeField] private Color[] _clusterColors =
        {
            new Color(0.55f, 0.9f, 0.45f, 0.9f), new Color(0.4f, 0.7f, 1f, 0.9f), new Color(1f, 0.8f, 0.35f, 0.9f),
            new Color(0.95f, 0.5f, 0.8f, 0.9f), new Color(0.6f, 0.95f, 0.9f, 0.9f), new Color(1f, 0.55f, 0.35f, 0.9f)
        };
        [SerializeField] private FacilitySprite[] _facilitySprites = Array.Empty<FacilitySprite>();
        [Header("최초 배치 기본값 (프리팹·씬 생성 시에만 사용)")]
        [SerializeField] private Vector3 _defaultOrigin = new Vector3(1000, 0, 0);
        [SerializeField] private Vector3 _defaultSpacing = new Vector3(200, 0, 0);
        [SerializeField] private float _defaultCameraSize = 5.5f;
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private int _floorMarginColumns = 3;
        [SerializeField] private int _floorMarginRows = 1;
        [SerializeField] private int _trainCars = 3;
        [SerializeField] private float _trainCarSpacing = 1.3f;
        [SerializeField] private float _trainScale = 1.25f;
        [SerializeField] private float _trainForwardAngle = 90f;
        [Header("정렬 순서")]
        [SerializeField] private int _floorOrder = 0;
        [SerializeField] private int _trackOrder = 1;
        [SerializeField] private int _cellOrder = 2;
        [SerializeField] private int _pieceOrder = 10;
        [SerializeField] private int _trainOrder = 20;

        public Sprite RubbleSprite => _rubbleSprite;
        public Sprite EmptySprite => _emptySprite;
        public Sprite PlotSprite => _plotSprite;
        public Sprite BuiltSprite => _builtSprite;
        public Sprite TrainSprite => _trainSprite;
        public Sprite TrackSprite => _trackSprite;
        public Sprite FloorSprite => _floorSprite;
        public Material SpriteMaterial => _spriteMaterial;
        public GameObject ItemPiecePrefab => _itemPiecePrefab;
        public GameObject FacilityPiecePrefab => _facilityPiecePrefab;
        public GameObject NpcPiecePrefab => _npcPiecePrefab;
        /// <summary>배치 고스트 프리팹(SpriteRenderer, 반투명 재질).</summary>
        public GameObject GhostPiecePrefab => _ghostPiecePrefab;
        /// <summary>완성 서식지 칸을 묶어 보이는 테두리 프리팹.</summary>
        public GameObject ClusterFramePrefab => _clusterFramePrefab;
        public Color GhostValidColor => _ghostValidColor;
        public Color GhostInvalidColor => _ghostInvalidColor;
        public float SnapRadius => _snapRadius;
        public Vector3 DefaultOrigin => _defaultOrigin;
        public Vector3 DefaultSpacing => _defaultSpacing;
        public float DefaultCameraSize => _defaultCameraSize;
        public float TileSize => _tileSize;
        public int FloorMarginColumns => _floorMarginColumns;
        public int FloorMarginRows => _floorMarginRows;
        public int TrainCars => _trainCars;
        public float TrainCarSpacing => _trainCarSpacing;
        public float TrainScale => _trainScale;
        public float TrainForwardAngle => _trainForwardAngle;
        public int FloorOrder => _floorOrder;
        public int TrackOrder => _trackOrder;
        public int CellOrder => _cellOrder;
        public int PieceOrder => _pieceOrder;
        public int TrainOrder => _trainOrder;

        /// <summary>칸 state에 보이는 타일. ItemPlaced는 빈 터 타일 위에 013 조각이, FacilityBuilt는 built 타일 위에 014 조각이 놓인다.</summary>
        public Sprite CellSprite(InteriorCellState state)
        {
            switch (state)
            {
                case InteriorCellState.Rubble: return _rubbleSprite;
                case InteriorCellState.FacilityPlot: return _plotSprite;
                case InteriorCellState.FacilityBuilt: return _builtSprite != null ? _builtSprite : _plotSprite;
                default: return _emptySprite;
            }
        }

        /// <summary>시설 id의 아이콘·고스트 스프라이트, 없으면 null.</summary>
        public Sprite FacilityIcon(string facilityId)
        {
            foreach (FacilitySprite entry in _facilitySprites) if (entry != null && entry.Id == facilityId) return entry.Sprite;
            return null;
        }

        /// <summary>서식지 종류 id에 고정으로 대응하는 묶음 색(문자열 해시로 색 목록에서 고른다).</summary>
        public Color ClusterColor(string typeId)
        {
            if (_clusterColors == null || _clusterColors.Length == 0) return Color.white;
            int hash = 17;
            foreach (char c in typeId ?? "") hash = unchecked(hash * 31 + c);
            return _clusterColors[(hash & 0x7fffffff) % _clusterColors.Length];
        }

        /// <summary>편집기 제작 도구가 시설 아이콘 표를 채운다.</summary>
        public void SetFacilitySprites(FacilitySprite[] entries) => _facilitySprites = entries ?? Array.Empty<FacilitySprite>();

        /// <summary>시설 id와 스프라이트 한 쌍.</summary>
        [Serializable]
        public sealed class FacilitySprite
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite _sprite;
            public FacilitySprite() { }
            public FacilitySprite(string id, Sprite sprite) { _id = id; _sprite = sprite; }
            public string Id => _id;
            public Sprite Sprite => _sprite;
        }

        void OnValidate()
        {
            _tileSize = Mathf.Max(0.1f, _tileSize);
            _snapRadius = Mathf.Max(0.1f, _snapRadius);
            _defaultCameraSize = Mathf.Max(1f, _defaultCameraSize);
            _trainCars = Mathf.Max(1, _trainCars);
            _floorMarginColumns = Mathf.Max(0, _floorMarginColumns);
            _floorMarginRows = Mathf.Max(0, _floorMarginRows);
        }
    }
}
