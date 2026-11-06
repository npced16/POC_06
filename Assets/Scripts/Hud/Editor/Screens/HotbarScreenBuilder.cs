using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Hotbar;
using IronRing.Combat;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Presentation.Interior;
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

        private static readonly Vector2 SlotSize = new Vector2(HotbarScreen.SLOT_WIDTH, 124);
        private static readonly Vector2 EntrySize = new Vector2(78, 98);

        /// <summary>
        /// theme과 저장된 핫바 데이터를 사용해 화면 루트와 재료 슬롯, 팔레트 프리팹을 만든다.
        /// 편집 가능한 Hotbar 화면 GameObject를 반환한다.
        /// </summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject slotPart = UiKit.SavePrefab(SLOT_PART, () => Cell(theme, "HotbarSlot", SlotSize, true), UiKit.ConsumeMarker("HotbarSlot"));
            GameObject entryPart = UiKit.SavePrefab(PALETTE_PART, () => Cell(theme, "PaletteItem", EntrySize, false), UiKit.ConsumeMarker("PaletteItem"));
            HotbarData data = HotbarData.Load();
            ItemSpriteSet sprites = ItemSpriteSet.Load();

            GameObject root = UiKit.ScreenRoot("Hotbar", true);
            HotbarScreen screen = root.AddComponent<HotbarScreen>();
            RectTransform content = UiKit.Stretch(UiKit.Node(root.transform, "Content"));

            // 인벤토리는 오른쪽 위 버튼 아래에서 펼친다.
            Image palette = UiKit.Panel(content, "Palette", theme);
            UiKit.Place(palette.rectTransform, Vector2.one, Vector2.one, new Vector2(-20, -282), new Vector2(760, 330));
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

            // 하단 막대는 빈 칸까지 포함해 아홉 칸을 항상 그린다.
            Image bar = UiKit.Bar(content, "Bar", theme);
            float barWidth = HotbarScreen.SLOT_COUNT * HotbarScreen.SLOT_WIDTH
                + (HotbarScreen.SLOT_COUNT - 1) * HotbarScreen.SLOT_SPACING + HotbarScreen.BAR_HORIZONTAL_PADDING;
            UiKit.Place(bar.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(barWidth, 140));
            HorizontalOrVerticalLayoutGroup row = UiKit.Row(bar.transform, "Slots", false, HotbarScreen.SLOT_SPACING);
            row.childAlignment = TextAnchor.MiddleLeft;
            UiKit.Stretch((RectTransform)row.transform, 10, 8, 10, 8);
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
            Button toggle = UiKit.Button(content, "PaletteToggle", "", theme);
            UiKit.Place((RectTransform)toggle.transform, Vector2.one, Vector2.one, new Vector2(-20, -286), new Vector2(58, 58));
            IconCatalog icons = AssetDatabase.LoadAssetAtPath<IconCatalog>("Assets/Prefabs/UI/IconCatalog.asset");
            Image inventoryIcon = UiKit.Image(toggle.transform, "Icon", icons.Get("ui_cargo"), Color.white);
            UiKit.Place(inventoryIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(34, 34));

            // 안내 줄과 알림
            Image hintBox = UiKit.Sliced(content, "HintBox", theme.ToastSprite, theme, false);
            hintBox.color = Color.clear;
            UiKit.Place(hintBox.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 166), new Vector2(520, 26));
            TextMeshProUGUI hint = UiKit.Label(hintBox.transform, "Hint", "", UiTextSize.Small, theme);
            UiKit.Stretch(hint.rectTransform, 8, 1, 8, 1);
            hint.color = theme.Muted;
            Image noticeBox = UiKit.Sliced(content, "Notice", theme.ToastSprite, theme, false);
            UiKit.Place(noticeBox.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(460, 32));
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
            UiKit.Bind(screen, "_bar", bar.rectTransform);
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
            BuildArmament(content, theme);
            return root;
        }

        /// <summary>거점 핫바 content에 공용 theme을 사용한 열차 정비 패널과 거치대 버튼을 만든다.</summary>
        private static void BuildArmament(Transform content, UiTheme theme)
        {
            TrainArmamentScreen controller = content.gameObject.AddComponent<TrainArmamentScreen>();
            Button open = UiKit.Button(content, "ArmamentOpen", "", theme);
            UiKit.Place((RectTransform)open.transform, Vector2.one, Vector2.one, new Vector2(-20, -354), new Vector2(58, 58)); // 기존 -150 → 도감 자리를 비워 -354(숨김 중)
            RectTransform hover = UiKit.Node(content, "TrainHover");
            TMP_Text hoverText = UiKit.Label(hover, "Prompt", "열차 정비?", UiTextSize.Normal, theme);
            UiKit.Place(hoverText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, 20), new Vector2(180, 36));
            hover.gameObject.SetActive(false);
            Image panel = UiKit.Panel(content, "ArmamentPanel", theme);
            UiKit.Stretch(panel.rectTransform);
            Sprite backgroundSprite = ArmamentSprite("armament_workshop_candidate", -1);
            Sprite[] turretSprites = new Sprite[7];
            for (int i = 0; i < turretSprites.Length; i++) turretSprites[i] = ArmamentSprite("armament_turrets", i);
            Image openIcon = UiKit.Image(open.transform, "Icon", turretSprites[0], Color.white, false);
            UiKit.Place(openIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(45, 45));
            openIcon.preserveAspect = true;
            Image background = UiKit.Image(panel.transform, "Workshop", backgroundSprite, Color.white, false);
            UiKit.Stretch(background.rectTransform);
            TMP_Text title = UiKit.Label(panel.transform, "Title", "열차 정비", UiTextSize.Title, theme);
            UiKit.Place(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -18), new Vector2(380, 40));
            Button close = UiKit.Button(panel.transform, "Close", "닫기", theme);
            UiKit.Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-20, -18), new Vector2(90, 44));
            Button[] slots = new Button[TrainArmament.SLOT_COUNT];
            TMP_Text[] slotTexts = new TMP_Text[slots.Length];
            Image[] slotIcons = new Image[slots.Length];
            Image[] ghostIcons = new Image[slots.Length];
            string mountPath = BuildMainCanvas.PARTS_FOLDER + "/ArmamentMount.prefab";
            GameObject mountPrefab = UiKit.SavePrefab(mountPath, () => ArmamentMount(theme, turretSprites[0]), UiKit.ConsumeMarker("ArmamentMount"));
            for (int i = 0; i < slots.Length; i++)
            {
                GameObject mount = (GameObject)PrefabUtility.InstantiatePrefab(mountPrefab, panel.transform);
                mount.name = "Mount" + (i + 1);
                slots[i] = mount.GetComponent<Button>();
                UiKit.Place((RectTransform)mount.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-450 + i * 200, 68), new Vector2(175, 208));
                slotTexts[i] = mount.transform.Find("Label").GetComponent<TMP_Text>();
                slotIcons[i] = mount.transform.Find("Turret").GetComponent<Image>();
                ghostIcons[i] = mount.transform.Find("Ghost").GetComponent<Image>();
            }
            Button[] cards = new Button[7];
            TMP_Text[] cardTexts = new TMP_Text[cards.Length];
            Image[] cardArt = new Image[cards.Length];
            GameObject[] cardLocks = new GameObject[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = UiKit.Button(panel.transform, "TurretCard" + (i + 1), "", theme);
                cards[i].gameObject.AddComponent<TrainTurretCard>();
                UiKit.Place((RectTransform)cards[i].transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-525 + i * 175, 15), new Vector2(166, 195));
                cardTexts[i] = cards[i].GetComponentInChildren<TMP_Text>(true);
                UiKit.Place(cardTexts[i].rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 6), new Vector2(154, 80));
                cardTexts[i].fontSize = 15;
                Image art = UiKit.Image(cards[i].transform, "TurretArt", turretSprites[i], Color.white, false);
                UiKit.Place(art.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(150, 100));
                art.preserveAspect = true;
                cardArt[i] = art;
                RectTransform locked = UiKit.Node(cards[i].transform, "Locked");
                UiKit.Stretch(locked);
                Image shackle = UiKit.Image(locked, "Shackle", null, new Color(.72f, .67f, .60f), false);
                UiKit.Place(shackle.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -37), new Vector2(18, 18));
                Image shackleInner = UiKit.Image(locked, "ShackleInner", null, new Color(.22f, .23f, .24f), false);
                UiKit.Place(shackleInner.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -37), new Vector2(12, 12));
                Image lockBody = UiKit.Image(locked, "LockBody", null, new Color(.72f, .67f, .60f), false);
                UiKit.Place(lockBody.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -49), new Vector2(22, 17));
                Image keyhole = UiKit.Image(locked, "Keyhole", null, new Color(.22f, .23f, .24f), false);
                UiKit.Place(keyhole.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -49), new Vector2(3, 7));
                TMP_Text lockedTitle = UiKit.Label(locked, "LockedTitle", "잠김", UiTextSize.Normal, theme);
                UiKit.Place(lockedTitle.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -77), new Vector2(140, 30));
                Image divider = UiKit.Image(locked, "Divider", null, new Color(.65f, .59f, .49f, .8f), false);
                UiKit.Place(divider.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -99), new Vector2(132, 1));
                TMP_Text lockedHint = UiKit.Label(locked, "LockedHint", "서식지 조합으로 해금", UiTextSize.Small, theme);
                UiKit.Place(lockedHint.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -120), new Vector2(150, 36));
                lockedHint.fontSize = 14;
                cardLocks[i] = locked.gameObject;
                locked.gameObject.SetActive(false);
            }
            TMP_Text notice = UiKit.Label(panel.transform, "Notice", "", UiTextSize.Small, theme);
            UiKit.Place(notice.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 185), new Vector2(900, 30));
            Image overlay = UiKit.Panel(panel.transform, "MountOverlay", theme);
            UiKit.Place(overlay.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 90), new Vector2(430, 150));
            TMP_Text selection = UiKit.Label(overlay.transform, "Selection", "", UiTextSize.Normal, theme);
            UiKit.Place(selection.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -5), new Vector2(410, 78));
            Button apply = UiKit.Button(overlay.transform, "Apply", "적용", theme);
            UiKit.Place((RectTransform)apply.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-130, 12), new Vector2(120, 45));
            Button remove = UiKit.Button(overlay.transform, "Remove", "철거", theme);
            UiKit.Place((RectTransform)remove.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(120, 45));
            Button overlayClose = UiKit.Button(overlay.transform, "Close", "닫기", theme);
            UiKit.Place((RectTransform)overlayClose.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(130, 12), new Vector2(120, 45));
            Image cardDetails = UiKit.Panel(panel.transform, "TurretDetails", theme, false);
            UiKit.Place(cardDetails.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 220), new Vector2(320, 135));
            TMP_Text cardDetailsText = UiKit.Label(cardDetails.transform, "Text", "", UiTextSize.Normal, theme);
            UiKit.Stretch(cardDetailsText.rectTransform, 12, 8, 12, 8);
            cardDetailsText.enableAutoSizing = true;
            cardDetailsText.fontSizeMin = 14;
            cardDetailsText.fontSizeMax = 20;
            cardDetails.gameObject.SetActive(false);
            UiKit.Bind(controller, "_open", open);
            UiKit.Bind(controller, "_trainHover", hover);
            UiKit.Bind(controller, "_outlineShader", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Generated/Battle/TrainHoverOutline.shader"));
            UiKit.Bind(controller, "_lockedShader", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Generated/Battle/LockedTurretUI.shader"));
            UiKit.Bind(controller, "_panel", panel.gameObject);
            UiKit.Bind(controller, "_close", close);
            UiKit.Bind(controller, "_overlay", overlay.gameObject);
            UiKit.Bind(controller, "_apply", apply);
            UiKit.Bind(controller, "_remove", remove);
            UiKit.Bind(controller, "_overlayClose", overlayClose);
            UiKit.Bind(controller, "_selection", selection);
            UiKit.Bind(controller, "_notice", notice);
            UiKit.Bind(controller, "_cardDetails", cardDetails.gameObject);
            UiKit.Bind(controller, "_cardDetailsText", cardDetailsText);
            BindArray(controller, "_slots", slots);
            BindArray(controller, "_slotTexts", slotTexts);
            BindArray(controller, "_slotIcons", slotIcons);
            BindArray(controller, "_ghostIcons", ghostIcons);
            BindArray(controller, "_turretSprites", turretSprites);
            BindArray(controller, "_cards", cards);
            BindArray(controller, "_cardTexts", cardTexts);
            BindArray(controller, "_cardArt", cardArt);
            BindArray(controller, "_cardLocks", cardLocks);
            overlay.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        /// <summary>theme과 기본 포탑 그림으로 넓은 입력 영역과 금속 거치대 표시를 갖춘 프리팹 루트를 반환한다.</summary>
        private static GameObject ArmamentMount(UiTheme theme, Sprite turretSprite)
        {
            Button button = UiKit.Button(null, "ArmamentMount", "빈 자리", theme);
            RectTransform root = (RectTransform)button.transform;
            root.sizeDelta = new Vector2(175, 208);
            ColorBlock invisible = button.colors;
            invisible.normalColor = Color.clear;
            invisible.highlightedColor = Color.clear;
            invisible.pressedColor = Color.clear;
            invisible.selectedColor = Color.clear;
            invisible.disabledColor = Color.clear;
            button.colors = invisible;
            button.image.color = Color.clear;
            button.gameObject.AddComponent<TrainMountDrag>();

            Image baseShadow = UiKit.Image(button.transform, "MountShadow", null, new Color(.09f, .11f, .12f, .9f), false);
            UiKit.Place(baseShadow.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 3), new Vector2(122, 15));
            baseShadow.transform.SetAsFirstSibling();
            Image baseRim = UiKit.Image(button.transform, "MountRim", null, new Color(.55f, .48f, .35f, 1f), false);
            UiKit.Place(baseRim.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 7), new Vector2(108, 9));
            Image baseTop = UiKit.Image(button.transform, "MountTop", null, new Color(.24f, .28f, .29f, 1f), false);
            UiKit.Place(baseTop.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(96, 5));
            Image plateBorder = UiKit.Image(button.transform, "LabelBorder", null, new Color(.53f, .48f, .39f, .92f), false);
            UiKit.Place(plateBorder.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -50), new Vector2(154, 32));
            Image plate = UiKit.Image(button.transform, "LabelPlate", null, new Color(.12f, .15f, .16f, .96f), false);
            UiKit.Place(plate.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -50), new Vector2(150, 28));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            UiKit.Place(label.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -50), new Vector2(146, 27));
            label.fontSize = 16;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 16;
            label.transform.SetAsLastSibling();
            Image icon = UiKit.Image(button.transform, "Turret", turretSprite, Color.white, false);
            UiKit.Place(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 57), new Vector2(125, 95));
            icon.preserveAspect = true;
            Outline outline = icon.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.96f, .86f, .56f, 1f);
            outline.effectDistance = new Vector2(3, 3);
            outline.enabled = false;
            Image ghost = UiKit.Image(button.transform, "Ghost", turretSprite, new Color(1f, 1f, 1f, .48f), false);
            UiKit.Place(ghost.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 57), new Vector2(125, 95));
            ghost.preserveAspect = true;
            ghost.gameObject.SetActive(false);
            return button.gameObject;
        }

        /// <summary>정비 이미지와 index를 사용해 배경 또는 포탑 스프라이트 애셋을 생성하거나 반환한다.</summary>
        private static Sprite ArmamentSprite(string name, int index)
        {
            string root = "Assets/Art/Generated/Battle/";
            string path = root + name + (index < 0 ? "" : "_" + index) + ".asset";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            AssetDatabase.ImportAsset(root + name + ".png", ImportAssetOptions.ForceSynchronousImport);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(root + name + ".png");
            Rect rect = index < 0
                ? new Rect(0, 0, texture.width, texture.height)
                : new Rect(index * (texture.width / 7), texture.height * .19f, texture.width / 7, texture.height * .62f);
            sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f));
            sprite.name = name + "_" + index;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        /// <summary>슬롯·팔레트 항목 부품: 슬롯 틀(클릭 받음) + 아이콘 + 이름(자동 축소) + 키 번호(withKey).</summary>
        private static GameObject Cell(UiTheme theme, string name, Vector2 size, bool withKey)
        {
            Image frame = UiKit.Slot(null, name, theme);
            frame.rectTransform.sizeDelta = size;
            HotbarCellView view = frame.gameObject.AddComponent<HotbarCellView>();
            Image icon = UiKit.Image(frame.transform, "Icon", null, Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -9), new Vector2(40, 40));
            icon.enabled = false;
            TextMeshProUGUI label = UiKit.Label(frame.transform, "Name", "", withKey ? UiTextSize.Title : UiTextSize.Small, theme);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = new Vector2(1, 0); labelRect.pivot = new Vector2(0.5f, 0);
            labelRect.offsetMin = new Vector2(3, withKey ? 32 : 26);
            labelRect.offsetMax = new Vector2(-3, withKey ? 74 : 47);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = !withKey;
            if (withKey) label.fontSize = Mathf.Clamp(theme.Size(UiTextSize.Title), 28f, 34f);
            else
            {
                label.fontSizeMax = theme.Size(UiTextSize.Small);
                label.fontSizeMin = theme.Size(UiTextSize.Small) * 0.6f;
            }
            IconCatalog priceIcons = AssetDatabase.LoadAssetAtPath<IconCatalog>("Assets/Prefabs/UI/IconCatalog.asset");
            Image priceIcon = UiKit.Image(frame.transform, "PriceCoin", priceIcons != null ? priceIcons.Get("ui_coin") : null, Color.white, false);
            UiKit.Place(priceIcon.rectTransform, Vector2.one, Vector2.one,
                new Vector2(withKey ? -51 : -43, -6), new Vector2(withKey ? 18 : 14, withKey ? 18 : 14));
            priceIcon.gameObject.SetActive(false);
            TextMeshProUGUI price = UiKit.Label(frame.transform, "Price", "", UiTextSize.Small, theme, TextAlignmentOptions.TopRight);
            UiKit.Place(price.rectTransform, Vector2.one, Vector2.one,
                new Vector2(-5, -3), new Vector2(withKey ? 40 : 32, withKey ? 23 : 19));
            price.fontSize = withKey ? 20f : 16f;
            price.textWrappingMode = TextWrappingModes.NoWrap;
            TextMeshProUGUI stockBadge = UiKit.Label(frame.transform, "StockBadge", "", UiTextSize.Small, theme);
            UiKit.Place(stockBadge.rectTransform, Vector2.one, Vector2.one,
                new Vector2(-5, -3), new Vector2(withKey ? 40 : 32, withKey ? 23 : 19));
            stockBadge.alignment = TextAlignmentOptions.TopRight;
            stockBadge.color = theme.Accent;
            stockBadge.gameObject.SetActive(false);
            UiKit.Bind(view, "_price", price);
            UiKit.Bind(view, "_priceIcon", priceIcon);
            UiKit.Bind(view, "_stockBadge", stockBadge);
            UiKit.Bind(view, "_frame", frame);
            UiKit.Bind(view, "_icon", icon);
            UiKit.Bind(view, "_name", label);
            if (withKey)
            {
                TextMeshProUGUI key = UiKit.Label(frame.transform, "Key", "1", UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
                UiKit.Place(key.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(5, -3), new Vector2(22, 20));
                key.color = theme.Accent;
                UiKit.Bind(view, "_key", key);
                HotbarData starter = HotbarData.Load();
                PlacementEntry first = starter.Slots.Length > 0 && starter.Slots[0] != null ? starter.Slots[0].ToEntry() : default;
                Preview(view, first, ItemSpriteSet.Load(), "1");
            }
            return frame.gameObject;
        }

        /// <summary>편집 모드 미리보기: Hotbar.json 배정의 아이콘(ItemSprites)과 한국어 이름.</summary>
        private static void Preview(HotbarCellView view, PlacementEntry entry, ItemSpriteSet sprites, string key)
        {
            if (view == null) return;
            if (key != null) view.SetKey(key);
            if (!entry.IsValid) { view.Show(null, ""); view.SetPrice(null, true); return; }
            string name = Lookup("item_" + entry.Id) ?? entry.Id;
            Sprite sprite = entry.Kind == PlacementKind.Item && sprites != null ? sprites.Get(entry.Id) : null;
            view.Show(sprite, name);
            TextAsset catalogJson = UnityEngine.Resources.Load<TextAsset>("Items/ItemCatalog");
            var item = entry.Kind == PlacementKind.Item && catalogJson != null ? IronRing.Items.ItemCatalog.Parse(catalogJson.text).Get(entry.Id) : null;
            view.SetPrice(item != null ? item.Price : (int?)null, true);
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
