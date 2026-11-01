using UnityEngine;

using UnityEditor;

using TMPro;

using IronRing.Hud.Trade;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    public static class TradeScreenBuilder
    {
        /// <summary>theme을 사용해 중앙 거래 패널과 여섯 품목 행을 만들고 직렬화된 화면 루트를 반환한다.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("Trade", true);
            TradeScreen screen = root.AddComponent<TradeScreen>();
            IconCatalog icons = AssetDatabase.LoadAssetAtPath<IconCatalog>("Assets/Prefabs/UI/IconCatalog.asset");
            var panel = UiKit.Panel(root.transform, "Market", theme);
            UiKit.Place(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -82), new Vector2(760, 476));
            var title = Text(panel.transform, "Title", Strings.Get("ui_trade_empty"), 24, 10, 712, 42, UiTextSize.Title, theme);
            var cargoIcon = UiKit.Image(panel.transform, "CargoIcon", icons.Get("ui_cargo"), Color.white, false);
            UiKit.Place(cargoIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -56), new Vector2(28, 28));
            var summary = Text(panel.transform, "Summary", string.Format(Strings.Get("ui_trade_summary"), 0, 0, 12), 60, 54, 660, 30, UiTextSize.Normal, theme);
            string[] ids = { "coal", "iron", "parts", "food", "glass", "brass" };
            var rows = new TradeRowView[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                RectTransform row = UiKit.Node(panel.transform, ids[i]);
                UiKit.Place(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -94 - i * 50), new Vector2(712, 48));
                rows[i] = row.gameObject.AddComponent<TradeRowView>();
                UiKit.Set(rows[i], "_goodsId", ids[i]);
                var icon = UiKit.Image(row, "Icon", icons.Get("goods_" + ids[i]), Color.white, false);
                UiKit.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(40, 40));
                UiKit.Bind(rows[i], "_icon", icon);
                UiKit.Bind(rows[i], "_name", Text(row, "Name", IronRing.Economy.EconomyInstaller.Goods?.NameOf(ids[i]) ?? Strings.Get("ui_trade_goods_" + ids[i]), 50, 10, 62, 28, UiTextSize.Normal, theme));
                UiKit.Bind(rows[i], "_price", Text(row, "Price", string.Format(Strings.Get("ui_trade_price"), 0, 0), 118, 0, 240, 24, UiTextSize.Small, theme));
                UiKit.Bind(rows[i], "_stock", Text(row, "Stock", string.Format(Strings.Get("ui_trade_stock"), 0, 0), 118, 24, 240, 24, UiTextSize.Small, theme));
                UiKit.Bind(rows[i], "_cargoCount", Text(row, "Owned", string.Format(Strings.Get("ui_trade_owned"), 0), 370, 10, 130, 28, UiTextSize.Small, theme));
                var buy = UiKit.Button(row, "Buy", Strings.Get("ui_trade_buy"), theme);
                var sell = UiKit.Button(row, "Sell", Strings.Get("ui_trade_sell"), theme);
                UiKit.Place((RectTransform)buy.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(520, 0), new Vector2(88, theme.ButtonHeight));
                UiKit.Place((RectTransform)sell.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(620, 0), new Vector2(88, theme.ButtonHeight));
                UiKit.Bind(rows[i], "_buy", buy); UiKit.Bind(rows[i], "_sell", sell);
            }
            var countdown = Text(panel.transform, "Countdown", "", 24, 416, 400, 30, UiTextSize.Small, theme);
            var depart = UiKit.Button(panel.transform, "Depart", Strings.Get("ui_trade_depart"), theme);
            UiKit.Place((RectTransform)depart.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 22), new Vector2(220, theme.ButtonHeight));
            var departIcon = UiKit.Image(depart.transform, "Icon", icons.Get("ui_depart"), Color.white, false);
            UiKit.Place(departIcon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(14, 0), new Vector2(24, 24));
            UiKit.Bind(screen, "_title", title); UiKit.Bind(screen, "_summary", summary); UiKit.Bind(screen, "_countdown", countdown);
            UiKit.Bind(screen, "_depart", depart); UiKit.Bind(screen, "_icons", icons);
            var serialized = new SerializedObject(screen); var list = serialized.FindProperty("_rows"); list.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>parent 아래 name 라벨을 지정된 위치·크기·테마로 생성하고 텍스트 컴포넌트를 반환한다.</summary>
        private static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float width, float height, UiTextSize size, UiTheme theme)
        {
            var label = UiKit.Label(parent, name, value, size, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
            return label;
        }
    }
}
