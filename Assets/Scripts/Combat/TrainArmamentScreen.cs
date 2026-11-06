using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Onboarding;
using IronRing.Presentation.Interior;

namespace IronRing.Combat
{
    /// <summary>거점의 포탑 목록과 거치대 정비를 연결한다.</summary>
    public sealed class TrainArmamentScreen : MonoBehaviour
    {
        [Header("정비 화면")]
        [SerializeField] private Button _open;
        [SerializeField] private RectTransform _trainHover;
        [SerializeField] private Shader _outlineShader;
        [SerializeField] private Shader _lockedShader;
        [SerializeField] private float _trainHoverPadding = .25f;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _close;
        [SerializeField] private Button[] _slots;
        [SerializeField] private TMP_Text[] _slotTexts;
        [SerializeField] private Image[] _slotIcons;
        [SerializeField] private Image[] _ghostIcons;
        [SerializeField] private Sprite[] _turretSprites;
        [SerializeField] private Button[] _cards;
        [SerializeField] private TMP_Text[] _cardTexts;
        [SerializeField] private Image[] _cardArt;
        [SerializeField] private GameObject[] _cardLocks;
        [SerializeField] private GameObject _overlay;
        [SerializeField] private TMP_Text _selection;
        [SerializeField] private TMP_Text _notice;
        [SerializeField] private TMP_Text _balance;
        [SerializeField] private GameObject _cardDetails;
        [SerializeField] private TMP_Text _cardDetailsText;
        [SerializeField] private Button _apply;
        [SerializeField] private Button _remove;
        [SerializeField] private Button _overlayClose;

        private BattleRules _rules;
        private IMoney _money;
        private IReadOnlyList<TurretRule> _available;
        private BattleLoadout _loadout;
        private int _selectedTurret;
        private int _selectedSlot;
        private int _hoverSlot = -1;
        private TrainArmamentVisual _trainVisual;
        private SpriteRenderer _outlinedRenderer;
        private Material _originalMaterial;
        private Material _outlineMaterial;
        private Material _lockedMaterial;

        void Awake()
        {
            TrainArmamentVisual.ConfigureSprites(_turretSprites);
            _lockedMaterial = new Material(_lockedShader);
            _open.onClick.AddListener(Open);
            _close.onClick.AddListener(Close);
            _apply.onClick.AddListener(Apply);
            _remove.onClick.AddListener(Remove);
            _overlayClose.onClick.AddListener(HideOverlay);
            if (DefenseVisibility.Hidden) _open.gameObject.SetActive(false); // 스펙 006: 디펜스 숨김 동안 정비 열기 버튼을 숨김
            for (int i = 0; i < _slots.Length; i++)
            {
                int index = i;
                _slots[i].onClick.AddListener(() => SelectSlot(index));
                _slots[i].GetComponent<TrainMountDrag>().Initialize(this, index);
            }
            for (int i = 0; i < _cards.Length; i++)
            {
                int index = i;
                _cards[i].onClick.AddListener(() => SelectTurret(index));
                _cards[i].GetComponent<TrainTurretCard>().Initialize(this, index);
            }
            _panel.SetActive(false);
        }

        void Update()
        {
            // 스펙 006: 디펜스 숨김 동안 열차 테두리와 클릭을 처리하지 않음
            if (DefenseVisibility.Hidden || _panel.activeSelf || OnboardingInstaller.Director?.Current != null || Mouse.current == null || Camera.main == null)
            {
                _trainHover.gameObject.SetActive(false);
                SetTrainOutline(null);
                return;
            }
            if (_trainVisual == null || !_trainVisual.isActiveAndEnabled)
                _trainVisual = FindFirstObjectByType<TrainArmamentVisual>();
            if (_trainVisual == null)
            {
                _trainHover.gameObject.SetActive(false);
                SetTrainOutline(null);
                return;
            }
            Bounds bounds = _trainVisual.GetComponent<SpriteRenderer>().bounds;
            bounds.Expand(_trainHoverPadding * 2f);
            Vector2 min = Camera.main.WorldToScreenPoint(bounds.min);
            Vector2 max = Camera.main.WorldToScreenPoint(bounds.max);
            Vector2 pointer = Mouse.current.position.ReadValue();
            bool over = pointer.x >= min.x && pointer.x <= max.x && pointer.y >= min.y && pointer.y <= max.y &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            _trainHover.gameObject.SetActive(over);
            SetTrainOutline(over ? _trainVisual.GetComponent<SpriteRenderer>() : null);
            if (!over) return;
            RectTransform parent = (RectTransform)_trainHover.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, min, null, out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, max, null, out Vector2 localMax);
            _trainHover.anchoredPosition = (localMin + localMax) * .5f;
            _trainHover.sizeDelta = localMax - localMin;
            // 열차 옆 NPC를 누른 입력은 NPC 클릭·끌기로 처리한다.
            if (Mouse.current.leftButton.wasPressedThisFrame && !IronRing.Presentation.HubPhase.InteriorNpcWalker.IsHoveringNpc) Open();
        }

