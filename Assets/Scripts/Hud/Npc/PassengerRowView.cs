using UnityEngine;

using TMPro;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 승객 화면의 한 줄 부품(Parts/PassengerRow.prefab): 인물과 요청 건물의 관계.
    /// 표시만 하고 상태는 갖지 않는다.
    /// </summary>
    public sealed class PassengerRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [Tooltip("완성할 건물과 합류할 승객을 짝지은 안내.")]
        [SerializeField] private TMP_Text _wants;

        /// <summary>name과 wants를 승객 줄에 표시하고 비어 있는 값의 라벨은 끈다.</summary>
        public void Show(string name, string wants)
        {
            Set(_wants, wants);
            Set(_name, name);
        }

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
