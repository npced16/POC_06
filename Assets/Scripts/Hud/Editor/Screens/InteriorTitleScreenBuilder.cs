using UnityEngine;

using UnityEditor;

using TMPro;

using IronRing.Hud.Interior;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022. 상황 화면 "InteriorTitle"(거점 내부 오른쪽 위 제목 상자) 프리팹 내용을 만든다. BuildMainCanvas.SCREENS가 부른다.</summary>
    public static class InteriorTitleScreenBuilder
    {
        /// <summary>노드 id → 문자열 키 표(3단계에서 Assets/UI를 지울 때 .meta와 함께 옮긴다).</summary>
        public const string CATALOG = "Assets/UI/Hud/Resources/Hud/HudCatalog.json";

        /// <summary>화면 루트(UiScreen "InteriorTitle", 상황 화면) + InteriorTitleScreen + 제목 패널과 예시 한국어 라벨.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("InteriorTitle", false);
            InteriorTitleScreen screen = root.AddComponent<InteriorTitleScreen>();
            UnityEngine.UI.Image box = UiKit.Panel(root.transform, "TitleBox", theme, false);
            UiKit.Place(box.rectTransform, Vector2.one, Vector2.one, new Vector2(-16, -104), new Vector2(320, 60));
            TextMeshProUGUI title = UiKit.Label(box.transform, "Title", Strings.Get("hub_1"), UiTextSize.Title, theme);
            UiKit.Stretch(title.rectTransform, 14, 6, 14, 6);
            UiKit.Bind(screen, "_title", title);
            UiKit.Bind(screen, "_catalogJson", AssetDatabase.LoadAssetAtPath<TextAsset>(CATALOG));
            return root;
        }
    }
}
