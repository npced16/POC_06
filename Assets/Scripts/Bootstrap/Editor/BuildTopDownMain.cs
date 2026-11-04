using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Localization;
using IronRing.Presentation.Interior.Editor;
using IronRing.Presentation.Map;
using IronRing.Presentation.Map.Editor;
using IronRing.Track;

namespace IronRing.Bootstrap.Editor
{
    /// <summary>
    /// Main 씬 제작 도구. 선로 화면은 Assets/Prefabs/Map 프리팹 인스턴스(TrackTile, HubBuilding, WaypointMarker, Train)로,
    /// 거점 내부는 Assets/Prefabs/Interior의 HubInterior 인스턴스로 놓아 사용자가 Hierarchy/Inspector에서 자유롭게 편집한다.
    /// 코드는 생성 뒤 참조만 저장한다(MapView, MainRoot).
    /// </summary>
    public static class BuildTopDownMain
    {
        private const string SCENE = "Assets/Scenes/Main.unity";
        private const string TRACK = "Assets/Scripts/Track/Resources/Track/ring-track.json";

        /// <summary>Main을 최초 탑뷰 구성으로 전환하며 기존 탑뷰 씬이면 수동 편집 상태를 그대로 열어 둔다.</summary>
        public static void Build()
        {
            BuildScene(false);
        }

        /// <summary>제작자가 명시적으로 실행하면 현재 Main을 JSON의 최초 배치로 프리팹 인스턴스로 다시 생성한다.</summary>
        public static void Rebuild()
        {
            BuildScene(true);
        }

        /// <summary>선로 JSON의 노드와 _map 모양 설정을 읽는다.</summary>
        internal static MapData LoadMapData()
        {
            TextAsset track = Load<TextAsset>(TRACK);
            MapData data = JsonUtility.FromJson<MapData>(track.text);
            if (data == null || data.Nodes == null || data.Settings == null) throw new InvalidOperationException("Invalid map data: " + TRACK);
            return data;
        }

