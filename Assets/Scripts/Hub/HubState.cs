using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Hub
{
    /// <summary>
    /// One permanent track hub's prosperity and development phase (Story 014, design/game-brief.md MVP feature 6).
    /// Transition table (alive = prosperity above RuinThreshold; settles = prosperity at/above SettledThreshold or habitat complete):
    ///   Ruined     --alive-->              Recovering
    ///   Recovering --not alive-->          Ruined
    ///   Recovering --alive and settles-->  Settled
    ///   Settled    --not alive-->          Collapsed
    ///   Collapsed  terminal in the MVP; ignores every prosperity change
    /// Every other (phase, condition) pair keeps the current phase. Settled below the threshold stays Settled until 0.
    /// </summary>
    public sealed class HubState
    {
        /// <summary>Immutable limits and growth profile for this hub.</summary>
        public HubDefinition Definition { get; }
        /// <summary>Current clamped prosperity.</summary>
        public float Prosperity { get; private set; }
        /// <summary>Current development phase.</summary>
        public HubPhase Phase { get; private set; }
        /// <summary>True once the hub has reached Settled during this run.</summary>
        public bool HasEverSettled { get; private set; }
        /// <summary>Ruined or Collapsed; consumed by Stories 005, 006, 010 and 011 through IHubStateSystem.IsRuined.</summary>
        public bool IsRuined => Phase == HubPhase.Ruined || Phase == HubPhase.Collapsed;

        /// <summary>Uses a validated definition to initialize prosperity and the starting phase from data.</summary>
        public HubState(HubDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Prosperity = definition.Initial;
            if (Prosperity <= definition.RuinThreshold) Phase = HubPhase.Ruined;
            else if (Prosperity >= definition.SettledThreshold) { Phase = HubPhase.Settled; HasEverSettled = true; }
            else Phase = HubPhase.Recovering;
        }

        /// <summary>Clamps delta to the data bounds and returns whether prosperity changed; Collapsed ignores all changes and Ruined ignores decreases.</summary>
        internal bool ApplyDelta(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            if (Phase == HubPhase.Collapsed) return false;
            if (Phase == HubPhase.Ruined && delta < 0) return false;
            float previous = Prosperity;
            Prosperity = (float)Math.Max(Definition.Minimum, Math.Min(Definition.Maximum, (double)Prosperity + delta));
            return previous != Prosperity;
        }

        /// <summary>Follows the transition table until stable and appends each entered phase to transitions in order.</summary>
        internal void Evaluate(bool habitatComplete, List<HubPhase> transitions)
        {
            bool alive = Prosperity > Definition.RuinThreshold;
            bool settles = Prosperity >= Definition.SettledThreshold || habitatComplete;
            while (true)
            {
                HubPhase next = Next(Phase, alive, settles);
                if (next == Phase) return;
                Phase = next;
                if (next == HubPhase.Settled) HasEverSettled = true;
                transitions.Add(next);
            }
        }

        /// <summary>Explicit transition table; returns the phase that follows current for the given conditions.</summary>
        private static HubPhase Next(HubPhase current, bool alive, bool settles)
        {
            switch (current)
            {
                case HubPhase.Ruined: return alive ? HubPhase.Recovering : HubPhase.Ruined;
                case HubPhase.Recovering: return !alive ? HubPhase.Ruined : settles ? HubPhase.Settled : HubPhase.Recovering;
                case HubPhase.Settled: return alive ? HubPhase.Settled : HubPhase.Collapsed;
                default: return HubPhase.Collapsed;
            }
        }
    }
}
