using UnityEngine;

namespace IronRing.Presentation.Map
{
    public sealed class HubTarget : MonoBehaviour
    {
        [Header("Hub identity")]
        [SerializeField] private string _hubId;
        public string HubId => _hubId;

        /// <summary>입력된 hubId를 저장하여 충돌체 클릭이 실제 거점을 식별하도록 한다.</summary>
        public void Configure(string hubId) => _hubId = hubId;
    }
}