        /// <summary>rebuild가 참이면 재배치하고 아니면 완성된 씬의 수동 설정을 보존한다.</summary>
        private static void BuildScene(bool rebuild)
        {
            var scene = EditorSceneManager.OpenScene(SCENE);
            if (!rebuild && scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<MapView>() != null) &&
                scene.GetRootGameObjects().Any(root => root.GetComponent<MainRoot>() != null)) return;
            TextAsset track = Load<TextAsset>(TRACK);
            MapData data = LoadMapData();
            if (data.Nodes.Select(node => node.Position).Distinct().Count() != data.Nodes.Length)
                throw new InvalidOperationException("Map nodes require distinct authored coordinates.");
            Dictionary<Vector2Int, string> railPlan = CreateRailPlan(data);
            MapSettings settings = data.Settings;
            MapCatalog catalog = MapPrefabs.Ensure(settings);
            foreach (GameObject root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = settings.CameraSize;
            camera.transform.position = settings.CameraPosition;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = settings.Background;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var rootObject = new GameObject("Iron Ring / Real Systems");
            var mapObject = new GameObject("Top Down Map");
            var map = mapObject.AddComponent<MapView>();
            Transform rails = new GameObject("Rails").transform;
            rails.SetParent(map.transform, false);
            foreach (var tile in railPlan)
            {
                GameObject instance = Place(catalog.TrackTilePrefab, rails, (Vector2)tile.Key * settings.TileSize);
                instance.name = tile.Value;
                instance.GetComponent<SpriteRenderer>().sprite = catalog.Sprite(tile.Value);
            }
            var nodes = new List<MapNode>();
            foreach (MapNodeData node in data.Nodes)
            {
                var anchor = new GameObject(node.Id).AddComponent<MapNode>();
                anchor.transform.SetParent(map.transform, false);
                anchor.transform.localPosition = node.Position;
                Transform hub = null;
                Vector2 markerPosition = node.Position;
                float labelDirection = -1f;
                GameObject marker;
                if (node.IsStation)
                {
                    markerPosition.y -= Mathf.Sign(markerPosition.y) * settings.HubInset;
                    labelDirection = -Mathf.Sign(markerPosition.y);
                    marker = Place(catalog.HubBuildingPrefab, anchor.transform, markerPosition);
                    // 스토리 048: 역 그림이 없는 거점(잿골 등)은 첫 역 그림을 쓴다.
                    marker.GetComponentInChildren<SpriteRenderer>().sprite = catalog.Sprite(node.LabelKey) ?? catalog.Sprite(data.Nodes[0].LabelKey);
                    marker.GetComponentInChildren<HubTarget>().Configure(node.Id);
                    hub = marker.transform;
                }
                else marker = Place(catalog.WaypointMarkerPrefab, anchor.transform, markerPosition);
                marker.name = node.LabelKey;
                TextMesh label = marker.GetComponentInChildren<TextMesh>();
                label.text = Strings.Get(node.LabelKey);
                label.transform.position = markerPosition + Vector2.up * (labelDirection * settings.LabelOffset);
                Transform bend = null;
                if (node.HasBend)
                {
                    bend = new GameObject("Track Bend").transform;
                    bend.SetParent(anchor.transform, false);
                    bend.position = node.Bend;
                }
                anchor.Configure(node.Id, hub, bend);
                if (node.IsStation && node.IsLocked)
                {
                    // 스토리 048: 잠긴 역은 어두운 폐허 표지 + "폐쇄된 선로". 열리면 MapLockedHub가 "{이름} · 폐허"로 바꾼다.
                    anchor.gameObject.AddComponent<MapLockedHub>().Configure(node.Id, marker.GetComponentsInChildren<SpriteRenderer>(true), label,
                        marker.GetComponentsInChildren<Collider2D>(true), Strings.Get("map_line_closed"),
                        string.Format(Strings.Get("map_hub_ruin"), Strings.Get(node.LabelKey)));
                    label.text = Strings.Get("map_line_closed");
                }
                nodes.Add(anchor);
            }
            Transform trainSprite = Place(catalog.TrainPrefab, map.transform, data.Nodes[0].Position).transform;
            Vector2 departureDirection = data.Nodes[1].Position - data.Nodes[0].Position;
            trainSprite.rotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(departureDirection.y, departureDirection.x) * Mathf.Rad2Deg - settings.SpriteForwardAngle);
            map.Configure(camera, trainSprite, nodes.ToArray(), settings.CornerRadius, settings.SpriteForwardAngle);
            var train = rootObject.AddComponent<TrainController>();
            train.ConfigurePresentation(false, true);
            rootObject.AddComponent<MainRoot>().Configure(
                track,
                Load<TextAsset>("Assets/Scripts/Npc/Data/NpcPool.json"),
                Load<TextAsset>("Assets/Scripts/Hub/Data/HubProfiles.json"),
                Load<TextAsset>("Assets/Scripts/Combat/Resources/Story005Encounters.json"),
                Load<TextAsset>("Assets/Scripts/Bootstrap/Data/MainSettings.json"), train, map);
            // Story 012: 거점 내부 프리팹 인스턴스(HubInteriors/HubInterior hub-n)를 같은 씬에 놓는다.
            Story012Build.PlaceInteriors(scene, Story012Build.EnsurePrefabs());
            EditorSceneManager.SaveScene(scene, SCENE);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = mapObject;
        }

