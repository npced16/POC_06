using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Art.Editor;
using IronRing.Bootstrap.Editor;
using IronRing.Presentation.Effects;
using IronRing.Presentation.Map;

namespace IronRing.Presentation.Interior.Editor
{
    /// <summary>
    /// 선로 지도에만 폐허 소품을 놓는 제작 도구. RuinProp 프리팹과 RuinCatalog를 보장한 뒤
    /// 씬의 RuinProps 아래 거점마다 소품이 없을 때 배치한다. 마을의 설치 가능한 칸에는 소품을 붙이지 않는다.
    /// </summary>
    public static class RuinBuild
    {
        private const string MAIN = "Assets/Scenes/Main.unity";
        private const string ROOT = "Assets/Prefabs/Interior/";
        private const string CATALOG = ROOT + "RuinCatalog.asset";
        private const string PROP = ROOT + "RuinProp.prefab";
        private const string SMOKE = "Assets/Prefabs/Fx/Smoke.prefab";
        private const string DATA = "Assets/Scripts/Presentation/Interior/Resources/Interior/RuinDressing.json";
        private const string MATERIAL = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string MAP_ROOT = "RuinProps";

        private static readonly (string kind, string[] frames)[] VARIANTS =
        {
            ("puddle", new[] { "ruin_puddle_0_0", "ruin_puddle_0_1" }),
            ("puddle", new[] { "ruin_puddle_1_0", "ruin_puddle_1_1" }),
            ("weeds", new[] { "ruin_weeds_0" }),
            ("weeds", new[] { "ruin_weeds_1" }),
            ("weeds", new[] { "ruin_weeds_2" }),
            ("rubble", new[] { "ruin_rubble_0" }),
            ("rubble", new[] { "ruin_rubble_1" }),
            (RuinCatalog.Campfire, new[] { "ruin_campfire_0", "ruin_campfire_1" }),
            ("tent", new[] { "ruin_tent_0" }),
        };

        /// <summary>선로 화면 거점 둘레 폐허 소품만 없을 때 놓는다.</summary>
        [MenuItem("IronRing/Map/Place Ruin Props")]
        public static void GenerateMap()
        {
            RuinCatalog catalog = EnsureCatalog();
            Scene scene = OpenMain();
            int placed = PlaceMap(scene, catalog);
            if (placed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RuinBuild.GenerateMap: map groups placed " + placed);
        }

        private static Scene OpenMain()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path == MAIN) return scene;
            if (scene.isDirty) throw new InvalidOperationException("Active scene is dirty; save it before running RuinBuild.");
            return EditorSceneManager.OpenScene(MAIN, OpenSceneMode.Single);
        }

        private static Sprite Sp(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(SpriteGenRuin.OUTPUT + name + ".png") ?? throw new InvalidOperationException("Run SpriteGenRuin.Generate first (missing " + name + ")");

        private static RuinCatalog EnsureCatalog()
        {
            GameObject prop = AssetDatabase.LoadAssetAtPath<GameObject>(PROP);
            if (prop == null)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL) ?? throw new InvalidOperationException("Missing " + MATERIAL);
                var temp = new GameObject("RuinProp");
                var renderer = temp.AddComponent<SpriteRenderer>();
                renderer.sprite = Sp("ruin_weeds_0");
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 3;
                temp.AddComponent<SpriteFrameLoop>().Configure(new[] { renderer.sprite }, 0f);
                prop = PrefabUtility.SaveAsPrefabAsset(temp, PROP);
                UnityEngine.Object.DestroyImmediate(temp);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<RuinCatalog>(CATALOG);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RuinCatalog>();
                AssetDatabase.CreateAsset(catalog, CATALOG);
            }
            RuinCatalog.Variant[] variants = VARIANTS.Select(v => new RuinCatalog.Variant(v.kind, v.frames.Select(Sp).ToArray())).ToArray();
            catalog.Configure(variants, prop, AssetDatabase.LoadAssetAtPath<GameObject>(SMOKE));
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>씬 루트 RuinProps 아래 거점마다 묶음이 없을 때만 소품을 놓는다. 놓은 묶음 수.</summary>
        private static int PlaceMap(Scene scene, RuinCatalog catalog)
        {
            var data = JsonUtility.FromJson<RuinDressingData>(File.ReadAllText(DATA));
            if (data?._map?._props == null) throw new InvalidOperationException("RuinDressing.json has no _map._props");
            BuildTopDownMain.MapSettings settings = BuildTopDownMain.LoadMapData().Settings;
            HubTarget[] hubs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HubTarget>(true))
                .OrderBy(h => h.HubId, StringComparer.Ordinal).ToArray();
            if (hubs.Length == 0) throw new InvalidOperationException("No HubTarget in " + scene.path);
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == MAP_ROOT);
            int placed = 0;
            if (root == null)
            {
                root = new GameObject(MAP_ROOT);
                SceneManager.MoveGameObjectToScene(root, scene);
                placed++;
            }
            foreach (HubTarget hub in hubs)
            {
                string name = MAP_ROOT + "_" + hub.HubId;
                if (root.transform.Find(name) != null) continue;
                var group = new GameObject(name);
                group.transform.SetParent(root.transform, false);
                Vector3 at = hub.transform.position;
                float sx = at.x > 0 ? -1f : 1f, sy = at.y > 0 ? -1f : 1f;
                var props = new List<GameObject>();
                GameObject campfire = null;
                for (int k = 0; k < data._map._props.Length; k++)
                {
                    RuinDressingData.MapProp mp = data._map._props[k];
                    Sprite[] frames = catalog.Frames(mp._kind, mp._variant);
                    if (frames == null || frames.Length == 0) continue;
                    bool fire = mp._kind == RuinCatalog.Campfire;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(catalog.PropPrefab, scene);
                    instance.name = "Ruin_" + mp._kind + "_" + k;
                    instance.transform.SetParent(group.transform, false);
                    instance.transform.position = new Vector3(at.x + mp._x * sx, at.y + mp._y * sy, 0f);
                    instance.transform.localScale = Vector3.one * mp._scale;
                    var renderer = instance.GetComponent<SpriteRenderer>();
                    renderer.sprite = frames[0];
                    renderer.sortingOrder = settings.HubOrder + (fire ? data._map._campfireOrderOffset : data._map._groundOrderOffset);
                    var loop = instance.GetComponent<SpriteFrameLoop>();
                    loop.Configure(frames, frames.Length > 1 ? (fire ? data._campfireFramesPerSecond : data._framesPerSecond) : 0f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(loop);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    if (fire)
                    {
                        if (catalog.SmokePrefab != null)
                        {
                            var smoke = (GameObject)PrefabUtility.InstantiatePrefab(catalog.SmokePrefab, instance.transform);
                            smoke.name = "Smoke";
                            smoke.transform.localPosition = new Vector3(0f, data._map._smokeOffsetY, 0f);
                            smoke.transform.localScale = Vector3.one * data._map._smokeScale;
                            foreach (SpriteRenderer r in smoke.GetComponentsInChildren<SpriteRenderer>(true))
                            {
                                r.sortingOrder = renderer.sortingOrder + 1;
                                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                            }
                        }
                        campfire = instance;
                    }
                    else props.Add(instance);
                }
                group.AddComponent<MapRuinProps>().Configure(hub.HubId, props.ToArray(), campfire);
                placed++;
            }
            return placed;
        }
    }
}
