using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Codex;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Presentation.Items;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>
    /// 스토리 022. 상황 화면 "Codex"(서식지 레시피 도감) 프리팹 내용과 부품 CodexCard·TileIcon을 만든다.
    /// 카드는 런타임에 카탈로그 수(19)만큼 부품에서 만들고, 프리팹에는 미리보기 카드 2장(해금 1·잠김 1)을 둔다(런타임에 지운다).
    /// </summary>
    public static class CodexScreenBuilder
    {
        /// <summary>카드 부품 경로.</summary>
        public const string CARD_PART = BuildMainCanvas.PARTS_FOLDER + "/CodexCard.prefab";
        /// <summary>타일 칸 부품 경로.</summary>
        public const string TILE_PART = BuildMainCanvas.PARTS_FOLDER + "/TileIcon.prefab";

        private static readonly Vector2 CardSize = new Vector2(304, 200);
        private const float TILE = 26;

        /// <summary>화면 루트(UiScreen "Codex", 상황 화면) + CodexScreen + 패널·머리줄·카드 격자.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject tilePart = UiKit.SavePrefabIfMissing(TILE_PART, () => Tile(theme));
            // 표식 .bridge/rebuild/CodexCard가 있으면 카드 부품도 같은 경로(GUID 유지)에 다시 만든다.
            GameObject cardPart = UiKit.SavePrefab(CARD_PART, () => Card(theme), ConsumeMarker("CodexCard"));

            GameObject root = UiKit.ScreenRoot("Codex", false);
            CodexScreen screen = root.AddComponent<CodexScreen>();
            Image panel = UiKit.Panel(root.transform, "Panel", theme);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0); rect.anchorMax = new Vector2(0.5f, 1); rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-500, 90); rect.offsetMax = new Vector2(500, -150);

            TextMeshProUGUI title = UiKit.Label(panel.transform, "Title", Strings.Get("ui_sample_codex_title"), UiTextSize.Title, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(title.rectTransform, 36, 8, 18, 120);
            title.color = theme.Accent;
            TextMeshProUGUI hint = UiKit.Label(panel.transform, "Hint", Strings.Get("codex_hint"), UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(hint.rectTransform, 24, 44, 18, 120); // 제목 아래 둘째 줄(겹치지 않게)
            hint.color = theme.Muted;
            Button close = UiKit.Button(panel.transform, "Close", Strings.Get("codex_close"), theme);
            UiKit.Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-14, -12), new Vector2(80, 32));

            ScrollRect scroll = UiKit.ScrollGrid(panel.transform, "Grid", CardSize, new Vector2(8, 8), 3, out RectTransform grid);
            UiKit.Stretch((RectTransform)scroll.transform, 14, 76, 14, 12);
            scroll.scrollSensitivity = 30;

            // 미리보기 카드(런타임에 지운다)
            ItemSpriteSet sprites = ItemSpriteSet.Load();
            var sample = ((GameObject)PrefabUtility.InstantiatePrefab(cardPart, grid)).GetComponent<CodexCardView>();
            sample.ShowUnlocked(Strings.Get("ui_sample_codex_name"),
                Strings.Get("codex_kind_cluster") + "  ·  " + string.Format(Strings.Get("codex_cells"), 4),
                string.Format(Strings.Get("codex_bonus"), "10"), string.Format(Strings.Get("codex_npc"), Strings.Get("ui_sample_codex_visitors")), true);
            sample.SetColumns(0);
            for (int i = 0; i < 4; i++)
                ((GameObject)PrefabUtility.InstantiatePrefab(tilePart, sample.Diagram)).GetComponent<CodexTileView>()
                    .Show(sprites != null ? sprites.Get("tile_boiler") : null, Strings.Get("ui_sample_codex_name").Substring(0, 1), false);
            var locked = ((GameObject)PrefabUtility.InstantiatePrefab(cardPart, grid)).GetComponent<CodexCardView>();
            locked.ShowLocked(Strings.Get("codex_locked"));

            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_hint", hint);
            UiKit.Bind(screen, "_close", close);
            UiKit.Bind(screen, "_grid", grid);
            UiKit.Bind(screen, "_cardPrefab", cardPart.GetComponent<CodexCardView>());
            UiKit.Bind(screen, "_tilePrefab", tilePart.GetComponent<CodexTileView>());
            return root;
        }

        private static bool ConsumeMarker(string id)
        {
            string marker = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, ".bridge", "rebuild", id);
            if (!System.IO.File.Exists(marker)) return false;
            System.IO.File.Delete(marker);
            Debug.Log("MAINCANVAS REBUILT PART: " + id);
            return true;
        }

        /// <summary>카드 부품: 슬롯 틀 바탕, 이름·완성 표시·종류 줄·배열 격자·보너스·NPC, 잠김 실루엣.</summary>
        private static GameObject Card(UiTheme theme)
        {
            Image frame = UiKit.Slot(null, "CodexCard", theme);
            frame.raycastTarget = false; // 스크롤 휠·드래그는 뷰포트가 받는다
            frame.rectTransform.sizeDelta = CardSize;
            CodexCardView view = frame.gameObject.AddComponent<CodexCardView>();

            TextMeshProUGUI name = UiKit.Label(frame.transform, "Name", "", UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(name.rectTransform, 34, 2, 12, 80); // 줄 높이(약 29px)보다 높아야 Ellipsis가 줄을 지우지 않는다
            name.color = theme.Accent;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;

            Image badge = UiKit.Sliced(frame.transform, "Badge", theme.SlotSelectedSprite, theme, false);
            UiKit.Place(badge.rectTransform, Vector2.one, Vector2.one, new Vector2(-8, -8), new Vector2(64, 24));
            TextMeshProUGUI badgeText = UiKit.Label(badge.transform, "Text", Strings.Get("codex_complete"), UiTextSize.Small, theme);
            badgeText.color = theme.Accent;

            TextMeshProUGUI meta = UiKit.Label(frame.transform, "Meta", "", UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(meta.rectTransform, 20, 37, 12, 12);
            meta.color = theme.Muted;

            RectTransform diagram = UiKit.Node(frame.transform, "Diagram");
            UiKit.StretchTop(diagram, TILE * 3 + 8, 60, 12, 12);
            GridLayoutGroup grid = diagram.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TILE, TILE);
            grid.spacing = new Vector2(3, 3);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 9;

            TextMeshProUGUI bonus = UiKit.Label(frame.transform, "Bonus", "", UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(bonus.rectTransform, 20, 148, 12, 12);
            TextMeshProUGUI npc = UiKit.Label(frame.transform, "Npc", "", UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(npc.rectTransform, 20, 170, 12, 12);
            npc.color = theme.Muted;
            // 스토리 025: 후보가 여럿이면 길어지므로 한 줄에 맞게 줄이고, 그래도 넘치면 말줄임
            npc.textWrappingMode = TextWrappingModes.NoWrap;
            npc.overflowMode = TextOverflowModes.Ellipsis;
            npc.enableAutoSizing = true;
            npc.fontSizeMax = theme.Size(UiTextSize.Small);
            npc.fontSizeMin = theme.Size(UiTextSize.Small) * 0.7f;

            Image silhouette = UiKit.Sliced(frame.transform, "Silhouette", theme.SlotSprite, theme, false);
            UiKit.Place(silhouette.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(150, 110));
            Color dark = theme.Panel * 0.45f; dark.a = 1f;
            silhouette.color = dark;
            silhouette.gameObject.SetActive(false);

            UiKit.Bind(view, "_name", name);
            UiKit.Bind(view, "_meta", meta);
            UiKit.Bind(view, "_bonus", bonus);
            UiKit.Bind(view, "_npc", npc);
            UiKit.Bind(view, "_badge", badge.gameObject);
            UiKit.Bind(view, "_silhouette", silhouette.gameObject);
            UiKit.Bind(view, "_diagram", diagram);
            UiKit.Bind(view, "_diagramGrid", grid);
            return frame.gameObject;
        }

        /// <summary>타일 칸 부품: 슬롯 틀 + 아이콘 + 글자(아이콘이 없을 때).</summary>
        private static GameObject Tile(UiTheme theme)
        {
            Image frame = UiKit.Slot(null, "TileIcon", theme);
            frame.raycastTarget = false;
            frame.rectTransform.sizeDelta = new Vector2(TILE, TILE);
            CodexTileView view = frame.gameObject.AddComponent<CodexTileView>();
            Image icon = UiKit.Image(frame.transform, "Icon", null, Color.white);
            UiKit.Stretch(icon.rectTransform, 3, 3, 3, 3);
            icon.enabled = false;
            TextMeshProUGUI count = UiKit.Label(frame.transform, "Count", "", UiTextSize.Small, theme);
            count.enableAutoSizing = true;
            count.fontSizeMax = theme.Size(UiTextSize.Small);
            count.fontSizeMin = theme.Size(UiTextSize.Small) * 0.5f;
            UiKit.Bind(view, "_frame", frame);
            UiKit.Bind(view, "_icon", icon);
            UiKit.Bind(view, "_count", count);
            return frame.gameObject;
        }
    }
}
