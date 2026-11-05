using UnityEngine;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 씬에 놓인 내부 칸 프리팹(Assets/Prefabs/Interior/InteriorCell.prefab) 인스턴스. 칸 식별(거점 id, 색인, 줄·칸, 위/아래)과
    /// 타일 렌더러, 동적 조각 자리(ItemSlot, FacilitySlot)를 가진다. 런타임은 HubInteriorView가 찾아 바인딩하고 타일만 바꾼다.
    /// 거점 id가 비어 있으면 부모 HubInteriorRoot의 id를 쓴다.
    /// </summary>
    public sealed class InteriorCellView : MonoBehaviour
    {
        [Header("칸 식별 (HubInterior 인스턴스마다 덮어쓴다)")]
        [SerializeField] private string _hubId;
        [SerializeField] private int _index;
        [SerializeField] private int _row;
        [SerializeField] private int _column;
        [SerializeField] private bool _isUpper;
        [Header("조각 자리")]
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Transform _itemSlot;
        [SerializeField] private Transform _facilitySlot;
        [Header("그리드 선")]
        [SerializeField] private SpriteRenderer _buildableMarker;
        [SerializeField] private Color _placeableLineColor = new Color32(236, 231, 203, 245);
        [SerializeField] private Color _usedLineColor = new Color32(123, 129, 131, 180);
        private string _resolvedHubId;

        /// <summary>이 칸이 속한 거점 id. 직접 값이 없으면 부모 HubInteriorRoot에서 읽는다.</summary>
        public string HubId
        {
            get
            {
                if (!string.IsNullOrEmpty(_hubId)) return _hubId;
                if (_resolvedHubId == null) _resolvedHubId = GetComponentInParent<HubInteriorRoot>()?.HubId ?? "";
                return _resolvedHubId;
            }
        }
        /// <summary>IInteriorCell.Index와 같은 색인(0..35).</summary>
        public int Index => _index;
        public int Row => _row;
        public int Column => _column;
        public bool IsUpper => _isUpper;
        /// <summary>013이 아이템 조각을 놓는 자리.</summary>
        public Transform ItemSlot => _itemSlot != null ? _itemSlot : transform;
        /// <summary>014가 시설 조각을 놓는 자리.</summary>
        public Transform FacilitySlot => _facilitySlot != null ? _facilitySlot : transform;

        /// <summary>프리팹 제작 도구가 칸 식별을 기록한다.</summary>
        public void Configure(int index, int row, int column, bool isUpper)
        {
            _index = index;
            _row = row;
            _column = column;
            _isUpper = isUpper;
        }

        /// <summary>프리팹 제작 도구가 타일 렌더러와 조각 자리를 연결한다.</summary>
        public void BindParts(SpriteRenderer renderer, Transform itemSlot, Transform facilitySlot)
        {
            _renderer = renderer;
            _itemSlot = itemSlot;
            _facilitySlot = facilitySlot;
        }

        /// <summary>marker를 칸 그리드 렌더러로 저장해 SetBuildable에서 선 색을 바꿀 수 있게 한다.</summary>
        public void BindBuildableMarker(SpriteRenderer marker) => _buildableMarker = marker;

        // 스펙 001: 온보딩 칸 강조·흐림이 색을 바꿀 수 있도록 타일·그리드 선 렌더러를 읽기 전용으로 공개.
        public SpriteRenderer TileRenderer => _renderer;
        public SpriteRenderer BuildableMarker => _buildableMarker;

        /// <summary>buildable 상태에 따라 _buildableMarker의 선 색을 밝은 설치 가능 색 또는 흐린 사용 중 색으로 바꾸고 그리드를 표시한다.</summary>
        public void SetBuildable(bool buildable)
        {
            if (_buildableMarker == null) return;
            _buildableMarker.color = buildable ? _placeableLineColor : _usedLineColor;
            _buildableMarker.enabled = true;
        }

        /// <summary>씬 인스턴스마다 거점 id를 덮어쓴다.</summary>
        public void SetHubId(string hubId) { _hubId = hubId; _resolvedHubId = null; }

        /// <summary>타일 스프라이트를 바꾼다(칸 상태 변화 시 HubInteriorView가 호출).</summary>
        public void SetSprite(Sprite sprite)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null) _renderer.sprite = sprite;
        }
    }
}
