using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Combat
{
    /// <summary>거점에서 포탑 종류와 거치대를 고르고 장비를 설치하거나 강화한다.</summary>
    public sealed class TrainArmamentScreen : MonoBehaviour
    {
        [Header("정비 화면")]
        [SerializeField] private Button _open;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _close;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private Button _apply;
        [SerializeField] private Button[] _slots;
        [SerializeField] private TMP_Text[] _slotTexts;
        [SerializeField] private Image[] _slotIcons;
        [SerializeField] private TMP_Text _selection;
        [SerializeField] private TMP_Text _notice;

        private BattleRules _rules;
        private BattleLoadout _loadout;
        private IReadOnlyList<TurretRule> _available;
        private int _selectedTurret;
        private int _selectedSlot;

        void Awake()
        {
            _open.onClick.AddListener(Open);
            _close.onClick.AddListener(Close);
            _previous.onClick.AddListener(Previous);
            _next.onClick.AddListener(Next);
            _apply.onClick.AddListener(Apply);
            for (int i = 0; i < _slots.Length; i++)
            {
                int slot = i;
                _slots[i].onClick.AddListener(() => SelectSlot(slot));
            }
            _panel.SetActive(false);
        }

        /// <summary>현재 거점 건물과 승객의 포탑 해금 상태를 읽어 정비 화면을 연다.</summary>
        private void Open()
        {
            _rules = BattleRules.Load();
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            _loadout = BattleIntegration.CreateLoadout(_rules, GameServices.Get<IHabitatClusters>(), GameServices.Get<INpcRecruiter>(), hubs != null ? hubs.HubIds : null);
            _available = _loadout.AvailableTurrets();
            _selectedTurret = 0;
            _selectedSlot = 0;
            _notice.text = "포탑을 고른 후 열차 지붕의 거치대를 눌러 설치하세요.";
            _panel.SetActive(true);
            Refresh();
        }

        /// <summary>정비 화면을 닫고 거점 화면으로 돌아간다.</summary>
        private void Close() { _panel.SetActive(false); }

        /// <summary>선택 후보를 이전 포탑으로 바꾸고 화면을 갱신한다.</summary>
        private void Previous() { _selectedTurret = (_selectedTurret + _available.Count - 1) % _available.Count; Refresh(); }

        /// <summary>선택 후보를 다음 포탑으로 바꾸고 화면을 갱신한다.</summary>
        private void Next() { _selectedTurret = (_selectedTurret + 1) % _available.Count; Refresh(); }

        /// <summary>slot 번호를 정비 대상으로 선택하고 거치대 표시를 갱신한다.</summary>
        private void SelectSlot(int slot) { _selectedSlot = slot; Apply(); }

        /// <summary>선택한 거치대와 포탑에 비용을 지불해 설치 또는 강화를 적용한다.</summary>
        private void Apply()
        {
            bool success = TrainArmament.Apply(_selectedSlot, _available[_selectedTurret].Id, GameServices.Get<IMoney>(), _rules.Economy);
            _notice.text = success ? "열차 정비를 완료했습니다." : "마르크가 부족합니다.";
            Refresh();
        }

        /// <summary>거치대 상태와 선택 포탑의 피해·사거리·가격을 화면에 반영한다.</summary>
        private void Refresh()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                string id = TrainArmament.TurretId(i);
                TurretRule placed = _rules.Turret(id);
                _slotTexts[i].text = $"{(_selectedSlot == i ? "▶ " : "")}{i + 1}번\n{(placed == null ? "빈 칸" : placed.Name + " Lv." + TrainArmament.Level(i))}";
                if (_slotIcons != null && i < _slotIcons.Length) _slotIcons[i].enabled = placed != null;
            }
            TurretRule turret = _available[_selectedTurret];
            _selection.text = $"{turret.Name}\n피해 {turret.BaseDamage:0.#}  사거리 {turret.Range:0.#}  간격 {turret.AttackInterval:0.0}초\n설치·교체 {_rules.Economy.TurretInstallMarks} / 같은 포탑 강화 {_rules.Economy.TurretUpgradeMarks} 마르크";
        }
    }
}
