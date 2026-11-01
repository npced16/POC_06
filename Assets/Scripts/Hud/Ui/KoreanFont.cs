using TMPro;

namespace IronRing.Hud.Ui
{
    /// <summary>스토리 022. 씬 MainCanvas(UiScreens)의 테마와 한국어 TMP 폰트에 대한 정적 접근. 캔버스가 없으면 null.</summary>
    public static class KoreanFont
    {
        /// <summary>현재 씬의 UI 테마(없으면 null).</summary>
        public static UiTheme Theme => UiScreens.Instance != null ? UiScreens.Instance.Theme : null;

        /// <summary>한국어 TMP 폰트 에셋(없으면 null).</summary>
        public static TMP_FontAsset Asset => Theme != null ? Theme.Font : null;

        /// <summary>text에 테마 폰트·크기·글자색을 입힌다. 테마가 없으면 아무것도 하지 않는다.</summary>
        public static void Apply(TMP_Text text, UiTextSize size)
        {
            UiTheme theme = Theme;
            if (text == null || theme == null) return;
            if (theme.Font != null) text.font = theme.Font;
            text.fontSize = theme.Size(size);
            text.color = theme.Text;
        }
    }
}
