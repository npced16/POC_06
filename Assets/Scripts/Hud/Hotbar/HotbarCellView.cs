using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Presentation.Interior;

namespace IronRing.Hud.Hotbar
{
    /// <summary>
    /// 스토리 022. 핫바 슬롯(HotbarSlot.prefab)과 팔레트 항목(PaletteItem.prefab) 부품. 아이콘·이름·키 번호·선택 틀만 그리고,
    /// 짧은 클릭은 click, 임계값(Hotbar.json) 이상 끌면 HotbarScreen에 드래그 시작·끝을 알린다. 게임 상태는 갖지 않는다.
    /// </summary>
    public sealed class HotbarCellView : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("가격 표시")]
        [SerializeField] private TMP_Text _price;
        [SerializeField] private Image _priceIcon;
        [SerializeField] private TMP_Text _stockBadge;
        [SerializeField] private Color _unaffordableTint = new Color(1f, 0.45f, 0.4f, 0.55f);
        [SerializeField] private Color _unaffordableText = new Color(1f, 0.5f, 0.45f, 1f);
        [Header("칸 참조")]
        [SerializeField] private Image _frame;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [Tooltip("키 번호(1~9). 팔레트 항목에는 없다.")]
        [SerializeField] private TMP_Text _key;

        private HotbarScreen _owner;
        private Func<PlacementEntry> _entry;
        private Action _click;
        private Action _rightClick;
        private Vector2 _pressPosition;
        private bool _dragStarted;
        private bool _suppressClick;

        /// <summary>이 칸의 항목과 좌클릭·우클릭 동작을 연결한다. 우클릭 동작이 없으면 무시한다.</summary>
        public void Bind(HotbarScreen owner, Func<PlacementEntry> entry, Action click, Action rightClick = null)
        {
            _owner = owner;
            _entry = entry;
            _click = click;
            _rightClick = rightClick;
        }

        /// <summary>아이콘(없으면 숨김)과 이름을 쓴다.</summary>
        public void Show(Sprite icon, string label)
        {
            if (_icon != null) { _icon.sprite = icon; _icon.enabled = icon != null; }
            if (_name != null) _name.text = label ?? string.Empty;
        }

        /// <summary>스토리 038: 살 수 없으면 아이콘·이름을 붉고 흐리게.</summary>
        public void SetAffordable(bool affordable)
        {
            Color tint = affordable ? Color.white : _unaffordableTint;
            if (_icon != null) _icon.color = tint;
            if (_name != null) _name.color = affordable ? Color.white : _unaffordableText;
        }

        /// <summary>price가 있으면 동전 아이콘과 숫자를 표시하고 affordable로 부족 상태를 변경한다.</summary>
        public void SetPrice(int? price, bool affordable)
        {
            _price.text = price.HasValue ? price.Value.ToString() : string.Empty;
            if (_priceIcon != null) _priceIcon.gameObject.SetActive(price.HasValue);
            _price.color = affordable ? Color.white : _unaffordableText;
            if (_priceIcon != null) _priceIcon.color = affordable ? Color.white : _unaffordableText;
            SetAffordable(affordable);
            if (!affordable) _frame.color = _unaffordableTint;
        }

        /// <summary>count가 양수이면 오른쪽 위 가격 대신 ×N 재고를 표시하고, 없으면 재고 배지를 숨긴다.</summary>
        public void SetStock(int count)
        {
            if (_stockBadge != null)
            {
                _stockBadge.gameObject.SetActive(count > 0);
                if (count > 0) _stockBadge.text = string.Format(Strings.Get("stock_badge"), count);
            }
            if (count <= 0) return;
            _price.text = string.Empty;
            if (_priceIcon != null) _priceIcon.gameObject.SetActive(false);
            _price.color = Color.white;
            SetAffordable(true);
        }

        /// <summary>키 번호 문구(편집기 미리보기·생성기용).</summary>
        public void SetKey(string key) { if (_key != null) _key.text = key; }

        /// <summary>선택이면 호박색 선택 틀, highlight면 테마 강조색으로 틀을 물들인다.</summary>
        public void SetState(bool selected, bool highlight, UiTheme theme)
        {
            if (_frame == null || theme == null) return;
            _frame.sprite = selected ? theme.SlotSelectedSprite : theme.SlotSprite;
            _frame.color = highlight ? theme.Accent : Color.white;
        }

        /// <summary>왼쪽 누름 위치를 기억한다(드래그 판정 기준).</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            _suppressClick = false;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pressPosition = eventData.position;
            _dragStarted = false;
        }

        /// <summary>임계값은 Hotbar.json으로 직접 재므로 EventSystem 기본 임계값을 끈다.</summary>
        public void OnInitializePotentialDrag(PointerEventData eventData) { eventData.useDragThreshold = false; }

        /// <summary>드래그 시작은 OnDrag에서 임계값을 넘을 때 한다.</summary>
        public void OnBeginDrag(PointerEventData eventData) { }

        /// <summary>왼쪽 버튼으로 임계값 이상 끌면 고스트 드래그를 시작한다.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (_dragStarted || _owner == null || _entry == null || eventData.button != PointerEventData.InputButton.Left) return;
            if ((eventData.position - _pressPosition).magnitude < _owner.DragThreshold) return;
            PlacementEntry entry = _entry();
            if (!entry.IsValid) return;
            _dragStarted = true;
            _suppressClick = true;
            _owner.BeginPointerDrag(entry);
        }

        /// <summary>드래그를 놓으면 HotbarScreen이 PlacementGhost.EndDrag(true)로 놓는다(UI 위면 고스트가 거른다).</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragStarted) return;
            _dragStarted = false;
            if (_owner != null) _owner.EndPointerDrag();
        }

        /// <summary>끌지 않은 좌클릭 또는 우클릭을 해당 슬롯 동작으로 전달한다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_suppressClick) return;
            if (eventData.button == PointerEventData.InputButton.Left) _click?.Invoke();
            else if (eventData.button == PointerEventData.InputButton.Right) _rightClick?.Invoke();
        }

        void OnDisable() { _dragStarted = false; }
    }
}
