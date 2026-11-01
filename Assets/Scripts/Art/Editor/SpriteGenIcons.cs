using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using IronRing.Hud.Ui;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    public static class SpriteGenIcons
    {
        private const string OUTPUT = "Assets/Art/Generated/icons/";
        private const string CATALOG = "Assets/Prefabs/UI/IconCatalog.asset";
        private const string EVIDENCE = "production/qa/evidence/story-040-economy-market-ledger/";
        private static readonly string[] IDS = { "goods_coal", "goods_iron", "goods_parts", "goods_food", "goods_glass", "goods_brass", "ui_coin", "ui_cargo", "ui_depart" };

        /// <summary>팔레트로 아홉 아이콘 PNG와 콘택트 시트를 만들고 Sprite 임포터 및 카탈로그 참조를 저장한다.</summary>
        public static void Generate()
        {
            Load(); Directory.CreateDirectory(OUTPUT); Directory.CreateDirectory(EVIDENCE);
            var pixels = new List<Color32[]>();
            foreach (string id in IDS)
            {
                Begin(); Layer(); Draw(id); EndLayer();
                C.Save(OUTPUT + id + ".png"); pixels.Add(C.Px);
            }
            File.WriteAllBytes(EVIDENCE + "icons_contact_sheet.png", Sheet(pixels, 3));
            AssetDatabase.Refresh();
            foreach (string id in IDS) ConfigureImporter(OUTPUT + id + ".png");
            IconCatalog catalog = AssetDatabase.LoadAssetAtPath<IconCatalog>(CATALOG);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<IconCatalog>(); AssetDatabase.CreateAsset(catalog, CATALOG); }
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("_entries");
            foreach (string id in IDS)
            {
                int index = -1;
                for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue == id) index = i;
                if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_id").stringValue = id;
                entry.FindPropertyRelative("_sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(OUTPUT + id + ".png");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            Debug.Log("UI040 ICONS: 9 sprites, catalog and contact sheet saved.");
        }

        /// <summary>id 품목 또는 UI 기호를 현재 64픽셀 캔버스에 팔레트 명암으로 그린다.</summary>
        private static void Draw(string id)
        {
            switch (id)
            {
                case "goods_coal":
                    C.Ellipse(23, 24, 16, 12, R.S); C.Ellipse(42, 25, 14, 12, R.B); C.Ellipse(31, 39, 13, 14, R.B);
                    C.Line(23, 43, 31, 49, R.L, 3); C.Line(14, 26, 22, 31, R.L, 3);
                    C.Line(29, 19, 34, 25, Bs.S, 2); C.Line(40, 31, 44, 35, Bs.B, 2); break;
                case "goods_iron": Ingot(12, 15, Fe); Ingot(23, 32, Fe); break;
                case "goods_brass": Ingot(12, 15, Bs); Ingot(23, 32, Bs); break;
                case "goods_parts":
                    Gear(27, 35, 19, 10, Cu); C.Disc(27, 35, 7, O.B); C.Ring(27, 35, 7, 9, Bs.L);
                    C.Line(39, 12, 51, 27, Fe.B, 8); C.Line(38, 14, 49, 27, Fe.L, 2); C.Disc(49, 28, 7, Bs.B); C.Line(45, 25, 52, 31, Bs.S, 2); break;
                case "goods_food":
                    C.Ellipse(32, 28, 20, 18, Wd.S); C.Ellipse(30, 31, 18, 18, Wd.L);
                    C.Ellipse(33, 37, 12, 10, Bs.B); C.Line(26, 42, 29, 35, Sm.B, 2); C.Line(34, 44, 37, 37, Sm.B, 2);
                    Box(13, 15, 38, 14, Wd); C.Line(14, 19, 50, 19, Wd.L); C.Rect(18, 15, 3, 14, Bs.S); C.Rect(43, 15, 3, 14, Bs.S); break;
                case "goods_glass":
                    Box(17, 12, 30, 37, Wa); C.Rect(26, 49, 12, 5, Bs.B); C.Rect(23, 46, 18, 4, Wa.L);
                    C.Line(21, 20, 39, 44, Sm.B, 3); C.Line(32, 17, 43, 32, Wa.L, 2); C.Rect(19, 14, 25, 3, Wa.S); break;
                case "ui_coin":
                    C.Disc(32, 30, 23, Bs.S); C.Disc(32, 33, 22, Bs.B); C.Ring(32, 33, 18, 20, Bs.L);
                    C.Line(24, 24, 24, 42, Bs.S, 3); C.Line(24, 42, 32, 32, Bs.S, 3); C.Line(32, 32, 40, 42, Bs.S, 3); C.Line(40, 42, 40, 24, Bs.S, 3); break;
                case "ui_cargo":
                    Box(11, 12, 42, 40, Wd); C.Rect(13, 29, 38, 2, Wd.S); C.Rect(13, 39, 38, 2, Wd.S);
                    C.Line(16, 16, 48, 47, Wd.L, 5); Box(11, 12, 5, 40, Bs); Box(48, 12, 5, 40, Bs);
                    foreach (int x in new[] { 13, 50 }) foreach (int y in new[] { 16, 47 }) C.Disc(x, y, 1, O.B); break;
                case "ui_depart":
                    C.Rect(10, 25, 27, 14, Bs.B);
                    for (int x = 32; x < 54; x++) C.Rect(x, 10 + x - 32, 1, 44 - (x - 32) * 2, Bs.B);
                    C.Line(12, 37, 35, 37, Bs.L, 2); C.Line(34, 51, 51, 34, Bs.L, 2); C.Rect(7, 14, 3, 36, Fe.B); break;
            }
        }

        /// <summary>x,y를 기준으로 지정된 금속 명암 s의 주괴를 캔버스에 그린다.</summary>
        private static void Ingot(int x, int y, Swatch s)
        {
            Box(x, y, 30, 14, s);
            for (int row = 0; row < 8; row++) C.Rect(x + row, y + 14 + row, 30 - row, 1, s.L);
            C.Rect(x + 3, y + 3, 22, 2, s.S);
        }
    }
}
