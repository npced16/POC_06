using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

namespace IronRing.Art.Editor
{
    /// <summary>팔레트 기반 32픽셀 아트를 외부 도구 없이 결정적으로 생성한다.</summary>
    public static class SpriteGen
    {
        private const int SIZE = 32;
        private const string OUTPUT = "Assets/Art/Generated/";
        private static Color32[] _palette;
        private static Color32[] _pixels;

        /// <summary>palette.json을 읽어 27개 PNG(선로 6, 거점 8, 아이콘 9, 내부 타일 4)와 contact_sheet.png를 생성하고 임포터를 설정한다.</summary>
        [MenuItem("IronRing/Art/Generate Sprites")]
        public static void Generate()
        {
            var data = JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/Art/palette.json"));
            _palette = data.Colors.Select(value => ColorUtility.TryParseHtmlString(value, out Color color)
                ? (Color32)color : throw new FormatException(value)).ToArray();
            if (_palette.Length != 12) throw new FormatException("Palette requires 12 colors");
            Directory.CreateDirectory(OUTPUT);
            var sprites = new List<Color32[]>();
            var names = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                Begin(); Rail(i);
                Save(new[] { "rail_horizontal", "rail_vertical", "rail_curve_ne", "rail_curve_nw", "rail_curve_se", "rail_curve_sw" }[i], sprites, names);
            }
            for (int i = 0; i < 4; i++)
            {
                Begin(); Hub(i, false); Save("hub_" + (i + 1), sprites, names);
                Begin(); Hub(i, true); Save("hub_" + (i + 1) + "_ruined", sprites, names);
            }
            string[] icons = { "train", "waypoint", "raider", "wall", "turret", "workshop", "npc", "prosperity_bg", "prosperity_fill" };
            foreach (string icon in icons) { Begin(); Icon(icon); Save(icon, sprites, names); }
            // Story 012: 거점 내부 탑뷰 타일(바닥, 잔해, 빈 터, 시설 부지).
            string[] tiles = { "interior_floor", "interior_rubble", "interior_empty", "interior_plot" };
            foreach (string tile in tiles) { Begin(); InteriorTile(tile); Save(tile, sprites, names); }
            const int cell = 40;
            int sheetRows = (sprites.Count + 5) / 6;
            var sheet = new Texture2D(cell * 6, cell * sheetRows, TextureFormat.RGBA32, false);
            var background = Enumerable.Repeat(_palette[0], sheet.width * sheet.height).ToArray();
            sheet.SetPixels32(background);
            for (int i = 0; i < sprites.Count; i++)
                sheet.SetPixels32((i % 6) * cell + 4, (sheetRows - 1 - i / 6) * cell + 4, SIZE, SIZE, sprites[i]);
            sheet.Apply();
            File.WriteAllBytes(OUTPUT + "contact_sheet.png", sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            File.WriteAllText(OUTPUT + "manifest.json", "{\"size\":32,\"sprites\":[" + string.Join(",", names.Select(n => "\"" + n + "\"")) + "]}");
            AssetDatabase.Refresh();
            foreach (string name in names.Concat(new[] { "contact_sheet" })) ConfigureImporter(OUTPUT + name + ".png");
            AssetDatabase.SaveAssets();
        }

        /// <summary>path의 PNG를 단일 스프라이트, 32 PPU, Point, 무압축으로 임포트한다.</summary>
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

        /// <summary>새 스프라이트를 위해 투명 픽셀 버퍼를 초기화한다.</summary>
        private static void Begin() { _pixels = new Color32[SIZE * SIZE]; }

