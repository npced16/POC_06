using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 051. 뒤 입력을 가리는 휴식지 사건 모달과 세로 선택지·결과 화면 프리팹을 만든다.</summary>
    public static class RestEventScreenBuilder
    {
        private static readonly Vector2 _panelSize = new Vector2(760, 500);

        /// <summary>theme으로 RestEvent 화면 루트와 중앙 패널을 만들고 RestEventScreen 참조를 바인딩해 반환한다.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("RestEvent", false);
            RestEventScreen screen = root.AddComponent<RestEventScreen>();

            Image dim = UiKit.Image(root.transform, "Dim", null, new Color(0f, 0f, 0f, 0.65f), true);
            UiKit.Stretch(dim.rectTransform);
            Color backingColor = theme.Panel;
            backingColor.a = 1f;
            Image backing = UiKit.Image(root.transform, "Backing", null, backingColor, false);
            UiKit.Place(backing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, _panelSize - new Vector2(2f, 2f));
            Image panel = UiKit.Panel(root.transform, "Panel", theme);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, _panelSize);

            TextMeshProUGUI title = UiKit.Label(panel.transform, "Title", "", UiTextSize.Heading, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(690f, 38f));
            title.color = theme.Accent;
            TextMeshProUGUI body = UiKit.Label(panel.transform, "Situation", "", UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(690f, 105f));
            body.textWrappingMode = TextWrappingModes.Normal;

            var choices = new Button[3];
            for (int i = 0; i < choices.Length; i++)
            {
                Button button = UiKit.Button(panel.transform, "Choice" + (i + 1), "", theme);
                UiKit.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -194f - i * 72f), new Vector2(690f, 60f));
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.enableAutoSizing = true;
                label.fontSizeMax = theme.Size(UiTextSize.Normal);
                label.fontSizeMin = theme.Size(UiTextSize.Small);
                choices[i] = button;
            }

            RectTransform resultBox = UiKit.Stretch(UiKit.Node(panel.transform, "ResultBox"));
            TextMeshProUGUI result = UiKit.Label(resultBox, "Result", "", UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(result.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(690f, 76f));
            result.textWrappingMode = TextWrappingModes.Normal;
            TextMeshProUGUI detail = UiKit.Label(resultBox, "Detail", "", UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(690f, 52f));
            detail.color = theme.Muted;
            detail.textWrappingMode = TextWrappingModes.Normal;
            Button continueButton = UiKit.Button(resultBox, "Continue", Strings.Get("loop_continue"), theme);
            UiKit.Place((RectTransform)continueButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-35f, 28f), new Vector2(140f, theme.ButtonHeight));
            resultBox.gameObject.SetActive(false);

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_body", body);
            HotbarScreenBuilder.BindArray(screen, "_choices", choices);
            UiKit.Bind(screen, "_resultBox", resultBox.gameObject);
            UiKit.Bind(screen, "_result", result);
            UiKit.Bind(screen, "_detail", detail);
            UiKit.Bind(screen, "_continue", continueButton);
            return root;
        }
    }
}
