using System;
using System.IO;

using UnityEngine;
using UnityEngine.UI;

using TMPro;
using UnityEditor;

using IronRing.Hud.Ui;

using Object = UnityEngine.Object;

namespace IronRing.Title.Editor
{
    /// <summary>
    /// 스펙 002 FR-014. 타이틀 초안 A안 배치(하단 선로·열차, 왼쪽 마스코트, 위 가운데 제목, 오른쪽 세로 버튼)와 슬롯 선택 패널을 가진 TitleCanvas 프리팹을 만든다.
    /// 이미 있으면 같은 경로에 덮어써 GUID를 유지한다. 색·스프라이트·글꼴은 UiTheme에서 읽는다.
    /// </summary>
    public static class TitleCanvasBuilder
    {
        /// <summary>프리팹 경로.</summary>
        public const string PREFAB = "Assets/Prefabs/UI/Title/TitleCanvas.prefab";
        /// <summary>슬롯 칸 수(스펙 003 수동 슬롯 수).</summary>
        public const int SLOT_COUNT = 3;

        private const string THEME = "Assets/Prefabs/UI/UiTheme.asset";
        private const string TITLE_FONT = "Assets/Fonts/Mulmaru SDF.asset";
        private const string LOCOMOTIVE = "Assets/Art/Generated/train_locomotive.png";
        private const string CARGO = "Assets/Art/Generated/train_cargo.png";
        private const string RAIL = "Assets/Art/Generated/rail_horizontal.png";
        private const string GROUND = "Assets/Art/Generated/tiles/tile_cobble.png";
        private const string MASCOT = "Assets/Art/Generated/mascot/mascot_default.png";

        private static readonly Color BRASS = new Color32(0xf0, 0xc2, 0x7a, 0xff);
        private static readonly Color SHADOW = new Color32(0x1e, 0x15, 0x22, 0xff);
        private static readonly Color BACK = new Color32(0x2a, 0x23, 0x31, 0xff);

        private static UiTheme _theme;
        private static TMP_FontAsset _titleFont;

        /// <summary>
        /// TitleCanvas 계층을 메모리에 만들고 컴포넌트 참조를 연결한 뒤 PREFAB 경로에 프리팹으로 저장한다.
        /// UiTheme·글꼴·열차·마스코트 에셋을 읽고, 하나라도 없으면 예외를 낸다.
        /// </summary>
        [MenuItem("IronRing/Title/Build Title Canvas")]
        public static void Generate()
        {
            _theme = Require(AssetDatabase.LoadAssetAtPath<UiTheme>(THEME), THEME);
            _titleFont = Require(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TITLE_FONT), TITLE_FONT);

            var root = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var rootRect = (RectTransform)root.transform;

            Stretch(Img(rootRect, "Background", null, BACK, Vector2.zero, Vector2.zero));
            Image ground = Img(rootRect, "Ground", LoadSprite(GROUND), Color.Lerp(Color.white, BACK, 0.68f), Vector2.zero, Vector2.zero);
            Stretch(ground);
            Tiled(ground, 1.4f);
            Image rail = Img(rootRect, "Rail", LoadSprite(RAIL), Color.white, new Vector2(0, -330), new Vector2(0, 180));
            rail.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rail.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            Tiled(rail, 0.556f);
            Train(rootRect, new Vector2(300, -330));
            Img(rootRect, "Mascot", LoadSprite(MASCOT), Color.white, new Vector2(-620, -40), new Vector2(520, 520));

            TMP_Text shadow = Label(rootRect, "GameTitleShadow", "title_game_name", new Vector2(6, 354), new Vector2(1400, 168), 120, SHADOW, _titleFont);
            TMP_Text title = Label(rootRect, "GameTitle", "title_game_name", new Vector2(0, 360), new Vector2(1400, 168), 120, BRASS, _titleFont);
            RectTransform menu = Rect(rootRect, "MenuButtons", new Vector2(560, 60), new Vector2(380, 340));
            Button newGame = MenuButton(menu, "NewGameButton", "title_new_game", new Vector2(0, 120), new Vector2(380, 96), true, out TMP_Text newGameLabel);
            Button cont = MenuButton(menu, "ContinueButton", "title_continue", new Vector2(0, 0), new Vector2(380, 96), false, out TMP_Text contLabel);
            Button quit = MenuButton(menu, "QuitButton", "title_quit", new Vector2(0, -120), new Vector2(380, 96), false, out TMP_Text quitLabel);

            SlotSelectPanel panel = BuildSlotPanel(rootRect);

