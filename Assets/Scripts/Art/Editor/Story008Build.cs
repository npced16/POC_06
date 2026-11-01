using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using UnityEngine;

using UnityEditor;

namespace IronRing.Art.Editor
{
    public static class Story008Build
    {
        /// <summary>스토리 008 스프라이트를 생성한다(UI Toolkit 미리보기 씬은 스토리 022에서 archive/legacy-uitoolkit/로 옮겼다).</summary>
        [MenuItem("IronRing/Art/Generate Story008 Sprites")]
        public static void Generate()
        {
            SpriteGen.Generate();
            AssetDatabase.SaveAssets();
        }

        /// <summary>PNG를 두 번 생성하여 해시 및 임포터 설정을 비교하고 결과를 스토리 증거 폴더에 저장한다.</summary>
        public static void Verify()
        {
            const string evidence = "production/qa/evidence/story-008-art-set-korean-base/";
            SpriteGen.Generate();
            string[] files = Directory.GetFiles("Assets/Art/Generated", "*.png").Select(path => path.Replace('\\', '/')).OrderBy(path => path).ToArray();
            if (files.Length != 28) throw new InvalidOperationException("Expected 27 sprites and one contact sheet");
            string[] hashes = files.Select(Hash).ToArray();
            SpriteGen.Generate();
            for (int i = 0; i < files.Length; i++)
            {
                if (hashes[i] != Hash(files[i])) throw new InvalidOperationException("Non-deterministic PNG: " + files[i]);
                var importer = (TextureImporter)AssetImporter.GetAtPath(files[i]);
                if (importer.spritePixelsPerUnit != ArtKit.PPU || importer.filterMode != FilterMode.Point ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed)
                    throw new InvalidOperationException("Invalid sprite import: " + files[i]);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(files[i]);
                if (!files[i].EndsWith("contact_sheet.png") && (texture.width != ArtKit.SIZE || texture.height != ArtKit.SIZE))
                    throw new InvalidOperationException("Invalid sprite dimensions: " + files[i]);
            }
            Directory.CreateDirectory(evidence);
            File.WriteAllText(evidence + "generation-check.txt", "PASS: 27 sprites, 64x64, PPU64, Point, Uncompressed; 28 PNG SHA256 hashes identical on two runs.\n" +
                string.Join("\n", files.Select((path, i) => path + " " + hashes[i])));
            File.Copy("Assets/Art/Generated/contact_sheet.png", evidence + "contact_sheet.png", true);
        }

        /// <summary>path의 파일 바이트를 읽어 결정성 비교용 SHA256 문자열을 반환한다.</summary>
        private static string Hash(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }
    }
}
