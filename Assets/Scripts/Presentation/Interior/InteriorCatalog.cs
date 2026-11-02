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
        [Header("건물 (스토리 027)")]
        [Tooltip("지붕 조각 프리팹(SpriteRenderer + 트리거 BoxCollider2D + BuildingPiece).")]
        [SerializeField] private GameObject _buildingPiecePrefab;
        [Tooltip("문·굴뚝·상징물·오목 모서리 프리팹(SpriteRenderer).")]
        [SerializeField] private GameObject _buildingPropPrefab;
        [SerializeField] private GameObject _buildingSmokePrefab;
        [SerializeField] private BuildingStyleSet[] _buildingStyles = Array.Empty<BuildingStyleSet>();
        [SerializeField] private Sprite[] _buildingDoors = Array.Empty<Sprite>();
        [SerializeField] private Sprite _buildingChimney;
        [SerializeField] private FacilitySprite[] _buildingSigns = Array.Empty<FacilitySprite>();
        [SerializeField] private int _buildingOrder = 12;
        [SerializeField] private int _buildingDoorOrder = 13;
        [SerializeField] private int _buildingSignOrder = 14;
        [Tooltip("완성 연출: 지붕이 아래에서 올라오는 시간(초). 0이면 연출 없음.")]
        [SerializeField] private float _buildingRiseSeconds = 0.3f;
        [SerializeField] private float _buildingRiseDistance = 0.3f;
        [Tooltip("스토리 031. 철거·깨짐 연출: 조각이 내려앉으며 사라지는 시간(초). 0이면 바로 사라진다.")]
        [SerializeField] private float _buildingCollapseSeconds = 0.25f;
        [Tooltip("스토리 031. 철거 연출에서 내려앉는 거리(월드 단위).")]
        [SerializeField] private float _buildingCollapseDistance = 0.3f;
        [Tooltip("스토리 031. Ctrl+Z로 되돌릴 수 있는 최근 동작 수.")]
        [SerializeField] private int _undoDepth = 5;
        [Tooltip("스토리 031. 되돌릴 때 \"되돌림: <동작>\" 토스트를 띄울지.")]
        [SerializeField] private bool _undoToast = true;
        [SerializeField] private float _buildingSignScale = 0.8f;
        [Tooltip("상징물을 칸 중심에서 위로 올리는 양(타일 배수).")]
        [SerializeField] private float _signLift = 0.1f;
        [Tooltip("굴뚝 위치: 맨 윗줄 오른쪽 칸 중심에서의 오프셋(타일 배수).")]
        [SerializeField] private Vector2 _chimneyOffset = new Vector2(0.28f, 0.2f);
        [SerializeField] private Vector2 _smokeOffset = new Vector2(0f, 0.45f);
        [Tooltip("QA 후속(건물 장식). 정면 창 무늬 덧조각(무늬 1 = [0], 무늬 2 = [1]). 무늬 0은 지붕 조각의 기본 창.")]
        [SerializeField] private Sprite[] _buildingWindows = Array.Empty<Sprite>();
        [Tooltip("QA 후속(건물 장식). 지붕 위 덧소품(id = HabitatTypes.json _decor._props).")]
        [SerializeField] private FacilitySprite[] _buildingDecor = Array.Empty<FacilitySprite>();
        [Tooltip("QA 후속(건물 장식). 덧소품 크기 배율.")]
        [SerializeField] private float _buildingDecorScale = 0.6f;
        [Tooltip("QA 후속(건물 장식). 덧소품을 칸 중심에서 위로 올리는 양(타일 배수).")]
        [SerializeField] private float _decorLift = 0.08f;
        [Tooltip("건물 조각 z 오프셋(+면 칸 콜라이더 뒤: 칸 클릭 레이캐스트가 칸을 먼저 맞춘다).")]
        [SerializeField] private float _buildingDepth = 0.05f;
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

        /// <summary>칸 state에 보이는 타일. ItemPlaced는 빈 터 타일 위에 013 조각이 놓인다.</summary>
        public Sprite CellSprite(InteriorCellState state)
        {
            switch (state)
            {
                case InteriorCellState.Rubble: return _rubbleSprite;
                case InteriorCellState.FacilityPlot: return _plotSprite;
                default: return _emptySprite;
            }
        }


        /// <summary>서식지 종류 id에 고정으로 대응하는 묶음 색(문자열 해시로 색 목록에서 고른다).</summary>
        public Color ClusterColor(string typeId)
        {
            if (_clusterColors == null || _clusterColors.Length == 0) return Color.white;
            int hash = 17;
            foreach (char c in typeId ?? "") hash = unchecked(hash * 31 + c);
            return _clusterColors[(hash & 0x7fffffff) % _clusterColors.Length];
        }

        /// <summary>스토리 027. 건물 지붕 조각 프리팹.</summary>
        public GameObject BuildingPiecePrefab => _buildingPiecePrefab;
        /// <summary>스토리 027. 문·굴뚝·상징물 프리팹.</summary>
        public GameObject BuildingPropPrefab => _buildingPropPrefab;
        /// <summary>스토리 027. 굴뚝 연기 프리팹(없으면 연기 없음).</summary>
        public GameObject BuildingSmokePrefab => _buildingSmokePrefab;
        public Sprite BuildingChimney => _buildingChimney;
        public int BuildingOrder => _buildingOrder;
        public int BuildingDoorOrder => _buildingDoorOrder;
        public int BuildingSignOrder => _buildingSignOrder;
        public float BuildingRiseSeconds => Mathf.Max(0f, _buildingRiseSeconds);
        public float BuildingRiseDistance => _buildingRiseDistance;
        /// <summary>스토리 031. 철거 연출 시간(초, 0 이상).</summary>
        public float BuildingCollapseSeconds => Mathf.Max(0f, _buildingCollapseSeconds);
        /// <summary>스토리 031. 철거 연출에서 내려앉는 거리.</summary>
        public float BuildingCollapseDistance => _buildingCollapseDistance;
        /// <summary>스토리 031. 되돌리기 기록 한도(1 이상).</summary>
        public int UndoDepth => Mathf.Max(1, _undoDepth);
        /// <summary>스토리 031. 되돌림 토스트를 띄울지.</summary>
        public bool UndoToast => _undoToast;
        public float BuildingSignScale => _buildingSignScale;
        public float SignLift => _signLift;
        public Vector2 ChimneyOffset => _chimneyOffset;
        public Vector2 SmokeOffset => _smokeOffset;
        public float BuildingDepth => _buildingDepth;
        /// <summary>건물 조각 프리팹과 지붕 스프라이트가 있으면 true(이때 ClusterFrameView는 테두리 대신 건물에 맡긴다).</summary>
        public bool HasBuildings => _buildingPiecePrefab != null && _buildingPropPrefab != null && _buildingStyles != null && _buildingStyles.Length > 0
            && _buildingStyles[0] != null && _buildingStyles[0].Piece(15) != null;

        /// <summary>style 계열(없으면 첫 계열)의 이웃 마스크(1=N,2=E,4=S,8=W) 지붕 조각.</summary>
        public Sprite RoofSprite(string style, int mask) => Style(style)?.Piece(mask);
        /// <summary>style 계열의 오목 모서리 조각(0 ne, 1 se, 2 sw, 3 nw).</summary>
        public Sprite InnerCornerSprite(string style, int corner) => Style(style)?.Corner(corner);
        /// <summary>문 스프라이트(variant를 문 수로 나눈 나머지).</summary>
        public Sprite BuildingDoor(int variant) => _buildingDoors == null || _buildingDoors.Length == 0 ? null : _buildingDoors[Mathf.Abs(variant) % _buildingDoors.Length];
        /// <summary>상징물 id의 스프라이트, 없으면 null.</summary>
        public Sprite BuildingSign(string id)
        {
            foreach (FacilitySprite entry in _buildingSigns ?? Array.Empty<FacilitySprite>()) if (entry != null && entry.Id == id) return entry.Sprite;
            return null;
        }

        /// <summary>QA 후속(건물 장식). 색조 tint가 입혀진 style 계열의 지붕 조각. 색조 계열이 없으면 기본 계열로 돌아간다.</summary>
        public Sprite RoofSprite(string style, string tint, int mask) => TintedStyle(style, tint)?.Piece(mask) ?? RoofSprite(style, mask);
        /// <summary>QA 후속(건물 장식). 색조 tint가 입혀진 오목 모서리 조각(없으면 기본 계열).</summary>
        public Sprite InnerCornerSprite(string style, string tint, int corner) => TintedStyle(style, tint)?.Corner(corner) ?? InnerCornerSprite(style, corner);
        /// <summary>QA 후속(건물 장식). 창 무늬(1부터) 덧조각, 0이거나 없으면 null.</summary>
        public Sprite BuildingWindow(int pattern) => pattern < 1 || _buildingWindows == null || pattern > _buildingWindows.Length ? null : _buildingWindows[pattern - 1];
        /// <summary>QA 후속(건물 장식). 덧소품 id의 스프라이트, 없으면 null.</summary>
        public Sprite BuildingDecor(string id)
        {
            foreach (FacilitySprite entry in _buildingDecor ?? Array.Empty<FacilitySprite>()) if (entry != null && entry.Id == id) return entry.Sprite;
            return null;
        }
        /// <summary>QA 후속(건물 장식). 덧소품 크기 배율.</summary>
        public float BuildingDecorScale => _buildingDecorScale;
        /// <summary>QA 후속(건물 장식). 덧소품 높이(타일 배수).</summary>
        public float DecorLift => _decorLift;
        /// <summary>QA 후속(건물 장식). 색조 계열 id("style@tint"; tint가 비었거나 base면 style 그대로). 생성기·카탈로그가 같은 규칙을 쓴다.</summary>
        public static string TintedStyleId(string style, string tint) => string.IsNullOrEmpty(tint) || tint == "base" ? style : style + "@" + tint;

        private BuildingStyleSet TintedStyle(string style, string tint)
        {
            if (string.IsNullOrEmpty(tint) || tint == "base" || _buildingStyles == null) return null;
            string id = TintedStyleId(style, tint);
            foreach (BuildingStyleSet set in _buildingStyles) if (set != null && set.Id == id) return set;
            return null;
        }

        /// <summary>QA 후속(건물 장식). 편집기 제작 도구(Story012Build)가 창 무늬·덧소품 표를 채운다.</summary>
        public void SetBuildingDecor(Sprite[] windows, FacilitySprite[] decor)
        {
            _buildingWindows = windows ?? Array.Empty<Sprite>();
            _buildingDecor = decor ?? Array.Empty<FacilitySprite>();
        }

        private BuildingStyleSet Style(string style)
        {
            if (_buildingStyles == null || _buildingStyles.Length == 0) return null;
            foreach (BuildingStyleSet set in _buildingStyles) if (set != null && set.Id == style) return set;
            return _buildingStyles[0];
        }

        /// <summary>편집기 제작 도구(Story012Build)가 건물 그림 표를 채운다.</summary>
        public void SetBuildingArt(BuildingStyleSet[] styles, Sprite[] doors, Sprite chimney, FacilitySprite[] signs)
        {
            _buildingStyles = styles ?? Array.Empty<BuildingStyleSet>();
            _buildingDoors = doors ?? Array.Empty<Sprite>();
            _buildingChimney = chimney;
            _buildingSigns = signs ?? Array.Empty<FacilitySprite>();
        }

        /// <summary>스토리 027. 지붕 계열 하나: 이웃 마스크 16조각과 오목 모서리 4조각.</summary>
        [Serializable]
        public sealed class BuildingStyleSet
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite[] _pieces = new Sprite[16];
            [SerializeField] private Sprite[] _innerCorners = new Sprite[4];
            public BuildingStyleSet() { }
            public BuildingStyleSet(string id, Sprite[] pieces, Sprite[] innerCorners) { _id = id; _pieces = pieces; _innerCorners = innerCorners; }
            public string Id => _id;
            public Sprite Piece(int mask) => _pieces != null && mask >= 0 && mask < _pieces.Length ? _pieces[mask] : null;
            public Sprite Corner(int corner) => _innerCorners != null && corner >= 0 && corner < _innerCorners.Length ? _innerCorners[corner] : null;
        }

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
