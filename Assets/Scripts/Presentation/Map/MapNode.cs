using UnityEngine;

namespace IronRing.Presentation.Map
{
    public sealed class MapNode : MonoBehaviour
    {
        [Header("Identity and authored anchors")]
        [SerializeField] private string _id;
        [SerializeField] private Transform _hub;
        [SerializeField] private Transform _bend;
        public string Id => _id;
        public Transform Hub => _hub;
        public Transform Bend => _bend;

        /// <summary>id와 거점 및 곡선 기준점을 받아 씬에 저장할 참조를 설정한다.</summary>
        public void Configure(string id, Transform hub, Transform bend)
        {
            _id = id;
            _hub = hub;
            _bend = bend;
        }
    }
}
