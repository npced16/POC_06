using System;

using IronRing.Contracts;

namespace IronRing.Combat
{
    /// <summary>Story 033. Real-time raid that decides a raid-mode encounter; WaypointResolver hands it the encounter and gets the outcome back.</summary>
    public interface IRaidRunner
    {
        /// <summary>True when the waypoint is still occupied (a raid happens there).</summary>
        bool IsOccupied(ITrackNode node);
        /// <summary>Starts the raid at node; the runner later calls WaypointResolver.CompleteRaid exactly once.</summary>
        void Begin(ITrackNode node);
    }

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
        private IRaidRunner _raid;
        /// <summary>Story 033: true while the active encounter is a real-time raid (no dice rounds, Advance is ignored).</summary>
        public bool IsRaid { get; private set; }
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
            Combat = null; EventTitle = null; LastOutcome = null; IsRaid = false;
            IsEncounterActive = true;
            _track.RequestHold(HOLD_REASON);
            if (_data.RaidMode && _raid != null && _raid.IsOccupied(node))
            {
                // Story 033: occupied waypoint -> the raid decides Victory/Defeat through CompleteRaid; dice combat and the event roll are skipped.
                IsRaid = true;
                EncounterStarted?.Invoke(node);
                _raid.Begin(node);
                return;
            }
            if (_data.RaidMode && _raid != null)
            {
                // Story 033: a liberated waypoint has no more raids; it always gives a trackside event.
                _selectedEvent = _data.Events[_random.NextInt(0, _data.Events.Length)];
                EventTitle = _selectedEvent.Title;
            }
            else if (_random.NextFloat() < _data.EventRatio)
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
            if (!IsEncounterActive || IsRaid) return;
            if (Combat == null)
            {
                ChangeResource(_selectedEvent.Resource, _selectedEvent.Delta);
                if (_data.EventProsperity != 0f) _hubs.ApplyDelta(_nextHub, _data.EventProsperity, HOLD_REASON);
                Finish(EncounterOutcome.EventResolved);
                return;
            }
            Combat.AdvanceRound();
            if (!Combat.IsComplete) return;
            ApplyCombatResult(Combat.Outcome);
            Finish(Combat.Outcome);
        }

        /// <summary>Story 033: attaches the raid runner used when the encounter data has _raidMode (null detaches it).</summary>
        public void AttachRaid(IRaidRunner raid) => _raid = raid;

        /// <summary>Story 033: the raid runner reports the raid outcome; applies the same rewards/penalties as dice combat and releases the hold.</summary>
        public void CompleteRaid(bool victory)
        {
            if (!IsEncounterActive || !IsRaid) return;
            EncounterOutcome outcome = victory ? EncounterOutcome.Victory : EncounterOutcome.Defeat;
            ApplyCombatResult(outcome);
            IsRaid = false;
            Finish(outcome);
        }

        /// <summary>Applies the combat reward (victory) or penalty (defeat) to the inventory and the next hub's prosperity.</summary>
        private void ApplyCombatResult(EncounterOutcome result)
        {
            if (result == EncounterOutcome.Victory)
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
