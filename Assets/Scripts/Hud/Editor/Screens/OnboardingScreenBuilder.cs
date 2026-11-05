using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Onboarding;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스펙 001 FR-006, FR-007. 항상 켜진 화면 "Onboarding"(화면 아래 왼쪽 마스코트 팝업) 프리팹 내용을 만든다. BuildMainCanvas.SCREENS가 부른다.</summary>
    public static class OnboardingScreenBuilder
    {
        private const string MASCOT_FOLDER = "Assets/Art/Generated/mascot/";
        private const float MASCOT_SIZE = 168f;
        private static readonly Vector2 BOX_POSITION = new(24f, 150f);
        private static readonly Vector2 BOX_SIZE = new(640f, 200f);

        /// <summary>
        /// theme 색·틀로 화면 루트(UiScreen "Onboarding", 항상 켜짐), 팝업 상자, 마스코트 이미지, 문구, 확인 버튼을 만든다.
        /// 마스코트 그림 3종을 OnboardingScreen에 연결하고 루트 GameObject를 돌려준다.
        /// </summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("Onboarding", true);
            OnboardingScreen screen = root.AddComponent<OnboardingScreen>();

            Image box = UiKit.Sliced(root.transform, "Box", theme.PanelSprite, theme, true);
            UiKit.Place(box.rectTransform, Vector2.zero, Vector2.zero, BOX_POSITION, BOX_SIZE);

            Sprite mascotDefault = AssetDatabase.LoadAssetAtPath<Sprite>(MASCOT_FOLDER + "mascot_default.png");
            Image mascot = UiKit.Image(box.transform, "Mascot", mascotDefault, Color.white);
            mascot.preserveAspect = true;
            UiKit.Place(mascot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(MASCOT_SIZE, MASCOT_SIZE));

            TextMeshProUGUI text = UiKit.Label(box.transform, "Text", Strings.Get("onboarding_select_material"), UiTextSize.Normal, theme);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            UiKit.Stretch(text.rectTransform, MASCOT_SIZE + 28f, 18f, 20f, 58f);

            Button confirm = UiKit.Button(box.transform, "Confirm", Strings.Get("onboarding_ok"), theme);
            UiKit.Place((RectTransform)confirm.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 12f), new Vector2(120f, 40f));

            UiKit.Bind(screen, "_box", box.gameObject);
            UiKit.Bind(screen, "_mascot", mascot);
            UiKit.Bind(screen, "_text", text);
            UiKit.Bind(screen, "_confirm", confirm);
            UiKit.Bind(screen, "_mascotDefault", mascotDefault);
            UiKit.Bind(screen, "_mascotHappy", AssetDatabase.LoadAssetAtPath<Sprite>(MASCOT_FOLDER + "mascot_happy.png"));
            UiKit.Bind(screen, "_mascotAlert", AssetDatabase.LoadAssetAtPath<Sprite>(MASCOT_FOLDER + "mascot_alert.png"));
            return root;
        }
    }
}
