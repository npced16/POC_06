using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Npc;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>
    /// 스토리 025. 상황 화면 "Passengers"(열차 승객 목록) 프리팹 내용과 부품 PassengerRow를 만든다.
    /// 줄은 런타임에 승객 수만큼 부품에서 만들며, 프리팹에는 미리보기 승객을 넣지 않는다.
    /// </summary>
    public static class PassengersScreenBuilder
    {
        /// <summary>승객 한 줄 부품 경로.</summary>
        public const string ROW_PART = BuildMainCanvas.PARTS_FOLDER + "/PassengerRow.prefab";

        private static readonly Vector2 PanelSize = new Vector2(720, 400);
        private static readonly Vector2 RowSize = new Vector2(664, 84);

        /// <summary>화면 루트(UiScreen "Passengers", 상황 화면) + PassengersScreen + 가운데 패널·머리줄·줄 목록·빈 상태 문구.</summary>
        public static GameObject Build(UiTheme theme)
        {
            // 표식 .bridge/rebuild/PassengerRow가 있으면 줄 부품도 같은 경로(GUID 유지)에 다시 만든다.
            GameObject rowPart = UiKit.SavePrefab(ROW_PART, () => Row(theme), ConsumeMarker("PassengerRow"));

            GameObject root = UiKit.ScreenRoot("Passengers", false);
            PassengersScreen screen = root.AddComponent<PassengersScreen>();
            Image panel = UiKit.Panel(root.transform, "Panel", theme);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), PanelSize);

            RectOffset pad = UiKit.FramePad(theme);
            TextMeshProUGUI title = UiKit.TitlePlate(panel.transform, "Title", string.Format(Strings.Get("passengers_title"), 2, 3), theme);
            Button close = UiKit.CloseButton(panel.transform, "Close", theme);

            ScrollRect scroll = UiKit.ScrollGrid(panel.transform, "List", RowSize, new Vector2(0, 8), 1, out RectTransform list);
            UiKit.Stretch((RectTransform)scroll.transform, pad.left, UiKit.ContentTop(theme) + 6, pad.right, pad.bottom);
            scroll.scrollSensitivity = 30;

            TextMeshProUGUI empty = UiKit.Label(panel.transform, "Empty", Strings.Get("passengers_empty"), UiTextSize.Normal, theme);
            UiKit.Stretch(empty.rectTransform, pad.left, UiKit.ContentTop(theme) + 6, pad.right, pad.bottom);
            empty.color = theme.Muted;
            empty.gameObject.SetActive(true);

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_empty", empty);
            UiKit.Bind(screen, "_close", close);
            UiKit.Bind(screen, "_list", list);
            UiKit.Bind(screen, "_rowPrefab", rowPart.GetComponent<PassengerRowView>());
            return root;
        }

        /// <summary>id에 해당하는 재생성 표식을 지우고 실제 삭제 여부를 반환한다.</summary>
        private static bool ConsumeMarker(string id)
        {
            string marker = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, ".bridge", "rebuild", id);
            if (!System.IO.File.Exists(marker)) return false;
            System.IO.File.Delete(marker);
            Debug.Log("MAINCANVAS REBUILT PART: " + id);
            return true;
        }

        /// <summary>승객 이름과 완성할 건물·합류 인물 관계만 담은 줄 부품을 만든다.</summary>
        private static GameObject Row(UiTheme theme)
        {
            Image frame = UiKit.Slot(null, "PassengerRow", theme);
            frame.raycastTarget = false; // 스크롤 휠·드래그는 뷰포트가 받는다
            frame.rectTransform.sizeDelta = RowSize;
            PassengerRowView view = frame.gameObject.AddComponent<PassengerRowView>();
            const float side = 16;

            TextMeshProUGUI name = UiKit.Label(frame.transform, "Name", "", UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(name.rectTransform, 32, 8, side, 400);
            name.color = theme.Accent;
            name.fontStyle = FontStyles.Bold;
            NoWrap(name);
            TextMeshProUGUI wants = UiKit.Label(frame.transform, "Wants", "", UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(wants.rectTransform, 32, 44, side, side);
            wants.color = theme.Accent;
            NoWrap(wants);

            UiKit.Bind(view, "_name", name);
            UiKit.Bind(view, "_wants", wants);
            return frame.gameObject;
        }

        /// <summary>label의 긴 한 줄 문장을 넘치면 말줄임으로 표시한다.</summary>
        private static void NoWrap(TextMeshProUGUI label)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

    }
}
