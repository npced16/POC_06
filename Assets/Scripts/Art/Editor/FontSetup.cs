using System;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

using UnityEditor;

using IronRing.Localization;

namespace IronRing.Art.Editor
{
    public static class FontSetup
    {
        private const string ROOT = "Assets/Fonts/Resources/";
        private const string FONT = ROOT + "NotoSansKR.asset";

        /// <summary>복사된 NotoSansKR 폰트와 한국어 테이블로 FontAsset, 기본 PanelSettings와 공유 USS를 생성한다.</summary>
        [MenuItem("IronRing/Art/Generate Korean Font")]
        public static void Generate()
        {
            Directory.CreateDirectory(ROOT);
            AssetDatabase.Refresh();
            FontAsset font = AssetDatabase.LoadAssetAtPath<FontAsset>(FONT);
            if (font == null)
            {
                font = FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf"));
                font.name = "NotoSansKR";
                font.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(font, FONT);
            }
            string characters = string.Concat(Strings.Parse(File.ReadAllText("Assets/Scripts/Localization/Resources/ko.json")).Values);
            characters += string.Concat(Enumerable.Range(32, 95).Select(value => (char)value));
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing font glyphs: " + missing);
            foreach (Texture2D atlas in font.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            PanelTextSettings text = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(ROOT + "KoreanTextSettings.asset");
            if (text == null)
            {
                text = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(text, ROOT + "KoreanTextSettings.asset");
            }
            text.defaultFontAsset = font;
            EditorUtility.SetDirty(text);
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(ROOT + "KoreanPanelSettings.asset");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 720);
                AssetDatabase.CreateAsset(panel, ROOT + "KoreanPanelSettings.asset");
            }
            panel.textSettings = text;
            EditorUtility.SetDirty(panel);
            File.WriteAllText(ROOT + "KoreanDefault.uss", ":root { -unity-font-definition: url(\"NotoSansKR.asset\"); }");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
