using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Track;

namespace IronRing.Presentation.Map
{
    /// <summary>
    /// 잠긴 역이 열릴 때마다 지도 선로 고리를 중심에서 바깥으로 넓힌다.
    /// 씬에 저작된 역·굽힘점 위치를 처음 배치로 삼아 가로·세로 각각 중심 반대쪽으로 밀고, 선로 타일을 다시 깔고,
    /// 구간 이동 시간을 처음 배치 대비 선로 길이 비율로 늘리며, 지도 카메라 크기를 키운다.
    /// 아무 역도 열리지 않은 동안에는 씬 배치와 선로 타일을 건드리지 않는다.
    /// </summary>
    public sealed class MapRingExpansion : MonoBehaviour
    {
        private const string RAIL_HORIZONTAL = "rail_horizontal";
        private const string RAIL_VERTICAL = "rail_vertical";
        private const string RAIL_CURVE_PREFIX = "rail_curve_";

        [Header("참조")]
        [SerializeField] private MapView _map;
        [SerializeField] private TrainController _train;
        [SerializeField] private Transform _rails;
        [SerializeField] private MapCatalog _catalog;
        [SerializeField] private Camera _camera;

        [Header("확장")]
        [Tooltip("처음 이만큼의 잠긴 역은 열려도 고리를 넓히지 않는다. 1이면 두 번째 마을은 그대로, 세 번째 마을부터 넓어진다.")]
        [SerializeField, Min(0)] private int _freeOpenedHubs = 1;
        [Tooltip("역 하나가 열릴 때 고리가 각 방향으로 넓어지는 칸 수.")]
        [SerializeField, Min(0)] private int _tilesPerOpenedHub = 2;
        [Tooltip("넓힐 때 기준이 되는 고리 중심(월드 좌표). 이 줄보다 큰 쪽은 +, 작은 쪽은 -로 밀린다.")]
        [SerializeField] private Vector2 _center = new Vector2(0f, 1f);
        [Tooltip("선로 타일 한 칸의 월드 크기.")]
        [SerializeField, Min(0.01f)] private float _tileSize = 1f;

        [Header("카메라")]
        [Tooltip("넓어진 한 칸마다 지도 카메라 크기에 더할 값.")]
        [SerializeField, Min(0f)] private float _cameraSizePerTile = 1f;

        private readonly Dictionary<string, MapNode> _byId = new Dictionary<string, MapNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _basePositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _baseBends = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly HashSet<string> _initiallyLocked = new HashSet<string>(StringComparer.Ordinal);
        private float[] _baseEdgeLengths;
        private float _baseCameraSize;
        private int _level;
        private bool _cameraPending;
        private RingTrack _track;
        private IViewMode _viewMode;

        /// <summary>현재 확장 단계(열린 잠긴 역 수).</summary>
        public int Level => _level;

        void Start()
        {
            _track = _train.Track;
            foreach (MapNode node in _map.GetComponentsInChildren<MapNode>(true))
            {
                _byId[node.Id] = node;
                _basePositions[node.Id] = node.transform.position;
                if (node.Bend != null) _baseBends[node.Id] = node.Bend.position;
            }
            foreach (TrackNode node in _track.Nodes)
                if (node.IsLocked) _initiallyLocked.Add(node.Id);
            _baseEdgeLengths = new float[_track.Nodes.Count];
            for (int i = 0; i < _baseEdgeLengths.Length; i++) _baseEdgeLengths[i] = EdgeLength(i, 0);
            _baseCameraSize = _camera.orthographicSize;
            _train.EdgeDurationScale = EdgeDurationScale;
            _track.NodeUnlocked += OnNodeUnlocked;
        }

        void LateUpdate()
        {
            if (!_cameraPending) return;
            if (_viewMode == null) _viewMode = GameServices.Get<IViewMode>();
            // 마을 내부를 보는 동안에는 내부 화면이 카메라를 쓰므로 지도로 돌아온 뒤 적용한다.
            if (_viewMode != null && _viewMode.InteriorOpen) return;
            _camera.orthographicSize = _baseCameraSize + _level * _tilesPerOpenedHub * _cameraSizePerTile;
            _cameraPending = false;
        }

