using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 033. 습격(점령된 종점 통과) 스프라이트를 팔레트 색으로 그린다: 증기 거미 2프레임(48), 녹슨 자동인형 2프레임(64), 몬스터 둥지와 무너진 둥지(96),
    /// 포탄(16), 명중 번쩍임(32), 점령된 종점 배경(128). 출력은 Assets/Art/Generated/raid/(PPU 64, Point, 무압축)이며 raid_contact_sheet.png를
    /// 스토리 증거 폴더에 복사하고, RaidCatalog.asset이 있으면 필드에 스프라이트를 넣는다.
    /// </summary>
    public static class SpriteGenRaid
    {
        public const string OUTPUT = "Assets/Art/Generated/raid/";
        public const string CATALOG = "Assets/Prefabs/Raid/RaidCatalog.asset";
        public const string EVIDENCE = "production/qa/evidence/story-033-raid-occupied-outpost/";

        private static readonly List<(string name, Color32[] px, int size)> Made = new List<(string, Color32[], int)>();

        /// <summary>습격 스프라이트 10장과 raid_contact_sheet.png를 만들고 임포터를 설정한 뒤 카탈로그를 채운다.</summary>
        [MenuItem("IronRing/Art/Generate Raid Sprites")]
        public static void Generate()
        {
            Load();
            Directory.CreateDirectory(OUTPUT);
            Made.Clear();
            for (int f = 0; f < 2; f++) { New(48); Spider(f); Save("raid_spider_" + f); }
            for (int f = 0; f < 2; f++) { New(64); Automaton(f); Save("raid_automaton_" + f); }
            New(96); Nest(false); Save("raid_nest");
            New(96); Nest(true); Save("raid_nest_broken");
            New(16); Shell(); Save("raid_shell");
            New(32); Hit(); Save("raid_hit");
            New(128); Terminal(); Save("terminal_occupied");

            File.WriteAllBytes(OUTPUT + "raid_contact_sheet.png", ContactSheet());
            Directory.CreateDirectory(EVIDENCE);
            File.Copy(OUTPUT + "raid_contact_sheet.png", EVIDENCE + "raid_contact_sheet.png", true);
            AssetDatabase.Refresh();
            foreach (var m in Made) ConfigureImporter(OUTPUT + m.name + ".png");
            ConfigureImporter(OUTPUT + "raid_contact_sheet.png");
            AssetDatabase.SaveAssets();
            Debug.Log("[SpriteGenRaid] " + AssignCatalog());
        }

        /// <summary>RaidCatalog.asset이 있으면 필드 이름으로 스프라이트를 넣고 결과 문자열을 돌려준다. 없으면 아무것도 바꾸지 않는다.</summary>
        public static string AssignCatalog()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CATALOG);
            if (asset == null) return "RaidCatalog not found at " + CATALOG + " (skipped)";
            var so = new SerializedObject(asset);
            var missing = new List<string>();
            SetArray(so, "_spiderFrames", new[] { "raid_spider_0", "raid_spider_1" }, missing);
            SetArray(so, "_automatonFrames", new[] { "raid_automaton_0", "raid_automaton_1" }, missing);
            SetOne(so, "_nest", "raid_nest", missing);
            SetOne(so, "_nestBroken", "raid_nest_broken", missing);
            SetOne(so, "_shell", "raid_shell", missing);
            SetOne(so, "_hit", "raid_hit", missing);
            SetOne(so, "_terminalOccupied", "terminal_occupied", missing);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return missing.Count == 0 ? "RaidCatalog assigned (9 sprites)" : "RaidCatalog assigned with missing: " + string.Join(", ", missing);
        }

        private static Sprite Sp(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(OUTPUT + name + ".png");

        private static void SetOne(SerializedObject so, string field, string sprite, List<string> missing)
        {
            SerializedProperty p = so.FindProperty(field);
            Sprite s = Sp(sprite);
            if (p == null || s == null) { missing.Add(field); return; }
            p.objectReferenceValue = s;
        }

        private static void SetArray(SerializedObject so, string field, string[] sprites, List<string> missing)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || !p.isArray) { missing.Add(field); return; }
            p.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite s = Sp(sprites[i]);
                if (s == null) missing.Add(field + "[" + i + "]");
                p.GetArrayElementAtIndex(i).objectReferenceValue = s;
            }
        }

        private static void New(int size) { C = new PixelCanvas(size); }

        private static void Save(string name)
        {
            C.Save(OUTPUT + name + ".png");
            Made.Add((name, C.Px, C.Size));
        }

        // ---------- 공용 ----------

        /// <summary>왼쪽 위에서 빛을 받는 타원 덩어리.</summary>
        private static void ShadedEllipse(float cx, float cy, float rx, float ry, Swatch s)
        {
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f) continue;
                    float l = -dx * 0.6f + dy * 0.8f;
                    C.Set(x, y, d > 0.88f && l < 0.2f ? s.S : l > 0.4f ? s.L : l < -0.4f ? s.S : s.B);
                }
        }

        /// <summary>현재 레이어의 불투명 픽셀 중 rect 안에 녹 점을 찍는다.</summary>
        private static void Rust(int x0, int y0, int w, int h, int salt, int density)
        {
            for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++)
            {
                if (C.Get(x, y).a < 200) continue;
                int k = H(x, y, salt);
                if (k % density == 0) C.Set(x, y, k % 3 == 0 ? Em.S : Cu.S);
                else if (k % (density * 4) == 1) C.Set(x, y, A(Ms.S, 200));
            }
        }

        /// <summary>현재 버퍼 전체를 t만큼 어둡게 한다.</summary>
        private static void Darken(float t)
        {
            for (int i = 0; i < C.Px.Length; i++)
            {
                Color32 p = C.Px[i];
                if (p.a == 0) continue;
                Color32 m = Mix(p, O.S, t);
                C.Px[i] = new Color32(m.r, m.g, m.b, p.a);
            }
        }

        // ---------- 증기 거미 (48) ----------

        private static void Spider(int f)
        {
            int by = 21 + f;
            C.Ellipse(24, 9, 17, 4, A(O.S, 110));
            int[] fx = { 15, 20, 20, 16 }, fy = { 31, 24, 15, 6 };
            Layer();
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    bool raised = ((i + (side > 0 ? 1 : 0)) % 2 == 0) == (f == 0);
                    int hx = 24 + side * 6, hy = by + 4 - i * 3;
                    int footX = 24 + side * fx[i] - (raised ? side : 0), footY = fy[i] + (raised ? 3 : 0);
                    int kx = hx + (int)((footX - hx) * 0.65f) + side, ky = Mathf.Max(hy, footY) + 6;
                    Swatch s = i == 0 ? new Swatch(Fe.B, Fe.S, O.L) : Fe;
                    C.Line(hx, hy, kx, ky, s.B, 2);
                    C.Line(hx, hy + 1, kx, ky + 1, s.L);
                    C.Line(kx, ky, footX, footY, s.S, 2);
                    C.Line(kx, ky, footX, footY + 1, s.B);
                    C.Rect(kx - 1, ky - 1, 2, 2, Bs.S); C.Set(kx - 1, ky, Bs.L);
                    C.Set(footX, footY, Bs.B);
                }
            EndLayer();
            Layer();
            Chimney(26, by + 6, 5, 9, Fe);
            Dome(24, by, 10, Cu);
            for (int x = 13; x <= 35; x++)
                for (int y = by - 1; y <= by; y++)
                {
                    float dx = x + 0.5f - 24, dy = y + 0.5f - by;
                    if (dx * dx + dy * dy <= 100) C.Set(x, y, y == by ? Bs.B : Bs.S);
                }
            RivetsH(16, 32, by, 3, Bs.L);
            Rust(14, by - 10, 21, 21, 51, 9);
            C.Disc(16.5f, by + 4.5f, 2.2f, Bs.L); C.Set(16, by + 4, O.B); C.Set(17, by + 5, Em.B);
            C.Rect(18, by - 8, 13, 5, O.S);
            C.Rect(18, by - 4, 13, 1, Fe.L);
            foreach (int ex in new[] { 19, 23, 27 }) { C.Rect(ex, by - 7, 2, 2, Gl.B); C.Set(ex, by - 6, Gl.L); }
            C.Line(21, by - 9, 20, by - 13, Bs.B); C.Set(20, by - 13, Bs.L);
            C.Line(27, by - 9, 28, by - 13, Bs.B); C.Set(28, by - 13, Bs.L);
            EndLayer();
            C.Disc(24.5f, by - 5.5f, 7, A(Gl.B, 40));
            Puff(29 + f, by + 18 + f * 2, 3 + f, 210);
            if (f == 1) Puff(31, by + 24, 2, 140);
        }

        // ---------- 녹슨 자동인형 (64) ----------

        private static void Automaton(int f)
        {
            bool leftFront = f == 0;
            C.Ellipse(32, 6, 20, 4, A(O.S, 110));
            Layer(); Arm(leftFront ? 50 : 8, 3, false); EndLayer();
            Layer();
            Box(22, 8, 8, 11, Fe); Box(34, 8, 8, 11, Fe);
            C.Set(25, 14, Bs.L); C.Set(37, 14, Bs.L);
            Box(19, 4, 12, 5, Fe); Box(33, 4, 12, 5, Fe);
            C.Rect(20, 4, 10, 1, O.S); C.Rect(34, 4, 10, 1, O.S);
            Chimney(18, 42, 5, 12, Fe);
            CylV(15, 18, 34, 24, Cu);
            C.Rect(15, 41, 34, 1, Cu.L);
            C.Rect(15, 20, 34, 2, Fe.S); C.Rect(15, 37, 34, 2, Fe.B); C.Rect(15, 38, 34, 1, Fe.L);
            RivetsH(16, 48, 21, 3, Bs.L); RivetsH(17, 47, 38, 3, Bs.L);
            RivetsV(16, 24, 35, 4, Bs.S); RivetsV(47, 24, 35, 4, Bs.S);
            Rust(15, 18, 34, 24, 77, 7);
            for (int k = 0; k < 5; k++) { int x = 18 + H(k, 1, 9) % 28; C.Rect(x, 34 - H(k, 2, 9) % 6, 1, 3 + H(k, 3, 9) % 3, Em.S); }
            Box(24, 23, 16, 11, Fe);
            C.Rect(26, 25, 12, 7, Em.B); C.Rect(26, 29, 12, 3, Gl.B); C.Rect(27, 31, 10, 1, Gl.L);
            for (int x = 28; x <= 36; x += 3) C.Rect(x, 25, 1, 7, O.S);
            Dome(17, 41, 6, Bs); Dome(47, 41, 6, Bs);
            C.Set(15, 43, Bs.L); C.Set(45, 43, Bs.L);
            Box(24, 42, 16, 11, Fe);
            C.Rect(25, 52, 14, 1, Fe.L); C.Rect(24, 42, 16, 1, O.S);
            C.Disc(32, 47.5f, 4.6f, Bs.S);
            C.Disc(32, 47.5f, 3.6f, Gl.S);
            C.Disc(32, 47.5f, 2.6f, Gl.B);
            C.Disc(31, 48.5f, 1.2f, Gl.L);
            C.Line(30, 51, 32, 48, O.B); C.Line(32, 48, 31, 45, O.B); C.Line(32, 48, 35, 47, O.B);
            C.Rect(26, 44, 2, 1, O.S); C.Rect(37, 44, 2, 1, O.S);
            EndLayer();
            Layer(); Arm(leftFront ? 8 : 50, -3, true); EndLayer();
            Glow(32, 47.5f, 8);
            C.Disc(32, 28, 9, A(Em.L, 30));
            Puff(20 + f, 57 + f * 2, 3 + f, 210);
            if (f == 0) Puff(22, 62, 2, 120);
        }

        /// <summary>어깨 아래로 늘어진 팔과 집게. dy는 위아래 흔들림(음수 = 앞으로 내림), front가 아니면 한 단 어둡다.</summary>
        private static void Arm(int x, int dy, bool front)
        {
            Swatch s = front ? Fe : new Swatch(Fe.B, Fe.S, O.L);
            Swatch c = front ? Cu : new Swatch(Cu.B, Cu.S, O.L);
            CylV(x, 24 + dy, 6, 17, s);
            C.Rect(x, 32 + dy, 6, 1, Bs.S);
            Box(x - 1, 18 + dy, 8, 7, c);
            C.Rect(x - 1, 14 + dy, 2, 4, Bs.B); C.Rect(x + 5, 14 + dy, 2, 4, Bs.B);
            C.Set(x - 1, 14 + dy, Bs.L); C.Set(x + 6, 14 + dy, Bs.L);
        }

        // ---------- 둥지 (96) ----------

        private static void Nest(bool broken)
        {
            int cy = broken ? 32 : 44, ry = broken ? 20 : 30;
            C.Ellipse(48, 15, 44, 10, A(O.S, 120));
            if (!broken) C.Ellipse(48, 15, 34, 7, A(Em.S, 50));
            Layer();
            CylV(6, 14, 6, 30, Cu); CylH(6, 40, 18, 6, Cu); Box(5, 14, 8, 3, Bs); Box(20, 39, 3, 8, Bs);
            if (broken) { CylV(84, 14, 6, 10, Fe); CylH(70, 6, 16, 5, Fe); }
            else { CylV(84, 14, 6, 24, Fe); CylH(72, 34, 18, 6, Fe); Box(83, 14, 8, 3, Bs); Box(73, 33, 3, 8, Bs); }
            Rust(4, 6, 90, 44, 21, 8);
            EndLayer();
            Layer();
            ShadedEllipse(48, cy, 30, ry, Fe);
            foreach (float k in new[] { -0.55f, -0.1f, 0.35f, 0.72f })
            {
                int y0 = Mathf.RoundToInt(cy + k * ry);
                for (int y = y0; y < y0 + 3; y++) for (int x = 18; x < 79; x++)
                {
                    float dx = (x + 0.5f - 48) / 30f, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy > 0.95f) continue;
                    C.Set(x, y, y == y0 + 2 ? Cu.L : y == y0 ? Cu.S : Cu.B);
                    if (y == y0 + 1 && (x % 4) == 0) C.Set(x, y, Bs.L);
                }
            }
            Rust(18, cy - ry, 61, 2 * ry, 33, 6);
            int mawY = cy - ry + 9;
            C.Ellipse(48, mawY, 11, 7, O.S);
            C.Ellipse(48, mawY - 1, 8, 4, broken ? Mo.S : Em.S);
            C.Ellipse(48, mawY - 2, 6, 3, broken ? O.S : Em.B);
            for (int x = 39; x <= 56; x += 3) { C.Line(x, mawY + 6, x + 1, mawY + 3, Bs.B); C.Set(x + 1, mawY + 3, Bs.L); }
            if (!broken)
            {
                C.Rect(43, mawY - 1, 2, 1, Gl.L); C.Rect(47, mawY - 1, 2, 1, Gl.L);
                C.Set(51, mawY - 3, Gl.L); C.Set(53, mawY - 3, Gl.L);
            }
            var ports = new List<(float x, float y)> { (32, cy + 6), (64, cy + 3) };
            if (!broken) ports.Add((48, cy + 17));
            foreach (var p in ports)
            {
                C.Disc(p.x, p.y, 4.6f, Bs.S); C.Ring(p.x, p.y, 3.4f, 4.6f, Bs.B);
                C.Disc(p.x, p.y, 3.5f, broken ? O.S : Gl.B);
                if (broken) C.Line((int)p.x - 2, (int)p.y + 2, (int)p.x + 1, (int)p.y - 2, Mo.L);
                else { C.Disc(p.x - 1, p.y + 1, 1.5f, Gl.L); C.Rect((int)p.x - 3, (int)p.y - 3, 7, 1, Gl.S); }
            }
            Crack(new[] { (50, cy + ry - 1), (46, cy + ry - 8), (49, cy + ry - 13), (44, cy + ry - 20) }, broken);
            Crack(new[] { (72, cy + ry / 2), (69, cy + 2), (73, cy - 4), (70, cy - 10) }, broken);
            Crack(new[] { (22, cy - 2), (27, cy - 8), (25, cy - 14) }, broken);
            if (broken)
            {
                int top = cy + ry;
                C.Ellipse(48, top - 4, 22, 5, O.S);
                C.Ellipse(48, top - 5, 18, 3, Mo.S);
                for (int x = 0; x < 96; x++)
                {
                    int cut = top - 3 - H(x / 3, 0, 5) % 6;
                    for (int y = cut; y <= top + 2; y++) if (y > cut + 1 || H(x, y, 6) % 3 == 0) C.Erase(x, y);
                }
            }
            else Chimney(43, cy + ry - 4, 10, 16, Fe);
            EndLayer();
            Layer();
            Gear(14, 12, 7, 8, Bs); Gear(80, 10, 6, 7, Cu); Gear(30, 6, 4, 6, Fe); Gear(66, 5, 3.5f, 6, Bs);
            for (int k = 0; k < 3; k++) { C.Line(56 + k * 4, 4, 54 + k * 4, 12 - k, Fe.L, 2); C.Set(54 + k * 4, 12 - k, Sm.S); }
            C.Line(20, 22, 12, 28, Fe.B, 2);
            if (broken)
            {
                CylH(58, 16, 22, 8, Fe); C.Rect(58, 16, 2, 8, Fe.S); C.Ellipse(80, 20, 2, 4, O.S);
                Rubble(14, 2, 70, 14, 16, 41);
            }
            EndLayer();
            if (broken) { Darken(0.35f); return; }
            foreach (var p in ports) Glow(p.x, p.y, 8);
            C.Disc(48, mawY - 1, 12, A(Em.B, 45));
            Puff(48, cy + ry + 15, 4, 220);
            Puff(52, cy + ry + 19, 3, 150);
            Puff(73, cy + 2, 3, 170);
        }

        private static void Crack((int x, int y)[] pts, bool broken)
        {
            for (int i = 0; i + 1 < pts.Length; i++) C.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, O.S, 2);
            for (int i = 0; i + 1 < pts.Length; i++) C.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, broken ? O.B : Gl.B);
        }

        // ---------- 포탄 (16) · 명중 (32) ----------

        private static void Shell()
        {
            for (int x = 0; x < 8; x++)
            {
                byte a = (byte)(40 + x * 25);
                C.Rect(x, 7, 1, 2, A(x < 4 ? Sm.S : Gl.S, a));
                if (x > 3) C.Set(x, x % 2 == 0 ? 6 : 9, A(Sm.B, (byte)(a / 2)));
            }
            Layer();
            CylH(8, 6, 5, 4, Bs);
            C.Rect(13, 6, 1, 4, Cu.B); C.Rect(14, 7, 1, 2, Cu.L);
            C.Rect(9, 6, 1, 4, Cu.S);
            EndLayer();
        }

        private static void Hit()
        {
            C.Disc(16, 16, 12, A(Gl.B, 50));
            C.Disc(16, 16, 8, A(Gl.L, 80));
            for (int k = 0; k < 4; k++) Ray(16, 16, k * 90, 15, Gl.S, 3);
            for (int k = 0; k < 4; k++) Ray(16, 16, k * 90, 14, Gl.B, 1);
            for (int k = 0; k < 4; k++) { Ray(16, 16, 45 + k * 90, 9, Em.B, 2); Ray(16, 16, 45 + k * 90, 8, Gl.B, 1); }
            C.Disc(16, 16, 5, Gl.B); C.Disc(16, 16, 3.6f, Gl.L); C.Disc(16, 16, 2.2f, Sm.L);
            foreach (var (x, y) in new[] { (5, 24), (26, 7), (25, 26), (7, 6) }) C.Set(x, y, Gl.L);
        }

        private static void Ray(int cx, int cy, float deg, float len, Color32 c, int thick)
        {
            float r = deg * Mathf.Deg2Rad;
            C.Line(cx, cy, cx + Mathf.RoundToInt(Mathf.Cos(r) * len), cy + Mathf.RoundToInt(Mathf.Sin(r) * len), c, thick);
        }

        // ---------- 점령된 종점 (128) ----------

        private static void Terminal()
        {
            Cobble(0, 0, 128, 128, G, 331);
            Darken(0.28f);
            for (int k = 0; k < 9; k++) C.Disc(10 + H(k, 1, 77) % 108, 10 + H(k, 2, 77) % 108, 3 + H(k, 3, 77) % 5, A(Ms.S, 170));
            for (int k = 0; k < 5; k++) C.Ellipse(16 + H(k, 4, 77) % 96, 14 + H(k, 5, 77) % 100, 7 + H(k, 6, 77) % 6, 4, A(O.S, 90));
            foreach (var (x, y) in new[] { (48, 18), (84, 100), (100, 50) })
                for (int i = 0; i < 3; i++) C.Line(x + i * 3, y, x + i * 3 + 6, y + 7, A(O.S, 200));
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float d = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) + H(x / 4, y / 4, 7) % 9;
                if (d > 64) C.Erase(x, y);
                else if (d > 58) { Color32 p = C.Get(x, y); C.Px[y * 128 + x] = new Color32(p.r, p.g, p.b, (byte)(p.a * (64 - d) / 6f)); }
            }
            C.Ellipse(22, 56, 16, 5, A(O.S, 140));
            C.Ellipse(64, 36, 40, 6, A(O.S, 120));

            Layer();
            Box(2, 102, 9, 8, Fe);
            for (int y = 56; y < 110; y += 9) { C.Line(16, y, 25, y + 9, Fe.S); C.Line(25, y, 16, y + 9, Fe.S); }
            C.Rect(14, 56, 3, 52, Fe.B); C.Rect(14, 56, 1, 52, Fe.L); C.Rect(24, 56, 3, 52, Fe.B); C.Rect(24, 56, 1, 52, Fe.L);
            Box(10, 98, 20, 11, Cu); Window(14, 101, 5, 4, false); C.Set(16, 103, Mo.L);
            C.Line(28, 106, 90, 116, Fe.B, 2); C.Line(28, 111, 90, 118, Fe.S);
            for (int x = 32; x < 90; x += 7) C.Line(x, 106 + (x - 28) * 10 / 62, x + 4, 111 + (x - 28) * 7 / 62, Fe.S);
            C.Line(88, 116, 88, 98, O.L); C.Rect(86, 94, 4, 4, Fe.L); C.Set(86, 94, O.B);
            Rust(2, 56, 90, 64, 13, 5);
            EndLayer();

            Layer();
            Box(26, 46, 34, 10, Sa); C.Rect(26, 47, 34, 1, Bs.S); Bricks(26, 38, 34, 8, Br);
            for (int x = 54; x < 62; x++) for (int y = 50 + (x - 54); y < 57; y++) C.Erase(x, y);
            Box(78, 42, 26, 10, new Swatch(Sa.B, Sa.S, Mo.S)); C.Rect(78, 43, 26, 1, Bs.S); Bricks(78, 34, 26, 8, new Swatch(Br.B, Br.S, Mo.S));
            for (int x = 78; x < 84; x++) for (int y = 46 + (84 - x); y < 53; y++) C.Erase(x, y);
            Rubble(56, 28, 26, 14, 12, 19);
            C.Line(66, 30, 74, 44, Wd.S, 2);
            EndLayer();

            Layer();
            C.Line(36, 56, 43, 80, Fe.B, 2); C.Line(35, 56, 42, 80, Fe.L);
            C.Rect(33, 54, 7, 3, Fe.S);
            Box(40, 80, 9, 8, Bs); C.Rect(42, 81, 5, 6, O.S); C.Set(44, 84, Mo.L); C.Set(45, 83, Mo.L);
            C.Line(40, 88, 49, 87, Fe.S, 2);
            EndLayer();

            Layer();
            Crate(90, 70, 14, 12); Crate(106, 60, 10, 9); Crate(16, 22, 13, 11);
            Rubble(94, 54, 14, 6, 6, 23);
            Barrel(110, 40, 6, false); Barrel(32, 18, 5, true);
            EndLayer();

            Layer();
            C.Rect(83, 82, 3, 7, Fe.B);
            C.Line(60, 90, 82, 84, Fe.B, 2);
            for (int i = 0; i < 22; i++)
            {
                int bx = 54 + i, byy = 92 - i * 4 / 22;
                C.Rect(bx, byy, 1, 9, Wd.B); C.Set(bx, byy + 8, Wd.L); C.Set(bx, byy, Wd.S);
                if (i > 2 && i < 19 && i % 3 != 0) C.Rect(bx, byy + 3, 1, 3, i % 4 == 0 ? Bs.B : Bs.L);
            }
            C.Line(72, 95, 76, 90, O.B);
            EndLayer();

            foreach (var (x, y, hot) in new[] { (34, 62, true), (104, 28, false), (62, 108, false) })
            {
                C.Set(x, y, hot ? Gl.L : Em.L); C.Set(x + 3, y, hot ? Gl.L : Em.L);
                C.Disc(x + 2, y + 0.5f, 3, A(Gl.B, 30));
            }
        }

        // ---------- 콘택트 시트 ----------

        /// <summary>위 두 줄은 칸(136px)마다 정수 배 확대, 아랫줄은 실제 크기로 돌바닥색 위에 늘어놓는다.</summary>
        private static byte[] ContactSheet()
        {
            const int cell = 136, perRow = 5;
            int rows = (Made.Count + perRow - 1) / perRow + 1;
            int w = cell * perRow, h = cell * rows;
            var fill = new Color32[w * h];
            for (int i = 0; i < fill.Length; i++) fill[i] = Bg.B;
            for (int y = 0; y < cell; y++) for (int x = 0; x < w; x++) fill[y * w + x] = Mix(G.S, Mo.B, 0.3f);
            for (int i = 0; i < Made.Count; i++)
            {
                var m = Made[i];
                int scale = Math.Max(1, 128 / m.size);
                int ox = (i % perRow) * cell + 4 + (128 - m.size * scale) / 2, oy = (rows - 1 - i / perRow) * cell + 4 + (128 - m.size * scale) / 2;
                Blit(fill, w, m.px, m.size, ox, oy, scale);
            }
            int cx = 4;
            foreach (var m in Made) { Blit(fill, w, m.px, m.size, cx, 4, 1); cx += m.size + 4; }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(fill); tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
        }

        private static void Blit(Color32[] dst, int w, Color32[] src, int size, int ox, int oy, int scale)
        {
            for (int y = 0; y < size * scale; y++) for (int x = 0; x < size * scale; x++)
            {
                int k = (oy + y) * w + ox + x;
                if (ox + x >= w || k >= dst.Length) continue;
                dst[k] = PixelCanvas.Over(src[(y / scale) * size + x / scale], dst[k]);
            }
        }
    }
}
