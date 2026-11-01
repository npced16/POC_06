using System;

using IronRing.Contracts;

namespace IronRing.Combat
{
    /// <summary>
    /// Story 005 / 039. Rest-stop events. Story 039 removed every combat path (dice combat, raid, en-route raid): an arrival at a
    /// rest stop holds the track and picks exactly one event from that stop's weight table (EncounterData, seeded random);
    /// Advance applies its effect and releases the hold with EncounterOutcome.EventResolved.
    /// </summary>
    public sealed class WaypointResolver : IWaypointResolver, IDisposable
    {
        private const string HOLD_REASON = "waypoint-encounter";
        private readonly EncounterData _data;
        private readonly ITrackRuntime _track;
        private readonly IResourceInventory _inventory;
        private readonly IHubStateSystem _hubs;
        private readonly IRandomSource _random;
        private ITrackNode _node;
        private string _nextHub;
        private EncounterEvent _selectedEvent;
        private bool _disposed;
        public bool IsEncounterActive { get; private set; }
        /// <summary>Title of the active (or last) event; maps to a string key through MainSettings _eventKeys.</summary>
        public string EventTitle { get; private set; }
        /// <summary>Id of the active (or last) event.</summary>
        public string EventId => _selectedEvent?.Id;
        public EncounterOutcome? LastOutcome { get; private set; }
        /// <summary>Story 039: boarding bonus of the event that just resolved (cleared on the next arrival).</summary>
        public float BoardChanceBonus { get; private set; }
        /// <summary>Story 039: codex unlocks granted by the event that just resolved (cleared on the next arrival).</summary>
        public int CodexUnlocks { get; private set; }
        /// <summary>Story 039: extra seconds the active event holds the train (track repair, "delay" effect).</summary>
        public float ExtraHoldSeconds => IsEncounterActive && _selectedEvent.Effect == EncounterData.EFFECT_DELAY ? Math.Max(0f, _selectedEvent.Amount) : 0f;
        public event Action<ITrackNode> EncounterStarted;
        public event Action<ITrackNode, EncounterOutcome> EncounterEnded;

        /// <summary>Injects the event table and system interfaces; arrival wiring stays with the composition root.</summary>
        public WaypointResolver(EncounterData data, ITrackRuntime track, IResourceInventory inventory, IHubStateSystem hubs, IRandomSource random)
        {
            data.Validate();
            _data = data; _track = track; _inventory = inventory; _hubs = hubs; _random = random;
        }

        /// <summary>Holds movement at an arrived rest stop and selects one event from its weight table; ignores hubs and active events.</summary>
        public void OnWaypointArrived(ITrackNode node)
        {
            if (_disposed || IsEncounterActive || node.Kind != TrackNodeKind.Waypoint) return;
            _nextHub = FindNextHub(node);
            _node = node;
            LastOutcome = null; BoardChanceBonus = 0f; CodexUnlocks = 0;
            _selectedEvent = Pick(node.Id);
            EventTitle = _selectedEvent.Title;
            IsEncounterActive = true;
            _track.RequestHold(HOLD_REASON);
            UnityEngine.Debug.Log($"EVENT: {node.Id} {_selectedEvent.Id}");
            EncounterStarted?.Invoke(node);
        }

        /// <summary>Applies the selected event's effect and releases the hold.</summary>
        public void Advance()
        {
            if (!IsEncounterActive) return;
            EncounterEvent e = _selectedEvent;
            switch (e.Effect)
            {
                case EncounterData.EFFECT_PROSPERITY:
                    if (e.Amount != 0f) _hubs.ApplyDelta(_nextHub, e.Amount, HOLD_REASON + ":" + e.Id);
                    break;
                case EncounterData.EFFECT_CODEX:
                    CodexUnlocks = Math.Max(0, (int)Math.Round(e.Amount));
                    break;
                case EncounterData.EFFECT_BOARDING:
                    BoardChanceBonus = Math.Max(0f, e.Amount);
                    break;
                case EncounterData.EFFECT_RESOURCE:
                    ChangeResource(e.Resource, e.Delta);
                    break;
            }
            Finish();
        }

        /// <summary>Weighted pick from the stop's table (or every event at weight 1) with the seeded random.</summary>
        private EncounterEvent Pick(string stopId)
        {
            RestStopTable table = _data.TableFor(stopId);
            if (table == null) return _data.Events[_random.NextInt(0, _data.Events.Length)];
            int total = 0;
            foreach (WeightedEvent pick in table.Events) total += pick.Weight;
            int roll = _random.NextInt(0, total);
            foreach (WeightedEvent pick in table.Events)
            {
                if (roll < pick.Weight) return _data.Find(pick.EventId);
                roll -= pick.Weight;
            }
            return _data.Find(table.Events[table.Events.Length - 1].EventId);
        }

        /// <summary>Uses ring list order and the waypoint id to return the next hub, wrapping across the ring boundary.</summary>
        private string FindNextHub(ITrackNode node)
        {
            int start = -1;
            for (int i = 0; i < _track.Nodes.Count; i++) if (_track.Nodes[i].Id == node.Id) start = i;
            if (start < 0) throw new ArgumentException("Waypoint is not on this track.");
            for (int i = 1; i <= _track.Nodes.Count; i++)
            {
                ITrackNode next = _track.Nodes[(start + i) % _track.Nodes.Count];
                if (next.Kind == TrackNodeKind.Hub) return next.HubId;
            }
            throw new ArgumentException("Track needs a hub.");
        }

        /// <summary>Applies the signed resource delta, limiting losses to the available stock.</summary>
        private void ChangeResource(string resource, int delta)
        {
            if (string.IsNullOrEmpty(resource)) return;
            if (delta >= 0) _inventory.Add(resource, delta);
            else _inventory.TrySpend(resource, (int)Math.Min(_inventory.Get(resource), -(long)delta));
        }

        /// <summary>Records the outcome, releases only this hold and publishes the ended node.</summary>
        private void Finish()
        {
            LastOutcome = EncounterOutcome.EventResolved;
            IsEncounterActive = false;
            _track.ReleaseHold(HOLD_REASON);
            EncounterEnded?.Invoke(_node, EncounterOutcome.EventResolved);
        }

        /// <summary>Releases an unfinished event hold without granting results and prevents future events.</summary>
        public void Dispose()
        {
            if (IsEncounterActive) _track.ReleaseHold(HOLD_REASON);
            IsEncounterActive = false;
            _disposed = true;
        }
    }
}
