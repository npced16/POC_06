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
        /// <summary>화면 루트(UiScreen "PhaseBanner", 항상 켜짐) + PhaseBannerScreen + 띠 줄(내용 폭에 맞춰 늘어나는 상자 두 개)과 패배 사유 줄.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("PhaseBanner", true);
            PhaseBannerScreen screen = root.AddComponent<PhaseBannerScreen>();

            RectTransform strip = UiKit.Node(root.transform, "Strip");
            UiKit.StretchTop(strip, 38, 74);
            HorizontalOrVerticalLayoutGroup row = UiKit.Row(strip, "Row", false, 8);
            row.childControlWidth = true; row.childControlHeight = true; row.childForceExpandHeight = true;

            string sample = Strings.Get("hub_1") + "  ·  " + Strings.Get("phase_recovering") + "  ·  " + Strings.Get("phase_prosperity") + " 12  ·  "
                + Strings.Get("phase_habitats") + " 2";
            TextMeshProUGUI hub = Box(row.transform, "HubBox", "Hub", sample, UiTextSize.Normal, theme);
            hub.color = theme.Accent;
            TextMeshProUGUI deadline = Box(row.transform, "DeadlineBox", "Deadline", Strings.Get("phase_deadline") + ": 3" + Strings.Get("phase_laps_left"), UiTextSize.Small, theme);
            deadline.color = theme.Accent;

            TextMeshProUGUI loss = UiKit.Label(root.transform, "Loss", Strings.Get("phase_loss_collapse"), UiTextSize.Title, theme);
            RectTransform lossRect = loss.rectTransform;
            lossRect.anchorMin = new Vector2(0, 0.34f); lossRect.anchorMax = new Vector2(1, 0.34f); lossRect.pivot = new Vector2(0.5f, 0.5f);
            lossRect.anchoredPosition = Vector2.zero; lossRect.sizeDelta = new Vector2(0, 44);
            loss.color = theme.Accent;
            loss.gameObject.SetActive(false);

            UiKit.Bind(screen, "_hubLabel", hub);
            UiKit.Bind(screen, "_deadlineBox", deadline.transform.parent.gameObject);
            UiKit.Bind(screen, "_deadline", deadline);
            UiKit.Bind(screen, "_loss", loss);
            return root;
        }

        /// <summary>글자 폭에 맞춰 늘어나는 패널 상자(가로 여백 14, 세로 4)와 한 줄 라벨.</summary>
        private static TextMeshProUGUI Box(Transform parent, string boxName, string labelName, string text, UiTextSize size, UiTheme theme)
        {
            Image box = UiKit.Panel(parent, boxName, theme, false);
            var layout = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 4, 4);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            TextMeshProUGUI label = UiKit.Label(box.transform, labelName, text, size, theme);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
    }
}
