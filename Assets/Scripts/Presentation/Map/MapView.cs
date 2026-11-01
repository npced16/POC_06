using System;
using System.Collections.Generic;

using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Presentation.Map
{
    public sealed class MapView : MonoBehaviour, IMapAnchors, IHubClickSource
    {
        [Header("Authored map references")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _train;
        [SerializeField] private MapNode[] _nodes;
        [Header("Train presentation")]
        [SerializeField] private float _cornerRadius = 0.5f;
        [SerializeField] private float _spriteForwardAngle = 90f;
        [Header("Hub picking")]
        [SerializeField] private LayerMask _hubLayers = -1;
        private ITrackRuntime _track;
        private Func<float> _progress;
        private readonly Dictionary<string, MapNode> _byId = new Dictionary<string, MapNode>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        public Transform TrainAnchor => _train;
        public Camera MapCamera => _camera;
        public event Action<string> HubClicked;

        /// <summary>씬 생성 시 camera, train, nodes 및 radius를 저장하여 수동 편집 가능한 참조를 구성한다.</summary>
        public void Configure(Camera camera, Transform train, MapNode[] nodes, float radius, float forwardAngle)
        {
            _camera = camera;
            _train = train;
            _nodes = nodes;
            _cornerRadius = radius;
            _spriteForwardAngle = forwardAngle;
        }

        /// <summary>실제 track과 진행도 읽기 함수를 받아 표시만 연결하며 이동 규칙은 변경하지 않는다. 직렬화 참조가 비면 씬의 프리팹 인스턴스를 찾는다.</summary>
        public void Bind(ITrackRuntime track, Func<float> progress)
        {
            _track = track;
            _progress = progress;
            if (_nodes == null || _nodes.Length == 0) _nodes = UnityEngine.Object.FindObjectsByType<MapNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (_train == null)
            {
                MapTrain marker = UnityEngine.Object.FindFirstObjectByType<MapTrain>(FindObjectsInactive.Include);
                if (marker != null) _train = marker.transform;
            }
            if (_camera == null) _camera = Camera.main;
            _byId.Clear();
            foreach (MapNode node in _nodes) _byId.Add(node.Id, node);
            foreach (ITrackNode node in track.Nodes)
                if (!_byId.ContainsKey(node.Id)) throw new ArgumentException("Missing map anchor: " + node.Id);
        }

        /// <summary>nodeId와 일치하는 선로 앵커를 반환하며 없는 식별자는 null을 반환한다.</summary>
        public Transform NodeAnchor(string nodeId)
        {
            foreach (MapNode node in _nodes) if (node.Id == nodeId) return node.transform;
            return null;
        }

        /// <summary>hubId와 일치하는 건물 앵커를 반환하며 거점이 아니면 null을 반환한다.</summary>
        public Transform HubAnchor(string hubId)
        {
            foreach (MapNode node in _nodes) if (node.Id == hubId) return node.Hub;
            return null;
        }

        void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (IsPointerOverUi(pointer)) return;
            Ray ray = _camera.ScreenPointToRay(pointer);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, _hubLayers);
            if (hit.collider != null && hit.collider.TryGetComponent(out HubTarget target))
                HubClicked?.Invoke(target.HubId);
        }

        /// <summary>현재 pointer 위치를 UI 레이캐스트로 검사하여 HUD가 클릭을 소비하는지 반환한다.</summary>
        private bool IsPointerOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _uiHits);
            return _uiHits.Count > 0;
        }

        void LateUpdate()
        {
            if (_track == null) return;
            int index = _track.CurrentNode.Index;
            MapNode from = _byId[_track.CurrentNode.Id];
            MapNode to = _byId[_track.Nodes[(index + 1) % _track.Nodes.Count].Id];
            float progress = Mathf.Clamp01(_progress());
            Vector3 position = Evaluate(from, to, progress, out Vector3 direction);
            _train.position = position;
            // 멈춰 있을 때(진행도 0)는 마지막 방향을 유지한다.
            if (!_track.IsHeld || progress > 0f)
                _train.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - _spriteForwardAngle);
        }

        /// <summary>노드 앵커와 실제 progress를 사용해 선로상의 위치를 반환하고 진행 접선을 direction에 기록한다.</summary>
        private Vector3 Evaluate(MapNode from, MapNode to, float progress, out Vector3 direction)
        {
            Vector3 start = from.transform.position;
            Vector3 end = to.transform.position;
            if (from.Bend == null)
            {
                direction = (end - start).normalized;
                return Vector3.Lerp(start, end, progress);
            }
            Vector3 bend = from.Bend.position;
            Vector3 incoming = (bend - start).normalized;
            Vector3 outgoing = (end - bend).normalized;
            float radius = Mathf.Min(_cornerRadius, Vector3.Distance(start, bend), Vector3.Distance(end, bend));
            Vector3 entry = bend - incoming * radius;
            Vector3 exit = bend + outgoing * radius;
            Vector3 center = entry + outgoing * radius;
            float firstLength = Vector3.Distance(start, entry);
            float arcLength = Mathf.PI * radius * 0.5f;
            float lastLength = Vector3.Distance(exit, end);
            float distance = progress * (firstLength + arcLength + lastLength);
            if (distance <= firstLength)
            {
                direction = incoming;
                return start + incoming * distance;
            }
            if (distance >= firstLength + arcLength)
            {
                direction = outgoing;
                return exit + outgoing * (distance - firstLength - arcLength);
            }
            float t = (distance - firstLength) / arcLength;
            Vector3 radial = Vector3.Slerp(entry - center, exit - center, t);
            direction = Vector3.Slerp(incoming, outgoing, t);
            return center + radial;
        }
    }
}
