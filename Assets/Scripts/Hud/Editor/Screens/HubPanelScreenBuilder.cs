using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022. 상황 화면 "HubPanel"(오른쪽 거점 패널) 프리팹과 부품 FacilityRow.prefab을 만든다.</summary>
    public static class HubPanelScreenBuilder
    {
        /// <summary>시설 한 줄 부품 프리팹 경로.</summary>
        public const string ROW_PART = BuildMainCanvas.PARTS_FOLDER + "/FacilityRow.prefab";

        private const float WIDTH = 330;
        private const float INNER = WIDTH - 28;
        private const float ROW_HEIGHT = 36;

        /// <summary>화면 루트(UiScreen "HubPanel", 상황 화면) + HubPanelScreen + 패널(제목·닫기·상태·서식지·시설 목록·안내).</summary>
        public static GameObject Build(UiTheme theme)
        {
            TextAsset catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(InteriorTitleScreenBuilder.CATALOG);
            HudCatalog catalog = JsonUtility.FromJson<HudCatalog>(catalogAsset.text);
            int rows = catalog.Facilities.Length;
            float listTop = 152;
            float messageTop = listTop + rows * (ROW_HEIGHT + 2) + 8;
            float height = messageTop + 56;

            GameObject root = UiKit.ScreenRoot("HubPanel", false);
            HubPanelScreen screen = root.AddComponent<HubPanelScreen>();
            Image panel = UiKit.Panel(root.transform, "Panel", theme);
            UiKit.Place(panel.rectTransform, Vector2.one, Vector2.one, new Vector2(-18, -84), new Vector2(WIDTH, height));

            TextMeshProUGUI title = UiKit.Label(panel.transform, "Title", Strings.Get("hub_1"), UiTextSize.Title, theme, TextAlignmentOptions.MidlineLeft);
            TopLeft(title.rectTransform, 12, 230, 40);
            title.color = theme.Accent;
            Button close = UiKit.Button(panel.transform, "Close", Strings.Get("hud_close"), theme);
            UiKit.Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-12, -14), new Vector2(76, 34));
            TextMeshProUGUI state = UiKit.Label(panel.transform, "State", Strings.Get("hud_prosperity") + "  12    " + Strings.Get("hud_npcs") + "  2", UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            TopLeft(state.rectTransform, 58, INNER, 26);
            TextMeshProUGUI habitat = UiKit.Label(panel.transform, "Habitat", Strings.Get("hud_habitat") + "  " + Strings.Get("hud_complete"), UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            TopLeft(habitat.rectTransform, 88, INNER, 26);
            TextMeshProUGUI section = UiKit.Label(panel.transform, "FacilitiesTitle", Strings.Get("hud_facilities"), UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            TopLeft(section.rectTransform, 120, INNER, 28);
            section.color = theme.Accent;

            RectTransform list = UiKit.Node(panel.transform, "Facilities");
            TopLeft(list, listTop, INNER, rows * (ROW_HEIGHT + 2));
            HorizontalOrVerticalLayoutGroup column = UiKit.Row(list, "Rows", true, 2);
            column.childAlignment = TextAnchor.UpperLeft;
            GameObject rowPrefab = UiKit.SavePrefabIfMissing(ROW_PART, () => BuildRow(theme));
            var placed = new List<FacilityRow>();
            foreach (string facility in catalog.Facilities)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(rowPrefab, column.transform);
                instance.name = "Row_" + facility;
                FacilityRow row = instance.GetComponent<FacilityRow>();
                if (row.Label != null) row.Label.text = Strings.Get("facility_" + facility) + "  0";
                placed.Add(row);
            }

            TextMeshProUGUI message = UiKit.Label(panel.transform, "Message", Strings.Get("train_stopped"), UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
            TopLeft(message.rectTransform, messageTop, INNER, 44);
            message.color = theme.Muted;

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_state", state);
            UiKit.Bind(screen, "_habitatLine", habitat);
            UiKit.Bind(screen, "_message", message);
            UiKit.Bind(screen, "_close", close);
            UiKit.Bind(screen, "_catalogJson", catalogAsset);
            ScreenArrayBinding.BindArray(screen, "_rows", placed);
            return root;
        }

        /// <summary>패널 왼쪽 위 기준(여백 14) top 아래에 width×height로 놓는다.</summary>
        private static void TopLeft(RectTransform rect, float top, float width, float height)
        {
            UiKit.Place(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -top), new Vector2(width, height));
        }

        /// <summary>FacilityRow 부품: 이름·개수 라벨(왼쪽) + 짓기 버튼(오른쪽).</summary>
        private static GameObject BuildRow(UiTheme theme)
        {
            RectTransform root = UiKit.Node(null, "FacilityRow");
            root.sizeDelta = new Vector2(INNER, ROW_HEIGHT);
            FacilityRow row = root.gameObject.AddComponent<FacilityRow>();
            TextMeshProUGUI label = UiKit.Label(root, "Label", Strings.Get("facility_wall") + "  0", UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(label.rectTransform, 4, 0, 96, 0);
            Button build = UiKit.Button(root, "Build", Strings.Get("hud_build"), theme);
            UiKit.Place((RectTransform)build.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(88, 30));
            UiKit.Bind(row, "_label", label);
            UiKit.Bind(row, "_build", build);
            return root.gameObject;
        }
    }
}
