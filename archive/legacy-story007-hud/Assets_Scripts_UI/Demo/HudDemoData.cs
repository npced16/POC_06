using System;

using UnityEngine;

namespace IronRing.UI.Demo
{
    /// <summary>Authored demo values, separate from the presentation and real gameplay systems.</summary>
    [Serializable]
    public sealed class HudDemoData
    {
        [Header("Demo catalog")]
        [SerializeField] private string[] _hubIds;
        [SerializeField] private string[] _resourceIds;
        [SerializeField] private string[] _facilityIds;
        [SerializeField] private int[] _slots;
        [Header("Demo state")]
        [SerializeField] private int _resourceAmount;
        [SerializeField] private float _prosperity;
        [SerializeField] private int _lap;
        [SerializeField] private int _buildCost;
        public string[] HubIds => _hubIds;
        public string[] ResourceIds => _resourceIds;
        public string[] FacilityIds => _facilityIds;
        public int[] Slots => _slots;
        public int ResourceAmount => _resourceAmount;
        public float Prosperity => _prosperity;
        public int Lap => _lap;
        public int BuildCost => _buildCost;
    }
}
