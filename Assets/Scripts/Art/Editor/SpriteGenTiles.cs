using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

using IronRing.Presentation.Items;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 016·019. 서식지 타일(아이템 카탈로그의 category "tile")의 64픽셀 탑뷰 스팀펑크 도트를 돌바닥 위에 그린다(색은 SteampunkPalette.json).
    /// 출력: Assets/Art/Generated/tiles/*.png, tiles_contact_sheet.png. 그리고 ItemSprites.asset(ItemSpriteSet)을 카탈로그 전체 기준으로 다시 채운다:
    /// 타일은 tiles/, 나머지는 items/의 PNG. 그림이 없는 새 타일 id는 경고와 함께 기본 판으로 그린다.
    /// </summary>
    public static class SpriteGenTiles
    {
        private const string OUTPUT = "Assets/Art/Generated/tiles/";
        private const string ITEM_OUTPUT = "Assets/Art/Generated/items/";
        private const string SET_DIR = "Assets/Scripts/Items/Resources/Items/";
        private const string SET_PATH = SET_DIR + "ItemSprites.asset";
        private const string CATALOG = "Assets/Scripts/Items/Resources/Items/ItemCatalog.json";

        /// <summary>카탈로그의 타일 아이템마다 PNG를 만들고 임포터와 ItemSpriteSet을 갱신한다.</summary>
        [MenuItem("IronRing/Art/Generate Tile Sprites")]
        public static void Generate()
        {
            Load();
            var catalog = IronRing.Items.ItemCatalog.Parse(File.ReadAllText(CATALOG));
            List<string> tiles = catalog.Items.Where(i => i.IsTile).Select(i => i.Id).ToList();
            Directory.CreateDirectory(OUTPUT);
            var sprites = new List<Color32[]>();
            foreach (string id in tiles)
            {
                Begin();
                if (id == "tile_cobble")
                {
                    C.Rect(0, 0, SIZE, SIZE, Wd.S);
                    for (int i = 0; i < 36; i++) C.Disc(5 + H(i, 1, 23) % 54, 5 + H(i, 2, 23) % 54, 1 + H(i, 3, 23) % 3, i % 3 == 0 ? Sa.B : Wd.B);
                }
                else Cobble(0, 0, SIZE, SIZE, G, 7);
                C.Rect(0, 0, SIZE, 1, Mo.S); C.Rect(0, 0, 1, SIZE, Mo.S);
                Layer();
                bool known = Draw(id);
                EndLayer();
                if (!known)
                {
                    Debug.LogWarning("SpriteGenTiles: no drawing for tile " + id + ", using a plain riveted plate.");
                    Layer(); Box(10, 10, 44, 44, Fe); RivetRing(32, 32, 18, 12, Bs.S); EndLayer();
                }
                if (id == "tile_gaslamp") Glow(32.5f, 45.5f, 18);
                if (id == "tile_chimney") { Puff(34, 50, 7, 210); Puff(40, 57, 5, 170); Puff(27, 58, 4, 140); }
                if (id == "tile_pipe") { Puff(44, 52, 4, 150); Puff(48, 57, 3, 110); }
                C.Save(OUTPUT + id + ".png");
                sprites.Add(C.Px);
            }
            File.WriteAllBytes(OUTPUT + "tiles_contact_sheet.png", Sheet(sprites, 7));
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
                else Debug.LogWarning("SpriteGenTiles: sprite missing for " + item.Id + " at " + path);
            }
            set.SetEntries(entries);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("SpriteGenTiles: " + tiles.Count + " tile sprites generated, ItemSprites has " + entries.Count + " entries.");
        }

        /// <summary>id 타일의 물체를 현재 레이어에 그린다(y=0이 아래). 모르는 id면 false.</summary>
        private static bool Draw(string id)
        {
            switch (id)
            {
                case "tile_boiler":
                    C.Ellipse(32, 9, 19, 4, Cu.S);
                    CylV(13, 9, 38, 42, Cu);
                    C.Ellipse(32, 51, 19, 5, Cu.L); C.Ellipse(32, 51, 15, 3, Cu.B);
                    foreach (int y in new[] { 17, 41 }) { C.Rect(13, y, 38, 2, Bs.B); C.Rect(13, y + 1, 38, 1, Bs.L); RivetsH(15, 49, y, 4, O.B); }
                    C.Disc(32, 29, 8, Bs.S); C.Disc(32, 29, 7, Bs.B); C.Disc(32, 29, 5.5f, Sm.L);
                    C.Line(32, 29, 36, 32, O.B); C.Set(28, 31, Em.B); C.Set(31, 33, O.S); C.Set(35, 33, O.S);
                    C.Rect(28, 54, 8, 6, Fe.B); C.Rect(28, 54, 2, 6, Fe.L); C.Rect(27, 59, 10, 2, Bs.S);
                    return true;
                case "tile_pipe":
                    PipeH(0, 24, 64, 14, Bs);
                    foreach (int x in new[] { 14, 46 }) { Box(x, 21, 5, 20, Cu); RivetsV(x + 2, 23, 38, 4, Bs.L); }
                    C.Rect(30, 38, 4, 6, Fe.B);
                    C.Ring(32, 47, 5, 7, Cu.B); C.Line(26, 47, 38, 47, Cu.S); C.Line(32, 41, 32, 53, Cu.S); C.Disc(32, 47, 1.5f, Bs.L);
                    C.Rect(4, 8, 8, 6, Fe.S); C.Rect(5, 13, 6, 1, Fe.L);
                    return true;
                case "tile_gear":
                    Gear(24, 26, 18, 12, Bs);
                    Gear(47, 46, 13, 9, Cu);
                    return true;
                case "tile_gaslamp":
                    LampPost(32, 8, 32, false);
                    return true;
                case "tile_coal":
                    for (int k = 0; k < 40; k++)
                    {
                        float a = H(k, 1, 31) % 628 / 100f, d = H(k, 2, 31) % 100 / 100f * 18f;
                        float cx = 32 + Mathf.Cos(a) * d * 1.2f, cy = 28 + Mathf.Sin(a) * d * 0.8f, r = 3 + H(k, 3, 31) % 3;
                        C.Disc(cx, cy, r, Fe.S); C.Disc(cx - 1, cy + 1, r - 1.5f, k % 3 == 0 ? Fe.B : O.B); C.Set((int)cx - 1, (int)cy + 1, Fe.L);
                    }
                    foreach (var p in new[] { (24, 26), (38, 32), (30, 18), (44, 22), (20, 34) }) { C.Set(p.Item1, p.Item2, Em.B); C.Set(p.Item1 + 1, p.Item2, Em.L); C.Set(p.Item1, p.Item2 - 1, Em.S); }
                    C.Set(33, 25, Gl.L);
                    return true;
                case "tile_girder":
                    foreach (int y in new[] { 8, 46 }) { Box(3, y, 58, 10, Fe); C.Rect(3, y + 4, 58, 2, Fe.S); RivetsH(6, 58, y + 2, 5, Bs.S); RivetsH(6, 58, y + 7, 5, Bs.S); }
                    Box(27, 18, 10, 28, Fe); C.Rect(31, 18, 2, 28, Fe.S); RivetsV(29, 20, 44, 5, Bs.S);
                    C.Line(6, 19, 26, 44, Fe.B, 3); C.Line(58, 19, 38, 44, Fe.B, 3); C.Line(6, 20, 26, 45, Fe.L);
                    return true;
                case "tile_cobble":
                    C.Rect(4, 4, 56, 4, Wd.B); C.Rect(4, 56, 56, 4, Wd.B);
                    C.Rect(4, 4, 4, 56, Wd.B); C.Rect(56, 4, 4, 56, Wd.B);
                    for (int i = 0; i < 9; i++) C.Disc(10 + H(i, 5, 23) % 44, 10 + H(i, 6, 23) % 44, 1.5f, Sa.L);
                    return true;
                case "tile_tank":
                    Box(9, 6, 46, 52, Fe);
                    C.Rect(13, 28, 38, 26, Wa.B); C.Rect(13, 50, 38, 4, Wa.S);
                    for (int k = 0; k < 4; k++) C.Rect(16 + k * 9, 34 + (k % 2) * 7, 6, 1, Wa.L);
                    foreach (int y in new[] { 12, 24 }) { C.Rect(9, y, 46, 2, Cu.B); RivetsH(11, 53, y + 1, 3, Bs.L); }
                    RivetsV(11, 28, 56, 4, Bs.S); RivetsV(52, 28, 56, 4, Bs.S);
                    C.Rect(28, 2, 8, 5, Bs.B); C.Rect(30, 0, 4, 3, Fe.S);
                    return true;
                case "tile_glass":
                    C.Rect(2, 2, 60, 60, Wa.S);
                    for (int k = 0; k < 9; k++) Shrub(9 + H(k, 3, 35) % 46, 9 + H(k, 4, 35) % 46, 4 + H(k, 5, 35) % 3, k);
                    C.Rect(2, 2, 60, 60, A(Wa.L, 70));
                    foreach (int v in new[] { 2, 21, 41, 60 }) { C.Rect(2, v, 60, 2, Bs.B); C.Rect(v, 2, 2, 60, Bs.B); }
                    for (int k = 0; k < 8; k++) { C.Set(6 + k, 36 + k, Sm.L); C.Set(26 + k, 16 + k, Sm.L); C.Set(45 + k, 46 + k, Sm.L); }
                    return true;
                case "tile_bunk":
                    Box(10, 5, 44, 54, Fe);
                    C.Rect(13, 8, 38, 48, Sm.S);
                    Box(15, 44, 34, 10, Sm); C.Rect(17, 50, 30, 2, Sm.L);
                    C.Rect(13, 8, 38, 32, Fa.B); C.Rect(13, 37, 38, 3, Fa.L); C.Rect(13, 8, 38, 2, Fa.S);
                    for (int x = 15; x < 51; x += 6) C.Rect(x, 11, 1, 24, Fa.S);
                    foreach (int x in new[] { 10, 53 }) foreach (int y in new[] { 5, 58 }) C.Set(x, y, Bs.L);
                    return true;
                case "tile_bench":
                    for (int y = 12; y <= 44; y += 11)
                    {
                        Box(8, y, 48, 8, Wd);
                        C.Rect(11, y + 5, 42, 1, Wd.L);
                        C.Rect(14, y + 2, 35, 1, Wd.S);
                    }
                    C.Rect(18, 10, 4, 43, Bs.B); C.Rect(42, 10, 4, 43, Bs.B);
                    return true;
                case "tile_seedling":
                    Box(12, 9, 40, 46, Wd);
                    C.Rect(16, 13, 32, 37, Sa.B);
                    foreach (int x in new[] { 23, 32, 41 })
                    {
                        C.Ellipse(x, 31, 4, 7, Gl.B);
                        C.Ellipse(x - 1, 34, 2, 3, Gl.L);
                    }
                    return true;
                case "tile_wire":
                    C.Line(0, 40, 64, 40, O.B); C.Line(0, 30, 64, 30, O.B);
                    C.Rect(30, 6, 5, 50, Wd.B); C.Rect(30, 6, 1, 50, Wd.L); C.Rect(34, 6, 1, 50, Wd.S);
                    Box(14, 42, 36, 5, Wd);
                    foreach (int x in new[] { 17, 25, 39, 47 }) { C.Disc(x, 48, 1.6f, Bs.L); C.Set(x, 48, Sm.L); }
                    for (int x = 0; x < SIZE; x++) { C.Set(x, 48 - Mathf.Abs(x - 32) / 10, Fe.S); C.Set(x, 44 + (x - 32) * (x - 32) / 220, O.B); }
                    return true;
                case "tile_chimney":
                    Bricks(18, 6, 28, 34, Br);
                    C.Rect(16, 38, 32, 6, Fe.B); C.Rect(16, 43, 32, 1, Fe.L); C.Rect(16, 38, 32, 1, Fe.S);
                    C.Rect(20, 40, 24, 3, O.S); C.Rect(22, 40, 20, 1, Em.S);
                    C.Rect(18, 18, 28, 1, Bs.S);
                    return true;
                default:
                    return false;
            }
        }
    }
}
