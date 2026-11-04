using System;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

namespace IronRing.Presentation.Interior.Editor
{
    /// <summary>마을의 반복 바닥 타일을 넓은 폐허 흙바닥으로 바꾸고 빈 건설 칸의 그림을 숨기는 편집기 제작 도구.</summary>
    public static class WastelandGroundBuild
    {
        private const string SCENE = "Assets/Scenes/Main.unity";
        private const string HUB_PREFAB = "Assets/Prefabs/Interior/HubInterior.prefab";
        private const string CELL_PREFAB = "Assets/Prefabs/Interior/InteriorCell.prefab";
        private const string GROUND_IMAGE = "Assets/Art/Generated/interior_wasteland.png";
        private const string MARKER_IMAGE = "Assets/Art/Generated/interior_buildable_gray.png";
        private const string OLD_MARKER_IMAGE = "Assets/Art/Generated/interior_buildable_soil.png";
        private const int WIDTH = 768;
        private const int HEIGHT = 480;
        private const int PIXELS_PER_UNIT = 32;
        private const int MARKER_SIZE = 64;

        /// <summary>Main 씬과 마을 프리팹의 바닥을 한 장의 황폐한 흙 지면으로 바꾸고, 빈 칸 그림을 숨겨 저장한다.</summary>
        [MenuItem("IronRing/Interior/Build Wasteland Ground")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing town prefabs.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE) throw new InvalidOperationException("Main scene must be active.");
            Sprite ground = CreateGroundSprite();
            Sprite marker = CreateBuildableSprite();

            GameObject cellContents = PrefabUtility.LoadPrefabContents(CELL_PREFAB);
            try
            {
                cellContents.GetComponent<SpriteRenderer>().sprite = null;
                ApplyMarker(cellContents.GetComponent<InteriorCellView>(), marker,
                    cellContents.GetComponent<SpriteRenderer>().sortingOrder - 1);
                PrefabUtility.SaveAsPrefabAsset(cellContents, CELL_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(cellContents); }

            GameObject hubContents = PrefabUtility.LoadPrefabContents(HUB_PREFAB);
            try
            {
                Apply(hubContents.GetComponent<HubInteriorRoot>(), ground, marker);
                PrefabUtility.SaveAsPrefabAsset(hubContents, HUB_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(hubContents); }

            foreach (HubInteriorRoot root in UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (root.gameObject.scene != scene) continue;
                Apply(root, ground, marker);
                PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform.Find("Floor").gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform.Find("Ground").GetComponent<SpriteRenderer>());
                foreach (InteriorCellView cell in root.Cells())
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cell.GetComponent<SpriteRenderer>());
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cell);
                    Transform markerNode = cell.transform.Find("BuildableMarker");
                    PrefabUtility.RecordPrefabInstancePropertyModifications(markerNode);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(markerNode.GetComponent<SpriteRenderer>());
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.DeleteAsset(OLD_MARKER_IMAGE);
            AssetDatabase.SaveAssets();
        }

