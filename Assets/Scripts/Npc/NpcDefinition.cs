using System;
using System.Collections.Generic;

namespace IronRing.Npc
{
    /// <summary>Immutable NPC identity, presentation and habitat eligibility read from external data.</summary>
    public sealed class NpcDefinition
    {
        /// <summary>Stable globally unique NPC id.</summary>
        public string Id { get; }
        /// <summary>Player-visible NPC name.</summary>
        public string Name { get; }
        /// <summary>The single arrival line shown to the player.</summary>
        public string Quote { get; }
        /// <summary>Hub ids at which this NPC can join.</summary>
        public IReadOnlyList<string> EligibleHubIds { get; }
        /// <summary>Whether the target hub must currently have a complete habitat.</summary>
        public bool RequiresCompleteHabitat { get; }
        /// <summary>
        /// Story 016: habitat type id (Habitats catalog) that must be complete at the hub for this NPC to be a candidate;
        /// null keeps the Story 003 rule. With a type id, an empty EligibleHubIds means "any hub".
        /// </summary>
        public string HabitatTypeId { get; }
        /// <summary>
        /// Story 017: dialogue lines cycled by INpcInteraction. The quote is always the first line, followed by the data's extra
        /// lines (NpcPool.json `_lines`, blanks and duplicates of the quote skipped). Never empty.
        /// </summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>Validates and copies identity, display text and allowed hubs into an immutable definition.</summary>
        public NpcDefinition(string id, string name, string quote, IEnumerable<string> eligibleHubIds, bool requiresCompleteHabitat, string habitatTypeId = null,
            IEnumerable<string> lines = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(quote))
                throw new ArgumentException("NPC id, name and quote must be non-empty.");
            if (eligibleHubIds == null) throw new ArgumentNullException(nameof(eligibleHubIds));
            var hubs = new List<string>(eligibleHubIds);
            if (hubs.Exists(string.IsNullOrWhiteSpace)) throw new ArgumentException("Eligible hub ids must be non-empty.");
            Id = id;
            Name = name;
            Quote = quote;
            EligibleHubIds = hubs.AsReadOnly();
            RequiresCompleteHabitat = requiresCompleteHabitat;
            HabitatTypeId = string.IsNullOrWhiteSpace(habitatTypeId) ? null : habitatTypeId;
            var allLines = new List<string> { quote };
            if (lines != null)
                foreach (string line in lines)
                    if (!string.IsNullOrWhiteSpace(line) && !allLines.Contains(line)) allLines.Add(line);
            Lines = allLines.AsReadOnly();
        }
    }
}
