using System;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Art.Editor;
using IronRing.Bootstrap.Editor;
using IronRing.Presentation.Effects;

namespace IronRing.Presentation.Map.Editor
{
    /// <summary>
    /// 스토리 019. 선로 화면 장식을 한 번 놓는 편집기 도구(멱등, 모달 없음). 하는 일:
    /// (1) Assets/Prefabs/Fx/Smoke.prefab(연기 6프레임 SpriteFrameLoop)이 없으면 만든다.
    /// (2) HubBuilding.prefab의 Sprite 자식과 Train.prefab에 Smoke 자식이 없으면 붙인다(굴뚝 머리 위치).
    /// (3) Assets/Prefabs/Map/Props/의 Crate·Barrel·Shrub·LampPost 프리팹이 없으면 만든다.
    /// (4) Main.unity의 Props 루트 아래, 거점 4곳마다 소품 4개 중 없는 것만 놓는다(이름 Prop_{거점순번}_{k}).
    /// (5) 씬의 카메라 배경색을 팔레트 background로 맞추고, 바뀐 것이 있을 때만 씬을 저장한다. 다른 것은 다시 만들지 않는다.
    /// </summary>
    public static class PropsBuild
    {
        private const string MAIN = "Assets/Scenes/Main.unity";
        private const string FX_DIR = "Assets/Prefabs/Fx/";
        private const string SMOKE = FX_DIR + "Smoke.prefab";
        private const string PROPS_DIR = MapPrefabs.ROOT + "Props/";
        private const string MATERIAL = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const float SMOKE_FPS = 8f;

        /// <summary>프리팹 이름, 스프라이트, 거점 기준 오프셋(+는 링 안쪽), 크기.</summary>
        private static readonly (string prefab, string sprite, Vector2 offset, float scale)[] LAYOUT =
        {
            ("LampPost", "prop_lamp_post", new Vector2(1.45f, 0.85f), 1.0f),
            ("Crate", "prop_crate", new Vector2(1.5f, -0.35f), 0.55f),
            ("Barrel", "prop_barrel", new Vector2(2.1f, -0.55f), 0.42f),
            ("Shrub", "prop_shrub", new Vector2(-1.4f, 1.2f), 0.7f),
        };

