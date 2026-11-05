using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Localization;
using IronRing.Save;

namespace IronRing.Title
{
    /// <summary>스펙 002 FR-008~FR-013. 이어하기 슬롯 선택 패널: 슬롯 칸 채우기, 칸 선택, 불러오기, 뒤로.</summary>
    public sealed class SlotSelectPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private TMP_Text _header;
        [SerializeField] private SlotRowView[] _rows;
        [SerializeField] private Button _loadButton;
        [SerializeField] private TMP_Text _loadLabel;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _backLabel;

        private SaveSlotInfo[] _infos;
        private int _selectedSlot = -1;

        private void Awake()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                int index = i;
                _rows[i].Button.onClick.AddListener(() => Select(index));
            }
            _loadButton.onClick.AddListener(OnLoad);
            _backButton.onClick.AddListener(OnBack);
        }

        /// <summary>패널을 켜고 SaveSlots.List()로 칸을 다시 채운다. 고른 칸을 지우고 불러오기를 비활성으로 둔다(FR-007·FR-008·FR-011).</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            _header.text = Strings.Get("title_continue");
            _loadLabel.text = Strings.Get("title_load");
            _backLabel.text = Strings.Get("title_back");
            _infos = SaveSlots.List();
            for (int i = 0; i < _rows.Length; i++) FillRow(i);
            _selectedSlot = -1;
            _loadButton.interactable = false;
        }

        /// <summary>
        /// i번 칸을 _infos[i]로 채운다. 결과가 모자라거나 빈 칸이면 빈 슬롯 문구, 손상 칸이면 손상 문구를 흐리게 쓰고 고르지 못하게 둔다(FR-009).
        /// 정상 칸은 SaveSlotInfo.Summary를 그대로 쓴다(FR-008).
        /// </summary>
        private void FillRow(int i)
        {
            bool exists = i < _infos.Length;
            int slot = exists ? _infos[i].Slot : i + 1;
            string label = string.Format(Strings.Get("save_slot_label"), slot);
            if (!exists || _infos[i].IsEmpty) _rows[i].Show(label, Strings.Get("save_slot_empty"), false);
            else if (_infos[i].IsCorrupt) _rows[i].Show(label, Strings.Get("save_slot_corrupt"), false);
            else _rows[i].Show(label, _infos[i].Summary, true);
        }

        /// <summary>index 칸을 고른다. 그 칸만 선택 테두리로 바꾸고 불러오기를 활성으로 둔다(FR-010·FR-011).</summary>
        private void Select(int index)
        {
            _selectedSlot = index;
            for (int i = 0; i < _rows.Length; i++) _rows[i].SetSelected(i == index);
            _loadButton.interactable = true;
        }

        /// <summary>불러오기 처리. 버튼을 비활성으로 바꾼 뒤 고른 칸의 슬롯 번호로 SaveSlots.LoadAndStart를 한 번 부른다(FR-012).</summary>
        private void OnLoad()
        {
            _loadButton.interactable = false;
            SaveSlots.LoadAndStart(_infos[_selectedSlot].Slot);
        }

        /// <summary>뒤로 처리. 고른 칸을 지우고 패널을 끈다(FR-013).</summary>
        private void OnBack()
        {
            _selectedSlot = -1;
            gameObject.SetActive(false);
        }
    }
}
