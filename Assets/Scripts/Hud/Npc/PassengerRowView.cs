using UnityEngine;
using UnityEngine.EventSystems;

using TMPro;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 승객 화면의 한 줄 부품(Parts/PassengerRow.prefab): 인물과 요청 건물의 관계.
    /// 표시와 끌기 시작 입력만 맡고 게임 상태는 갖지 않는다.
    /// </summary>
    public sealed class PassengerRowView : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private TMP_Text _name;
        [Tooltip("완성할 건물과 합류할 승객을 짝지은 안내.")]
        [SerializeField] private TMP_Text _wants;

        private string _npcId;

        /// <summary>npcId를 보관하고 name과 wants를 승객 줄에 표시한다. 비어 있는 값의 라벨은 끈다.</summary>
        public void Show(string npcId, string name, string wants)
        {
            _npcId = npcId;
            Set(_wants, wants);
            Set(_name, name);
        }

        /// <summary>스펙 005. 이 줄의 승객 끌기를 승객 화면에 요청한다.</summary>
        public void OnBeginDrag(PointerEventData eventData) => PassengersScreen.Instance.BeginRowDrag(_npcId);

        /// <summary>끌기 이벤트를 이 줄이 받기 위한 빈 처리. 위치는 NpcDragController가 따라간다.</summary>
        public void OnDrag(PointerEventData eventData) { }

        /// <summary>label에 text를 표시하고 text가 비어 있으면 라벨을 끈다.</summary>
        private static void Set(TMP_Text label, string text)
        {
            if (label == null) return;
            label.text = text ?? string.Empty;
            bool on = !string.IsNullOrEmpty(text);
            if (label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
        }
    }
}
