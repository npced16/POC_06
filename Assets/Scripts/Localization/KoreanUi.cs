using UnityEngine;
using UnityEngine.UIElements;

namespace IronRing.Localization
{
    public static class KoreanUi
    {
        /// <summary>기본 한글 PanelSettings를 반환하여 새 UIDocument의 런타임 폰트 설정에 사용한다.</summary>
        public static PanelSettings DefaultPanel => Resources.Load<PanelSettings>("KoreanPanelSettings");

        /// <summary>root에 공유 폰트 스타일을 추가하여 자식 텍스트가 한국어 FontAsset을 상속하게 한다.</summary>
        public static void Apply(VisualElement root)
        {
            StyleSheet sheet = Resources.Load<StyleSheet>("KoreanDefault");
            if (!root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }
    }
}
