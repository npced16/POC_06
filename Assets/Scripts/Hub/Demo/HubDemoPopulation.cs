using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Hub.Demo
{
    /// <summary>Local demo/test population fake, isolated from other story implementations.</summary>
    public sealed class HubDemoPopulation : INpcRecruiter, IFacilityBuilder
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _npcs = new Dictionary<string, IReadOnlyList<string>>();
        private readonly Dictionary<string, IReadOnlyList<string>> _facilities = new Dictionary<string, IReadOnlyList<string>>();
        /// <summary>Unused recruitment event required by the consumed contract.</summary>
        public event Action<string, string> NpcJoined { add { } remove { } }
        /// <summary>Unused building event required by the consumed contract.</summary>
        public event Action<string, string> FacilityBuilt { add { } remove { } }

        /// <summary>Seeds hubId with the supplied NPC and facility counts, replacing the fake's local populations.</summary>
        public void SetCounts(string hubId, int npcs, int facilities)
        {
            _npcs[hubId] = Array.AsReadOnly(new string[npcs]);
            _facilities[hubId] = Array.AsReadOnly(new string[facilities]);
        }

        /// <summary>Returns the fake's NPC list for hubId, or an empty list for an unseeded hub.</summary>
        public IReadOnlyList<string> NpcsAt(string hubId) => _npcs.TryGetValue(hubId, out var values) ? values : Array.Empty<string>();

        /// <summary>Returns the fake's facility list for hubId, or an empty list for an unseeded hub.</summary>
        public IReadOnlyList<string> FacilitiesAt(string hubId) => _facilities.TryGetValue(hubId, out var values) ? values : Array.Empty<string>();

        /// <summary>Accepts hubId visits without recruitment; the demo only simulates hub growth.</summary>
        public void OnHubVisited(string hubId) { }

        /// <summary>Returns false for any hubId/facilityId because this fake does not implement building.</summary>
        public bool CanBuild(string hubId, string facilityId) => false;

        /// <summary>Returns false for any hubId/facilityId without changing fake population.</summary>
        public bool TryBuild(string hubId, string facilityId) => false;

        /// <summary>Returns zero combat bonus for hubId because combat is outside this demo.</summary>
        public float CombatBonus(string hubId) => 0;
    }
}
