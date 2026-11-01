using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 019. 선로 화면 장식 소품 도트(Assets/Art/Generated/props/prop_crate·prop_barrel·prop_shrub·prop_lamp_post.png)를 투명 바탕에 그린다.
    /// 색은 SteampunkPalette.json. 소품 프리팹과 씬 배치는 IronRing.Presentation.Map.Editor.PropsBuild가 한다.
    /// </summary>
    public static class SpriteGenProps
    {
        public const string OUTPUT = "Assets/Art/Generated/props/";
        public static readonly string[] NAMES = { "prop_crate", "prop_barrel", "prop_shrub", "prop_lamp_post" };

        /// <summary>소품 PNG 4장과 props_contact_sheet.png를 만들고 임포터를 설정한다.</summary>
        [MenuItem("IronRing/Art/Generate Prop Sprites")]
        public static void Generate()
        {
            Load();
            Directory.CreateDirectory(OUTPUT);
            var sprites = new List<Color32[]>();
            foreach (string name in NAMES)
            {
                Begin();
                C.Ellipse(name == "prop_lamp_post" ? 32 : 34, name == "prop_lamp_post" ? 6 : 10, name == "prop_lamp_post" ? 8 : 24, 5, A(O.S, 110));
                if (name == "prop_lamp_post") Glow(32.5f, 46.5f, 20);
                Layer();
                switch (name)
                {
                    case "prop_crate": Crate(6, 10, 40, 38); Crate(30, 30, 28, 26); break;
                    case "prop_barrel": Barrel(30, 32, 24, false); break;
                    case "prop_shrub": Shrub(32, 30, 24, 7); Shrub(18, 22, 12, 9); Shrub(47, 20, 11, 11); break;
                    default: LampPost(32, 4, 38, false); break;
                }
                EndLayer();
                C.Save(OUTPUT + name + ".png");
                sprites.Add(C.Px);
            }
            File.WriteAllBytes(OUTPUT + "props_contact_sheet.png", Sheet(sprites, NAMES.Length));
            AssetDatabase.Refresh();
            foreach (string name in NAMES) ConfigureImporter(OUTPUT + name + ".png");
            ConfigureImporter(OUTPUT + "props_contact_sheet.png");
            AssetDatabase.SaveAssets();
        }
    }
}
