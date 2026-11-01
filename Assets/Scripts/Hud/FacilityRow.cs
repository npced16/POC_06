using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace IronRing.Hud
{
    /// <summary>스토리 022. 거점 패널의 시설 한 줄(이름·개수 라벨 + 짓기 버튼) 부품. HubPanelScreen이 참조로 채운다.</summary>
    public sealed class FacilityRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Button _build;

        /// <summary>시설 이름과 개수를 쓰는 라벨.</summary>
        public TMP_Text Label => _label;

        /// <summary>짓기 버튼.</summary>
        public Button Build => _build;
    }
}
