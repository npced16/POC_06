using System;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

namespace IronRing.Combat.Editor
{
    /// <summary>
    /// BattleReference 씬의 열차와 선로를 전투 카메라의 세로 가운데로 옮기고, 적이 위·아래 끝에서 나오도록 BattlePocDriver의 높이 값을 한 번 설정한다.
    /// 메뉴에서 실행할 때만 적용되며, 이후 Inspector에서 고친 값은 다시 실행하기 전까지 유지된다.
    /// </summary>
    public static class BattleLayoutBuild
    {
        private const string SCENE = "Assets/Scenes/BattleReference.unity";
        private const string TRAIN_ART = "Side View Train";
        private const string RAILWAY_ROOT = "Battle Railway";
        private const float SPAWN_MARGIN = 0.7f;

        /// <summary>
        /// 열차 그림 중심이 카메라 세로 중심에 오도록 열차와 선로 루트를 옮기고 열차·위·아래 출현 높이를 설정한 뒤 씬을 저장한다.
        /// 씬의 전투 카메라, 열차 루트, 선로 루트를 사용하며, 각 Transform과 Driver의 _trainY·_spawnY·_bottomSpawnY를 변경한다.
        /// </summary>
        [MenuItem("IronRing/Battle/Center Train Layout")]
        public static void CenterTrain()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before changing the battle layout.");
            Scene scene = SceneManager.GetSceneByPath(SCENE);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException("BattleReference has unsaved changes; save it first.");

            BattlePocDriver driver = null;
            Transform railway = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (driver == null) driver = root.GetComponentInChildren<BattlePocDriver>(true);
                if (root.name == RAILWAY_ROOT) railway = root.transform;
            }
            if (driver == null) throw new InvalidOperationException("BattleReference has no BattlePocDriver.");
            var serialized = new SerializedObject(driver);
            Camera battleCamera = (Camera)serialized.FindProperty("_battleCamera").objectReferenceValue;
            Transform train = ((Transform)serialized.FindProperty("_turretSlots").GetArrayElementAtIndex(0).objectReferenceValue).parent;
            Transform trainArt = train.Find(TRAIN_ART);
            if (battleCamera == null || trainArt == null) throw new InvalidOperationException("Battle camera or train art missing.");

            // 열차 그림의 세로 중심을 카메라 중심에 맞추는 이동량이며, 선로와 Driver의 열차 높이도 같은 양만큼 옮긴다.
            float cameraY = battleCamera.transform.position.y;
            float delta = cameraY - trainArt.GetComponent<Renderer>().bounds.center.y;
            // 씬을 바로 저장하므로 Undo 기록을 남기지 않는다. Undo 기록은 열려 있는 다른 씬까지 변경 표시를 남길 수 있다.
            train.position += Vector3.up * delta;
            if (railway != null) railway.position += Vector3.up * delta;
            serialized.FindProperty("_trainY").floatValue += delta;
            serialized.FindProperty("_spawnY").floatValue = cameraY + battleCamera.orthographicSize - SPAWN_MARGIN;
            serialized.FindProperty("_bottomSpawnY").floatValue = cameraY - battleCamera.orthographicSize + SPAWN_MARGIN;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"BATTLE LAYOUT: train moved {delta:0.00} to camera center, railway {(railway != null ? "moved" : "missing")}");
        }
    }
}
