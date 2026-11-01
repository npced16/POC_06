using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using IronRing.Contracts;
using IronRing.Items;

namespace IronRing.Items.Tests
{
    /// <summary>테스트용 카탈로그·거점 조건 팩토리. 값은 실제 데이터 기본값과 같은 모양이다.</summary>
    internal static class ItemsFixtures
    {
        public static ItemCatalog Catalog()
        {
            var tags = new[]
            {
                new KeyValuePair<string, string>("heat", "열"), new KeyValuePair<string, string>("industry", "산업"),
                new KeyValuePair<string, string>("water", "물"), new KeyValuePair<string, string>("plant", "식물")
            };
            var items = new[]
            {
                new ItemDefinition("brazier", "화로", new[] { "heat" }, Cost(("scrap", 1), ("coal", 2))),
                new ItemDefinition("steam_pipe", "증기관", new[] { "heat", "industry" }, Cost(("scrap", 2), ("coal", 2), ("water", 1))),
                new ItemDefinition("gear", "톱니 장식", new[] { "industry" }, Cost(("scrap", 2), ("coal", 1))),
                new ItemDefinition("seedling", "화분", new[] { "plant" }, Cost(("water", 1), ("grain", 1)))
            };
            return new ItemCatalog(items, tags);
        }

        public static HubTagRequirement[] Rules() => new[]
        {
            new HubTagRequirement("hub-1", new[] { new KeyValuePair<string, int>("heat", 2), new KeyValuePair<string, int>("industry", 1) }),
            new HubTagRequirement("hub-2", new[] { new KeyValuePair<string, int>("plant", 2), new KeyValuePair<string, int>("water", 1) })
        };

        public static IReadOnlyDictionary<string, int> Cost(params (string id, int amount)[] entries)
        {
            var d = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var e in entries) d[e.id] = e.amount;
            return d;
        }
    }

    /// <summary>IResourceInventory 가짜. TrySpend 실패를 강제할 수 있다.</summary>
    internal sealed class FakeResources : IResourceInventory
    {
        private readonly Dictionary<string, int> _stock = new Dictionary<string, int>(StringComparer.Ordinal);
        public event Action<string, int> Changed;
        public FakeResources(params (string id, int amount)[] stock) { foreach (var s in stock) _stock[s.id] = s.amount; }
        public int Get(string resourceId) => resourceId != null && _stock.TryGetValue(resourceId, out int v) ? v : 0;
        public void Add(string resourceId, int amount) { _stock[resourceId] = Get(resourceId) + amount; Changed?.Invoke(resourceId, Get(resourceId)); }
        public bool TrySpend(string resourceId, int amount)
        {
            if (Get(resourceId) < amount) return false;
            _stock[resourceId] = Get(resourceId) - amount;
            Changed?.Invoke(resourceId, Get(resourceId));
            return true;
        }
        public IReadOnlyDictionary<string, int> Snapshot() => new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(_stock));
    }

    internal sealed class FakeCell : IInteriorCell
    {
        public int Index { get; set; }
        public int Row => 0;
        public int Column => Index;
        public bool IsUpper => true;
        public InteriorCellState State { get; set; }
        public string ItemId { get; set; }
        public string FacilityId => null;
    }

    /// <summary>IHubInterior + 칸 쓰기 가짜. 거점마다 칸 수를 정하고 상태를 직접 지정할 수 있다.</summary>
    internal sealed class FakeInterior : IHubInterior, IInteriorItemSlots
    {
        private readonly Dictionary<string, List<FakeCell>> _cells = new Dictionary<string, List<FakeCell>>(StringComparer.Ordinal);
        public event Action<string, int> CellChanged;
        public event Action<string, int> InvestigationsChanged { add { } remove { } }
        public bool RefuseWrites { get; set; }

        public FakeInterior(int cellsPerHub, params string[] hubs)
        {
            foreach (string hub in hubs)
            {
                var list = new List<FakeCell>();
                for (int i = 0; i < cellsPerHub; i++) list.Add(new FakeCell { Index = i, State = InteriorCellState.Empty });
                _cells.Add(hub, list);
            }
        }

        public void SetState(string hub, int index, InteriorCellState state) => _cells[hub][index].State = state;

        public IReadOnlyList<IInteriorCell> Cells(string hubId)
        {
            if (hubId == null || !_cells.TryGetValue(hubId, out var list)) throw new ArgumentException("Unknown hub: " + hubId);
            return list.ConvertAll(c => (IInteriorCell)c);
        }

        public int InvestigationsLeft(string hubId) => 0;
        public bool CanInvestigate(string hubId, int cellIndex) => false;
        public InvestigationResult Investigate(string hubId, int cellIndex, out string foundId) { foundId = null; return InvestigationResult.Empty; }
        public void OnDocked(string hubId) { }

        public bool TryPlaceItem(string hubId, int cellIndex, string itemId)
        {
            FakeCell cell = _cells[hubId][cellIndex];
            if (RefuseWrites || cell.State != InteriorCellState.Empty) return false;
            cell.State = InteriorCellState.ItemPlaced;
            cell.ItemId = itemId;
            CellChanged?.Invoke(hubId, cellIndex);
            return true;
        }

        public bool TryRemoveItem(string hubId, int cellIndex)
        {
            FakeCell cell = _cells[hubId][cellIndex];
            if (RefuseWrites || cell.State != InteriorCellState.ItemPlaced) return false;
            cell.State = InteriorCellState.Empty;
            cell.ItemId = null;
            CellChanged?.Invoke(hubId, cellIndex);
            return true;
        }
    }
}
