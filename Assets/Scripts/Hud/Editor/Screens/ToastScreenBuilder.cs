using UnityEngine;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022. 항상 켜진 화면 "Toast"(위 가운데, 단계 띠·정착 기한 줄 아래 한 줄 토스트) 프리팹 내용을 만든다. BuildMainCanvas.SCREENS가 부른다.</summary>
    public static class ToastScreenBuilder
    {
        /// <summary>화면 루트(UiScreen "Toast", 항상 켜짐) + ToastScreen + 토스트 상자(Box)와 예시 한국어 문구.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("Toast", true);
            ToastScreen screen = root.AddComponent<ToastScreen>();
            UnityEngine.UI.Image box = UiKit.Sliced(root.transform, "Box", theme.ToastSprite, theme, false);
            UiKit.Place(box.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(560, 44));
            string sample = string.Format(Strings.Get("placement_habitat_complete"), Strings.Get("hub_1"));
            TextMeshProUGUI text = UiKit.Label(box.transform, "Text", sample, UiTextSize.Normal, theme);
            UiKit.Stretch(text.rectTransform, 12, 4, 12, 4);
            UiKit.Bind(screen, "_box", box.gameObject);
            UiKit.Bind(screen, "_text", text);
            return root;
        }
    }
}
