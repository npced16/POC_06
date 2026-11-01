using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 036. 폐허 연출 소품을 팔레트 색으로 그린다(64×64, 1칸): 물웅덩이 2종×2프레임(녹물 섞인 탁한 물, 기름막, 잔물결), 잡초 3종(틈새 풀·이음새 이끼·마른 풀),
    /// 잔해 2종(부서진 톱니와 벽돌·녹슨 파이프와 철판), 모닥불 2프레임(돌 고리·장작·드럼통), 천막과 침낭 1종. 장식 아이템 아이콘 3장(deco_campfire·deco_puddle·deco_weeds)은
    /// 같은 그림을 Assets/Art/Generated/items/에도 저장해 기존 ItemSprites 재생성(SpriteGenTiles)이 찾게 하고, ItemSprites.asset에 바로 덧붙인다.
    /// 출력: Assets/Art/Generated/ruin/(PPU 64, Point, 무압축), ruin_contact_sheet.png(증거 폴더에 복사).
    /// </summary>
    public static class SpriteGenRuin
    {
        public const string OUTPUT = "Assets/Art/Generated/ruin/";
        public const string ITEM_OUTPUT = "Assets/Art/Generated/items/";
        public const string ITEM_SPRITES = "Assets/Scripts/Items/Resources/Items/ItemSprites.asset";
        public const string EVIDENCE = "production/qa/evidence/story-036-ruin-dressing/";

        private static readonly List<(string name, Color32[] px)> Made = new List<(string, Color32[])>();

        /// <summary>폐허 소품 12장, 장식 아이콘 3장, 콘택트 시트를 만들고 임포터와 ItemSprites를 갱신한다.</summary>
        [MenuItem("IronRing/Art/Generate Ruin Sprites")]
        public static void Generate()
        {
            Load();
            Directory.CreateDirectory(OUTPUT);
            Made.Clear();
            for (int v = 0; v < 2; v++) for (int f = 0; f < 2; f++) { Begin(); Puddle(v, f); Save("ruin_puddle_" + v + "_" + f); }
            for (int v = 0; v < 3; v++) { Begin(); Weeds(v); Save("ruin_weeds_" + v); }
            for (int v = 0; v < 2; v++) { Begin(); RubblePile(v); Save("ruin_rubble_" + v); }
            for (int f = 0; f < 2; f++) { Begin(); Campfire(f); Save("ruin_campfire_" + f); }
            Begin(); Tent(); Save("ruin_tent_0");

            var icons = new (string id, string source)[] { ("deco_campfire", "ruin_campfire_0"), ("deco_puddle", "ruin_puddle_0_0"), ("deco_weeds", "ruin_weeds_0") };
            Directory.CreateDirectory(ITEM_OUTPUT);
            foreach (var icon in icons) File.Copy(OUTPUT + icon.source + ".png", ITEM_OUTPUT + icon.id + ".png", true);

            var sheet = new List<Color32[]>();
            foreach (var m in Made) sheet.Add(m.px);
            File.WriteAllBytes(OUTPUT + "ruin_contact_sheet.png", Sheet(sheet, 6));
            Directory.CreateDirectory(EVIDENCE);
            File.Copy(OUTPUT + "ruin_contact_sheet.png", EVIDENCE + "ruin_contact_sheet.png", true);

            AssetDatabase.Refresh();
            foreach (var m in Made) ConfigureImporter(OUTPUT + m.name + ".png");
            foreach (var icon in icons) ConfigureImporter(ITEM_OUTPUT + icon.id + ".png");
            ConfigureImporter(OUTPUT + "ruin_contact_sheet.png");
            int added = AppendItemSprites(icons);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpriteGenRuin] " + Made.Count + " ruin sprites, ItemSprites entries set: " + added);
        }

        private static void Save(string name)
        {
            C.Save(OUTPUT + name + ".png");
            Made.Add((name, (Color32[])C.Px.Clone()));
        }

        /// <summary>ItemSprites.asset의 _entries에 장식 아이콘을 id로 넣거나 바꾼다(다른 항목은 그대로).</summary>
        private static int AppendItemSprites((string id, string source)[] icons)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ITEM_SPRITES);
            if (asset == null) { Debug.LogWarning("[SpriteGenRuin] ItemSprites.asset missing: " + ITEM_SPRITES); return 0; }
            var so = new SerializedObject(asset);
            SerializedProperty entries = so.FindProperty("_entries");
            int set = 0;
            foreach (var icon in icons)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ITEM_OUTPUT + icon.id + ".png");
                if (sprite == null) continue;
                int index = -1;
                for (int i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue == icon.id) { index = i; break; }
                if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
                SerializedProperty e = entries.GetArrayElementAtIndex(index);
                e.FindPropertyRelative("_id").stringValue = icon.id;
                e.FindPropertyRelative("_sprite").objectReferenceValue = sprite;
                set++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return set;
        }

        // ----- 그림 -----

        /// <summary>녹물 섞인 탁한 웅덩이: 젖은 가장자리, 깊은 그늘, 녹 얼룩, 기름막 무지개 띠, 프레임마다 다른 잔물결.</summary>
        private static void Puddle(int variant, int frame)
        {
            (float x, float y, float rx, float ry)[] blobs = variant == 0
                ? new[] { (32f, 30f, 21f, 12f), (20f, 37f, 10f, 8f), (44f, 24f, 11f, 7f) }
                : new[] { (22f, 24f, 13f, 9f), (43f, 40f, 12f, 8f), (33f, 32f, 7f, 5f) };
            Color32 rim = Mix(Mo.S, O.B, 0.3f), wet = Mix(G.S, Mo.B, 0.5f);
            Color32 water = Mix(Wa.B, Cu.S, 0.3f), deep = Mix(Wa.S, O.B, 0.35f), lite = Mix(Wa.L, Sm.S, 0.3f), rust = Mix(Cu.S, Wa.S, 0.35f);
            foreach (var b in blobs) C.Ellipse(b.x, b.y, b.rx + 3f, b.ry + 2.5f, A(wet, 150));
            foreach (var b in blobs) C.Ellipse(b.x, b.y, b.rx + 1f, b.ry + 1f, rim);
            foreach (var b in blobs) C.Ellipse(b.x, b.y, b.rx, b.ry, water);
            foreach (var b in blobs) C.Ellipse(b.x + 2f, b.y - 1.5f, b.rx * 0.62f, b.ry * 0.5f, deep);
            for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
            {
                Color32 p = C.Get(x, y);
                if (p.a < 255 || !Near(p, water)) continue;
                bool edge = !Near(C.Get(x, y + 2), water) || !Near(C.Get(x - 2, y), water);
                if (edge && H(x, y, 31 + variant) % 3 == 0) C.Set(x, y, rust);
            }
            var main = blobs[0];
            int sx = (int)(main.x - main.rx * 0.5f), sy = (int)(main.y + main.ry * 0.35f);
            for (int i = 0; i < 9; i++)
            {
                int yy = sy + (i < 3 ? 0 : i < 6 ? 1 : 0);
                C.Set(sx + i, yy + 1, A(Bs.L, 190));
                C.Set(sx + i + 1, yy, A(Fa.L, 170));
                C.Set(sx + i + 2, yy - 1, A(Wa.L, 200));
            }
            C.Rect((int)(main.x - main.rx * 0.65f), (int)(main.y + 2), 3, 1, lite);
            C.Set((int)(main.x + main.rx * 0.4f), (int)(main.y + main.ry * 0.5f), lite);
            var ripple = blobs[frame == 0 ? 2 : 1];
            float r = frame == 0 ? 2.5f : 4.5f;
            for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
            {
                float dx = (x + 0.5f - ripple.x) / (r * 1.6f), dy = (y + 0.5f - ripple.y) / r, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 0.8f && d <= 1.05f && C.Get(x, y).a == 255 && !Near(C.Get(x, y), rim)) C.Set(x, y, A(lite, 220));
            }
            if (frame == 1) C.Set((int)ripple.x, (int)ripple.y, lite);
            else
            {
                const float r2 = 5.5f;
                for (int k = 0; k < 10; k += 2)
                {
                    float a = k * 0.63f;
                    int x = (int)(ripple.x + Mathf.Cos(a) * r2 * 1.6f), y = (int)(ripple.y + Mathf.Sin(a) * r2);
                    if (C.Get(x, y).a == 255) C.Set(x, y, A(lite, 140));
                }
            }
        }

        private static bool Near(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 12;

        /// <summary>잡초: 0 = 바닥 틈의 풀포기, 1 = 쇠판 이음새의 이끼 띠, 2 = 키 큰 마른 풀과 씨앗 이삭.</summary>
        private static void Weeds(int variant)
        {
            if (variant == 0)
            {
                int[] cx = { 12, 20, 27, 34, 41, 48, 54 };
                for (int i = 0; i < cx.Length - 1; i++) C.Line(cx[i], 20 + H(i, 0, 5) % 4, cx[i + 1], 20 + H(i + 1, 0, 5) % 4, O.S, 2);
                Layer();
                for (int k = 0; k < 14; k++)
                {
                    int bx = 16 + k * 3 + H(k, 1, 7) % 2, by = 21 + H(k, 2, 7) % 3, len = 7 + H(k, 3, 7) % 9, lean = H(k, 4, 7) % 7 - 3;
                    Color32 c = k % 4 == 0 ? Sa.S : k % 4 == 1 ? Ms.L : k % 4 == 2 ? Ms.B : Mix(Sa.B, Ms.B, 0.5f);
                    C.Line(bx, by, bx + lean, by + len, c, 2);
                    C.Set(bx + lean, by + len, k % 3 == 0 ? Sa.L : Ms.L);
                }
                C.Disc(30, 22, 3, Sh.S); C.Disc(40, 23, 2.5f, Sh.S); C.Disc(29, 23, 1.5f, Sh.B);
                EndLayer();
            }
            else if (variant == 1)
            {
                C.Rect(4, 31, 56, 2, Mo.S); C.Rect(4, 33, 56, 1, G.L);
                RivetsH(7, 58, 29, 9, Fe.S); RivetsH(7, 58, 35, 9, Fe.S);
                Layer();
                for (int k = 0; k < 8; k++)
                {
                    float x = 9 + k * 6.5f + H(k, 1, 11) % 3, y = 32 + (H(k, 2, 11) % 5 - 2), r = 2.5f + H(k, 3, 11) % 3;
                    C.Disc(x, y, r, Ms.S); C.Disc(x - 0.5f, y + 0.5f, r - 0.8f, Ms.B); C.Disc(x - 1f, y + 1f, r * 0.4f, Ms.L);
                }
                for (int k = 0; k < 6; k++)
                {
                    int x = 12 + k * 8 + H(k, 4, 11) % 4, y = 35 + H(k, 5, 11) % 3;
                    C.Line(x, y, x - 1, y + 4, Sh.B); C.Set(x - 1, y + 5, Sh.L);
                }
                EndLayer();
                for (int k = 0; k < 10; k++) C.Set(8 + H(k, 6, 11) % 48, 24 + H(k, 7, 11) % 16, k % 2 == 0 ? Ms.S : Ms.B);
            }
            else
            {
                C.Ellipse(32, 18, 9, 3, A(O.S, 120));
                Layer();
                for (int k = 0; k < 15; k++)
                {
                    float a = Mathf.Lerp(0.55f, 2.6f, k / 14f);
                    int len = 15 + H(k, 1, 13) % 12;
                    int ex = (int)(32 + Mathf.Cos(a) * len * 0.9f), ey = (int)(19 + Mathf.Sin(a) * len);
                    Color32 c = k % 3 == 0 ? Sa.B : k % 3 == 1 ? Sa.S : Ms.L;
                    C.Line(32 + (k % 5 - 2), 19, ex, ey, c, k % 2 == 0 ? 2 : 1);
                    if (k % 4 == 1) { C.Rect(ex - 1, ey, 2, 4, Sa.L); C.Set(ex, ey + 4, Sa.B); }
                }
                C.Ellipse(26, 20, 4, 2, Sh.S); C.Ellipse(38, 21, 4, 2, Sh.B); C.Ellipse(32, 19, 3, 2, Ms.B);
                EndLayer();
            }
        }

        /// <summary>잔해: 0 = 반쯤 부러진 구리 톱니와 벽돌 더미, 1 = 끊어진 녹슨 파이프와 휜 철판.</summary>
        private static void RubblePile(int variant)
        {
            C.Ellipse(32, 24, 24, 9, A(O.S, 110));
            Layer();
            if (variant == 0)
            {
                Gear(24, 32, 13, 10, Cu);
                for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
                    if (x > 26 && y > 30 + (x - 26) * 0.9f) C.Erase(x, y);
                for (int k = 0; k < 18; k++)
                {
                    int x = 14 + H(k, 1, 17) % 22, y = 22 + H(k, 2, 17) % 20;
                    if (C.Get(x, y).a > 0) C.Set(x, y, Mix(Cu.S, Wd.S, 0.5f));
                }
                Box(34, 14, 10, 5, Br); Box(43, 17, 9, 5, Br); Box(38, 19, 10, 5, Br); Box(46, 23, 7, 4, W);
                Rubble(12, 12, 40, 8, 6, 23);
            }
            else
            {
                var rusty = new Swatch(Mix(Fe.L, Cu.B, 0.45f), Mix(Fe.B, Cu.S, 0.45f), Mix(Fe.S, Cu.S, 0.3f));
                CylH(10, 30, 36, 8, rusty);
                Box(9, 29, 3, 10, Bs);
                for (int y = 30; y < 38; y++) for (int x = 40; x < 46; x++) if (x > 40 + H(0, y, 29) % 5) C.Erase(x, y);
                C.Rect(40, 32, 2, 4, O.S);
                for (int k = 0; k < 14; k++) C.Set(12 + H(k, 1, 31) % 30, 31 + H(k, 2, 31) % 6, Cu.S);
                Box(24, 14, 13, 9, Fe); RivetsH(26, 35, 21, 3, Bs.S); C.Line(25, 15, 35, 21, Fe.S);
                Box(44, 40, 11, 7, W2); C.Rect(44, 43, 11, 1, W2.S);
                Rubble(10, 16, 44, 8, 5, 37);
            }
            EndLayer();
        }

        /// <summary>모닥불: 광륜, 돌 고리, 엇갈린 장작과 재, 프레임마다 다른 불꽃, 불티, 오른쪽 위 녹슨 드럼통.</summary>
        private static void Campfire(int frame)
        {
            Glow(31.5f, 29.5f, 22);
            Layer();
            C.Disc(32, 27, 9, Mo.S);
            C.Disc(32, 27, 6, Mix(Mo.S, Sm.S, 0.25f));
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI * 2 / 10;
                float sx = 32 + Mathf.Cos(a) * 12.5f, sy = 27 + Mathf.Sin(a) * 10f;
                Dome(sx, sy, 3.2f + H(k, 1, 41) % 2 * 0.6f, k % 3 == 0 ? Sa : G);
            }
            C.Line(23, 21, 40, 33, Wd.B, 3); C.Line(23, 33, 40, 21, Wd.S, 3);
            C.Line(23, 22, 40, 34, Wd.L, 1);
            C.Set(23, 21, Wd.L); C.Set(40, 21, Wd.L);
            EndLayer();
            Layer();
            C.Ellipse(32, 28, 6, 4, Em.S);
            if (frame == 0)
            {
                C.Ellipse(32, 32, 4.5f, 7, Em.B); C.Ellipse(27.5f, 30, 2.5f, 4.5f, Em.B); C.Ellipse(36.5f, 30.5f, 2.5f, 4, Em.B);
                C.Ellipse(32, 31, 3, 5, Em.L); C.Ellipse(32, 30, 1.6f, 3, Gl.L); C.Rect(32, 38, 1, 2, Em.L);
            }
            else
            {
                C.Ellipse(31, 31, 4.5f, 6, Em.B); C.Ellipse(36, 32, 2.5f, 5, Em.B); C.Ellipse(27, 29.5f, 2.5f, 3.5f, Em.B);
                C.Ellipse(31.5f, 30.5f, 3, 4.5f, Em.L); C.Ellipse(33, 30, 1.6f, 2.6f, Gl.L); C.Rect(36, 37, 1, 2, Em.L);
            }
            EndLayer();
            int[] spark = frame == 0 ? new[] { 29, 43, 35, 46, 31, 49 } : new[] { 34, 42, 28, 45, 36, 50 };
            for (int i = 0; i < spark.Length; i += 2) C.Set(spark[i], spark[i + 1], i == 0 ? Gl.L : Em.L);
            Layer();
            C.Disc(52, 49, 7, Fe.S);
            C.Disc(51.5f, 49.5f, 6, Mix(Fe.B, Cu.S, 0.35f));
            C.Ring(52, 49, 3.5f, 4.5f, Fe.L);
            C.Disc(52, 49, 3.5f, O.S);
            C.Set(49, 52, Cu.L); C.Set(56, 46, Cu.S);
            EndLayer();
        }

        /// <summary>생존자 흔적: 기운 천막(밝은 윗면·그늘 아랫면, 덧댄 천, 줄과 말뚝)과 말아 둔 침낭, 깡통 잔.</summary>
        private static void Tent()
        {
            C.Line(8, 26, 3, 20, Sa.S); C.Line(40, 26, 46, 20, Sa.S); C.Line(8, 54, 3, 59, Sa.S); C.Line(40, 54, 46, 59, Sa.S);
            Layer();
            C.Rect(8, 41, 33, 13, Fa.L);
            C.Rect(8, 27, 33, 14, Fa.S);
            for (int x = 10; x < 40; x += 6) { C.Rect(x, 42, 1, 11, Fa.B); C.Rect(x, 28, 1, 12, Mix(Fa.S, O.B, 0.3f)); }
            C.Rect(7, 40, 35, 2, Wd.L); C.Rect(7, 40, 35, 1, Wd.S);
            Box(24, 45, 8, 6, Sa); C.Rect(25, 48, 6, 1, Sa.S);
            C.Rect(8, 27, 33, 1, Mix(Fa.S, O.B, 0.5f));
            C.Rect(3, 19, 2, 2, Wd.S); C.Rect(45, 19, 2, 2, Wd.S); C.Rect(3, 59, 2, 2, Wd.S); C.Rect(45, 59, 2, 2, Wd.S);
            EndLayer();
            Layer();
            CylH(26, 8, 26, 9, Sa);
            C.Disc(26, 12.5f, 4.5f, Sa.S); C.Ring(26, 12.5f, 1.5f, 2.6f, Sa.L); C.Set(26, 12, O.S);
            C.Rect(34, 8, 2, 9, Wd.S); C.Rect(46, 8, 2, 9, Wd.S);
            C.Disc(56, 22, 2.5f, Fe.B); C.Disc(55.5f, 22.5f, 1.2f, Fe.L);
            EndLayer();
        }
    }
}
