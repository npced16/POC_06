using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 027. 완성 서식지 위에 세우는 건물 조각을 팔레트 기반 64픽셀 도트로 그린다(Assets/Art/Generated/buildings/).
    /// 지붕 4계열(machine·housing·storage·glass) × 이웃 비트마스크 16조각(1=N, 2=E, 4=S, 8=W가 이웃 있음; 닫힌 쪽에 처마·벽, 열린 쪽은 지붕이 이어짐)
    /// + 오목 모서리 덧조각 4종(ne·se·sw·nw), 문 2종, 굴뚝, 서식지 종류별 상징물 19종과 콘택트 시트. 끝나면 InteriorCatalog의 건물 참조를 채운다.
    /// QA 후속(건물 장식): 지붕 색조 3종(TINTS, 팔레트 copper·wall·moss 쪽으로 명도 보존 이동; 외곽선·황동·불빛은 그대로) × 계열마다 20조각,
    /// 정면 창 무늬 덧조각 2종, 지붕 덧소품 10종(DECOR), 그리고 HabitatTypes.json 19종의 3칸 견본 시트
    /// (production/qa/evidence/qa-followups/buildings_contact_sheet.png, 데이터 순서로 4열).
    /// </summary>
    public static class SpriteGenBuildings
    {
        /// <summary>출력 폴더.</summary>
        public const string OUTPUT = "Assets/Art/Generated/buildings/";
        /// <summary>지붕 계열 id(HabitatTypes.json의 _buildingStyle).</summary>
        public static readonly string[] STYLES = { "machine", "housing", "storage", "glass", "yard" };
        /// <summary>오목 모서리 덧조각 순서.</summary>
        public static readonly string[] CORNERS = { "ne", "se", "sw", "nw" };
        /// <summary>상징물 id(HabitatTypes.json의 _signProp).</summary>
        public static readonly string[] SIGNS =
        {
            "lamp_post", "coal_heap", "cobble_arch", "earth_marker", "big_gear", "boiler_dome", "pipe_valve", "small_gear", "girder", "tank", "glass_roof",
            "bunk", "bench_tools", "wire_pole", "water_tower", "twin_lamps", "engine", "dynamo", "crane", "chimney_stack"
        };

        /// <summary>QA 후속. 지붕 색조 id(HabitatTypes.json _decor._roofTint; "base"는 기존 지붕).</summary>
        public static readonly string[] TINTS = { "copper", "wine", "moss" };
        /// <summary>QA 후속. 창 무늬 덧조각 수(무늬 1..N; 0은 지붕 조각의 기본 창).</summary>
        public const int WINDOW_PATTERNS = 2;
        /// <summary>QA 후속. 지붕 덧소품 id(HabitatTypes.json _decor._props).</summary>
        public static readonly string[] DECOR =
        {
            "cog_sign", "steam_pipes", "twin_chimney", "water_tank", "glass_cap", "gas_lamp", "tele_pole", "coal_pile", "iron_frame", "laundry"
        };
        /// <summary>QA 후속. 19종 견본 시트 경로.</summary>
        public const string EVIDENCE_SHEET = "production/qa/evidence/qa-followups/buildings_contact_sheet.png";
        private const string HABITAT_JSON = "Assets/Scripts/Habitats/Resources/Habitats/HabitatTypes.json";

        /// <summary>QA 후속. 색조 지붕 조각 파일 이름(base면 기존 이름).</summary>
        public static string RoofName(string style, string tint, int mask) => IsBase(tint) ? RoofName(style, mask) : "bld_" + style + "_" + tint + "_" + mask.ToString("00");
        /// <summary>QA 후속. 색조 오목 모서리 파일 이름(base면 기존 이름).</summary>
        public static string CornerName(string style, string tint, string corner) => IsBase(tint) ? CornerName(style, corner) : "bld_" + style + "_" + tint + "_inner_" + corner;
        /// <summary>QA 후속. 창 무늬 덧조각 파일 이름(무늬 1부터).</summary>
        public static string WindowName(int pattern) => "bld_window_" + pattern;
        /// <summary>QA 후속. 지붕 덧소품 파일 이름.</summary>
        public static string DecorName(string id) => "bld_decor_" + id;
        private static bool IsBase(string tint) => string.IsNullOrEmpty(tint) || tint == "base";

        /// <summary>지붕 조각 파일 이름.</summary>
        public static string RoofName(string style, int mask) => "bld_" + style + "_" + mask.ToString("00");
        /// <summary>오목 모서리 파일 이름.</summary>
        public static string CornerName(string style, string corner) => "bld_" + style + "_inner_" + corner;
        /// <summary>상징물 파일 이름.</summary>
        public static string SignName(string id) => "bld_sign_" + id;

        /// <summary>모든 건물 스프라이트와 콘택트 시트를 다시 그리고 임포터·카탈로그를 갱신한다.</summary>
        [MenuItem("IronRing/Art/Generate Building Sprites")]
        public static void Generate()
        {
            ArtKit.Load();
            Directory.CreateDirectory(OUTPUT);
            var names = new List<string>();
            var sheet = new List<Color32[]>();
            var pixels = new Dictionary<string, Color32[]>();
            void Save(string name) { C.Save(OUTPUT + name + ".png"); names.Add(name); pixels[name] = C.Px; sheet.Add(C.Px); }
            void SaveQuiet(string name) { C.Save(OUTPUT + name + ".png"); names.Add(name); pixels[name] = C.Px; }
            foreach (string style in STYLES)
            {
                for (int mask = 0; mask < 16; mask++) { Begin(); DrawRoof(style, mask); Save(RoofName(style, mask)); }
                for (int i = 0; i < CORNERS.Length; i++) { Begin(); DrawCorner(style, i); Save(CornerName(style, CORNERS[i])); }
                // QA 후속: 색조 지붕은 기존 시트에 넣지 않는다(19종 견본 시트에서 본다)
                foreach (string tint in TINTS)
                {
                    for (int mask = 0; mask < 16; mask++) { Begin(); DrawRoof(style, mask); Tint(tint); SaveQuiet(RoofName(style, tint, mask)); }
                    for (int i = 0; i < CORNERS.Length; i++) { Begin(); DrawCorner(style, i); Tint(tint); SaveQuiet(CornerName(style, tint, CORNERS[i])); }
                }
            }
            for (int p = 1; p <= WINDOW_PATTERNS; p++) { Begin(); Layer(); DrawWindowPattern(p); EndLayer(); Save(WindowName(p)); }
            foreach (string id in DECOR) { Begin(); C.Ellipse(32, 5, 20, 4, A(O.S, 100)); Layer(); DrawDecor(id); EndLayer(); Save(DecorName(id)); }
            for (int i = 0; i < 2; i++) { Begin(); Layer(); DrawDoor(i); EndLayer(); Save("bld_door_" + i); }
            Begin(); Layer(); ArtKit.Chimney(26, 4, 12, 34, Br); EndLayer(); Save("bld_chimney");
            foreach (string id in SIGNS) { Begin(); SignShadow(); Layer(); SignBase(); DrawSign(id); EndLayer(); Save(SignName(id)); }
            File.WriteAllBytes(OUTPUT + "buildings_contact_sheet.png", Sheet(sheet, 10));
            WriteTypeSheet(pixels);
            AssetDatabase.Refresh();
            foreach (string name in names) ConfigureImporter(OUTPUT + name + ".png");
            ConfigureImporter(OUTPUT + "buildings_contact_sheet.png");
            global::IronRing.Presentation.Interior.Editor.Story012Build.EnsureBuildingAssets();
            Debug.Log($"SpriteGenBuildings: {names.Count}개 스프라이트 생성");
        }

        private static Swatch RoofSwatch(string style) => style == "machine" ? Fe : style == "storage" ? G : style == "glass" ? Wa : R;
        private static Swatch CapSwatch(string style) => style == "machine" ? Bs : style == "storage" ? Fe : style == "glass" ? Cu : Wd;

        /// <summary>mask(1=N,2=E,4=S,8=W 이웃 있음)에 맞는 지붕 한 칸. 남쪽이 닫히면 아래 16픽셀은 정면 벽.</summary>
        private static void DrawRoof(string style, int mask)
        {
            if (style == "yard") { DrawYard(mask); return; }
            bool n = (mask & 1) != 0, e = (mask & 2) != 0, s = (mask & 4) != 0, w = (mask & 8) != 0;
            Swatch roof = RoofSwatch(style), cap = CapSwatch(style);
            int y0 = s ? 0 : 16;
            if (!s) DrawWall(style, mask);
            for (int y = y0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                    C.Set(x, y, RoofPixel(style, roof, x, y));
            if (!n) { C.Rect(0, SIZE - 5, SIZE, 4, cap.B); C.Rect(0, SIZE - 2, SIZE, 1, cap.L); C.Rect(0, SIZE - 5, SIZE, 1, cap.S); C.Rect(0, SIZE - 1, SIZE, 1, O.B); }
            if (!e) { C.Rect(SIZE - 5, y0, 4, SIZE - y0, cap.B); C.Rect(SIZE - 5, y0, 1, SIZE - y0, cap.L); C.Rect(SIZE - 1, 0, 1, SIZE, O.B); }
            if (!w) { C.Rect(1, y0, 4, SIZE - y0, cap.B); C.Rect(4, y0, 1, SIZE - y0, cap.S); C.Rect(0, 0, 1, SIZE, O.B); }
            if (!s) { C.Rect(0, y0, SIZE, 3, cap.S); C.Rect(0, y0 + 2, SIZE, 1, cap.L); C.Rect(0, y0 - 1, SIZE, 1, O.B); C.Rect(0, y0 - 2, SIZE, 1, A(O.S, 150)); C.Rect(0, 0, SIZE, 1, O.B); }
            // 029: 바깥 모서리 황동 덮개(두 닫힌 변이 만나는 곳)
            if (!n && !e) CornerCap(SIZE - 7, SIZE - 7);
            if (!n && !w) CornerCap(1, SIZE - 7);
            if (!s && !e) CornerCap(SIZE - 7, y0);
            if (!s && !w) CornerCap(1, y0);
            // 029: 북서 바깥 모서리 조각(굴뚝은 북동, 상징물은 가운데)에 지붕 장식 하나
            if (!n && !w && (e || s)) { Layer(); DrawOrnament(H(mask, System.Array.IndexOf(STYLES, style), 17) % 3); EndLayer(); }
        }

        /// <summary>mask의 열린 변은 이어지고 닫힌 변은 목재 울타리로 둘러싼 흙 마당을 그린다.</summary>
        private static void DrawYard(int mask)
        {
            C.Rect(0, 0, SIZE, SIZE, Wd.S);
            for (int i = 0; i < 90; i++) C.Disc(H(i, 1, 91) % SIZE, H(i, 2, 91) % SIZE, 1 + H(i, 3, 91) % 2, i % 5 == 0 ? Sa.L : Wd.B);
            if ((mask & 1) == 0) { C.Rect(0, 58, SIZE, 4, Wd.B); C.Rect(0, 61, SIZE, 1, Wd.L); }
            if ((mask & 2) == 0) { C.Rect(59, 0, 4, SIZE, Wd.B); C.Rect(59, 0, 1, SIZE, Wd.L); }
            if ((mask & 4) == 0) { C.Rect(0, 2, SIZE, 4, Wd.B); C.Rect(0, 5, SIZE, 1, Wd.L); }
            if ((mask & 8) == 0) { C.Rect(1, 0, 4, SIZE, Wd.B); C.Rect(4, 0, 1, SIZE, Wd.L); }
            foreach (int x in new[] { 6, 29, 53 })
            {
                if ((mask & 1) == 0) Box(x, 55, 5, 8, Wd);
                if ((mask & 4) == 0) Box(x, 1, 5, 8, Wd);
            }
        }

        /// <summary>029: 6×6 황동 모서리 덮개와 리벳.</summary>
        private static void CornerCap(int x, int y)
        {
            Box(x, y, 6, 6, Bs);
            C.Set(x + 2, y + 3, Bs.S); C.Set(x + 3, y + 2, O.B);
        }

        /// <summary>029: 지붕 장식 0 환풍기, 1 안테나, 2 작은 물탱크(북서 칸 왼쪽 위). 종류는 style·mask 시드.</summary>
        private static void DrawOrnament(int kind)
        {
            switch (kind)
            {
                case 0:
                    Box(10, 38, 12, 4, Fe);
                    Dome(16, 46, 6, Fe);
                    for (int x = 12; x <= 20; x += 2) C.Rect(x, 44, 1, 4, O.S);
                    C.Set(16, 52, Bs.L);
                    break;
                case 1:
                    Box(12, 37, 8, 3, Fe);
                    C.Rect(15, 40, 2, 17, Fe.B); C.Rect(15, 40, 1, 17, Fe.L);
                    C.Rect(11, 50, 10, 1, Cu.B); C.Rect(12, 54, 8, 1, Cu.B);
                    C.Disc(16, 58, 1.6f, Gl.B); C.Set(15, 58, Gl.L);
                    break;
                default:
                    C.Rect(10, 36, 2, 5, Fe.S); C.Rect(21, 36, 2, 5, Fe.S);
                    CylV(9, 40, 15, 11, Cu);
                    C.Rect(9, 43, 15, 1, Bs.S); C.Rect(9, 48, 15, 1, Bs.S);
                    C.Ellipse(16.5f, 51, 7.5f, 2, Cu.L); C.Set(16, 51, O.B);
                    break;
            }
        }

        private static Color32 RoofPixel(string style, Swatch r, int x, int y)
        {
            int hsh = H(x, y, 77);
            switch (style)
            {
                case "machine": // 029: 리벳 박은 철판. 16픽셀 판, 세로 이음 돌기(밝음+그늘), 가로 이음에 황동 리벳, 판마다 명암 차
                {
                    int px = x % 16, py = y % 16;
                    if (px == 0) return r.L;
                    if (px == 1) return r.S;
                    if (py == 0) return r.S;
                    if (py == 1) return (px % 4 == 3) ? Bs.S : r.L;
                    if (py == 14 && px % 4 == 3) return Bs.S;
                    bool lightPanel = H(x / 16, y / 16, 5) % 3 == 0;
                    Color32 c = lightPanel ? Mix(r.B, r.L, 0.35f) : r.B;
                    return hsh % 41 == 0 ? r.S : c;
                }
                case "storage": // 029: 골함석. 3픽셀 이랑(밝음·기본·그늘), 21줄마다 판 겹침, 녹 얼룩
                {
                    int py = y % 21;
                    if (py == 0) return O.S;
                    if (py == 1) return r.L;
                    Color32 c = x % 3 == 0 ? r.L : x % 3 == 1 ? r.B : r.S;
                    if (H(x / 5, y / 4, 13) % 11 == 0) c = Mix(c, Cu.S, 0.55f);
                    return c;
                }
                case "glass": // 029: 유리 격자. 12×16 쇠 창살, 대각 반사 띠, 일부 칸은 안쪽 불빛
                {
                    int px = x % 12, py = y % 16;
                    if (px == 0 || py == 0) return Fe.B;
                    if (px == 1 || py == 15) return Fe.L;
                    int d = (x + y) % 24;
                    if (d == 0 || d == 1) return Wa.L;
                    if (d == 3) return Mix(Wa.L, Wa.B, 0.5f);
                    bool lit = H(x / 12, y / 16, 9) % 5 == 0;
                    Color32 c = py < 5 ? Wa.S : Wa.B;
                    return lit ? Mix(c, Gl.S, 0.35f) : c;
                }
                default: // 029: 기와 줄. 6줄 단, 8픽셀 기와 엇갈림, 아랫단 둥근 끝, 이끼 점
                {
                    int row = y / 6, ly = y % 6, lx = (x + (row % 2) * 4) % 8;
                    if (ly == 0) return r.S;
                    if (lx == 0) return ly == 5 ? r.B : r.S;
                    if (ly == 1 && (lx == 1 || lx == 7)) return r.S;
                    if (ly == 5) return r.L;
                    if (hsh % 53 == 0) return Ms.S;
                    return lx < 3 && ly > 2 ? Mix(r.B, r.L, 0.4f) : r.B;
                }
            }
        }

        /// <summary>정면 벽(아래 16픽셀)과 양쪽 창.</summary>
        private static void DrawWall(string style, int mask)
        {
            if (style == "housing") Bricks(0, 0, SIZE, 16, Br);
            else if (style == "storage") { C.Rect(0, 0, SIZE, 16, Wd.S); for (int x = 0; x < SIZE; x += 6) C.Rect(x, 0, 1, 16, O.S); }
            else { C.Rect(0, 0, SIZE, 16, Fe.S); C.Rect(0, 14, SIZE, 1, Fe.L); RivetsH(3, SIZE - 3, 3, 6, Bs.S); }
            WallWindows(style, mask);
        }

        /// <summary>029: 벽 띠의 호박색 창 1~2개(남쪽 이웃이 빈 조각만 벽이 있음). 문 자리(가운데)는 비운다. 개수·위치는 style·mask 시드.</summary>
        private static void WallWindows(string style, int mask)
        {
            int pick = H(mask, System.Array.IndexOf(STYLES, style), 23) % 3;
            if (pick != 1) Window(8, 4, 8, 7, true);
            if (pick != 0) Window(48, 4, 8, 7, true);
            C.Rect(0, 1, SIZE, 1, A(O.S, 120));
        }

        /// <summary>오목 모서리(두 이웃은 있고 대각만 빔)를 덮는 작은 처마 조각. corner: 0 ne, 1 se, 2 sw, 3 nw.</summary>
        private static void DrawCorner(string style, int corner)
        {
            Swatch cap = CapSwatch(style);
            bool east = corner == 0 || corner == 1, north = corner == 0 || corner == 3;
            int x = east ? SIZE - 5 : 1;
            int y = north ? SIZE - 5 : 0;
            C.Rect(x, y, 4, 5, cap.B);
            C.Rect(x, y, 4, 1, cap.S);
            C.Set(east ? SIZE - 1 : 0, north ? SIZE - 1 : 0, O.B);
        }

        /// <summary>문: 0 = 아치 나무문(돌 문틀), 1 = 리벳 쇠문(황동 문틀). 칸 아래 가운데, 돌계단 2단 위(029).</summary>
        private static void DrawDoor(int variant)
        {
            Swatch body = variant == 0 ? Wd : Fe, frame = variant == 0 ? Sa : Bs;
            Box(18, 0, 28, 2, Sa); Box(20, 2, 24, 2, Sa);
            C.Rect(20, 4, 24, 15, frame.B); C.Ellipse(32, 19, 12, 6, frame.B);
            C.Rect(20, 4, 1, 15, frame.L); C.Rect(43, 4, 1, 15, frame.S);
            C.Rect(23, 4, 18, 15, body.B);
            C.Ellipse(32, 19, 9, 4, body.B);
            for (int x = 26; x < 40; x += 4) C.Rect(x, 4, 1, 18, body.S);
            C.Rect(23, 4, 1, 15, body.L);
            if (variant == 1) { RivetsH(25, 39, 8, 3, Bs.L); RivetsH(25, 39, 16, 3, Bs.L); }
            C.Rect(36, 10, 2, 2, Bs.L);
            C.Set(32, 24, frame.L); C.Rect(31, 23, 3, 1, frame.S);
        }

        /// <summary>029: 상징물 아래 반투명 그림자(외곽선 레이어 밖).</summary>
        private static void SignShadow() => C.Ellipse(34, 6, 23, 5, A(O.S, 110));

        /// <summary>029: 상징물 받침(쇠판 + 황동 리벳).</summary>
        private static void SignBase()
        {
            Box(18, 3, 28, 5, Fe);
            C.Rect(18, 7, 28, 1, Fe.L);
            RivetsH(20, 44, 5, 6, Bs.S);
        }

        /// <summary>서식지 종류 상징물(지붕 위 가운데).</summary>
        private static void DrawSign(string id)
        {
            switch (id)
            {
                case "lamp_post": LampPost(32, 8, 44, true); break;
                case "twin_lamps": LampPost(21, 10, 34, true); LampPost(43, 10, 34, true); break;
                case "coal_heap":
                    C.Ellipse(32, 22, 20, 11, Fe.S);
                    for (int i = 0; i < 14; i++) C.Disc(16 + H(i, 1, 5) % 32, 16 + H(i, 2, 5) % 14, 3, i % 3 == 0 ? Fe.B : O.S);
                    break;
                case "cobble_arch": Box(12, 10, 10, 30, Sa); Box(42, 10, 10, 30, Sa); Box(10, 38, 44, 10, Sa); C.Rect(22, 10, 20, 2, Mo.S); break;
                case "earth_marker":
                    C.Ellipse(32, 14, 23, 8, Wd.S);
                    for (int i = 0; i < 10; i++) C.Disc(15 + H(i, 2, 41) % 34, 13 + H(i, 3, 41) % 9, 2, Sa.B);
                    C.Rect(29, 17, 5, 34, Wd.B); C.Rect(29, 17, 1, 34, Wd.L);
                    C.Rect(34, 36, 16, 12, Fa.B); C.Rect(34, 46, 16, 2, Fa.L);
                    C.Disc(20, 27, 5, Bs.B); C.Line(20, 27, 28, 39, Wd.L, 2);
                    break;
                case "big_gear": Gear(32, 32, 22, 12, Bs); break;
                case "small_gear": Gear(25, 28, 13, 9, Fe); Gear(43, 41, 9, 7, Bs); break;
                case "boiler_dome": Dome(32, 26, 17, Cu); RivetRing(32, 26, 12, 10, Bs.S); break;
                case "pipe_valve": PipeH(6, 28, 52, 8, Cu); C.Disc(32, 32, 9, Fe.B); C.Ring(32, 32, 5, 8, Bs.B); C.Disc(32, 32, 2, O.B); break;
                case "girder":
                    C.Rect(10, 18, 44, 5, Fe.B); C.Rect(10, 42, 44, 5, Fe.B);
                    for (int x = 10; x < 54; x += 11) { C.Line(x, 22, x + 11, 42, Fe.L, 2); C.Line(x + 11, 22, x, 42, Fe.S, 2); }
                    RivetsH(12, 52, 20, 5, Bs.S); RivetsH(12, 52, 44, 5, Bs.S);
                    break;
                case "tank": Barrel(32, 30, 16, true); break;
                case "water_tower": C.Rect(19, 6, 3, 22, Fe.B); C.Rect(42, 6, 3, 22, Fe.B); C.Line(21, 8, 42, 26, Fe.S); Barrel(32, 38, 14, true); break;
                case "glass_roof":
                    Box(12, 14, 40, 30, Fe);
                    for (int i = 0; i < 3; i++) { C.Rect(15 + i * 12, 17, 10, 24, Wa.B); C.Line(16 + i * 12, 19, 22 + i * 12, 38, Wa.L); }
                    Shrub(32, 46, 6, 3);
                    break;
                case "bunk": Box(14, 14, 36, 30, Wd); C.Rect(17, 30, 30, 10, Fa.B); C.Rect(17, 17, 30, 10, Fa.S); C.Rect(17, 38, 9, 3, Fa.L); C.Rect(17, 25, 9, 3, Fa.L); break;
                case "bench_tools": Box(10, 16, 44, 12, Wd); Crate(14, 28, 14, 12); Gear(43, 36, 7, 6, Fe); C.Line(30, 30, 38, 40, Fe.L, 2); break;
                case "wire_pole":
                    C.Rect(30, 6, 4, 46, Wd.B); C.Rect(30, 6, 1, 46, Wd.L); C.Rect(18, 46, 28, 3, Wd.S);
                    C.Line(2, 42, 19, 47, Cu.B); C.Line(45, 47, 62, 42, Cu.B); C.Disc(20, 49, 2, Gl.B); C.Disc(44, 49, 2, Gl.B);
                    break;
                case "engine": CylH(8, 20, 34, 20, Fe); RivetsV(14, 22, 38, 4, Bs.S); Gear(47, 34, 10, 8, Bs); break;
                case "dynamo":
                    Gear(24, 30, 12, 8, Fe); CylV(38, 14, 14, 30, Cu);
                    C.Line(48, 56, 41, 46, Gl.L, 2); C.Line(41, 46, 49, 44, Gl.L, 2); C.Line(49, 44, 42, 34, Gl.L, 2);
                    break;
                case "crane": C.Rect(14, 6, 4, 46, Fe.B); C.Rect(14, 48, 38, 4, Fe.B); C.Line(18, 40, 30, 50, Fe.S, 2); C.Line(48, 48, 48, 30, O.B); Crate(41, 18, 14, 12); break;
                case "chimney_stack": ArtKit.Chimney(24, 6, 16, 44, Br); Glow(32, 10, 9); break;
                default: Box(16, 16, 32, 32, Fe); RivetRing(32, 32, 12, 10, Bs.S); break;
            }
        }
    
        /// <summary>QA 후속. 색조 tint 쪽으로 명도를 보존해 옮긴다(외곽선·황동·불빛·반투명 그림자는 그대로).</summary>
        private static void Tint(string tint)
        {
            Swatch t = tint == "copper" ? Cu : tint == "wine" ? W : Ms;
            var keep = new HashSet<int> { Key(O.B), Key(O.S), Key(Gl.L), Key(Gl.B), Key(Gl.S), Key(Bs.L), Key(Bs.B), Key(Bs.S) };
            for (int i = 0; i < C.Px.Length; i++)
            {
                Color32 c = C.Px[i];
                if (c.a < 200 || keep.Contains(Key(c))) continue;
                float lum = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                Color32 shade = lum < 0.22f ? t.S : lum < 0.36f ? t.B : t.L;
                Color32 m = Mix(c, shade, 0.6f);
                m.a = c.a;
                C.Px[i] = m;
            }
        }

        private static int Key(Color32 c) => (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;

        /// <summary>QA 후속. 정면 벽 띠(아래 16픽셀)의 창 무늬 덧조각. 1 = 나무 덧창 + 꽃상자, 2 = 황동 테 둥근 현창. 기본 창(x 8·48)을 덮는다.</summary>
        private static void DrawWindowPattern(int pattern)
        {
            foreach (int cx in new[] { 12, 52 })
            {
                if (pattern == 1)
                {
                    C.Rect(cx - 9, 3, 3, 11, Wd.B); C.Rect(cx - 9, 3, 1, 11, Wd.L); C.Rect(cx + 6, 3, 3, 11, Wd.B); C.Rect(cx + 8, 3, 1, 11, Wd.S);
                    for (int y = 5; y < 14; y += 3) { C.Rect(cx - 8, y, 2, 1, Wd.S); C.Rect(cx + 6, y, 2, 1, Wd.S); }
                    C.Rect(cx - 5, 4, 10, 10, Gl.B); C.Rect(cx - 5, 11, 10, 3, Gl.L); C.Rect(cx - 5, 4, 10, 1, Gl.S);
                    C.Rect(cx, 4, 1, 10, Wd.S); C.Rect(cx - 5, 8, 10, 1, Wd.S);
                    Box(cx - 7, 1, 14, 3, Wd);
                    for (int x = cx - 6; x <= cx + 5; x += 2) C.Set(x, 4, (x / 2) % 2 == 0 ? Sh.L : Em.L);
                }
                else
                {
                    C.Disc(cx, 8, 6, Bs.S); C.Disc(cx, 8, 5, Bs.B);
                    C.Disc(cx, 8, 3.6f, Gl.B); C.Disc(cx - 1, 9, 1.6f, Gl.L);
                    C.Set(cx, 14, O.B); C.Set(cx, 2, O.B); C.Set(cx - 6, 8, O.B); C.Set(cx + 6, 8, O.B);
                }
            }
        }

        /// <summary>QA 후속. 지붕 덧소품(64픽셀, 표시 때 InteriorCatalog._buildingDecorScale로 줄인다).</summary>
        private static void DrawDecor(string id)
        {
            switch (id)
            {
                case "cog_sign": // 톱니 간판: 쇠 기둥 + 팔 + 매달린 나무판에 황동 톱니
                    C.Rect(12, 4, 4, 50, Fe.B); C.Rect(12, 4, 1, 50, Fe.L); C.Rect(12, 50, 36, 4, Fe.B); C.Rect(12, 53, 36, 1, Fe.L);
                    C.Line(26, 50, 26, 42, O.B); C.Line(46, 50, 46, 42, O.B);
                    Box(20, 20, 32, 22, Wd); Gear(36, 31, 8, 8, Bs); C.Disc(36, 31, 2, O.B);
                    break;
                case "steam_pipes": // 증기 배관: 구리 가로관 + 세로관 + 밸브 바퀴 + 김
                    PipeH(4, 12, 56, 7, Cu); CylV(10, 12, 8, 30, Cu); C.Rect(9, 40, 10, 3, Bs.B);
                    C.Ring(44, 15, 4, 6, Bs.B); C.Disc(44, 15, 1.5f, O.B); C.Rect(28, 10, 3, 11, Bs.S); C.Rect(50, 10, 3, 11, Bs.S);
                    Puff(14, 50, 6, 200); Puff(19, 57, 4, 160);
                    break;
                case "twin_chimney": // 굴뚝 2개: 벽돌·쇠, 높이 다름, 연기
                    ArtKit.Chimney(12, 4, 14, 40, Br); ArtKit.Chimney(36, 4, 12, 52, Fe);
                    Puff(19, 50, 5, 190); Puff(42, 59, 4, 170);
                    break;
                case "water_tank": // 물탱크: 쇠 다리 위 나무통 + 황동 띠 + 구리 고깔
                    C.Rect(18, 4, 3, 16, Fe.B); C.Rect(43, 4, 3, 16, Fe.B); C.Line(20, 6, 43, 18, Fe.S); C.Line(43, 6, 20, 18, Fe.S);
                    CylV(15, 18, 34, 26, Wd); C.Rect(15, 24, 34, 2, Bs.S); C.Rect(15, 36, 34, 2, Bs.S);
                    C.Ellipse(32, 46, 17, 4, Cu.B); C.Ellipse(32, 49, 11, 3, Cu.L); C.Disc(32, 53, 2, Cu.S);
                    break;
                case "glass_cap": // 온실 유리 지붕: 쇠 틀 유리 돔 + 안쪽 초록
                    Box(10, 6, 44, 8, Fe); Dome(32, 14, 20, Wa);
                    Shrub(26, 20, 5, 7); Shrub(38, 19, 4, 9);
                    C.Line(32, 14, 32, 34, Fe.B); C.Line(32, 14, 16, 26, Fe.B); C.Line(32, 14, 48, 26, Fe.B);
                    C.Line(20, 28, 26, 32, Wa.L); C.Disc(32, 35, 2, Bs.B);
                    break;
                case "gas_lamp": // 가스등 기둥: 낮은 받침 + 호박색 등 + 광륜
                    Box(24, 4, 16, 5, Sa); LampPost(32, 8, 42, true);
                    break;
                case "tele_pole": // 전신주: 나무 기둥 + 가로대 2개 + 애자 + 처진 전선
                    C.Rect(30, 2, 5, 56, Wd.B); C.Rect(30, 2, 1, 56, Wd.L); C.Rect(34, 2, 1, 56, Wd.S);
                    C.Rect(16, 50, 33, 3, Wd.S); C.Rect(20, 42, 25, 3, Wd.S);
                    foreach (int x in new[] { 18, 26, 39, 47 }) C.Disc(x, 54, 1.6f, Gl.S);
                    C.Line(0, 50, 18, 54, O.B); C.Line(47, 54, 63, 50, O.B); C.Line(0, 40, 21, 45, O.B); C.Line(44, 45, 63, 40, O.B);
                    break;
                case "coal_pile": // 석탄 수레: 쇠 수레 + 바퀴 + 위로 쌓인 석탄 + 삽
                    Box(10, 8, 44, 14, Fe); RivetsH(13, 51, 18, 6, Bs.S);
                    C.Disc(18, 7, 4, O.B); C.Disc(46, 7, 4, O.B); C.Disc(18, 7, 1.5f, Fe.L); C.Disc(46, 7, 1.5f, Fe.L);
                    C.Ellipse(32, 24, 19, 8, O.S);
                    for (int i = 0; i < 12; i++) C.Disc(17 + H(i, 3, 11) % 30, 22 + H(i, 4, 11) % 8, 2.5f, i % 3 == 0 ? Fe.B : Fe.S);
                    C.Line(48, 22, 56, 44, Wd.B, 2); Box(53, 42, 7, 6, Fe);
                    break;
                case "iron_frame": // 철골 골조: 기둥 2 + 들보 3 + X 가새 + 리벳
                    C.Rect(10, 4, 4, 52, Fe.B); C.Rect(50, 4, 4, 52, Fe.B); C.Rect(10, 4, 1, 52, Fe.L); C.Rect(50, 4, 1, 52, Fe.L);
                    foreach (int y in new[] { 4, 26, 50 }) { C.Rect(10, y, 44, 4, Fe.B); C.Rect(10, y + 3, 44, 1, Fe.L); RivetsH(12, 52, y + 1, 5, Bs.S); }
                    C.Line(14, 8, 50, 26, Fe.S, 2); C.Line(50, 8, 14, 26, Fe.S, 2); C.Line(14, 30, 50, 50, Fe.S, 2);
                    break;
                case "laundry": // 빨래줄과 깃발: 나무 기둥 둘, 처진 줄, 천 조각, 구리 깃발
                    C.Rect(6, 4, 3, 46, Wd.B); C.Rect(55, 4, 3, 54, Wd.B);
                    C.Line(8, 46, 32, 42, O.B); C.Line(32, 42, 56, 46, O.B);
                    C.Rect(12, 34, 8, 11, Fa.B); C.Rect(12, 34, 8, 2, Fa.S); C.Rect(24, 32, 7, 10, Sm.B); C.Rect(35, 33, 9, 9, Cu.L); C.Rect(47, 36, 6, 9, Fa.L);
                    for (int y = 0; y < 8; y++) C.Rect(58, 50 + y, 6 - y * 3 / 4, 1, y < 4 ? Cu.B : Cu.L);
                    break;
                default: Box(20, 10, 24, 24, Fe); break;
            }
        }

        /// <summary>
        /// QA 후속. HabitatTypes.json 19종을 3칸 가로 건물 견본(왼쪽 덧소품 1, 가운데 문·상징물, 오른쪽 굴뚝; 문 칸을 뺀 양 끝 칸에 창 무늬)으로
        /// 데이터 순서대로 4열 시트에 늘어놓는다. BuildingView 배치 규칙(상징물 0.8배·SignLift 0.1, 덧소품 0.6배·DecorLift 0.08, 굴뚝 (0.28, 0.2))을 따른다.
        /// 견본 아래 띠 색은 지붕 계열(machine 쇠·housing 벽돌·storage 나무·glass 물)이다.
        /// </summary>
        private static void WriteTypeSheet(Dictionary<string, Color32[]> px)
        {
            var catalog = global::IronRing.Habitats.HabitatCatalog.Parse(File.ReadAllText(HABITAT_JSON));
            var types = new List<global::IronRing.Habitats.HabitatType>(catalog.Types);
            const int perRow = 4, cw = SIZE * 3 + 16, ch = SIZE + 24;
            int rows = (types.Count + perRow - 1) / perRow, w = cw * perRow, h = ch * rows;
            var buf = new Color32[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = Bg.B;
            for (int t = 0; t < types.Count; t++)
            {
                global::IronRing.Habitats.HabitatType type = types[t];
                string style = type.BuildingStyle ?? STYLES[0];
                global::IronRing.Habitats.HabitatDecor decor = type.Decor ?? global::IronRing.Habitats.HabitatDecor.DefaultFor(style);
                int ox = (t % perRow) * cw + 8, oy = (rows - 1 - t / perRow) * ch + 14;
                Color32 band = style == "machine" ? Fe.B : style == "storage" ? Wd.B : style == "glass" ? Wa.B : Br.B;
                for (int y = 0; y < 4; y++) for (int x = 0; x < SIZE * 3; x++) buf[(oy - 9 + y) * w + ox + x] = band;
                int[] masks = { 2, 10, 8 };
                for (int c = 0; c < 3; c++)
                    Blit(buf, w, h, Get(px, RoofName(style, decor.RoofTint, masks[c])) ?? Get(px, RoofName(style, masks[c])), ox + c * SIZE + 32, oy + 32, 1f);
                Color32[] window = decor.WindowPattern > 0 ? Get(px, WindowName(decor.WindowPattern)) : null;
                if (window != null) { Blit(buf, w, h, window, ox + 32, oy + 32, 1f); Blit(buf, w, h, window, ox + 2 * SIZE + 32, oy + 32, 1f); }
                int door = 0;
                foreach (char chr in type.Id) door += chr;
                Blit(buf, w, h, Get(px, "bld_door_" + (door % 2)), ox + SIZE + 32, oy + 32, 1f);
                Blit(buf, w, h, Get(px, "bld_chimney"), ox + 2 * SIZE + 32 + 18, oy + 32 + 13, 1f);
                Blit(buf, w, h, Get(px, SignName(type.SignProp)), ox + SIZE + 32, oy + 32 + 6, 0.8f);
                if (decor.Props.Count > 0) Blit(buf, w, h, Get(px, DecorName(decor.Props[0])), ox + 32, oy + 32 + 5, 0.6f);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(buf);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(EVIDENCE_SHEET));
            File.WriteAllBytes(EVIDENCE_SHEET, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static Color32[] Get(Dictionary<string, Color32[]> px, string name) => name != null && px.TryGetValue(name, out Color32[] v) ? v : null;

        /// <summary>QA 후속. 64×64 src를 (cx, cy) 중심으로 scale배 최근접 표본화해 알파 합성한다.</summary>
        private static void Blit(Color32[] dst, int w, int h, Color32[] src, int cx, int cy, float scale)
        {
            if (src == null) return;
            int half = Mathf.CeilToInt(SIZE * scale / 2f);
            for (int y = -half; y < half; y++)
                for (int x = -half; x < half; x++)
                {
                    int sx = Mathf.FloorToInt((x + 0.5f) / scale + SIZE / 2f), sy = Mathf.FloorToInt((y + 0.5f) / scale + SIZE / 2f);
                    int dx = cx + x, dy = cy + y;
                    if (sx < 0 || sy < 0 || sx >= SIZE || sy >= SIZE || dx < 0 || dy < 0 || dx >= w || dy >= h) continue;
                    dst[dy * w + dx] = PixelCanvas.Over(src[sy * SIZE + sx], dst[dy * w + dx]);
                }
        }
}
}
