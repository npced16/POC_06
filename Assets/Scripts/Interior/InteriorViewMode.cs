using System;

using IronRing.Contracts.Presentation;

namespace IronRing.Interior
{
    /// <summary>
    /// 스토리 012. 선로 화면/거점 내부 화면 전환 상태의 순수 C# 구현. Bootstrap이 정차 시 Open, 출발 시 Close를 부르고
    /// 뷰·HUD는 IViewMode로 읽기만 한다. 전이표: Closed --Open(hub)--> Open(hub); Open(hub) --Close--> Closed;
    /// Open(a) --Open(b)--> Closed --> Open(b) (닫힘 알림 뒤 열림 알림); 같은 거점 Open과 닫힌 상태 Close는 무시.
    /// </summary>
    public sealed class InteriorViewMode : IViewMode
    {
        /// <summary>내부 화면이 열려 있는지.</summary>
        public bool InteriorOpen => InteriorHubId != null;
        /// <summary>열려 있는 내부 화면의 거점 id, 아니면 null.</summary>
        public string InteriorHubId { get; private set; }
        /// <summary>열릴 때 true, 닫힐 때 false.</summary>
        public event Action<bool> InteriorChanged;

        /// <summary>hubId의 내부 화면을 연다. 다른 거점이 열려 있으면 먼저 닫는다. 같은 거점이면 아무것도 하지 않는다.</summary>
        public void Open(string hubId)
        {
            if (string.IsNullOrWhiteSpace(hubId)) throw new ArgumentException("Hub id is required.", nameof(hubId));
            if (InteriorHubId == hubId) return;
            if (InteriorOpen) Close();
            InteriorHubId = hubId;
            InteriorChanged?.Invoke(true);
        }

        /// <summary>내부 화면을 닫고 선로 화면으로 돌아간다. 이미 닫혀 있으면 아무것도 하지 않는다.</summary>
        public void Close()
        {
            if (!InteriorOpen) return;
            InteriorHubId = null;
            InteriorChanged?.Invoke(false);
        }
    }
}
