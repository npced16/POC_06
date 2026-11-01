using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using TMPro;

using IronRing.Presentation.Interior;
using IronRing.Hud.Ui;

namespace IronRing.Hud.Hotbar
{
    /// <summary>
    /// 스토리 022. 핫바 슬롯(HotbarSlot.prefab)과 팔레트 항목(PaletteItem.prefab) 부품. 아이콘·이름·키 번호·선택 틀만 그리고,
    /// 짧은 클릭은 click, 임계값(Hotbar.json) 이상 끌면 HotbarScreen에 드래그 시작·끝을 알린다. 게임 상태는 갖지 않는다.
    /// </summary>
    public sealed class HotbarCellView : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _frame;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [Tooltip("키 번호(1~9). 팔레트 항목에는 없다.")]
        [SerializeField] private TMP_Text _key;

        private HotbarScreen _owner;
        private Func<PlacementEntry> _entry;
        private Action _click;
        private Vector2 _pressPosition;
        private bool _dragStarted;

        /// <summary>이 칸이 나타내는 항목과 클릭 동작을 연결한다(런타임 초기화).</summary>
        public void Bind(HotbarScreen owner, Func<PlacementEntry> entry, Action click)
        {
            _owner = owner;
            _entry = entry;
            _click = click;
        }

        /// <summary>아이콘(없으면 숨김)과 이름을 쓴다.</summary>
        public void Show(Sprite icon, string label)
        {
            if (_icon != null) { _icon.sprite = icon; _icon.enabled = icon != null; }
            if (_name != null) _name.text = label ?? string.Empty;
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
            _owner.BeginPointerDrag(entry);
        }

        /// <summary>드래그를 놓으면 HotbarScreen이 PlacementGhost.EndDrag(true)로 놓는다(UI 위면 고스트가 거른다).</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragStarted) return;
            _dragStarted = false;
            if (_owner != null) _owner.EndPointerDrag();
        }

        /// <summary>끌지 않은 왼쪽 클릭만 클릭으로 친다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _dragStarted) return;
            _click?.Invoke();
        }

        void OnDisable() { _dragStarted = false; }
    }
}
