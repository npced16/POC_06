using System;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

using IronRing.Bootstrap.Editor;

namespace IronRing.Presentation.Map.Editor
{
    /// <summary>
    /// 선로 화면 프리팹 제작 도구. Assets/Prefabs/Map/에 MapCatalog와 프리팹(TrackTile, HubBuilding, WaypointMarker, Train)을 없을 때만 만든다
    /// (사용자 편집 보존). 카탈로그의 스프라이트·재질·폰트·프리팹 참조는 매번 다시 채운다. 모양 값은 ring-track.json의 _map 설정에서 읽는다.
    /// </summary>
    public static class MapPrefabs
    {
        public const string ROOT = "Assets/Prefabs/Map/";
        private const string CATALOG = ROOT + "MapCatalog.asset";
        private const string TRACK_PREFAB = ROOT + "TrackTile.prefab";
        private const string HUB_PREFAB = ROOT + "HubBuilding.prefab";
        private const string WAYPOINT_PREFAB = ROOT + "WaypointMarker.prefab";
        private const string TRAIN_PREFAB = ROOT + "Train.prefab";
        private const string ART = "Assets/Art/Generated";
        private const string MATERIAL = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string FONT = "Assets/Fonts/NotoSansKR-Regular.otf";

        /// <summary>선로 데이터의 모양 설정으로 카탈로그와 프리팹을 보장한다.</summary>
        [MenuItem("IronRing/Map/Generate Prefabs")]
        public static void Generate()
        {
            Ensure(BuildTopDownMain.LoadMapData().Settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>카탈로그를 채우고 없는 프리팹만 settings 모양으로 만들어 카탈로그를 돌려준다.</summary>
        internal static MapCatalog Ensure(BuildTopDownMain.MapSettings settings)
        {
            Directory.CreateDirectory(ROOT);
            Material material = Load<Material>(MATERIAL);
            Font font = Load<Font>(FONT);
            MapCatalog catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CATALOG);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MapCatalog>();
                AssetDatabase.CreateAsset(catalog, CATALOG);
            }
            var serialized = new SerializedObject(catalog);
            string[] paths = Directory.GetFiles(ART, "*.png").Select(path => path.Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            SerializedProperty sprites = serialized.FindProperty("_sprites");
            sprites.arraySize = 0;
            foreach (string path in paths)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                int index = sprites.arraySize++;
                sprites.GetArrayElementAtIndex(index).objectReferenceValue = sprite;
            }
            serialized.FindProperty("_spriteMaterial").objectReferenceValue = material;
            serialized.FindProperty("_labelFont").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject track = EnsureTrackTile(catalog, settings, material);
            GameObject hub = EnsureHubBuilding(catalog, settings, material, font);
            GameObject waypoint = EnsureWaypointMarker(catalog, settings, material, font);
            GameObject train = EnsureTrain(catalog, settings, material);
            serialized.Update();
            serialized.FindProperty("_trackTilePrefab").objectReferenceValue = track;
            serialized.FindProperty("_hubBuildingPrefab").objectReferenceValue = hub;
            serialized.FindProperty("_waypointMarkerPrefab").objectReferenceValue = waypoint;
            serialized.FindProperty("_trainPrefab").objectReferenceValue = train;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>TrackTile.prefab: 가로 선로 스프라이트 한 장. 인스턴스마다 종류(가로·세로·곡선)를 덮어쓴다.</summary>
        private static GameObject EnsureTrackTile(MapCatalog catalog, BuildTopDownMain.MapSettings settings, Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TRACK_PREFAB);
            if (existing != null) return existing;
            var temp = new GameObject("TrackTile");
            AddSprite(temp, catalog.Sprite("rail_horizontal"), material, settings.RailOrder);
            temp.transform.localScale = Vector3.one * settings.TileSize;
            return Save(temp, TRACK_PREFAB);
        }

        /// <summary>HubBuilding.prefab: 루트(앵커) 아래 Sprite(건물, 충돌체, HubTarget)와 Name(한글 TextMesh).</summary>
        private static GameObject EnsureHubBuilding(MapCatalog catalog, BuildTopDownMain.MapSettings settings, Material material, Font font)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(HUB_PREFAB);
            if (existing != null) return existing;
            var temp = new GameObject("HubBuilding");
            GameObject sprite = Child(temp, "Sprite");
            AddSprite(sprite, catalog.Sprite("hub_1"), material, settings.HubOrder);
            sprite.transform.localScale = Vector3.one * settings.HubScale;
            sprite.AddComponent<BoxCollider2D>().size = Vector2.one;
            sprite.AddComponent<HubTarget>().Configure("hub-1");
            Label(temp, settings, font, "거점", -settings.LabelOffset);
            return Save(temp, HUB_PREFAB);
        }

        /// <summary>WaypointMarker.prefab: 루트 아래 Sprite(표식)와 Name(한글 TextMesh).</summary>
        private static GameObject EnsureWaypointMarker(MapCatalog catalog, BuildTopDownMain.MapSettings settings, Material material, Font font)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(WAYPOINT_PREFAB);
            if (existing != null) return existing;
            var temp = new GameObject("WaypointMarker");
            GameObject sprite = Child(temp, "Sprite");
            AddSprite(sprite, catalog.Sprite("waypoint"), material, settings.WaypointOrder);
            sprite.transform.localScale = Vector3.one * settings.WaypointScale;
            Label(temp, settings, font, "경유지", -settings.LabelOffset);
            return Save(temp, WAYPOINT_PREFAB);
        }

        /// <summary>Train.prefab: 열차 스프라이트와 MapTrain 표식.</summary>
        private static GameObject EnsureTrain(MapCatalog catalog, BuildTopDownMain.MapSettings settings, Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TRAIN_PREFAB);
            if (existing != null) return existing;
            var temp = new GameObject("Train");
            AddSprite(temp, catalog.Sprite("train"), material, settings.TrainOrder);
            temp.transform.localScale = Vector3.one * settings.TrainScale;
            temp.AddComponent<MapTrain>();
            return Save(temp, TRAIN_PREFAB);
        }

        /// <summary>target에 sprite, material, order의 SpriteRenderer를 붙인다.</summary>
        private static void AddSprite(GameObject target, Sprite sprite, Material material, int order)
        {
            var renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
        }

        /// <summary>parent 아래 "Name" TextMesh를 settings 모양과 font로 만들어 offsetY에 놓는다. 텍스트는 인스턴스마다 덮어쓴다.</summary>
        private static void Label(GameObject parent, BuildTopDownMain.MapSettings settings, Font font, string text, float offsetY)
        {
            GameObject label = Child(parent, "Name");
            label.transform.localPosition = new Vector3(0, offsetY, 0);
            var mesh = label.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = settings.FontSize;
            mesh.characterSize = settings.LabelSize / settings.PixelsPerUnit;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = settings.LabelColor;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = settings.LabelOrder;
        }

        /// <summary>parent 아래 name 자식 오브젝트를 만든다.</summary>
        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        /// <summary>temp를 path 프리팹으로 저장하고 임시 오브젝트를 지운 뒤 에셋을 돌려준다.</summary>
        private static GameObject Save(GameObject temp, string path)
        {
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
            UnityEngine.Object.DestroyImmediate(temp);
            return asset;
        }

        /// <summary>path의 필수 에셋을 읽어 T로 돌려주며 누락은 예외로 알린다.</summary>
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
            return asset;
        }
    }
}