            TitleScreen screen = root.AddComponent<TitleScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("_gameTitle").objectReferenceValue = title;
            so.FindProperty("_gameTitleShadow").objectReferenceValue = shadow;
            so.FindProperty("_newGameButton").objectReferenceValue = newGame;
            so.FindProperty("_continueButton").objectReferenceValue = cont;
            so.FindProperty("_quitButton").objectReferenceValue = quit;
            so.FindProperty("_newGameLabel").objectReferenceValue = newGameLabel;
            so.FindProperty("_continueLabel").objectReferenceValue = contLabel;
            so.FindProperty("_quitLabel").objectReferenceValue = quitLabel;
            so.FindProperty("_slotPanel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            if (existing != null) KeepLayout(existing.transform, rootRect);

            Directory.CreateDirectory(Path.GetDirectoryName(PREFAB));
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
            Object.DestroyImmediate(root);
            Debug.Log("TITLE: " + PREFAB + (existing != null ? " 갱신(기존 배치 유지)" : " 생성"));
        }

        /// <summary>
        /// 기존 프리팹 old에서 같은 경로의 RectTransform 값(앵커·피벗·위치·크기·회전·배율)을 새 계층 fresh에 옮겨, 제작자가 바꾼 배치를 유지한다(스펙 002 7절).
        /// 기존에 없던 오브젝트는 A안 처음 값을 그대로 둔다.
        /// </summary>
        private static void KeepLayout(Transform old, RectTransform fresh)
        {
            foreach (RectTransform target in fresh.GetComponentsInChildren<RectTransform>(true))
            {
                if (target == fresh) continue;
                string path = AnimationUtility.CalculateTransformPath(target, fresh);
                if (old.Find(path) is not RectTransform source) continue;
                target.anchorMin = source.anchorMin;
                target.anchorMax = source.anchorMax;
                target.pivot = source.pivot;
                target.anchoredPosition = source.anchoredPosition;
                target.sizeDelta = source.sizeDelta;
                target.localEulerAngles = source.localEulerAngles;
                target.localScale = source.localScale;
            }
        }

