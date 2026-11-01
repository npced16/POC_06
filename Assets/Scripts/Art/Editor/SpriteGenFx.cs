using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 019. 굴뚝 연기 스프라이트 6프레임(Assets/Art/Generated/fx/smoke_0..5.png)을 팔레트 연기색으로 그린다. 연기 덩이는 아래 가운데(픽셀 32, 6)에서
    /// 피어올라 커지며 옅어지고, 6프레임이 끊김 없이 돈다. Smoke.prefab(PropsBuild)이 SpriteFrameLoop로 넘긴다.
    /// </summary>
    public static class SpriteGenFx
    {
        public const string OUTPUT = "Assets/Art/Generated/fx/";
        public const int FRAMES = 6;

        /// <summary>연기 프레임과 fx_contact_sheet.png를 만들고 임포터를 설정한다.</summary>
        [MenuItem("IronRing/Art/Generate Fx Sprites")]
        public static void Generate()
        {
            Load();
            Directory.CreateDirectory(OUTPUT);
            var sprites = new List<Color32[]>();
            for (int f = 0; f < FRAMES; f++)
            {
                Begin();
                for (int i = 3; i >= 0; i--)
                {
                    float t = ((float)f / FRAMES + i / 4f) % 1f;
                    float cx = 32 + Mathf.Sin(t * Mathf.PI * 2 + i) * 3f + t * 8f, cy = 6 + t * 46f, r = 4 + t * 9f;
                    Puff(cx, cy, r, (byte)(235 * Mathf.Pow(1f - t, 0.6f)));
                }
                C.Save(OUTPUT + "smoke_" + f + ".png");
                sprites.Add(C.Px);
            }
            File.WriteAllBytes(OUTPUT + "fx_contact_sheet.png", Sheet(sprites, FRAMES));
            AssetDatabase.Refresh();
            for (int f = 0; f < FRAMES; f++) ConfigureImporter(OUTPUT + "smoke_" + f + ".png");
            ConfigureImporter(OUTPUT + "fx_contact_sheet.png");
            AssetDatabase.SaveAssets();
        }
    }
}