        /// <summary>prefab 인스턴스를 parent 아래 월드 position에 놓고 돌려준다(프리팹 연결 유지).</summary>
        private static GameObject Place(GameObject prefab, Transform parent, Vector2 position)
        {
            if (prefab == null) throw new InvalidOperationException("Map catalog is missing a prefab. Run IronRing.Presentation.Map.Editor.MapPrefabs.Generate.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            return instance;
        }

        /// <summary>data의 노드와 굽힘점을 정수 격자로 검증하고 해당 경로만 덮는 타일 계획을 반환한다.</summary>
        private static Dictionary<Vector2Int, string> CreateRailPlan(MapData data)
        {
            var plan = new Dictionary<Vector2Int, string>();
            float size = data.Settings.TileSize;
            for (int i = 0; i < data.Nodes.Length; i++)
            {
                MapNodeData node = data.Nodes[i];
                Vector2Int start = GridPoint(node.Position, size);
                Vector2Int end = GridPoint(data.Nodes[(i + 1) % data.Nodes.Length].Position, size);
                if (!node.HasBend)
                {
                    AddRailSegment(plan, start, end);
                    continue;
                }
                Vector2Int bend = GridPoint(node.Bend, size);
                Vector2 incoming = (Vector2)(bend - start);
                Vector2 outgoing = (Vector2)(end - bend);
                if (Mathf.Abs(Vector2.Dot(incoming.normalized, outgoing.normalized)) > 0.001f)
                    throw new InvalidOperationException("Map bend must turn at a right angle: " + node.Id);
                AddRailSegment(plan, start, bend);
                AddRailSegment(plan, bend, end);
                Vector2 sides = -incoming.normalized + outgoing.normalized;
                plan[bend] = "rail_curve_" + (sides.y > 0 ? "n" : "s") + (sides.x > 0 ? "e" : "w");
            }
            return plan;
        }

        /// <summary>position이 size 간격의 격자 위에 있는지 검사하고 정수 타일 좌표를 반환한다.</summary>
        private static Vector2Int GridPoint(Vector2 position, float size)
        {
            if (size <= 0) throw new InvalidOperationException("Tile size must be positive.");
            Vector2 grid = position / size;
            Vector2Int point = Vector2Int.RoundToInt(grid);
            if (Vector2.Distance(grid, point) > 0.001f)
                throw new InvalidOperationException("Map point must align to the tile grid.");
            return point;
        }

        /// <summary>start부터 end까지 축 정렬을 검증하고 정수 반복으로 직선 타일을 plan에 기록한다.</summary>
        private static void AddRailSegment(Dictionary<Vector2Int, string> plan, Vector2Int start, Vector2Int end)
        {
            Vector2Int delta = end - start;
            if ((delta.x != 0 && delta.y != 0) || delta == Vector2Int.zero)
                throw new InvalidOperationException("Rail segment must be nonzero and axis aligned.");
            Vector2Int step = new Vector2Int(Math.Sign(delta.x), Math.Sign(delta.y));
            int count = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
            string sprite = delta.x != 0 ? "rail_horizontal" : "rail_vertical";
            for (int i = 0; i <= count; i++) plan[start + step * i] = sprite;
        }

        /// <summary>path의 필수 애셋을 읽어 T로 반환하며 누락된 제작 입력은 예외로 알린다.</summary>
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing Main asset: " + path);
            return asset;
        }

        [Serializable]
        internal sealed class MapData
        {
            [SerializeField] private MapNodeData[] _nodes;
            [SerializeField] private MapSettings _map;
            public MapNodeData[] Nodes => _nodes;
            public MapSettings Settings => _map;
        }

        [Serializable]
        internal sealed class MapNodeData
        {
            [SerializeField] private string _id;
            [SerializeField] private bool _isStation;
            [SerializeField] private bool _locked;
            [SerializeField] private Vector2 _mapPosition;
            [SerializeField] private string _labelKey;
            [SerializeField] private bool _hasBend;
            [SerializeField] private Vector2 _bend;
            public string Id => _id;
            public bool IsStation => _isStation;
            public bool IsLocked => _locked;
            public Vector2 Position => _mapPosition;
            public string LabelKey => _labelKey;
            public bool HasBend => _hasBend;
            public Vector2 Bend => _bend;
        }

        [Serializable]
        internal sealed class MapSettings
        {
            [SerializeField] private float _tileSize;
            [SerializeField] private float _cornerRadius;
            [SerializeField] private float _cameraSize;
            [SerializeField] private Vector3 _cameraPosition;
            [SerializeField] private float _hubScale;
            [SerializeField] private float _waypointScale;
            [SerializeField] private float _trainScale;
            [SerializeField] private float _hubInset;
            [SerializeField] private float _labelOffset;
            [SerializeField] private float _labelSize;
            [SerializeField] private Color _background;
            [SerializeField] private int _fontSize;
            [SerializeField] private int _pixelsPerUnit;
            [SerializeField] private Color _labelColor;
            [SerializeField] private int _railOrder;
            [SerializeField] private int _waypointOrder;
            [SerializeField] private int _hubOrder;
            [SerializeField] private int _trainOrder;
            [SerializeField] private int _labelOrder;
            [SerializeField] private float _spriteForwardAngle;
            public float TileSize => _tileSize;
            public float CornerRadius => _cornerRadius;
            public float CameraSize => _cameraSize;
            public Vector3 CameraPosition => _cameraPosition;
            public float HubScale => _hubScale;
            public float WaypointScale => _waypointScale;
            public float TrainScale => _trainScale;
            public float HubInset => _hubInset;
            public float LabelOffset => _labelOffset;
            public float LabelSize => _labelSize;
            public Color Background => _background;
            public int FontSize => _fontSize;
            public int PixelsPerUnit => _pixelsPerUnit;
            public Color LabelColor => _labelColor;
            public int RailOrder => _railOrder;
            public int WaypointOrder => _waypointOrder;
            public int HubOrder => _hubOrder;
            public int TrainOrder => _trainOrder;
            public int LabelOrder => _labelOrder;
            public float SpriteForwardAngle => _spriteForwardAngle;
        }
    }
}
