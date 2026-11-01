using System;

namespace IronRing.Hub
{
    /// <summary>Run-wide hub rules shared by every definition parsed from one data file (Story 014 deadline).</summary>
    public sealed class HubRunRules
    {
        /// <summary>Laps allowed before the first settlement; reaching 0 with no settled hub loses the run.</summary>
        public int DeadlineLaps { get; }

        /// <summary>Validates and stores the data-driven deadline in laps (at least 1).</summary>
        public HubRunRules(int deadlineLaps)
        {
            if (deadlineLaps < 1) throw new ArgumentOutOfRangeException(nameof(deadlineLaps), "Deadline must be at least one lap.");
            DeadlineLaps = deadlineLaps;
        }
    }

    /// <summary>Immutable hub limits and growth coefficients supplied by game data (Stories 004 and 014).</summary>
    public sealed class HubDefinition
    {
        /// <summary>Stable identity shared with track data.</summary>
        public string Id { get; }
        /// <summary>Lowest allowed prosperity.</summary>
        public float Minimum { get; }
        /// <summary>Highest allowed prosperity.</summary>
        public float Maximum { get; }
        /// <summary>Prosperity when the run starts.</summary>
        public float Initial { get; }
        /// <summary>Prosperity at or below which a hub is Ruined (never settled) or Collapsed (settled before).</summary>
        public float RuinThreshold { get; }
        /// <summary>Base lap contribution after the profile decay multiplier. Not applied while Ruined.</summary>
        public float BasePerLap { get; }
        /// <summary>Contribution per NPC after the profile NPC multiplier.</summary>
        public float PerNpc { get; }
        /// <summary>Contribution per facility after the profile facility multiplier.</summary>
        public float PerFacility { get; }
        /// <summary>Story 014: lap contribution per item placed in the hub interior.</summary>
        public float PerPlacedItem { get; }
        /// <summary>Story 014: lap contribution while the hub habitat is complete.</summary>
        public float HabitatBonus { get; }
        /// <summary>Story 014: prosperity at or above which a recovering hub settles.</summary>
        public float SettledThreshold { get; }
        /// <summary>Story 023: true lets one completed habitat settle the hub; false (default) settles by prosperity only.</summary>
        public bool SettleByHabitat { get; }
        /// <summary>Story 014: shared run rules, or null when the deadline is disabled (legacy data and tests).</summary>
        public HubRunRules Rules { get; }

        /// <summary>Story 004 overload: no item/habitat contribution, settles only at Maximum, no deadline.</summary>
        public HubDefinition(string id, float minimum, float maximum, float initial, float ruinThreshold,
            float basePerLap, float perNpc, float perFacility)
            : this(id, minimum, maximum, initial, ruinThreshold, basePerLap, perNpc, perFacility, 0f, 0f, maximum, null) { }

        /// <summary>Validates the supplied id, bounds, growth coefficients and phase threshold and stores an immutable definition.</summary>
        public HubDefinition(string id, float minimum, float maximum, float initial, float ruinThreshold,
            float basePerLap, float perNpc, float perFacility, float perPlacedItem, float habitatBonus,
            float settledThreshold, HubRunRules rules, bool settleByHabitat = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Hub id is required.", nameof(id));
            float[] values = { minimum, maximum, initial, ruinThreshold, basePerLap, perNpc, perFacility, perPlacedItem, habitatBonus, settledThreshold };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Hub values must be finite.");
            if (minimum > ruinThreshold || ruinThreshold >= maximum || initial < minimum || initial > maximum)
                throw new ArgumentException("Invalid hub bounds, threshold or initial prosperity.");
            if (settledThreshold <= ruinThreshold || settledThreshold > maximum)
                throw new ArgumentException("Settled threshold must lie above the ruin threshold and within the maximum.");
            Id = id;
            Minimum = minimum;
            Maximum = maximum;
            Initial = initial;
            RuinThreshold = ruinThreshold;
            BasePerLap = basePerLap;
            PerNpc = perNpc;
            PerFacility = perFacility;
            PerPlacedItem = perPlacedItem;
            HabitatBonus = habitatBonus;
            SettledThreshold = settledThreshold;
            Rules = rules;
            SettleByHabitat = settleByHabitat;
        }
    }
}
