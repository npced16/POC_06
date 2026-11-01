using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Combat.Demo
{
    public sealed class CombatDemoNode : ITrackNode
    {
        public string Id { get; set; }
        public int Index { get; set; }
        public TrackNodeKind Kind { get; set; }
        public string HubId { get; set; }
    }

    public sealed class CombatDemoTrack : ITrackRuntime
    {
        private readonly HashSet<string> _holds = new HashSet<string>();
        public IReadOnlyList<ITrackNode> Nodes { get; }
        public ITrackNode CurrentNode { get; private set; }
        public int Lap => 0;
        public bool IsHeld => _holds.Count > 0;
        public event Action<ITrackNode> Arrived;
        public event Action<int> LapCompleted { add { } remove { } }
        /// <summary>Accepts ring nodes and initializes the demonstration at its first node.</summary>
        public CombatDemoTrack(params ITrackNode[] nodes) { Nodes = nodes; CurrentNode = nodes[0]; }
        /// <summary>Raises an arrival for the supplied node unless movement is held.</summary>
        public void Arrive(ITrackNode node) { if (IsHeld) return; CurrentNode = node; Arrived?.Invoke(node); }
        /// <summary>Leaves departure unchanged because the demo only models encounter holds.</summary>
        public void Depart() { }
        /// <summary>Adds the supplied reason to the set of movement holds.</summary>
        public void RequestHold(string reason) { _holds.Add(reason); }
        /// <summary>Removes only the supplied reason from the movement holds.</summary>
        public void ReleaseHold(string reason) { _holds.Remove(reason); }
    }

    public sealed class CombatDemoInventory : IResourceInventory
    {
        private readonly Dictionary<string, int> _stock = new Dictionary<string, int>();
        public event Action<string, int> Changed;
        /// <summary>Returns the current stock for the supplied resource, or zero.</summary>
        public int Get(string resourceId) => _stock.TryGetValue(resourceId, out int amount) ? amount : 0;
        /// <summary>Adds a non-negative amount to the named stock and publishes its new value.</summary>
        public void Add(string resourceId, int amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); _stock[resourceId] = Get(resourceId) + amount; Changed?.Invoke(resourceId, Get(resourceId)); }
        /// <summary>Spends the supplied amount only when available and returns whether stock changed.</summary>
        public bool TrySpend(string resourceId, int amount) { if (amount < 0 || Get(resourceId) < amount) return false; _stock[resourceId] = Get(resourceId) - amount; Changed?.Invoke(resourceId, Get(resourceId)); return true; }
        /// <summary>Returns a copy of all demo resource balances.</summary>
        public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_stock);
    }

    public sealed class CombatDemoHubs : IHubStateSystem
    {
        private readonly Dictionary<string, float> _prosperity = new Dictionary<string, float>();
        public IReadOnlyList<string> HubIds { get; }
        public bool IsRunLost => false;
        public string LastChangedHub { get; private set; }
        public event Action<string, float> ProsperityChanged;
        public event Action<string> HubRuined { add { } remove { } }
        public event Action RunLost { add { } remove { } }
        /// <summary>Initializes each supplied hub to the given demo prosperity.</summary>
        public CombatDemoHubs(float initial, params string[] ids) { HubIds = ids; foreach (string id in ids) _prosperity[id] = initial; }
        /// <summary>Returns the prosperity of the supplied hub.</summary>
        public float GetProsperity(string hubId) => _prosperity[hubId];
        /// <summary>Returns whether the supplied hub has depleted prosperity.</summary>
        public bool IsRuined(string hubId) => GetProsperity(hubId) <= 0;
        /// <summary>Applies a signed delta to the named hub and publishes the new prosperity.</summary>
        public void ApplyDelta(string hubId, float delta, string reason) { _prosperity[hubId] += delta; LastChangedHub = hubId; ProsperityChanged?.Invoke(hubId, _prosperity[hubId]); }
        /// <summary>Accepts a lap notification without changing demo prosperity.</summary>
        public void OnLapCompleted(int lap) { }
    }

    public sealed class CombatDemoFacilities : IFacilityBuilder
    {
        public float Bonus { get; set; }
        public string LastQueriedHub { get; private set; }
        public event Action<string, string> FacilityBuilt { add { } remove { } }
        /// <summary>Returns the empty demo facility list for any hub.</summary>
        public IReadOnlyList<string> FacilitiesAt(string hubId) => Array.Empty<string>();
        /// <summary>Returns false because building is outside the encounter demo.</summary>
        public bool CanBuild(string hubId, string facilityId) => false;
        /// <summary>Returns false without spending resources because this demo does not build.</summary>
        public bool TryBuild(string hubId, string facilityId) => false;
        /// <summary>Records the queried hub and returns the configured demo combat bonus.</summary>
        public float CombatBonus(string hubId) { LastQueriedHub = hubId; return Bonus; }
    }

    public sealed class CombatDemoRandom : IRandomSource
    {
        private readonly Queue<float> _rolls;
        private readonly int _integer;
        /// <summary>Stores scripted rolls and an integer offset for deterministic demonstrations.</summary>
        public CombatDemoRandom(int integer, params float[] rolls) { _integer = integer; _rolls = new Queue<float>(rolls); }
        /// <summary>Returns and consumes the next scripted roll; throws when the script is exhausted.</summary>
        public float NextFloat() => _rolls.Dequeue();
        /// <summary>Returns the configured offset clamped to the supplied integer range.</summary>
        public int NextInt(int minInclusive, int maxExclusive) => Math.Min(maxExclusive - 1, minInclusive + _integer);
    }
}
