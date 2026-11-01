using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Presentation.Interior;
using IronRing.Hud.Hotbar;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Presentation.Items;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>
    /// 스토리 022. 항상 켜진 화면 "Hotbar"(거점 내부 하단 9칸·팔레트·알림·드래그 칩) 프리팹 내용과 부품 HotbarSlot·PaletteItem을 만든다.
    /// 9칸은 생성 때 슬롯 부품 인스턴스로 넣고(디자이너가 보이게), 팔레트 항목은 런타임에 부품에서 만든다(예시 4개는 런타임에 지운다).
    /// </summary>
    public static class HotbarScreenBuilder
    {
        /// <summary>슬롯 부품 경로.</summary>
        public const string SLOT_PART = BuildMainCanvas.PARTS_FOLDER + "/HotbarSlot.prefab";
        /// <summary>팔레트 항목 부품 경로.</summary>
        public const string PALETTE_PART = BuildMainCanvas.PARTS_FOLDER + "/PaletteItem.prefab";

        private static readonly Vector2 SlotSize = new Vector2(66, 74);
        private static readonly Vector2 EntrySize = new Vector2(78, 72);

        /// <summary>화면 루트(UiScreen "Hotbar", 항상 켜짐) + HotbarScreen + 내용.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject slotPart = UiKit.SavePrefabIfMissing(SLOT_PART, () => Cell(theme, "HotbarSlot", SlotSize, true));
            GameObject entryPart = UiKit.SavePrefabIfMissing(PALETTE_PART, () => Cell(theme, "PaletteItem", EntrySize, false));
            HotbarData data = HotbarData.Load();
            ItemSpriteSet sprites = ItemSpriteSet.Load();

            GameObject root = UiKit.ScreenRoot("Hotbar", true);
            HotbarScreen screen = root.AddComponent<HotbarScreen>();
            RectTransform content = UiKit.Stretch(UiKit.Node(root.transform, "Content"));

            // 팔레트(기본 꺼짐: 토글 버튼으로 연다)
            Image palette = UiKit.Panel(content, "Palette", theme);
            UiKit.Place(palette.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(760, 330));
            TextMeshProUGUI paletteTitle = UiKit.Label(palette.transform, "Title", Strings.Get("hotbar_palette_title"), UiTextSize.Title, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(paletteTitle.rectTransform, 34, 8, 16, 100);
            paletteTitle.color = theme.Accent;
            Button paletteClose = UiKit.Button(palette.transform, "Close", Strings.Get("inv_close"), theme);
            UiKit.Place((RectTransform)paletteClose.transform, Vector2.one, Vector2.one, new Vector2(-12, -10), new Vector2(76, theme.ButtonHeight));
            TextMeshProUGUI paletteHint = UiKit.Label(palette.transform, "Hint", Strings.Get("hotbar_palette_hint"), UiTextSize.Small, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.StretchTop(paletteHint.rectTransform, 22, 44, 16, 16);
            paletteHint.color = theme.Muted;
            ScrollRect list = UiKit.ScrollGrid(palette.transform, "List", EntrySize, new Vector2(6, 6), 8, out RectTransform listContent);
            UiKit.Stretch((RectTransform)list.transform, 14, 72, 14, 12);
            int sample = 0;
            foreach (HotbarData.SlotData slot in data.Slots)
            {
                if (slot == null || sample >= 4) break;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(entryPart, listContent);
                Preview(item.GetComponent<HotbarCellView>(), slot.ToEntry(), sprites, null);
                sample++;
            }
            palette.gameObject.SetActive(false);

            // 하단 막대: 9칸 + 팔레트 버튼
            Image bar = UiKit.Bar(content, "Bar", theme);
            UiKit.Place(bar.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(758, 90));
            HorizontalOrVerticalLayoutGroup row = UiKit.Row(bar.transform, "Slots", false, 6);
            row.childAlignment = TextAnchor.MiddleLeft;
            UiKit.Stretch((RectTransform)row.transform, 10, 8, 104, 8);
            var views = new HotbarCellView[HotbarScreen.SLOT_COUNT];
            for (int i = 0; i < views.Length; i++)
            {
                var cell = (GameObject)PrefabUtility.InstantiatePrefab(slotPart, row.transform);
                cell.name = "Slot" + (i + 1);
                views[i] = cell.GetComponent<HotbarCellView>();
                HotbarData.SlotData slot = i < data.Slots.Length ? data.Slots[i] : null;
                Preview(views[i], slot != null ? slot.ToEntry() : default, sprites, (i + 1).ToString());
                if (i == 0) views[i].SetState(true, false, theme);
            }
            Button toggle = UiKit.Button(bar.transform, "PaletteToggle", Strings.Get("hotbar_palette"), theme);
            UiKit.Place((RectTransform)toggle.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(86, theme.ButtonHeight));

            // 안내 줄과 알림
            Image hintBox = UiKit.Sliced(content, "HintBox", theme.ToastSprite, theme, false);
            UiKit.Place(hintBox.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 108), new Vector2(520, 26));
            TextMeshProUGUI hint = UiKit.Label(hintBox.transform, "Hint", Strings.Get("hotbar_hint"), UiTextSize.Small, theme);
            UiKit.Stretch(hint.rectTransform, 8, 1, 8, 1);
            hint.color = theme.Muted;
            Image noticeBox = UiKit.Sliced(content, "Notice", theme.ToastSprite, theme, false);
            UiKit.Place(noticeBox.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 138), new Vector2(460, 32));
            TextMeshProUGUI notice = UiKit.Label(noticeBox.transform, "Text", Strings.Get("ui_sample_notice"), UiTextSize.Normal, theme);
            UiKit.Stretch(notice.rectTransform, 10, 2, 10, 2);
            notice.color = theme.Accent;

            // 드래그 칩(드래그 중에만 켜짐, 클릭 통과)
            Image chip = UiKit.Sliced(content, "DragChip", theme.SlotSelectedSprite, theme, false);
            UiKit.Place(chip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
            Image chipIcon = UiKit.Image(chip.transform, "Icon", null, Color.white);
            UiKit.Stretch(chipIcon.rectTransform, 6, 6, 6, 6);
            chip.gameObject.SetActive(false);

            UiKit.Bind(screen, "_content", content.gameObject);
            BindArray(screen, "_slotViews", views);
            UiKit.Bind(screen, "_hint", hint);
            UiKit.Bind(screen, "_noticeBox", noticeBox.gameObject);
            UiKit.Bind(screen, "_notice", notice);
            UiKit.Bind(screen, "_paletteToggle", toggle);
            UiKit.Bind(screen, "_palette", palette.gameObject);
            UiKit.Bind(screen, "_paletteTitle", paletteTitle);
            UiKit.Bind(screen, "_paletteHint", paletteHint);
            UiKit.Bind(screen, "_paletteClose", paletteClose);
            UiKit.Bind(screen, "_paletteContent", listContent);
            UiKit.Bind(screen, "_paletteItemPrefab", entryPart.GetComponent<HotbarCellView>());
            UiKit.Bind(screen, "_dragChip", chip.rectTransform);
            UiKit.Bind(screen, "_dragIcon", chipIcon);
            return root;
        }

        /// <summary>슬롯·팔레트 항목 부품: 슬롯 틀(클릭 받음) + 아이콘 + 이름(자동 축소) + 키 번호(withKey).</summary>
        private static GameObject Cell(UiTheme theme, string name, Vector2 size, bool withKey)
        {
            Image frame = UiKit.Slot(null, name, theme);
            frame.rectTransform.sizeDelta = size;
            HotbarCellView view = frame.gameObject.AddComponent<HotbarCellView>();
            Image icon = UiKit.Image(frame.transform, "Icon", null, Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(40, 40));
            icon.enabled = false;
            TextMeshProUGUI label = UiKit.Label(frame.transform, "Name", "", UiTextSize.Small, theme);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = new Vector2(1, 0); labelRect.pivot = new Vector2(0.5f, 0);
            labelRect.offsetMin = new Vector2(3, 3); labelRect.offsetMax = new Vector2(-3, 22);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMax = theme.Size(UiTextSize.Small);
            label.fontSizeMin = theme.Size(UiTextSize.Small) * 0.6f;
            UiKit.Bind(view, "_frame", frame);
            UiKit.Bind(view, "_icon", icon);
            UiKit.Bind(view, "_name", label);
            if (withKey)
            {
                TextMeshProUGUI key = UiKit.Label(frame.transform, "Key", "1", UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
                UiKit.Place(key.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(5, -3), new Vector2(22, 20));
                key.color = theme.Accent;
                UiKit.Bind(view, "_key", key);
            }
            return frame.gameObject;
        }

        /// <summary>편집 모드 미리보기: Hotbar.json 배정의 아이콘(ItemSprites)과 한국어 이름.</summary>
        private static void Preview(HotbarCellView view, PlacementEntry entry, ItemSpriteSet sprites, string key)
        {
            if (view == null) return;
            if (key != null) view.SetKey(key);
            if (!entry.IsValid) { view.Show(null, ""); return; }
            string name = Lookup((entry.Kind == PlacementKind.Facility ? "facility_" : "item_") + entry.Id) ?? entry.Id;
            Sprite sprite = entry.Kind == PlacementKind.Item && sprites != null ? sprites.Get(entry.Id) : null;
            view.Show(sprite, name);
        }

        private static string Lookup(string key)
        {
            try { return Strings.Get(key); }
            catch (System.Collections.Generic.KeyNotFoundException) { return null; }
        }

        /// <summary>target의 배열 필드에 values를 꽂는다.</summary>
        internal static void BindArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field) ?? throw new System.ArgumentException(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
