using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>잔고와 오른쪽 설계·승객 아이콘, 오른쪽 아래 출발 버튼을 가진 TopBar 화면을 만든다.</summary>
    public static class TopBarScreenBuilder
    {
        /// <summary>theme과 아이콘 카탈로그로 최소 HUD를 구성하고 TopBarScreen 참조를 연결한다.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("TopBar", true);
            TopBarScreen screen = root.AddComponent<TopBarScreen>();

            RectTransform bar = UiKit.Node(root.transform, "Bar");
            UiKit.StretchTop(bar, 54, 12, 16, 16);
            IconCatalog icons = AssetDatabase.LoadAssetAtPath<IconCatalog>("Assets/Prefabs/UI/IconCatalog.asset");
            var coin = UiKit.Image(bar.transform, "Coin", icons.Get("ui_coin"), Color.white, false);
            UiKit.Place(coin.rectTransform, new Vector2(1, .5f), new Vector2(.5f, .5f), new Vector2(-34, -12), new Vector2(42, 42));
            var balance = UiKit.Label(bar.transform, "Balance", "84", UiTextSize.Normal, theme);
            UiKit.Place(balance.rectTransform, new Vector2(1, .5f), new Vector2(.5f, .5f), new Vector2(-34, 22), new Vector2(80, 30));
            balance.fontStyle = FontStyles.Bold;
            balance.textWrappingMode = TextWrappingModes.NoWrap;
            balance.enableAutoSizing = true;
            balance.fontSize = 28;
            balance.fontSizeMin = 24;
            balance.fontSizeMax = 28;
            balance.outlineColor = theme.Panel;
            balance.outlineWidth = 0.2f;
            UiKit.Bind(screen, "_balance", balance); UiKit.Bind(screen, "_coin", coin); UiKit.Bind(screen, "_icons", icons);
            Button codex = UiKit.Button(root.transform, "Codex", "", theme);
            UiKit.Place((RectTransform)codex.transform, Vector2.one, Vector2.one, new Vector2(-20, -82), new Vector2(58, 58));
            Image codexIcon = UiKit.Image(codex.transform, "Icon", icons.Get("ui_book"), Color.white);
            UiKit.Place(codexIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(34, 34));

            Button passengers = UiKit.Button(root.transform, "Passengers", "", theme);
            UiKit.Place((RectTransform)passengers.transform, Vector2.one, Vector2.one, new Vector2(-20, -150), new Vector2(58, 58));
            Image passengersIcon = UiKit.Image(passengers.transform, "Icon", icons.Get("ui_people"), Color.white);
            UiKit.Place(passengersIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-3, 2), new Vector2(32, 32));
            TextMeshProUGUI passengerCount = UiKit.Label(passengers.transform, "Count", "0", UiTextSize.Normal, theme);
            UiKit.Place(passengerCount.rectTransform, Vector2.one, Vector2.one, new Vector2(3, 2), new Vector2(24, 22));
            passengerCount.fontSize = 18;

            Button depart = UiKit.Button(root.transform, "Depart", Strings.Get("hud_depart"), theme);
            UiKit.Place((RectTransform)depart.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 166), new Vector2(76, 76));
            TMP_Text departLabel = depart.GetComponentInChildren<TMP_Text>(true);
            UiKit.Place((RectTransform)departLabel.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 4), new Vector2(68, 24));
            departLabel.fontSize = 18;
            Image departIcon = UiKit.Image(depart.transform, "Icon", icons.Get("ui_depart"), Color.white);
            UiKit.Place(departIcon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -7), new Vector2(38, 38));

            UiKit.Bind(screen, "_depart", depart);
            UiKit.Bind(screen, "_codex", codex);
            UiKit.Bind(screen, "_passengers", passengers);
            UiKit.Bind(screen, "_passengerCount", passengerCount);
            return root;
        }
    }
}
