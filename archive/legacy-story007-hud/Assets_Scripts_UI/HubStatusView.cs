namespace IronRing.UI
{
    /// <summary>Immutable hub presentation snapshot, independent of Unity and gameplay implementations.</summary>
    public sealed class HubStatusView
    {
        public string Id { get; }
        public float Prosperity { get; }
        public bool IsRuined { get; }
        public bool HabitatReady { get; }
        public int NpcCount { get; }
        public int FacilityCount { get; }
        /// <summary>Copies the supplied hub identity, prosperity, ruin, habitat, NPC and facility values into a read-only record.</summary>
        public HubStatusView(string id, float prosperity, bool ruined, bool habitatReady, int npcCount, int facilityCount)
        {
            Id = id; Prosperity = prosperity; IsRuined = ruined;
            HabitatReady = habitatReady; NpcCount = npcCount; FacilityCount = facilityCount;
        }
    }
}
