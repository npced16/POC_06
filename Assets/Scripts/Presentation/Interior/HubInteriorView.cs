using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Interior;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 012. 거점 내부 탑뷰의 런타임 바인더. 씬에 놓인 HubInterior 프리팹 인스턴스(HubInteriorRoot + InteriorCellView 36개)를 찾아
    /// 앵커 표를 만들고, 칸 상태가 바뀌면 카탈로그 타일만 바꾼다. 정적 배치(바닥·선로·열차·칸)는 런타임에 만들지 않고 씬/프리팹에서 편집한다.
    /// IViewMode가 열리면 IMapAnchors.MapCamera를 그 인스턴스 위치로 옮기고 닫히면 되돌린다. 칸 클릭은 씬 칸의 Collider2D 레이캐스트로 잡아
    /// IInteriorCellClickSource로 알린다. IInteriorAnchors·IInteriorNavigation도 구현한다. Bootstrap이 Create로 만들고 등록한다.
    /// </summary>
    public sealed class HubInteriorView : MonoBehaviour, IInteriorAnchors, IInteriorCellClickSource, IInteriorNavigation
    {
        [Header("칸 클릭")]
        [SerializeField] private LayerMask _cellLayers = -1;
        private IHubInterior _interior;
        private InteriorViewMode _viewMode;
        private IMapAnchors _anchors;
        private readonly Dictionary<string, HubArea> _areas = new Dictionary<string, HubArea>(StringComparer.Ordinal);
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private Vector3 _mapCameraPosition;
        private float _mapCameraSize;
        private bool _cameraMoved;

        /// <summary>hubId, cellIndex. 열린 내부 화면에서 UI 위가 아닌 칸을 왼쪽 클릭했을 때.</summary>
        public event Action<string, int> CellClicked;

        /// <summary>바인더 오브젝트를 만들고 interior, hubIds, viewMode(상태 소유자), anchors로 씬 인스턴스에 연결해 돌려준다.</summary>
        public static HubInteriorView Create(IHubInterior interior, IReadOnlyList<string> hubIds, InteriorViewMode viewMode, IMapAnchors anchors)
        {
            var view = new GameObject(nameof(HubInteriorView)).AddComponent<HubInteriorView>();
            view.Initialize(interior, hubIds, viewMode, anchors);
            return view;
        }

        /// <summary>IInteriorNavigation: hubId의 내부 화면을 연다(정차 시 자동 열림과 같은 경로). 이미 열려 있으면 무시.</summary>
        public void Open(string hubId) => _viewMode.Open(hubId);

        /// <summary>IInteriorNavigation: 내부 화면을 닫고 선로 화면으로 돌아간다(출발 시와 같은 경로).</summary>
        public void Close() => _viewMode.Close();

        /// <summary>씬의 HubInteriorRoot 인스턴스를 거점 id로 찾아 바인딩하고 칸 변경과 화면 전환을 구독한다.</summary>
        private void Initialize(IHubInterior interior, IReadOnlyList<string> hubIds, InteriorViewMode viewMode, IMapAnchors anchors)
        {
            _interior = interior ?? throw new ArgumentNullException(nameof(interior));
            _viewMode = viewMode ?? throw new ArgumentNullException(nameof(viewMode));
            _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
            if (hubIds == null) throw new ArgumentNullException(nameof(hubIds));
            HubInteriorRoot[] roots = UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (string hubId in hubIds)
            {
                HubInteriorRoot root = Array.Find(roots, candidate => candidate.HubId == hubId);
                if (root == null)
                {
                    UnityEngine.Debug.LogError($"HubInteriorView: Main 씬에 거점 '{hubId}'의 HubInterior 프리팹 인스턴스가 없습니다. IronRing.Presentation.Interior.Editor.Story012Build.Generate를 실행하세요.");
                    continue;
                }
                HubArea area = Bind(root, hubId);
                if (area != null) _areas.Add(hubId, area);
            }
            _interior.CellChanged += OnCellChanged;
            _viewMode.InteriorChanged += OnInteriorChanged;
            if (_viewMode.InteriorOpen) OnInteriorChanged(true);
        }

        /// <summary>root의 칸 인스턴스를 색인으로 정렬해 검증하고 현재 상태 타일을 입힌 구역을 돌려준다. 칸이 빠지거나 겹치면 null.</summary>
        private HubArea Bind(HubInteriorRoot root, string hubId)
        {
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(hubId);
            if (root.Catalog == null)
            {
                UnityEngine.Debug.LogError($"HubInteriorView: '{root.name}'에 InteriorCatalog가 연결되지 않았습니다.");
                return null;
            }
            var views = new InteriorCellView[cells.Count];
            foreach (InteriorCellView view in root.Cells())
            {
                if (view.Index < 0 || view.Index >= views.Length || views[view.Index] != null)
                {
                    UnityEngine.Debug.LogError($"HubInteriorView: '{root.name}'의 칸 색인 {view.Index}이(가) 범위 밖이거나 중복입니다(칸 {cells.Count}개 필요).");
                    return null;
                }
                views[view.Index] = view;
            }
            for (int index = 0; index < views.Length; index++)
            {
                if (views[index] == null)
                {
                    UnityEngine.Debug.LogError($"HubInteriorView: '{root.name}'에 칸 색인 {index}이(가) 없습니다.");
                    return null;
                }
                views[index].SetSprite(root.Catalog.CellSprite(cells[index].State));
            }
            return new HubArea { Root = root, Cells = views };
        }

        /// <summary>hubId의 cellIndex 칸 앵커(씬 칸 오브젝트). 내부 맵이나 칸이 없으면 null.</summary>
        public Transform CellAnchor(string hubId, int cellIndex)
        {
            if (hubId == null || !_areas.TryGetValue(hubId, out HubArea area)) return null;
            return cellIndex >= 0 && cellIndex < area.Cells.Length ? area.Cells[cellIndex].transform : null;
        }

        /// <summary>hubId 내부 맵의 열차 앵커. 없으면 null.</summary>
        public Transform TrainAnchor(string hubId) => hubId != null && _areas.TryGetValue(hubId, out HubArea area) ? area.Root.TrainAnchor : null;

        /// <summary>hubId 내부 맵의 중심(인스턴스 루트 위치). 모르는 거점이면 Vector3.zero.</summary>
        public Vector3 Center(string hubId) => hubId != null && _areas.TryGetValue(hubId, out HubArea area) ? area.Root.transform.position : Vector3.zero;

        /// <summary>바뀐 칸의 타일을 현재 상태 스프라이트로 교체한다.</summary>
        private void OnCellChanged(string hubId, int cellIndex)
        {
            if (!_areas.TryGetValue(hubId, out HubArea area) || cellIndex < 0 || cellIndex >= area.Cells.Length) return;
            area.Cells[cellIndex].SetSprite(area.Root.Catalog.CellSprite(_interior.Cells(hubId)[cellIndex].State));
        }

        /// <summary>open이면 선로 화면 카메라 위치를 저장하고 열린 거점 인스턴스로 카메라를 옮기며, 닫히면 저장한 위치로 되돌린다.</summary>
        private void OnInteriorChanged(bool open)
        {
            Camera camera = _anchors.MapCamera;
            if (camera == null) return;
            if (open)
            {
                if (_viewMode.InteriorHubId == null || !_areas.TryGetValue(_viewMode.InteriorHubId, out HubArea area)) return;
                if (!_cameraMoved)
                {
                    _mapCameraPosition = camera.transform.position;
                    _mapCameraSize = camera.orthographicSize;
                    _cameraMoved = true;
                }
                Vector3 center = area.Root.transform.position;
                camera.transform.position = new Vector3(center.x, center.y, _mapCameraPosition.z);
                camera.orthographicSize = area.Root.CameraSize;
            }
            else if (_cameraMoved)
            {
                camera.transform.position = _mapCameraPosition;
                camera.orthographicSize = _mapCameraSize;
                _cameraMoved = false;
            }
        }

        void Update()
        {
            if (!_viewMode.InteriorOpen || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (IsPointerOverUi(pointer)) return;
            Camera camera = _anchors.MapCamera;
            if (camera == null) return;
            Ray ray = camera.ScreenPointToRay(pointer);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, _cellLayers);
            if (hit.collider == null || !hit.collider.TryGetComponent(out InteriorCellView cell)) return;
            if (cell.HubId != _viewMode.InteriorHubId) return;
            CellClicked?.Invoke(cell.HubId, cell.Index);
        }

        /// <summary>pointer가 HUD 위인지 EventSystem 레이캐스트로 검사한다(MapView와 같은 규칙).</summary>
        private bool IsPointerOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _uiHits);
            return _uiHits.Count > 0;
        }

        void OnDestroy()
        {
            if (_interior != null) _interior.CellChanged -= OnCellChanged;
            if (_viewMode != null) _viewMode.InteriorChanged -= OnInteriorChanged;
            if (_cameraMoved && _anchors != null && _anchors.MapCamera != null)
            {
                _anchors.MapCamera.transform.position = _mapCameraPosition;
                _anchors.MapCamera.orthographicSize = _mapCameraSize;
            }
        }

        private sealed class HubArea
        {
            public HubInteriorRoot Root { get; set; }
            public InteriorCellView[] Cells { get; set; }
        }
    }
}
