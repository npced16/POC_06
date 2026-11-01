using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>
    /// 스토리 028. 상황 화면 "RunResult"(전체 화면 그늘 + 가운데 결과 카드: 제목판, 사유, 거점별 줄, 총 바퀴, 다시 시작 버튼) 프리팹 내용을 만든다.
    /// 색·스프라이트·글자 크기는 테마(Simple 스킨 기본)에서 온다. 크기 상수는 배치용이다.
    /// </summary>
    public static class RunResultScreenBuilder
    {
        /// <summary>그늘 불투명도(Defeat 화면과 같다).</summary>
        private const float SHADE_ALPHA = 0.86f;
        private static readonly Vector2 CardSize = new Vector2(820, 470);
        private const float BODY_HEIGHT = 44;
        private const float HUBS_HEIGHT = 146;
        private const float LAPS_HEIGHT = 40;
        private const float GAP = 8;
        private static readonly Vector2 ButtonSize = new Vector2(220, 56);
        private const float BUTTON_BOTTOM = 22;

        /// <summary>화면 루트(UiScreen "RunResult", 상황 화면) + RunResultScreen + 클릭을 막는 그늘과 카드.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("RunResult", false);
            RunResultScreen screen = root.AddComponent<RunResultScreen>();
            Color shadeColor = theme.Panel; shadeColor.a = SHADE_ALPHA;
            Image shade = UiKit.Image(root.transform, "Shade", null, shadeColor, true);
            shade.preserveAspect = false;
            UiKit.Stretch(shade.rectTransform);
            Image card = UiKit.Panel(shade.transform, "Card", theme);
            UiKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardSize);
            TextMeshProUGUI title = UiKit.TitlePlate(card.transform, "Title", Strings.Get("result_victory"), theme);

            float top = UiKit.ContentTop(theme);
            float width = CardSize.x - 80;
            TextMeshProUGUI body = UiKit.Label(card.transform, "Body", Strings.Get("result_body_partial"), UiTextSize.Normal, theme);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -top), new Vector2(width, BODY_HEIGHT));
            top += BODY_HEIGHT + GAP;
            TextMeshProUGUI hubs = UiKit.Label(card.transform, "Hubs", "", UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(hubs.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -top), new Vector2(width, HUBS_HEIGHT));
            top += HUBS_HEIGHT + GAP;
            TextMeshProUGUI laps = UiKit.Label(card.transform, "Laps", "", UiTextSize.Normal, theme);
            UiKit.Place(laps.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -top), new Vector2(width, LAPS_HEIGHT));

            top += LAPS_HEIGHT + GAP;
            TextMeshProUGUI finance = UiKit.Label(card.transform, "Finance", string.Format(Strings.Get("ui_result_finance"), 0, 0), UiTextSize.Normal, theme);
            UiKit.Place(finance.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -top), new Vector2(width, 36));
            UiKit.Bind(screen, "_finance", finance);

            Button restart = UiKit.Button(card.transform, "Restart", Strings.Get("result_restart"), theme);
            UiKit.Place((RectTransform)restart.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, BUTTON_BOTTOM), ButtonSize);

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_body", body);
            UiKit.Bind(screen, "_hubs", hubs);
            UiKit.Bind(screen, "_laps", laps);
            UiKit.Bind(screen, "_restart", restart);
            return root;
        }
    }
}
