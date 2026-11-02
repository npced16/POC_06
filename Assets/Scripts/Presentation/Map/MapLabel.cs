using UnityEngine;

using TMPro;

using DevPhase = IronRing.Contracts.HubPhase;

namespace IronRing.Presentation.Map
{
    /// <summary>
    /// 스토리 022. 선로 지도 표식(HubBuilding·WaypointMarker 프리팹)의 월드 공간 TMP 라벨: 이름(NameLabel)과 거점 단계(PhaseLabel, 거점만).
    /// 이름은 RingHubPhaseView가 SetName으로 쓰고, 아직 안 썼으면 씬 인스턴스의 옛 TextMesh 이름을 그대로 옮긴다(경유지).
    /// 단계 글자와 색은 RingHubPhaseView가 SetPhase로 바꾼다. 위치·크기는 디자이너가 프리팹에서 조정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _phase;
        [Tooltip("단계별 라벨 글자색: 폐허, 복구 중, 정착, 붕괴.")]
        [SerializeField] private Color[] _phaseColors =
        {
            new Color32(196, 140, 108, 255), new Color32(255, 207, 106, 255), new Color32(150, 205, 140, 255), new Color32(214, 96, 80, 255),
        };

        private bool _nameSet;
        private bool _phaseSet;

        void Awake()
        {
            if (!_nameSet && _name != null)
            {
                // 지도 생성기(BuildTopDownMain)가 인스턴스마다 넣어 둔 옛 TextMesh 이름을 옮긴다(예시 문구 대신).
                TextMesh legacy = GetComponentInChildren<TextMesh>(true);
                if (legacy != null && !string.IsNullOrEmpty(legacy.text)) _name.text = legacy.text;
            }
            if (!_phaseSet && _phase != null) _phase.text = string.Empty;
        }

        /// <summary>이름 라벨에 text를 쓴다.</summary>
        public void SetName(string text)
        {
            _nameSet = true;
            if (_name != null) _name.text = text;
        }

        /// <summary>스토리 048. 단계 라벨을 보이거나 숨긴다(잠긴 역 표지).</summary>
        public void SetPhaseVisible(bool visible)
        {
            if (_phase != null && _phase.gameObject.activeSelf != visible) _phase.gameObject.SetActive(visible);
        }

        /// <summary>단계 라벨에 text를 쓰고 phase 색을 입힌다(단계 라벨이 없는 표식이면 무시).</summary>
        public void SetPhase(DevPhase phase, string text)
        {
            _phaseSet = true;
            if (_phase == null) return;
            _phase.text = text;
            int index = (int)phase;
            if (index >= 0 && index < _phaseColors.Length) _phase.color = _phaseColors[index];
        }
    }
}
