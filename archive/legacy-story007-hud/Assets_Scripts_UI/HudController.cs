using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.UI
{
    /// <summary>Event-driven presentation model; only explicit player commands request system changes.</summary>
    public sealed class HudController : IDisposable
    {
        private readonly ITrackRuntime _track;
        private readonly IResourceInventory _inventory;
        private readonly IHabitatSystem _habitat;
        private readonly INpcRecruiter _npcs;
        private readonly IHubStateSystem _hubs;
        private readonly IWaypointResolver _waypoints;
        private readonly IFacilityBuilder _facilities;
        private readonly Dictionary<string, int> _resources = new Dictionary<string, int>();
        private readonly Dictionary<string, HubStatusView> _hubViews = new Dictionary<string, HubStatusView>();
        public IReadOnlyDictionary<string, int> Resources => _resources;
        public IReadOnlyDictionary<string, HubStatusView> Hubs => _hubViews;
        public string CurrentNode { get; private set; }
        public int Lap { get; private set; }
        public bool IsLost { get; private set; }
        public string SelectedHub { get; private set; }
        public string Message { get; private set; } = "Select a hub to manage its habitat and defenses.";
        public event Action Changed;

        /// <summary>Receives all seven system interfaces, reads their initial snapshots and subscribes to subsequent changes.</summary>
        public HudController(ITrackRuntime track, IResourceInventory inventory, IHabitatSystem habitat,
            INpcRecruiter npcs, IHubStateSystem hubs, IWaypointResolver waypoints, IFacilityBuilder facilities)
        {
            _track = track; _inventory = inventory; _habitat = habitat; _npcs = npcs;
            _hubs = hubs; _waypoints = waypoints; _facilities = facilities;
            foreach (var pair in inventory.Snapshot()) _resources.Add(pair.Key, pair.Value);
            foreach (string id in hubs.HubIds) RefreshHub(id);
            CurrentNode = track.CurrentNode?.Id ?? "In transit";
            Lap = track.Lap; IsLost = hubs.IsRunLost;
            track.Arrived += OnArrived; track.LapCompleted += OnLap;
            inventory.Changed += OnResource;
            habitat.EventFlagRaised += OnHabitat;
            npcs.NpcJoined += OnNpc;
            hubs.ProsperityChanged += OnProsperity; hubs.HubRuined += OnRuined; hubs.RunLost += OnLost;
            waypoints.EncounterStarted += OnEncounterStarted; waypoints.EncounterEnded += OnEncounterEnded;
            facilities.FacilityBuilt += OnBuilt;
        }

        /// <summary>Uses the hub id to change only local selection and publishes the new presentation state.</summary>
        public void SelectHub(string hubId)
        {
            if (!_hubViews.ContainsKey(hubId)) return;
            SelectedHub = hubId; Message = "Selected " + hubId; Changed?.Invoke();
        }

        /// <summary>Requests placement using the selected hub, slot and resource; returns acceptance without spending resources itself.</summary>
        public bool Place(int slotIndex, string resourceId)
        {
            bool accepted = !IsLost && SelectedHub != null && _habitat.TryPlace(SelectedHub, slotIndex, resourceId);
            Message = accepted ? "Placement accepted." : "Placement refused by habitat system.";
            Changed?.Invoke(); return accepted;
        }

        /// <summary>Requests a facility by id at the selected hub; returns the builder result without changing inventory or hub state.</summary>
        public bool Build(string facilityId)
        {
            bool accepted = !IsLost && SelectedHub != null && _facilities.TryBuild(SelectedHub, facilityId);
            Message = accepted ? "Construction accepted." : "Construction refused by facility system.";
            Changed?.Invoke(); return accepted;
        }

        /// <summary>Reads one hub's snapshot from injected interfaces and replaces its presentation record.</summary>
        private void RefreshHub(string id)
        {
            _hubViews[id] = new HubStatusView(id, _hubs.GetProsperity(id), _hubs.IsRuined(id),
                _habitat.IsEventFlagSet(id), _npcs.NpcsAt(id).Count, _facilities.FacilitiesAt(id).Count);
        }
        /// <summary>Uses the arrival node to update the displayed location.</summary>
        private void OnArrived(ITrackNode node) { CurrentNode = node.Id; Changed?.Invoke(); }
        /// <summary>Uses the completed lap count to update the displayed lap.</summary>
        private void OnLap(int lap) { Lap = lap; Changed?.Invoke(); }
        /// <summary>Uses the resource id and event amount to replace the cached quantity.</summary>
        private void OnResource(string id, int amount) { _resources[id] = amount; Changed?.Invoke(); }
        /// <summary>Uses the hub id to refresh habitat status after its event.</summary>
        private void OnHabitat(string id) { RefreshHub(id); Changed?.Invoke(); }
        /// <summary>Uses the joining hub and NPC id to refresh the hub's population display.</summary>
        private void OnNpc(string id, string npc) { RefreshHub(id); Changed?.Invoke(); }
        /// <summary>Uses the changed hub id and prosperity notification to refresh its status.</summary>
        private void OnProsperity(string id, float value) { RefreshHub(id); Changed?.Invoke(); }
        /// <summary>Uses the ruined hub id to preserve its row and show ruins.</summary>
        private void OnRuined(string id) { RefreshHub(id); Changed?.Invoke(); }
        /// <summary>Uses the run-lost event to latch defeat presentation without changing the simulation.</summary>
        private void OnLost() { IsLost = true; Changed?.Invoke(); }
        /// <summary>Uses the encounter node to display an active encounter message.</summary>
        private void OnEncounterStarted(ITrackNode node) { Message = "Encounter at " + node.Id; Changed?.Invoke(); }
        /// <summary>Uses the encounter node and result to display its outcome.</summary>
        private void OnEncounterEnded(ITrackNode node, EncounterOutcome outcome) { Message = node.Id + ": " + outcome; Changed?.Invoke(); }
        /// <summary>Uses the building's hub and facility id to refresh the facility count.</summary>
        private void OnBuilt(string id, string facility) { RefreshHub(id); Changed?.Invoke(); }

        /// <summary>Removes all subscriptions from the injected systems, leaving system state unchanged.</summary>
        public void Dispose()
        {
            _track.Arrived -= OnArrived; _track.LapCompleted -= OnLap; _inventory.Changed -= OnResource;
            _habitat.EventFlagRaised -= OnHabitat; _npcs.NpcJoined -= OnNpc;
            _hubs.ProsperityChanged -= OnProsperity; _hubs.HubRuined -= OnRuined; _hubs.RunLost -= OnLost;
            _waypoints.EncounterStarted -= OnEncounterStarted; _waypoints.EncounterEnded -= OnEncounterEnded;
            _facilities.FacilityBuilt -= OnBuilt;
        }
    }
}