        void OnDestroy()
        {
            if (_track != null) _track.NodeUnlocked -= OnNodeUnlocked;
            if (_train != null && _train.EdgeDurationScale == (Func<int, float>)EdgeDurationScale) _train.EdgeDurationScale = null;
        }

        /// <summary>
        /// 처음 잠겨 있던 역이 열리면 열린 수에서 _freeOpenedHubs를 뺀 값을 확장 단계로 지도에 적용한다.
        /// node를 사용하며, _level과 지도 배치를 변경한다.
        /// </summary>
        private void OnNodeUnlocked(TrackNode node)
        {
            if (!_initiallyLocked.Contains(node.Id)) return;
            int opened = 0;
            foreach (TrackNode track in _track.Nodes)
                if (_initiallyLocked.Contains(track.Id) && !track.IsLocked) opened++;
            ApplyLevel(opened - _freeOpenedHubs);
        }

        /// <summary>
        /// level 단계로 역·굽힘점을 옮기고 선로 타일을 다시 깔며 카메라 적용을 예약한다.
        /// level을 사용하며, MapNode 위치·_rails 자식·_level·_cameraPending을 변경한다.
        /// </summary>
        public void ApplyLevel(int level)
        {
            level = Mathf.Max(0, level);
            if (level == _level) return;
            _level = level;
            float offset = Offset(level);
            foreach (KeyValuePair<string, MapNode> pair in _byId)
            {
                pair.Value.transform.position = Expand(_basePositions[pair.Key], offset, pair.Value.transform.position.z);
                // 굽힘점은 역의 자식이므로 역을 옮긴 뒤 월드 위치를 따로 맞춘다.
                if (pair.Value.Bend != null && _baseBends.TryGetValue(pair.Key, out Vector2 bend))
                    pair.Value.Bend.position = Expand(bend, offset, pair.Value.Bend.position.z);
            }
            RebuildRails(offset);
            _cameraPending = true;
            Debug.Log($"MAP: ring expanded level={_level} offset={offset:0.##}");
        }

        /// <summary>
        /// 구간 번호의 현재 선로 길이를 처음 배치 길이로 나눈 배율을 돌려준다.
        /// edgeIndex와 _level을 사용하며, 상태는 바꾸지 않는다.
        /// </summary>
        private float EdgeDurationScale(int edgeIndex)
        {
            if (_level == 0 || _baseEdgeLengths == null || edgeIndex < 0 || edgeIndex >= _baseEdgeLengths.Length) return 1f;
            float baseLength = _baseEdgeLengths[edgeIndex];
            return baseLength > 0f ? EdgeLength(edgeIndex, Offset(_level)) / baseLength : 1f;
        }

        /// <summary>
        /// 구간 시작 역에서 굽힘점을 거쳐 다음 역까지의 선로 길이를 계산한다.
        /// edgeIndex와 offset을 사용하며, 넓힌 위치 기준 길이를 반환한다.
        /// </summary>
        private float EdgeLength(int edgeIndex, float offset)
        {
            string fromId = _track.Nodes[edgeIndex].Id;
            string toId = _track.Nodes[(edgeIndex + 1) % _track.Nodes.Count].Id;
            Vector2 start = Expand(_basePositions[fromId], offset);
            Vector2 end = Expand(_basePositions[toId], offset);
            if (!_baseBends.TryGetValue(fromId, out Vector2 bend)) return Vector2.Distance(start, end);
            Vector2 corner = Expand(bend, offset);
            return Vector2.Distance(start, corner) + Vector2.Distance(corner, end);
        }

