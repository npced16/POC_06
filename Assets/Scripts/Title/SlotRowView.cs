using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace IronRing.Title
{
    /// <summary>스펙 002 FR-009·FR-010. 슬롯 선택 패널의 칸 하나(번호·요약 글자, 테두리, 버튼).</summary>
    public sealed class SlotRowView : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _frame;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _summary;

        [Header("표시")]
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _mutedColor = Color.gray;
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _selectedSprite;

        /// <summary>칸을 누르는 버튼(패널이 클릭을 연결한다).</summary>
        public Button Button => _button;

        /// <summary>
        /// 칸에 label 번호와 summary 글자를 쓴다. selectable이 false면 글자를 흐린 색으로 바꾸고 버튼을 비활성으로 둔다(FR-009).
        /// 선택 테두리는 기본으로 되돌린다.
        /// </summary>
        public void Show(string label, string summary, bool selectable)
        {
            _label.text = label;
            _summary.text = summary;
            _label.color = selectable ? _textColor : _mutedColor;
            _summary.color = selectable ? _textColor : _mutedColor;
            _button.interactable = selectable;
            SetSelected(false);
        }

        /// <summary>selected면 테두리를 선택 스프라이트로, 아니면 기본 스프라이트로 바꾼다(FR-010).</summary>
        public void SetSelected(bool selected)
        {
            _frame.sprite = selected ? _selectedSprite : _normalSprite;
        }
    }
}