        void OnDisable()
        {
            SetTrainOutline(null);
            UnbindMoney();
            _panel.SetActive(false);
        }

        void OnDestroy()
        {
            SetTrainOutline(null);
            if (_outlineMaterial != null) Destroy(_outlineMaterial);
            if (_lockedMaterial != null) Destroy(_lockedMaterial);
        }

        /// <summary>renderer가 있으면 포인터용 실루엣 머티리얼을 적용하고 없으면 기존 머티리얼을 복원한다.</summary>
        private void SetTrainOutline(SpriteRenderer renderer)
        {
            if (_outlinedRenderer == renderer) return;
            if (_outlinedRenderer != null) _outlinedRenderer.sharedMaterial = _originalMaterial;
            _outlinedRenderer = renderer;
            if (renderer == null) return;
            _originalMaterial = renderer.sharedMaterial;
            if (_outlineMaterial == null) _outlineMaterial = new Material(_outlineShader);
            renderer.sharedMaterial = _outlineMaterial;
        }

        /// <summary>거점 해금 상태를 읽고 정비 패널을 연다.</summary>
        public void Open()
        {
            if (DefenseVisibility.Hidden) return; // 스펙 006: 디펜스 숨김 동안 정비 창을 열지 않음
            _rules = BattleRules.Load();
            UnbindMoney();
            _money = GameServices.Get<IMoney>();
            _money.Changed += RefreshBalance;
            RefreshBalance();
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            _loadout = BattleIntegration.CreateLoadout(_rules, GameServices.Get<IHabitatClusters>(), GameServices.Get<INpcRecruiter>(), hubs != null ? hubs.HubIds : null);
            _available = _loadout.AvailableTurrets();
            _selectedTurret = 0;
            _selectedSlot = -1;
            _notice.text = "포탑을 선택하고 거치대를 클릭하세요 · 우클릭으로 철거";
            _panel.SetActive(true);
            _overlay.SetActive(false);
            _cardDetails.SetActive(false);
            Refresh();
        }

        /// <summary>정비 패널을 닫는다.</summary>
        private void Close() { _panel.SetActive(false); UnbindMoney(); }

        /// <summary>현재 마르크 잔고를 정비 화면에 표시한다.</summary>
        private void RefreshBalance() { _balance.text = $"{_money.Balance:N0} 마르크"; }

        /// <summary>기존 마르크 서비스의 잔고 변경 구독을 해제한다.</summary>
        private void UnbindMoney()
        {
            if (_money == null) return;
            _money.Changed -= RefreshBalance;
            _money = null;
        }

        /// <summary>거치대 오버레이를 닫는다.</summary>
        private void HideOverlay() { _overlay.SetActive(false); }

        /// <summary>index의 해금된 포탑을 선택하고 카드와 오버레이를 갱신한다.</summary>
        private void SelectTurret(int index)
        {
            if (index >= _rules.Turrets.Count || !IsAvailable(_rules.Turrets[index])) return;
            _selectedTurret = index;
            Refresh();
        }

        /// <summary>slot에 선택한 포탑을 설치하거나 같은 종류를 강화한다.</summary>
        private void SelectSlot(int slot)
        {
            _selectedSlot = slot;
            _overlay.SetActive(false);
            Apply();
            ShowMountPreview(slot);
        }

        /// <summary>slot의 정비 오버레이를 열어 철거 작업을 제공한다.</summary>
        public void OpenSlotOptions(int slot)
        {
            _selectedSlot = slot;
            HideMountPreview();
            RectTransform overlay = (RectTransform)_overlay.transform;
            PositionAboveTurret(overlay, slot);
            _overlay.SetActive(true);
            Refresh();
        }

