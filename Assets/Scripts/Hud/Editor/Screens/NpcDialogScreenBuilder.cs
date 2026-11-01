using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Npc;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022. 상황 화면 "NpcDialog"(왼쪽 아래 NPC 대화 상자) 프리팹 내용과 부품 DialogOption을 만든다. 선택지는 런타임에 부품에서 만든다.</summary>
    public static class NpcDialogScreenBuilder
    {
        /// <summary>선택지 버튼 부품 경로.</summary>
        public const string OPTION_PART = BuildMainCanvas.PARTS_FOLDER + "/DialogOption.prefab";

        /// <summary>화면 루트(UiScreen "NpcDialog", 상황 화면) + NpcDialogScreen + 세로 배치 대화 상자.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject optionPart = UiKit.SavePrefabIfMissing(OPTION_PART, () => Option(theme));

            GameObject root = UiKit.ScreenRoot("NpcDialog", false);
            NpcDialogScreen screen = root.AddComponent<NpcDialogScreen>();
            Image panel = UiKit.Panel(root.transform, "Dialog", theme);
            UiKit.Place(panel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(18, 150), new Vector2(390, 0)); // 스토리 025: 출신지 줄만큼 넓힘
            VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(14, 14, 12, 12);
            column.spacing = 6;
            column.childControlWidth = true; column.childControlHeight = true;
            column.childForceExpandWidth = true; column.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalOrVerticalLayoutGroup header = UiKit.Row(panel.transform, "Header", false, 8);
            header.childControlWidth = true; header.childControlHeight = true;
            header.childAlignment = TextAnchor.LowerLeft;
            TextMeshProUGUI title = UiKit.Label(header.transform, "Title", Strings.Get("ui_sample_npc_title"), UiTextSize.Normal, theme, TextAlignmentOptions.BottomLeft);
            title.color = theme.Accent;
            title.fontStyle = FontStyles.Bold;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            TextMeshProUGUI home = UiKit.Label(header.transform, "Home", Strings.Get("ui_sample_npc_home"), UiTextSize.Small, theme, TextAlignmentOptions.BottomRight);
            home.color = theme.Muted;
            home.textWrappingMode = TextWrappingModes.NoWrap;
            TextMeshProUGUI origin = UiKit.Label(header.transform, "Origin", string.Format(Strings.Get("npc_dialog_origin"), Strings.Get("ui_sample_passenger_1_origin")), UiTextSize.Small, theme, TextAlignmentOptions.BottomRight);
            origin.color = theme.Muted;
            origin.textWrappingMode = TextWrappingModes.NoWrap;

            TextMeshProUGUI line = UiKit.Label(panel.transform, "Line", Strings.Get("ui_sample_npc_line"), UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            line.gameObject.AddComponent<LayoutElement>().minHeight = 44;
            TextMeshProUGUI result = UiKit.Label(panel.transform, "Result", Strings.Get("ui_sample_npc_result"), UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
            result.fontStyle = FontStyles.Bold;

            HorizontalOrVerticalLayoutGroup buttons = UiKit.Row(panel.transform, "Buttons", false, 8);
            buttons.childControlWidth = true; buttons.childControlHeight = true;
            buttons.childAlignment = TextAnchor.MiddleLeft;
            HorizontalOrVerticalLayoutGroup options = UiKit.Row(buttons.transform, "Options", false, 6);
            options.childControlWidth = true; options.childControlHeight = true;
            options.childAlignment = TextAnchor.MiddleLeft;
            options.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            PrefabUtility.InstantiatePrefab(optionPart, options.transform);
            Button close = UiKit.Button(buttons.transform, "Close", Strings.Get("npc_dialog_close"), theme);
            LayoutElement closeSize = close.gameObject.AddComponent<LayoutElement>();
            closeSize.preferredWidth = 72; closeSize.preferredHeight = 34;

            TextMeshProUGUI reasons = UiKit.Label(panel.transform, "Reasons", "", UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
            reasons.color = theme.Muted;
            reasons.gameObject.SetActive(false);

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_home", home);
            UiKit.Bind(screen, "_origin", origin);
            UiKit.Bind(screen, "_line", line);
            UiKit.Bind(screen, "_result", result);
            UiKit.Bind(screen, "_reasons", reasons);
            UiKit.Bind(screen, "_options", options.transform);
            UiKit.Bind(screen, "_optionPrefab", optionPart.GetComponent<DialogOptionView>());
            UiKit.Bind(screen, "_close", close);
            return root;
        }

        /// <summary>선택지 부품: 구리 버튼 + 라벨, 배치 크기는 LayoutElement.</summary>
        private static GameObject Option(UiTheme theme)
        {
            Button button = UiKit.Button(null, "DialogOption", Strings.Get("ui_sample_npc_option"), theme);
            ((RectTransform)button.transform).sizeDelta = new Vector2(110, 34);
            LayoutElement size = button.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 110; size.preferredHeight = 34;
            DialogOptionView view = button.gameObject.AddComponent<DialogOptionView>();
            UiKit.Bind(view, "_button", button);
            UiKit.Bind(view, "_label", button.GetComponentInChildren<TMP_Text>(true));
            return button.gameObject;
        }
    }
}