        /// <summary>name 파일에 현재 픽셀을 저장하고 시트용 sprites 및 names 목록에 추가한다.</summary>
        private static void Save(string name, List<Color32[]> sprites, List<string> names)
        {
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            texture.SetPixels32(_pixels); texture.Apply();
            File.WriteAllBytes(OUTPUT + name + ".png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            sprites.Add(_pixels); names.Add(name);
        }

        /// <summary>x,y 위치에 팔레트 index 색을 찍어 현재 버퍼를 변경한다.</summary>
        private static void Pixel(int x, int y, int index)
        {
            if (x >= 0 && x < SIZE && y >= 0 && y < SIZE) _pixels[y * SIZE + x] = _palette[index];
        }

        /// <summary>x,y에서 w,h 크기의 사각형을 index 색으로 채운다.</summary>
        private static void Rect(int x, int y, int w, int h, int index)
        {
            for (int py = y; py < y + h; py++) for (int px = x; px < x + w; px++) Pixel(px, py, index);
        }

        /// <summary>cx,cy 중심의 radius 원을 index 색으로 채운다.</summary>
        private static void Disc(int cx, int cy, int radius, int index)
        {
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                if (x * x + y * y <= radius * radius) Pixel(cx + x, cy + y, index);
        }

        /// <summary>kind 방향에 맞는 두 레일과 침목을 현재 타일에 그린다.</summary>
        private static void Rail(int kind)
        {
            for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
            {
                int u = kind == 1 ? y : x;
                int v = kind == 1 ? x : y;
                if (kind < 2)
                {
                    if (v >= 6 && v <= 25 && u % 7 < 3) Pixel(x, y, 3);
                    if (v == 9 || v == 22) Pixel(x, y, 2);
                    if (v == 10 || v == 21) Pixel(x, y, 5);
                }
                else
                {
                    int cx = kind == 2 || kind == 4 ? 31 : 0;
                    int cy = kind == 2 || kind == 3 ? 31 : 0;
                    float radius = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float angle = Mathf.Atan2(Mathf.Abs(y - cy), Mathf.Abs(x - cx));
                    if (radius >= 6 && radius <= 25 && (int)(angle * 16) % 4 < 2) Pixel(x, y, 3);
                    if (Mathf.Abs(radius - 10) < 1 || Mathf.Abs(radius - 21) < 1) Pixel(x, y, 5);
                }
            }
        }

        /// <summary>kind 거점의 탑뷰 실루엣을 그리고 ruined이면 같은 바닥 위에 잔해를 남긴다.</summary>
        private static void Hub(int kind, bool ruined)
        {
            Rect(3, 3, 26, 25, 0); Rect(4, 4, 24, 23, 2); Rect(5, 5, 22, 21, 1);
            if (ruined)
            {
                Rect(6, 6, 3, 18, 3); Rect(7, 6, 17, 3, 3); Rect(23, 8, 3, 14, 2);
                for (int i = 0; i < 9; i++) Rect(9 + (i * 7) % 15, 9 + (i * 5) % 15, 3, 2, i % 2 == 0 ? 2 : 3);
                Rect(14, 14, 5, 3, kind == 1 ? 8 : 10); return;
            }
            if (kind == 0)
            {
                Rect(7, 7, 18, 17, 3); Rect(8, 9, 16, 13, 4); Rect(9, 16, 14, 2, 5);
                Rect(11, 9, 9, 5, 0); Rect(12, 10, 7, 3, 11); Rect(7, 22, 5, 7, 0); Rect(8, 23, 3, 5, 5);
                Disc(10, 29, 2, 9); Rect(21, 20, 4, 6, 2);
            }
            if (kind == 1)
            {
                Rect(7, 7, 18, 17, 3); Rect(9, 9, 14, 13, 8);
                for (int x = 10; x < 23; x += 5) { Rect(x, 10, 2, 11, 7); Rect(x, 13, 3, 2, 9); }
                Rect(6, 23, 20, 3, 5); Rect(15, 7, 2, 16, 5);
            }
            if (kind == 2)
            {
                Rect(6, 6, 20, 19, 8); for (int y = 7; y < 25; y += 4) Rect(7, y, 18, 1, 7);
                Rect(7, 17, 18, 5, 3); Rect(7, 20, 18, 1, 5); Rect(13, 7, 5, 18, 4);
                Rect(8, 9, 3, 5, 6); Rect(20, 10, 3, 4, 5);
            }
            if (kind == 3)
            {
                Rect(7, 7, 18, 18, 3); Rect(10, 10, 12, 12, 2); Rect(12, 12, 8, 8, 4);
                foreach (int x in new[] { 6, 22 }) foreach (int y in new[] { 6, 22 }) { Rect(x, y, 5, 5, 4); Rect(x, y + 3, 5, 2, 5); }
                Rect(14, 5, 4, 5, 0); Rect(15, 15, 2, 5, 6);
            }
            Pixel(5, 5, 5); Pixel(26, 5, 5); Pixel(5, 25, 5); Pixel(26, 25, 5);
        }

        /// <summary>name에 해당하는 열차, 인물, 시설 또는 게이지를 버퍼에 그린다.</summary>
        private static void Icon(string name)
        {
            switch (name)
            {
                case "train":
                    Rect(7, 4, 18, 23, 0); Rect(9, 5, 14, 21, 4); Rect(10, 6, 12, 5, 3);
                    Rect(10, 13, 12, 12, 5); Rect(12, 14, 8, 8, 2); Rect(13, 17, 6, 3, 7);
                    Rect(13, 25, 6, 4, 6); Rect(7, 2, 18, 3, 2); Rect(6, 9, 3, 4, 2); Rect(23, 9, 3, 4, 2);
                    Disc(16, 23, 2, 0); break;
                case "waypoint":
                    Disc(16, 15, 11, 0); Disc(16, 15, 9, 4); Disc(16, 15, 7, 1);
                    Rect(15, 8, 2, 16, 6); Rect(11, 19, 9, 2, 5); Rect(13, 21, 5, 2, 5); break;
                case "raider":
                case "npc":
                    Rect(9, 5, 5, 8, 0); Rect(18, 5, 5, 8, 0); Rect(8, 12, 16, 11, name == "npc" ? 8 : 10);
                    Rect(11, 21, 10, 7, 3); Rect(12, 21, 8, 5, 6); Rect(10, 26, 12, 3, 2);
                    Rect(13, 23, 2, 2, 0); Rect(18, 23, 2, 2, 0); Rect(15, 14, 2, 6, 5); break;
                case "wall":
                    Rect(3, 8, 26, 15, 0); Rect(4, 9, 24, 13, 3);
                    for (int x = 5; x < 28; x += 6) { Rect(x, 10, 4, 11, 4); Rect(x, 20, 4, 3, 5); } break;
                case "turret":
                    Disc(16, 14, 11, 0); Disc(16, 14, 9, 3); Disc(16, 14, 7, 5); Disc(16, 14, 5, 2);
                    Rect(13, 16, 6, 13, 0); Rect(14, 18, 4, 10, 4); Rect(14, 27, 4, 2, 6); break;
                case "workshop":
                    Rect(5, 5, 23, 22, 0); Rect(6, 6, 21, 20, 3); Rect(7, 14, 19, 11, 4);
                    Rect(7, 23, 19, 2, 5); Rect(10, 6, 7, 8, 1); Rect(20, 7, 4, 5, 7);
                    Disc(16, 19, 4, 6); Disc(16, 19, 2, 3); break;
                case "prosperity_bg": Rect(1, 11, 30, 10, 0); Rect(2, 12, 28, 8, 4); Rect(3, 13, 26, 6, 1); break;
                case "prosperity_fill": Rect(3, 13, 26, 6, 8); Rect(3, 16, 26, 3, 7); Rect(3, 18, 26, 1, 9); break;
            }
        }

        /// <summary>name에 해당하는 거점 내부 타일(바닥·잔해·빈 터·시설 부지)을 같은 석판 바닥 위에 그려 격자가 이어지게 한다.</summary>
        private static void InteriorTile(string name)
        {
            // 바닥: 어두운 석판, 왼쪽·아래 이음새, 군데군데 밝은 돌.
            Rect(0, 0, SIZE, SIZE, 1); Rect(0, 0, SIZE, 1, 0); Rect(0, 0, 1, SIZE, 0);
            for (int i = 0; i < 7; i++) Pixel(2 + (i * 11) % 29, 3 + (i * 7) % 27, 2);
            switch (name)
            {
                case "interior_rubble":
                    // 무너진 벽돌과 들보 더미: 그림자(0), 몸체(2/3), 윗면 하이라이트(5), 녹슨 조각(10)과 황동 파편(4).
                    Rect(4, 5, 10, 6, 0); Rect(4, 6, 9, 5, 2); Rect(5, 10, 7, 1, 5);
                    Rect(15, 3, 8, 5, 0); Rect(15, 4, 7, 4, 3); Rect(16, 7, 5, 1, 5);
                    Rect(20, 9, 9, 7, 0); Rect(20, 10, 8, 6, 2); Rect(21, 15, 6, 1, 5);
                    Rect(5, 14, 11, 7, 0); Rect(5, 15, 10, 6, 3); Rect(6, 20, 8, 1, 5);
                    Rect(17, 18, 10, 8, 0); Rect(17, 19, 9, 7, 2); Rect(18, 25, 7, 1, 5);
                    Rect(8, 23, 8, 5, 0); Rect(8, 24, 7, 4, 3); Rect(9, 27, 5, 1, 5);
                    Rect(12, 12, 4, 3, 10); Rect(24, 5, 3, 3, 10); Rect(3, 27, 4, 2, 4);
                    break;
                case "interior_empty":
                    // 치운 빈 터: 밝은 석판과 옅은 테두리, 아이템을 놓을 수 있다는 표시로 가운데 청록 점.
                    Rect(2, 2, 28, 28, 2); Rect(3, 3, 26, 26, 1); Rect(4, 4, 24, 24, 2);
                    for (int y = 6; y < 26; y += 5) for (int x = 6; x < 26; x += 5) Pixel(x, y, 1);
                    Rect(15, 15, 2, 2, 7);
                    break;
                case "interior_plot":
                    // 옛 시설 부지: 갈색 토대 테두리, 네 모서리 황동 기초, 가운데 황동 십자 표식.
                    Rect(3, 3, 26, 26, 3); Rect(5, 5, 22, 22, 1); Rect(6, 6, 20, 20, 2);
                    foreach (int x in new[] { 2, 25 }) foreach (int y in new[] { 2, 25 }) { Rect(x, y, 5, 5, 4); Rect(x, y + 4, 5, 1, 5); Pixel(x + 1, y + 1, 5); }
                    Rect(12, 15, 8, 2, 4); Rect(15, 12, 2, 8, 4);
                    break;
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
