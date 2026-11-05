using UnityEngine;

namespace IronRing.Presentation.Map
{
    public enum WaypointContent
    {
        Event,
        Battle
    }

    public sealed class MapNode : MonoBehaviour
    {
        [Header("Identity and authored anchors")]
        [SerializeField] private string _id;
        [SerializeField] private Transform _hub;
        [SerializeField] private Transform _bend;
        [Header("Waypoint content (ignored for hubs)")]
        [SerializeField] private WaypointContent _waypointContent;
        public string Id => _id;
        public Transform Hub => _hub;
        public Transform Bend => _bend;
        public WaypointContent Content => _waypointContent;

        void OnDrawGizmos()
        {
            if (_hub != null || _waypointContent != WaypointContent.Battle) return;
            Gizmos.color = new Color(1f, 0.35f, 0.12f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.65f);
            Gizmos.DrawWireSphere(transform.position, 0.85f);
        }

        /// <summary>id와 거점 및 곡선 기준점을 받아 씬에 저장할 참조를 설정한다.</summary>
        public void Configure(string id, Transform hub, Transform bend)
        {
            _id = id;
            _hub = hub;
            _bend = bend;
        }
    }
}
