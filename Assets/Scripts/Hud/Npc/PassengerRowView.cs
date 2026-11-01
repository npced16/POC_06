using UnityEngine;

using TMPro;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 스토리 025. 승객 화면의 한 줄 부품(Parts/PassengerRow.prefab): 직업명·출신지·탄 지 몇 바퀴·제안·힌트.
    /// 표시만 하고 상태는 갖지 않는다.
    /// </summary>
    public sealed class PassengerRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _origin;
        [SerializeField] private TMP_Text _laps;
        [SerializeField] private TMP_Text _proposal;
        [SerializeField] private TMP_Text _hint;

        /// <summary>한 줄을 채운다. 비어 있는 값은 그 라벨을 끈다(편집 모드 미리보기에서도 쓴다).</summary>
        public void Show(string name, string origin, string laps, string proposal, string hint)
        {
            Set(_name, name);
            Set(_origin, origin);
            Set(_laps, laps);
            Set(_proposal, proposal);
            Set(_hint, hint);
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label == null) return;
            label.text = text ?? string.Empty;
            bool on = !string.IsNullOrEmpty(text);
            if (label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
        }
    }
}
