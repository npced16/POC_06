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
        /// <summary>스토리 027: 대화 상자 폭(초상 상자만큼 넓힘).</summary>
        private const float PANEL_WIDTH = 500;
        /// <summary>스토리 027: 초상 상자 한 변.</summary>
        private const float PORTRAIT = 80;

        /// <summary>화면 루트(UiScreen "NpcDialog", 상황 화면) + NpcDialogScreen + 세로 배치 대화 상자.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject optionPart = UiKit.SavePrefab(OPTION_PART, () => Option(theme), ConsumeMarker("DialogOption"));

            GameObject root = UiKit.ScreenRoot("NpcDialog", false);
            NpcDialogScreen screen = root.AddComponent<NpcDialogScreen>();
            Image panel = UiKit.Panel(root.transform, "Dialog", theme);
            UiKit.Place(panel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(18, 150), new Vector2(PANEL_WIDTH, 0)); // 스토리 025: 출신지 줄만큼 넓힘, 024: 프레임 여백만큼 더(내용 폭 유지)
            VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            RectOffset pad = UiKit.FramePad(theme);
            // 오른쪽 위 닫기 자리: Sheet는 둥근 X 반지름, Simple은 "닫기" 텍스트 버튼 폭(스토리 026)
            int closeReserve = theme.Skin == UiSkin.Simple ? (int)UiKit.SIMPLE_CLOSE_WIDTH + 8 : (int)(UiKit.CLOSE_SIZE / 2);
            column.padding = new RectOffset(pad.left, pad.right, pad.top, pad.bottom);
            column.spacing = 6;
            column.childControlWidth = true; column.childControlHeight = true;
            column.childForceExpandWidth = true; column.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalOrVerticalLayoutGroup header = UiKit.Row(panel.transform, "Header", false, 8);
            header.childControlWidth = true; header.childControlHeight = true;
            header.childAlignment = TextAnchor.MiddleLeft;
            header.padding = new RectOffset(0, closeReserve, 0, 0); // 스토리 026: 닫기는 제목 줄 오른쪽 끝
            TextMeshProUGUI title = UiKit.Label(header.transform, "Title", Strings.Get("ui_sample_npc_title"), UiTextSize.Heading, theme, TextAlignmentOptions.BottomLeft);
            title.color = theme.Accent;
            title.fontStyle = FontStyles.Bold;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            header.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1; // 027: 제목 줄은 패널 전체 폭
            // 스토리 026: 집·출신지는 제목 아래 별도 한 줄(Small, 오른쪽 정렬)
            HorizontalOrVerticalLayoutGroup meta = UiKit.Row(panel.transform, "Meta", false, 8);
            meta.childControlWidth = true; meta.childControlHeight = true;
            meta.childForceExpandWidth = false;
            meta.childAlignment = TextAnchor.MiddleRight;
            TextMeshProUGUI home = UiKit.Label(meta.transform, "Home", Strings.Get("ui_sample_npc_home"), UiTextSize.Small, theme, TextAlignmentOptions.BottomRight);
            home.color = theme.Muted;
            home.textWrappingMode = TextWrappingModes.NoWrap;
            TextMeshProUGUI origin = UiKit.Label(meta.transform, "Origin", string.Format(Strings.Get("npc_dialog_origin"), Strings.Get("ui_sample_passenger_1_origin")), UiTextSize.Small, theme, TextAlignmentOptions.BottomRight);
            origin.color = theme.Muted;
            origin.textWrappingMode = TextWrappingModes.NoWrap;

            // 스토리 027: 왼쪽 초상 상자 + 오른쪽 대사·결과 열
            HorizontalOrVerticalLayoutGroup body = UiKit.Row(panel.transform, "Body", false, 10);
            body.childControlWidth = true; body.childControlHeight = true;
            body.childForceExpandWidth = false; body.childForceExpandHeight = false;
            body.childAlignment = TextAnchor.UpperLeft;
            var box = new GameObject("PortraitBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(body.transform, false);
            Image boxImage = box.GetComponent<Image>();
            boxImage.color = new Color(0f, 0f, 0f, 0.28f);
            boxImage.raycastTarget = false;
            LayoutElement boxSize = box.AddComponent<LayoutElement>();
            boxSize.minWidth = boxSize.preferredWidth = PORTRAIT;
            boxSize.minHeight = boxSize.preferredHeight = PORTRAIT;
            var picture = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            picture.transform.SetParent(box.transform, false);
            RectTransform pictureRect = (RectTransform)picture.transform;
            pictureRect.anchorMin = Vector2.zero; pictureRect.anchorMax = Vector2.one;
            pictureRect.offsetMin = new Vector2(4, 4); pictureRect.offsetMax = new Vector2(-4, -4);
            Image portrait = picture.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.enabled = false;
            var textColumn = new GameObject("Text", typeof(RectTransform), typeof(VerticalLayoutGroup));
            textColumn.transform.SetParent(body.transform, false);
            VerticalLayoutGroup texts = textColumn.GetComponent<VerticalLayoutGroup>();
            texts.spacing = 6;
            texts.childControlWidth = true; texts.childControlHeight = true;
            texts.childForceExpandWidth = true; texts.childForceExpandHeight = false;
            textColumn.AddComponent<LayoutElement>().flexibleWidth = 1;
            TextMeshProUGUI line = UiKit.Label(textColumn.transform, "Line", Strings.Get("ui_sample_npc_line"), UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            line.gameObject.AddComponent<LayoutElement>().minHeight = 44;
            TextMeshProUGUI result = UiKit.Label(textColumn.transform, "Result", Strings.Get("ui_sample_npc_result"), UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
            result.fontStyle = FontStyles.Bold;

            HorizontalOrVerticalLayoutGroup buttons = UiKit.Row(panel.transform, "Buttons", false, 8);
            buttons.childControlWidth = true; buttons.childControlHeight = true;
            buttons.childAlignment = TextAnchor.MiddleLeft;
            HorizontalOrVerticalLayoutGroup options = UiKit.Row(buttons.transform, "Options", false, 6);
            options.childControlWidth = true; options.childControlHeight = true;
            options.childAlignment = TextAnchor.MiddleLeft;
            options.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            PrefabUtility.InstantiatePrefab(optionPart, options.transform);
            Button close = UiKit.CloseButton(panel.transform, "Close", theme); // 스토리 024: 둥근 X를 오른쪽 위 모서리에
            close.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            if (theme.Skin == UiSkin.Simple)
            {
                // 스토리 027: Simple "닫기"는 패널 오른쪽 위 모서리(pad 안쪽)에 고정
                RectTransform closeRect = (RectTransform)close.transform;
                closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
                closeRect.anchoredPosition = new Vector2(-pad.right, -pad.top);
            }

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
            UiKit.Bind(screen, "_panel", panel.rectTransform);
            UiKit.Bind(screen, "_portrait", portrait);
            return root;
        }

        /// <summary>선택지 부품: 구리 버튼 + 라벨, 배치 크기는 LayoutElement.</summary>
        private static GameObject Option(UiTheme theme)
        {
            Button button = UiKit.Button(null, "DialogOption", Strings.Get("ui_sample_npc_option"), theme);
            ((RectTransform)button.transform).sizeDelta = new Vector2(110, theme.ButtonHeight);
            LayoutElement size = button.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 110; size.preferredHeight = theme.ButtonHeight;
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
