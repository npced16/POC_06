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
    /// 스토리 016. 서식지 타일(아이템 카탈로그의 category "tile")의 32픽셀 바닥 도트를 SpriteGen(008)·SpriteGenItems(013)와 같은
    /// 팔레트·임포터 설정으로 생성한다. 출력: Assets/Art/Generated/tiles/*.png, tiles_contact_sheet.png.
    /// 그리고 ItemSprites.asset(ItemSpriteSet)을 카탈로그 전체 기준으로 다시 채운다: 타일은 tiles/, 나머지는 items/의 PNG.
    /// SpriteGen.cs·SpriteGenItems.cs는 건드리지 않는다.
    /// </summary>
    public static class SpriteGenTiles
    {
        private const int SIZE = 32;
        private const string OUTPUT = "Assets/Art/Generated/tiles/";
        private const string ITEM_OUTPUT = "Assets/Art/Generated/items/";
        private const string SET_DIR = "Assets/Scripts/Items/Resources/Items/";
        private const string SET_PATH = SET_DIR + "ItemSprites.asset";
        private const string CATALOG = "Assets/Scripts/Items/Resources/Items/ItemCatalog.json";
        private const string EVIDENCE = "production/qa/evidence/story-016-habitat-types-clusters/";
        private static Color32[] _palette;
        private static Color32[] _pixels;

        /// <summary>카탈로그의 타일 아이템마다 PNG를 만들고 임포터와 ItemSpriteSet을 갱신한다.</summary>
        [MenuItem("IronRing/Art/Generate Tile Sprites")]
        public static void Generate()
        {
            var data = JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/Art/palette.json"));
            _palette = data.Colors.Select(value => ColorUtility.TryParseHtmlString(value, out Color color)
                ? (Color32)color : throw new FormatException(value)).ToArray();
            if (_palette.Length != 12) throw new FormatException("Palette requires 12 colors");
            var catalog = IronRing.Items.ItemCatalog.Parse(File.ReadAllText(CATALOG));
            List<string> tiles = catalog.Items.Where(i => i.IsTile).Select(i => i.Id).ToList();
            Directory.CreateDirectory(OUTPUT);
            var sprites = new List<Color32[]>();
            foreach (string id in tiles)
            {
                Begin();
                if (!Draw(id)) throw new InvalidOperationException("No drawing for tile " + id);
                Save(OUTPUT + id + ".png");
                sprites.Add(_pixels);
            }

            const int cell = 40, perRow = 7;
            int sheetRows = Math.Max(1, (sprites.Count + perRow - 1) / perRow);
            var sheet = new Texture2D(cell * perRow, cell * sheetRows, TextureFormat.RGBA32, false);
            sheet.SetPixels32(Enumerable.Repeat(_palette[1], sheet.width * sheet.height).ToArray());
            for (int i = 0; i < sprites.Count; i++)
            {
                Color32[] px = sprites[i];
                int ox = (i % perRow) * cell + 4, oy = (sheetRows - 1 - i / perRow) * cell + 4;
                for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
                    if (px[y * SIZE + x].a > 0) sheet.SetPixel(ox + x, oy + y, px[y * SIZE + x]);
            }
            sheet.Apply();
            byte[] png = sheet.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sheet);
            File.WriteAllBytes(OUTPUT + "tiles_contact_sheet.png", png);
            Directory.CreateDirectory(EVIDENCE);
            File.WriteAllBytes(EVIDENCE + "tiles_contact_sheet.png", png);
            AssetDatabase.Refresh();
            foreach (string id in tiles) ConfigureImporter(OUTPUT + id + ".png");
            ConfigureImporter(OUTPUT + "tiles_contact_sheet.png");

            Directory.CreateDirectory(SET_DIR);
            var set = AssetDatabase.LoadAssetAtPath<ItemSpriteSet>(SET_PATH);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ItemSpriteSet>();
                AssetDatabase.CreateAsset(set, SET_PATH);
            }
            var entries = new List<KeyValuePair<string, Sprite>>();
            foreach (var item in catalog.Items)
            {
                string path = (item.IsTile ? OUTPUT : ITEM_OUTPUT) + item.Id + ".png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) entries.Add(new KeyValuePair<string, Sprite>(item.Id, sprite));
                else UnityEngine.Debug.LogWarning("SpriteGenTiles: sprite missing for " + item.Id + " at " + path);
            }
            set.SetEntries(entries);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("SpriteGenTiles: " + tiles.Count + " tile sprites generated, ItemSprites has " + entries.Count + " entries.");
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

        /// <summary>결정적 의사 난수(위치 해시). 같은 입력이면 같은 무늬가 나온다.</summary>
        private static int Hash(int x, int y, int salt)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }

        /// <summary>바탕을 채우고 1픽셀 테두리를 둘러 칸 경계가 보이게 한다.</summary>
        private static void Base(int fill, int rim)
        {
            Rect(0, 0, SIZE, SIZE, fill);
            Rect(0, 0, SIZE, 1, rim); Rect(0, SIZE - 1, SIZE, 1, rim); Rect(0, 0, 1, SIZE, rim); Rect(SIZE - 1, 0, 1, SIZE, rim);
        }

        /// <summary>salt로 흩뿌린 점(밀도 1/every).</summary>
        private static void Speckle(int index, int every, int salt)
        {
            for (int y = 2; y < SIZE - 2; y++) for (int x = 2; x < SIZE - 2; x++)
                if (Hash(x, y, salt) % every == 0) Pixel(x, y, index);
        }

        /// <summary>
        /// id의 타일을 그린다. y=0이 아래. 팔레트: 0 외곽선, 1·2 회청, 3 갈색, 4 황동, 5 금, 6 크림, 7 연청록, 8 진청록, 9 옅은 청록, 10 적갈, 11 주황.
        /// </summary>
        private static bool Draw(string id)
        {
            switch (id)
            {
                case "tile_boiler":
                    Base(1, 0);
                    Rect(6, 3, 20, 26, 0); Rect(7, 4, 18, 24, 4); Rect(8, 4, 3, 24, 5); Rect(21, 4, 3, 24, 3);
                    Rect(7, 9, 18, 1, 3); Rect(7, 22, 18, 1, 3);
                    for (int x = 8; x < 25; x += 3) { Pixel(x, 8, 6); Pixel(x, 23, 6); }
                    Disc(16, 16, 4, 0); Disc(16, 16, 3, 6); Pixel(16, 16, 0); Pixel(17, 17, 10); Pixel(18, 18, 10);
                    Rect(14, 28, 4, 3, 0); Rect(15, 28, 2, 3, 2);
                    return true;
                case "tile_pipe":
                    Base(1, 0);
                    Rect(1, 12, 30, 9, 0); Rect(1, 13, 30, 7, 4); Rect(1, 18, 30, 1, 5); Rect(1, 13, 30, 1, 3);
                    Rect(5, 11, 3, 11, 0); Rect(6, 12, 1, 9, 5); Rect(24, 11, 3, 11, 0); Rect(25, 12, 1, 9, 5);
                    Rect(15, 20, 2, 4, 0); Disc(16, 26, 4, 0); Disc(16, 26, 3, 10); Disc(16, 26, 1, 0);
                    Rect(12, 26, 9, 1, 0); Rect(16, 22, 1, 9, 0);
                    Disc(9, 6, 2, 9); Disc(13, 4, 1, 6);
                    return true;
                case "tile_gear":
                    Base(1, 0);
                    Disc(11, 12, 8, 0); Disc(11, 12, 7, 4); Rect(10, 2, 3, 21, 4); Rect(1, 11, 21, 3, 4);
                    Disc(11, 12, 3, 3); Disc(11, 12, 1, 0);
                    Disc(23, 23, 6, 0); Disc(23, 23, 5, 5); Rect(22, 16, 3, 15, 5); Rect(16, 22, 15, 3, 5);
                    Disc(23, 23, 2, 3); Pixel(23, 23, 0);
                    return true;
                case "tile_gaslamp":
                    Base(1, 0);
                    Disc(16, 23, 9, 2); Disc(16, 23, 6, 3);
                    Rect(15, 3, 3, 16, 0); Rect(16, 4, 1, 15, 2); Rect(12, 3, 9, 2, 0);
                    Rect(12, 18, 9, 9, 0); Rect(13, 19, 7, 7, 5); Rect(14, 20, 5, 5, 6); Rect(16, 19, 1, 7, 4);
                    Rect(11, 27, 11, 2, 0); Pixel(16, 29, 0);
                    return true;
                case "tile_coal":
                    Base(2, 1);
                    for (int y = 0; y < 18; y++) { int half = 13 - y * 12 / 18; Rect(16 - half, 3 + y, half * 2, 1, 0); }
                    for (int i = 0; i < 18; i++) Rect(5 + Hash(i, 1, 31) % 20, 3 + Hash(i, 2, 31) % 14, 2, 2, 1);
                    Pixel(10, 8, 6); Pixel(18, 12, 6); Pixel(14, 16, 9); Pixel(22, 6, 9); Pixel(12, 4, 11);
                    return true;
                case "tile_girder":
                    Base(1, 0);
                    Rect(3, 4, 26, 5, 0); Rect(4, 5, 24, 3, 2); Rect(3, 23, 26, 5, 0); Rect(4, 24, 24, 3, 2);
                    Rect(13, 8, 6, 16, 0); Rect(14, 8, 4, 16, 2); Rect(14, 8, 1, 16, 9);
                    for (int x = 6; x < 27; x += 4) { Pixel(x, 6, 9); Pixel(x, 25, 9); }
                    Pixel(16, 12, 9); Pixel(16, 19, 9);
                    return true;
                case "tile_cobble":
                    Base(10, 0);
                    Rect(1, 1, 30, 30, 1);
                    for (int row = 0; row < 6; row++)
                    {
                        int y = 2 + row * 5, offset = row % 2 == 0 ? 0 : 3;
                        for (int x = 1 + offset; x < SIZE - 1; x += 6) Rect(x, y, 5, 4, Hash(x, y, 33) % 3 == 0 ? 3 : 10);
                    }
                    return true;
                case "tile_tank":
                    Base(1, 0);
                    Rect(5, 3, 22, 26, 0); Rect(6, 4, 20, 24, 2); Rect(6, 4, 20, 12, 8); Rect(6, 15, 20, 1, 9);
                    Rect(6, 4, 3, 24, 1); Rect(6, 11, 20, 1, 1); Rect(6, 22, 20, 1, 1);
                    for (int x = 8; x < 26; x += 3) { Pixel(x, 12, 9); Pixel(x, 23, 9); }
                    Rect(4, 28, 24, 2, 0); Rect(8, 1, 2, 3, 0); Rect(22, 1, 2, 3, 0);
                    return true;
                case "tile_glass":
                    Base(0, 4);
                    Rect(1, 1, 30, 30, 8);
                    for (int i = 0; i < 12; i++) Disc(3 + Hash(i, 3, 35) % 26, 3 + Hash(i, 4, 35) % 14, 2, 7);
                    Rect(1, 1, 30, 1, 4); Rect(1, 10, 30, 1, 4); Rect(1, 20, 30, 1, 4); Rect(1, 30, 30, 1, 4);
                    Rect(1, 1, 1, 30, 4); Rect(11, 1, 1, 30, 4); Rect(21, 1, 1, 30, 4); Rect(30, 1, 1, 30, 4);
                    for (int k = 0; k < 6; k++) { Pixel(4 + k, 23 + k, 9); Pixel(14 + k, 12 + k, 9); Pixel(24 + k, 22 + k, 9); }
                    return true;
                case "tile_bunk":
                    Base(1, 0);
                    Rect(4, 3, 24, 26, 0); Rect(5, 4, 22, 24, 3);
                    Rect(6, 5, 20, 22, 6);
                    Rect(6, 5, 20, 15, 10); Rect(6, 19, 20, 1, 11); Rect(6, 12, 20, 1, 11);
                    Rect(8, 21, 16, 5, 9); Rect(8, 25, 16, 1, 6);
                    return true;
                case "tile_bench":
                    Base(1, 0);
                    Rect(3, 6, 26, 20, 0); Rect(4, 7, 24, 18, 3); Rect(4, 24, 24, 1, 4);
                    for (int y = 10; y < 24; y += 5) Rect(4, y, 24, 1, 0);
                    Rect(7, 13, 10, 3, 2); Rect(15, 12, 4, 5, 2);
                    Rect(20, 9, 2, 12, 4); Rect(19, 19, 4, 3, 2);
                    Disc(9, 21, 2, 5); Pixel(9, 21, 0);
                    return true;
                case "tile_wire":
                    Base(1, 0);
                    Rect(15, 2, 3, 28, 0); Rect(16, 3, 1, 27, 3);
                    Rect(6, 24, 21, 2, 0); Rect(7, 24, 19, 1, 3);
                    Pixel(8, 26, 9); Pixel(16, 26, 9); Pixel(24, 26, 9);
                    for (int x = 0; x < SIZE; x++) { Pixel(x, 21 + (x - 16) * (x - 16) / 80, 5); Pixel(x, 18 + (x - 16) * (x - 16) / 90, 9); }
                    return true;
                case "tile_chimney":
                    Base(1, 0);
                    Rect(9, 2, 14, 20, 0); Rect(10, 3, 12, 18, 10);
                    for (int row = 0; row < 6; row++)
                    {
                        int y = 3 + row * 3, offset = row % 2 == 0 ? 0 : 2;
                        Rect(10, y, 12, 1, 3);
                        for (int x = 10 + offset; x < 22; x += 4) Pixel(x, y + 1, 3);
                    }
                    Rect(8, 20, 16, 3, 0); Rect(9, 21, 14, 1, 2);
                    Disc(14, 26, 3, 2); Disc(19, 27, 3, 9); Disc(24, 29, 2, 9); Disc(10, 29, 2, 2);
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
