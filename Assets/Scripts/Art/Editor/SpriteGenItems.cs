using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 013·019. 옛 아이템(카탈로그에서 타일이 아닌 것)의 64픽셀 탑뷰 스팀펑크 도트를 투명 바탕에 그린다(색은 SteampunkPalette.json).
    /// 출력: Assets/Art/Generated/items/*.png, items_contact_sheet.png. ItemSprites.asset은 SpriteGenTiles가 카탈로그 전체로 다시 채운다
    /// (예전에는 타일 id까지 그리려다 실패했다: 이제 타일은 건너뛴다).
    /// </summary>
    public static class SpriteGenItems
    {
        private const string OUTPUT = "Assets/Art/Generated/items/";
        private const string CATALOG = "Assets/Scripts/Items/Resources/Items/ItemCatalog.json";

        /// <summary>타일이 아닌 카탈로그 아이템 PNG를 만들고 임포터를 설정한다. 그림이 없는 id는 경고 후 건너뛴다.</summary>
        [MenuItem("IronRing/Art/Generate Item Sprites")]
        public static void Generate()
        {
            Load();
            List<string> ids = IronRing.Items.ItemCatalog.Parse(File.ReadAllText(CATALOG)).Items.Where(i => !i.IsTile).Select(i => i.Id).ToList();
            Directory.CreateDirectory(OUTPUT);
            var sprites = new List<Color32[]>();
            var drawn = new List<string>();
            foreach (string id in ids)
            {
                Begin();
                C.Ellipse(32, 12, 20, 5, A(O.S, 100));
                Layer();
                bool known = Draw(id);
                EndLayer();
                if (!known) { Debug.LogWarning("SpriteGenItems: no drawing for item " + id + ", skipped."); continue; }
                if (id == "lantern") Glow(32, 34, 20);
                if (id == "brazier") { Glow(32, 32, 22); Puff(36, 50, 5, 150); }
                if (id == "steam_pipe") { Puff(46, 50, 6, 190); Puff(51, 57, 4, 140); }
                C.Save(OUTPUT + id + ".png");
                sprites.Add(C.Px); drawn.Add(id);
            }
            File.WriteAllBytes(OUTPUT + "items_contact_sheet.png", Sheet(sprites, 6));
            AssetDatabase.Refresh();
            foreach (string id in drawn) ConfigureImporter(OUTPUT + id + ".png");
            ConfigureImporter(OUTPUT + "items_contact_sheet.png");
            AssetDatabase.SaveAssets();
            Debug.Log("SpriteGenItems: " + drawn.Count + " item sprites generated.");
        }

        /// <summary>id 아이템을 현재 레이어에 그린다. 모르는 id면 false.</summary>
        private static bool Draw(string id)
        {
            switch (id)
            {
                case "lantern":
                    C.Rect(30, 48, 4, 6, Fe.B); C.Ring(32, 55, 2, 4, Fe.S);
                    Box(22, 16, 20, 32, Bs); C.Rect(25, 19, 14, 26, Gl.B); C.Rect(27, 30, 10, 12, Gl.L); C.Rect(31, 19, 2, 26, Bs.S);
                    C.Rect(20, 46, 24, 3, Fe.B); C.Rect(20, 14, 24, 3, Fe.S);
                    return true;
                case "brazier":
                    Dome(32, 28, 20, Fe); RivetRing(32, 28, 18, 18, Bs.S);
                    C.Disc(32, 29, 14, O.S);
                    for (int k = 0; k < 14; k++) C.Disc(22 + H(k, 1, 5) % 20, 20 + H(k, 2, 5) % 18, 2 + H(k, 3, 5) % 2, k % 3 == 0 ? Em.L : k % 3 == 1 ? Em.B : Em.S);
                    C.Disc(32, 30, 3, Gl.L);
                    return true;
                case "water_barrel":
                    Barrel(32, 30, 22, true);
                    return true;
                case "well":
                    C.Disc(32, 30, 22, G.S); C.Disc(32, 30, 20, G.B);
                    for (int k = 0; k < 12; k++) { float a = k * Mathf.PI / 6; C.Line(32 + (int)(Mathf.Cos(a) * 14), 30 + (int)(Mathf.Sin(a) * 14), 32 + (int)(Mathf.Cos(a) * 20), 30 + (int)(Mathf.Sin(a) * 20), Mo.B); }
                    C.Disc(32, 30, 13, Mo.S); C.Disc(32, 30, 11, Wa.S); C.Disc(30, 32, 6, Wa.B); C.Set(28, 34, Wa.L);
                    Box(6, 27, 52, 6, Wd); C.Rect(30, 26, 4, 8, Fe.B); C.Disc(32, 30, 2, Bs.B);
                    return true;
                case "seedling":
                    C.Ellipse(32, 22, 18, 10, Wd.S); C.Ellipse(31, 23, 15, 8, Wd.B);
                    C.Rect(31, 22, 2, 18, Sh.S);
                    C.Ellipse(24, 38, 8, 4, Sh.B); C.Ellipse(40, 42, 8, 4, Sh.B); C.Ellipse(23, 39, 5, 2, Sh.L); C.Ellipse(39, 43, 5, 2, Sh.L);
                    return true;
                case "vegetable_patch":
                    Box(4, 6, 56, 50, Wd); C.Rect(7, 9, 50, 44, Wd.S);
                    for (int row = 0; row < 3; row++)
                    {
                        int y = 15 + row * 14;
                        C.Rect(8, y - 3, 48, 2, Mix(Wd.S, O.B, 0.4f));
                        for (int x = 13; x < 56; x += 10) { Shrub(x, y + 2, 4, row * 10 + x); C.Disc(x + 2, y, 1.6f, row == 1 ? Fa.L : Em.B); }
                    }
                    return true;
                case "bench":
                    foreach (int x in new[] { 10, 50 }) { C.Rect(x, 14, 4, 34, Fe.S); C.Rect(x, 14, 1, 34, Fe.B); }
                    for (int k = 0; k < 4; k++) { Box(4, 18 + k * 8, 56, 6, Wd); C.Rect(5, 22 + k * 8, 54, 1, Wd.L); }
                    RivetsH(12, 52, 21, 40, Bs.L);
                    return true;
                case "tent":
                    for (int y = 8; y < 54; y++)
                    {
                        int half = 4 + (54 - y) * 22 / 46;
                        C.Rect(32 - half, y, half, 1, Fa.L); C.Rect(32, y, half, 1, Fa.S);
                    }
                    C.Rect(31, 8, 2, 48, Wd.B); C.Rect(28, 8, 8, 8, O.S); C.Rect(29, 8, 6, 6, Mix(Gl.S, O.B, 0.4f));
                    for (int y = 14; y < 50; y += 9) { C.Set(32 - 4 - (54 - y) * 22 / 46 + 2, y, Fa.B); C.Set(32 + 2 + (54 - y) * 22 / 46, y, Fa.B); }
                    return true;
                case "storage_crate":
                    Crate(10, 10, 44, 42);
                    return true;
                case "gear":
                    Gear(32, 32, 24, 12, Bs);
                    return true;
                case "flag":
                    C.Rect(14, 6, 4, 52, Fe.B); C.Rect(14, 6, 1, 52, Fe.L); C.Disc(16, 58, 2.5f, Bs.L);
                    for (int y = 30; y < 56; y++) { int w = 30 - Mathf.Abs(y - 43) / 3; C.Rect(18, y, w, 1, y > 50 ? Fa.L : y < 34 ? Fa.S : Fa.B); }
                    C.Rect(44, 38, 4, 10, O.S);
                    Gear(31, 43, 6, 8, Bs);
                    return true;
                case "steam_pipe":
                    PipeH(4, 18, 30, 12, Cu);
                    CylV(32, 18, 12, 30, Cu); Box(30, 44, 16, 4, Bs); Box(30, 16, 16, 4, Bs);
                    C.Rect(36, 48, 4, 4, Fe.B); RivetsH(8, 30, 23, 4, Bs.L);
                    return true;
                default:
                    return false;
            }
        }
    }
}
