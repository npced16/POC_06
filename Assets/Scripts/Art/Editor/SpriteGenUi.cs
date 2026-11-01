using System.IO;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 022. UGUI 9분할 스프라이트 생성기(64×64, 테두리 8px). 색은 모두 SteampunkPalette.json에서 온다.
    /// 패널(자두색 바탕·황동 2px 테두리·리벳), 버튼(구리), 눌린 버튼, 슬롯(어두운 틀), 선택 슬롯(호박색 테두리), 토스트(반투명 자두색).
    /// </summary>
    public static class SpriteGenUi
    {
        /// <summary>출력 폴더.</summary>
        public const string OUTPUT = "Assets/Art/Generated/ui/";
        /// <summary>9분할 테두리(픽셀, 네 변 모두).</summary>
        public const int BORDER = 8;

        /// <summary>UI 스프라이트 PNG를 그리고 Sprite(PPU 64, Point, 무압축, FullRect, border 8)로 임포트한다. 같은 입력이면 같은 결과.</summary>
        [MenuItem("IronRing/Art/Generate UI Sprites")]
        public static void Generate()
        {
            Load();
            Swatch plum = P["ui_panel"];
            Directory.CreateDirectory(OUTPUT);
            Save("ui_panel", Bs, plum.L, plum.B, plum.S, true, 255);
            Save("ui_button", Bs, Cu.L, Cu.B, Cu.S, false, 255);
            Save("ui_button_pressed", new Swatch(Bs.S, Bs.B, Bs.L), Cu.S, Cu.S, Cu.B, false, 255);
            Save("ui_slot", Fe, O.S, Mo.S, Mo.B, false, 255);
            Save("ui_slot_selected", Gl, O.S, Mo.S, Mo.B, false, 255);
            Save("ui_toast", Bs, plum.B, plum.S, plum.S, false, 235);
            AssetDatabase.Refresh();
            foreach (string name in new[] { "ui_panel", "ui_button", "ui_button_pressed", "ui_slot", "ui_slot_selected", "ui_toast" })
                Configure(OUTPUT + name + ".png");
            Debug.Log("UI SPRITES: " + OUTPUT);
        }

        /// <summary>외곽선 1px + rim 2px(위·왼쪽 밝음, 아래·오른쪽 그늘) + 바탕(윗줄 hi, 아랫줄 lo) 틀을 그려 저장한다.</summary>
        private static void Save(string name, Swatch rim, Color32 hi, Color32 fill, Color32 lo, bool rivets, byte alpha)
        {
            Begin();
            int s = SIZE;
            C.Rect(0, 0, s, s, O.B);
            C.Rect(1, 1, s - 2, s - 2, rim.B);
            C.Rect(1, s - 2, s - 2, 1, rim.L); C.Rect(1, 1, 1, s - 2, rim.L);
            C.Rect(1, 1, s - 2, 1, rim.S); C.Rect(s - 2, 1, 1, s - 2, rim.S);
            Raw(3, 3, s - 6, s - 6, A(fill, alpha));
            Raw(3, s - 4, s - 6, 1, A(hi, alpha));
            Raw(3, 3, s - 6, 1, A(lo, alpha));
            if (rivets)
                foreach (int x in new[] { 5, s - 6 }) foreach (int y in new[] { 5, s - 6 })
                {
                    C.Set(x, y, rim.L);
                    C.Set(x + 1, y - 1, rim.S);
                }
            foreach (int x in new[] { 0, s - 1 }) foreach (int y in new[] { 0, s - 1 }) C.Erase(x, y);
            C.Save(OUTPUT + name + ".png");
        }

        /// <summary>합성 없이 픽셀을 그대로 덮어쓴다(반투명 바탕용).</summary>
        private static void Raw(int x, int y, int w, int h, Color32 c)
        {
            for (int py = y; py < y + h; py++) for (int px = x; px < x + w; px++) C.Px[py * C.Size + px] = c;
        }

        /// <summary>ArtKit.ConfigureImporter와 같은 설정에 9분할 테두리와 FullRect 메시를 더해 한 번에 다시 임포트한다.</summary>
        private static void Configure(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.InvalidOperationException("Not imported: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PPU;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = new Vector4(BORDER, BORDER, BORDER, BORDER);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
