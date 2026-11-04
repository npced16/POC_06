using UnityEngine;

using TMPro;

namespace IronRing.Hud.Ui
{
    /// <summary>스토리 022. 테마 글자 크기 단계.</summary>
    public enum UiTextSize { Small, Normal, Title, Heading }

    /// <summary>스토리 026. UI 스킨: Simple(평면 패널·텍스트 닫기, 기본) | Sheet(스토리 024 시트 조각·제목판·둥근 닫기).</summary>
    public enum UiSkin { Simple, Sheet }

    /// <summary>
    /// 스토리 022. UGUI 화면 공용 테마 데이터(Assets/Prefabs/UI/UiTheme.asset). 색은 BuildMainCanvas가 SteampunkPalette.json에서 처음 한 번 채우고,
    /// 이후에는 디자이너가 이 에셋에서 고친다. 스크립트는 색·크기·스프라이트를 코드에 두지 않고 여기서 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("스킨 (스토리 026) — 바꾼 뒤 BuildMainCanvas.Generate(전체 표식)로 재생성")]
        [SerializeField] private UiSkin _skin = UiSkin.Simple;

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
        [Tooltip("스토리 024: 패널 소제목·배너")]
        [SerializeField] private float _fontHeading = 22f;
        [Tooltip("스토리 024: 라벨 줄 높이 = 글자 크기 × 이 값 이상(잘림 방지). Noto Sans KR 줄 높이가 약 ×1.45라 1.5")]
        [SerializeField] private float _lineHeightFactor = 1.5f;
        [Tooltip("스토리 024: 모든 글자 버튼의 높이")]
        [SerializeField] private float _buttonHeight = 40f;
        [Tooltip("스토리 024: 좁은 줄(시설 줄 짓기 등)의 작은 버튼 높이")]
        [SerializeField] private float _buttonHeightSmall = 32f;

        [Header("9분할 스프라이트 (Assets/Art/Generated/ui)")]
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _buttonSprite;
        [SerializeField] private Sprite _buttonPressedSprite;
        [SerializeField] private Sprite _slotSprite;
        [SerializeField] private Sprite _slotSelectedSprite;
        [SerializeField] private Sprite _toastSprite;
        [Tooltip("Image.pixelsPerUnitMultiplier. 캔버스 referencePixelsPerUnit 64 기준 0.5면 스프라이트 한 픽셀이 화면 기준 2픽셀로 그려진다.")]
        [SerializeField] private float _sliceMultiplier = 0.5f;

        [Header("스토리 024 시트 조각 (Assets/Art/UI/SteampunkUiSheet_keyed.png)")]
        [SerializeField] private Sprite _frameTitledSprite;
        [SerializeField] private Sprite _progressBarSprite;
        [SerializeField] private Sprite _roundCloseSprite;
        [SerializeField] private Sprite _buttonDarkSprite;
        [SerializeField] private Sprite _slotCopperSprite;
        [Header("내용 여백 (시트 픽셀, x=왼 y=아래 z=오른 w=위). 화면 여백 = 값 / SliceMultiplier")]
        [SerializeField] private Vector4 _panelPad;
        [SerializeField] private Vector4 _frameTitledPad;
        [SerializeField] private Vector4 _toastPad;
        [SerializeField] private Vector4 _buttonPad;
        [SerializeField] private Vector4 _slotPad;
        [SerializeField] private Vector4 _progressBarPad;

        /// <summary>현재 스킨 모드. UiKit이 제목·닫기·슬롯 모양을 이 값으로 고른다.</summary>
        public UiSkin Skin => _skin;
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
        /// <summary>화면에 사용하는 한국어 TMP 폰트 에셋.</summary>
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
        /// <summary>제목판이 붙은 프레임(스토리 024, 가로로 늘리면 제목판도 늘어나 현재 화면에서는 쓰지 않음).</summary>
        public Sprite FrameTitledSprite => _frameTitledSprite;
        /// <summary>진행 막대 틀.</summary>
        public Sprite ProgressBarSprite => _progressBarSprite;
        /// <summary>둥근 X 닫기 버튼(Simple, 9분할 안 함).</summary>
        public Sprite RoundCloseSprite => _roundCloseSprite;
        /// <summary>보조 버튼(어두운 판).</summary>
        public Sprite ButtonDarkSprite => _buttonDarkSprite;
        /// <summary>강조 슬롯(구리).</summary>
        public Sprite SlotCopperSprite => _slotCopperSprite;

        /// <summary>스프라이트 안쪽의 내용 여백(화면 단위). 글자·아이콘이 파이프·테두리와 겹치지 않게 쓴다. 모르는 스프라이트는 0.</summary>
        public RectOffset ContentPadding(Sprite sprite)
        {
            Vector4 pad = Vector4.zero;
            if (sprite == null) return new RectOffset();
            if (sprite == _panelSprite) pad = _panelPad;
            else if (sprite == _frameTitledSprite) pad = _frameTitledPad;
            else if (sprite == _toastSprite) pad = _toastPad;
            else if (sprite == _buttonSprite || sprite == _buttonPressedSprite || sprite == _buttonDarkSprite) pad = _buttonPad;
            else if (sprite == _slotSprite || sprite == _slotSelectedSprite || sprite == _slotCopperSprite) pad = _slotPad;
            else if (sprite == _progressBarSprite) pad = _progressBarPad;
            float k = _sliceMultiplier > 0f ? 1f / _sliceMultiplier : 1f;
            return new RectOffset(Mathf.RoundToInt(pad.x * k), Mathf.RoundToInt(pad.z * k), Mathf.RoundToInt(pad.w * k), Mathf.RoundToInt(pad.y * k));
        }

        /// <summary>단계별 글자 크기.</summary>
        public float Size(UiTextSize size) => size switch
        {
            UiTextSize.Small => _fontSmall,
            UiTextSize.Title => _fontTitle,
            UiTextSize.Heading => _fontHeading,
            _ => _fontNormal,
        };

        /// <summary>단계 글자가 잘리지 않는 최소 줄 높이(글자 크기 × 줄 높이 계수, 올림).</summary>
        public float LineHeight(UiTextSize size) => Mathf.Ceil(Size(size) * _lineHeightFactor);

        /// <summary>글자 버튼 공통 높이.</summary>
        public float ButtonHeight => _buttonHeight;

        /// <summary>좁은 줄 안의 작은 글자 버튼 높이.</summary>
        public float ButtonHeightSmall => _buttonHeightSmall;
    }
}
