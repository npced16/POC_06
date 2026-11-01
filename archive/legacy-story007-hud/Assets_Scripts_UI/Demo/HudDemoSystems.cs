using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.UI.Demo
{
    /// <summary>Scripted demo-only implementations that publish the same events as real systems.</summary>
    public sealed class HudDemoSystems : ITrackRuntime, IResourceInventory, IHabitatSystem,
        INpcRecruiter, IHubStateSystem, IWaypointResolver, IFacilityBuilder
    {
        private readonly HudDemoData _data;
        private readonly List<ITrackNode> _nodes = new List<ITrackNode>();
        private readonly Dictionary<string, int> _stock = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _prosperity = new Dictionary<string, float>();
        private readonly Dictionary<string, string> _slots = new Dictionary<string, string>();
        private readonly Dictionary<string, List<string>> _built = new Dictionary<string, List<string>>();
        private readonly HashSet<string> _flags = new HashSet<string>();
        private readonly HashSet<string> _ruins = new HashSet<string>();
        public IReadOnlyList<ITrackNode> Nodes => _nodes;
        public ITrackNode CurrentNode { get; private set; }
        public int Lap { get; private set; }
        public bool IsHeld { get; private set; }
        public IReadOnlyList<string> HubIds => _data.HubIds;
        public bool IsRunLost => _ruins.Count == _data.HubIds.Length;
        public bool IsEncounterActive { get; private set; }
        public event Action<ITrackNode> Arrived;
        public event Action<int> LapCompleted;
        public event Action<string, int> Changed;
        public event Action<string> EventFlagRaised;
        public event Action<string, string> NpcJoined;
        public event Action<string, float> ProsperityChanged;
        public event Action<string> HubRuined;
        public event Action RunLost;
        public event Action<ITrackNode> EncounterStarted;
        public event Action<ITrackNode, EncounterOutcome> EncounterEnded;
        public event Action<string, string> FacilityBuilt;

        /// <summary>Uses authored demo data to initialize inventory, nodes and hub snapshots.</summary>
        public HudDemoSystems(HudDemoData data)
        {
            _data = data; Lap = data.Lap;
            foreach (string id in data.ResourceIds) _stock[id] = data.ResourceAmount;
            foreach (string id in data.HubIds)
            {
                _nodes.Add(new DemoNode(id, _nodes.Count));
                _prosperity[id] = data.Prosperity; _built[id] = new List<string>();
            }
            CurrentNode = _nodes[0];
        }
        /// <summary>Uses the resource id to return its demo stock amount.</summary>
        public int Get(string resourceId) => _stock.TryGetValue(resourceId, out int value) ? value : 0;
        /// <summary>Uses resource id and amount to increase stock and emit its event.</summary>
        public void Add(string resourceId, int amount) { _stock[resourceId] = Get(resourceId) + amount; Changed?.Invoke(resourceId, Get(resourceId)); }
        /// <summary>Uses resource id and amount to spend available stock; returns success and emits the new amount.</summary>
        public bool TrySpend(string resourceId, int amount)
        {
            if (amount < 0 || Get(resourceId) < amount) return false;
            _stock[resourceId] = Get(resourceId) - amount; Changed?.Invoke(resourceId, Get(resourceId)); return true;
        }
        /// <summary>Returns an inventory snapshot without modifying stock.</summary>
        public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_stock);
        /// <summary>Uses hub, slot and resource ids to place one item; returns refusal for invalid or occupied slots.</summary>
        public bool TryPlace(string hubId, int slotIndex, string resourceId)
        {
            string key = hubId + ":" + slotIndex;
            if (!_prosperity.ContainsKey(hubId) || IsRuined(hubId) || Array.IndexOf(_data.Slots, slotIndex) < 0 || _slots.ContainsKey(key)) return false;
            if (!TrySpend(resourceId, 1)) return false;
            _slots[key] = resourceId;
            if (_flags.Add(hubId)) EventFlagRaised?.Invoke(hubId);
            UnityEngine.Debug.Log("STORY007 PLACE accepted " + key + " " + resourceId);
            return true;
        }
        /// <summary>Uses hub and slot to remove a placed resource and return it to demo stock.</summary>
        public bool TryRemove(string hubId, int slotIndex)
        {
            string key = hubId + ":" + slotIndex;
            if (!_slots.TryGetValue(key, out string resource)) return false;
            _slots.Remove(key); Add(resource, 1); return true;
        }
        /// <summary>Uses hub and slot to return its resource id or null.</summary>
        public string SlotContent(string hubId, int slotIndex) => _slots.TryGetValue(hubId + ":" + slotIndex, out string resource) ? resource : null;
        /// <summary>Uses hub id to report the scripted demo habitat flag.</summary>
        public bool IsHabitatComplete(string hubId) => _flags.Contains(hubId);
        /// <summary>Uses hub id to report its event flag without modifying it.</summary>
        public bool IsEventFlagSet(string hubId) => _flags.Contains(hubId);
        /// <summary>Uses a hub visit to emit the scripted NPC notification.</summary>
        public void OnHubVisited(string hubId) { NpcJoined?.Invoke(hubId, "Demo engineer"); }
        /// <summary>Uses hub id to return the demo's empty NPC roster.</summary>
        public IReadOnlyList<string> NpcsAt(string hubId) => Array.Empty<string>();
        /// <summary>Uses hub id to return its prosperity value.</summary>
        public float GetProsperity(string hubId) => _prosperity[hubId];
        /// <summary>Uses hub id to return whether it is ruined.</summary>
        public bool IsRuined(string hubId) => _ruins.Contains(hubId);
        /// <summary>Uses hub, delta and reason to update prosperity and emit ruin and defeat transitions.</summary>
        public void ApplyDelta(string hubId, float delta, string reason)
        {
            _prosperity[hubId] += delta; ProsperityChanged?.Invoke(hubId, _prosperity[hubId]);
            if (_prosperity[hubId] <= 0 && _ruins.Add(hubId)) { HubRuined?.Invoke(hubId); if (IsRunLost) RunLost?.Invoke(); }
        }
        /// <summary>Uses a supplied lap to update and publish the demo track lap.</summary>
        public void OnLapCompleted(int lap) { Lap = lap; LapCompleted?.Invoke(lap); }
        /// <summary>Advances to the next data node and publishes arrival.</summary>
        public void Depart() { CurrentNode = _nodes[(CurrentNode.Index + 1) % _nodes.Count]; Arrived?.Invoke(CurrentNode); }
        /// <summary>Uses a hold reason to set the demo held state.</summary>
        public void RequestHold(string reason) { IsHeld = true; }
        /// <summary>Uses a hold reason to release the demo held state.</summary>
        public void ReleaseHold(string reason) { IsHeld = false; }
        /// <summary>Uses an encounter node to emit scripted start and completion events.</summary>
        public void OnWaypointArrived(ITrackNode node)
        {
            IsEncounterActive = true; EncounterStarted?.Invoke(node);
            IsEncounterActive = false; EncounterEnded?.Invoke(node, EncounterOutcome.EventResolved);
        }
        /// <summary>Uses hub id to return its built facilities.</summary>
        public IReadOnlyList<string> FacilitiesAt(string hubId) => _built[hubId];
        /// <summary>Uses hub and facility ids to evaluate authored demo cost and ruin constraints.</summary>
        public bool CanBuild(string hubId, string facilityId) => !IsRuined(hubId) && Array.IndexOf(_data.FacilityIds, facilityId) >= 0 && Get(_data.ResourceIds[0]) >= _data.BuildCost;
        /// <summary>Uses hub and facility ids to request demo construction; spends its authored cost and emits completion on success.</summary>
        public bool TryBuild(string hubId, string facilityId)
        {
            if (!CanBuild(hubId, facilityId) || !TrySpend(_data.ResourceIds[0], _data.BuildCost)) return false;
            _built[hubId].Add(facilityId); FacilityBuilt?.Invoke(hubId, facilityId);
            UnityEngine.Debug.Log("STORY007 BUILD accepted " + hubId + " " + facilityId); return true;
        }
        /// <summary>Uses hub id to return a neutral demo combat bonus.</summary>
        public float CombatBonus(string hubId) => 0;

        private sealed class DemoNode : ITrackNode
        {
            public string Id { get; }
            public int Index { get; }
            public TrackNodeKind Kind => TrackNodeKind.Hub;
            public string HubId => Id;
            /// <summary>Uses the data id and ring index to construct an immutable demo node.</summary>
            public DemoNode(string id, int index) { Id = id; Index = index; }
        }
    }
}
