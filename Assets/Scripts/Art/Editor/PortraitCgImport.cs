using System;

using UnityEngine;

using UnityEditor;

using IronRing.Npc;

namespace IronRing.Art.Editor
{
    /// <summary>Story 029의 CG 초상을 기존 NPC 카탈로그에 배정한다.</summary>
    public static class PortraitCgImport
    {
        private const string PORTRAIT_DIRECTORY = "Assets/Art/Portraits/";
        private const string CATALOG_PATH = "Assets/Scripts/Npc/Resources/Npc/NpcPortraits.asset";

        /// <summary>
        /// 기존 카탈로그의 NPC id로 CG 파일을 읽고 임포트 설정과 스프라이트 참조를 갱신한다.
        /// 입력 인자는 없으며 id와 순서를 보존하고 배정 수 및 검증 결과 문자열을 반환한다.
        /// </summary>
        public static string Assign()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before assigning portrait CG assets.");

            NpcPortraitCatalog catalog = AssetDatabase.LoadAssetAtPath<NpcPortraitCatalog>(CATALOG_PATH);
            if (catalog == null)
                throw new InvalidOperationException("NPC portrait catalog is missing.");

            var serializedCatalog = new SerializedObject(catalog);
            SerializedProperty entries = serializedCatalog.FindProperty("_entries");
            if (entries.arraySize != 35)
                throw new InvalidOperationException("Expected exactly 35 existing NPC entries.");

            var sprites = new Sprite[entries.arraySize];
            for (int i = 0; i < entries.arraySize; i++)
            {
                string id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue;
                sprites[i] = ImportSprite(id);
            }
            Sprite fallback = ImportSprite("fallback");

            for (int i = 0; i < entries.arraySize; i++)
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("_sprite").objectReferenceValue = sprites[i];
            serializedCatalog.FindProperty("_fallback").objectReferenceValue = fallback;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);

            serializedCatalog.Update();
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("_sprite").objectReferenceValue != sprites[i])
                    throw new InvalidOperationException("Portrait assignment verification failed.");
            }
            if (serializedCatalog.FindProperty("_fallback").objectReferenceValue != fallback)
                throw new InvalidOperationException("Fallback assignment verification failed.");

            return "PortraitCgImport.Assign: verified 35 NPC sprites + 1 fallback; 512x640; Sprite, PPU 100, Bilinear, Uncompressed; existing ids/order preserved.";
        }

        /// <summary>
        /// id에 해당하는 PNG의 Sprite 임포트 설정을 적용한다.
        /// id로 경로를 결정하며 크기를 검증한 Sprite를 반환하고 해당 텍스처 설정만 저장한다.
        /// </summary>
        private static Sprite ImportSprite(string id)
        {
            string path = PORTRAIT_DIRECTORY + "portrait_" + id + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Portrait PNG is missing: " + path);

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != 512 || height != 640)
                throw new InvalidOperationException("Expected 512x640 portrait: " + path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || sprite.rect.width != 512 || sprite.rect.height != 640)
                throw new InvalidOperationException("Portrait sprite import failed: " + path);
            return sprite;
        }
    }
}
