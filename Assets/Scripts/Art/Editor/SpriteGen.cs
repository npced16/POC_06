using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 008·019. 탑뷰 스팀펑크 64픽셀 도트(선로 6, 거점 4+폐허 4, 열차·경유지·인물·시설·게이지 9, 내부 타일 4)와 contact_sheet.png를
    /// 외부 도구 없이 결정적으로 생성한다. 색은 Assets/Art/Palette/SteampunkPalette.json(ArtKit)에서 읽는다. 파일 이름은 그대로 두어 GUID 참조를 지킨다.
    /// 모든 거점의 굴뚝 머리는 같은 자리(픽셀 x 42~48, 윗줄 y 58)라서 HubBuilding 프리팹의 Smoke 자식 하나로 맞는다.
    /// </summary>
    public static class SpriteGen
    {
        private const string OUTPUT = "Assets/Art/Generated/";

        /// <summary>27개 PNG와 contact_sheet.png를 만들고 임포터(PPU 64, Point, 무압축)를 설정한다.</summary>
        [MenuItem("IronRing/Art/Generate Sprites")]
        public static void Generate()
        {
            Load();
            Directory.CreateDirectory(OUTPUT);
            var sprites = new List<Color32[]>();
            var names = new List<string>();
            string[] rails = { "rail_horizontal", "rail_vertical", "rail_curve_ne", "rail_curve_nw", "rail_curve_se", "rail_curve_sw" };
            for (int i = 0; i < rails.Length; i++) { Begin(); Rail(i); Save(rails[i], sprites, names); }
            for (int i = 0; i < 4; i++)
            {
                Begin(); Hub(i, false); Save("hub_" + (i + 1), sprites, names);
                Begin(); Hub(i, true); Save("hub_" + (i + 1) + "_ruined", sprites, names);
            }
            string[] icons = { "train", "waypoint", "raider", "wall", "turret", "workshop", "npc", "prosperity_bg", "prosperity_fill" };
            foreach (string icon in icons) { Begin(); Icon(icon); Save(icon, sprites, names); }
            string[] tiles = { "interior_floor", "interior_rubble", "interior_empty", "interior_plot" };
            foreach (string tile in tiles) { Begin(); InteriorTile(tile); Save(tile, sprites, names); }
            File.WriteAllBytes(OUTPUT + "contact_sheet.png", Sheet(sprites, 7));
            File.WriteAllText(OUTPUT + "manifest.json", "{\"size\":" + SIZE + ",\"sprites\":[" + string.Join(",", names.Select(n => "\"" + n + "\"")) + "]}");
            AssetDatabase.Refresh();
            foreach (string name in names.Concat(new[] { "contact_sheet" })) ConfigureImporter(OUTPUT + name + ".png");
            AssetDatabase.SaveAssets();
        }

        private static void Save(string name, List<Color32[]> sprites, List<string> names)
        {
            C.Save(OUTPUT + name + ".png");
            sprites.Add(C.Px); names.Add(name);
        }

        /// <summary>나무 침목 위 어두운 강철 레일과 황동 볼트. kind 0 가로, 1 세로, 2~5 곡선(ne, nw, se, sw).</summary>
        private static void Rail(int kind)
        {
            if (kind < 2)
            {
                Layer();
                for (int u = 1; u < SIZE; u += 8)
                {
                    Box(u, 12, 5, 40, Wd);
                    C.Rect(u + 1, 13, 1, 38, Wd.L);
                }
                EndLayer();
                Layer();
                foreach (int y in new[] { 19, 42 }) { C.Rect(0, y, SIZE, 3, Fe.B); C.Rect(0, y + 1, SIZE, 1, Fe.L); C.Rect(0, y, SIZE, 1, Fe.S); }
                EndLayer();
                for (int u = 1; u < SIZE; u += 8) foreach (int y in new[] { 16, 24, 39, 47 }) C.Set(u + 2, y, Bs.B);
                if (kind == 1) C.Transpose();
                return;
            }
            float cx = kind == 2 || kind == 4 ? SIZE : 0, cy = kind == 2 || kind == 3 ? SIZE : 0;
            Layer();
            for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy, r = Mathf.Sqrt(dx * dx + dy * dy);
                float s = Mathf.Atan2(Mathf.Abs(dy), Mathf.Abs(dx)) * 32f % 8f;
                if (r < 12 || r >= 52 || s >= 5f) continue;
                C.Set(x, y, s < 1f ? Wd.L : s >= 4f ? Wd.S : Wd.B);
            }
            EndLayer();
            Layer();
            for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy, r = Mathf.Sqrt(dx * dx + dy * dy);
                foreach (float center in new[] { 20.5f, 43.5f })
                {
                    float d = r - center;
                    if (Mathf.Abs(d) < 1.5f) C.Set(x, y, Mathf.Abs(d) < 0.5f ? Fe.L : d < 0 ? Fe.S : Fe.B);
                }
            }
            EndLayer();
            for (int k = 0; k < 7; k++)
            {
                float a = (k * 8 + 2.5f) / 32f;
                if (a > Mathf.PI / 2) break;
                foreach (float rr in new[] { 17f, 24f, 40f, 47f })
                {
                    float sx = Mathf.Abs(Mathf.Cos(a)) * rr, sy = Mathf.Abs(Mathf.Sin(a)) * rr;
                    C.Set(Mathf.FloorToInt(cx == 0 ? sx : cx - sx), Mathf.FloorToInt(cy == 0 ? sy : cy - sy), Bs.B);
                }
            }
        }

        /// <summary>돌 광장 받침(둥근 모서리, 외곽선).</summary>
        private static void Pad(int salt)
        {
            Layer();
            Cobble(2, 2, 60, 60, G, salt);
            for (int y = 2; y < 62; y++) for (int x = 2; x < 62; x++)
            {
                float qx = Mathf.Max(0, Mathf.Max(8 - x, x - 55)), qy = Mathf.Max(0, Mathf.Max(8 - y, y - 55));
                if (qx * qx + qy * qy > 36) C.Erase(x, y);
            }
            EndLayer();
        }

        /// <summary>kind 거점(0 시청, 1 주조소, 2 창고 부두, 3 역 요새). ruined이면 불 꺼지고 무너진 지붕과 잔해.</summary>
        private static void Hub(int kind, bool ruined)
        {
            bool lit = !ruined;
            Pad(10 + kind);
            if (kind == 2)
            {
                C.Rect(3, 3, 58, 6, Wa.B); C.Rect(3, 8, 58, 1, Mo.S);
                for (int x = 5; x < 60; x += 7) C.Rect(x + kind % 2, 5 + x % 3, 3, 1, Wa.L);
            }
            Layer();
            switch (kind)
            {
                case 0:
                    Roof(6, 26, 52, 28, R, false);
                    Bricks(8, 10, 48, 16, W);
                    foreach (int x in new[] { 11, 18, 42, 49 }) Window(x, 14, 4, 7, lit);
                    Bricks(24, 8, 16, 22, W2);
                    Roof(22, 30, 20, 6, Cu, false);
                    C.Disc(32, 23, 5.5f, Bs.S); C.Disc(32, 23, 4.5f, Bs.B); C.Disc(32, 23, 3.2f, lit ? Gl.B : Mo.S); C.Disc(31.5f, 23.5f, 1.5f, lit ? Gl.L : O.S);
                    Box(29, 8, 6, 9, Wd); C.Rect(32, 8, 1, 8, Wd.S); C.Set(33, 12, Bs.L);
                    Gear(18, 42, 7, 10, Bs);
                    Chimney(13, 44, 6, 13, Br);
                    break;
                case 1:
                    Roof(4, 24, 30, 26, R, true);
                    Bricks(6, 10, 26, 14, W2);
                    Window(9, 14, 4, 6, lit); Box(17, 10, 10, 10, Fe); C.Rect(18, 11, 8, 8, lit ? Em.S : O.S); RivetsH(18, 25, 18, 2, Bs.S);
                    CylV(34, 10, 24, 22, Cu); C.Rect(34, 14, 24, 1, Bs.S); C.Rect(34, 24, 24, 1, Bs.S);
                    Window(44, 16, 4, 6, lit);
                    Dome(46, 34, 12, Cu); C.Ring(46, 34, 8, 9, Bs.S); RivetRing(46, 34, 10.5f, 16, Bs.L);
                    PipeH(28, 28, 10, 4, Bs);
                    Gear(17, 37, 8, 10, Bs); Gear(27, 44, 4, 8, Cu);
                    break;
                case 2:
                    Roof(4, 24, 56, 28, R, true);
                    Bricks(6, 10, 52, 14, Br);
                    foreach (int x in new[] { 10, 17, 44, 51 }) Window(x, 15, 4, 5, lit);
                    Box(25, 10, 14, 12, Wd); C.Rect(32, 10, 1, 11, Wd.S); C.Line(26, 11, 31, 20, Wd.L); C.Line(33, 11, 38, 20, Wd.L);
                    C.Rect(12, 34, 12, 4, lit ? Mix(Gl.S, R.B, 0.4f) : R.S); C.Rect(12, 37, 12, 1, lit ? Gl.B : R.L);
                    C.Line(14, 44, 5, 60, Cu.B, 2); C.Line(13, 44, 4, 60, Cu.L); C.Line(5, 60, 9, 60, Fe.B);
                    break;
                default:
                    Roof(8, 22, 48, 32, R, false);
                    Bricks(10, 8, 44, 14, W2);
                    foreach (int x in new[] { 15, 21, 40, 46 }) Window(x, 12, 3, 5, lit);
                    Box(27, 8, 10, 10, Wd); C.Rect(32, 8, 1, 9, Wd.S); Window(29, 19, 6, 1, lit);
                    Dome(32, 38, 10, Cu);
                    foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) C.Line(32 + d.Item1 * 3, 38 + d.Item2 * 3, 32 + d.Item1 * 9, 38 + d.Item2 * 9, Bs.S);
                    C.Disc(32, 38, 2.5f, lit ? Gl.B : O.S); C.Set(31, 38, lit ? Gl.L : Mo.S);
                    foreach (var p in new[] { (11, 22), (53, 22), (11, 52) }) { Dome(p.Item1, p.Item2, 6, R); C.Set(p.Item1 - 1, p.Item2, Bs.L); }
                    break;
            }
            Chimney(42, 45, 7, 14, kind == 2 ? Br : kind == 0 ? Cu : Fe);
            if (ruined) Ruin(40 + kind);
            EndLayer();
            if (!ruined) return;
            Layer(); Rubble(6, 4, 52, 8, 9, 50 + kind); EndLayer();
        }

        /// <summary>name 열차·경유지·인물·시설·게이지를 그린다. 열차 앞(굴뚝)은 위쪽.</summary>
        private static void Icon(string name)
        {
            switch (name)
            {
                case "train":
                    Layer();
                    for (int y = 8; y < 56; y += 11) { Box(14, y, 4, 7, Fe); Box(46, y, 4, 7, Fe); }
                    Box(17, 5, 30, 54, Fe);
                    for (int y = 17; y < 58; y += 12) C.Rect(18, y, 28, 1, Fe.S);
                    RivetsV(19, 7, 56, 3, Bs.S); RivetsV(44, 7, 56, 3, Bs.S);
                    CylV(21, 26, 22, 28, Cu);
                    foreach (int y in new[] { 31, 41, 51 }) { C.Rect(21, y, 22, 1, Bs.B); C.Rect(21, y - 1, 22, 1, Bs.S); }
                    Box(19, 6, 26, 18, W);
                    Roof(20, 9, 24, 14, R, false);
                    Window(16, 14, 2, 4, true); Window(46, 14, 2, 4, true);
                    Box(17, 58, 30, 3, Bs);
                    C.Disc(32, 53, 4.5f, Fe.S); C.Ring(32, 53, 3, 4.5f, Fe.L); C.Disc(32, 53, 2.5f, O.S);
                    C.Disc(25, 46, 2, Bs.B); C.Set(25, 46, Sm.L);
                    EndLayer();
                    break;
                case "waypoint":
                    Layer();
                    C.Rect(30, 4, 4, 18, Fe.B); C.Rect(30, 4, 1, 18, Fe.L); C.Rect(26, 3, 12, 3, Fe.S);
                    Gear(32, 36, 20, 12, Bs);
                    C.Disc(32, 36, 7, Fe.S); C.Disc(32, 36, 5.5f, Em.S); C.Disc(32, 36, 4, Gl.B); C.Disc(31, 37, 2, Gl.L);
                    EndLayer();
                    Glow(32, 36, 11);
                    break;
                case "npc":
                case "raider":
                    bool npc = name == "npc";
                    C.Ellipse(32, 14, 14, 5, A(O.S, 110));
                    Layer();
                    Swatch coat = npc ? Wa : Fe;
                    C.Ellipse(32, 26, 14, 13, coat.S); C.Ellipse(31, 27, 12, 11, coat.B); C.Ellipse(27, 30, 5, 5, coat.L);
                    C.Rect(31, 15, 2, 20, coat.S);
                    if (npc) { C.Rect(20, 30, 24, 3, Bs.S); C.Set(32, 22, Bs.L); C.Set(32, 26, Bs.L); }
                    else { C.Ellipse(32, 36, 10, 3, Fa.B); C.Rect(24, 35, 16, 1, Fa.S); }
                    C.Disc(32, 44, 9, Sa.B); C.Disc(30, 46, 5, Sa.L);
                    if (npc) { C.Disc(32, 47, 10, Wd.S); C.Disc(32, 48, 7, Wd.B); C.Ring(32, 48, 5.5f, 7, Bs.S); C.Disc(30, 50, 3, Wd.L); }
                    else
                    {
                        C.Disc(32, 47, 9, Fe.S); C.Disc(31, 48, 7, Fe.B);
                        C.Rect(23, 41, 18, 3, O.S); C.Disc(27, 42, 2.5f, Cu.B); C.Disc(37, 42, 2.5f, Cu.B); C.Set(26, 43, Em.L); C.Set(36, 43, Em.L);
                    }
                    EndLayer();
                    break;
                case "wall":
                    Layer();
                    Bricks(4, 16, 56, 26, W2);
                    for (int x = 4; x < 60; x += 10) { Box(x, 42, 6, 8, W2); C.Rect(x + 1, 48, 4, 1, Bs.S); }
                    C.Rect(4, 26, 56, 4, Fe.B); C.Rect(4, 29, 56, 1, Fe.L); C.Rect(4, 26, 56, 1, Fe.S); RivetsH(6, 58, 28, 4, Bs.L);
                    EndLayer();
                    break;
                case "turret":
                    Layer();
                    Dome(32, 26, 22, Fe); RivetRing(32, 26, 19.5f, 24, Bs.S);
                    Dome(32, 28, 13, Cu); C.Ring(32, 28, 9, 10, Bs.S);
                    C.Rect(29, 34, 6, 26, Fe.B); C.Rect(30, 34, 2, 26, Fe.L); C.Rect(34, 34, 1, 26, Fe.S);
                    Box(28, 56, 8, 4, Bs); C.Rect(30, 59, 4, 1, O.S);
                    C.Disc(32, 28, 3, Em.B); C.Set(31, 29, Gl.L);
                    EndLayer();
                    break;
                case "workshop":
                    Layer();
                    Roof(6, 24, 52, 30, R, true);
                    Bricks(8, 8, 48, 16, Br);
                    Window(12, 12, 5, 7, true); Window(47, 12, 5, 7, true);
                    Box(26, 8, 12, 12, Wd); C.Rect(27, 9, 10, 10, Em.S); C.Rect(28, 10, 8, 3, Gl.S);
                    Gear(22, 40, 9, 10, Bs); Gear(33, 47, 5, 8, Cu);
                    Chimney(44, 38, 7, 18, Cu);
                    EndLayer();
                    break;
                case "prosperity_bg":
                    C.Rect(2, 22, 60, 20, O.B); C.Rect(3, 23, 58, 18, Bs.S); C.Rect(4, 24, 56, 16, Bs.B); C.Rect(4, 39, 56, 1, Bs.L);
                    C.Rect(6, 26, 52, 12, Mo.S); C.Set(4, 24, Bs.L); C.Set(59, 24, Bs.L); C.Set(4, 39, O.B); C.Set(59, 39, O.B);
                    break;
                case "prosperity_fill":
                    C.Rect(6, 26, 52, 12, Gl.B); C.Rect(6, 34, 52, 3, Gl.L); C.Rect(6, 26, 52, 2, Gl.S); C.Rect(6, 37, 52, 1, Gl.S);
                    break;
            }
        }

        /// <summary>거점 내부 칸: 돌바닥(줄눈·이끼·먼지), 잔해, 리벳 슬레이트 판(빈 터), 옛 시설 부지.</summary>
        private static void InteriorTile(string name)
        {
            Cobble(0, 0, SIZE, SIZE, G, 3);
            C.Rect(0, 0, SIZE, 1, Mo.S); C.Rect(0, 0, 1, SIZE, Mo.S);
            switch (name)
            {
                case "interior_rubble":
                    Layer();
                    Rubble(4, 4, 50, 50, 14, 21);
                    Box(10, 30, 34, 4, Wd); C.Line(14, 18, 44, 44, Wd.S, 2);
                    C.Rect(40, 12, 6, 3, Cu.B); C.Set(41, 14, Cu.L);
                    EndLayer();
                    break;
                case "interior_empty":
                    Layer();
                    Box(4, 4, 56, 56, R);
                    for (int y = 4; y < 60; y += 13) C.Rect(5, y, 54, 1, R.S);
                    C.Rect(32, 5, 1, 54, R.S); C.Rect(33, 5, 1, 54, R.L);
                    RivetsH(7, 57, 7, 6, Bs.S); RivetsH(7, 57, 56, 6, Bs.S); RivetsV(7, 13, 50, 6, Bs.S); RivetsV(56, 13, 50, 6, Bs.S);
                    C.Disc(32, 32, 4, Bs.S); C.Disc(32, 32, 3, Bs.B); C.Disc(31.5f, 32.5f, 1.2f, Bs.L);
                    EndLayer();
                    break;
                case "interior_plot":
                    Layer();
                    C.Rect(8, 8, 48, 48, Wd.B); C.Rect(12, 12, 40, 40, Mo.S);
                    for (int x = 12; x < 52; x += 2) C.Set(x, 52 - (x % 4), Wd.L);
                    foreach (int x in new[] { 5, 49 }) foreach (int y in new[] { 5, 49 }) Box(x, y, 10, 10, Bs);
                    C.Rect(26, 31, 12, 3, Bs.B); C.Rect(30, 26, 3, 13, Bs.B); C.Rect(26, 33, 12, 1, Bs.L);
                    EndLayer();
                    break;
            }
        }
    }
}
