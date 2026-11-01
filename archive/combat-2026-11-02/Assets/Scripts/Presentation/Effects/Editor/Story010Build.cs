using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Presentation.Effects.Demo;

namespace IronRing.Presentation.Effects.Editor
{
    public static class Story010Build
    {
        private const string ROOT = "Assets/Scripts/Presentation/Effects/Resources/";
        private const string SCENE = "Assets/Scenes/Demo/Story010/Story010Effects.unity";

        /// <summary>담당 설정 애셋과 데모 씬을 최초 한 번 생성하고 이후 수동 배치와 설정을 보존한다.</summary>
        public static void Generate()
        {
            Directory.CreateDirectory(ROOT);
            var settings = AssetDatabase.LoadAssetAtPath<EffectsSettings>(ROOT + "Story010Effects.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<EffectsSettings>();
                AssetDatabase.CreateAsset(settings, ROOT + "Story010Effects.asset");
                var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, ROOT + "EffectsSprite.mat");
                var serialized = new SerializedObject(settings);
                string[] paths = Directory.GetFiles("Assets/Art/Generated", "*.png");
                SerializedProperty sprites = serialized.FindProperty("_sprites");
                sprites.arraySize = 0;
                foreach (string path in paths)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'));
                    if (sprite == null) continue;
                    int index = sprites.arraySize++; sprites.GetArrayElementAtIndex(index).objectReferenceValue = sprite;
                }
                serialized.FindProperty("_spriteMaterial").objectReferenceValue = material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (!File.Exists(SCENE))
            {
                Scene previous = SceneManager.GetActiveScene();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("EffectsCamera").AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.098f, 0.118f, 0.149f);
                var demo = new GameObject("EffectsScenario").AddComponent<EffectsDemo>();
                Vector3[] positions = { new Vector3(-4, 2), new Vector3(4, 2), new Vector3(4, -2), new Vector3(-4, -2) };
                var serialized = new SerializedObject(demo);
                serialized.FindProperty("_mapCamera").objectReferenceValue = camera;
                SerializedProperty hubs = serialized.FindProperty("_hubs"); hubs.arraySize = positions.Length;
                for (int index = 0; index < positions.Length; index++) hubs.GetArrayElementAtIndex(index).objectReferenceValue = Place(settings, "hub_" + (index + 1), positions[index], 10);
                for (int x = -3; x <= 3; x++)
                {
                    Place(settings, "rail_horizontal", new Vector3(x, 2), 0);
                    Place(settings, "rail_horizontal", new Vector3(x, -2), 0);
                }
                for (int y = -1; y <= 1; y++)
                {
                    Place(settings, "rail_vertical", new Vector3(-4, y), 0);
                    Place(settings, "rail_vertical", new Vector3(4, y), 0);
                }
                serialized.FindProperty("_waypoint").objectReferenceValue = Place(settings, "waypoint", new Vector3(0, 2), 10);
                Transform train = Place(settings, "train", new Vector3(-1, 2), 15);
                train.rotation = Quaternion.Euler(0, 0, -90);
                serialized.FindProperty("_train").objectReferenceValue = train;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Directory.CreateDirectory(Path.GetDirectoryName(SCENE));
                EditorSceneManager.SaveScene(scene, SCENE);
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>settings에서 name 아트를 읽어 position과 order로 데모 오브젝트를 만들고 Transform을 반환한다.</summary>
        private static Transform Place(EffectsSettings settings, string name, Vector3 position, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite = settings.Sprite(name); renderer.sharedMaterial = settings.SpriteMaterial;
            renderer.sortingOrder = order; renderer.transform.position = position;
            return renderer.transform;
        }
    }
}
