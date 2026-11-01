using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Npc;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>
    /// 스토리 029: 상황 화면 "NpcDialog"(옛 온라인 게임식 대화 장면) 프리팹 내용과 부품 DialogOption을 만든다.
    /// 자식 순서(뒤 → 앞): Dim(전체 그늘, 클릭 = 닫기) → Dialog(아래 가운데 상자: Simple 패널 + 안쪽 1px 황동 줄, 이름·집/출신·대사·버튼 줄) → Portrait(왼쪽 아래 큰 초상).
    /// 크기는 캔버스 기준 해상도(1280×720)의 비율과 테마(글자 줄 높이·버튼 높이·패널 여백)에서 계산한다. 선택지는 런타임에 부품에서 만든다.
    /// </summary>
    public static class NpcDialogScreenBuilder
    {
        /// <summary>선택지 버튼 부품 경로.</summary>
        public const string OPTION_PART = BuildMainCanvas.PARTS_FOLDER + "/DialogOption.prefab";
        /// <summary>캔버스 기준 해상도(BuildMainCanvas의 CanvasScaler와 같음).</summary>
        private static readonly Vector2 REFERENCE = new Vector2(1280, 720);
        /// <summary>초상 높이 = 기준 높이의 비율(스토리 029 AC: 55%).</summary>
        private const float PORTRAIT_HEIGHT_FRACTION = 0.55f;
        /// <summary>초상 원본 비율(portrait_&lt;id&gt;.png 96×128). 런타임에 실제 스프라이트 비율로 다시 맞춘다.</summary>
        private const float PORTRAIT_ASPECT = 96f / 128f;
        /// <summary>초상의 화면 왼쪽 여백.</summary>
        private const float PORTRAIT_LEFT = 16;
        /// <summary>대화 상자 폭 = 기준 폭의 비율(AC: 70%).</summary>
        private const float BOX_WIDTH_FRACTION = 0.7f;
        /// <summary>대화 상자 아래 여백.</summary>
        private const float BOX_BOTTOM = 24;
        /// <summary>대화 상자 최소 높이.</summary>
        private const float BOX_MIN_HEIGHT = 190;
        /// <summary>안쪽 황동 줄의 패널 가장자리로부터 들임과 두께.</summary>
        private const float INNER_INSET = 4, INNER_LINE = 1;
        /// <summary>대사 최대 줄 수.</summary>
        private const int BODY_LINES = 4;
        /// <summary>줄 사이 간격, 초상과 글자 사이 간격.</summary>
        private const float GAP = 6, PORTRAIT_GAP = 16;
        /// <summary>[계속하기]·[닫기] 버튼 폭, 선택지 부품 폭.</summary>
        private const float ACTION_WIDTH = 120, OPTION_WIDTH = 120;
        /// <summary>그늘 기본 알파(런타임에 NpcDialogScreen._dialogDimAlpha로 덮어씀).</summary>
        private const float DIM_ALPHA = 0.35f;
        /// <summary>대화 상자 받침 알파(1 = 불투명, 뒤의 핫바가 비치지 않음).</summary>
        private const float BOX_BACKING_ALPHA = 1f;

        /// <summary>화면 루트(UiScreen "NpcDialog", 상황 화면) + NpcDialogScreen + 그늘·상자·초상.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject optionPart = UiKit.SavePrefab(OPTION_PART, () => Option(theme), ConsumeMarker("DialogOption"));

            GameObject root = UiKit.ScreenRoot("NpcDialog", false);
            NpcDialogScreen screen = root.AddComponent<NpcDialogScreen>();

            // 1) 전체 그늘: 클릭을 받아 아래 세계(배치·NPC 클릭)를 막고, 누르면 닫는다.
            Image dim = UiKit.Image(root.transform, "Dim", null, new Color(0f, 0f, 0f, DIM_ALPHA), true);
            UiKit.Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.navigation = new Navigation { mode = Navigation.Mode.None };

            // 2) 아래 가운데 대화 상자
            RectOffset pad = UiKit.FramePad(theme);
            float heading = theme.LineHeight(UiTextSize.Heading);
            float body = theme.LineHeight(UiTextSize.Normal) * BODY_LINES;
            float boxWidth = REFERENCE.x * BOX_WIDTH_FRACTION;
            float boxHeight = Mathf.Max(BOX_MIN_HEIGHT, pad.top + heading + GAP + body + GAP + theme.ButtonHeight + pad.bottom);
            // 불투명 받침: Simple 패널 스프라이트 자체가 반투명(알파 224)이라 뒤의 핫바가 비친다. 같은 자리에 테마 Panel 색(알파 BOX_BACKING_ALPHA)을 먼저 깐다.
            Color backingColor = theme.Panel; backingColor.a = BOX_BACKING_ALPHA;
            Image backing = UiKit.Image(root.transform, "DialogBacking", null, backingColor, false);
            UiKit.Place(backing.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, BOX_BOTTOM + 1), new Vector2(boxWidth - 2, boxHeight - 2));
            Image panel = UiKit.Panel(root.transform, "Dialog", theme);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, BOX_BOTTOM), new Vector2(boxWidth, boxHeight));
            RectTransform inner = UiKit.Stretch(UiKit.Node(panel.transform, "InnerLine"), INNER_INSET, INNER_INSET, INNER_INSET, INNER_INSET);
            Edge(inner, "Top", new Vector2(0, 1), new Vector2(1, 1), theme);
            Edge(inner, "Bottom", new Vector2(0, 0), new Vector2(1, 0), theme);
            Edge(inner, "Left", new Vector2(0, 0), new Vector2(0, 1), theme);
            Edge(inner, "Right", new Vector2(1, 0), new Vector2(1, 1), theme);

            float portraitHeight = REFERENCE.y * PORTRAIT_HEIGHT_FRACTION;
            float portraitWidth = Mathf.Round(portraitHeight * PORTRAIT_ASPECT);
            float boxLeft = (REFERENCE.x - boxWidth) / 2f;
            float textLeft = Mathf.Max(pad.left, PORTRAIT_LEFT + portraitWidth - boxLeft + PORTRAIT_GAP); // 초상이 상자 왼쪽을 덮는 만큼 글자를 민다

            TextMeshProUGUI title = UiKit.Label(panel.transform, "Name", Strings.Get("ui_sample_npc_title"), UiTextSize.Heading, theme, TextAlignmentOptions.BottomLeft);
            UiKit.StretchTop(title.rectTransform, heading, pad.top, textLeft, pad.right);
            title.color = theme.Accent;
            title.fontStyle = FontStyles.Bold;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            TextMeshProUGUI meta = UiKit.Label(panel.transform, "Meta", Strings.Get("ui_sample_npc_home"), UiTextSize.Small, theme, TextAlignmentOptions.BottomRight);
            UiKit.StretchTop(meta.rectTransform, heading, pad.top, textLeft, pad.right);
            meta.color = theme.Muted;
            meta.textWrappingMode = TextWrappingModes.NoWrap;

            TextMeshProUGUI line = UiKit.Label(panel.transform, "Line", Strings.Get("ui_sample_npc_line"), UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(line.rectTransform, textLeft, pad.top + heading + GAP, pad.right, pad.bottom + theme.ButtonHeight + GAP);
            line.overflowMode = TextOverflowModes.Truncate;

            TextMeshProUGUI reasons = UiKit.Label(panel.transform, "Reasons", "", UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(reasons.rectTransform, Vector2.zero, Vector2.zero, new Vector2(textLeft, pad.bottom), new Vector2(boxWidth * 0.4f, theme.ButtonHeight));
            reasons.color = theme.Muted;
            reasons.gameObject.SetActive(false);

            // 오른쪽 아래 버튼 줄: [선택지들][닫기] 또는 [계속하기]
            HorizontalOrVerticalLayoutGroup actions = UiKit.Row(panel.transform, "Actions", false, GAP);
            UiKit.Place((RectTransform)actions.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-pad.right, pad.bottom), new Vector2(0, theme.ButtonHeight));
            actions.childControlWidth = true; actions.childControlHeight = true;
            actions.childForceExpandWidth = false; actions.childForceExpandHeight = true;
            actions.childAlignment = TextAnchor.MiddleRight;
            actions.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            HorizontalOrVerticalLayoutGroup options = UiKit.Row(actions.transform, "Options", false, GAP);
            options.childControlWidth = true; options.childControlHeight = true;
            options.childForceExpandWidth = false; options.childForceExpandHeight = true;
            options.childAlignment = TextAnchor.MiddleRight;
            PrefabUtility.InstantiatePrefab(optionPart, options.transform);
            Button close = ActionButton(actions.transform, "Close", Strings.Get("npc_dialog_close"), theme);
            Button next = ActionButton(actions.transform, "Continue", Strings.Get("npc_dialog_continue"), theme);

            // 3) 왼쪽 아래 큰 초상(상자 앞)
            Image portrait = UiKit.Image(root.transform, "Portrait", null, Color.white, false);
            UiKit.Place(portrait.rectTransform, Vector2.zero, Vector2.zero, new Vector2(PORTRAIT_LEFT, 0), new Vector2(portraitWidth, portraitHeight));
            portrait.preserveAspect = true;
            portrait.enabled = false;

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_meta", meta);
            UiKit.Bind(screen, "_line", line);
            UiKit.Bind(screen, "_reasons", reasons);
            UiKit.Bind(screen, "_options", options.transform);
            UiKit.Bind(screen, "_optionPrefab", optionPart.GetComponent<DialogOptionView>());
            UiKit.Bind(screen, "_close", close);
            UiKit.Bind(screen, "_continue", next);
            UiKit.Bind(screen, "_dim", dim);
            UiKit.Bind(screen, "_panel", panel.rectTransform);
            UiKit.Bind(screen, "_portrait", portrait);
            return root;
        }

        /// <summary>안쪽 테두리 한 변: anchorMin~anchorMax 쪽 가장자리에 붙은 INNER_LINE 두께의 황동(theme.Accent) 줄. 클릭 통과.</summary>
        private static void Edge(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, UiTheme theme)
        {
            Image edge = UiKit.Image(parent, name, null, theme.Accent, false);
            RectTransform rect = edge.rectTransform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = anchorMin.x == anchorMax.x ? new Vector2(INNER_LINE, 0) : new Vector2(0, INNER_LINE);
        }

        /// <summary>버튼 줄의 테마 버튼(폭 ACTION_WIDTH, 높이 theme.ButtonHeight).</summary>
        private static Button ActionButton(Transform parent, string name, string text, UiTheme theme)
        {
            Button button = UiKit.Button(parent, name, text, theme);
            LayoutElement size = button.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = ACTION_WIDTH; size.preferredHeight = theme.ButtonHeight;
            return button;
        }

        /// <summary>선택지 부품: 구리 버튼 + 라벨, 배치 크기는 LayoutElement.</summary>
        private static GameObject Option(UiTheme theme)
        {
            Button button = UiKit.Button(null, "DialogOption", Strings.Get("ui_sample_npc_option"), theme);
            ((RectTransform)button.transform).sizeDelta = new Vector2(OPTION_WIDTH, theme.ButtonHeight);
            LayoutElement size = button.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = OPTION_WIDTH; size.preferredHeight = theme.ButtonHeight;
            DialogOptionView view = button.gameObject.AddComponent<DialogOptionView>();
            UiKit.Bind(view, "_button", button);
            UiKit.Bind(view, "_label", button.GetComponentInChildren<TMP_Text>(true));
            return button.gameObject;
        }

        private static bool ConsumeMarker(string id)
        {
            string marker = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, ".bridge", "rebuild", id);
            if (!System.IO.File.Exists(marker)) return false;
            System.IO.File.Delete(marker);
            Debug.Log("MAINCANVAS REBUILT PART: " + id);
            return true;
        }
    }
}