        /// <summary>
        /// root 아래에 처음엔 꺼진 슬롯 선택 패널(어둡게 덮기, 테두리, 제목, 슬롯 SLOT_COUNT칸, 불러오기·뒤로)을 만들고 참조를 연결해 돌려준다.
        /// </summary>
        private static SlotSelectPanel BuildSlotPanel(RectTransform root)
        {
            RectTransform panelRect = Rect(root, "SlotSelectPanel", Vector2.zero, Vector2.zero);
            Stretch(panelRect);
            Stretch(Img(panelRect, "Dim", null, new Color(0f, 0f, 0f, 0.8f), Vector2.zero, Vector2.zero));
            Sprite frameSprite = _theme.FrameTitledSprite != null ? _theme.FrameTitledSprite : _theme.PanelSprite;
            Image frame = Img(panelRect, "Frame", frameSprite, Color.white, new Vector2(0, -10), new Vector2(1100, 800));
            Sliced(frame);
            TMP_Text header = Label(frame.rectTransform, "Header", "title_continue", new Vector2(0, 330), new Vector2(600, 70), 52, BRASS, _titleFont);

            var rows = new SlotRowView[SLOT_COUNT];
            for (int i = 0; i < SLOT_COUNT; i++) rows[i] = SlotRow(frame.rectTransform, i, new Vector2(0, 170 - i * 150));

            Button load = MenuButton(frame.rectTransform, "LoadButton", "title_load", new Vector2(150, -300), new Vector2(280, 84), true, out TMP_Text loadLabel);
            Button back = MenuButton(frame.rectTransform, "BackButton", "title_back", new Vector2(-150, -300), new Vector2(280, 84), false, out TMP_Text backLabel);

            SlotSelectPanel panel = panelRect.gameObject.AddComponent<SlotSelectPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("_header").objectReferenceValue = header;
            SerializedProperty rowsProp = so.FindProperty("_rows");
            rowsProp.arraySize = SLOT_COUNT;
            for (int i = 0; i < SLOT_COUNT; i++) rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            so.FindProperty("_loadButton").objectReferenceValue = load;
            so.FindProperty("_loadLabel").objectReferenceValue = loadLabel;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.FindProperty("_backLabel").objectReferenceValue = backLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            panelRect.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>parent 아래 pos 위치에 index번 슬롯 칸(테두리 버튼, 번호 글자, 요약 글자, SlotRowView)을 만들어 돌려준다.</summary>
        private static SlotRowView SlotRow(RectTransform parent, int index, Vector2 pos)
        {
            Image frame = Img(parent, "Slot" + index, _theme.SlotSprite, Color.white, pos, new Vector2(920, 130));
            Sliced(frame);
            Button button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            TMP_Text label = Label(frame.rectTransform, "Label", "save_slot_label", new Vector2(-230, 22), new Vector2(420, 50), 34, _theme.Text, null, TextAlignmentOptions.Left);
            TMP_Text summary = Label(frame.rectTransform, "Summary", "save_slot_empty", new Vector2(0, -24), new Vector2(860, 50), 30, _theme.Text, null, TextAlignmentOptions.Left);

            SlotRowView view = frame.gameObject.AddComponent<SlotRowView>();
            var so = new SerializedObject(view);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_frame").objectReferenceValue = frame;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_summary").objectReferenceValue = summary;
            so.FindProperty("_textColor").colorValue = _theme.Text;
            so.FindProperty("_mutedColor").colorValue = _theme.Muted;
            so.FindProperty("_normalSprite").objectReferenceValue = _theme.SlotSprite;
            so.FindProperty("_selectedSprite").objectReferenceValue = _theme.SlotSelectedSprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>
        /// 기관차(머리가 오른쪽)와 뒤에 화물칸 두 량을 center 기준 가로로 눕혀 놓는다. 세로 원본 그림을 z축 -90도 돌린다.
        /// </summary>
        private static void Train(RectTransform root, Vector2 center)
        {
            RectTransform train = Rect(root, "Train", center, Vector2.zero);
            const float locoLen = 520f, cargoLen = 300f, gap = 12f;
            for (int i = 2; i >= 1; i--)
            {
                float x = -locoLen / 2 - gap - cargoLen * (i - 0.5f) - gap * (i - 1);
                Image car = Img(train, "Cargo" + i, LoadSprite(CARGO), Color.white, new Vector2(x, 0), new Vector2(cargoLen * 1024f / 1536f * 0.8f, cargoLen));
                car.rectTransform.localEulerAngles = new Vector3(0, 0, -90);
            }
            Image loco = Img(train, "Locomotive", LoadSprite(LOCOMOTIVE), Color.white, Vector2.zero, new Vector2(locoLen * 724f / 2172f, locoLen));
            loco.rectTransform.localEulerAngles = new Vector3(0, 0, -90);
        }

        /// <summary>
        /// parent 아래 테마 버튼(primary면 기본, 아니면 어두운 스프라이트)과 key 자리표시 글자를 만든다. 비활성 색은 반투명으로 둔다.
        /// 버튼을 돌려주고 글자는 label로 내보낸다.
        /// </summary>
        private static Button MenuButton(RectTransform parent, string name, string key, Vector2 pos, Vector2 size, bool primary, out TMP_Text label)
        {
            Sprite sprite = primary || _theme.ButtonDarkSprite == null ? _theme.ButtonSprite : _theme.ButtonDarkSprite;
            Image img = Img(parent, name, sprite, Color.white, pos, size);
            Sliced(img);
            Button button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
            label = Label(img.rectTransform, "Label", key, Vector2.zero, size, 40, _theme.Text, null);
            return button;
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

        /// <summary>parent 아래 sprite·color의 Image를 pos·size로 만들어 돌려준다. sprite가 있으면 비율을 유지한다.</summary>
        private static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size)
        {
            Image img = Rect(parent, name, pos, size).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = sprite != null;
            return img;
        }

        /// <summary>
        /// parent 아래 TMP 글자를 만든다. 자리표시 글자는 문구 키 key이고, 실행 중에 화면 클래스가 ko.json 값으로 바꾼다.
        /// fontSize·color·font(null이면 테마 글꼴)·정렬을 적용해 돌려준다.
        /// </summary>
        private static TMP_Text Label(Transform parent, string name, string key, Vector2 pos, Vector2 size, float fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI tmp = Rect(parent, name, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : _theme.Font;
            tmp.text = key;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>image의 RectTransform을 부모 전체로 늘린다.</summary>
        private static void Stretch(Image image) => Stretch(image.rectTransform);

        /// <summary>rt를 부모 전체로 늘린다.</summary>
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>image를 9분할 늘이기로 바꾸고 비율 유지를 끈다.</summary>
        private static void Sliced(Image image)
        {
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
        }

        /// <summary>image를 multiplier 배율의 바둑판 반복으로 바꾸고 비율 유지를 끈다.</summary>
        private static void Tiled(Image image, float multiplier)
        {
            image.type = Image.Type.Tiled;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = multiplier;
        }

        /// <summary>path의 Sprite를 읽어 돌려준다. 없으면 예외를 낸다.</summary>
        private static Sprite LoadSprite(string path) => Require(AssetDatabase.LoadAssetAtPath<Sprite>(path), path);

        /// <summary>asset이 null이면 path를 담은 예외를 내고, 아니면 그대로 돌려준다.</summary>
        private static T Require<T>(T asset, string path) where T : Object
        {
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
            return asset;
        }
    }
}
