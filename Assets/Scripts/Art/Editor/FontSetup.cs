using System;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

using IronRing.Localization;

namespace IronRing.Art.Editor
{
    public static class FontSetup
    {
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
