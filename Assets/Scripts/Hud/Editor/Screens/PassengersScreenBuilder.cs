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
    /// 스토리 025. 상황 화면 "Passengers"(열차 승객 목록) 프리팹 내용과 부품 PassengerRow를 만든다.
    /// 줄은 런타임에 승객 수만큼 부품에서 만들고, 프리팹에는 미리보기 줄 2개를 둔다(런타임에 지운다). 색·스프라이트는 테마 값만 쓴다.
    /// </summary>
    public static class PassengersScreenBuilder
    {
        /// <summary>승객 한 줄 부품 경로.</summary>
        public const string ROW_PART = BuildMainCanvas.PARTS_FOLDER + "/PassengerRow.prefab";

        private static readonly Vector2 PanelSize = new Vector2(720, 470);
        private static readonly Vector2 RowSize = new Vector2(664, 112);
        private const float PAD = 18; // 틀 안쪽 여백(테마 틀이 두꺼워져도 글자가 닿지 않게)

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
            TextMeshProUGUI hint = UiKit.Label(panel.transform, "Hint", Strings.Get("passengers_hint"), UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(hint.rectTransform, theme.LineHeight(UiTextSize.Small), UiKit.ContentTop(theme), pad.left + 4, pad.right + 4);
            hint.color = theme.Muted;
            Button close = UiKit.CloseButton(panel.transform, "Close", theme);

            ScrollRect scroll = UiKit.ScrollGrid(panel.transform, "List", RowSize, new Vector2(0, 8), 1, out RectTransform list);
            UiKit.Stretch((RectTransform)scroll.transform, pad.left, UiKit.ContentTop(theme) + theme.LineHeight(UiTextSize.Small) + 6, pad.right, pad.bottom);
            scroll.scrollSensitivity = 30;

            TextMeshProUGUI empty = UiKit.Label(panel.transform, "Empty", Strings.Get("passengers_empty"), UiTextSize.Normal, theme);
            UiKit.Stretch(empty.rectTransform, pad.left, UiKit.ContentTop(theme) + theme.LineHeight(UiTextSize.Small) + 6, pad.right, pad.bottom);
            empty.color = theme.Muted;
            empty.gameObject.SetActive(false); // 미리보기는 줄 2개를 보인다. 런타임에 승객이 없으면 켠다

            // 미리보기 줄(런타임에 지운다)
            for (int i = 1; i <= 2; i++)
            {
                var sample = ((GameObject)PrefabUtility.InstantiatePrefab(rowPart, list)).GetComponent<PassengerRowView>();
                string p = "ui_sample_passenger_" + i + "_";
                sample.Show(Strings.Get(p + "name"),
                    string.Format(Strings.Get("passengers_origin"), Strings.Get(p + "origin")),
                    string.Format(Strings.Get("passengers_laps"), i - 1),
                    "“" + Strings.Get(p + "proposal") + "”",
                    Strings.Get(p + "hint"));
            }

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_hint", hint);
            UiKit.Bind(screen, "_empty", empty);
            UiKit.Bind(screen, "_close", close);
            UiKit.Bind(screen, "_list", list);
            UiKit.Bind(screen, "_rowPrefab", rowPart.GetComponent<PassengerRowView>());
            return root;
        }

        private static bool ConsumeMarker(string id)
        {
            string marker = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, ".bridge", "rebuild", id);
            if (!System.IO.File.Exists(marker)) return false;
            System.IO.File.Delete(marker);
            Debug.Log("MAINCANVAS REBUILT PART: " + id);
            return true;
        }

        /// <summary>줄 부품: 슬롯 틀 바탕, 첫 줄 직업명·출신지·바퀴, 둘째 줄 제안, 셋째 줄 힌트.</summary>
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
            TextMeshProUGUI origin = UiKit.Label(frame.transform, "Origin", "", UiTextSize.Small, theme, TextAlignmentOptions.MidlineRight);
            UiKit.StretchTop(origin.rectTransform, 32, 8, 270, 130);
            origin.color = theme.Muted;
            NoWrap(origin);
            TextMeshProUGUI laps = UiKit.Label(frame.transform, "Laps", "", UiTextSize.Small, theme, TextAlignmentOptions.MidlineRight);
            UiKit.Place(laps.rectTransform, Vector2.one, Vector2.one, new Vector2(-side, -8), new Vector2(110, 32));
            laps.color = theme.Accent;
            NoWrap(laps);

            TextMeshProUGUI proposal = UiKit.Label(frame.transform, "Proposal", "", UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(proposal.rectTransform, 32, 42, side, side);
            Shrink(proposal, theme, UiTextSize.Normal);
            TextMeshProUGUI hint = UiKit.Label(frame.transform, "Hint", "", UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(hint.rectTransform, 28, 76, side, side);
            hint.color = theme.Muted;
            hint.fontStyle = FontStyles.Italic;
            Shrink(hint, theme, UiTextSize.Small);

            UiKit.Bind(view, "_name", name);
            UiKit.Bind(view, "_origin", origin);
            UiKit.Bind(view, "_laps", laps);
            UiKit.Bind(view, "_proposal", proposal);
            UiKit.Bind(view, "_hint", hint);
            return frame.gameObject;
        }

        private static void NoWrap(TextMeshProUGUI label)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>한 줄에 맞도록 글자를 줄인다(긴 제안·힌트). 그래도 넘치면 말줄임.</summary>
        private static void Shrink(TextMeshProUGUI label, UiTheme theme, UiTextSize size)
        {
            NoWrap(label);
            label.enableAutoSizing = true;
            label.fontSizeMax = theme.Size(size);
            label.fontSizeMin = theme.Size(size) * 0.7f;
        }
    }
}
