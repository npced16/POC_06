using System;

namespace IronRing.Hub
{
    /// <summary>Run-wide hub rules shared by every definition parsed from one data file (Story 014 deadline).</summary>
    public sealed class HubRunRules
    {
        /// <summary>Laps allowed before the first settlement; reaching 0 with no settled hub loses the run.</summary>
        public int DeadlineLaps { get; }
        /// <summary>Story 030: when true the run ends as Victory the moment every hub is Settled (HubProfiles.json `_earlyWin`).</summary>
        public bool EarlyWin { get; }
        /// <summary>Story 042: when true the town hub phase follows the town tier (TownTiers.json) and the run outcome follows the tier (HubProfiles.json `_useTownTiers`).</summary>
        public bool UseTownTiers { get; }
        /// <summary>Story 042/045: prosperity per lap for each resident of the town hub (HubProfiles.json `_perResident`).</summary>
        public float PerResident { get; }
        /// <summary>QA 2026-11-01 (mixed-cluster prosperity cap): a Cluster/Set building counts at most ceil(RequiredCount x ratio) cells for prosperity (HubProfiles.json `_cellCapRatio`, default 1.5).</summary>
        public float CellCapRatio { get; }

        /// <summary>Validates and stores the data-driven deadline in laps (at least 1) and the Story 030 early-win switch.</summary>
        public HubRunRules(int deadlineLaps, bool earlyWin = false, bool useTownTiers = false, float perResident = 0f, float cellCapRatio = 1.5f)
        {
            CellCapRatio = cellCapRatio > 0f ? cellCapRatio : 1.5f;
            UseTownTiers = useTownTiers;
            PerResident = perResident;
            if (deadlineLaps < 1) throw new ArgumentOutOfRangeException(nameof(deadlineLaps), "Deadline must be at least one lap.");
            DeadlineLaps = deadlineLaps;
            EarlyWin = earlyWin;
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
        /// <summary>Base lap contribution after the profile decay multiplier. Applied in every phase but Collapsed (Story 028: ruins too).</summary>
        public float BasePerLap { get; }
        /// <summary>Contribution per NPC after the profile NPC multiplier.</summary>
        public float PerNpc { get; }
        /// <summary>Story 014: lap contribution per item placed in the hub interior.</summary>
        public float PerPlacedItem { get; }
        /// <summary>Story 028: lap contribution per interior cell that belongs to a completed habitat cluster.</summary>
        public float PerHabitatCell { get; }
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
            float basePerLap, float perNpc)
            : this(id, minimum, maximum, initial, ruinThreshold, basePerLap, perNpc, 0f, 0f, maximum, null) { }

        /// <summary>Validates the supplied id, bounds, growth coefficients and phase threshold and stores an immutable definition.</summary>
        public HubDefinition(string id, float minimum, float maximum, float initial, float ruinThreshold,
            float basePerLap, float perNpc, float perPlacedItem, float habitatBonus,
            float settledThreshold, HubRunRules rules, bool settleByHabitat = false, float perHabitatCell = 0f)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Hub id is required.", nameof(id));
            float[] values = { minimum, maximum, initial, ruinThreshold, basePerLap, perNpc, perPlacedItem, habitatBonus, settledThreshold, perHabitatCell };
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
            PerPlacedItem = perPlacedItem;
            HabitatBonus = habitatBonus;
            SettledThreshold = settledThreshold;
            Rules = rules;
            SettleByHabitat = settleByHabitat;
            PerHabitatCell = perHabitatCell;
        }
    }
}
