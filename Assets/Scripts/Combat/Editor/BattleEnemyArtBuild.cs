using System;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Art.Editor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Combat.Editor
{
    /// <summary>
    /// 새 적 유형(자폭병·방패수호병·근접 보스·원거리 보스)의 정면 도트를 그려 Assets/Art/Generated/Battle/enemy_*.png로 저장하고,
    /// BattleReference 씬의 BattlePocDriver 적 스프라이트 목록에 연결한다. 색은 SteampunkPalette.json에서 읽는다.
    /// </summary>
    public static class BattleEnemyArtBuild
    {
        private const int SIZE = 96;
        private const string OUTPUT = "Assets/Art/Generated/Battle/";
        private const string SCENE = "Assets/Scenes/BattleReference.unity";
        private const string EVIDENCE = "production/qa/evidence/battle-enemy-types/";
        private const int PREVIEW_SCALE = 4;
        private static readonly string[] ENEMY_IDS = { "bomber", "shield_guard", "warlord", "gunner_boss" };

        /// <summary>
        /// 네 적 유형의 PNG를 그리고 스프라이트로 임포트한 뒤 확대 미리보기를 증거 폴더에 저장한다.
        /// ENEMY_IDS를 사용하며, Assets/Art/Generated/Battle의 enemy_*.png 파일을 새로 쓴다.
        /// </summary>
        [MenuItem("IronRing/Art/Generate Battle Enemy Sprites")]
        public static void Generate()
        {
            Load();
            Directory.CreateDirectory(EVIDENCE);
            var sheet = new Color32[ENEMY_IDS.Length][];
            for (int i = 0; i < ENEMY_IDS.Length; i++)
            {
                C = new PixelCanvas(SIZE);
                C.Ellipse(48, 5, 26, 4, A(O.S, 110));
                Layer();
                switch (ENEMY_IDS[i])
                {
                    case "bomber": DrawBomber(); break;
                    case "shield_guard": DrawShieldGuard(); break;
                    case "warlord": DrawWarlord(); break;
                    default: DrawGunnerBoss(); break;
                }
                EndLayer();
                C.Save(OUTPUT + "enemy_" + ENEMY_IDS[i] + ".png");
                sheet[i] = C.Px;
            }
            File.WriteAllBytes(EVIDENCE + "enemy_types_preview.png", Preview(sheet));
            AssetDatabase.Refresh();
            foreach (string id in ENEMY_IDS) ConfigureSprite(OUTPUT + "enemy_" + id + ".png");
            AssetDatabase.SaveAssets();
            Debug.Log("BATTLE ART: generated " + ENEMY_IDS.Length + " enemy sprites");
        }

        /// <summary>
        /// BattleReference 씬의 BattlePocDriver에 적 id와 생성한 스프라이트를 짝지어 넣고 씬을 저장한다.
        /// SCENE과 ENEMY_IDS를 사용하며, 씬이 닫혀 있으면 추가로 열었다가 저장 후 닫는다.
        /// </summary>
        [MenuItem("IronRing/Battle/Bind Enemy Sprites")]
        public static void BindToBattleScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before binding enemy sprites.");
            Scene scene = SceneManager.GetSceneByPath(SCENE);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException("BattleReference has unsaved changes; save it first.");
            BattlePocDriver driver = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if ((driver = root.GetComponentInChildren<BattlePocDriver>(true)) != null) break;
            if (driver == null) throw new InvalidOperationException("BattleReference has no BattlePocDriver.");
            var serialized = new SerializedObject(driver);
            SerializedProperty bindings = serialized.FindProperty("_enemySprites");
            bindings.arraySize = ENEMY_IDS.Length;
            for (int i = 0; i < ENEMY_IDS.Length; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OUTPUT + "enemy_" + ENEMY_IDS[i] + ".png");
                if (sprite == null) throw new InvalidOperationException("Missing sprite for " + ENEMY_IDS[i] + "; run Generate first.");
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_enemyId").stringValue = ENEMY_IDS[i];
                entry.FindPropertyRelative("_sprite").objectReferenceValue = sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("BATTLE ART: bound " + ENEMY_IDS.Length + " enemy sprites to " + SCENE);
        }

        /// <summary>
        /// 둥근 폭탄을 머리 위로 든 작은 자폭병을 그린다.
        /// 현재 캔버스 C를 사용하며, 픽셀 버퍼를 변경한다.
        /// </summary>
        private static void DrawBomber()
        {
            // 폭탄을 먼저 그려 팔과 머리가 앞에 오게 한다.
            Dome(48, 76, 16, P["roof"]);
            C.Ring(48, 76, 12, 13.5f, Bs.S);
            C.Disc(42, 82, 3, A(Sm.L, 150));
            C.Rect(45, 90, 6, 4, Bs.B);
            C.Line(50, 93, 58, 94, Wd.B, 2);
            Glow(60, 94, 5);
            C.Disc(60, 94, 2, Em.L);
            C.Line(34, 50, 36, 70, Fe.B, 5);
            C.Line(62, 50, 60, 70, Fe.B, 5);
            Dome(36, 70, 4, Fe);
            Dome(60, 70, 4, Fe);
            Box(36, 8, 8, 22, Fe);
            Box(52, 8, 8, 22, Fe);
            Box(34, 3, 11, 7, Wd);
            Box(51, 3, 11, 7, Wd);
            Box(32, 28, 32, 24, Wd);
            for (int x = 36; x <= 54; x += 6)
            {
                Box(x, 33, 5, 14, Fa);
                C.Rect(x, 46, 5, 2, Bs.L);
            }
            C.Rect(32, 30, 32, 3, Bs.S);
            RivetsH(34, 62, 31, 4, Bs.L);
            Dome(48, 59, 9, Fe);
            C.Rect(39, 58, 18, 4, O.B);
            C.Disc(43.5f, 60, 2.6f, Em.B);
            C.Disc(52.5f, 60, 2.6f, Em.B);
            C.Set(43, 61, Gl.L);
            C.Set(52, 61, Gl.L);
        }

        /// <summary>
        /// 몸을 가리는 큰 철제 방패와 투구 눈구멍이 보이는 방패수호병을 그린다.
        /// 현재 캔버스 C를 사용하며, 픽셀 버퍼를 변경한다.
        /// </summary>
        private static void DrawShieldGuard()
        {
            Box(32, 6, 11, 18, Fe);
            Box(53, 6, 11, 18, Fe);
            Box(30, 2, 14, 6, R);
            Box(52, 2, 14, 6, R);
            Box(24, 50, 48, 14, W);
            Dome(24, 58, 9, Bs);
            Dome(72, 58, 9, Bs);
            Box(37, 60, 22, 20, Fe);
            Dome(48, 78, 11, Fe);
            C.Rect(46, 84, 4, 9, Bs.B);
            C.Rect(46, 92, 4, 1, Bs.L);
            C.Rect(40, 69, 16, 3, O.S);
            C.Rect(42, 70, 12, 1, Gl.B);
            RivetsV(38, 62, 78, 4, Bs.L);
            RivetsV(57, 62, 78, 4, Bs.L);
            // 방패는 몸통 앞을 덮는 가장 큰 덩어리다.
            Box(18, 12, 60, 46, Fe);
            C.Rect(18, 12, 60, 3, Bs.S);
            C.Rect(18, 55, 60, 3, Bs.L);
            C.Rect(18, 12, 3, 46, Bs.B);
            C.Rect(75, 12, 3, 46, Bs.S);
            C.Rect(21, 26, 54, 2, Fe.S);
            C.Rect(21, 42, 54, 2, Fe.S);
            RivetsH(23, 73, 17, 5, Bs.L);
            RivetsH(23, 73, 52, 5, Bs.L);
            Gear(48, 35, 12, 12, Bs);
            C.Disc(48, 35, 3, Em.B);
            Box(76, 30, 7, 9, Cu);
        }

        /// <summary>
        /// 뿔 투구, 넓은 황동 어깨, 가슴 화로, 증기 망치를 든 근접 보스를 붉은 망토와 함께 그린다.
        /// 현재 캔버스 C를 사용하며, 픽셀 버퍼를 변경한다.
        /// </summary>
        private static void DrawWarlord()
        {
            Box(16, 14, 64, 52, Fa);
            for (int x = 20; x < 78; x += 7) C.Rect(x, 14, 2, 46, Fa.S);
            Box(27, 6, 15, 24, Fe);
            Box(54, 6, 15, 24, Fe);
            Box(25, 1, 19, 7, R);
            Box(52, 1, 19, 7, R);
            Box(23, 26, 50, 38, Fe);
            C.Rect(23, 28, 50, 3, Bs.S);
            Box(37, 34, 22, 16, R);
            C.Rect(39, 36, 18, 12, O.S);
            for (int x = 40; x <= 54; x += 4) C.Rect(x, 36, 2, 12, Em.B);
            Glow(48, 42, 10);
            C.Rect(41, 40, 14, 4, Em.L);
            RivetsH(26, 70, 60, 4, Bs.L);
            Box(36, 60, 24, 16, R);
            Dome(48, 74, 11, R);
            C.Line(38, 74, 26, 90, Sa.L, 4);
            C.Line(58, 74, 70, 90, Sa.L, 4);
            C.Line(38, 74, 26, 90, Sa.B, 2);
            C.Line(58, 74, 70, 90, Sa.B, 2);
            C.Rect(40, 66, 16, 4, O.S);
            C.Disc(48, 68, 3.5f, Em.L);
            C.Set(48, 68, Gl.L);
            Dome(19, 60, 13, Bs);
            Dome(77, 60, 13, Bs);
            C.Line(14, 68, 10, 78, Bs.L, 3);
            C.Line(82, 68, 86, 78, Bs.L, 3);
            Box(6, 24, 14, 28, Fe);
            Dome(13, 21, 9, Fe);
            C.Rect(8, 18, 10, 2, O.S);
            C.Line(82, 52, 86, 14, Wd.B, 4);
            Box(74, 4, 21, 15, R);
            C.Rect(74, 8, 21, 2, Bs.B);
            C.Rect(74, 14, 21, 2, Bs.B);
            C.Rect(76, 4, 2, 15, Fe.L);
        }

        /// <summary>
        /// 실크해트와 푸른 렌즈, 긴 코트, 어깨에 멘 장포신 대포와 탄약통을 가진 원거리 보스를 그린다.
        /// 현재 캔버스 C를 사용하며, 픽셀 버퍼를 변경한다.
        /// </summary>
        private static void DrawGunnerBoss()
        {
            Dome(28, 48, 11, Cu);
            C.Ring(28, 48, 6, 7.5f, Bs.B);
            C.Disc(28, 48, 2.5f, O.S);
            C.Line(42, 28, 34, 4, Fe.B, 4);
            C.Line(54, 28, 62, 4, Fe.B, 4);
            C.Line(48, 28, 48, 6, Cu.S, 3);
            Dome(34, 4, 4, R);
            Dome(62, 4, 4, R);
            Dome(48, 6, 3, R);
            Box(36, 24, 24, 36, Wa);
            C.Rect(47, 24, 2, 36, Wa.S);
            RivetsV(44, 28, 56, 5, Bs.L);
            RivetsV(51, 28, 56, 5, Bs.L);
            C.Rect(36, 40, 24, 3, Wd.S);
            Box(38, 58, 20, 14, Fe);
            C.Ring(51, 65, 3.5f, 5.5f, Bs.B);
            C.Disc(51, 65, 3.5f, Wa.L);
            C.Disc(50, 66, 1.6f, Sm.L);
            C.Rect(40, 64, 6, 2, O.S);
            C.Rect(34, 72, 28, 3, R.S);
            CylV(40, 75, 16, 12, R);
            C.Rect(40, 78, 16, 2, Bs.B);
            C.Line(58, 46, 66, 62, Wa.B, 4);
            // 어깨에서 오른쪽 위로 길게 뻗은 포신이 근접 보스와 다른 실루엣을 만든다.
            C.Line(56, 58, 86, 86, Fe.S, 9);
            C.Line(56, 59, 86, 87, Fe.B, 6);
            C.Line(55, 61, 85, 89, Fe.L, 2);
            C.Line(61, 69, 69, 61, Bs.B, 2);
            C.Line(73, 81, 81, 73, Bs.B, 2);
            Dome(87, 87, 6, Fe);
            C.Disc(88, 88, 2.5f, O.S);
            Puff(84, 93, 3, 170);
        }

        /// <summary>
        /// 생성한 PNG를 Point 필터·무압축 단일 스프라이트로 임포트한다.
        /// path를 사용하며, 텍스처 임포터 설정을 변경한다.
        /// </summary>
        private static void ConfigureSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Not imported: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SIZE;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 적 도트들을 배경색 위에 가로로 늘어놓고 PREVIEW_SCALE배로 키운 미리보기 PNG를 만든다.
        /// sprites를 사용하며, PNG 바이트를 반환한다.
        /// </summary>
        private static byte[] Preview(Color32[][] sprites)
        {
            int cell = SIZE + 8;
            int width = cell * sprites.Length * PREVIEW_SCALE;
            int height = cell * PREVIEW_SCALE;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var fill = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = x / PREVIEW_SCALE % cell - 4;
                    int sy = y / PREVIEW_SCALE - 4;
                    int index = x / PREVIEW_SCALE / cell;
                    Color32 pixel = sx >= 0 && sy >= 0 && sx < SIZE && sy < SIZE ? sprites[index][sy * SIZE + sx] : default;
                    fill[y * width + x] = PixelCanvas.Over(pixel, Bg.B);
                }
            texture.SetPixels32(fill);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }
    }
}
