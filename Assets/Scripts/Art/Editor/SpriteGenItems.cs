using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

using IronRing.Presentation.Items;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 013. 아이템 12종의 32픽셀 도트를 SpriteGen(008)과 같은 팔레트·임포터 설정으로 생성한다.
    /// 출력: Assets/Art/Generated/items/*.png, items_contact_sheet.png, 그리고 런타임 조회용
    /// Assets/Scripts/Items/Resources/Items/ItemSprites.asset(ItemSpriteSet). SpriteGen.cs는 건드리지 않는다.
    /// </summary>
    public static class SpriteGenItems
    {
        private const int SIZE = 32;
        private const string OUTPUT = "Assets/Art/Generated/items/";
        private const string SET_DIR = "Assets/Scripts/Items/Resources/Items/";
        private const string SET_PATH = SET_DIR + "ItemSprites.asset";
        private const string CATALOG = "Assets/Scripts/Items/Resources/Items/ItemCatalog.json";
        private const string EVIDENCE = "production/qa/evidence/story-013-items-tag-habitat/";
        private static Color32[] _palette;
        private static Color32[] _pixels;

        /// <summary>카탈로그 순서대로 아이템 PNG를 만들고 임포터와 ItemSpriteSet을 갱신한다.</summary>
        [MenuItem("IronRing/Art/Generate Item Sprites")]
        public static void Generate()
        {
            var data = JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/Art/palette.json"));
            _palette = data.Colors.Select(value => ColorUtility.TryParseHtmlString(value, out Color color)
                ? (Color32)color : throw new FormatException(value)).ToArray();
            if (_palette.Length != 12) throw new FormatException("Palette requires 12 colors");
            List<string> ids = IronRing.Items.ItemCatalog.Parse(File.ReadAllText(CATALOG)).Items.Select(i => i.Id).ToList();
            Directory.CreateDirectory(OUTPUT);
            Directory.CreateDirectory(SET_DIR);
            var sprites = new List<Color32[]>();
            foreach (string id in ids)
            {
                Begin();
                if (!Draw(id)) throw new InvalidOperationException("No drawing for item " + id);
                Save(OUTPUT + id + ".png");
                sprites.Add(_pixels);
            }
            const int cell = 40;
            var sheet = new Texture2D(cell * 6, cell * 2, TextureFormat.RGBA32, false);
            sheet.SetPixels32(Enumerable.Repeat(_palette[1], sheet.width * sheet.height).ToArray());
            for (int i = 0; i < sprites.Count; i++)
            {
                Color32[] px = sprites[i];
                int ox = (i % 6) * cell + 4, oy = (1 - i / 6) * cell + 4;
                for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
                    if (px[y * SIZE + x].a > 0) sheet.SetPixel(ox + x, oy + y, px[y * SIZE + x]);
            }
            sheet.Apply();
            byte[] png = sheet.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sheet);
            File.WriteAllBytes(OUTPUT + "items_contact_sheet.png", png);
            Directory.CreateDirectory(EVIDENCE);
            File.WriteAllBytes(EVIDENCE + "items_contact_sheet.png", png);
            AssetDatabase.Refresh();
            foreach (string id in ids) ConfigureImporter(OUTPUT + id + ".png");
            ConfigureImporter(OUTPUT + "items_contact_sheet.png");

            var set = AssetDatabase.LoadAssetAtPath<ItemSpriteSet>(SET_PATH);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ItemSpriteSet>();
                AssetDatabase.CreateAsset(set, SET_PATH);
            }
            set.SetEntries(ids.Select(id => new KeyValuePair<string, Sprite>(id, AssetDatabase.LoadAssetAtPath<Sprite>(OUTPUT + id + ".png"))).ToList());
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("SpriteGenItems: " + ids.Count + " item sprites generated.");
        }

        /// <summary>SpriteGen과 같은 설정: 단일 스프라이트, 32 PPU, Point, 무압축.</summary>
        private static void ConfigureImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SIZE;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static void Begin() { _pixels = new Color32[SIZE * SIZE]; }

        private static void Save(string path)
        {
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            texture.SetPixels32(_pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void Pixel(int x, int y, int index)
        {
            if (x >= 0 && x < SIZE && y >= 0 && y < SIZE) _pixels[y * SIZE + x] = _palette[index];
        }

        private static void Rect(int x, int y, int w, int h, int index)
        {
            for (int py = y; py < y + h; py++) for (int px = x; px < x + w; px++) Pixel(px, py, index);
        }

        private static void Disc(int cx, int cy, int radius, int index)
        {
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                if (x * x + y * y <= radius * radius) Pixel(cx + x, cy + y, index);
        }

        /// <summary>
        /// id의 아이템을 그린다. y=0이 아래. 팔레트: 0 외곽선, 1·2 회청, 3 갈색, 4 황동, 5 금, 6 크림, 7·8 청록, 9 옅은 청록, 10 적갈, 11 주황.
        /// </summary>
        private static bool Draw(string id)
        {
            switch (id)
            {
                case "lantern":
                    Rect(10, 2, 12, 3, 0); Rect(11, 3, 10, 1, 3);
                    Rect(14, 4, 4, 18, 0); Rect(15, 5, 2, 17, 2);
                    Rect(10, 21, 12, 9, 0); Rect(11, 22, 10, 7, 4); Rect(12, 23, 8, 5, 5); Rect(14, 24, 4, 3, 6);
                    Rect(9, 29, 14, 2, 3); Pixel(16, 31, 0);
                    return true;
                case "brazier":
                    Rect(9, 2, 2, 7, 0); Rect(21, 2, 2, 7, 0); Rect(15, 2, 2, 6, 0);
                    Rect(7, 8, 18, 8, 0); Rect(8, 9, 16, 6, 3); Rect(6, 15, 20, 2, 4);
                    Disc(16, 21, 5, 10); Disc(16, 21, 3, 11); Disc(16, 20, 1, 5);
                    Rect(15, 26, 2, 3, 11); Rect(12, 24, 2, 3, 10); Rect(19, 24, 2, 3, 10);
                    return true;
                case "water_barrel":
                    Rect(9, 3, 14, 24, 0); Rect(10, 4, 12, 22, 3); Rect(13, 4, 2, 22, 4);
                    Rect(10, 8, 12, 2, 2); Rect(10, 19, 12, 2, 2);
                    Rect(10, 24, 12, 2, 7); Rect(11, 25, 10, 1, 9);
                    return true;
                case "well":
                    Rect(6, 3, 20, 12, 0); Rect(7, 4, 18, 10, 2); Rect(8, 5, 16, 2, 1); Rect(9, 12, 14, 2, 8); Rect(10, 13, 12, 1, 7);
                    Rect(7, 14, 2, 13, 0); Rect(23, 14, 2, 13, 0);
                    Rect(4, 26, 24, 3, 0); Rect(5, 27, 22, 1, 10);
                    Rect(16, 16, 1, 10, 6); Rect(14, 15, 5, 3, 4);
                    return true;
                case "seedling":
                    Rect(10, 2, 12, 10, 0); Rect(11, 3, 10, 8, 10); Rect(9, 11, 14, 3, 0); Rect(10, 12, 12, 1, 11);
                    Rect(15, 14, 2, 8, 8);
                    Rect(10, 16, 5, 3, 7); Rect(17, 18, 5, 3, 7); Rect(11, 17, 3, 1, 9);
                    Disc(16, 24, 4, 5); Disc(16, 24, 2, 11); Pixel(16, 24, 6);
                    return true;
                case "vegetable_patch":
                    Rect(3, 3, 26, 20, 0); Rect(4, 4, 24, 18, 3);
                    Rect(4, 9, 24, 1, 0); Rect(4, 15, 24, 1, 0);
                    foreach (int x in new[] { 7, 15, 23 })
                    {
                        Rect(x, 10, 2, 4, 8); Rect(x - 1, 12, 4, 2, 7);
                        Rect(x, 16, 2, 5, 8); Rect(x - 1, 19, 4, 2, 7); Pixel(x, 21, 9);
                    }
                    Rect(6, 23, 2, 6, 3); Rect(24, 23, 2, 6, 3); Rect(6, 27, 20, 1, 4);
                    return true;
                case "bench":
                    Rect(6, 4, 2, 9, 0); Rect(24, 4, 2, 9, 0);
                    Rect(4, 12, 24, 4, 0); Rect(5, 13, 22, 2, 4);
                    Rect(6, 15, 2, 6, 0); Rect(24, 15, 2, 6, 0);
                    Rect(4, 20, 24, 5, 0); Rect(5, 21, 22, 3, 3); Rect(5, 22, 22, 1, 4);
                    return true;
                case "tent":
                    for (int y = 0; y < 22; y++)
                    {
                        int half = 13 - y * 13 / 22;
                        Rect(16 - half, 4 + y, half * 2, 1, 0);
                        if (half > 1) Rect(17 - half, 4 + y, half * 2 - 2, 1, (y / 3) % 2 == 0 ? 9 : 7);
                    }
                    for (int y = 0; y < 11; y++) { int h = 4 - y * 4 / 11; Rect(16 - h, 4 + y, h * 2, 1, 1); }
                    Rect(15, 25, 2, 5, 0); Rect(16, 28, 4, 2, 10);
                    return true;
                case "storage_crate":
                    Rect(5, 4, 22, 22, 0); Rect(6, 5, 20, 20, 3);
                    for (int i = 0; i < 20; i++) { Pixel(6 + i, 5 + i, 4); Pixel(7 + i, 5 + i, 4); }
                    Rect(6, 14, 20, 2, 0);
                    Rect(5, 4, 3, 3, 4); Rect(24, 4, 3, 3, 4); Rect(5, 23, 3, 3, 4); Rect(24, 23, 3, 3, 4);
                    return true;
                case "gear":
                    Rect(14, 2, 4, 28, 0); Rect(2, 14, 28, 4, 0);
                    Disc(7, 7, 3, 0); Disc(25, 7, 3, 0); Disc(7, 25, 3, 0); Disc(25, 25, 3, 0);
                    Disc(16, 16, 11, 0); Disc(16, 16, 9, 4);
                    Rect(15, 3, 2, 26, 4); Rect(3, 15, 26, 2, 4); Disc(7, 7, 2, 4); Disc(25, 7, 2, 4); Disc(7, 25, 2, 4); Disc(25, 25, 2, 4);
                    Disc(16, 16, 9, 4); Disc(16, 16, 6, 5); Disc(16, 16, 4, 0); Disc(16, 16, 2, 2);
                    return true;
                case "flag":
                    Rect(4, 2, 10, 3, 0); Rect(5, 3, 8, 1, 3);
                    Rect(7, 4, 3, 26, 0); Rect(8, 5, 1, 24, 2); Disc(8, 30, 1, 5);
                    Rect(10, 17, 17, 12, 0); Rect(10, 18, 16, 10, 10); Rect(10, 22, 16, 2, 11);
                    Disc(18, 23, 3, 5); Disc(18, 23, 1, 10);
                    return true;
                case "steam_pipe":
                    Rect(1, 9, 30, 9, 0); Rect(2, 10, 28, 7, 2); Rect(2, 15, 28, 1, 9); Rect(2, 10, 28, 1, 1);
                    Rect(7, 7, 4, 13, 0); Rect(8, 8, 2, 11, 4); Rect(21, 7, 4, 13, 0); Rect(22, 8, 2, 11, 4);
                    Rect(15, 17, 2, 5, 0); Disc(16, 23, 3, 0); Disc(16, 23, 2, 10); Pixel(16, 23, 11);
                    Disc(25, 25, 3, 6); Disc(21, 28, 2, 9); Disc(28, 29, 2, 9);
                    return true;
                default:
                    return false;
            }
        }

        [Serializable]
        private sealed class Palette
        {
            [SerializeField] private string[] _colors;
            public string[] Colors => _colors;
        }
    }
}
