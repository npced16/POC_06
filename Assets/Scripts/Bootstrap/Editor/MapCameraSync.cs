using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

namespace IronRing.Bootstrap.Editor
{
    /// <summary>
    /// 스토리 034 후속. 씬 전체를 다시 만드는 BuildTopDownMain 없이, ring-track.json `_map._cameraSize`를 열린 Main 씬의
    /// Main Camera(선로 화면 기본 직교 크기)에 반영한다. 값이 같으면 아무것도 하지 않는다(멱등).
    /// </summary>
    public static class MapCameraSync
    {
        private const string MAIN = "Assets/Scenes/Main.unity";

        /// <summary>트랙 데이터의 카메라 크기를 씬 카메라에 맞추고, 바뀌었으면 씬을 저장한다.</summary>
        [MenuItem("IronRing/Map/Sync Camera Size From Track Data")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MAIN) scene = EditorSceneManager.OpenScene(MAIN, OpenSceneMode.Single);
            float size = BuildTopDownMain.LoadMapData().Settings.CameraSize;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Main Camera" || !root.TryGetComponent(out Camera camera)) continue;
                if (Mathf.Approximately(camera.orthographicSize, size)) { Debug.Log("MapCameraSync: unchanged " + size); return; }
                camera.orthographicSize = size;
                EditorUtility.SetDirty(camera);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("MapCameraSync: camera size -> " + size);
                return;
            }
            Debug.LogWarning("MapCameraSync: Main Camera not found");
        }
    }
}
