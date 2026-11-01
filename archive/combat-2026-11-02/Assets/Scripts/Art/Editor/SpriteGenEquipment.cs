using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 034. 열차 장비 탑뷰 도트(64×64, PPU 64, 진행 방향 = 오른쪽): 포탑, 중포(가로 2칸: 그림이 가로 64·세로 32픽셀),
    /// 방벽판, 증기 부스터, 수리 장치, 그리드 칸 틀. 출력 Assets/Art/Generated/equipment/*.png + equipment_contact_sheet.png.
    /// </summary>
    public static class SpriteGenEquipment
    {
        /// <summary>출력 폴더.</summary>
        public const string OUTPUT = "Assets/Art/Generated/equipment/";
        /// <summary>장비 id(TrainEquipment.json과 같음).</summary>
        public static readonly string[] IDS = { "turret", "heavy_turret", "armor_plate", "steam_booster", "repair_rig" };
        /// <summary>칸 틀 스프라이트 이름.</summary>
        public const string CELL_FRAME = "equipment_cell";

        /// <summary>장비 5종과 칸 틀 PNG를 만들고 임포터를 설정한다(같은 입력이면 같은 그림).</summary>
        [MenuItem("IronRing/Art/Generate Equipment Sprites")]
        public static void Generate()
        {
            Load();
            Directory.CreateDirectory(OUTPUT);
            var sheet = new List<Color32[]>();
            var names = new List<string>();
            foreach (string id in IDS)
            {
                Begin();
                Layer();
                Draw(id);
                EndLayer();
                if (id == "steam_booster") { Puff(10, 40, 5, 170); Puff(5, 46, 4, 120); }
                C.Save(OUTPUT + id + ".png");
                sheet.Add(C.Px); names.Add(id);
            }
            Begin();
            Frame();
            C.Save(OUTPUT + CELL_FRAME + ".png");
            sheet.Add(C.Px); names.Add(CELL_FRAME);
            File.WriteAllBytes(OUTPUT + "equipment_contact_sheet.png", Sheet(sheet, 6));
            AssetDatabase.Refresh();
            foreach (string name in names) ConfigureImporter(OUTPUT + name + ".png");
            ConfigureImporter(OUTPUT + "equipment_contact_sheet.png");
            AssetDatabase.SaveAssets();
            Debug.Log("SpriteGenEquipment: " + names.Count + " equipment sprites generated.");
        }

        private static void Draw(string id)
        {
            switch (id)
            {
                case "turret":
                    C.Disc(30, 32, 22, Fe.S); C.Disc(30, 32, 20, Fe.B); RivetRing(30, 32, 18, 14, Bs.L);
                    Dome(30, 32, 12, Bs);
                    CylH(34, 28, 28, 8, Fe); C.Rect(56, 27, 6, 10, Fe.S); C.Rect(36, 31, 20, 2, Fe.L);
                    C.Disc(26, 36, 3, Bs.L);
                    break;
                case "heavy_turret":
                    Box(2, 17, 58, 30, Fe); RivetsH(5, 57, 19, 6, Bs.L); RivetsH(5, 57, 44, 6, Bs.L);
                    C.Disc(22, 32, 13, Fe.S); Dome(22, 32, 11, Cu); RivetRing(22, 32, 9, 10, Bs.B);
                    CylH(30, 27, 32, 10, Fe); C.Rect(58, 25, 5, 14, Fe.S); C.Rect(32, 31, 24, 2, Fe.L);
                    C.Rect(44, 26, 3, 12, Bs.S);
                    break;
                case "armor_plate":
                    Box(6, 6, 52, 52, Fe); C.Rect(10, 10, 44, 44, Fe.B);
                    C.Line(10, 10, 53, 53, Fe.S, 2); C.Line(10, 53, 53, 10, Fe.S, 2);
                    RivetsH(10, 54, 9, 6, Bs.L); RivetsH(10, 54, 54, 6, Bs.L); RivetsV(9, 10, 54, 6, Bs.L); RivetsV(54, 10, 54, 6, Bs.L);
                    C.Rect(26, 26, 12, 12, Fe.L); C.Disc(32, 32, 3, Bs.B);
                    break;
                case "steam_booster":
                    CylH(12, 18, 40, 28, Bs); C.Rect(12, 18, 3, 28, Bs.S); C.Rect(49, 18, 3, 28, Bs.S);
                    RivetsV(18, 20, 44, 5, Cu.S); RivetsV(45, 20, 44, 5, Cu.S);
                    C.Rect(4, 22, 9, 6, Fe.B); C.Rect(4, 36, 9, 6, Fe.B); C.Rect(2, 23, 3, 4, O.S); C.Rect(2, 37, 3, 4, O.S);
                    C.Disc(32, 32, 6, Cu.B); C.Ring(32, 32, 4, 6, Cu.S); C.Rect(31, 32, 2, 5, O.S);
                    C.Rect(52, 28, 8, 8, Fe.S);
                    break;
                case "repair_rig":
                    Box(8, 8, 48, 48, Wd); C.Rect(12, 12, 40, 40, Wd.S);
                    Gear(26, 36, 12, 8, Bs); C.Disc(26, 36, 4, Fe.S);
                    Gear(42, 22, 8, 6, Cu); C.Disc(42, 22, 3, Fe.S);
                    C.Line(14, 14, 34, 34, Fe.L, 3); C.Ring(14, 14, 2, 5, Fe.B); C.Disc(14, 14, 2, Wd.S);
                    break;
            }
        }

        /// <summary>칸 틀: 반투명 어두운 바닥 + 놋쇠 테두리 + 모서리 리벳.</summary>
        private static void Frame()
        {
            C.Rect(2, 2, 60, 60, A(O.S, 90));
            C.Rect(2, 2, 60, 3, Bs.B); C.Rect(2, 59, 60, 3, Bs.B); C.Rect(2, 2, 3, 60, Bs.B); C.Rect(59, 2, 3, 60, Bs.B);
            C.Rect(2, 2, 60, 1, Bs.S); C.Rect(2, 61, 60, 1, Bs.L);
            foreach (int x in new[] { 4, 59 }) foreach (int y in new[] { 4, 59 }) C.Disc(x, y, 2, Bs.L);
        }
    }
}
