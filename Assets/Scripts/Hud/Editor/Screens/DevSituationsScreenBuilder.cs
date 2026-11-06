using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Onboarding;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Onboarding;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스펙 001 FR-012~FR-014. 항상 켜진 화면 "DevSituations"(F9 개발자 상황 메뉴) 프리팹 내용을 만든다. BuildMainCanvas.SCREENS가 부른다.</summary>
    public static class DevSituationsScreenBuilder
    {
        private const string TOPICS_PATH = "Onboarding/OnboardingTopics";
        private const float PANEL_WIDTH = 460f;
        private const float TITLE_HEIGHT = 56f;
        private const float ROW_HEIGHT = 30f;
        private const float ROW_GAP = 2f;
        private const float PAD = 12f;

        /// <summary>
        /// theme 색·글자로 화면 루트(UiScreen "DevSituations", 항상 켜짐), 오른쪽 가운데 메뉴 패널, 안내 줄,
        /// OnboardingTopics.json을 order 순으로 정렬한 항목마다 버튼 한 행을 만든다. 루트 GameObject를 돌려준다.
        /// </summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("DevSituations", true);
            DevSituationsScreen screen = root.AddComponent<DevSituationsScreen>();

            TextAsset asset = Resources.Load<TextAsset>(TOPICS_PATH);
            IReadOnlyList<OnboardingTopic> topics = OnboardingTopicsData.Parse(asset != null ? asset.text : null).Topics;
            float height = TITLE_HEIGHT + topics.Count * (ROW_HEIGHT + ROW_GAP) + PAD * 2f;

            Image panel = UiKit.Panel(root.transform, "Panel", theme);
            UiKit.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(PANEL_WIDTH, height));

            TextMeshProUGUI title = UiKit.Label(panel.transform, "Title", Strings.Get("dev_situations_title"), UiTextSize.Normal, theme);
            UiKit.StretchTop(title.rectTransform, TITLE_HEIGHT - 8f, PAD, PAD, PAD);

            RectTransform rows = UiKit.Node(panel.transform, "Rows");
            UiKit.Stretch(rows, PAD, PAD + TITLE_HEIGHT, PAD, PAD);
            for (int i = 0; i < topics.Count; i++)
            {
                Button row = UiKit.Button(rows, "Row_" + topics[i].Id, (i + 1) + ". " + topics[i].Id, theme);
                UiKit.Place((RectTransform)row.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * (ROW_HEIGHT + ROW_GAP)), new Vector2(PANEL_WIDTH - PAD * 2f, ROW_HEIGHT));
                row.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            }

            UiKit.Bind(screen, "_panel", panel.gameObject);
            UiKit.Bind(screen, "_rows", rows);
            return root;
        }
    }
}
