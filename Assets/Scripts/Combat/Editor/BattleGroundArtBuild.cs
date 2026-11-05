using System;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Art.Editor;

namespace IronRing.Combat.Editor
{
    /// <summary>
    /// 위에서 내려다본 폐허 흙바닥 전장 그림(Assets/Art/Generated/Battle/battlefield_topdown.png)을 그리고,
    /// BattleReference 씬의 전장 배경(Battle Scenery/Ruined Battlefield)에 연결한다. 색은 SteampunkPalette.json에서 읽는다.
    /// </summary>
    public static class BattleGroundArtBuild
    {
        private const int WIDTH = 736;
        private const int HEIGHT = 416;
        private const int PIXELS_PER_UNIT = 32;
        private const string OUTPUT = "Assets/Art/Generated/Battle/battlefield_topdown.png";
        private const string SCENE = "Assets/Scenes/BattleReference.unity";
        private const string SCENERY_ROOT = "Battle Scenery";
        private const string BACKDROP = "Ruined Battlefield";
        private const int RAIL_BAND_MIN = 160;
        private const int RAIL_BAND_MAX = 240;

        private static Color32[] _pixels;
        private static SteampunkPalette _palette;

        /// <summary>
        /// 흙바닥·다져진 길·균열·자갈·마른 풀·바위·포격 구덩이를 그려 탑뷰 전장 PNG를 저장하고 스프라이트로 임포트한다.
        /// 팔레트 파일을 사용하며, OUTPUT 파일을 새로 쓴다.
        /// </summary>
        [MenuItem("IronRing/Art/Generate Battle Top-Down Ground")]
        public static void Generate()
        {
            _palette = SteampunkPalette.Load();
            _pixels = new Color32[WIDTH * HEIGHT];
            DrawSoil();
            DrawCracks(40);
            DrawCraters(7);
            DrawRocks(30);
            DrawGrass(120);
            DrawPebbles(520);
            var texture = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
            texture.SetPixels32(_pixels);
            texture.Apply();
            File.WriteAllBytes(OUTPUT, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            _pixels = null;
            AssetDatabase.ImportAsset(OUTPUT, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(OUTPUT);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PIXELS_PER_UNIT;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            Debug.Log("BATTLE ART: generated top-down battlefield " + OUTPUT);
        }

        /// <summary>
        /// BattleReference 씬의 전장 배경 스프라이트를 탑뷰 땅 그림으로 바꾸고, 기존 배경과 같은 크기를 덮도록 배율을 맞춘 뒤 씬을 저장한다.
        /// SCENE과 OUTPUT을 사용하며, 배경 오브젝트의 sprite와 localScale을 변경한다.
        /// </summary>
        [MenuItem("IronRing/Battle/Apply Top-Down Ground")]
        public static void ApplyToBattleScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before changing the battle background.");
            Sprite ground = AssetDatabase.LoadAssetAtPath<Sprite>(OUTPUT);
            if (ground == null) throw new InvalidOperationException("Missing " + OUTPUT + "; run Generate first.");
            Scene scene = SceneManager.GetSceneByPath(SCENE);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException("BattleReference has unsaved changes; save it first.");
            SpriteRenderer backdrop = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != SCENERY_ROOT) continue;
                Transform node = root.transform.Find(BACKDROP);
                if (node != null) backdrop = node.GetComponent<SpriteRenderer>();
            }
            if (backdrop == null) throw new InvalidOperationException("Battle backdrop missing.");
            // 기존 배경이 덮던 월드 크기를 유지해 카메라 구도와 전장 범위를 바꾸지 않는다.
            Vector3 coveredSize = backdrop.bounds.size;
            backdrop.sprite = ground;
            backdrop.transform.localScale = new Vector3(coveredSize.x / ground.bounds.size.x, coveredSize.y / ground.bounds.size.y, 1f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("BATTLE ART: applied top-down ground to " + SCENE);
        }

