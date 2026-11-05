using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>전투 화면과 구분되는 전면 정차역 배경과 사건 선택지 화면 프리팹을 만든다.</summary>
    public static class RestEventScreenBuilder
    {
        private static readonly Vector2 _panelSize = new Vector2(670, 600);

        /// <summary>theme으로 역 승강장 배경과 오른쪽 사건 선택 패널을 만들고 RestEventScreen 참조를 바인딩해 반환한다.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("RestEvent", false);
            RestEventScreen screen = root.AddComponent<RestEventScreen>();

            Image backdrop = UiKit.Image(root.transform, "StationNight", null, new Color(0.07f, 0.13f, 0.16f, 1f), true);
            UiKit.Stretch(backdrop.rectTransform);
            Image skyBand = UiKit.Image(root.transform, "Horizon", null, new Color(0.16f, 0.24f, 0.25f, 1f), false);
            UiKit.Place(skyBand.rectTransform, new Vector2(0.27f, 0.42f), new Vector2(0.27f, 0.42f), Vector2.zero, new Vector2(760f, 250f));
            Image platform = UiKit.Image(root.transform, "Platform", null, new Color(0.3f, 0.27f, 0.23f, 1f), false);
            UiKit.Place(platform.rectTransform, new Vector2(0.27f, 0f), new Vector2(0.27f, 0f), new Vector2(0f, 92f), new Vector2(760f, 185f));
            Image rail = UiKit.Image(root.transform, "PlatformRail", null, new Color(0.7f, 0.47f, 0.22f, 1f), false);
            UiKit.Place(rail.rectTransform, new Vector2(0.27f, 0f), new Vector2(0.27f, 0f), new Vector2(0f, 16f), new Vector2(760f, 12f));
            Image engine = UiKit.Image(root.transform, "ParkedEngine", null, new Color(0.14f, 0.17f, 0.17f, 1f), false);
            UiKit.Place(engine.rectTransform, new Vector2(0.24f, 0.27f), new Vector2(0.24f, 0.27f), Vector2.zero, new Vector2(345f, 145f));
            Image chimney = UiKit.Image(root.transform, "EngineChimney", null, new Color(0.12f, 0.14f, 0.14f, 1f), false);
            UiKit.Place(chimney.rectTransform, new Vector2(0.15f, 0.43f), new Vector2(0.15f, 0.43f), Vector2.zero, new Vector2(46f, 90f));
            Image lamp = UiKit.Image(root.transform, "StationLamp", null, new Color(1f, 0.73f, 0.3f, 1f), false);
            UiKit.Place(lamp.rectTransform, new Vector2(0.38f, 0.63f), new Vector2(0.38f, 0.63f), Vector2.zero, new Vector2(36f, 36f));
            Image lampPost = UiKit.Image(root.transform, "LampPost", null, new Color(0.42f, 0.37f, 0.29f, 1f), false);
            UiKit.Place(lampPost.rectTransform, new Vector2(0.38f, 0.37f), new Vector2(0.38f, 0.37f), Vector2.zero, new Vector2(10f, 165f));
            Color backingColor = theme.Panel;
            backingColor.a = 1f;
            Image backing = UiKit.Image(root.transform, "Backing", null, backingColor, false);
            UiKit.Place(backing.rectTransform, new Vector2(0.72f, 0.5f), new Vector2(0.72f, 0.5f), Vector2.zero, _panelSize - new Vector2(2f, 2f));
            Image panel = UiKit.Panel(root.transform, "Panel", theme);
            UiKit.Place(panel.rectTransform, new Vector2(0.72f, 0.5f), new Vector2(0.72f, 0.5f), Vector2.zero, _panelSize);

            TextMeshProUGUI title = UiKit.Label(panel.transform, "Title", "", UiTextSize.Heading, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(600f, 42f));
            title.color = theme.Accent;
            TextMeshProUGUI body = UiKit.Label(panel.transform, "Situation", "", UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(600f, 115f));
            body.textWrappingMode = TextWrappingModes.Normal;

            var choices = new Button[3];
            for (int i = 0; i < choices.Length; i++)
            {
                Button button = UiKit.Button(panel.transform, "Choice" + (i + 1), "", theme);
                UiKit.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -248f - i * 77f), new Vector2(600f, 62f));
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.enableAutoSizing = true;
                label.fontSizeMax = theme.Size(UiTextSize.Normal);
                label.fontSizeMin = theme.Size(UiTextSize.Small);
                choices[i] = button;
            }

            RectTransform resultBox = UiKit.Stretch(UiKit.Node(panel.transform, "ResultBox"));
            TextMeshProUGUI result = UiKit.Label(resultBox, "Result", "", UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(result.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -245f), new Vector2(600f, 88f));
            result.textWrappingMode = TextWrappingModes.Normal;
            TextMeshProUGUI detail = UiKit.Label(resultBox, "Detail", "", UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(600f, 74f));
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
