using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Presentation.Interior;

namespace IronRing.Onboarding
{
    /// <summary>
    /// 스펙 001 FR-022. return_town 팝업이 뜬 동안 카메라를 황동골 역(열린 마을 내부의 열차 앵커) 쪽으로 옮기며 확대하고,
    /// 팝업이 닫히면 강조 전 위치와 크기로 되돌린다. 도착하면 마을 내부가 자동으로 열리므로, 내부가 열린 뒤에 시작한다.
    /// 강조 중에 화면 모드가 바뀌면 카메라는 마을 화면이 맡으므로 되돌리지 않고 끝낸다. OnboardingScreen이 매 프레임 Tick을 부른다.
    /// </summary>
    public sealed class OnboardingCameraFocus
    {
        private readonly float _seconds;
        private readonly float _zoomRatio;
        private Camera _camera;
        private IViewMode _view;
        private bool _pending;
        private bool _returning;
        private bool _active;
        private string _hubId;
        private Vector3 _fromPosition;
        private float _fromSize;
        private Vector3 _startPosition;
        private float _startSize;
        private Vector3 _targetPosition;
        private float _targetSize;
        private float _started;

        /// <summary>seconds(옮기는 시간 초)와 zoomRatio(강조 때 카메라 크기 비율, 1보다 작을수록 확대)를 받아 둔다.</summary>
        public OnboardingCameraFocus(float seconds, float zoomRatio)
        {
            _seconds = Mathf.Max(0.01f, seconds);
            _zoomRatio = Mathf.Clamp(zoomRatio, 0.1f, 1f);
        }

        /// <summary>강조를 예약한다. 마을 내부가 열려 있으면 다음 Tick에서 시작한다.</summary>
        public void Begin()
        {
            _pending = true;
            _returning = false;
        }

        /// <summary>강조 중이면 강조 전 위치와 크기로 되돌리기 시작하고, 예약만 돼 있으면 취소한다.</summary>
        public void End()
        {
            _pending = false;
            if (!_active || _returning) return;
            StartMove(_fromPosition, _fromSize);
            _returning = true;
        }

        /// <summary>
        /// 매 프레임 부른다. 예약돼 있고 마을 내부가 열려 있으면 역 쪽 강조를 시작하고, 진행 중이면 카메라 위치·크기를 보간한다.
        /// 강조 중 마을 내부가 닫히거나 다른 마을로 바뀌면 되돌리지 않고 끝낸다.
        /// </summary>
        public void Tick()
        {
            _view ??= GameServices.Get<IViewMode>();
            if (_pending) TryStart();
            if (!_active) return;
            if (_view == null || !_view.InteriorOpen || _view.InteriorHubId != _hubId) { _active = false; return; }
            float t = Mathf.Clamp01((Time.unscaledTime - _started) / _seconds);
            float eased = t * t * (3f - 2f * t);
            _camera.transform.position = Vector3.Lerp(_startPosition, _targetPosition, eased);
            _camera.orthographicSize = Mathf.Lerp(_startSize, _targetSize, eased);
            if (t >= 1f && _returning) _active = false;
        }

        /// <summary>황동골(첫 마을) 내부가 열려 있으면 지금 카메라 상태를 기억하고 열차 앵커 쪽으로 옮기기 시작한다. 다른 마을이면 예약만 지운다.</summary>
        private void TryStart()
        {
            if (_view == null || !_view.InteriorOpen) return;
            // 귀환 안내는 황동골(첫 마을) 도착을 가리키므로, 다른 마을 내부가 열려 있으면 연출하지 않는다.
            IReadOnlyList<string> hubs = GameServices.Get<IHubStateSystem>()?.HubIds;
            if (hubs != null && hubs.Count > 0 && _view.InteriorHubId != hubs[0]) { _pending = false; Debug.Log("ONBOARDING: camera focus skipped (not home hub)"); return; }
            HubInteriorView interior = Object.FindAnyObjectByType<HubInteriorView>();
            Transform station = interior != null ? interior.TrainAnchor(_view.InteriorHubId) : null;
            _camera = Camera.main;
            _pending = false;
            if (station == null || _camera == null) { Debug.Log("ONBOARDING: camera focus skipped (no station anchor)"); return; }
            _hubId = _view.InteriorHubId;
            _fromPosition = _camera.transform.position;
            _fromSize = _camera.orthographicSize;
            _active = true;
            StartMove(new Vector3(station.position.x, station.position.y, _fromPosition.z), _fromSize * _zoomRatio);
        }

        /// <summary>지금 카메라 상태에서 position·size로 _seconds 동안 옮기기 시작한다.</summary>
        private void StartMove(Vector3 position, float size)
        {
            _startPosition = _camera.transform.position;
            _startSize = _camera.orthographicSize;
            _targetPosition = position;
            _targetSize = size;
            _started = Time.unscaledTime;
        }
    }
}