        /// <summary>
        /// 넓은 잡음과 중간 잡음을 단계별로 끊어 칠해 도트 느낌의 흙바닥과 다져진 길 자국을 채운다.
        /// _palette를 사용하며, _pixels 전체를 변경한다.
        /// </summary>
        private static void DrawSoil()
        {
            Swatch brick = _palette["brick"];
            Swatch sand = _palette["sandstone"];
            Swatch wood = _palette["wood"];
            // 벽돌색만 쓰면 보랏빛이 돌아 나무색을 섞어 따뜻한 흙색으로 맞춘다.
            Color32[] levels =
            {
                Color32.Lerp(brick.S, wood.S, 0.55f),
                Color32.Lerp(brick.S, wood.B, 0.5f),
                Color32.Lerp(brick.B, wood.L, 0.45f),
                Color32.Lerp(brick.B, sand.S, 0.6f),
                sand.S
            };
            for (int y = 0; y < HEIGHT; y++)
                for (int x = 0; x < WIDTH; x++)
                {
                    float broad = Mathf.PerlinNoise(x * 0.006f + 11.3f, y * 0.007f + 3.1f);
                    float mid = Mathf.PerlinNoise(x * 0.03f + 47.9f, y * 0.032f + 15.4f);
                    float tone = Mathf.Clamp01(broad * 0.6f + mid * 0.32f + Hash(x, y, 1) * 0.08f);
                    // 단계를 끊어 칠해 부드러운 그라데이션 대신 도트 명암 띠를 만든다.
                    int level = Mathf.Clamp(Mathf.FloorToInt(tone * levels.Length), 0, levels.Length - 1);
                    Color32 color = levels[level];
                    float path = Mathf.PerlinNoise(x * 0.012f + 81.5f, y * 0.05f + 7.7f);
                    if (path > 0.66f) color = Color32.Lerp(color, sand.B, 0.45f);
                    _pixels[y * WIDTH + x] = color;
                }
        }

        /// <summary>
        /// 무작위 꺾은선으로 마른 흙의 균열을 count개 그린다.
        /// count와 고정 해시를 사용하며, _pixels를 변경한다.
        /// </summary>
        private static void DrawCracks(int count)
        {
            Color32 crack = Color32.Lerp(Color32.Lerp(_palette["brick"].S, _palette["wood"].S, 0.5f), _palette["outline"].B, 0.3f);
            for (int k = 0; k < count; k++)
            {
                float x = Hash(k, 0, 11) * WIDTH;
                float y = Hash(k, 1, 11) * HEIGHT;
                float angle = Hash(k, 2, 11) * Mathf.PI * 2f;
                int length = 18 + (int)(Hash(k, 3, 11) * 46);
                for (int step = 0; step < length; step++)
                {
                    Set((int)x, (int)y, crack);
                    angle += (Hash(k, step, 12) - 0.5f) * 0.9f;
                    x += Mathf.Cos(angle);
                    y += Mathf.Sin(angle);
                    // 일정 확률로 짧은 가지를 쳐 거미줄 같은 균열을 만든다.
                    if (Hash(k, step, 13) > 0.93f) Branch((int)x, (int)y, angle + 1.2f, 6 + (int)(Hash(k, step, 14) * 8), crack, k + step);
                }
            }
        }

        /// <summary>
        /// x, y에서 angle 방향으로 length만큼 짧은 균열 가지를 그린다.
        /// 시작 위치, 방향, 길이, 색, 해시 salt를 사용하며, _pixels를 변경한다.
        /// </summary>
        private static void Branch(int x, int y, float angle, int length, Color32 color, int salt)
        {
            float px = x;
            float py = y;
            for (int step = 0; step < length; step++)
            {
                Set((int)px, (int)py, color);
                angle += (Hash(salt, step, 15) - 0.5f) * 0.8f;
                px += Mathf.Cos(angle);
                py += Mathf.Sin(angle);
            }
        }

        /// <summary>
        /// 어두운 바닥과 밝은 가장자리를 가진 포격 구덩이를 선로 띠 밖에 count개 그린다.
        /// count와 고정 해시를 사용하며, _pixels를 변경한다.
        /// </summary>
        private static void DrawCraters(int count)
        {
            Swatch brick = _palette["brick"];
            Swatch sand = _palette["sandstone"];
            Color32 floor = Color32.Lerp(brick.S, _palette["outline"].B, 0.4f);
            for (int k = 0; k < count; k++)
            {
                float cx = 30 + Hash(k, 0, 21) * (WIDTH - 60);
                float cy = OutsideRailBand(Hash(k, 1, 21));
                float r = 9 + Hash(k, 2, 21) * 12;
                for (int y = (int)(cy - r - 3); y <= cy + r + 3; y++)
                    for (int x = (int)(cx - r - 3); x <= cx + r + 3; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float dy = y + 0.5f - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                        // 왼쪽 위에서 빛을 받아 위쪽 가장자리는 밝고 아래쪽 안쪽은 어둡다.
                        float light = (-dx + dy) / (r * 1.4f);
                        if (d <= 0.75f) Set(x, y, light < 0f ? floor : Color32.Lerp(floor, brick.S, 0.5f));
                        else if (d <= 1.0f) Set(x, y, light > 0.15f ? sand.B : brick.S);
                        else if (d <= 1.25f && Hash(x, y, 22) > 0.55f) Set(x, y, sand.S);
                    }
            }
        }

