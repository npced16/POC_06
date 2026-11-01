using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022. 항상 켜진 화면 "TopBar"(위 띠: 현재 위치·바퀴·승객 수·도감 버튼, 왼쪽 아래: 다음 라운드·출발) 프리팹 내용을 만든다.</summary>
    public static class TopBarScreenBuilder
    {
        /// <summary>화면 루트(UiScreen "TopBar", 항상 켜짐) + TopBarScreen + 위 띠 패널과 아래 행동 버튼 줄.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("TopBar", true);
            TopBarScreen screen = root.AddComponent<TopBarScreen>();

            Image bar = UiKit.Bar(root.transform, "Bar", theme);
            UiKit.StretchTop(bar.rectTransform, 54, 12, 16, 16);
            TextMeshProUGUI location = UiKit.Label(bar.transform, "Location", Strings.Get("hud_location") + "  " + Strings.Get("hub_1"), UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(location.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(300, 44));
            TextMeshProUGUI lap = UiKit.Label(bar.transform, "Lap", Strings.Get("hud_turn") + "  3", UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(lap.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(570, 0), new Vector2(140, 44));
            lap.color = theme.Accent;
            IconCatalog icons = AssetDatabase.LoadAssetAtPath<IconCatalog>("Assets/Prefabs/UI/IconCatalog.asset");
            var coin = UiKit.Image(bar.transform, "Coin", icons.Get("ui_coin"), Color.white, false);
            UiKit.Place(coin.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(328, 0), new Vector2(28, 28));
            var balance = UiKit.Label(bar.transform, "Balance", string.Format(Strings.Get("ui_topbar_balance"), 0), UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(balance.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(364, 0), new Vector2(200, 44));
            UiKit.Bind(screen, "_balance", balance); UiKit.Bind(screen, "_coin", coin); UiKit.Bind(screen, "_icons", icons);
            Button codex = UiKit.Button(bar.transform, "Codex", Strings.Get("codex_open"), theme);
            UiKit.Place((RectTransform)codex.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(132, theme.ButtonHeight));
            // 스토리 025: 승객 수 버튼(도감 버튼 왼쪽)
            Button passengers = UiKit.Button(bar.transform, "Passengers", string.Format(Strings.Get("hud_passengers"), 2, 3), theme);
            UiKit.Place((RectTransform)passengers.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-156, 0), new Vector2(160, theme.ButtonHeight));

            RectTransform actions = UiKit.Node(root.transform, "Actions");
            UiKit.Place(actions, Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(320, theme.ButtonHeight));
            HorizontalOrVerticalLayoutGroup row = UiKit.Row(actions, "Row", false, 6);
            row.childAlignment = TextAnchor.MiddleLeft;
            Button depart = UiKit.Button(row.transform, "Depart", Strings.Get("hud_depart"), theme);
            ((RectTransform)depart.transform).sizeDelta = new Vector2(150, theme.ButtonHeight);

            UiKit.Bind(screen, "_location", location);
            UiKit.Bind(screen, "_lap", lap);
            UiKit.Bind(screen, "_depart", depart);
            UiKit.Bind(screen, "_codex", codex);
            UiKit.Bind(screen, "_passengers", passengers);
            UiKit.Bind(screen, "_catalogJson", AssetDatabase.LoadAssetAtPath<TextAsset>(InteriorTitleScreenBuilder.CATALOG));
            return root;
        }
    }
}
