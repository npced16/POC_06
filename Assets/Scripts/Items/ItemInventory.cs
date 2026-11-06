using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using IronRing.Contracts;

namespace IronRing.Items
{
    /// <summary>
    /// 스토리 013. 아이템 수량과 자원 제작. 카탈로그에 없는 id는 거부한다.
    /// 제작은 비용 전체가 있을 때만 자원을 빼고 1개를 더하며, 모자라면 아무것도 바꾸지 않는다.
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class ItemInventory : IItemInventory, ISaveParticipant
    {
        private readonly ItemCatalog _catalog;
        private readonly IResourceInventory _resources;
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>itemId, 새 수량. 수량이 실제로 바뀐 뒤에만 발생한다.</summary>
        public event Action<string, int> Changed;

        /// <summary>카탈로그와 제작에 쓸 자원 재고를 주입받는다.</summary>
        public ItemInventory(ItemCatalog catalog, IResourceInventory resources)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        /// <summary>이 인벤토리가 따르는 카탈로그.</summary>
        public ItemCatalog Catalog => _catalog;

        /// <summary>보유 수량, 모르는 id는 0. 무제한 모드(스토리 016)에서는 카탈로그 아이템이면 UnlimitedCount 이상.</summary>
        public int Count(string itemId)
        {
            int stored = itemId != null && _counts.TryGetValue(itemId, out int n) ? n : 0;
            return _catalog.Unlimited && _catalog.Contains(itemId) ? Math.Max(stored, _catalog.UnlimitedCount) : stored;
        }

        /// <summary>카탈로그 아이템을 amount(0 이상)만큼 더한다. 무제한 모드에서는 수량을 바꾸지 않는다.</summary>
        public void Add(string itemId, int amount)
        {
            Validate(itemId, amount);
            if (amount == 0) return;
            if (_catalog.Unlimited) return; // 스펙 003: 무제한 모드는 꺼낼 때처럼 더해도 늘지 않게 해 저장 수량이 쌓이지 않도록 추가
            int value = checked(Count(itemId) + amount);
            _counts[itemId] = value;
            Changed?.Invoke(itemId, value);
        }

        /// <summary>amount만큼 있으면 빼고 true, 없으면 false이며 바꾸지 않는다.</summary>
        public bool TryRemove(string itemId, int amount)
        {
            Validate(itemId, amount);
            if (_catalog.Unlimited) return true; // 스토리 016: 무제한 모드는 꺼내도 줄지 않는다.
            int current = Count(itemId);
            if (current < amount) return false;
            if (amount == 0) return true;
            _counts[itemId] = current - amount;
            Changed?.Invoke(itemId, current - amount);
            return true;
        }

        /// <summary>현재 수량의 읽기 전용 복사본(0인 항목 제외). 무제한 모드에서는 카탈로그 순서의 모든 아이템.</summary>
        public IReadOnlyDictionary<string, int> Snapshot()
        {
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            if (_catalog.Unlimited)
            {
                foreach (ItemDefinition item in _catalog.Items) copy[item.Id] = Count(item.Id);
                return new ReadOnlyDictionary<string, int>(copy);
            }
            foreach (var entry in _counts) if (entry.Value > 0) copy.Add(entry.Key, entry.Value);
            return new ReadOnlyDictionary<string, int>(copy);
        }

        /// <summary>제작이 켜져 있고 카탈로그 아이템이며 비용 전체를 자원 재고가 감당하면 true.</summary>
        public bool CanCraft(string itemId)
        {
            if (_catalog.CraftingDisabled) return false; // 스토리 016: 제작 비활성(데이터 플래그).
            ItemDefinition item = _catalog.Get(itemId);
            if (item == null) return false;
            foreach (var cost in item.Cost) if (_resources.Get(cost.Key) < cost.Value) return false;
            return true;
        }

        /// <summary>비용을 빼고 1개를 더한다. 모자라면 false이고 자원·아이템 모두 그대로다.</summary>
        public bool TryCraft(string itemId)
        {
            if (!CanCraft(itemId)) return false;
            ItemDefinition item = _catalog.Get(itemId);
            var spent = new List<KeyValuePair<string, int>>();
            foreach (var cost in item.Cost)
            {
                if (_resources.TrySpend(cost.Key, cost.Value)) { spent.Add(cost); continue; }
                foreach (var refund in spent) _resources.Add(refund.Key, refund.Value);
                return false;
            }
            Add(itemId, 1);
            return true;
        }

        /// <summary>id가 카탈로그에 있고 amount가 음수가 아닌지 확인한다.</summary>
        private void Validate(string itemId, int amount)
        {
            if (!_catalog.Contains(itemId)) throw new ArgumentException("Unknown item: " + itemId, nameof(itemId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        }

        // 스펙 003: 저장 슬롯용 아이템 재고 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "items";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 21;

        /// <summary>저장된 수량 _counts 가운데 0이 아닌 것을 id 순으로 정렬해 JSON으로 돌려준다. 상태를 바꾸지 않는다.</summary>
        public string Capture()
        {
            List<string> ids = new List<string>();
            foreach (KeyValuePair<string, int> entry in _counts) if (entry.Value != 0) ids.Add(entry.Key);
            ids.Sort(StringComparer.Ordinal);
            CountSave state = new CountSave { Ids = ids.ToArray(), Counts = new int[ids.Count] };
            for (int i = 0; i < ids.Count; i++) state.Counts[i] = _counts[ids[i]];
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 _counts를 바꾼다. 카탈로그에 없는 id는 건너뛰고 저장에 없는 아이템은 0으로 둔다.
        /// 바뀐 아이템마다 Changed를 낸다.
        /// </summary>
        public void Restore(string json)
        {
            CountSave state = UnityEngine.JsonUtility.FromJson<CountSave>(json);
            Dictionary<string, int> next = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < state.Ids.Length && i < state.Counts.Length; i++)
            {
                if (_catalog.Contains(state.Ids[i])) next[state.Ids[i]] = state.Counts[i];
            }
            foreach (string id in new List<string>(_counts.Keys)) if (!next.ContainsKey(id)) next[id] = 0;
            foreach (KeyValuePair<string, int> entry in next)
            {
                int before = _counts.TryGetValue(entry.Key, out int n) ? n : 0;
                _counts[entry.Key] = entry.Value;
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