        /// <summary>
        /// 위쪽은 밝고 아래쪽은 그늘진 작은 바위를 선로 띠 밖에 count개 그린다.
        /// count와 고정 해시를 사용하며, _pixels를 변경한다.
        /// </summary>
        private static void DrawRocks(int count)
        {
            Swatch sand = _palette["sandstone"];
            Color32 outline = _palette["outline"].B;
            for (int k = 0; k < count; k++)
            {
                int cx = 8 + (int)(Hash(k, 0, 31) * (WIDTH - 16));
                int cy = (int)OutsideRailBand(Hash(k, 1, 31));
                int w = 7 + (int)(Hash(k, 2, 31) * 9);
                int h = 5 + (int)(Hash(k, 3, 31) * 5);
                for (int y = -1; y <= h; y++)
                    for (int x = -1; x <= w; x++)
                    {
                        bool edge = x < 0 || y < 0 || x == w || y == h;
                        if (edge && (x < 0 || x == w) && (y < 0 || y == h)) continue;
                        Set(cx + x, cy + y, edge ? outline : y >= h - 1 ? sand.L : y == 0 ? sand.S : sand.B);
                    }
                for (int x = 0; x < w; x++) Set(cx + x, cy - 2, Color32.Lerp(_palette["brick"].S, outline, 0.4f));
            }
        }

        /// <summary>
        /// 이끼색과 덤불색으로 작은 V자 마른 풀 포기를 선로 띠 밖에 count개 그린다.
        /// count와 고정 해시를 사용하며, _pixels를 변경한다.
        /// </summary>
        private static void DrawGrass(int count)
        {
            Swatch moss = _palette["moss"];
            Swatch shrub = _palette["shrub"];
            for (int k = 0; k < count; k++)
            {
                int cx = (int)(Hash(k, 0, 41) * WIDTH);
                int cy = (int)OutsideRailBand(Hash(k, 1, 41));
                int blades = 4 + (int)(Hash(k, 2, 41) * 4);
                Color32 dry = _palette["glow"].S;
                for (int b = 0; b < blades; b++)
                {
                    int lean = b - blades / 2;
                    int height = 5 + (int)(Hash(k, b, 42) * 4);
                    for (int s = 0; s < height; s++)
                    {
                        // 끝은 마른 노란빛, 아래는 이끼색으로 칠해 시든 풀포기로 보이게 한다.
                        Color32 color = s >= height - 2 ? dry : b % 2 == 0 ? moss.B : shrub.S;
                        Set(cx + lean * s / 3, cy + s, color);
                        Set(cx + lean * s / 3 + 1, cy + s, s == 0 ? moss.S : color);
                    }
                }
            }
        }

        /// <summary>
        /// 그림자 한 점을 가진 1~2픽셀 자갈을 count개 흩뿌린다.
        /// count와 고정 해시를 사용하며, _pixels를 변경한다.
        /// </summary>
        private static void DrawPebbles(int count)
        {
            Swatch ground = _palette["ground"];
            Swatch sand = _palette["sandstone"];
            for (int k = 0; k < count; k++)
            {
                int x = (int)(Hash(k, 0, 51) * WIDTH);
                int y = (int)(Hash(k, 1, 51) * HEIGHT);
                Color32 stone = Hash(k, 2, 51) > 0.5f ? ground.L : sand.L;
                Set(x, y, stone);
                if (Hash(k, 3, 51) > 0.6f) Set(x + 1, y, stone);
                Set(x, y - 1, ground.S);
            }
        }

        /// <summary>
        /// 0~1 값을 열차·선로가 지나는 가운데 띠를 피한 세로 위치로 바꾼다.
        /// t와 RAIL_BAND_MIN·RAIL_BAND_MAX를 사용하며, 띠 위나 아래의 y를 반환한다.
        /// </summary>
        private static float OutsideRailBand(float t)
        {
            float below = RAIL_BAND_MIN - 12;
            float above = HEIGHT - RAIL_BAND_MAX - 12;
            float y = t * (below + above);
            return y < below ? 6 + y : RAIL_BAND_MAX + 6 + (y - below);
        }

        /// <summary>
        /// 범위 안의 x, y 픽셀에 color를 칠한다.
        /// 위치와 색을 사용하며, 범위 밖이면 아무것도 바꾸지 않는다.
        /// </summary>
        private static void Set(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= WIDTH || y >= HEIGHT) return;
            _pixels[y * WIDTH + x] = color;
        }

        /// <summary>
        /// x, y, salt로 반복 가능한 0~1 잡음값을 만든다.
        /// 세 정수를 사용하며, 같은 입력이면 같은 값을 반환한다.
        /// </summary>
        private static float Hash(int x, int y, int salt)
        {
            return PixelCanvas.Hash(x, y, salt) / (float)int.MaxValue;
        }
    }
}
