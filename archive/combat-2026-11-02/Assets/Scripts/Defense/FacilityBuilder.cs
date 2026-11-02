using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Defense
{
    /// <summary>Builds facilities at docked hubs using only injected system contracts.</summary>
    public sealed class FacilityBuilder : IFacilityBuilder
    {
        private readonly Dictionary<string, FacilityDefinition> _definitions = new Dictionary<string, FacilityDefinition>();
        private readonly Dictionary<string, List<string>> _built = new Dictionary<string, List<string>>();
        private readonly IResourceInventory _inventory;
        private readonly IHubStateSystem _hubs;
        private readonly Func<string, bool> _isDocked;
        private readonly int _limit;
        private readonly bool _allowBuildInRuins;
        private bool _building;

        /// <summary>Publishes hub id and facility id after successful construction and effects.</summary>
        public event Action<string, string> FacilityBuilt;

        /// <summary>Uses definitions, rules and injected inventory, hubs and dock predicate to initialize empty construction state.</summary>
        public FacilityBuilder(IEnumerable<FacilityDefinition> definitions, int limit, bool allowBuildInRuins,
            IResourceInventory inventory, IHubStateSystem hubs, Func<string, bool> isDocked)
        {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _hubs = hubs ?? throw new ArgumentNullException(nameof(hubs));
            _isDocked = isDocked ?? throw new ArgumentNullException(nameof(isDocked));
            _limit = limit;
            _allowBuildInRuins = allowBuildInRuins;
            foreach (var definition in definitions) _definitions.Add(definition.Id, definition);
            foreach (string hub in hubs.HubIds) _built.Add(hub, new List<string>());
        }

        /// <summary>Returns a read-only snapshot of facility ids for hubId, or an empty list for an unknown hub.</summary>
        public IReadOnlyList<string> FacilitiesAt(string hubId)
        {
            return hubId != null && _built.TryGetValue(hubId, out var list) ? Array.AsReadOnly(list.ToArray()) : Array.Empty<string>();
        }

        /// <summary>Checks hubId, facilityId, docking, ruin policy, capacity and complete cost without changing state.</summary>
        public bool CanBuild(string hubId, string facilityId)
        {
            if (_building || hubId == null || facilityId == null || !_built.TryGetValue(hubId, out var list)
                || !_definitions.TryGetValue(facilityId, out var definition) || !_isDocked(hubId)
                || list.Count >= _limit || (!_allowBuildInRuins && _hubs.IsRuined(hubId))) return false;
            foreach (var cost in definition.Costs)
                if (_inventory.Get(cost.Key) < cost.Value) return false;
            return true;
        }

        /// <summary>Spends the configured cost for hubId and facilityId, applies effects and emits one event; returns false on refusal.</summary>
        public bool TryBuild(string hubId, string facilityId)
        {
            if (!CanBuild(hubId, facilityId)) return false;
            _building = true;
            var definition = _definitions[facilityId];
            var spent = new List<KeyValuePair<string, int>>();
            try
            {
                foreach (var cost in definition.Costs)
                {
                    if (!_inventory.TrySpend(cost.Key, cost.Value))
                    {
                        foreach (var refund in spent) _inventory.Add(refund.Key, refund.Value);
                        return false;
                    }
                    spent.Add(cost);
                }
                _built[hubId].Add(facilityId);
                if (definition.ProsperityDelta != 0f) _hubs.ApplyDelta(hubId, definition.ProsperityDelta, "facility:" + facilityId);
            }
            finally { _building = false; }
            FacilityBuilt?.Invoke(hubId, facilityId);
            return true;
        }

        /// <summary>Sums configured combat effects at hubId and returns the bonus consumed by Story 005.</summary>
        public float CombatBonus(string hubId)
        {
            float bonus = 0f;
            foreach (string id in FacilitiesAt(hubId)) bonus += _definitions[id].CombatBonus;
            return bonus;
        }
    }
}
