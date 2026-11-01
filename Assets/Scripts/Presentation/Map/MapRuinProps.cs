using System;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Presentation.Interior;

namespace IronRing.Presentation.Map
{
    /// <summary>
    /// 스토리 036. 선로 화면 거점 둘레의 폐허 소품 묶음(씬 RuinProps/RuinProps_{거점 id}, RuinBuild.GenerateMap이 프리팹 인스턴스로 놓는다).
    /// 거점 단계에 따라 소품(웅덩이·잡초·잔해)과 모닥불을 켜고 끈다. 어느 단계에서 켤지는 RuinDressing.json _map에서 읽는다.
    /// </summary>
    public sealed class MapRuinProps : MonoBehaviour
    {
        [SerializeField] private string _hubId;
        [SerializeField] private GameObject[] _props = Array.Empty<GameObject>();
        [SerializeField] private GameObject _campfire;
        [SerializeField] private string _dataResource = RuinDressingData.Resource;
        private IHubDevelopment _development;
        private RuinDressingData _data;

        /// <summary>이 묶음의 거점 id.</summary>
        public string HubId => _hubId;

        /// <summary>편집기 제작 도구가 거점 id, 소품들, 모닥불을 기록한다.</summary>
        public void Configure(string hubId, GameObject[] props, GameObject campfire)
        {
            _hubId = hubId;
            _props = props ?? Array.Empty<GameObject>();
            _campfire = campfire;
        }

        private void OnEnable() => GameServices.Ready += Bind;

        private void OnDisable()
        {
            GameServices.Ready -= Bind;
            if (_development != null) _development.PhaseChanged -= OnPhaseChanged;
            _development = null;
        }

        private void Bind()
        {
            if (_development != null) return;
            _development = GameServices.Get<IHubDevelopment>();
            if (_development == null) return;
            _data ??= RuinDressingData.Load(_dataResource);
            _development.PhaseChanged += OnPhaseChanged;
            Apply(_development.GetPhase(_hubId));
        }

        private void OnPhaseChanged(string hubId, IronRing.Contracts.HubPhase phase) { if (hubId == _hubId) Apply(phase); }

        private void Apply(IronRing.Contracts.HubPhase phase)
        {
            bool props = RuinDressingData.Has(_data?._map?._propPhases, phase);
            foreach (GameObject prop in _props) if (prop != null && prop.activeSelf != props) prop.SetActive(props);
            bool fire = RuinDressingData.Has(_data?._map?._campfirePhases, phase);
            if (_campfire != null && _campfire.activeSelf != fire) _campfire.SetActive(fire);
        }
    }
}
