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
    /// </summary>
    public static class SpriteGenBuildings
    {
        /// <summary>출력 폴더.</summary>
        public const string OUTPUT = "Assets/Art/Generated/buildings/";
        /// <summary>지붕 계열 id(HabitatTypes.json의 _buildingStyle).</summary>
        public static readonly string[] STYLES = { "machine", "housing", "storage", "glass" };
        /// <summary>오목 모서리 덧조각 순서.</summary>
        public static readonly string[] CORNERS = { "ne", "se", "sw", "nw" };
        /// <summary>상징물 id(HabitatTypes.json의 _signProp).</summary>
        public static readonly string[] SIGNS =
        {
            "lamp_post", "coal_heap", "cobble_arch", "big_gear", "boiler_dome", "pipe_valve", "small_gear", "girder", "tank", "glass_roof",
            "bunk", "bench_tools", "wire_pole", "water_tower", "twin_lamps", "engine", "dynamo", "crane", "chimney_stack"
        };

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
            void Save(string name) { C.Save(OUTPUT + name + ".png"); names.Add(name); sheet.Add(C.Px); }
            foreach (string style in STYLES)
            {
                for (int mask = 0; mask < 16; mask++) { Begin(); DrawRoof(style, mask); Save(RoofName(style, mask)); }
                for (int i = 0; i < CORNERS.Length; i++) { Begin(); DrawCorner(style, i); Save(CornerName(style, CORNERS[i])); }
            }
            for (int i = 0; i < 2; i++) { Begin(); Layer(); DrawDoor(i); EndLayer(); Save("bld_door_" + i); }
            Begin(); Layer(); ArtKit.Chimney(26, 4, 12, 34, Br); EndLayer(); Save("bld_chimney");
            foreach (string id in SIGNS) { Begin(); Layer(); DrawSign(id); EndLayer(); Save(SignName(id)); }
            File.WriteAllBytes(OUTPUT + "buildings_contact_sheet.png", Sheet(sheet, 10));
            AssetDatabase.Refresh();
            foreach (string name in names) ConfigureImporter(OUTPUT + name + ".png");
            ConfigureImporter(OUTPUT + "buildings_contact_sheet.png");
            global::IronRing.Presentation.Interior.Editor.Story012Build.EnsureBuildingAssets();
            Debug.Log($"SpriteGenBuildings: {names.Count}개 스프라이트 생성");
        }

        private static Swatch RoofSwatch(string style) => style == "machine" ? Fe : style == "storage" ? Wd : style == "glass" ? Wa : R;
        private static Swatch CapSwatch(string style) => style == "machine" ? Bs : style == "storage" ? Fe : style == "glass" ? Cu : Wd;

        /// <summary>mask(1=N,2=E,4=S,8=W 이웃 있음)에 맞는 지붕 한 칸. 남쪽이 닫히면 아래 16픽셀은 정면 벽.</summary>
        private static void DrawRoof(string style, int mask)
        {
            bool n = (mask & 1) != 0, e = (mask & 2) != 0, s = (mask & 4) != 0, w = (mask & 8) != 0;
            Swatch roof = RoofSwatch(style), cap = CapSwatch(style);
            int y0 = s ? 0 : 16;
            if (!s) DrawWall(style);
            for (int y = y0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                    C.Set(x, y, RoofPixel(style, roof, x, y));
            if (!n) { C.Rect(0, SIZE - 5, SIZE, 4, cap.B); C.Rect(0, SIZE - 2, SIZE, 1, cap.L); C.Rect(0, SIZE - 5, SIZE, 1, cap.S); C.Rect(0, SIZE - 1, SIZE, 1, O.B); }
            if (!e) { C.Rect(SIZE - 5, y0, 4, SIZE - y0, cap.B); C.Rect(SIZE - 5, y0, 1, SIZE - y0, cap.L); C.Rect(SIZE - 1, 0, 1, SIZE, O.B); }
            if (!w) { C.Rect(1, y0, 4, SIZE - y0, cap.B); C.Rect(4, y0, 1, SIZE - y0, cap.S); C.Rect(0, 0, 1, SIZE, O.B); }
            if (!s) { C.Rect(0, y0, SIZE, 3, cap.S); C.Rect(0, y0 + 2, SIZE, 1, cap.L); C.Rect(0, y0 - 1, SIZE, 1, O.B); C.Rect(0, 0, SIZE, 1, O.B); }
        }

        private static Color32 RoofPixel(string style, Swatch r, int x, int y)
        {
            switch (style)
            {
                case "machine": // 골함석: 4픽셀 세로 골, 16줄마다 리벳 띠
                    if (y % 16 == 8) return (x % 8 == 3) ? Bs.L : r.S;
                    return x % 4 == 0 ? r.L : x % 4 == 2 ? r.S : r.B;
                case "storage": // 널빤지: 8줄마다 이음, 못 자국
                    if (y % 8 == 0) return r.S;
                    if (y % 8 == 4 && (x + (y / 8) * 13) % 21 == 0) return Fe.S;
                    return (x + (y / 8) * 11) % 32 == 0 ? r.S : (y % 8 == 7 ? r.L : r.B);
                case "glass": // 유리판: 16픽셀 쇠 틀, 대각 반사
                    if (x % 16 == 0 || y % 16 == 0) return Fe.B;
                    if (x % 16 == 1 || y % 16 == 15) return Fe.L;
                    int d = (x % 16) - (y % 16);
                    return d == 4 || d == 5 ? Wa.L : (y % 16 < 5 ? Wa.S : Wa.B);
                default: // 비늘 기와: 4줄마다 단, 엇갈린 이음
                    if (y % 4 == 0) return r.S;
                    if (((x + ((y / 4) % 2) * 4) % 8) == 0) return r.S;
                    return y % 4 == 3 ? r.L : r.B;
            }
        }

        /// <summary>정면 벽(아래 16픽셀)과 양쪽 창.</summary>
        private static void DrawWall(string style)
        {
            if (style == "housing") Bricks(0, 0, SIZE, 16, Br);
            else if (style == "storage") { C.Rect(0, 0, SIZE, 16, Wd.S); for (int x = 0; x < SIZE; x += 6) C.Rect(x, 0, 1, 16, O.S); }
            else { C.Rect(0, 0, SIZE, 16, Fe.S); C.Rect(0, 14, SIZE, 1, Fe.L); RivetsH(3, SIZE - 3, 3, 6, Bs.S); }
            Window(8, 4, 8, 7, true);
            Window(48, 4, 8, 7, true);
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

        /// <summary>문: 0 = 아치 나무문, 1 = 리벳 쇠문. 칸 아래 가운데.</summary>
        private static void DrawDoor(int variant)
        {
            Swatch body = variant == 0 ? Wd : Fe;
            C.Rect(23, 0, 18, 15, body.B);
            C.Ellipse(32, 15, 9, 4, body.B);
            for (int x = 26; x < 40; x += 4) C.Rect(x, 0, 1, 16, body.S);
            C.Rect(23, 0, 1, 15, body.L);
            if (variant == 1) { RivetsH(25, 39, 4, 3, Bs.L); RivetsH(25, 39, 12, 3, Bs.L); }
            C.Rect(36, 7, 2, 2, Bs.L);
            C.Rect(21, 0, 22, 1, Mo.S);
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
    }
}
