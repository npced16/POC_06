using UnityEngine;

using TMPro;

namespace IronRing.Hud.Ui
{
    /// <summary>스토리 022. 테마 글자 크기 단계.</summary>
    public enum UiTextSize { Small, Normal, Title }

    /// <summary>
    /// 스토리 022. UGUI 화면 공용 테마 데이터(Assets/Prefabs/UI/UiTheme.asset). 색은 BuildMainCanvas가 SteampunkPalette.json에서 처음 한 번 채우고,
    /// 이후에는 디자이너가 이 에셋에서 고친다. 스크립트는 색·크기·스프라이트를 코드에 두지 않고 여기서 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("색 (팔레트에서 생성)")]
        [SerializeField] private Color _panel = Color.white;
        [SerializeField] private Color _border = Color.white;
        [SerializeField] private Color _accent = Color.white;
        [SerializeField] private Color _button = Color.white;
        [SerializeField] private Color _text = Color.white;
        [SerializeField] private Color _muted = Color.gray;

        [Header("글꼴")]
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private float _fontSmall = 16f;
        [SerializeField] private float _fontNormal = 20f;
        [SerializeField] private float _fontTitle = 28f;

        [Header("9분할 스프라이트 (Assets/Art/Generated/ui)")]
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _buttonSprite;
        [SerializeField] private Sprite _buttonPressedSprite;
        [SerializeField] private Sprite _slotSprite;
        [SerializeField] private Sprite _slotSelectedSprite;
        [SerializeField] private Sprite _toastSprite;
        [Tooltip("Image.pixelsPerUnitMultiplier. 캔버스 referencePixelsPerUnit 64 기준 0.5면 스프라이트 한 픽셀이 화면 기준 2픽셀로 그려진다.")]
        [SerializeField] private float _sliceMultiplier = 0.5f;

        /// <summary>패널 바탕색(자두색).</summary>
        public Color Panel => _panel;
        /// <summary>테두리색(황동).</summary>
        public Color Border => _border;
        /// <summary>강조색(호박색 불빛).</summary>
        public Color Accent => _accent;
        /// <summary>버튼색(구리).</summary>
        public Color Button => _button;
        /// <summary>기본 글자색(연기색).</summary>
        public Color Text => _text;
        /// <summary>흐린 글자색(힌트·보조 문구).</summary>
        public Color Muted => _muted;
        /// <summary>한국어 TMP 폰트 에셋(NotoSansKR SDF).</summary>
        public TMP_FontAsset Font => _font;
        /// <summary>9분할 패널 스프라이트.</summary>
        public Sprite PanelSprite => _panelSprite;
        /// <summary>버튼 스프라이트.</summary>
        public Sprite ButtonSprite => _buttonSprite;
        /// <summary>눌린 버튼 스프라이트(Button SpriteSwap).</summary>
        public Sprite ButtonPressedSprite => _buttonPressedSprite;
        /// <summary>슬롯 틀 스프라이트.</summary>
        public Sprite SlotSprite => _slotSprite;
        /// <summary>선택된 슬롯 틀 스프라이트(호박색 테두리).</summary>
        public Sprite SlotSelectedSprite => _slotSelectedSprite;
        /// <summary>토스트 바탕 스프라이트.</summary>
        public Sprite ToastSprite => _toastSprite;
        /// <summary>9분할 Image의 pixelsPerUnitMultiplier.</summary>
        public float SliceMultiplier => _sliceMultiplier;

        /// <summary>단계별 글자 크기.</summary>
        public float Size(UiTextSize size) => size == UiTextSize.Small ? _fontSmall : size == UiTextSize.Title ? _fontTitle : _fontNormal;
    }
}
