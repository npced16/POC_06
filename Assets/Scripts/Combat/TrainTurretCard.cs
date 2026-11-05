using UnityEngine;
using UnityEngine.EventSystems;

namespace IronRing.Combat
{
    /// <summary>포탑 카드의 포인터 진입과 이탈을 정비 화면에 전달한다.</summary>
    public sealed class TrainTurretCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TrainArmamentScreen _screen;
        private int _index;

        /// <summary>screen과 index를 저장해 해당 카드의 상세 정보를 연결한다.</summary>
        public void Initialize(TrainArmamentScreen screen, int index) { _screen = screen; _index = index; }

        /// <summary>포인터 진입 시 eventData와 연결된 카드의 상세 정보를 표시한다.</summary>
        public void OnPointerEnter(PointerEventData eventData) { _screen.ShowCardDetails(_index); }

        /// <summary>포인터 이탈 시 eventData와 연결된 카드의 상세 정보를 숨긴다.</summary>
        public void OnPointerExit(PointerEventData eventData) { _screen.HideCardDetails(); }
    }
}