        /// <summary>선택한 포탑을 거치대에 설치하거나 강화하고 결과를 표시한다.</summary>
        private void Apply()
        {
            TurretRule turret = _rules.Turrets[_selectedTurret];
            bool success = TrainArmament.Apply(_selectedSlot, turret.Id, GameServices.Get<IMoney>(), _rules.Economy);
            _notice.text = success ? "정비를 완료했습니다." : "마르크가 부족합니다.";
            Refresh();
        }

        /// <summary>선택 거치대의 포탑을 환불 없이 철거하고 화면을 갱신한다.</summary>
        private void Remove()
        {
            TrainArmament.Remove(_selectedSlot);
            _notice.text = "포탑을 철거했습니다.";
            Refresh();
        }

        /// <summary>from과 to 거치대의 포탑을 교환하고 화면을 갱신한다.</summary>
        public void Move(int from, int to)
        {
            TrainArmament.Move(from, to);
            _overlay.SetActive(false);
            _notice.text = "포탑 위치를 변경했습니다.";
            Refresh();
        }

        /// <summary>turret이 현재 해금 목록에 있으면 true를 반환한다.</summary>
        private bool IsAvailable(TurretRule turret)
        {
            foreach (TurretRule available in _available) if (available.Id == turret.Id) return true;
            return false;
        }

        /// <summary>index 카드의 해금 상태와 실제 마을 보너스가 적용된 기본 능력치를 표시한다.</summary>
        public void ShowCardDetails(int index)
        {
            if (index >= _rules.Turrets.Count) return;
            TurretRule turret = _rules.Turrets[index];
            bool available = IsAvailable(turret);
            if (available) { _cardDetails.SetActive(false); return; }
            float damage = turret.BaseDamage + _loadout.Bonus(turret.Id, "damage");
            float range = turret.Range + _loadout.Bonus(turret.Id, "range");
            float interval = Mathf.Max(.1f, turret.AttackInterval - _loadout.Bonus(turret.Id, "interval"));
            _cardDetailsText.text = available
                ? $"{turret.Name}\n피해 {damage:0.#}   사거리 {range:0.#}\n공격 간격 {interval:0.##}초"
                : $"{turret.Name}\n잠김\n서식지 조합을 완성해 해금";
            RectTransform detail = (RectTransform)_cardDetails.transform;
            detail.anchoredPosition = new Vector2(Mathf.Clamp(((RectTransform)_cards[index].transform).anchoredPosition.x, -490, 490), 220);
            _cardDetails.SetActive(true);
        }

        /// <summary>포탑 카드의 상세 정보 패널을 숨긴다.</summary>
        public void HideCardDetails() { _cardDetails.SetActive(false); }

        /// <summary>slot 위에 선택 포탑 고스트와 기존 포탑의 강화·교체 상태를 표시한다.</summary>
        public void ShowMountPreview(int slot)
        {
            if (!_panel.activeSelf || _overlay.activeSelf) return;
            HideMountPreview();
            _hoverSlot = slot;
            TurretRule selected = _rules.Turrets[_selectedTurret];
            TurretRule current = _rules.Turret(TrainArmament.TurretId(slot));
            bool same = current != null && current.Id == selected.Id;
            _ghostIcons[slot].sprite = _turretSprites[_selectedTurret];
            _ghostIcons[slot].gameObject.SetActive(current == null);
            _slotIcons[slot].color = Color.white;
            Outline outline = _slotIcons[slot].GetComponent<Outline>();
            outline.enabled = current != null;
            outline.effectColor = same ? new Color(.25f, .65f, 1f, 1f) : new Color(1f, .2f, .2f, 1f);
            int cost = same ? _rules.Economy.TurretUpgradeMarks : _rules.Economy.TurretInstallMarks;
            string action = same ? "같은 포탑 · 강화" : current == null ? "설치" : "교체 · 기존 포탑 철거 / 환불 없음";
            string currentText = current == null ? "빈 자리" : $"{current.Name} Lv.{TrainArmament.Level(slot)}";
            string stats = current == null ? "" : $"\n피해 {current.BaseDamage + (TrainArmament.Level(slot) - 1) * current.DamagePerLevel + _loadout.Bonus(current.Id, "damage"):0.#}  사거리 {current.Range + _loadout.Bonus(current.Id, "range"):0.#}  간격 {Mathf.Max(.1f, current.AttackInterval - _loadout.Bonus(current.Id, "interval")):0.##}초";
            _cardDetailsText.text = $"{currentText}{stats}\n{selected.Name} {action}\n비용 {cost} 마르크";
            RectTransform detail = (RectTransform)_cardDetails.transform;
            PositionAboveTurret(detail, slot);
            _cardDetails.SetActive(true);
        }

