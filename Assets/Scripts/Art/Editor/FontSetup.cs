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

        private const string TMP_FONT = "Assets/Fonts/NotoSansKR SDF.asset";

        /// <summary>
        /// 스토리 022. TMP 필수 리소스(Assets/TextMesh Pro)가 없으면 가져오고, NotoSansKR-Regular.otf로 동적 TMP 폰트 에셋(Assets/Fonts/NotoSansKR SDF.asset,
        /// 36pt·패딩 6·SDFAA·1024², 다중 아틀라스)을 만든다. 한국어 표(ko.json)와 ASCII 글자를 미리 넣고 아틀라스·머티리얼을 하위 에셋으로 저장한다. 이미 있으면 건너뛴다.
        /// </summary>
        [MenuItem("IronRing/Art/Generate TMP Korean Font")]
        public static void GenerateTmp()
        {
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh();
            }
            if (AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(TMP_FONT) != null) { Debug.Log("TMP FONT: exists " + TMP_FONT); return; }
            if (Shader.Find("TextMeshPro/Distance Field") == null) { Debug.LogError("TMP FONT: TMP 셰이더가 아직 없습니다. 필수 리소스 가져오기가 끝난 뒤 다시 실행하세요."); return; }
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
            TMPro.TMP_FontAsset font = TMPro.TMP_FontAsset.CreateFontAsset(source, 36, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                TMPro.AtlasPopulationMode.Dynamic, true);
            font.name = "NotoSansKR SDF";
            font.isMultiAtlasTexturesEnabled = true;
            AssetDatabase.CreateAsset(font, TMP_FONT);
            string characters = string.Concat(Strings.Parse(File.ReadAllText("Assets/Scripts/Localization/Resources/ko.json")).Values);
            characters += string.Concat(Enumerable.Range(32, 95).Select(value => (char)value));
            if (!font.TryAddCharacters(characters, out string missing)) Debug.LogWarning("TMP FONT: 없는 글자 " + missing);
            for (int i = 0; i < font.atlasTextures.Length; i++)
            {
                Texture2D atlas = font.atlasTextures[i];
                if (atlas == null || AssetDatabase.Contains(atlas)) continue;
                atlas.name = "NotoSansKR SDF Atlas" + (i == 0 ? "" : " " + i);
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.material.name = "NotoSansKR SDF Material";
            if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TMP_FONT);
            Debug.Log("TMP FONT: created " + TMP_FONT + " atlases=" + font.atlasTextures.Length);
        }
    }
}
