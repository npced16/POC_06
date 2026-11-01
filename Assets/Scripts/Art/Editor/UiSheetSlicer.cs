using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 스토리 024. tools/art/ui_sheet_rects.json(키잉 스크립트 출력)으로 SteampunkUiSheet_keyed.png를 Multiple 스프라이트로 나눈다.
    /// 2D Sprite 1.0.0의 ISpriteEditorDataProvider 사용(TextureImporter.spritesheet는 Unity 6에서 obsolete). 멱등: 같은 이름은 같은 spriteID를 유지한다.
    /// </summary>
    public static class UiSheetSlicer
    {
        /// <summary>키잉된 시트 텍스처 경로.</summary>
        public const string TEXTURE = "Assets/Art/UI/SteampunkUiSheet_keyed.png";
        /// <summary>조각 rect·border JSON(저장소 루트 기준).</summary>
        public const string RECTS = "tools/art/ui_sheet_rects.json";
        private const float PPU = 64f;

        [Serializable] private sealed class Edge { public float l, b, r, t; }
        [Serializable] private sealed class Piece { public string name; public float x, y, w, h; public Edge border; public Edge pad; }
        [Serializable] private sealed class Wrapper { public Piece[] items; }

        /// <summary>JSON 조각 목록(이름·rect·border·pad). 테마 적용에서도 쓴다.</summary>
        public static IReadOnlyList<(string name, Rect rect, Vector4 border, Vector4 pad)> LoadPieces()
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, RECTS);
            var w = JsonUtility.FromJson<Wrapper>("{\"items\":" + File.ReadAllText(path) + "}");
            return w.items.Select(p => (p.name, new Rect(p.x, p.y, p.w, p.h),
                new Vector4(p.border.l, p.border.b, p.border.r, p.border.t),
                p.pad == null ? Vector4.zero : new Vector4(p.pad.l, p.pad.b, p.pad.r, p.pad.t))).ToList();
        }

        /// <summary>임포터 설정 + 스프라이트 rect 기록. 메뉴 IronRing/Art/Slice UI Sheet.</summary>
        [MenuItem("IronRing/Art/Slice UI Sheet")]
        public static void Apply()
        {
            var importer = AssetImporter.GetAtPath(TEXTURE) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("키잉된 시트가 없다: " + TEXTURE + " (python tools/art/key_ui_sheet.py 먼저)");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PPU;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            Dictionary<string, SpriteRect> existing = provider.GetSpriteRects().ToDictionary(r => r.name);
            var rects = new List<SpriteRect>();
            foreach (var p in LoadPieces())
            {
                existing.TryGetValue(p.name, out SpriteRect r);
                r ??= new SpriteRect { name = p.name, spriteID = GUID.Generate() };
                r.rect = p.rect;
                r.border = p.border;
                r.alignment = SpriteAlignment.Center;
                r.pivot = new Vector2(0.5f, 0.5f);
                rects.Add(r);
            }
            provider.SetSpriteRects(rects.ToArray());
            var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            ids?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            Debug.Log($"[UiSheetSlicer] {rects.Count}개 조각: {TEXTURE}");
        }
    }
}
