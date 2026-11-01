using UnityEngine;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 033. 항상 켜진 화면 "Raid"(위 가운데 습격 패널: 내구도 바·라벨, 둥지·몬스터 수, 사격 안내) 프리팹 내용을 만든다.</summary>
    public static class RaidScreenBuilder
    {
        /// <summary>화면 루트(UiScreen "Raid", 항상 켜짐) + RaidScreen + 패널(기본 꺼짐).</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("Raid", true);
            RaidScreen screen = root.AddComponent<RaidScreen>();

            UnityEngine.UI.Image panel = UiKit.Panel(root.transform, "Panel", theme, false);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(440, 112));

            UnityEngine.UI.Image back = UiKit.Image(panel.transform, "HullBack", null, new Color(0.12f, 0.09f, 0.07f, 0.9f));
            UiKit.Place(back.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(400, 26));
            UnityEngine.UI.Image fill = UiKit.Image(back.transform, "HullFill", null, theme.Accent);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = new Vector2(0.7f, 1f); fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hull = UiKit.Label(back.transform, "HullLabel", string.Format(Strings.Get("raid_hull"), 7, 10), UiTextSize.Normal, theme);
            UiKit.Stretch(hull.rectTransform);
            // 노란(Accent) 채움 위에서도 읽히도록 어두운 잉크색 굵은 글자(사용자 피드백 01:25).
            hull.color = new Color32(34, 24, 18, 255);
            hull.fontStyle = FontStyles.Bold;

            TextMeshProUGUI counts = UiKit.Label(panel.transform, "Counts", string.Format(Strings.Get("raid_counts"), 3, 4), UiTextSize.Normal, theme);
            UiKit.Place(counts.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(400, 28));
            TextMeshProUGUI hint = UiKit.Label(panel.transform, "Hint", Strings.Get("raid_hint"), UiTextSize.Small, theme);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(400, 24));
            hint.color = theme.Muted;
            panel.gameObject.SetActive(false);

            UiKit.Bind(screen, "_panel", panel.gameObject);
            UiKit.Bind(screen, "_hullFill", fillRect);
            UiKit.Bind(screen, "_hullLabel", hull);
            UiKit.Bind(screen, "_countLabel", counts);
            UiKit.Bind(screen, "_hint", hint);
            return root;
        }
    }
}
