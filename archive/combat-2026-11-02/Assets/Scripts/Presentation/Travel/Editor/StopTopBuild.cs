using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRing.Presentation.Travel.Editor
{
    /// <summary>
    /// 스토리 038 생성기(멱등). 037의 TravelCatalog·PrimRect/PrimCircle/PrimLabel을 재사용해 StopTop.prefab
    /// (StopTopView + StopRaidTopView + ScrapWorkerView, 자식 Ground·Train)을 없을 때만 만들고, 열린 Main 씬에 StopTop 인스턴스(y = −400)가
    /// 없으면 놓고 저장한다. 카탈로그가 없으면 TravelSideBuild.Generate를 먼저 부른다. 두 번째 실행은 "unchanged".
    /// </summary>
    public static class StopTopBuild
    {
        private const string PREFABS = "Assets/Prefabs/Travel";
        private const string CATALOG = PREFABS + "/TravelCatalog.asset";
        private const string ROOT_PREFAB = PREFABS + "/StopTop.prefab";
        private const string SETTINGS = "Assets/Scripts/Bootstrap/Data/MainSettings.json";
        private static readonly Vector3 ROOT_POSITION = new Vector3(0f, -400f, 0f);

        /// <summary>브리지 execute-method 진입점.</summary>
        public static void Generate()
        {
            int changes = 0;
            TravelCatalog catalog = AssetDatabase.LoadAssetAtPath<TravelCatalog>(CATALOG);
            if (catalog == null || catalog.RectPrefab == null) { TravelSideBuild.Generate(); catalog = AssetDatabase.LoadAssetAtPath<TravelCatalog>(CATALOG); changes++; }

            GameObject rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT_PREFAB);
            if (rootPrefab == null)
            {
                var go = new GameObject("StopTop");
                foreach (string child in new[] { "Ground", "Train" }) new GameObject(child).transform.SetParent(go.transform, false);
                var view = go.AddComponent<StopTopView>();
                var raid = go.AddComponent<StopRaidTopView>();
                var workers = go.AddComponent<ScrapWorkerView>();
                var so = new SerializedObject(view);
                so.FindProperty("_catalog").objectReferenceValue = catalog;
                so.FindProperty("_settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SETTINGS);
                so.FindProperty("_ground").objectReferenceValue = go.transform.Find("Ground");
                so.FindProperty("_train").objectReferenceValue = go.transform.Find("Train");
                so.ApplyModifiedPropertiesWithoutUndo();
                foreach (Object component in new Object[] { raid, workers })
                {
                    var cso = new SerializedObject(component);
                    cso.FindProperty("_view").objectReferenceValue = view;
                    cso.ApplyModifiedPropertiesWithoutUndo();
                }
                rootPrefab = PrefabUtility.SaveAsPrefabAsset(go, ROOT_PREFAB);
                Object.DestroyImmediate(go);
                changes++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Main.unity")) Debug.LogWarning($"StopTopBuild: active scene is {scene.path}, expected Main.unity; scene not touched.");
            else if (Object.FindFirstObjectByType<StopTopView>(FindObjectsInactive.Include) == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, scene);
                instance.transform.position = ROOT_POSITION;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changes++;
            }
            Debug.Log($"StopTopBuild: {(changes == 0 ? "unchanged" : $"changes={changes}")} root={ROOT_PREFAB} scene={scene.path}");
        }
    }
}
