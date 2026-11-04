using System;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;

namespace IronRing.Presentation.Map.Editor
{
    /// <summary>지도 선로 열차 프리팹에 기관차 그림을 배치하는 제작 도구.</summary>
    public static class MapTrainArtBuild
    {
        private const string MAIN_SCENE = "Assets/Scenes/Main.unity";
        private const string TRAIN_PREFAB = "Assets/Prefabs/Map/Train.prefab";
        private const string LOCOMOTIVE_IMAGE = "Assets/Art/Generated/train_locomotive.png";

        /// <summary>
        /// 열린 Main 씬을 확인하고 기관차 스프라이트를 Train.prefab에 적용한다.
        /// 씬 인스턴스가 참조하는 프리팹만 저장하며 씬의 수동 편집은 유지한다.
        /// </summary>
        [MenuItem("IronRing/Map/Apply Train Art")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing train prefabs.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE) throw new InvalidOperationException("Main scene must be active.");

            Sprite locomotive = AssetDatabase.LoadAssetAtPath<Sprite>(LOCOMOTIVE_IMAGE);
            if (locomotive == null) throw new InvalidOperationException("Train locomotive sprite is missing.");

            GameObject root = PrefabUtility.LoadPrefabContents(TRAIN_PREFAB);
            try
            {
                SpriteRenderer original = root.GetComponent<SpriteRenderer>();
                bool changed = original.enabled;
                original.enabled = false;
                changed |= EnsureVisual(root.transform, "LocomotiveVisual", locomotive,
                    Vector3.zero, new Vector3(2.7f, 1.4f, 1f), original.sharedMaterial, original.sortingOrder);
                foreach (string name in new[] { "CargoVisual 1", "CargoVisual 2" })
                {
                    Transform cargo = root.transform.Find(name);
                    if (cargo == null) continue;
                    UnityEngine.Object.DestroyImmediate(cargo.gameObject);
                    changed = true;
                }

                Transform smoke = root.transform.Find("Smoke");
                if (smoke != null && (Mathf.Approximately(smoke.localPosition.y, 0.62f)
                    || Mathf.Approximately(smoke.localPosition.y, 0.15f)))
                {
                    smoke.localPosition = new Vector3(smoke.localPosition.x, 0.28f, smoke.localPosition.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(smoke);
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, TRAIN_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// parent 아래 name의 그림을 찾거나 만들고 sprite, material, order를 반영한다.
        /// position은 새로 만들 때만 설정하고 기존 기본 크기만 새 크기로 갱신한 뒤 변경 여부를 반환한다.
        /// </summary>
        private static bool EnsureVisual(Transform parent, string name, Sprite sprite, Vector3 position, Vector3 scale, Material material, int order)
        {
            Transform visual = parent.Find(name);
            bool created = visual == null;
            bool resized = false;
            if (created)
            {
                visual = new GameObject(name).transform;
                visual.SetParent(parent, false);
                visual.localPosition = position;
                visual.localScale = scale;
            }
            else if (Mathf.Approximately(visual.localScale.x, 1.5f)
                && Mathf.Approximately(visual.localScale.y, 0.78f))
            {
                visual.localScale = scale;
                resized = true;
            }

            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            bool changed = created || resized || renderer.sprite != sprite || renderer.sharedMaterial != material || renderer.sortingOrder != order;
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return changed;
        }
    }
}
