using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Hub.Demo
{
    /// <summary>Local demo/test population fake, isolated from other story implementations.</summary>
    public sealed class HubDemoPopulation : INpcRecruiter
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _npcs = new Dictionary<string, IReadOnlyList<string>>();
        /// <summary>Unused recruitment event required by the consumed contract.</summary>
        public event Action<string, string> NpcJoined { add { } remove { } }

        /// <summary>Seeds hubId with the supplied NPC count, replacing the fake's local population.</summary>
        public void SetCounts(string hubId, int npcs)
        {
            _npcs[hubId] = Array.AsReadOnly(new string[npcs]);
        }

        /// <summary>Returns the fake's NPC list for hubId, or an empty list for an unseeded hub.</summary>
        public IReadOnlyList<string> NpcsAt(string hubId) => _npcs.TryGetValue(hubId, out var values) ? values : Array.Empty<string>();

        /// <summary>Accepts hubId visits without recruitment; the demo only simulates hub growth.</summary>
        public void OnHubVisited(string hubId) { }
    }
}
