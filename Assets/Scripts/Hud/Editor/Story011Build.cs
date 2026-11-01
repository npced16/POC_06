using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Hud.Demo;

namespace IronRing.Hud.Editor
{
    /// <summary>Story011 소유 데모 애셋만 최초 생성하고 제작자가 저장한 씬 설정은 보존한다.</summary>
    public static class Story011Build
    {
        private const string SCENE_PATH = "Assets/Scenes/Demo/Story011/Story011Hud.unity";
        private const string ICON_PATH = "Assets/UI/Hud/Resources/Hud/Icons/";

        /// <summary>아이콘과 데모 씬이 없을 때 생성하고 기존 씬과 사용자 활성 씬을 보존한다.</summary>
        public static void Create()
        {
            Directory.CreateDirectory(ICON_PATH);
            CreateIcon("coal", new Color32(112, 127, 135, 255), 0);
            CreateIcon("scrap", new Color32(185, 132, 84, 255), 1);
            CreateIcon("water", new Color32(95, 183, 204, 255), 2);
            CreateIcon("grain", new Color32(227, 185, 85, 255), 3);
            AssetDatabase.Refresh();
            if (File.Exists(SCENE_PATH)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Demo Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 7.5f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.09f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.tag = "MainCamera";
            for (int x = -8; x <= 8; x++)
            {
                AddSprite("rail_horizontal", new Vector2(x, 3), 1, 0);
                AddSprite("rail_horizontal", new Vector2(x, -3), 1, 0);
            }
            for (int y = -2; y <= 2; y++)
            {
                AddSprite("rail_vertical", new Vector2(-9, y), 1, 0);
                AddSprite("rail_vertical", new Vector2(9, y), 1, 0);
            }
            AddSprite("rail_curve_se", new Vector2(-9, 3), 1, 0);
            AddSprite("rail_curve_sw", new Vector2(9, 3), 1, 0);
            AddSprite("rail_curve_ne", new Vector2(-9, -3), 1, 0);
            AddSprite("rail_curve_nw", new Vector2(9, -3), 1, 0);
            Vector2[] positions = { new Vector2(-6, 3), new Vector2(6, 3), new Vector2(6, -3), new Vector2(-6, -3) };
            var hubs = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                hubs[i] = AddSprite("hub_" + (i + 1), positions[i], 2, 2);
                hubs[i].gameObject.AddComponent<BoxCollider2D>();
            }
            AddSprite("waypoint", new Vector2(0, 3), 1, 1);
            AddSprite("waypoint", new Vector2(0, -3), 1, 1);
            Transform train = AddSprite("train", positions[0] + Vector2.down, 1.25f, 3);
            new GameObject("Story011 Demo World").AddComponent<HudDemoWorld>().Configure(camera, hubs, train);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
            AssetDatabase.SaveAssets();
        }

        /// <summary>name 그림을 position, scale, order로 배치하고 생성된 Transform을 반환한다.</summary>
        private static Transform AddSprite(string name, Vector2 position, float scale, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/" + name + ".png");
            renderer.sortingOrder = order; renderer.transform.position = position;
            renderer.transform.localScale = Vector3.one * scale;
            return renderer.transform;
        }

        /// <summary>name, color, shape로 자원 픽토그램을 최초 생성하고 point 필터 텍스처 애셋으로 저장한다.</summary>
        private static void CreateIcon(string name, Color32 color, int shape)
        {
            string path = ICON_PATH + name + ".png";
            if (File.Exists(path)) return;
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                bool fill;
                if (shape == 0) fill = (x >= 2 && x <= 7 && y >= 3 && y <= 8) || (x >= 7 && x <= 13 && y >= 2 && y <= 7) || (x >= 5 && x <= 10 && y >= 7 && y <= 12);
                else if (shape == 1) fill = (y >= 3 && y <= 5 && x >= 2 && x <= 13) || (y >= 10 && y <= 12 && x >= 2 && x <= 13) || (x >= 6 && x <= 9 && y >= 4 && y <= 11);
                else if (shape == 2) fill = (y >= 2 && y <= 8 && Mathf.Abs(x - 8) <= 4) || (y >= 9 && y <= 13 && Mathf.Abs(x - 8) <= 13 - y);
                else fill = (x == 7 && y >= 1 && y <= 13) || (y >= 4 && y <= 12 && y % 3 != 0 && ((x >= 3 && x <= 6) || (x >= 8 && x <= 11)));
                texture.SetPixel(x, y, fill ? color : new Color32(0, 0, 0, 0));
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.SaveAndReimport();
        }
    }
}
