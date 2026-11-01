using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Hub
{
    /// <summary>
    /// Pure C# hub simulation (Stories 004 and 014): prosperity, development phases, deadline and run loss.
    /// Composition wires track LapCompleted to OnLapCompleted; Story 014's installer attaches item/habitat sources.
    /// Per lap per hub (Collapsed skipped): (Ruined ? 0 : BasePerLap) + PerPlacedItem * items
    ///   + Σ completed habitat clusters (catalog bonus, or HabitatBonus when the catalog gives none; Story 016)
    ///   + PerNpc * npcs + PerFacility * facilities. Settlement is judged before the deadline in the same lap.
    /// </summary>
    public sealed class RunState : IHubStateSystem, IHubDevelopment, IRunLossInfo
    {
        private readonly Dictionary<string, HubState> _states = new Dictionary<string, HubState>();
        private readonly INpcRecruiter _npcs;
        private readonly IFacilityBuilder _facilities;
        private readonly HubRunRules _rules;
        private IProsperitySources _sources = NullProsperitySources.Instance;
        private int _lastLap;

        /// <summary>Permanent read-only hub identities in data order, including ruins.</summary>
        public IReadOnlyList<string> HubIds { get; }
        /// <summary>True once every ever-settled hub collapsed, or the deadline passed with no settlement.</summary>
        public bool IsRunLost { get; private set; }
        /// <summary>IRunLossInfo: why the run ended (contract enum); None while alive.</summary>
        public RunLossReason LastLossReason { get; private set; }
        /// <summary>Laps left before the first-settlement deadline; int.MaxValue when the data defines no deadline. Never below 0.</summary>
        public int LapsUntilDeadline { get; private set; }
        /// <summary>Highest lap number already applied; scripted demos tick LastLap + 1.</summary>
        public int LastLap => _lastLap;
        /// <summary>Reports hub identity and new prosperity after a value change.</summary>
        public event Action<string, float> ProsperityChanged;
        /// <summary>Reports a hub identity whenever it enters Ruined (from Recovering) or Collapsed.</summary>
        public event Action<string> HubRuined;
        /// <summary>Reports the run loss once per run.</summary>
        public event Action RunLost;
        /// <summary>hubId and the phase it just entered, once per transition in order.</summary>
        public event Action<string, HubPhase> PhaseChanged;

        /// <summary>Copies hub definitions and injects NPC/facility interfaces; rejects empty or duplicate hub data.</summary>
        public RunState(IEnumerable<HubDefinition> definitions, INpcRecruiter npcs, IFacilityBuilder facilities)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            _npcs = npcs ?? throw new ArgumentNullException(nameof(npcs));
            _facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
            var ids = new List<string>();
            foreach (HubDefinition definition in definitions)
            {
                _states.Add(definition.Id, new HubState(definition));
                ids.Add(definition.Id);
                if (_rules == null) _rules = definition.Rules;
            }
            if (ids.Count == 0) throw new ArgumentException("A run requires at least one hub.", nameof(definitions));
            HubIds = ids.AsReadOnly();
            LapsUntilDeadline = _rules?.DeadlineLaps ?? int.MaxValue;
        }

        /// <summary>Attaches placed-item and habitat readers (Story 014 installer); until then both read as 0/false.</summary>
        public void AttachSources(IProsperitySources sources)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        }

        /// <summary>Looks up hubId and returns its current prosperity; unknown ids throw.</summary>
        public float GetProsperity(string hubId) => _states[hubId].Prosperity;

        /// <summary>True when hubId is Ruined or Collapsed; unknown ids throw.</summary>
        public bool IsRuined(string hubId) => _states[hubId].IsRuined;

        /// <summary>Current development phase of hubId; unknown ids throw.</summary>
        public HubPhase GetPhase(string hubId) => _states[hubId].Phase;

        /// <summary>True when hubId has reached Settled at least once; unknown ids throw.</summary>
        public bool HasEverSettled(string hubId) => _states[hubId].HasEverSettled;

        /// <summary>Applies delta to hubId (Collapsed ignores it, Ruined ignores decreases), then re-evaluates its phase.</summary>
        public void ApplyDelta(string hubId, float delta, string reason)
        {
            HubState state = _states[hubId];
            if (state.ApplyDelta(delta)) ProsperityChanged?.Invoke(hubId, state.Prosperity);
            Evaluate(hubId, state);
        }

        /// <summary>Re-evaluates hubId's phase without changing prosperity (for example after its habitat completed).</summary>
        public void RefreshPhase(string hubId) => Evaluate(hubId, _states[hubId]);

        /// <summary>For a new lap number, applies each hub's lap rule, then counts the deadline down; duplicate or old laps do nothing.</summary>
        public void OnLapCompleted(int lap)
        {
            if (lap <= _lastLap || IsRunLost) return;
            _lastLap = lap;
            foreach (string id in HubIds)
            {
                HubState state = _states[id];
                if (state.Phase == HubPhase.Collapsed) continue;
                HubDefinition data = state.Definition;
                float delta = (state.Phase == HubPhase.Ruined ? 0f : data.BasePerLap)
                    + data.PerPlacedItem * _sources.PlacedItemCount(id)
                    + _sources.HabitatBonus(id, data.HabitatBonus)
                    + data.PerNpc * _npcs.NpcsAt(id).Count
                    + data.PerFacility * _facilities.FacilitiesAt(id).Count;
                ApplyDelta(id, delta, "Lap completed");
                if (IsRunLost) return;
            }
            if (_rules == null) return;
            if (LapsUntilDeadline > 0) LapsUntilDeadline--;
            if (LapsUntilDeadline <= 0 && !AnyEverSettled()) Lose(RunLossReason.DeadlineExpired);
        }

        /// <summary>Runs hubId's transition table and raises PhaseChanged/HubRuined per transition, then checks the collapse loss.</summary>
        private void Evaluate(string hubId, HubState state)
        {
            var transitions = new List<HubPhase>(2);
            state.Evaluate(_sources.IsHabitatComplete(hubId), transitions);
            bool collapsed = false;
            foreach (HubPhase phase in transitions)
            {
                PhaseChanged?.Invoke(hubId, phase);
                if (phase == HubPhase.Ruined || phase == HubPhase.Collapsed) HubRuined?.Invoke(hubId);
                collapsed |= phase == HubPhase.Collapsed;
            }
            if (collapsed && !IsRunLost && AllSettledCollapsed()) Lose(RunLossReason.AllSettledCollapsed);
        }

        /// <summary>True when at least one hub ever settled and every such hub is Collapsed.</summary>
        private bool AllSettledCollapsed()
        {
            bool any = false;
            foreach (HubState state in _states.Values)
            {
                if (!state.HasEverSettled) continue;
                if (state.Phase != HubPhase.Collapsed) return false;
                any = true;
            }
            return any;
        }

        /// <summary>True when any hub has ever settled.</summary>
        private bool AnyEverSettled()
        {
            foreach (HubState state in _states.Values) if (state.HasEverSettled) return true;
            return false;
        }

        /// <summary>Latches the loss with reason and raises RunLost once.</summary>
        private void Lose(RunLossReason reason)
        {
            if (IsRunLost) return;
            IsRunLost = true;
            LastLossReason = reason;
            RunLost?.Invoke();
        }
    }
}