        /// <summary>연기·소품 프리팹을 보장하고 Main.unity에 없는 소품만 놓고 카메라 배경색을 맞춘다.</summary>
        [MenuItem("IronRing/Map/Place Props and Smoke")]
        public static void Generate()
        {
            BuildTopDownMain.MapSettings settings = BuildTopDownMain.LoadMapData().Settings;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL) ?? throw new InvalidOperationException("Missing " + MATERIAL);
            GameObject smoke = EnsureSmoke(material);
            AttachSmoke(MapPrefabs.ROOT + "HubBuilding.prefab", "Sprite", new Vector3(0.21f, 0.6f, 0f), 0.45f, settings.HubOrder + 1, smoke);
            AttachSmoke(MapPrefabs.ROOT + "Train.prefab", null, new Vector3(0f, 0.62f, 0f), 0.5f, settings.TrainOrder + 1, smoke);
            GameObject[] props = LAYOUT.Select(entry => EnsureProp(entry.prefab, entry.sprite, material, settings.HubOrder - 1)).ToArray();
            AssetDatabase.SaveAssets();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MAIN)
            {
                if (scene.isDirty) throw new InvalidOperationException("Active scene is dirty; save it before placing props.");
                scene = EditorSceneManager.OpenScene(MAIN, OpenSceneMode.Single);
            }
            int placed = PlaceProps(scene, props);
            bool camera = SetCameraBackground(scene, ArtPalette());
            if (placed > 0 || camera)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("PropsBuild: placed " + placed + " props, camera background changed=" + camera);
        }

        private static Color ArtPalette() => SteampunkPalette.Load()["background"].B;

        /// <summary>Smoke.prefab: SpriteRenderer(smoke_0) + SpriteFrameLoop(6프레임). 있으면 그대로 둔다.</summary>
        private static GameObject EnsureSmoke(Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SMOKE);
            if (existing != null) return existing;
            System.IO.Directory.CreateDirectory(FX_DIR);
            var frames = Enumerable.Range(0, SpriteGenFx.FRAMES)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteGenFx.OUTPUT + "smoke_" + i + ".png") ?? throw new InvalidOperationException("Run SpriteGenFx.Generate first"))
                .ToArray();
            var temp = new GameObject("Smoke");
            var renderer = temp.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sharedMaterial = material;
            temp.AddComponent<SpriteFrameLoop>().Configure(frames, SMOKE_FPS);
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, SMOKE);
            UnityEngine.Object.DestroyImmediate(temp);
            return asset;
        }

        /// <summary>path 프리팹의 childPath(null이면 루트) 아래 Smoke 인스턴스가 없을 때만 붙인다.</summary>
        private static void AttachSmoke(string path, string childPath, Vector3 position, float scale, int order, GameObject smoke)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform parent = childPath == null ? root.transform : root.transform.Find(childPath);
                if (parent == null) throw new InvalidOperationException(path + " has no child " + childPath);
                if (parent.Find("Smoke") != null) return;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(smoke, parent);
                instance.name = "Smoke";
                instance.transform.localPosition = position;
                instance.transform.localScale = Vector3.one * scale;
                instance.GetComponent<SpriteRenderer>().sortingOrder = order;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>Props/name.prefab: 소품 스프라이트 하나. 있으면 그대로 둔다.</summary>
        private static GameObject EnsureProp(string name, string sprite, Material material, int order)
        {
            string path = PROPS_DIR + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            System.IO.Directory.CreateDirectory(PROPS_DIR);
            var temp = new GameObject(name);
            var renderer = temp.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteGenProps.OUTPUT + sprite + ".png") ?? throw new InvalidOperationException("Run SpriteGenProps.Generate first");
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
            UnityEngine.Object.DestroyImmediate(temp);
            return asset;
        }

        /// <summary>씬의 거점(HubTarget) 위치를 거점 id 순으로 읽어 Props 루트 아래 없는 소품만 놓고 놓은 수를 돌려준다.</summary>
        private static int PlaceProps(Scene scene, GameObject[] prefabs)
        {
            HubTarget[] hubs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HubTarget>(true))
                .OrderBy(h => h.HubId, StringComparer.Ordinal).ToArray();
            if (hubs.Length == 0) throw new InvalidOperationException("No HubTarget in " + scene.path);
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Props");
            int placed = 0;
            if (root == null)
            {
                root = new GameObject("Props");
                SceneManager.MoveGameObjectToScene(root, scene);
                placed++;
            }
            for (int h = 0; h < hubs.Length; h++)
            {
                Vector3 hub = hubs[h].transform.position;
                float sx = hub.x > 0 ? -1f : 1f, sy = hub.y > 0 ? -1f : 1f;
                for (int k = 0; k < LAYOUT.Length; k++)
                {
                    string name = "Prop_" + (h + 1) + "_" + k;
                    if (root.transform.Find(name) != null) continue;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[k], scene);
                    instance.name = name;
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.position = new Vector3(hub.x + LAYOUT[k].offset.x * sx, hub.y + LAYOUT[k].offset.y * sy, 0f);
                    instance.transform.localScale = Vector3.one * LAYOUT[k].scale;
                    placed++;
                }
            }
            return placed;
        }

        /// <summary>씬의 모든 카메라 배경색을 color로 맞추고 바뀐 것이 있으면 true.</summary>
        private static bool SetCameraBackground(Scene scene, Color color)
        {
            bool changed = false;
            foreach (Camera camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            {
                if (camera.clearFlags == CameraClearFlags.SolidColor && (Vector4)camera.backgroundColor == (Vector4)color) continue;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = color;
                EditorUtility.SetDirty(camera);
                changed = true;
            }
            return changed;
        }
    }
}
