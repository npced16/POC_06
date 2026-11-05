using UnityEngine;
using UnityEngine.EventSystems;

namespace IronRing.Combat
{
    /// <summary>거치대 간 포탑 드래그 이동을 전달한다.</summary>
    public sealed class TrainMountDrag : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private TrainArmamentScreen _screen;
        private int _index;

        /// <summary>screen과 index를 저장해 이 거치대의 이동 출발점을 설정한다.</summary>
        public void Initialize(TrainArmamentScreen screen, int index) { _screen = screen; _index = index; }

        /// <summary>eventData의 드래그 시작을 받아 버튼 클릭이 실행되지 않도록 한다.</summary>
        public void OnBeginDrag(PointerEventData eventData) { eventData.eligibleForClick = false; }

        /// <summary>eventData의 포인터 진입 시 선택한 포탑의 고스트와 거치대 정보를 표시한다.</summary>
        public void OnPointerEnter(PointerEventData eventData) { _screen.ShowMountPreview(_index); }

        /// <summary>eventData의 포인터 이탈 시 거치대 미리보기와 강조를 숨긴다.</summary>
        public void OnPointerExit(PointerEventData eventData) { _screen.HideMountPreview(); }

        /// <summary>eventData가 오른쪽 클릭이면 해당 거치대의 철거 메뉴를 연다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) _screen.OpenSlotOptions(_index);
        }

        /// <summary>eventData의 놓인 위치에서 거치대를 찾아 포탑을 이동한다.</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerCurrentRaycast.gameObject == null) return;
            TrainMountDrag target = eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<TrainMountDrag>();
            if (target != null) _screen.Move(_index, target._index);
        }
    }
}
