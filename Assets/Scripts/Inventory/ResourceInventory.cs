using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using IronRing.Contracts;

namespace IronRing.Inventory
{
    /// <summary>Pure C# inventory with defensive snapshots and exact integer transfers.</summary>
    public sealed class ResourceInventory : IResourceInventory
    {
        private readonly Dictionary<string, int> _stock;

        /// <summary>Emits resource id and resulting quantity after a successful nonzero transfer.</summary>
        public event Action<string, int> Changed;

        /// <summary>Copies startingStock into owned inventory state; rejects invalid ids and negative quantities.</summary>
        public ResourceInventory(IReadOnlyDictionary<string, int> startingStock)
        {
            if (startingStock == null) throw new ArgumentNullException(nameof(startingStock));
            _stock = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in startingStock)
            {
                Validate(entry.Key, entry.Value);
                _stock.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>Reads resourceId and returns its available amount, or zero for an unknown id.</summary>
        public int Get(string resourceId)
        {
            return resourceId != null && _stock.TryGetValue(resourceId, out int value) ? value : 0;
        }

        /// <summary>Adds non-negative amount to resourceId and emits Changed with the resulting stock.</summary>
        public void Add(string resourceId, int amount)
        {
            Validate(resourceId, amount);
            if (amount == 0) return;
            int value = checked(Get(resourceId) + amount);
            _stock[resourceId] = value;
            Changed?.Invoke(resourceId, value);
        }

        /// <summary>Spends amount from resourceId when available; returns false without mutation if insufficient.</summary>
        public bool TrySpend(string resourceId, int amount)
        {
            Validate(resourceId, amount);
            int current = Get(resourceId);
            if (current < amount) return false;
            if (amount == 0) return true;
            _stock[resourceId] = current - amount;
            Changed?.Invoke(resourceId, current - amount);
            return true;
        }

        /// <summary>Returns a read-only copy of current resource ids and quantities, independent of future changes.</summary>
        public IReadOnlyDictionary<string, int> Snapshot()
        {
            return new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(_stock, StringComparer.Ordinal));
        }

        /// <summary>Validates resourceId and amount at the inventory boundary; throws for invalid input.</summary>
        private static void Validate(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("Resource id is required.", nameof(resourceId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        }
    }
}
