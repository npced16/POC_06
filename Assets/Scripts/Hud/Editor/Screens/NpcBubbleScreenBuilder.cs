using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Npc;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>항상 켜진 화면 "NpcBubble"과 말풍선 부품을 만든다. BuildMainCanvas.SCREENS가 부른다.</summary>
    public static class NpcBubbleScreenBuilder
    {
        /// <summary>말풍선 부품 프리팹 경로.</summary>
        public const string BUBBLE_PART = BuildMainCanvas.PARTS_FOLDER + "/NpcBubble.prefab";
        private const int PAD_X = 10;
        private const int PAD_Y = 5;
        private const float TAIL_SIZE = 10f;

        /// <summary>theme을 사용해 화면 루트(UiScreen, NpcBubbleScreen)를 만들고 말풍선 부품을 연결해 반환한다.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject part = UiKit.SavePrefab(BUBBLE_PART, () => Bubble(theme), UiKit.ConsumeMarker("NpcBubble"));
            GameObject root = UiKit.ScreenRoot("NpcBubble", true);
            NpcBubbleScreen screen = root.AddComponent<NpcBubbleScreen>();
            UiKit.Bind(screen, "_bubblePrefab", part.GetComponent<NpcBubbleView>());
            return root;
        }

        /// <summary>theme 색의 말풍선 부품을 만들어 반환한다. 모든 그래픽은 클릭을 통과시키고 글자 길이에 맞춰 크기가 변한다.</summary>
        private static GameObject Bubble(UiTheme theme)
        {
            Image body = UiKit.Image(null, "NpcBubble", null, theme.Panel);
            body.preserveAspect = false;
            Outline edge = body.gameObject.AddComponent<Outline>();
            edge.effectColor = theme.Border;
            edge.effectDistance = new Vector2(1f, -1f);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Vector2(160, 30));

            HorizontalLayoutGroup layout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(PAD_X, PAD_X, PAD_Y, PAD_Y);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image tail = UiKit.Image(body.transform, "Tail", null, theme.Panel);
            tail.preserveAspect = false;
            tail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiKit.Place(tail.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(TAIL_SIZE, TAIL_SIZE));
            tail.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

            TextMeshProUGUI text = UiKit.Label(body.transform, "Text", Strings.Get("ui_sample_npc_line"), UiTextSize.Small, theme);
            text.textWrappingMode = TextWrappingModes.NoWrap;

            NpcBubbleView view = body.gameObject.AddComponent<NpcBubbleView>();
            UiKit.Bind(view, "_rect", body.rectTransform);
            UiKit.Bind(view, "_text", text);
            UiKit.Bind(view, "_tail", tail.rectTransform);
            body.gameObject.SetActive(false);
            return body.gameObject;
        }
    }
}
