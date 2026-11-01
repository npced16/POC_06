using UnityEngine;

using UnityEditor;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Hud.Warehouse;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    public static class WarehouseScreenBuilder
    {
        /// <summary>theme으로 창고 패널과 품목 여섯 행을 만들고 참조가 저장된 화면 루트를 반환한다.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("Warehouse", false);
            WarehouseScreen screen = root.AddComponent<WarehouseScreen>();
            IconCatalog icons = AssetDatabase.LoadAssetAtPath<IconCatalog>("Assets/Prefabs/UI/IconCatalog.asset");
            var panel = UiKit.Panel(root.transform, "WarehousePanel", theme);
            UiKit.Place(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 20), new Vector2(760, 420));
            var title = Text(panel.transform, "Title", string.Format(Strings.Get("ui_warehouse_title"), Strings.Get("hub_1"), 0, 60), 24, 10, 620, 42, UiTextSize.Title, theme);
            var close = UiKit.Button(panel.transform, "Close", Strings.Get("inv_close"), theme);
            UiKit.Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-16, -14), new Vector2(80, theme.ButtonHeight));
            Text(panel.transform, "Hint", Strings.Get("ui_warehouse_hint"), 24, 52, 712, 24, UiTextSize.Small, theme);
            string[] ids = { "coal", "iron", "parts", "food", "glass", "brass" };
            var rows = new WarehouseRowView[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                RectTransform row = UiKit.Node(panel.transform, ids[i]);
                UiKit.Place(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -82 - i * 46), new Vector2(712, 44));
                rows[i] = row.gameObject.AddComponent<WarehouseRowView>();
                UiKit.Set(rows[i], "_goodsId", ids[i]);
                var icon = UiKit.Image(row, "Icon", icons.Get("goods_" + ids[i]), Color.white, false);
                UiKit.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(40, 40));
                UiKit.Bind(rows[i], "_icon", icon);
                UiKit.Bind(rows[i], "_name", Text(row, "Name", Strings.Get("ui_trade_goods_" + ids[i]), 54, 8, 88, 28, UiTextSize.Normal, theme));
                UiKit.Bind(rows[i], "_stock", Text(row, "Stock", string.Format(Strings.Get("ui_warehouse_stock"), 0), 156, 8, 160, 28, UiTextSize.Normal, theme));
                UiKit.Bind(rows[i], "_cargoCount", Text(row, "Cargo", string.Format(Strings.Get("ui_warehouse_train"), 0), 326, 8, 160, 28, UiTextSize.Normal, theme));
                var load = UiKit.Button(row, "Load", Strings.Get("ui_warehouse_load"), theme);
                var unload = UiKit.Button(row, "Unload", Strings.Get("ui_warehouse_unload"), theme);
                UiKit.Place((RectTransform)load.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(516, 0), new Vector2(88, theme.ButtonHeight));
                UiKit.Place((RectTransform)unload.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(616, 0), new Vector2(96, theme.ButtonHeight));
                load.interactable = false; unload.interactable = false;
                UiKit.Bind(rows[i], "_load", load); UiKit.Bind(rows[i], "_unload", unload);
            }
            var cargo = Text(panel.transform, "CargoTotal", string.Format(Strings.Get("ui_warehouse_cargo"), 0, 12), 24, 374, 712, 30, UiTextSize.Normal, theme);
            UiKit.Bind(screen, "_title", title); UiKit.Bind(screen, "_cargoTotal", cargo); UiKit.Bind(screen, "_close", close); UiKit.Bind(screen, "_icons", icons);
            HotbarScreenBuilder.BindArray(screen, "_rows", rows);
            return root;
        }

        /// <summary>parent 아래 name 라벨을 주어진 위치·크기·테마로 만들고 텍스트 컴포넌트를 반환한다.</summary>
        private static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float width, float height, UiTextSize size, UiTheme theme)
        {
            var label = UiKit.Label(parent, name, value, size, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
            return label;
        }
    }
}
