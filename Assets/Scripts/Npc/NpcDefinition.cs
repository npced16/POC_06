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
        /// <summary>Hub ids at which this NPC can join (Story 023: NpcPool.json `_hubIds`, hidden from the player).</summary>
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
        /// <summary>Story 023: train-passenger data (origin, chance, conditions, proposal, hint). Null for legacy entries built in code.</summary>
        public NpcPassengerProfile Passenger { get; }
        /// <summary>Story 030: habitat type id this NPC teaches when it joins a hub (NpcPool.json `_teachesTypeId`); null = none.</summary>
        public string TeachesTypeId { get; }
        /// <summary>Job effect applied in the NPC's home town: income, housing, or light.</summary>
        public string WorkKind { get; }
        /// <summary>Amount of the job effect applied while this NPC lives in a town.</summary>
        public int WorkBonus { get; }
        /// <summary>마을에서 말풍선으로 띄우는 혼잣말(NpcPool.json `_mutterLines`, 빈 줄 제외). 없으면 빈 목록이다.</summary>
        public IReadOnlyList<string> MutterLines { get; }

        /// <summary>
        /// NPC의 신원, 대사, 방문 조건, 직무 종류와 효과를 검증해 불변 정의로 복사한다.
        /// 반환값은 없으며 공개 프로퍼티에 검증된 NPC 데이터를 저장한다.
        /// </summary>
        public NpcDefinition(string id, string name, string quote, IEnumerable<string> eligibleHubIds, bool requiresCompleteHabitat, string habitatTypeId = null,
            IEnumerable<string> lines = null, NpcPassengerProfile passenger = null, string teachesTypeId = null,
            string workKind = null, int workBonus = 0, IEnumerable<string> mutterLines = null)
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
            Passenger = passenger;
            TeachesTypeId = string.IsNullOrWhiteSpace(teachesTypeId) ? null : teachesTypeId;
            if (workKind != null && workKind != "income" && workKind != "housing" && workKind != "light")
                throw new ArgumentException("Unknown NPC work kind.", nameof(workKind));
            if (workBonus < 0) throw new ArgumentOutOfRangeException(nameof(workBonus));
            WorkKind = workKind;
            WorkBonus = workBonus;
            var mutters = new List<string>();
            if (mutterLines != null)
                foreach (string line in mutterLines)
                    if (!string.IsNullOrWhiteSpace(line)) mutters.Add(line);
            MutterLines = mutters.AsReadOnly();
        }

        /// <summary>True when hubId is one of this NPC's hubs (an empty hub list means any hub).</summary>
        public bool AllowsHub(string hubId)
        {
            if (hubId == null) return false;
            if (EligibleHubIds.Count == 0) return true;
            foreach (string hub in EligibleHubIds) if (hub == hubId) return true;
            return false;
        }
    }

    /// <summary>Story 023: one hidden condition (`{ "_type", "_value" }`). Evaluated by NpcConditions.</summary>
    public sealed class NpcCondition
    {
        /// <summary>prosperityAtLeast | lapAtLeast | visitAtLeast | habitatComplete | npcPresent | phaseAtLeast.</summary>
        public string Type { get; }
        /// <summary>Number, habitat type id, NPC id or phase name depending on Type.</summary>
        public string Value { get; }

        /// <summary>Stores the condition as authored; unknown types are reported at evaluation time.</summary>
        public NpcCondition(string type, string value) { Type = type ?? ""; Value = value ?? ""; }

        /// <inheritdoc/>
        public override string ToString() => Type + "≥" + Value;
    }

    /// <summary>Story 023: how an NPC rides the train — where it boards, its chance, conditions and what it says.</summary>
    public sealed class NpcPassengerProfile
    {
        /// <summary>Waypoint node id where the NPC can board, or "*" for any waypoint.</summary>
        public string Origin { get; }
        /// <summary>Korean origin name shown to the player ("북쪽 고개").</summary>
        public string OriginName { get; }
        /// <summary>Boarding chance per resolved waypoint, 0..1.</summary>
        public float Chance { get; }
        /// <summary>Conditions checked at boarding (all must hold).</summary>
        public IReadOnlyList<NpcCondition> BoardConditions { get; }
        /// <summary>Hidden conditions checked at a hub before disembarking (all must hold). Never shown to the player.</summary>
        public IReadOnlyList<NpcCondition> Conditions { get; }
        /// <summary>Proposal line announced when boarding.</summary>
        public string Proposal { get; }
        /// <summary>Hint line about the hidden conditions, announced when boarding.</summary>
        public string Hint { get; }

        /// <summary>Validates the chance and copies the lists.</summary>
        public NpcPassengerProfile(string origin, string originName, float chance, IEnumerable<NpcCondition> boardConditions,
            IEnumerable<NpcCondition> conditions, string proposal, string hint)
        {
            if (float.IsNaN(chance) || chance < 0f || chance > 1f) throw new ArgumentOutOfRangeException(nameof(chance));
            Origin = string.IsNullOrWhiteSpace(origin) ? "*" : origin;
            OriginName = originName ?? "";
            Chance = chance;
            BoardConditions = new List<NpcCondition>(boardConditions ?? Array.Empty<NpcCondition>()).AsReadOnly();
            Conditions = new List<NpcCondition>(conditions ?? Array.Empty<NpcCondition>()).AsReadOnly();
            Proposal = proposal ?? "";
            Hint = hint ?? "";
        }
    }
}
