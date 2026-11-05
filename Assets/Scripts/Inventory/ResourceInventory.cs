using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using IronRing.Contracts;

namespace IronRing.Inventory
{
    /// <summary>Pure C# inventory with defensive snapshots and exact integer transfers.</summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class ResourceInventory : IResourceInventory, ISaveParticipant
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

        // 스펙 003: 저장 슬롯용 자원 재고 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "resources";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 20;

        /// <summary>자원 재고 _stock 가운데 0이 아닌 것을 id 순으로 정렬해 JSON으로 돌려준다. 상태를 바꾸지 않는다.</summary>
        public string Capture()
        {
            List<string> ids = new List<string>();
            foreach (KeyValuePair<string, int> entry in _stock) if (entry.Value != 0) ids.Add(entry.Key);
            ids.Sort(StringComparer.Ordinal);
            CountSave state = new CountSave { Ids = ids.ToArray(), Counts = new int[ids.Count] };
            for (int i = 0; i < ids.Count; i++) state.Counts[i] = _stock[ids[i]];
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 _stock을 바꾼다. 저장에 없는 자원은 0으로 둔다.
        /// 바뀐 자원마다 Changed를 낸다.
        /// </summary>
        public void Restore(string json)
        {
            CountSave state = UnityEngine.JsonUtility.FromJson<CountSave>(json);
            Dictionary<string, int> next = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < state.Ids.Length && i < state.Counts.Length; i++) next[state.Ids[i]] = state.Counts[i];
            foreach (string id in new List<string>(_stock.Keys)) if (!next.ContainsKey(id)) next[id] = 0;
            foreach (KeyValuePair<string, int> entry in next)
            {
                int before = Get(entry.Key);
                _stock[entry.Key] = entry.Value;
                if (before != entry.Value) Changed?.Invoke(entry.Key, entry.Value);
            }
        }

        /// <summary>스펙 003. 저장 형식: id와 수량의 짝(같은 순번).</summary>
        [Serializable]
        private sealed class CountSave
        {
            public string[] Ids = Array.Empty<string>();
            public int[] Counts = Array.Empty<int>();
        }
    }
}
