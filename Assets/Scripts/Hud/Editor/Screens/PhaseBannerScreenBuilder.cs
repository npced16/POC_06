using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.HubPhase;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022. 항상 켜진 화면 "PhaseBanner"(위 가운데 단계 띠 + 정착 기한 상자, 아래쪽 패배 사유 줄) 프리팹 내용을 만든다.</summary>
    public static class PhaseBannerScreenBuilder
    {
        /// <summary>배너·기한 상자 공통 높이(Heading 22 한 줄).</summary>
        private const float BOX_HEIGHT = 40f;

        /// <summary>화면 루트(UiScreen "PhaseBanner", 항상 켜짐) + PhaseBannerScreen + 띠 줄(내용 폭에 맞춰 늘어나는 상자 두 개)과 패배 사유 줄.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("PhaseBanner", true);
            PhaseBannerScreen screen = root.AddComponent<PhaseBannerScreen>();

            RectTransform strip = UiKit.Node(root.transform, "Strip");
            UiKit.StretchTop(strip, BOX_HEIGHT, 74);
            HorizontalOrVerticalLayoutGroup row = UiKit.Row(strip, "Row", false, 8);
            row.childControlWidth = true; row.childControlHeight = true; row.childForceExpandHeight = true;

            string sample = Strings.Get("hub_1") + "  ·  " + Strings.Get("phase_recovering") + "  ·  " + Strings.Get("phase_prosperity") + " 12  ·  "
                + Strings.Get("phase_habitats") + " 2";
            TextMeshProUGUI hub = Box(row.transform, "HubBox", "Hub", sample, UiTextSize.Heading, theme);
            hub.color = theme.Accent;
            TextMeshProUGUI deadline = Box(row.transform, "DeadlineBox", "Deadline", Strings.Get("phase_deadline") + ": 3" + Strings.Get("phase_laps_left"), UiTextSize.Heading, theme);
            deadline.color = theme.Accent;

            TextMeshProUGUI loss = UiKit.Label(root.transform, "Loss", Strings.Get("phase_loss_collapse"), UiTextSize.Title, theme);
            RectTransform lossRect = loss.rectTransform;
            lossRect.anchorMin = new Vector2(0, 0.34f); lossRect.anchorMax = new Vector2(1, 0.34f); lossRect.pivot = new Vector2(0.5f, 0.5f);
            lossRect.anchoredPosition = Vector2.zero; lossRect.sizeDelta = new Vector2(0, 44);
            loss.color = theme.Accent;
            loss.gameObject.SetActive(false);

            TextMeshProUGUI nextTier = UiKit.Label(root.transform, "NextTier", string.Format(Strings.Get("ui_tier_next"), Strings.Get("ui_tier_name_1"), 3, 5, 12, 20), UiTextSize.Normal, theme);
            UiKit.Place(nextTier.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -116), new Vector2(820, 30));
            // 스토리 045: 목표 줄(다음 등급 줄 아래). 승리 등급까지의 조건과 기한 바퀴.
            TextMeshProUGUI goal = UiKit.Label(root.transform, "Goal", string.Format(Strings.Get("ui_goal_line"), Strings.Get("ui_tier_name_3"), 3, 25, 12, 100) + string.Format(Strings.Get("ui_goal_laps"), 3, 20), UiTextSize.Small, theme);
            UiKit.Place(goal.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -142), new Vector2(820, 24));
            goal.color = theme.Muted;
            UiKit.Bind(screen, "_goal", goal);
            var tierBanner = UiKit.Bar(root.transform, "TierBanner", theme, false);
            UiKit.Place(tierBanner.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 80));
            var tierBannerText = UiKit.Label(tierBanner.transform, "Text", string.Format(Strings.Get("ui_tier_promoted"), Strings.Get("hub_1"), Strings.Get("ui_tier_name_1")), UiTextSize.Title, theme);
            UiKit.Stretch(tierBannerText.rectTransform, 24, 12, 24, 12);
            tierBannerText.color = theme.Accent;
            tierBanner.gameObject.SetActive(false);
            UiKit.Bind(screen, "_nextTier", nextTier); UiKit.Bind(screen, "_tierBanner", tierBanner.gameObject); UiKit.Bind(screen, "_tierBannerText", tierBannerText);
            UiKit.Bind(screen, "_hubLabel", hub);
            UiKit.Bind(screen, "_deadlineBox", deadline.transform.parent.gameObject);
            UiKit.Bind(screen, "_deadline", deadline);
            UiKit.Bind(screen, "_loss", loss);
            return root;
        }

        /// <summary>글자 폭에 맞춰 늘어나는 패널 상자(가로 여백 14, 세로 4)와 한 줄 라벨.</summary>
        private static TextMeshProUGUI Box(Transform parent, string boxName, string labelName, string text, UiTextSize size, UiTheme theme)
        {
            Image box = UiKit.Bar(parent, boxName, theme, false);
            var layout = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            int side = UiKit.PlateSide(theme); // 스토리 024: title_plate 끝 장식에 글자가 닿지 않게
            layout.padding = new RectOffset(side, side, 4, 4);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            TextMeshProUGUI label = UiKit.Label(box.transform, labelName, text, size, theme);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
    }
}
