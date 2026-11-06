using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using TMPro;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 승객 화면의 한 줄 부품(Parts/PassengerRow.prefab): NPC 그림과 직업 이름.
    /// 표시와 끌기 시작 입력만 맡고 게임 상태는 갖지 않는다.
    /// </summary>
    public sealed class PassengerRowView : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private TMP_Text _name;
        [Tooltip("마을을 걷는 NPC와 같은 그림.")]
        [SerializeField] private Image _body;

        private string _npcId;

        /// <summary>npcId를 보관하고 name과 그림 body를 승객 줄에 표시한다.</summary>
        public void Show(string npcId, string name, Sprite body)
        {
            _npcId = npcId;
            _name.text = name;
            _body.sprite = body;
        }

        /// <summary>스펙 005. 이 줄의 승객 끌기를 승객 화면에 요청한다.</summary>
        public void OnBeginDrag(PointerEventData eventData) => PassengersScreen.Instance.BeginRowDrag(_npcId);

        /// <summary>끌기 이벤트를 이 줄이 받기 위한 빈 처리. 위치는 NpcDragController가 따라간다.</summary>
        public void OnDrag(PointerEventData eventData) { }
    }
}
