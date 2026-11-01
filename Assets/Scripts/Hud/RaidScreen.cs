using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud
{
    /// <summary>
    /// 스토리 033. 항상 켜진 화면 "Raid": 습격 중에만 내구도 바("내구도 7/10")·"둥지 N · 몬스터 N"·"클릭으로 사격" 패널을 보이고,
    /// 시작·끝 토스트를 띄운다. IRaid만 읽고 게임 상태를 바꾸지 않는다. 습격 중 다른 입력 차단 여부는 IsRaidActive로 묻는다.
    /// </summary>
    public sealed class RaidScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _hullFill;
        [SerializeField] private TMP_Text _hullLabel;
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private float _toastSeconds = 3f;
        private IRaid _raid;

        /// <summary>습격이 진행 중이면 true(배치·대화·도감 입력을 막을 때 쓴다).</summary>
        public static bool IsRaidActive => GameServices.Get<IRaid>()?.IsActive ?? false;

        private void Update()
        {
            if (_raid == null)
            {
                _raid = GameServices.Get<IRaid>();
                if (_raid == null) { if (_panel != null && _panel.activeSelf) _panel.SetActive(false); return; }
                _raid.RaidStarted += OnStarted;
                _raid.RaidEnded += OnEnded;
            }
            bool on = _raid.IsActive;
            if (_panel != null && _panel.activeSelf != on) _panel.SetActive(on);
            if (!on) return;
            float ratio = _raid.HullMax > 0 ? Mathf.Clamp01((float)_raid.Hull / _raid.HullMax) : 0f;
            if (_hullFill != null) _hullFill.anchorMax = new Vector2(ratio, 1f);
            if (_hullLabel != null) _hullLabel.text = string.Format(Strings.Get("raid_hull"), _raid.Hull, _raid.HullMax);
            if (_countLabel != null) _countLabel.text = string.Format(Strings.Get("raid_counts"), _raid.NestsAlive, _raid.MonstersAlive);
        }

        private void OnStarted() => ToastScreen.Show(Strings.Get("raid_toast_start"), _toastSeconds);

        private void OnEnded(bool victory)
        {
            string text = Strings.Get(victory ? "raid_toast_victory" : "raid_toast_defeat");
            if (victory && _raid.TerminalLiberated) text += "  ·  " + Strings.Get("raid_toast_liberated");
            ToastScreen.Show(text, _toastSeconds);
        }

        private void OnDestroy()
        {
            if (_raid == null) return;
            _raid.RaidStarted -= OnStarted;
            _raid.RaidEnded -= OnEnded;
        }
    }
}
