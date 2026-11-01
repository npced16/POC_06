using UnityEngine;
using UnityEngine.UIElements;

using IronRing.Localization;

namespace IronRing.Art.Demo
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ArtPreview : MonoBehaviour
    {
        [Header("Preview")]
        [SerializeField] private Texture2D _contactSheet;
        [SerializeField] private Vector2 _sheetSize = new Vector2(720, 480);
        [SerializeField] private Color _background = new Color(0.098f, 0.118f, 0.149f);
        [SerializeField] private Color _textColor = new Color(0.96f, 0.90f, 0.73f);
        [SerializeField] private int _titleSize = 30;
        [SerializeField] private int _labelSize = 22;

        /// <summary>초기 애셋 생성 시 sheet를 저장하여 런타임 시트 표시 입력으로 사용한다.</summary>
        public void Configure(Texture2D sheet) { _contactSheet = sheet; }

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            KoreanUi.Apply(root);
            root.style.backgroundColor = _background;
            root.style.color = _textColor;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.flexGrow = 1;
            AddLabel(root, "demo_title", _titleSize);
            AddLabel(root, "demo_subtitle", _labelSize);
            var image = new Image { image = _contactSheet, scaleMode = ScaleMode.StretchToFill };
            image.style.width = _sheetSize.x;
            image.style.height = _sheetSize.y;
            root.Add(image);
            AddLabel(root, "demo_font_sample", _labelSize);
            AddLabel(root, "demo_caption", 18);
            AddLabel(root, "demo_footer", 16);
        }

        /// <summary>key의 한국어를 size 크기로 표시하는 Label을 root에 추가한다.</summary>
        private static void AddLabel(VisualElement root, string key, int size)
        {
            var label = new Label(Strings.Get(key));
            label.style.fontSize = size;
            label.style.marginTop = 4;
            label.style.marginBottom = 4;
            root.Add(label);
        }
    }
}
