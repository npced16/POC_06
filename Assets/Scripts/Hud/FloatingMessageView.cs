using UnityEngine;

using TMPro;

namespace IronRing.Hud
{
    /// <summary>스토리 022. 전투·상태 변화 때 월드 위치에서 떠오르는 한 줄 문구 부품(FloatingMessage.prefab). CombatFxScreen이 재사용한다.</summary>
    public sealed class FloatingMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        /// <summary>문구 라벨.</summary>
        public TMP_Text Label => _label;

        /// <summary>화면 좌표 배치용 RectTransform.</summary>
        public RectTransform Rect => (RectTransform)transform;
    }
}