        /// <summary>
        /// 기존 선로 타일을 지우고 넓힌 역·굽힘점을 잇는 직선·곡선 타일을 새로 깐다.
        /// offset과 _catalog의 타일 프리팹·스프라이트를 사용하며, _rails의 자식을 변경한다.
        /// </summary>
        private void RebuildRails(float offset)
        {
            for (int i = _rails.childCount - 1; i >= 0; i--) Destroy(_rails.GetChild(i).gameObject);
            Dictionary<Vector2Int, string> plan = new Dictionary<Vector2Int, string>();
            int count = _track.Nodes.Count;
            for (int i = 0; i < count; i++)
            {
                string fromId = _track.Nodes[i].Id;
                Vector2Int start = GridPoint(Expand(_basePositions[fromId], offset));
                Vector2Int end = GridPoint(Expand(_basePositions[_track.Nodes[(i + 1) % count].Id], offset));
                if (!_baseBends.TryGetValue(fromId, out Vector2 bendPosition))
                {
                    AddSegment(plan, start, end, fromId);
                    continue;
                }
                Vector2Int bend = GridPoint(Expand(bendPosition, offset));
                AddSegment(plan, start, bend, fromId);
                AddSegment(plan, bend, end, fromId);
                Vector2 sides = -((Vector2)(bend - start)).normalized + ((Vector2)(end - bend)).normalized;
                plan[bend] = RAIL_CURVE_PREFIX + (sides.y > 0 ? "n" : "s") + (sides.x > 0 ? "e" : "w");
            }
            foreach (KeyValuePair<Vector2Int, string> tile in plan)
            {
                GameObject instance = Instantiate(_catalog.TrackTilePrefab, _rails);
                instance.name = tile.Value;
                instance.transform.position = (Vector2)tile.Key * _tileSize;
                instance.GetComponent<SpriteRenderer>().sprite = _catalog.Sprite(tile.Value);
            }
        }

        /// <summary>
        /// start부터 end까지 축 정렬 직선 타일을 plan에 기록한다. 대각선 구간은 경고만 남긴다.
        /// plan, start, end와 경고용 nodeId를 사용하며, plan을 변경한다.
        /// </summary>
        private static void AddSegment(Dictionary<Vector2Int, string> plan, Vector2Int start, Vector2Int end, string nodeId)
        {
            Vector2Int delta = end - start;
            if (delta == Vector2Int.zero) return;
            if (delta.x != 0 && delta.y != 0)
            {
                Debug.LogWarning($"MAP: rail segment from {nodeId} is not axis aligned and was skipped.");
                return;
            }
            Vector2Int step = new Vector2Int(Math.Sign(delta.x), Math.Sign(delta.y));
            int length = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
            string sprite = delta.x != 0 ? RAIL_HORIZONTAL : RAIL_VERTICAL;
            for (int i = 0; i <= length; i++) plan[start + step * i] = sprite;
        }

        /// <summary>
        /// 처음 위치를 중심 반대쪽으로 가로·세로 각각 offset만큼 민다. 중심선 위의 좌표는 그대로 둔다.
        /// position과 offset, _center를 사용하며, 넓힌 위치를 반환한다.
        /// </summary>
        private Vector2 Expand(Vector2 position, float offset)
        {
            return new Vector2(position.x + Push(position.x - _center.x) * offset,
                position.y + Push(position.y - _center.y) * offset);
        }

        /// <summary>Expand와 같고 z는 z 값을 유지한 Vector3를 반환한다.</summary>
        private Vector3 Expand(Vector2 position, float offset, float z)
        {
            Vector2 expanded = Expand(position, offset);
            return new Vector3(expanded.x, expanded.y, z);
        }

        /// <summary>중심과의 차이 delta의 부호를 돌려준다. 중심선 위면 0이다.</summary>
        private static float Push(float delta) => Mathf.Abs(delta) < 0.001f ? 0f : Mathf.Sign(delta);

        /// <summary>level 단계의 월드 이동 거리를 돌려준다.</summary>
        private float Offset(int level) => level * _tilesPerOpenedHub * _tileSize;

        /// <summary>월드 position을 선로 타일 격자 좌표로 반올림해 돌려준다.</summary>
        private Vector2Int GridPoint(Vector2 position) => Vector2Int.RoundToInt(position / _tileSize);
    }
}
