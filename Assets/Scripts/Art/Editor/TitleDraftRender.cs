using System;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Hud.Ui;

using Object = UnityEngine.Object;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 타이틀 화면 이미지 초안(스펙 002 작성 전 검토용). Main.unity를 건드리지 않는 미리보기 씬에 UGUI·TMP로 화면을 짜서 1920×1080 PNG로 찍는다.
    /// 타이틀 3안(A·B·C), 이어하기 슬롯 선택 화면, 비교 시트를 Assets/Art/Generated/title/에 저장한다. 버튼 문구는 초안용 임시 글자다.
    /// </summary>
    public static class TitleDraftRender
    {
        /// <summary>출력 폴더.</summary>
        public const string OUTPUT = "Assets/Art/Generated/title/";
        /// <summary>렌더 크기.</summary>
        public const int WIDTH = 1920, HEIGHT = 1080;

        private const string THEME = "Assets/Prefabs/UI/UiTheme.asset";
        private const string TITLE_FONT = "Assets/Fonts/Mulmaru SDF.asset";
        private const string LOCOMOTIVE = "Assets/Art/Generated/train_locomotive.png";
        private const string CARGO = "Assets/Art/Generated/train_cargo.png";
        private const string RAIL = "Assets/Art/Generated/rail_horizontal.png";
        private const string GROUND = "Assets/Art/Generated/tiles/tile_cobble.png";
        private const string MASCOT = "Assets/Art/Generated/mascot/mascot_default.png";
        private const string TITLE = "황동골 철도 회사";

        private static readonly Color BRASS = new Color32(0xf0, 0xc2, 0x7a, 0xff);
        private static readonly Color SHADOW = new Color32(0x1e, 0x15, 0x22, 0xff);
        private static readonly Color BACK = new Color32(0x2a, 0x23, 0x31, 0xff);

        private static UiTheme _theme;
        private static TMP_FontAsset _titleFont;

        /// <summary>
        /// 타이틀 3안, 슬롯 선택 화면, 비교 시트를 렌더해 OUTPUT에 PNG로 저장한다.
        /// UiTheme·폰트·열차·마스코트 에셋을 읽고, OUTPUT 폴더의 PNG를 바꾼다.
        /// </summary>
        [MenuItem("IronRing/Art/Render Title Drafts")]
        public static void Generate()
        {
            _theme = AssetDatabase.LoadAssetAtPath<UiTheme>(THEME);
            _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TITLE_FONT);
            Directory.CreateDirectory(OUTPUT);
            Texture2D a = Render(LayoutA), b = Render(LayoutB), c = Render(LayoutC), s = Render(LayoutSlots);
            Save("title_a.png", a); Save("title_b.png", b); Save("title_c.png", c); Save("title_slots.png", s);
            Texture2D sheet = Render(root => LayoutCompare(root, new[] { a, b, c, s }));
            Save("title_compare.png", sheet);
            foreach (Texture2D t in new[] { a, b, c, s, sheet }) Object.DestroyImmediate(t);
            AssetDatabase.Refresh();
            Debug.Log("TITLE_DRAFT: " + OUTPUT + " title_a/b/c, title_slots, title_compare");
        }

        // ---------------------------------------------------------------- 안별 배치

        /// <summary>A안: 하단 선로를 가로지르는 열차, 왼쪽 큰 마스코트, 가운데 위 제목, 오른쪽 버튼 열을 root 아래에 만든다.</summary>
        private static void LayoutA(RectTransform root)
        {
            Ground(root, 0.68f);
            Rail(root, -330);
            Train(root, new Vector2(300, -330), 1f);
            Mascot(root, new Vector2(-620, -40), 520, false);
            TitleText(root, new Vector2(0, 360), 120);
            ButtonColumn(root, new Vector2(560, 60), 380);
        }

        /// <summary>B안: 위쪽 선로에 열차, 가운데 제목과 세로 버튼, 오른쪽 아래 마스코트를 root 아래에 만든다.</summary>
        private static void LayoutB(RectTransform root)
        {
            Ground(root, 0.65f);
            Rail(root, 380);
            Train(root, new Vector2(-100, 380), 0.85f);
            TitleText(root, new Vector2(0, 160), 130);
            ButtonColumn(root, new Vector2(0, -170), 420);
            Mascot(root, new Vector2(700, -330), 400, true);
        }

        /// <summary>C안: 왼쪽 메뉴 패널(제목·버튼), 오른쪽 정류장 선로에 정차한 열차와 왼쪽을 보는 마스코트를 root 아래에 만든다.</summary>
        private static void LayoutC(RectTransform root)
        {
            Ground(root, 0.72f);
            Rail(root, -40);
            Train(root, new Vector2(560, -40), 1f);
            Mascot(root, new Vector2(620, -330), 400, true);
            Image panel = Img(root, "MenuPanel", _theme.PanelSprite, Color.white, new Vector2(-560, 0), new Vector2(640, 980));
            panel.type = Image.Type.Sliced;
            panel.preserveAspect = false;
            TitleText(panel.rectTransform, new Vector2(0, 300), 70);
            ButtonColumn(panel.rectTransform, new Vector2(0, -120), 400);
        }

        /// <summary>이어하기 슬롯 선택 화면: 어둡게 깐 A안 배경 위에 슬롯 3칸, 불러오기·뒤로 버튼 패널을 root 아래에 만든다.</summary>
        private static void LayoutSlots(RectTransform root)
        {
            LayoutA(root);
            Img(root, "Dim", null, new Color(0f, 0f, 0f, 0.8f), Vector2.zero, new Vector2(WIDTH, HEIGHT));
            Sprite frame = _theme.FrameTitledSprite != null ? _theme.FrameTitledSprite : _theme.PanelSprite;
            Image panel = Img(root, "SlotPanel", frame, Color.white, new Vector2(0, -10), new Vector2(1100, 800));
            panel.type = Image.Type.Sliced;
            panel.preserveAspect = false;
            Label(panel.rectTransform, "이어하기", new Vector2(0, 330), new Vector2(600, 70), 52, BRASS, _titleFont);
            string[] head = { "슬롯 1", "슬롯 2", "슬롯 3" };
            string[] body = { "황동골 2단계 · 동전 340 · 승객 3명", "황동골 1단계 · 동전 85 · 승객 1명", "비어 있음" };
            string[] when = { "2026-11-05 21:30", "2026-11-04 18:02", "" };
            for (int i = 0; i < 3; i++)
            {
                Sprite slot = i == 0 ? _theme.SlotSelectedSprite : _theme.SlotSprite;
                Image row = Img(panel.rectTransform, "Slot" + i, slot, Color.white, new Vector2(0, 170 - i * 150), new Vector2(920, 130));
                row.type = Image.Type.Sliced;
                row.preserveAspect = false;
                Color text = i == 2 ? _theme.Muted : _theme.Text;
                Label(row.rectTransform, head[i], new Vector2(-330, 22), new Vector2(220, 50), 34, i == 2 ? _theme.Muted : BRASS, null, TextAlignmentOptions.Left);
                Label(row.rectTransform, body[i], new Vector2(40, -24), new Vector2(820, 50), 30, text, null, TextAlignmentOptions.Left);
                Label(row.rectTransform, when[i], new Vector2(250, 22), new Vector2(360, 50), 26, _theme.Muted, null, TextAlignmentOptions.Right);
            }
            Button(panel.rectTransform, "불러오기", new Vector2(150, -300), new Vector2(280, 84), true);
            Button(panel.rectTransform, "뒤로", new Vector2(-150, -300), new Vector2(280, 84), false);
        }

        /// <summary>렌더한 textures 4장을 2×2로 늘어놓고 각 칸에 안 이름을 붙인 비교 시트를 root 아래에 만든다.</summary>
        private static void LayoutCompare(RectTransform root, Texture2D[] textures)
        {
            Img(root, "Back", null, BACK, Vector2.zero, new Vector2(WIDTH, HEIGHT));
            string[] names = { "A안", "B안", "C안", "이어하기 슬롯 선택" };
            for (int i = 0; i < textures.Length; i++)
            {
                var pos = new Vector2(i % 2 == 0 ? -478 : 478, i < 2 ? 262 : -278);
                RectTransform cell = Rect(root, "Cell" + i, pos, new Vector2(WIDTH * 0.48f, HEIGHT * 0.48f));
                cell.gameObject.AddComponent<RawImage>().texture = textures[i];
                Label(root, names[i], pos + new Vector2(-255, 232), new Vector2(400, 44), 32, BRASS, _titleFont, TextAlignmentOptions.Left);
            }
        }

        // ---------------------------------------------------------------- 부품

        /// <summary>root 전체에 어둡게 칠한 돌바닥 타일을 깐다. shade가 클수록 어둡다.</summary>
        private static void Ground(RectTransform root, float shade)
        {
            Img(root, "Back", null, BACK, Vector2.zero, new Vector2(WIDTH, HEIGHT));
            Image tile = Img(root, "Ground", Load(GROUND), Color.Lerp(Color.white, BACK, shade), Vector2.zero, new Vector2(WIDTH, HEIGHT));
            tile.type = Image.Type.Tiled;
            tile.preserveAspect = false;
            tile.pixelsPerUnitMultiplier = 1.4f;
        }

        /// <summary>y 높이에 화면 가로 전체를 잇는 선로 타일을 깐다.</summary>
        private static void Rail(RectTransform root, float y)
        {
            Image rail = Img(root, "Rail", Load(RAIL), Color.white, new Vector2(0, y), new Vector2(WIDTH, 180));
            rail.type = Image.Type.Tiled;
            rail.preserveAspect = false;
            rail.pixelsPerUnitMultiplier = 0.556f;
        }

        /// <summary>
        /// 기관차(머리가 오른쪽)와 뒤에 화물칸 두 량을 center 기준으로 가로로 눕혀 놓는다. scale은 전체 크기 배율이다.
        /// 세로 원본 그림을 z축 -90도 돌려 붙인다.
        /// </summary>
        private static void Train(RectTransform root, Vector2 center, float scale)
        {
            float locoLen = 520 * scale, cargoLen = 300 * scale, gap = 12 * scale;
            var locoSize = new Vector2(locoLen * 724f / 2172f, locoLen);
            var cargoSize = new Vector2(cargoLen * 1024f / 1536f * 0.8f, cargoLen);
            for (int i = 2; i >= 1; i--)
            {
                float cx = center.x - locoLen / 2 - gap - cargoLen * (i - 0.5f) - gap * (i - 1);
                Image car = Img(root, "Cargo" + i, Load(CARGO), Color.white, new Vector2(cx, center.y), cargoSize);
                car.rectTransform.localEulerAngles = new Vector3(0, 0, -90);
            }
            Image loco = Img(root, "Locomotive", Load(LOCOMOTIVE), Color.white, center, locoSize);
            loco.rectTransform.localEulerAngles = new Vector3(0, 0, -90);
        }

        /// <summary>마스코트 기본 표정을 pos에 size 크기로 놓는다. flip이면 좌우를 뒤집어 왼쪽을 보게 한다.</summary>
        private static void Mascot(RectTransform root, Vector2 pos, float size, bool flip)
        {
            Image img = Img(root, "Mascot", Load(MASCOT), Color.white, pos, new Vector2(size, size));
            if (flip) img.rectTransform.localScale = new Vector3(-1, 1, 1);
        }

        /// <summary>게임 제목을 그림자 한 겹과 함께 pos에 size 글자 크기로 쓴다.</summary>
        private static void TitleText(RectTransform root, Vector2 pos, float size)
        {
            Label(root, TITLE, pos + new Vector2(6, -6), new Vector2(1400, size * 1.4f), size, SHADOW, _titleFont);
            Label(root, TITLE, pos, new Vector2(1400, size * 1.4f), size, BRASS, _titleFont);
        }

        /// <summary>새 게임·이어하기·종료 버튼을 center 기준 세로로 width 폭으로 놓는다.</summary>
        private static void ButtonColumn(RectTransform root, Vector2 center, float width)
        {
            string[] labels = { "새 게임", "이어하기", "종료" };
            for (int i = 0; i < labels.Length; i++) Button(root, labels[i], center + new Vector2(0, 120 - i * 120), new Vector2(width, 96), i == 0);
        }

        /// <summary>테마 버튼 스프라이트(primary면 기본, 아니면 어두운 버튼)에 label을 얹은 버튼 모양을 pos에 size로 만든다.</summary>
        private static void Button(RectTransform root, string label, Vector2 pos, Vector2 size, bool primary)
        {
            Sprite sprite = primary || _theme.ButtonDarkSprite == null ? _theme.ButtonSprite : _theme.ButtonDarkSprite;
            Image img = Img(root, "Button_" + label, sprite, Color.white, pos, size);
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            Label(img.rectTransform, label, Vector2.zero, size, 40, _theme.Text, null);
        }

        // ---------------------------------------------------------------- 기본 요소

        /// <summary>parent 아래 가운데 기준 pos·size의 RectTransform을 만들어 돌려준다.</summary>
        private static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>parent 아래 sprite·color의 Image를 pos·size로 만들어 돌려준다(sprite가 있으면 비율 유지).</summary>
        private static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size)
        {
            Image img = Rect(parent, name, pos, size).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = sprite != null;
            return img;
        }

        /// <summary>parent 아래 text를 pos·size 상자에 fontSize·color·font(null이면 테마 글꼴)·정렬로 쓰는 TMP 글자를 만들어 돌려준다.</summary>
        private static TextMeshProUGUI Label(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI tmp = Rect(parent, "Text", pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : _theme.Font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        /// <summary>path의 Sprite를 읽어 돌려준다.</summary>
        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        // ---------------------------------------------------------------- 렌더

        /// <summary>
        /// 미리보기 씬에 직교 카메라와 1920×1080 월드 공간 캔버스를 만들고 build로 화면을 채운 뒤 찍은 텍스처를 돌려준다.
        /// 미리보기 씬은 렌더 뒤 닫으므로 열린 씬은 바뀌지 않는다.
        /// </summary>
        private static Texture2D Render(Action<RectTransform> build)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var camGo = new GameObject("DraftCamera");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            Camera cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = HEIGHT / 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BACK;
            cam.transform.position = new Vector3(0, 0, -100);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            var rt = new RenderTexture(WIDTH, HEIGHT, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var canvasGo = new GameObject("DraftCanvas", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var root = (RectTransform)canvasGo.transform;
            root.sizeDelta = new Vector2(WIDTH, HEIGHT);
            root.position = Vector3.zero;
            build(root);

            // TMP 글자 메시는 편집 모드에서 바로 생기지 않으므로 강제로 만든 뒤 찍는다.
            Canvas.ForceUpdateCanvases();
            foreach (TextMeshProUGUI tmp in canvasGo.GetComponentsInChildren<TextMeshProUGUI>()) tmp.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            EditorSceneManager.ClosePreviewScene(scene);
            return tex;
        }

        /// <summary>tex를 OUTPUT/name PNG로 저장한다.</summary>
        private static void Save(string name, Texture2D tex) => File.WriteAllBytes(OUTPUT + name, tex.EncodeToPNG());
    }
}
