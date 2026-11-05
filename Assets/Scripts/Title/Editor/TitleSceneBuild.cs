using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

namespace IronRing.Title.Editor
{
    /// <summary>
    /// 스펙 002 FR-001·FR-002. Title.unity에 카메라·EventSystem·TitleCanvas 인스턴스 중 없는 것만 만들고, 빌드 목록을 Title 0번·Main 1번으로 맞춘다(멱등).
    /// Title 씬은 추가 모드로 열고 닫아서, 열려 있는 Main 씬은 바꾸지 않는다.
    /// </summary>
    public static class TitleSceneBuild
    {
        /// <summary>타이틀 씬 경로.</summary>
        public const string TITLE_SCENE = "Assets/Scenes/Title.unity";
        /// <summary>플레이 씬 경로.</summary>
        public const string MAIN_SCENE = "Assets/Scenes/Main.unity";

        private static readonly Color CAMERA_BACK = new Color32(0x2a, 0x23, 0x31, 0xff);

        /// <summary>
        /// Title 씬을 만들거나 열어 빠진 오브젝트를 보충하고 저장한 뒤 닫는다. 끝으로 빌드 목록 순서를 맞춘다.
        /// TitleCanvas 프리팹이 없으면 먼저 TitleCanvasBuilder로 만든다.
        /// </summary>
        [MenuItem("IronRing/Title/Build Title Scene")]
        public static void Generate()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TitleCanvasBuilder.PREFAB);
            if (prefab == null)
            {
                TitleCanvasBuilder.Generate();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TitleCanvasBuilder.PREFAB);
            }

            Scene scene = SceneManager.GetSceneByPath(TITLE_SCENE);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
            {
                scene = File.Exists(TITLE_SCENE)
                    ? EditorSceneManager.OpenScene(TITLE_SCENE, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }

            EnsureCamera(scene);
            EnsureEventSystem(scene);
            EnsureCanvas(scene, prefab);

            EditorSceneManager.SaveScene(scene, TITLE_SCENE);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            OrderBuildScenes();
            Debug.Log("TITLE: Title.unity 갱신, 빌드 목록 Title 0번 · Main 1번");
        }

        /// <summary>scene 루트에 Camera가 없으면 배경색을 칠하는 직교 카메라를 만든다.</summary>
        private static void EnsureCamera(Scene scene)
        {
            if (FindRoot<Camera>(scene) != null) return;
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            Camera cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CAMERA_BACK;
            go.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        /// <summary>scene 루트에 EventSystem이 없으면 InputSystemUIInputModule과 함께 만들고, 있으면 입력 모듈만 보충한다.</summary>
        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem events = FindRoot<EventSystem>(scene);
            if (events == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(go, scene);
                return;
            }
            if (events.GetComponent<InputSystemUIInputModule>() == null) events.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>scene 루트에 TitleScreen이 없으면 prefab 인스턴스를 놓는다.</summary>
        private static void EnsureCanvas(Scene scene, GameObject prefab)
        {
            if (FindRoot<TitleScreen>(scene) != null) return;
            PrefabUtility.InstantiatePrefab(prefab, scene);
        }

        /// <summary>scene 루트 오브젝트와 그 자식에서 T 컴포넌트를 찾아 첫 번째를 돌려준다(없으면 null).</summary>
        private static T FindRoot<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>빌드 목록을 Title 0번, Main 1번으로 두고 그 밖의 씬은 원래 순서와 활성 여부를 유지해 뒤에 붙인다.</summary>
        private static void OrderBuildScenes()
        {
            var ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(TITLE_SCENE, true),
                new EditorBuildSettingsScene(MAIN_SCENE, true),
            };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.path != TITLE_SCENE && s.path != MAIN_SCENE) ordered.Add(s);
            EditorBuildSettings.scenes = ordered.ToArray();
        }
    }
}
