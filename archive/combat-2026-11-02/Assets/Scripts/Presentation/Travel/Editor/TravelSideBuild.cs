using System.IO;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using TMPro;

namespace IronRing.Presentation.Travel.Editor
{
    /// <summary>
    /// 스토리 037 생성기(멱등). 흰 사각형·원 스프라이트(Assets/Art/Generated/travel), 프리미티브 프리팹(PrimRect/PrimCircle/PrimLabel),
    /// TravelCatalog.asset, TravelSide.prefab(TravelSideView + TravelRaidSideView + 층 자식)을 없을 때만 만들고,
    /// 열린 Main 씬에 TravelSide 인스턴스(y = −200)가 없으면 놓고 저장한다. 두 번째 실행은 "unchanged".
    /// </summary>
    public static class TravelSideBuild
    {
        private const string ART = "Assets/Art/Generated/travel";
        private const string WHITE = ART + "/white.png";
        private const string CIRCLE = ART + "/circle.png";
        private const string PREFABS = "Assets/Prefabs/Travel";
        private const string RECT_PREFAB = PREFABS + "/PrimRect.prefab";
        private const string CIRCLE_PREFAB = PREFABS + "/PrimCircle.prefab";
        private const string LABEL_PREFAB = PREFABS + "/PrimLabel.prefab";
        private const string CATALOG = PREFABS + "/TravelCatalog.asset";
        private const string ROOT_PREFAB = PREFABS + "/TravelSide.prefab";
        private const string SETTINGS = "Assets/Scripts/Bootstrap/Data/MainSettings.json";
        private const string FONT = "Assets/Fonts/NotoSansKR SDF.asset";
        private static readonly Vector3 ROOT_POSITION = new Vector3(0f, -200f, 0f);

        /// <summary>브리지 execute-method 진입점.</summary>
        public static void Generate()
        {
            int changes = 0;
            Directory.CreateDirectory(ART);
            Directory.CreateDirectory(PREFABS);
            Sprite white = EnsureSprite(WHITE, 4, 4, false, ref changes);
            Sprite circle = EnsureSprite(CIRCLE, 32, 32, true, ref changes);
            GameObject rect = EnsureShapePrefab(RECT_PREFAB, white, ref changes);
            GameObject round = EnsureShapePrefab(CIRCLE_PREFAB, circle, ref changes);
            GameObject label = EnsureLabelPrefab(ref changes);

            TravelCatalog catalog = AssetDatabase.LoadAssetAtPath<TravelCatalog>(CATALOG);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<TravelCatalog>(); AssetDatabase.CreateAsset(catalog, CATALOG); changes++; }
            bool catalogChanged = false;
            if (catalog.WhiteSprite != white) { catalog.WhiteSprite = white; catalogChanged = true; }
            if (catalog.CircleSprite != circle) { catalog.CircleSprite = circle; catalogChanged = true; }
            if (catalog.RectPrefab != rect) { catalog.RectPrefab = rect; catalogChanged = true; }
            if (catalog.CirclePrefab != round) { catalog.CirclePrefab = round; catalogChanged = true; }
            if (catalog.LabelPrefab != label) { catalog.LabelPrefab = label; catalogChanged = true; }
            if (catalogChanged) { EditorUtility.SetDirty(catalog); changes++; }
            AssetDatabase.SaveAssets();

            GameObject rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT_PREFAB);
            if (rootPrefab == null)
            {
                var go = new GameObject("TravelSide");
                foreach (string layer in new[] { "Backdrop", "Far", "Mid", "Near", "Train" }) new GameObject(layer).transform.SetParent(go.transform, false);
                var view = go.AddComponent<TravelSideView>();
                var raid = go.AddComponent<TravelRaidSideView>();
                var so = new SerializedObject(view);
                so.FindProperty("_catalog").objectReferenceValue = catalog;
                so.FindProperty("_settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SETTINGS);
                so.FindProperty("_backdrop").objectReferenceValue = go.transform.Find("Backdrop");
                so.FindProperty("_far").objectReferenceValue = go.transform.Find("Far");
                so.FindProperty("_mid").objectReferenceValue = go.transform.Find("Mid");
                so.FindProperty("_near").objectReferenceValue = go.transform.Find("Near");
                so.FindProperty("_train").objectReferenceValue = go.transform.Find("Train");
                so.ApplyModifiedPropertiesWithoutUndo();
                var rso = new SerializedObject(raid);
                rso.FindProperty("_view").objectReferenceValue = view;
                rso.ApplyModifiedPropertiesWithoutUndo();
                rootPrefab = PrefabUtility.SaveAsPrefabAsset(go, ROOT_PREFAB);
                Object.DestroyImmediate(go);
                changes++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            bool sceneChanged = false;
            if (!scene.path.EndsWith("Main.unity")) Debug.LogWarning($"TravelSideBuild: active scene is {scene.path}, expected Main.unity; scene not touched.");
            else if (Object.FindFirstObjectByType<TravelSideView>(FindObjectsInactive.Include) == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, scene);
                instance.transform.position = ROOT_POSITION;
                sceneChanged = true;
                changes++;
            }
            if (sceneChanged) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log($"TravelSideBuild: {(changes == 0 ? "unchanged" : $"changes={changes}")} catalog={CATALOG} root={ROOT_PREFAB} scene={scene.path}");
        }

        private static Sprite EnsureSprite(string path, int w, int h, bool round, ref int changes)
        {
            if (!File.Exists(path))
            {
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float a = 1f;
                        if (round)
                        {
                            float dx = x + 0.5f - w * 0.5f, dy = y + 0.5f - h * 0.5f;
                            a = Mathf.Clamp01(w * 0.5f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        }
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                changes++;
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, w) || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = w;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = round ? FilterMode.Bilinear : FilterMode.Point;
                importer.SaveAndReimport();
                changes++;
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject EnsureShapePrefab(string path, Sprite sprite, ref int changes)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            changes++;
            return prefab;
        }

        private static GameObject EnsureLabelPrefab(ref int changes)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LABEL_PREFAB);
            if (prefab != null) return prefab;
            var go = new GameObject("PrimLabel");
            var text = go.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(6f, 1f);
            text.text = "라벨";
            prefab = PrefabUtility.SaveAsPrefabAsset(go, LABEL_PREFAB);
            Object.DestroyImmediate(go);
            changes++;
            return prefab;
        }
    }
}
