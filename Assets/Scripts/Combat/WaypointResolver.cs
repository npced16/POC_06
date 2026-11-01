using System;

using IronRing.Contracts;

namespace IronRing.Combat
{
    public sealed class WaypointResolver : IWaypointResolver, IDisposable
    {
        private const string HOLD_REASON = "waypoint-encounter";
        private readonly EncounterData _data;
        private readonly ITrackRuntime _track;
        private readonly IResourceInventory _inventory;
        private readonly IHubStateSystem _hubs;
        private readonly IFacilityBuilder _facilities;
        private readonly IRandomSource _random;
        private ITrackNode _node;
        private string _nextHub;
        private EncounterEvent _selectedEvent;
        private bool _disposed;
        public bool IsEncounterActive { get; private set; }
        public CombatEncounter Combat { get; private set; }
        public string EventTitle { get; private set; }
        public EncounterOutcome? LastOutcome { get; private set; }
        public event Action<ITrackNode> EncounterStarted;
        public event Action<ITrackNode, EncounterOutcome> EncounterEnded;

        /// <summary>Injects encounter data and system interfaces; arrival wiring stays with the composition root.</summary>
        public WaypointResolver(EncounterData data, ITrackRuntime track, IResourceInventory inventory,
            IHubStateSystem hubs, IFacilityBuilder facilities, IRandomSource random)
        {
            data.Validate();
            _data = data; _track = track; _inventory = inventory; _hubs = hubs; _facilities = facilities; _random = random;
        }

        /// <summary>Uses an arrived waypoint to hold movement and select a data event or staged combat; ignores hubs and active encounters.</summary>
        public void OnWaypointArrived(ITrackNode node)
        {
            if (_disposed || IsEncounterActive || node.Kind != TrackNodeKind.Waypoint) return;
            _nextHub = FindNextHub(node);
            _node = node;
            Combat = null; EventTitle = null; LastOutcome = null;
            IsEncounterActive = true;
            _track.RequestHold(HOLD_REASON);
            if (_random.NextFloat() < _data.EventRatio)
            {
                _selectedEvent = _data.Events[_random.NextInt(0, _data.Events.Length)];
                EventTitle = _selectedEvent.Title;
            }
            else
                Combat = new CombatEncounter(_data, _facilities.CombatBonus(_nextHub), _random);
            EncounterStarted?.Invoke(node);
        }

        /// <summary>Resolves the selected event or advances one combat round, applies final rewards or penalties, and releases the encounter hold.</summary>
        public void Advance()
        {
            if (!IsEncounterActive) return;
            if (Combat == null)
            {
                ChangeResource(_selectedEvent.Resource, _selectedEvent.Delta);
                if (_data.EventProsperity != 0f) _hubs.ApplyDelta(_nextHub, _data.EventProsperity, HOLD_REASON);
                Finish(EncounterOutcome.EventResolved);
                return;
            }
            Combat.AdvanceRound();
            if (!Combat.IsComplete) return;
            if (Combat.Outcome == EncounterOutcome.Victory)
            {
                _inventory.Add(_data.RewardResource, _data.RewardAmount);
                // Story 016: encounters reward prosperity only (resource amounts are 0 in data).
                if (_data.VictoryProsperity != 0f) _hubs.ApplyDelta(_nextHub, _data.VictoryProsperity, HOLD_REASON);
            }
            else
            {
                string resource = _data.PenaltyResources[_random.NextInt(0, _data.PenaltyResources.Length)];
                ChangeResource(resource, -_data.PenaltyAmount);
                _hubs.ApplyDelta(_nextHub, _data.ProsperityPenalty, HOLD_REASON);
            }
            Finish(Combat.Outcome);
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
            if (delta >= 0) _inventory.Add(resource, delta);
            else _inventory.TrySpend(resource, (int)Math.Min(_inventory.Get(resource), -(long)delta));
        }

        /// <summary>Records the outcome, clears encounter state, releases only this hold and publishes the ended node.</summary>
        private void Finish(EncounterOutcome outcome)
        {
            LastOutcome = outcome;
            IsEncounterActive = false;
            _track.ReleaseHold(HOLD_REASON);
            EncounterEnded?.Invoke(_node, outcome);
        }

        /// <summary>Releases an unfinished encounter hold without granting results and prevents future encounters.</summary>
        public void Dispose()
        {
            if (IsEncounterActive) _track.ReleaseHold(HOLD_REASON);
            IsEncounterActive = false;
            _disposed = true;
        }
    }
}
