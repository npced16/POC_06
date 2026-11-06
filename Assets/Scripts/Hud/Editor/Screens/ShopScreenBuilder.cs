using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스펙 010. 녹슨 다리 찢어진 설계도 상점의 전면 화면 프리팹(상품 카드 4칸·잔고·구매 결과·계속)을 만든다.</summary>
    public static class ShopScreenBuilder
    {
        private const int CARD_COUNT = 4;
        private static readonly Vector2 _panelSize = new Vector2(960f, 600f);
        private static readonly Vector2 _cardSize = new Vector2(210f, 250f);
        private const float CARD_SPACING = 16f;

        /// <summary>theme으로 월드 입력을 가리는 배경, 상점 패널, 상품 카드를 만들고 ShopScreen 참조를 바인딩해 반환한다.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("Shop", false);
            ShopScreen screen = root.AddComponent<ShopScreen>();

            Image backdrop = UiKit.Image(root.transform, "Dimmer", null, new Color(0.05f, 0.06f, 0.07f, 0.82f), true);
            backdrop.preserveAspect = false;
            UiKit.Stretch(backdrop.rectTransform);
            Image panel = UiKit.Panel(root.transform, "Panel", theme);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, _panelSize);

            TextMeshProUGUI title = UiKit.Label(panel.transform, "Title", "", UiTextSize.Heading, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(600f, 44f));
            title.color = theme.Accent;
            TextMeshProUGUI balance = UiKit.Label(panel.transform, "Balance", "", UiTextSize.Normal, theme, TextAlignmentOptions.MidlineRight);
            UiKit.Place(balance.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -24f), new Vector2(260f, 44f));
            TextMeshProUGUI intro = UiKit.Label(panel.transform, "Intro", "", UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(intro.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(888f, 52f));
            intro.color = theme.Muted;

            var cards = new GameObject[CARD_COUNT];
            var names = new TMP_Text[CARD_COUNT];
            var prices = new TMP_Text[CARD_COUNT];
            var reasons = new TMP_Text[CARD_COUNT];
            var buttons = new Button[CARD_COUNT];
            float rowWidth = CARD_COUNT * _cardSize.x + (CARD_COUNT - 1) * CARD_SPACING;
            for (int i = 0; i < CARD_COUNT; i++)
            {
                Image card = UiKit.Panel(panel.transform, "Card" + (i + 1), theme, false);
                float x = -rowWidth * 0.5f + _cardSize.x * 0.5f + i * (_cardSize.x + CARD_SPACING);
                UiKit.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -150f), _cardSize);
                TextMeshProUGUI name = UiKit.Label(card.transform, "Name", "", UiTextSize.Normal, theme, TextAlignmentOptions.Top);
                UiKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(_cardSize.x - 24f, 60f));
                name.enableAutoSizing = true;
                name.fontSizeMax = theme.Size(UiTextSize.Normal);
                name.fontSizeMin = theme.Size(UiTextSize.Small);
                TextMeshProUGUI price = UiKit.Label(card.transform, "Price", "", UiTextSize.Normal, theme, TextAlignmentOptions.Center);
                UiKit.Place(price.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(_cardSize.x - 24f, 36f));
                price.color = theme.Accent;
                TextMeshProUGUI reason = UiKit.Label(card.transform, "Reason", "", UiTextSize.Small, theme, TextAlignmentOptions.Center);
                UiKit.Place(reason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(_cardSize.x - 24f, 40f));
                reason.color = theme.Muted;
                Button buy = UiKit.Button(card.transform, "Buy", Strings.Get("shop_buy"), theme);
                UiKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(_cardSize.x - 36f, theme.ButtonHeight));
                cards[i] = card.gameObject;
                names[i] = name;
                prices[i] = price;
                reasons[i] = reason;
                buttons[i] = buy;
            }

            TextMeshProUGUI empty = UiKit.Label(panel.transform, "Empty", "", UiTextSize.Normal, theme, TextAlignmentOptions.Center);
            UiKit.Place(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(888f, 60f));
            empty.color = theme.Muted;
            empty.gameObject.SetActive(false);
            TextMeshProUGUI result = UiKit.Label(panel.transform, "Result", "", UiTextSize.Normal, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(result.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 96f), new Vector2(700f, 70f));
            Button continueButton = UiKit.Button(panel.transform, "Continue", Strings.Get("loop_continue"), theme);
            UiKit.Place((RectTransform)continueButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 28f), new Vector2(160f, theme.ButtonHeight));

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_intro", intro);
            UiKit.Bind(screen, "_balance", balance);
            ScreenArrayBinding.BindArray(screen, "_cards", cards);
            ScreenArrayBinding.BindArray(screen, "_names", names);
            ScreenArrayBinding.BindArray(screen, "_prices", prices);
            ScreenArrayBinding.BindArray(screen, "_reasons", reasons);
            ScreenArrayBinding.BindArray(screen, "_buyButtons", buttons);
            UiKit.Bind(screen, "_empty", empty);
            UiKit.Bind(screen, "_result", result);
            UiKit.Bind(screen, "_continue", continueButton);
            return root;
        }
    }
}
