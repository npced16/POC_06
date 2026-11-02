using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Defense.Demo
{
    /// <summary>Isolated interface inventory for the facility demo and tests.</summary>
    public sealed class DefenseDemoInventory : IResourceInventory
    {
        private readonly Dictionary<string, int> _stock = new Dictionary<string, int>();
        /// <summary>Publishes the changed resource id and its resulting balance.</summary>
        public event Action<string, int> Changed;

        /// <summary>Returns the stored amount for resourceId or zero.</summary>
        public int Get(string resourceId) => _stock.TryGetValue(resourceId, out int amount) ? amount : 0;
        /// <summary>Adds amount to resourceId and publishes its new balance.</summary>
        public void Add(string resourceId, int amount) { _stock[resourceId] = Get(resourceId) + amount; Changed?.Invoke(resourceId, Get(resourceId)); }
        /// <summary>Spends amount from resourceId when affordable and returns success.</summary>
        public bool TrySpend(string resourceId, int amount)
        {
            if (Get(resourceId) < amount) return false;
            _stock[resourceId] = Get(resourceId) - amount;
            Changed?.Invoke(resourceId, Get(resourceId));
            return true;
        }
        /// <summary>Returns a copy of the current resource balances.</summary>
        public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_stock);
    }

    /// <summary>Isolated hub-state contract used to show and test facility effects.</summary>
    public sealed class DefenseDemoHubs : IHubStateSystem
    {
        private readonly Dictionary<string, float> _prosperity = new Dictionary<string, float>();
        private readonly HashSet<string> _ruins = new HashSet<string>();
        /// <summary>Hub ids provided to this local fake.</summary>
        public IReadOnlyList<string> HubIds { get; }
        /// <summary>Whether all local fake hubs are ruined.</summary>
        public bool IsRunLost => _ruins.Count == HubIds.Count;
        /// <summary>Publishes a changed hub and its resulting prosperity.</summary>
        public event Action<string, float> ProsperityChanged;
        /// <summary>Publishes a hub when it first becomes ruined.</summary>
        public event Action<string> HubRuined;
        /// <summary>Publishes when all fake hubs become ruined.</summary>
        public event Action RunLost;

        /// <summary>Initializes the supplied ids as live hubs with zero prosperity.</summary>
        public DefenseDemoHubs(params string[] ids) { HubIds = Array.AsReadOnly(ids); foreach (string id in ids) _prosperity.Add(id, 0f); }
        /// <summary>Returns prosperity stored for hubId.</summary>
        public float GetProsperity(string hubId) => _prosperity[hubId];
        /// <summary>Returns whether hubId is marked ruined.</summary>
        public bool IsRuined(string hubId) => _ruins.Contains(hubId);
        /// <summary>Applies delta for hubId and publishes its new prosperity; reason identifies the effect source.</summary>
        public void ApplyDelta(string hubId, float delta, string reason) { _prosperity[hubId] += delta; ProsperityChanged?.Invoke(hubId, _prosperity[hubId]); }
        /// <summary>Accepts a lap notification without simulating another story's growth rules.</summary>
        public void OnLapCompleted(int lap) { }
        /// <summary>Marks hubId ruined and raises the corresponding state notifications.</summary>
        public void Ruin(string hubId) { if (!_ruins.Add(hubId)) return; HubRuined?.Invoke(hubId); if (IsRunLost) RunLost?.Invoke(); }
    }
}
