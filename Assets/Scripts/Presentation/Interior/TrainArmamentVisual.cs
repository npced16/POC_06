using UnityEngine;

using IronRing.Combat;

namespace IronRing.Presentation.Interior
{
    /// <summary>거점 열차의 지붕에 현재 장착한 포탑을 표시한다.</summary>
    public sealed class TrainArmamentVisual : MonoBehaviour
    {
        private static Sprite[] _sprites;

        [Header("지붕 거치대")]
        [SerializeField] private SpriteRenderer[] _turrets;

        void OnEnable()
        {
            TrainArmament.Changed += Refresh;
            Refresh();
        }

        void OnDisable() { TrainArmament.Changed -= Refresh; }

        /// <summary>sprites를 거점 포탑 표시에 연결하고 활성 열차의 장착 외형을 갱신한다.</summary>
        public static void ConfigureSprites(Sprite[] sprites)
        {
            _sprites = sprites;
            foreach (TrainArmamentVisual visual in FindObjectsByType<TrainArmamentVisual>(FindObjectsSortMode.None))
                visual.Refresh();
        }

        /// <summary>저장된 거치대별 포탑 유무를 읽어 각 지붕 표시를 켜거나 끈다.</summary>
        private void Refresh()
        {
            BattleRules rules = BattleRules.Load();
            for (int i = 0; i < _turrets.Length; i++)
            {
                string id = TrainArmament.TurretId(i);
                _turrets[i].enabled = !string.IsNullOrEmpty(id);
                if (_sprites == null) continue;
                for (int j = 0; j < rules.Turrets.Count && j < _sprites.Length; j++)
                    if (rules.Turrets[j].Id == id) { _turrets[i].sprite = _sprites[j]; break; }
            }
        }
    }
}
