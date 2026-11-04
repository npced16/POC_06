using System;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Localization;

namespace IronRing.Art.Editor
{
    public static class FontSetup
    {
        private const string TMP_FONT = "Assets/Fonts/NotoSansKR SDF.asset";
        private const string MULMARU_SOURCE = "Assets/Fonts/Mulmaru.otf";
        private const string MULMARU_FONT = "Assets/Fonts/Mulmaru SDF.asset";
        private const string UI_THEME = "Assets/Prefabs/UI/UiTheme.asset";

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

        /// <summary>
        /// Mulmaru.otf와 ko.json의 글자를 사용해 동적 TMP 폰트 에셋을 만든다.
        /// 생성된 에셋을 반환하며 이미 있으면 기존 에셋을 반환한다.
        /// </summary>
        public static TMPro.TMP_FontAsset GenerateMulmaru()
        {
            TMPro.TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(MULMARU_FONT);
            if (existing != null) return existing;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(MULMARU_SOURCE);
            if (source == null) throw new InvalidOperationException("Mulmaru font source is missing: " + MULMARU_SOURCE);
            TMPro.TMP_FontAsset font = TMPro.TMP_FontAsset.CreateFontAsset(source, 36, 6,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                TMPro.AtlasPopulationMode.Dynamic, true);
            font.name = "Mulmaru SDF";
            font.isMultiAtlasTexturesEnabled = true;
            TMPro.TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(TMP_FONT);
            if (fallback != null)
                font.fallbackFontAssetTable = new System.Collections.Generic.List<TMPro.TMP_FontAsset> { fallback };
            AssetDatabase.CreateAsset(font, MULMARU_FONT);
            string characters = string.Concat(Strings.Parse(File.ReadAllText("Assets/Scripts/Localization/Resources/ko.json")).Values);
            characters += string.Concat(Enumerable.Range(32, 95).Select(value => (char)value));
            if (!font.TryAddCharacters(characters, out string missing)) Debug.LogWarning("MULMARU FONT: missing glyphs " + missing);
            for (int i = 0; i < font.atlasTextures.Length; i++)
            {
                Texture2D atlas = font.atlasTextures[i];
                if (atlas == null || AssetDatabase.Contains(atlas)) continue;
                atlas.name = "Mulmaru SDF Atlas" + (i == 0 ? "" : " " + i);
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.material.name = "Mulmaru SDF Material";
            if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MULMARU_FONT);
            Debug.Log("MULMARU FONT: created atlases=" + font.atlasTextures.Length);
            return font;
        }

        /// <summary>
        /// Mulmaru TMP 폰트를 테마와 프로젝트 프리팹 및 열린 Main 씬의 TMP 글자에 연결한다.
        /// 기존 Noto 글꼴을 쓰는 글자만 바꾸고 변경된 에셋과 씬을 저장한다.
        /// </summary>
        [MenuItem("IronRing/Art/Apply Mulmaru Font")]
        public static void ApplyMulmaru()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before changing font assets.");
            TMPro.TMP_FontAsset font = GenerateMulmaru();
            TMPro.TMP_FontAsset previous = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(TMP_FONT);
            var theme = AssetDatabase.LoadAssetAtPath<IronRing.Hud.Ui.UiTheme>(UI_THEME);
            if (theme == null) throw new InvalidOperationException("UI theme is missing: " + UI_THEME);
            var themeData = new SerializedObject(theme);
            themeData.FindProperty("_font").objectReferenceValue = font;
            themeData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                try
                {
                    foreach (TMPro.TMP_Text label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    {
                        if (label.font != previous) continue;
                        label.font = font;
                        dirty = true;
                        changed++;
                    }
                    if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            Scene scene = SceneManager.GetActiveScene();
            bool sceneChanged = false;
            if (scene.path == "Assets/Scenes/Main.unity")
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (TMPro.TMP_Text label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (label.font != previous) continue;
                    label.font = font;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                    sceneChanged = true;
                    changed++;
                }
                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("MULMARU FONT: applied labels=" + changed + " scene=" + sceneChanged);
        }
    }
}
