using System;

using UnityEngine;

namespace IronRing.Train.Equipment
{
    /// <summary>
    /// 스토리 034 표현 카탈로그(Resources/Train/TrainEquipmentCatalog.asset, 편집기 TrainEquipmentBuild.Generate가 채운다).
    /// 장비 id별 스프라이트, 그리드 칸 틀 스프라이트, EquipmentPiece·TrainCarGrid 프리팹, 거점 내부 칸 크기·정렬 순서·틀 색을 가진다.
    /// 거점 내부(TrainEquipmentView)와 습격 화면(RaidView)이 같이 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/Train Equipment Catalog")]
    public sealed class TrainEquipmentCatalog : ScriptableObject
    {
        /// <summary>Resources 경로(확장자 없음).</summary>
        public const string RESOURCE = "Train/TrainEquipmentCatalog";

        [SerializeField] private IconEntry[] _icons = Array.Empty<IconEntry>();
        [SerializeField] private Sprite _cellFrame;
        [SerializeField] private GameObject _piecePrefab;
        [SerializeField] private GameObject _gridPrefab;
        [Tooltip("거점 내부에서 장비 칸 한 변의 월드 길이.")]
        [SerializeField] private float _cellSize = 0.42f;
        [Tooltip("스냅 반경(칸 크기 배수).")]
        [SerializeField] private float _snapRadius = 1.0f;
        [Tooltip("장비 스프라이트가 칸을 채우는 비율.")]
        [SerializeField] private float _pieceFill = 0.95f;
        [SerializeField] private int _frameOrder = 21;
        [SerializeField] private int _pieceOrder = 22;
        [SerializeField] private int _ghostOrder = 23;
        [SerializeField] private Color _frameColor = new Color(1f, 0.85f, 0.45f, 0.9f);
        [Tooltip("장비 모드 되돌리기 기록 수.")]
        [SerializeField] private int _undoDepth = 8;
        [Tooltip("습격 화면에서 장비 스프라이트가 칸을 채우는 비율.")]
        [SerializeField] private float _raidPieceFill = 1.0f;
        [SerializeField] private int _raidPieceOrderOffset = 1;
        [Tooltip("장비 모드에서 열차 3량에 맞춰 당기는 카메라 직교 크기(작을수록 확대).")]
        [SerializeField] private float _equipmentCameraSize = 1.6f;
        [Tooltip("장비 모드 카메라 보간 속도(초당, 지수 감쇠).")]
        [SerializeField] private float _cameraLerpPerSecond = 8f;

        private static TrainEquipmentCatalog _cached;

        /// <summary>그리드 칸 틀 스프라이트.</summary>
        public Sprite CellFrame => _cellFrame;
        /// <summary>장비 조각 프리팹(SpriteRenderer 포함).</summary>
        public GameObject PiecePrefab => _piecePrefab;
        /// <summary>열차 칸 3×2 그리드 프리팹.</summary>
        public GameObject GridPrefab => _gridPrefab;
        /// <summary>거점 내부 칸 크기(월드).</summary>
        public float CellSize => Mathf.Max(0.05f, _cellSize);
        /// <summary>스냅 반경(칸 크기 배수).</summary>
        public float SnapRadius => _snapRadius;
        /// <summary>장비가 칸을 채우는 비율.</summary>
        public float PieceFill => _pieceFill;
        /// <summary>칸 틀 정렬 순서.</summary>
        public int FrameOrder => _frameOrder;
        /// <summary>장비 정렬 순서.</summary>
        public int PieceOrder => _pieceOrder;
        /// <summary>장비 고스트 정렬 순서.</summary>
        public int GhostOrder => _ghostOrder;
        /// <summary>칸 틀 색.</summary>
        public Color FrameColor => _frameColor;
        /// <summary>되돌리기 기록 수.</summary>
        public int UndoDepth => Mathf.Max(1, _undoDepth);
        /// <summary>습격 화면 장비 채움 비율.</summary>
        public float RaidPieceFill => _raidPieceFill;
        /// <summary>습격 화면 장비 정렬(열차 위 + 이 값).</summary>
        public int RaidPieceOrderOffset => _raidPieceOrderOffset;

        /// <summary>장비 모드 카메라 직교 크기.</summary>
        public float EquipmentCameraSize => Mathf.Max(0.3f, _equipmentCameraSize);
        /// <summary>장비 모드 카메라 보간 속도.</summary>
        public float CameraLerpPerSecond => Mathf.Max(0.1f, _cameraLerpPerSecond);

        /// <summary>id 장비 스프라이트. 없으면 null.</summary>
        public Sprite Icon(string id)
        {
            if (string.IsNullOrEmpty(id) || _icons == null) return null;
            foreach (IconEntry entry in _icons) if (entry != null && entry.Id == id) return entry.Sprite;
            return null;
        }

        /// <summary>편집기 제작 도구가 아이콘·틀·프리팹을 채운다.</summary>
        public void Configure(IconEntry[] icons, Sprite cellFrame, GameObject piecePrefab, GameObject gridPrefab)
        {
            _icons = icons; _cellFrame = cellFrame; _piecePrefab = piecePrefab; _gridPrefab = gridPrefab;
        }

        /// <summary>Resources에서 한 번 읽어 캐시한다. 없으면 null.</summary>
        public static TrainEquipmentCatalog Load()
        {
            if (_cached == null) _cached = Resources.Load<TrainEquipmentCatalog>(RESOURCE);
            return _cached;
        }

        /// <summary>장비 id와 스프라이트 쌍.</summary>
        [Serializable]
        public sealed class IconEntry
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite _sprite;

            /// <summary>id와 스프라이트로 만든다.</summary>
            public IconEntry(string id, Sprite sprite) { _id = id; _sprite = sprite; }
            /// <summary>장비 id.</summary>
            public string Id => _id;
            /// <summary>스프라이트.</summary>
            public Sprite Sprite => _sprite;
        }
    }
}
