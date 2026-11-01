using System;

using UnityEngine;

namespace IronRing.Hud
{
    /// <summary>HUD가 표시하는 자원, 시설과 거점 슬롯 수의 제작 데이터.</summary>
    [Serializable]
    public sealed class HudCatalog
    {
        [Header("목록")]
        [SerializeField] private string[] _resources;
        [SerializeField] private string[] _facilities;
        [SerializeField] private HubEntry[] _hubs;
        [SerializeField] private NodeEntry[] _nodes;
        public string[] Resources => _resources;
        public string[] Facilities => _facilities;
        public HubEntry[] Hubs => _hubs;

        /// <summary>id에 해당하는 제작된 슬롯 수를 반환하며 게임 상태를 변경하지 않는다.</summary>
        public int SlotCount(string id)
        {
            foreach (HubEntry hub in _hubs) if (hub.Id == id) return hub.Slots;
            return 0;
        }

        /// <summary>id를 문자열 테이블 키로 변환해 반환하며 알 수 없는 노드는 현재 위치 키를 반환한다.</summary>
        public string NodeKey(string id)
        {
            foreach (NodeEntry node in _nodes) if (node.Id == id) return node.Key;
            return "hud_location";
        }

        [Serializable]
        public sealed class HubEntry
        {
            [SerializeField] private string _id;
            [SerializeField] private int _slots;
            public string Id => _id;
            public int Slots => _slots;
        }

        [Serializable]
        private sealed class NodeEntry
        {
            [SerializeField] private string _id;
            [SerializeField] private string _key;
            public string Id => _id;
            public string Key => _key;
        }
    }
}