        /// <summary>고정된 잡음과 흙색을 WIDTH×HEIGHT 픽셀에 그려 단일 지면 스프라이트를 만들고 반환한다.</summary>
        private static Sprite CreateGroundSprite()
        {
            var texture = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
            var pixels = new Color32[WIDTH * HEIGHT];
            for (int y = 0; y < HEIGHT; y++)
            {
                for (int x = 0; x < WIDTH; x++)
                {
                    float broad = Mathf.PerlinNoise(x * 0.009f + 6.3f, y * 0.011f + 18.7f);
                    float mottled = Mathf.PerlinNoise(x * 0.038f + 27.1f, y * 0.042f + 4.9f);
                    float grain = Hash(x, y);
                    float tone = Mathf.Clamp01(broad * 0.52f + mottled * 0.37f + grain * 0.11f);
                    float clay = Mathf.PerlinNoise(x * 0.015f + 41.2f, y * 0.017f + 73.6f);
                    Color dark = clay > 0.57f ? new Color32(49, 43, 37, 255) : new Color32(43, 44, 43, 255);
                    Color light = clay > 0.57f ? new Color32(91, 73, 55, 255) : new Color32(76, 69, 59, 255);
                    Color color = Color.Lerp(dark, light, tone);
                    if (grain > 0.985f && mottled > 0.45f) color = Color.Lerp(color, new Color32(112, 101, 84, 255), 0.55f);
                    pixels[y * WIDTH + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(GROUND_IMAGE));
            File.WriteAllBytes(GROUND_IMAGE, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return ImportSprite(GROUND_IMAGE, PIXELS_PER_UNIT);
        }

        /// <summary>MARKER_SIZE 픽셀 그림에 투명한 중심과 축소 화면에서도 보이는 얇은 테두리를 그려 칸 그리드 스프라이트를 반환한다.</summary>
        private static Sprite CreateBuildableSprite()
        {
            var texture = new Texture2D(MARKER_SIZE, MARKER_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[MARKER_SIZE * MARKER_SIZE];
            for (int y = 0; y < MARKER_SIZE; y++)
            {
                for (int x = 0; x < MARKER_SIZE; x++)
                {
                    bool edge = x >= 2 && x < MARKER_SIZE - 2 && y >= 2 && y < MARKER_SIZE - 2 &&
                        (x < 5 || x >= MARKER_SIZE - 5 || y < 5 || y >= MARKER_SIZE - 5);
                    pixels[y * MARKER_SIZE + x] = edge
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(MARKER_IMAGE, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return ImportSprite(MARKER_IMAGE, MARKER_SIZE);
        }

        /// <summary>path의 PNG를 point 필터와 ppu 기준의 Sprite로 가져와 반환한다.</summary>
        private static Sprite ImportSprite(string path, int ppu)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>x와 y 픽셀 좌표에서 반복 가능한 0~1 잡음값을 반환한다.</summary>
        private static float Hash(int x, int y)
        {
            uint value = unchecked((uint)(x * 374761393 + y * 668265263 + 1442695041));
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value ^ (value >> 16)) / (float)uint.MaxValue;
        }

        /// <summary>root의 반복 Floor를 숨기고 ground를 연결하며, 빈 칸에 marker를 배치한다.</summary>
        private static void Apply(HubInteriorRoot root, Sprite ground, Sprite marker)
        {
            Transform floor = root.transform.Find("Floor");
            floor.gameObject.SetActive(false);
            Transform node = root.transform.Find("Ground");
            if (node == null)
            {
                node = new GameObject("Ground").transform;
                node.SetParent(root.transform, false);
            }
            node.localPosition = Vector3.zero;
            node.localRotation = Quaternion.identity;
            node.localScale = Vector3.one;
            SpriteRenderer renderer = node.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = node.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = ground;
            renderer.sortingOrder = root.Catalog.FloorOrder;
            foreach (InteriorCellView cell in root.Cells())
            {
                cell.SetSprite(null);
                ApplyMarker(cell, marker, root.Catalog.CellOrder - 1);
            }
        }

        /// <summary>cell에 marker 그리드 선을 order 순서로 배치하고 렌더러를 칸 뷰에 연결한다.</summary>
        private static void ApplyMarker(InteriorCellView cell, Sprite marker, int order)
        {
            Transform node = cell.transform.Find("BuildableMarker");
            if (node == null)
            {
                node = new GameObject("BuildableMarker").transform;
                node.SetParent(cell.transform, false);
            }
            node.localPosition = Vector3.zero;
            node.localRotation = Quaternion.identity;
            node.localScale = Vector3.one;
            SpriteRenderer renderer = node.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = node.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = marker;
            renderer.color = Color.white;
            renderer.sortingOrder = order;
            cell.BindBuildableMarker(renderer);
            cell.SetBuildable(true);
        }
    }
}
