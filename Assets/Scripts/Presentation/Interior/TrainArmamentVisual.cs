using UnityEngine;

using IronRing.Combat;

namespace IronRing.Presentation.Interior
{
    /// <summary>거점 열차의 지붕에 현재 장착한 포탑을 표시한다.</summary>
    public sealed class TrainArmamentVisual : MonoBehaviour
    {
        [Header("지붕 거치대")]
        [SerializeField] private SpriteRenderer[] _turrets;

        void OnEnable()
        {
            TrainArmament.Changed += Refresh;
            Refresh();
        }

        void OnDisable() { TrainArmament.Changed -= Refresh; }

        /// <summary>저장된 거치대별 포탑 유무를 읽어 각 지붕 표시를 켜거나 끈다.</summary>
        private void Refresh()
        {
            for (int i = 0; i < _turrets.Length; i++) _turrets[i].enabled = !string.IsNullOrEmpty(TrainArmament.TurretId(i));
        }
    }
}