        /// <summary>slot 포탑의 화면상 위쪽 경계에 popup을 맞추고 패널 가장자리에서 잘리지 않게 배치한다.</summary>
        private void PositionAboveTurret(RectTransform popup, int slot)
        {
            RectTransform parent = (RectTransform)popup.parent;
            RectTransform icon = _slotIcons[slot].rectTransform;
            Vector3 top = parent.InverseTransformPoint(icon.TransformPoint(new Vector3(0f, icon.rect.yMax, 0f)));
            float left = parent.rect.xMin + popup.rect.width * popup.pivot.x;
            float right = parent.rect.xMax - popup.rect.width * (1f - popup.pivot.x);
            popup.localPosition = new Vector3(Mathf.Clamp(top.x, left, right), top.y + 8f + popup.rect.height * popup.pivot.y, 0f);
        }

        /// <summary>현재 거치대의 고스트·강조와 상세 정보를 숨긴다.</summary>
        public void HideMountPreview()
        {
            if (_hoverSlot >= 0)
            {
                _ghostIcons[_hoverSlot].gameObject.SetActive(false);
                _slotIcons[_hoverSlot].color = Color.white;
                _slotIcons[_hoverSlot].GetComponent<Outline>().enabled = false;
            }
            _hoverSlot = -1;
            _cardDetails.SetActive(false);
        }

        /// <summary>포탑 목록, 거치대와 선택 결과를 현재 상태에 맞춰 갱신한다.</summary>
        private void Refresh()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                bool exists = i < _rules.Turrets.Count;
                _cards[i].gameObject.SetActive(exists);
                if (!exists) continue;
                TurretRule card = _rules.Turrets[i];
                bool available = IsAvailable(card);
                _cards[i].image.color = available ? (i == _selectedTurret ? new Color(1f, .9f, .7f) : Color.white) : new Color(.5f, .5f, .5f);
                _cardArt[i].color = available ? Color.white : new Color(.7f, .7f, .7f, .85f);
                _cardArt[i].material = available ? null : _lockedMaterial;
                _cardLocks[i].SetActive(!available);
                _cardTexts[i].gameObject.SetActive(available);
                float damage = card.BaseDamage + _loadout.Bonus(card.Id, "damage");
                float range = card.Range + _loadout.Bonus(card.Id, "range");
                float interval = Mathf.Max(.1f, card.AttackInterval - _loadout.Bonus(card.Id, "interval"));
                _cardTexts[i].text = available
                    ? $"{card.Name}\n피해 {damage:0.#}\n사거리 {range:0.#}\n간격 {interval:0.##}초"
                    : $"{card.Name}\n잠김";
            }
            for (int i = 0; i < _slots.Length; i++)
            {
                TurretRule placed = _rules.Turret(TrainArmament.TurretId(i));
                _slotTexts[i].text = placed == null ? "빈 자리" : $"{placed.Name} Lv.{TrainArmament.Level(i)}";
                _slotIcons[i].enabled = placed != null;
                if (placed != null)
                    for (int j = 0; j < _rules.Turrets.Count && j < _turretSprites.Length; j++)
                        if (_rules.Turrets[j].Id == placed.Id) { _slotIcons[i].sprite = _turretSprites[j]; break; }
            }
            if (_selectedSlot < 0) return;
            TurretRule selected = _rules.Turrets[_selectedTurret];
            string current = TrainArmament.TurretId(_selectedSlot);
            bool upgrade = current == selected.Id;
            int cost = upgrade ? _rules.Economy.TurretUpgradeMarks : _rules.Economy.TurretInstallMarks;
            _selection.text = $"{selected.Name}\n{(upgrade ? "강화" : string.IsNullOrEmpty(current) ? "설치" : "교체")} · {cost} 마르크\n교체·철거 시 환불 없음";
            _apply.interactable = true;
            _remove.interactable = !string.IsNullOrEmpty(current);
        }
    }
}
