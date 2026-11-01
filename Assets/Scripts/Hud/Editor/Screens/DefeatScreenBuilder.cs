using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022. 상황 화면 "Defeat"(전체 화면 그늘 + 가운데 패배 카드) 프리팹 내용을 만든다.</summary>
    public static class DefeatScreenBuilder
    {
        /// <summary>그늘 불투명도(옛 #defeat 배경 0.86).</summary>
        private const float SHADE_ALPHA = 0.86f;

        /// <summary>화면 루트(UiScreen "Defeat", 상황 화면) + DefeatScreen + 클릭을 막는 그늘과 카드(제목·본문).</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("Defeat", false);
            DefeatScreen screen = root.AddComponent<DefeatScreen>();
            Color shadeColor = theme.Panel; shadeColor.a = SHADE_ALPHA;
            Image shade = UiKit.Image(root.transform, "Shade", null, shadeColor, true);
            shade.preserveAspect = false;
            UiKit.Stretch(shade.rectTransform);
            Image card = UiKit.Panel(shade.transform, "Card", theme);
            UiKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 200));
            TextMeshProUGUI title = UiKit.Label(card.transform, "Title", Strings.Get("defeat_title"), UiTextSize.Title, theme);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(580, 50));
            title.fontSize = theme.Size(UiTextSize.Title) * 1.25f;
            title.color = theme.Accent;
            TextMeshProUGUI body = UiKit.Label(card.transform, "Body", Strings.Get("defeat_body"), UiTextSize.Normal, theme);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(580, 60));
            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_body", body);
            return root;
        }
    }
}
