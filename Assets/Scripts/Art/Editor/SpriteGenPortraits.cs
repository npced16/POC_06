using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using IronRing.Npc;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 029. NPC 상반신 초상(96×128, PPU 64, Point)을 종이 인형식으로 조합해 그린다(Assets/Art/Generated/portraits/).
    /// 얼굴형 3 · 피부 4 · 눈 3 · 머리 모양 6 · 머리색 6 · 수염 4 · 머리 장비 6 · 옷깃 4 · 장식 7을 NPC id 시드(FNV-1a)로 고르고,
    /// NpcPool.json의 선택 항목 <c>_portrait</c>(<c>_hat _beard _hair _collar _acc</c>)가 있으면 그 값을 쓴다. 색은 모두 SteampunkPalette.json.
    /// 끝나면 Resources/Npc/NpcPortraits.asset(NpcPortraitCatalog)을 SerializedObject로 채운다(멱등).
    /// </summary>
    public static class SpriteGenPortraits
    {
        /// <summary>출력 폴더.</summary>
        public const string OUTPUT = "Assets/Art/Generated/portraits/";
        /// <summary>초상 카탈로그 에셋 경로.</summary>
        public const string CATALOG = "Assets/Scripts/Npc/Resources/Npc/NpcPortraits.asset";
        /// <summary>NPC 풀 데이터(힌트 읽기 전용, NpcPoolData를 거치지 않는다).</summary>
        public const string POOL = "Assets/Scripts/Npc/Data/NpcPool.json";
        /// <summary>초상 크기.</summary>
        public const int WIDTH = 96, HEIGHT = 128;
        /// <summary>2배 확대 미리보기(검수용, 프로젝트 Temp 폴더).</summary>
        public const string PREVIEW = "Temp/portraits_preview_x2.png";

        private const int CANVAS = 128, CROP = 16, CX = 62, CY = 64;

        /// <summary>머리 장비 값.</summary>
        public static readonly string[] HATS = { "hat", "cap", "goggles", "mask", "bandana", "bare" };
        /// <summary>수염 값.</summary>
        public static readonly string[] BEARDS = { "none", "mustache", "beard", "goatee" };
        /// <summary>머리색 값.</summary>
        public static readonly string[] HAIRS = { "black", "brown", "copper", "grey", "blond", "auburn" };
        /// <summary>옷깃 값.</summary>
        public static readonly string[] COLLARS = { "coat", "apron", "scarf", "uniform" };
        /// <summary>장식 값(soot·glow는 힌트 전용).</summary>
        public static readonly string[] ACCS = { "none", "monocle", "pipe", "earring", "scar", "soot", "glow" };

        private static readonly string[] SeedHats = { "bare", "bare", "cap", "hat", "goggles", "bandana", "bare", "cap" };
        private static readonly string[] SeedBeards = { "none", "none", "mustache", "beard", "goatee", "none" };
        private static readonly string[] SeedAccs = { "none", "none", "earring", "scar", "pipe", "monocle", "none" };

        /// <summary>한 사람의 조합.</summary>
        public sealed class Look
        {
            public int Face, Skin, Eyes, HairStyle, Coat, Seed;
            public string Hair, Hat, Beard, Collar, Acc;
            public bool Shadowed;
            /// <summary>로그용 요약.</summary>
            public override string ToString() => $"face{Face} skin{Skin} eyes{Eyes} hair{HairStyle}/{Hair} hat={Hat} beard={Beard} collar={Collar} acc={Acc}";
        }

        private static Look _l;
        private static Swatch _skin, _hair, _coat;
        private static float _rx, _ry, _p, _taper;

        /// <summary>모든 초상·대체 초상·콘택트 시트를 다시 그리고 카탈로그를 채운다.</summary>
        [MenuItem("IronRing/Art/Generate NPC Portraits")]
        public static void Generate()
        {
            ArtKit.Load();
            Directory.CreateDirectory(OUTPUT);
            Pool pool = JsonUtility.FromJson<Pool>(File.ReadAllText(POOL));
            if (pool?._npcs == null || pool._npcs.Length == 0) throw new FormatException("No NPCs in " + POOL);
            var ids = new List<string>();
            var sheet = new List<Color32[]>();
            foreach (PoolNpc npc in pool._npcs)
            {
                if (string.IsNullOrEmpty(npc._id)) continue;
                Look look = Compose(npc._id, npc._portrait);
                sheet.Add(SavePortrait("portrait_" + npc._id, look));
                ids.Add(npc._id);
                Debug.Log($"PORTRAIT: {npc._id} {look}");
            }
            Look fallback = Compose("fallback", null);
            fallback.Hat = "bare"; fallback.Beard = "none"; fallback.Acc = "none"; fallback.Shadowed = true;
            sheet.Add(SavePortrait("portrait_fallback", fallback));

            File.WriteAllBytes(OUTPUT + "portraits_contact_sheet.png", SheetPng(sheet, 6, 1));
            File.WriteAllBytes(PREVIEW, SheetPng(sheet, 6, 2));
            AssetDatabase.Refresh();
            foreach (string id in ids) ConfigureImporter(OUTPUT + "portrait_" + id + ".png");
            ConfigureImporter(OUTPUT + "portrait_fallback.png");
            ConfigureImporter(OUTPUT + "portraits_contact_sheet.png");
            FillCatalog(ids);
            Debug.Log($"SpriteGenPortraits: 초상 {ids.Count}장 + 대체 초상, 카탈로그 {CATALOG}");
        }

        /// <summary>id 시드와 힌트로 조합을 정한다(힌트 값이 비었거나 모르는 값이면 시드).</summary>
        public static Look Compose(string id, PortraitHint hint)
        {
            int seed = Fnv(id);
            int Pick(int salt, int n) => PixelCanvas.Hash(seed, salt, 29) % n;
            string Choose(string hinted, string[] allowed, string[] seeded, int salt) =>
                !string.IsNullOrEmpty(hinted) && Array.IndexOf(allowed, hinted) >= 0 ? hinted : seeded[Pick(salt, seeded.Length)];
            return new Look
            {
                Seed = seed,
                Face = Pick(1, 3), Skin = Pick(2, 4), Eyes = Pick(3, 3), HairStyle = Pick(4, 6), Coat = Pick(5, 6),
                Hair = Choose(hint?._hair, HAIRS, HAIRS, 6),
                Hat = Choose(hint?._hat, HATS, SeedHats, 7),
                Beard = Choose(hint?._beard, BEARDS, SeedBeards, 8),
                Collar = Choose(hint?._collar, COLLARS, COLLARS, 9),
                Acc = Choose(hint?._acc, ACCS, SeedAccs, 10),
            };
        }

        private static int Fnv(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s) { h ^= ch; h *= 16777619; }
                return (int)(h & 0x7fffffff);
            }
        }

        private static Color32[] SavePortrait(string name, Look look)
        {
            Draw(look);
            var px = new Color32[WIDTH * HEIGHT];
            for (int y = 0; y < HEIGHT; y++) for (int x = 0; x < WIDTH; x++) px[y * WIDTH + x] = C.Get(x + CROP, y);
            var texture = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
            texture.SetPixels32(px); texture.Apply();
            File.WriteAllBytes(OUTPUT + name + ".png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return px;
        }

        // ---------------------------------------------------------------- 그리기

        private static void Draw(Look look)
        {
            _l = look;
            ArtKit.C = new PixelCanvas(CANVAS);
            _skin = look.Skin == 0 ? P["skin_fair"] : look.Skin == 1 ? P["skin_tan"] : look.Skin == 2 ? P["skin_olive"] : P["skin_deep"];
            _hair = look.Hair == "black" ? Fe : look.Hair == "brown" ? Wd : look.Hair == "copper" ? Cu : look.Hair == "blond" ? Bs : look.Hair == "auburn" ? Fa : new Swatch(Sm.S, G.L, G.B);
            _coat = look.Collar == "uniform" ? (look.Coat % 2 == 0 ? Fe : W2) : look.Coat switch { 0 => Fa, 1 => Wd, 2 => W, 3 => W2, 4 => Cu, _ => Ms };
            switch (look.Face)
            {
                case 0: _rx = 19; _ry = 26; _p = 2f; _taper = 0.28f; break;
                case 1: _rx = 22; _ry = 24; _p = 2f; _taper = 0.12f; break;
                default: _rx = 21; _ry = 25; _p = 3.2f; _taper = 0.16f; break;
            }

            if (look.HairStyle == 2 || look.HairStyle == 4) { Layer(); HairBack(); EndLayer(); }
            Layer(); Body(); EndLayer();
            Layer(); Head(); EndLayer();
            if (look.Collar == "scarf" || look.Collar == "uniform") { Layer(); CollarTop(); EndLayer(); }
            Face();
            Layer(); HairFront(); EndLayer();
            if (look.Beard != "none") { Layer(); FacialHair(); EndLayer(); Mouth(look.Beard == "beard"); }
            if (look.Hat != "bare") { Layer(); Headwear(); EndLayer(); }
            Accessory();
            if (look.Shadowed) Shadow();
        }

        private static bool InHead(int x, int y, float grow = 0f)
        {
            float dy = (y + 0.5f - CY) / (_ry + grow);
            float rx = _rx + grow;
            if (dy < 0) rx *= 1f - _taper * dy * dy;
            float dx = (x + 0.5f - CX) / rx;
            return Mathf.Pow(Mathf.Abs(dx), _p) + Mathf.Pow(Mathf.Abs(dy), _p) <= 1f;
        }

        /// <summary>초버타원 덩어리를 왼쪽 위 빛의 2~3단 명암으로 채운다.</summary>
        private static void Blob(float cx, float cy, float rx, float ry, Swatch s, float p = 2f, float taper = 0f, float rimLight = 0.42f)
        {
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
                {
                    float dy = (y + 0.5f - cy) / ry, r = rx;
                    if (dy < 0) r *= 1f - taper * dy * dy;
                    float dx = (x + 0.5f - cx) / r;
                    float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(dx), p) + Mathf.Pow(Mathf.Abs(dy), p), 1f / p);
                    if (d > 1f) continue;
                    float l = -dx * 0.7f + dy * 0.45f;
                    C.Set(x, y, l > rimLight && d > 0.55f ? s.L : (l < -0.3f || (d > 0.86f && l < 0.05f)) ? s.S : s.B);
                }
        }

        private static bool SameSwatch(Swatch a, Swatch b) => a.B.r == b.B.r && a.B.g == b.B.g && a.B.b == b.B.b;

        private static void Body()
        {
            bool apron = _l.Collar == "apron";
            Swatch cloth = apron ? Sa : _coat;
            Blob(64, 0, 46, 34, cloth, 2.4f, 0f, 0.3f);
            C.Line(30, 2, 34, 18, cloth.S); C.Line(98, 2, 94, 18, cloth.S);
            if (apron)
            {
                Swatch leather = SameSwatch(_coat, Fa) ? Wd : _coat;
                Box(48, 0, 32, 24, leather);
                C.Line(50, 23, 56, 34, Wd.S, 2); C.Line(77, 23, 71, 34, Wd.S, 2);
                C.Set(50, 21, Bs.L); C.Set(77, 21, Bs.L);
                C.Rect(56, 6, 16, 6, leather.S); C.Rect(56, 11, 16, 1, leather.L);
                return;
            }
            for (int y = 8; y <= 34; y++)
            {
                int half = (y - 8) / 3;
                C.Rect(64 - half, y, half * 2, 1, Sm.B);
                C.Rect(64 - half - 4, y, 4, 1, _coat.L);
                C.Rect(64 + half, y, 4, 1, _coat.S);
            }
            C.Rect(63, 8, 2, 26, Sm.S);
            if (_l.Collar == "coat")
            {
                C.Rect(60, 26, 9, 4, Fa.B); C.Rect(62, 20, 5, 7, Fa.S); C.Set(61, 29, Fa.L);
                C.Set(58, 4, Bs.L); C.Set(70, 4, Bs.L); C.Set(58, 0, Bs.B); C.Set(70, 0, Bs.B);
            }
            if (_l.Collar == "uniform")
            {
                C.Rect(56, 0, 16, 34, _coat.B);
                for (int y = 3; y < 28; y += 7) { C.Set(58, y, Bs.L); C.Set(69, y, Bs.L); C.Set(59, y, Bs.S); C.Set(70, y, Bs.S); }
                Blob(30, 27, 11, 4, Bs, 2f, 0f, 0.2f); Blob(98, 27, 11, 4, Bs, 2f, 0f, 0.2f);
                for (int x = 21; x < 40; x += 3) C.Set(x, 23, Bs.S);
                for (int x = 89; x < 108; x += 3) C.Set(x, 23, Bs.S);
            }
        }

        private static void CollarTop()
        {
            if (_l.Collar == "scarf")
            {
                Swatch s = _l.Coat % 2 == 0 ? Ms : Fa;
                if (SameSwatch(_coat, s)) s = Bs;
                Blob(64, 33, 21, 6, s, 2f, 0f, 0.2f);
                Box(44, 8, 9, 24, s);
                for (int y = 11; y < 30; y += 5) C.Rect(45, y, 7, 1, s.S);
                for (int x = 44; x < 53; x += 2) C.Set(x, 7, s.B);
            }
            else
            {
                Box(52, 29, 24, 7, _coat);
                C.Rect(52, 35, 24, 1, Bs.L); C.Rect(52, 29, 24, 1, Bs.S);
                C.Set(54, 32, Bs.L); C.Set(73, 32, Bs.L);
            }
        }

        private static void Head()
        {
            int chin = Mathf.RoundToInt(CY - _ry);
            C.Rect(55, 26, 17, chin - 20, _skin.B);
            C.Rect(66, 26, 6, chin - 20, _skin.S);
            C.Rect(55, chin - 5, 17, 5, _skin.S);
            Blob(CX + _rx - 1, CY - 1, 4.5f, 6.5f, _skin, 2f, 0f, 0.1f);
            C.Rect(Mathf.RoundToInt(CX + _rx - 1), CY - 3, 1, 5, _skin.S);
            Blob(CX, CY, _rx, _ry, _skin, _p, _taper, 0.4f);
        }

        private static int EyeY => CY + 1;
        private static int T => Mathf.RoundToInt(_ry) - 23;

        private static void Face()
        {
            int[] eyes = { CX - 10, CX + 5 };
            Color32 brow = _l.Hair == "grey" ? _hair.S : Mix(_hair.S, O.B, 0.4f);
            foreach (int ex in eyes)
            {
                switch (_l.Eyes)
                {
                    case 0:
                        C.Rect(ex, EyeY - 1, 3, 3, O.S); C.Set(ex, EyeY + 1, Sm.L);
                        C.Rect(ex - 1, EyeY + 4, 5, 1, brow);
                        break;
                    case 1:
                        C.Rect(ex - 1, EyeY - 1, 5, 3, Sm.L); C.Rect(ex - 1, EyeY - 1, 2, 3, O.S); C.Set(ex, EyeY + 1, Sm.B);
                        C.Rect(ex - 1, EyeY + 2, 5, 1, O.B);
                        C.Rect(ex - 1, EyeY + 5, 5, 1, brow); C.Set(ex + 3, EyeY + 6, brow);
                        break;
                    default:
                        C.Rect(ex - 1, EyeY, 5, 1, O.B); C.Rect(ex, EyeY - 1, 2, 1, O.S);
                        C.Rect(ex - 1, EyeY - 2, 4, 1, _skin.S);
                        C.Rect(ex - 1, EyeY + 3, 5, 2, brow);
                        break;
                }
            }
            C.Rect(CX + 9, EyeY - 2, 2, 4, _skin.S);
            C.Rect(CX - 1, CY - 10, 1, 8, _skin.S);
            C.Rect(CX - 5, CY - 11, 5, 1, _skin.S);
            C.Rect(CX - 4, CY - 9, 2, 6, _skin.L);
            C.Set(CX - 5, CY - 10, Mix(_skin.S, O.B, 0.3f));
            Mouth(false);
            C.Rect(CX - 16, CY - 9, 3, 2, A(Fa.L, 90));
            C.Rect(CX + 9, CY - 9, 3, 2, A(Fa.L, 70));
        }

        private static void Mouth(bool inBeard)
        {
            Color32 lip = Mix(_skin.S, Fa.S, 0.5f);
            if (inBeard) { C.Rect(CX - 6, CY - 18, 7, 2, Mix(O.S, Fa.S, 0.3f)); return; }
            C.Rect(CX - 6, CY - 17, 7, 1, lip);
            C.Set(CX - 7, CY - 16, lip);
            C.Rect(CX - 5, CY - 19, 5, 1, _skin.S);
        }

        private static Color32 HairShade(int x, int y)
        {
            float hx = (x + 0.5f - CX) / (_rx + 3), hy = (y + 0.5f - CY) / (_ry + 3);
            if (((x * 2 + y) % 7) == 0) return _hair.S;
            if (hx < -0.15f && hy > 0.55f && hx > -0.75f) return _hair.L;
            if (hx > 0.5f || hy < 0.15f) return _hair.S;
            return _hair.B;
        }

        private static void HairBack()
        {
            for (int y = CY - 26; y < CY + 32; y++) for (int x = CX - 30; x < CX + 30; x++)
            {
                bool inside = _l.HairStyle == 2
                    ? InHead(x, y, 5) && y > CY - 22
                    : InHead(x, y, 4 + 2 * Mathf.Sin(Mathf.Atan2(y - CY, x - CX) * 9)) && y > CY - 8;
                if (inside) C.Set(x, y, HairShade(x, y));
            }
        }

        private static void HairFront()
        {
            int style = _l.HairStyle;
            for (int y = CY - 10; y < CANVAS; y++) for (int x = CX - 30; x < CX + 30; x++)
            {
                float edge = Mathf.Abs(x + 0.5f - CX) / _rx;
                bool inside;
                switch (style)
                {
                    case 0: inside = InHead(x, y, 2) && (y >= CY + 10 || (edge > 0.82f && y >= CY + 1)); break;
                    case 1: inside = InHead(x, y, 2) && (y >= CY + 9 + (x - CX) / 5 || (x < CX - 4 && x > CX - 16 && y >= CY + 6 + (CX - x) / 4) || (edge > 0.84f && y >= CY)); break;
                    case 2: inside = InHead(x, y, 3) && (y >= CY + 8 || edge > 0.8f); break;
                    case 3: inside = InHead(x, y, 2) && ((edge > 0.78f && y >= CY - 2 && y <= CY + 13) || (y >= CY + 19 && (x % 4 == 0))); break;
                    case 4: inside = InHead(x, y, 3 + 2 * Mathf.Sin(Mathf.Atan2(y - CY, x - CX) * 9)) && (y >= CY + 9 || (edge > 0.8f && y >= CY + 2)); break;
                    default:
                        float px = x + 0.5f - (CX - 3), py = y + 0.5f - (CY + _ry + 1);
                        inside = (InHead(x, y, 2) && (y >= CY + 11 || (edge > 0.85f && y >= CY + 2))) || px * px / 225f + py * py / 49f <= 1f;
                        break;
                }
                if (inside) C.Set(x, y, HairShade(x, y));
            }
        }

        private static void FacialHair()
        {
            Swatch h = _hair;
            switch (_l.Beard)
            {
                case "mustache":
                    C.Rect(CX - 11, CY - 16, 15, 3, h.B); C.Rect(CX - 10, CY - 14, 13, 1, h.L);
                    C.Set(CX - 12, CY - 15, h.B); C.Set(CX - 13, CY - 14, h.B); C.Set(CX + 4, CY - 15, h.S); C.Set(CX + 5, CY - 14, h.S);
                    break;
                case "goatee":
                    C.Rect(CX - 9, CY - 15, 11, 2, h.B); C.Rect(CX - 8, CY - 14, 9, 1, h.L);
                    Blob(CX - 2, CY - 23, 4.5f, 5.5f, h, 2f, 0f, 0.2f);
                    break;
                default:
                    float hrx = _rx - 5;
                    for (int y = CY - 32; y < CY + 4; y++) for (int x = CX - 30; x < CX + 30; x++)
                    {
                        float edge = Mathf.Abs(x + 0.5f - CX) / _rx;
                        bool jaw = InHead(x, y, 1) && (y < CY - 13 || (edge > 0.8f && y < CY + 3));
                        float px = x + 0.5f - (CX - 1), py = y + 0.5f - (CY - _ry);
                        bool hang = px * px / (hrx * hrx) + py * py / 49f <= 1f;
                        if (jaw || hang) C.Set(x, y, HairShade(x, y));
                    }
                    C.Rect(CX - 11, CY - 16, 15, 3, h.B); C.Rect(CX - 10, CY - 14, 13, 1, h.L);
                    break;
            }
        }

        private static void Headwear()
        {
            int Y = CY + T;
            switch (_l.Hat)
            {
                case "hat":
                {
                    Swatch s = _l.Coat % 2 == 0 ? Fe : Mo;
                    CylV(CX - 14, Y + 18, 28, 26, s);
                    C.Rect(CX - 15, Y + 43, 30, 3, s.L); C.Rect(CX - 15, Y + 43, 30, 1, s.B);
                    C.Rect(CX - 14, Y + 19, 28, 5, _l.Coat % 3 == 0 ? Fa.B : Cu.S);
                    Gear(CX + 6, Y + 21.5f, 3.5f, 6, Bs);
                    Blob(CX + 1, Y + 17, _rx + 11, 4.5f, s, 2f, 0f, 0.15f);
                    C.Rect(Mathf.RoundToInt(CX - _rx - 8), Y + 17, Mathf.RoundToInt(2 * _rx + 18), 1, s.L);
                    break;
                }
                case "cap":
                {
                    Swatch s = _l.Coat % 3 == 0 ? G : _l.Coat % 3 == 1 ? Wd : W2;
                    for (int y = Y + 10; y < CANVAS; y++) for (int x = CX - 30; x < CX + 30; x++)
                    {
                        float dx = (x + 0.5f - CX - 1) / (_rx + 3), dy = (y + 0.5f - Y - 10) / 14f;
                        if (dx * dx + dy * dy > 1f) continue;
                        float l = -dx * 0.7f + dy * 0.5f;
                        C.Set(x, y, l > 0.4f ? s.L : l < -0.3f ? s.S : s.B);
                    }
                    for (int y = Y + 13; y < Y + 24; y += 4) C.Rect(CX - 14, y, 30, 1, s.S);
                    Blob(CX - 15, Y + 10, 13, 3.5f, s, 2f, 0f, 0.6f);
                    C.Rect(CX - 26, Y + 8, 20, 1, s.S);
                    C.Set(CX + 1, Y + 24, Bs.L); C.Set(CX + 2, Y + 24, Bs.B);
                    break;
                }
                case "goggles":
                {
                    int x0 = Mathf.RoundToInt(CX - _rx - 2), w = Mathf.RoundToInt(2 * _rx + 4);
                    C.Rect(x0, Y + 10, w, 4, Wd.S);
                    C.Rect(x0, Y + 13, w, 1, Wd.B);
                    foreach (int gx in new[] { CX - 8, CX + 7 })
                    {
                        C.Disc(gx, Y + 13, 6.5f, Bs.S);
                        C.Ring(gx, Y + 13, 4.4f, 6.5f, Bs.B);
                        C.Disc(gx, Y + 13, 4.4f, Wa.S);
                        C.Disc(gx - 0.5f, Y + 13.5f, 3.2f, Wa.B);
                        C.Rect(gx - 3, Y + 14, 2, 2, Wa.L); C.Set(gx - 3, Y + 17, Bs.L); C.Set(gx - 4, Y + 16, Bs.L);
                        C.Set(gx + 1, Y + 11, Gl.L);
                    }
                    C.Rect(CX - 2, Y + 12, 3, 2, Bs.S);
                    break;
                }
                case "mask":
                {
                    Box(CX - 18, Y + 14, 36, 16, Fe);
                    C.Rect(CX - 16, Y + 28, 32, 2, Fe.L);
                    C.Rect(CX - 11, Y + 18, 22, 6, O.S); C.Rect(CX - 11, Y + 23, 22, 1, Fe.S);
                    C.Rect(CX - 10, Y + 19, 6, 2, A(Gl.B, 160)); C.Set(CX - 9, Y + 21, Gl.L);
                    RivetsH(CX - 16, CX + 16, Y + 16, 4, Bs.S); RivetsH(CX - 16, CX + 16, Y + 27, 4, Bs.S);
                    C.Disc(CX - 19, Y + 15, 2.5f, Bs.B); C.Disc(CX + 19, Y + 15, 2.5f, Bs.S);
                    break;
                }
                case "bandana":
                {
                    Swatch s = _l.Coat % 2 == 0 ? Fa : Wa;
                    for (int y = Y + 7; y < CANVAS; y++) for (int x = CX - 30; x < CX + 30; x++)
                    {
                        if (!InHead(x, y, 2.5f)) continue;
                        float dx = (x + 0.5f - CX) / _rx;
                        Color32 c = dx > 0.45f || y < Y + 9 ? s.S : dx < -0.2f && y > Y + 14 ? s.L : s.B;
                        if (H(x / 3, y / 3, 41) % 9 == 0 && (x + y) % 3 == 0) c = Sm.S;
                        C.Set(x, y, c);
                    }
                    int kx = Mathf.RoundToInt(CX + _rx + 3);
                    Blob(kx, Y + 9, 4, 3.5f, s, 2f, 0f, 0.2f);
                    C.Line(kx, Y + 6, kx + 4, Y - 2, s.S, 2);
                    C.Line(kx + 2, Y + 7, kx + 8, Y + 1, s.B, 2);
                    break;
                }
            }
        }

        private static void Accessory()
        {
            switch (_l.Acc)
            {
                case "monocle":
                    C.Disc(CX + 6.5f, EyeY + 0.5f, 4.2f, A(Wa.L, 80));
                    C.Ring(CX + 6.5f, EyeY + 0.5f, 4.2f, 5.6f, Bs.B);
                    C.Set(CX + 3, EyeY + 4, Bs.L); C.Set(CX + 4, EyeY + 5, Bs.L);
                    C.Line(CX + 11, EyeY - 3, CX + 16, 30, Bs.S); C.Line(CX + 16, 30, CX + 12, 18, Bs.S);
                    break;
                case "pipe":
                    C.Line(CX - 5, CY - 17, CX - 14, CY - 21, Wd.S, 2);
                    Box(CX - 19, CY - 24, 6, 8, Wd); C.Rect(CX - 18, CY - 17, 4, 1, O.S); C.Set(CX - 17, CY - 17, Em.B);
                    Puff(CX - 18, CY - 11, 3f, 110); Puff(CX - 21, CY - 5, 2.2f, 80);
                    break;
                case "earring":
                    C.Disc(CX + _rx - 0.5f, CY - 9.5f, 1.6f, Bs.B); C.Set(Mathf.RoundToInt(CX + _rx - 1), CY - 9, Bs.L);
                    break;
                case "scar":
                    C.Line(CX - 15, CY + 3, CX - 9, CY - 6, Mix(_skin.S, Fa.S, 0.6f));
                    C.Set(CX - 14, CY, _skin.L); C.Set(CX - 12, CY - 3, _skin.L);
                    break;
                case "soot":
                    for (int k = 0; k < 12; k++)
                    {
                        int x = CX - 16 + H(k, 1, _l.Seed) % 30, y = CY - 18 + H(k, 2, _l.Seed) % 30;
                        if (InHead(x, y)) C.Disc(x, y, 1 + H(k, 3, _l.Seed) % 2, A(O.S, 105));
                    }
                    C.Rect(CX - 16, CY - 9, 6, 2, A(O.S, 90));
                    break;
                case "glow":
                    for (int y = CY - 30; y < CY + 30; y++) for (int x = CX - 30; x < CX + 4; x++)
                    {
                        if (C.Get(x, y).a == 0 || !InHead(x, y, 1) || y > CY + 7) continue;
                        bool rim = C.Get(x - 2, y).a == 0;
                        C.Set(x, y, rim ? A(Gl.L, 170) : A(Gl.B, (byte)(x < CX - 8 ? 70 : 35)));
                    }
                    break;
            }
        }

        /// <summary>대체 초상: 그늘 속 실루엣과 황동 테두리 빛.</summary>
        private static void Shadow()
        {
            Color32[] px = C.Px;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                Color32 m = Mix(px[i], O.S, 0.78f);
                px[i] = new Color32(m.r, m.g, m.b, px[i].a);
            }
            for (int y = 1; y < CANVAS - 1; y++) for (int x = 1; x < CX; x++)
                if (C.Get(x, y).a > 0 && (C.Get(x - 1, y).a == 0 || C.Get(x, y + 1).a == 0)) C.Set(x, y, Bs.S);
            C.Rect(CX - 10, EyeY, 3, 1, Gl.B); C.Rect(CX + 5, EyeY, 3, 1, Gl.B);
        }

        // ---------------------------------------------------------------- 시트·카탈로그

        private static byte[] SheetPng(List<Color32[]> sprites, int perRow, int scale)
        {
            int cw = WIDTH + 8, ch = HEIGHT + 8;
            int rows = Math.Max(1, (sprites.Count + perRow - 1) / perRow);
            int w = cw * perRow * scale, h = ch * rows * scale;
            var fill = new Color32[w * h];
            for (int i = 0; i < fill.Length; i++) fill[i] = Bg.B;
            for (int i = 0; i < sprites.Count; i++)
            {
                int ox = ((i % perRow) * cw + 4) * scale, oy = ((rows - 1 - i / perRow) * ch + 4) * scale;
                for (int y = 0; y < HEIGHT * scale; y++) for (int x = 0; x < WIDTH * scale; x++)
                {
                    int k = (oy + y) * w + ox + x;
                    fill[k] = PixelCanvas.Over(sprites[i][(y / scale) * WIDTH + x / scale], fill[k]);
                }
            }
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false);
            sheet.SetPixels32(fill); sheet.Apply();
            byte[] png = sheet.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sheet);
            return png;
        }

        private static void FillCatalog(List<string> ids)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<NpcPortraitCatalog>(CATALOG);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CATALOG));
                catalog = ScriptableObject.CreateInstance<NpcPortraitCatalog>();
                AssetDatabase.CreateAsset(catalog, CATALOG);
            }
            var so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("_entries");
            entries.arraySize = ids.Count;
            for (int i = 0; i < ids.Count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_id").stringValue = ids[i];
                entry.FindPropertyRelative("_sprite").objectReferenceValue = LoadSprite("portrait_" + ids[i]);
            }
            so.FindProperty("_fallback").objectReferenceValue = LoadSprite("portrait_fallback");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static Sprite LoadSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OUTPUT + name + ".png");
            if (sprite == null) throw new InvalidOperationException("Portrait sprite not imported: " + name);
            return sprite;
        }

#pragma warning disable 0649
        [Serializable] private sealed class Pool { public PoolNpc[] _npcs; }
        [Serializable] private sealed class PoolNpc { public string _id; public PortraitHint _portrait; }
#pragma warning restore 0649
    }

    /// <summary>스토리 029. NpcPool.json의 선택 항목 _portrait(빈 값은 시드로 정함).</summary>
    [Serializable]
    public sealed class PortraitHint
    {
        public string _hat, _beard, _hair, _collar, _acc;
    }
}
