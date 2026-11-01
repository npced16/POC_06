using System.IO;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using IronRing.Presentation.Effects;
using IronRing.Presentation.Map;

using RaidCatalog = IronRing.Combat.Raid.RaidCatalog;

namespace IronRing.Presentation.Raid.Editor
{
    /// <summary>
    /// 스토리 033 제작 도구(멱등). Assets/Prefabs/Raid에 RaidCatalog.asset과 Monster·Nest·Shell·Hit 프리팹을 없을 때만 만들고,
    /// 카탈로그의 프리팹·연기 참조를 채우고, 열린 씬의 MapView 오브젝트에 RaidView가 없으면 붙여 카탈로그를 넣고 씬을 저장한다.
    /// 스프라이트 필드는 아트 담당(SpriteGenRaid)이 이름으로 채운다.
    /// </summary>
    public static class RaidBuild
    {
        private const string FOLDER = "Assets/Prefabs/Raid";
        private const string CATALOG = FOLDER + "/RaidCatalog.asset";
        private const string SMOKE = "Assets/Prefabs/Fx/Smoke.prefab";

        /// <summary>브리지 execute-method 진입점.</summary>
        public static void Generate()
        {
            Directory.CreateDirectory(FOLDER);
            RaidCatalog catalog = AssetDatabase.LoadAssetAtPath<RaidCatalog>(CATALOG);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RaidCatalog>();
                AssetDatabase.CreateAsset(catalog, CATALOG);
            }
            var so = new SerializedObject(catalog);
            Assign(so, "_monsterPrefab", Prefab("Monster", true, true, 0.6f));
            Assign(so, "_nestPrefab", Prefab("Nest", true, false, 1.2f));
            Assign(so, "_shellPrefab", Prefab("Shell", false, false, 0.2f));
            Assign(so, "_hitPrefab", Prefab("Hit", false, false, 0.5f));
            Assign(so, "_smokePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(SMOKE));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            MapView map = Object.FindFirstObjectByType<MapView>(FindObjectsInactive.Include);
            if (map == null) { UnityEngine.Debug.LogWarning("RaidBuild: no MapView in the open scene; RaidView not added."); return; }
            RaidView view = map.GetComponent<RaidView>();
            bool changed = false;
            if (view == null) { view = map.gameObject.AddComponent<RaidView>(); changed = true; }
            var viewSo = new SerializedObject(view);
            SerializedProperty property = viewSo.FindProperty("_catalog");
            if (property.objectReferenceValue != catalog) { property.objectReferenceValue = catalog; changed = true; }
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                EditorSceneManager.SaveScene(map.gameObject.scene);
            }
            UnityEngine.Debug.Log($"RaidBuild: catalog={CATALOG} view={(changed ? "added/updated" : "unchanged")} scene={map.gameObject.scene.path}");
        }

        private static void Assign(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null && property.objectReferenceValue == null) property.objectReferenceValue = value;
        }

        /// <summary>name 프리팹이 없으면 만든다: SpriteRenderer(+ 클릭용 BoxCollider2D·RaidPiece, + 2프레임 SpriteFrameLoop). 스프라이트는 실행 중 RaidView가 넣는다.</summary>
        private static GameObject Prefab(string name, bool clickable, bool animated, float colliderSize)
        {
            string path = FOLDER + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.AddComponent<SpriteRenderer>();
            if (animated) go.AddComponent<SpriteFrameLoop>();
            if (clickable)
            {
                go.AddComponent<BoxCollider2D>().size = Vector2.one * colliderSize;
                go.AddComponent<RaidPiece>();
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
