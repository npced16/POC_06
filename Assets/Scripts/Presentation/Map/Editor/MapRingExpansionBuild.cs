using System;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Track;

namespace IronRing.Presentation.Map.Editor
{
    /// <summary>
    /// 지도 선로 확장(MapRingExpansion)을 씬의 Top Down Map에 붙이는 생성기.
    /// 이미 붙어 있으면 Inspector에서 고친 값은 그대로 두고 비어 있는 참조만 채운다.
    /// </summary>
    public static class MapRingExpansionBuild
    {
        private const string MAIN_SCENE = "Assets/Scenes/Main.unity";
        private const string CATALOG = MapPrefabs.ROOT + "MapCatalog.asset";
        private const string RAILS_NAME = "Rails";

        /// <summary>
        /// Main 씬에 선로 확장을 설치하고 결과를 콘솔에 남긴다.
        /// MAIN_SCENE을 사용하며, Main 씬을 저장한다.
        /// </summary>
        [MenuItem("IronRing/Map/Install Ring Expansion (Main)")]
        public static void InstallMain() => Debug.Log(Install(MAIN_SCENE));

        /// <summary>
        /// 지금 활성화된 씬에 선로 확장을 설치하고 결과를 콘솔에 남긴다.
        /// 활성 씬 경로를 사용하며, 그 씬을 저장한다.
        /// </summary>
        [MenuItem("IronRing/Map/Install Ring Expansion (Active Scene)")]
        public static void InstallActive() => Debug.Log(Install(SceneManager.GetActiveScene().path));

        /// <summary>
        /// scenePath 씬을 열어 MapView 오브젝트에 MapRingExpansion을 붙이고 참조와 처음 값을 채운 뒤 저장한다.
        /// 씬이 열려 있지 않았으면 추가로 열었다가 닫는다. 설치 결과 문구를 반환한다.
        /// </summary>
        public static string Install(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode에서는 설치할 수 없습니다.");
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                MapView map = Find<MapView>(scene);
                TrainController train = Find<TrainController>(scene);
                if (map == null || train == null) throw new InvalidOperationException(scenePath + "에 MapView 또는 TrainController가 없습니다.");
                MapRingExpansion expansion = map.GetComponent<MapRingExpansion>();
                bool created = expansion == null;
                if (created) expansion = map.gameObject.AddComponent<MapRingExpansion>();

                SerializedObject so = new SerializedObject(expansion);
                FillIfEmpty(so, "_map", map);
                FillIfEmpty(so, "_train", train);
                FillIfEmpty(so, "_rails", map.transform.Find(RAILS_NAME));
                FillIfEmpty(so, "_catalog", AssetDatabase.LoadAssetAtPath<MapCatalog>(CATALOG));
                FillIfEmpty(so, "_camera", map.MapCamera != null ? map.MapCamera : Find<Camera>(scene));
                // 중심은 처음 설치할 때만 역 배치의 가운데로 정하고 이후에는 Inspector 값을 보존한다.
                if (created) so.FindProperty("_center").vector2Value = RingCenter(map);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Vector2 center = so.FindProperty("_center").vector2Value;
                return $"{scenePath}: {(created ? "added" : "kept")} MapRingExpansion center={center} tiles={so.FindProperty("_tilesPerOpenedHub").intValue}";
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 직렬화 필드 name이 비어 있을 때만 value를 넣는다.
        /// so와 name, value를 사용하며, 비어 있던 참조를 변경한다.
        /// </summary>
        private static void FillIfEmpty(SerializedObject so, string name, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property.objectReferenceValue == null) property.objectReferenceValue = value;
        }

        /// <summary>
        /// 지도 역 위치의 가로·세로 범위 가운데를 정수로 반올림해 돌려준다.
        /// map 아래 MapNode 위치를 사용하며, 상태는 바꾸지 않는다.
        /// </summary>
        private static Vector2 RingCenter(MapView map)
        {
            MapNode[] nodes = map.GetComponentsInChildren<MapNode>(true);
            if (nodes.Length == 0) return Vector2.zero;
            Vector2 min = nodes[0].transform.position;
            Vector2 max = min;
            foreach (MapNode node in nodes)
            {
                min = Vector2.Min(min, node.transform.position);
                max = Vector2.Max(max, node.transform.position);
            }
            Vector2 center = (min + max) * 0.5f;
            return new Vector2(Mathf.Round(center.x), Mathf.Round(center.y));
        }

        /// <summary>scene의 루트들 아래에서 T 컴포넌트를 하나 찾아 반환하고 없으면 null을 반환한다.</summary>
        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
