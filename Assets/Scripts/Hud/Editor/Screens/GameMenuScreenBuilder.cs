using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Save;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>
    /// 스펙 003. 항상 켜진 화면 "GameMenu" 프리팹 내용을 만든다. BuildMainCanvas.SCREENS가 부른다.
    /// 자식 순서(뒤 → 앞): Dim(전체 그늘) → MenuPanel(버튼 4개·저장 불가 이유) → SlotPanel(슬롯 3칸·닫기) → ConfirmPanel(문구·예·아니오).
    /// </summary>
    public static class GameMenuScreenBuilder
    {
        private const float DIM_ALPHA = 0.6f;
        private const float PAD = 20f;
        private const float GAP = 10f;
        private const float TITLE_HEIGHT = 40f;
        private const float BUTTON_HEIGHT = 48f;
        private const float REASON_HEIGHT = 24f;
        private const float SLOT_HEIGHT = 64f;
        private const float MENU_WIDTH = 360f;
        private const float SLOT_WIDTH = 460f;
        private const float CONFIRM_WIDTH = 440f;
        private const float CONFIRM_HEIGHT = 210f;
        private const float CONFIRM_BUTTON_WIDTH = 150f;

        /// <summary>
        /// theme 색·글자로 화면 루트(UiScreen "GameMenu", 항상 켜짐)와 Dim, 메뉴·슬롯·확인 패널을 만들고 GameMenuScreen 참조를 꽂는다.
        /// 문구는 ko.json에 키가 있으면 그 값, 없으면 키 이름을 편집 모드 미리보기로 넣는다(Play 때 화면 스크립트가 다시 쓴다). 루트를 돌려준다.
        /// </summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("GameMenu", true);
            GameMenuScreen screen = root.AddComponent<GameMenuScreen>();

            Image dim = UiKit.Image(root.transform, "Dim", null, new Color(0f, 0f, 0f, DIM_ALPHA), true);
            UiKit.Stretch(dim.rectTransform);

            // 메뉴 패널: 제목, 게임 저장, 저장 불가 이유, 백그라운드 실행, 타이틀 화면으로, 게임 종료(FR-003 순서).
            float menuHeight = PAD + TITLE_HEIGHT + GAP + BUTTON_HEIGHT + REASON_HEIGHT + GAP + 3f * BUTTON_HEIGHT + 2f * GAP + PAD;
            RectTransform menu = PanelBox(root.transform, "MenuPanel", new Vector2(MENU_WIDTH, menuHeight), theme);
            float y = PAD;
            TextMeshProUGUI menuTitle = Title(menu, "Title", Text("game_menu_title"), y, theme);
            y += TITLE_HEIGHT + GAP;
            Button save = Row(menu, "Save", Text("game_menu_save"), y, MENU_WIDTH, theme);
            y += BUTTON_HEIGHT;
            TextMeshProUGUI reason = UiKit.Label(menu, "SaveBlocked", Text("game_menu_save_blocked"), UiTextSize.Small, theme);
            UiKit.Place(reason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(MENU_WIDTH - PAD * 2f, REASON_HEIGHT));
            reason.color = theme.Accent;
            y += REASON_HEIGHT + GAP;
            Button background = Row(menu, "Background", Text("game_menu_background_on"), y, MENU_WIDTH, theme);
            y += BUTTON_HEIGHT + GAP;
            Button toTitle = Row(menu, "ToTitle", Text("game_menu_to_title"), y, MENU_WIDTH, theme);
            y += BUTTON_HEIGHT + GAP;
            Button quit = Row(menu, "Quit", Text("game_menu_quit"), y, MENU_WIDTH, theme);

            // 슬롯 패널: 제목, 슬롯 칸 SLOT_COUNT개(번호 줄 + 요약 줄), 닫기.
            int slotCount = SaveSlots.SLOT_COUNT;
            float slotHeight = PAD + TITLE_HEIGHT + GAP + slotCount * SLOT_HEIGHT + (slotCount - 1) * GAP + GAP * 2f + BUTTON_HEIGHT + PAD;
            RectTransform slotPanel = PanelBox(root.transform, "SlotPanel", new Vector2(SLOT_WIDTH, slotHeight), theme);
            y = PAD;
            TextMeshProUGUI slotTitle = Title(slotPanel, "Title", Text("game_menu_save"), y, theme);
            y += TITLE_HEIGHT + GAP;
            var slots = new List<Button>();
            for (int i = 0; i < slotCount; i++)
            {
                Button slot = UiKit.Button(slotPanel, "Slot" + (i + 1), Text("save_slot_label") + "\n" + Text("save_slot_empty"), theme);
                UiKit.Place((RectTransform)slot.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(SLOT_WIDTH - PAD * 2f, SLOT_HEIGHT));
                TMP_Text label = slot.GetComponentInChildren<TMP_Text>(true);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontSize = theme.Size(UiTextSize.Small);
                UiKit.Stretch(label.rectTransform, 14f, 4f, 14f, 4f);
                slots.Add(slot);
                y += SLOT_HEIGHT + GAP;
            }
            y += GAP;
            Button close = UiKit.Button(slotPanel, "Close", Text("game_menu_close"), theme);
            UiKit.Place((RectTransform)close.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(CONFIRM_BUTTON_WIDTH, BUTTON_HEIGHT));

            // 확인 패널: 문구, 예·아니오.
            RectTransform confirm = PanelBox(root.transform, "ConfirmPanel", new Vector2(CONFIRM_WIDTH, CONFIRM_HEIGHT), theme);
            TextMeshProUGUI message = UiKit.Label(confirm, "Message", Text("game_menu_quit_confirm"), UiTextSize.Normal, theme);
            UiKit.Stretch(message.rectTransform, PAD, PAD, PAD, PAD + BUTTON_HEIGHT + GAP);
            Button yes = UiKit.Button(confirm, "Yes", Text("game_menu_yes"), theme);
            UiKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-GAP, PAD), new Vector2(CONFIRM_BUTTON_WIDTH, BUTTON_HEIGHT));
            Button no = UiKit.Button(confirm, "No", Text("game_menu_no"), theme);
            UiKit.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(GAP, PAD), new Vector2(CONFIRM_BUTTON_WIDTH, BUTTON_HEIGHT));

            // 편집 모드 미리보기는 메뉴 패널만 보인다(한 번에 패널 하나).
            slotPanel.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);

            UiKit.Bind(screen, "_dim", dim.gameObject);
            UiKit.Bind(screen, "_menuPanel", menu.gameObject);
            UiKit.Bind(screen, "_slotPanel", slotPanel.gameObject);
            UiKit.Bind(screen, "_confirmPanel", confirm.gameObject);
            UiKit.Bind(screen, "_menuTitle", menuTitle);
            UiKit.Bind(screen, "_saveButton", save);
            UiKit.Bind(screen, "_saveBlocked", reason);
            UiKit.Bind(screen, "_backgroundButton", background);
            UiKit.Bind(screen, "_toTitleButton", toTitle);
            UiKit.Bind(screen, "_quitButton", quit);
            UiKit.Bind(screen, "_slotTitle", slotTitle);
            ScreenArrayBinding.BindArray(screen, "_slotButtons", slots);
            UiKit.Bind(screen, "_slotClose", close);
            UiKit.Bind(screen, "_confirmMessage", message);
            UiKit.Bind(screen, "_yesButton", yes);
            UiKit.Bind(screen, "_noButton", no);
            return root;
        }

        /// <summary>
        /// parent 가운데에 size 크기의 패널 상자(name)를 만든다. 반투명 테마 패널 뒤에 불투명 바탕을 깐다.
        /// 자식을 담을 상자 RectTransform을 돌려준다.
        /// </summary>
        private static RectTransform PanelBox(Transform parent, string name, Vector2 size, UiTheme theme)
        {
            RectTransform box = UiKit.Node(parent, name);
            UiKit.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Color backingColor = theme.Panel;
            backingColor.a = 1f;
            Image backing = UiKit.Image(box, "Backing", null, backingColor, true);
            backing.preserveAspect = false;
            UiKit.Stretch(backing.rectTransform, 1f, 1f, 1f, 1f);
            UiKit.Panel(box, "Frame", theme);
            return box;
        }

        /// <summary>panel 위에서 top만큼 내려온 자리에 강조색 제목(name, text)을 만들어 돌려준다.</summary>
        private static TextMeshProUGUI Title(RectTransform panel, string name, string text, float top, UiTheme theme)
        {
            TextMeshProUGUI title = UiKit.Label(panel, name, text, UiTextSize.Heading, theme);
            UiKit.StretchTop(title.rectTransform, TITLE_HEIGHT, top, PAD, PAD);
            title.color = theme.Accent;
            title.fontStyle = FontStyles.Bold;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            return title;
        }

        /// <summary>panel 위에서 top만큼 내려온 자리에 panelWidth에 맞춘 가로 버튼(name, text)을 만들어 돌려준다.</summary>
        private static Button Row(RectTransform panel, string name, string text, float top, float panelWidth, UiTheme theme)
        {
            Button button = UiKit.Button(panel, name, text, theme);
            UiKit.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(panelWidth - PAD * 2f, BUTTON_HEIGHT));
            return button;
        }

        /// <summary>key의 한국어 문구를 돌려준다. ko.json에 아직 없으면 키 이름을 돌려준다(편집 모드 미리보기용).</summary>
        private static string Text(string key)
        {
            try { return Strings.Get(key); }
            catch (KeyNotFoundException) { return key; }
        }
    }
}
