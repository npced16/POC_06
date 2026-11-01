using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Items
{
    /// <summary>거점 하나의 태그 조건(예: 주조소 = 열 2 + 산업 1). 데이터에서 온다.</summary>
    public sealed class HubTagRequirement
    {
        /// <summary>거점 id.</summary>
        public string HubId { get; }
        /// <summary>태그 id별 필요 수.</summary>
        public IReadOnlyDictionary<string, int> Required { get; }
        /// <summary>데이터 순서의 태그 id(표시용).</summary>
        public IReadOnlyList<string> Order { get; }

        /// <summary>hubId와 (태그, 필요 수) 목록을 검증한다. 조건이 하나도 없으면 거부한다.</summary>
        public HubTagRequirement(string hubId, IEnumerable<KeyValuePair<string, int>> required)
        {
            if (string.IsNullOrWhiteSpace(hubId)) throw new ArgumentException("Hub id is required.", nameof(hubId));
            if (required == null) throw new ArgumentNullException(nameof(required));
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var entry in required)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 1) throw new ArgumentException("Invalid tag requirement on " + hubId, nameof(required));
                if (map.ContainsKey(entry.Key)) throw new ArgumentException("Duplicate tag requirement on " + hubId, nameof(required));
                map.Add(entry.Key, entry.Value);
                order.Add(entry.Key);
            }
            if (map.Count == 0) throw new ArgumentException("Hub needs at least one tag requirement: " + hubId, nameof(required));
            HubId = hubId;
            Required = map;
            Order = order;
        }

        /// <summary>HabitatTags.json 형식을 읽는다.</summary>
        public static List<HubTagRequirement> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Habitat tag json is required.", nameof(json));
            var data = JsonUtility.FromJson<RulesData>(json);
            if (data == null || data._hubs == null || data._hubs.Length == 0) throw new ArgumentException("Invalid habitat tag json.", nameof(json));
            var result = new List<HubTagRequirement>();
            foreach (HubData hub in data._hubs)
            {
                if (hub == null) throw new ArgumentException("Null hub entry.", nameof(json));
                var pairs = new List<KeyValuePair<string, int>>();
                foreach (TagCount t in hub._requires ?? Array.Empty<TagCount>())
                    pairs.Add(new KeyValuePair<string, int>(t?._tag, t?._count ?? 0));
                result.Add(new HubTagRequirement(hub._hubId, pairs));
            }
            return result;
        }

#pragma warning disable 0649
        [Serializable] private sealed class RulesData { public HubData[] _hubs; }
        [Serializable] private sealed class HubData { public string _hubId; public TagCount[] _requires; }
        [Serializable] private sealed class TagCount { public string _tag; public int _count; }
#pragma warning restore 0649
    }

    /// <summary>표시용 태그 진행도 한 줄: 태그 id, 현재 합, 필요 수.</summary>
    public readonly struct TagProgress
    {
        public readonly string TagId;
        public readonly int Current;
        public readonly int Required;
        public TagProgress(string tagId, int current, int required) { TagId = tagId; Current = current; Required = required; }
        public bool Met => Current >= Required;
    }

    /// <summary>HUD가 태그 진행도를 읽는 창구. ItemsInstaller가 GameServices에 등록한다.</summary>
    public interface ITagHabitatProgress
    {
        /// <summary>거점 조건의 태그별 진행도(데이터 순서). 조건 없는 거점은 빈 목록.</summary>
        IReadOnlyList<TagProgress> Progress(string hubId);
    }

    /// <summary>
    /// 스토리 013. 태그 조합 서식지. 거점 내부에 배치된 아이템 태그의 합이 거점 조건을 모두 채우면 완성이다.
    /// 완성되면 이벤트 플래그가 켜지고(한 번만 EventFlagRaised) 아이템을 치워도 플래그는 남는다(sticky).
    /// IHabitatSystem의 슬롯 메서드(002의 자원 슬롯 방식)는 비활성: TryPlace/TryRemove는 false, SlotContent는 null.
    /// 칸 변화(IHubInterior.CellChanged)마다 그 거점을 다시 판정한다.
    /// </summary>
    public sealed class TagHabitatSystem : IHabitatSystem, ITagHabitatProgress, IDisposable
    {
        private readonly ItemCatalog _catalog;
        private readonly IHubInterior _interior;
        private readonly Dictionary<string, HubTagRequirement> _rules = new Dictionary<string, HubTagRequirement>(StringComparer.Ordinal);
        private readonly HashSet<string> _complete = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);
        private bool _disposed;

        /// <summary>hubId: 플래그가 처음 켜질 때 한 번.</summary>
        public event Action<string> EventFlagRaised;

        /// <summary>카탈로그(태그), 거점 조건, 칸을 읽을 내부를 주입받고 CellChanged를 구독한다.</summary>
        public TagHabitatSystem(ItemCatalog catalog, IEnumerable<HubTagRequirement> requirements, IHubInterior interior)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _interior = interior ?? throw new ArgumentNullException(nameof(interior));
            if (requirements == null) throw new ArgumentNullException(nameof(requirements));
            foreach (HubTagRequirement rule in requirements)
            {
                if (rule == null || _rules.ContainsKey(rule.HubId)) throw new ArgumentException("Duplicate or null hub requirement.", nameof(requirements));
                _rules.Add(rule.HubId, rule);
            }
            _interior.CellChanged += OnCellChanged;
        }

        /// <summary>비활성(002 자원 슬롯 방식). 항상 false.</summary>
        public bool TryPlace(string hubId, int slotIndex, string resourceId) => false;
        /// <summary>비활성(002 자원 슬롯 방식). 항상 false.</summary>
        public bool TryRemove(string hubId, int slotIndex) => false;
        /// <summary>비활성(002 자원 슬롯 방식). 항상 null.</summary>
        public string SlotContent(string hubId, int slotIndex) => null;

        /// <summary>지금 배치된 아이템이 조건을 채우는지(아이템을 치우면 false로 돌아갈 수 있다).</summary>
        public bool IsHabitatComplete(string hubId) => hubId != null && _complete.Contains(hubId);

        /// <summary>한 번이라도 완성된 적이 있는지(sticky).</summary>
        public bool IsEventFlagSet(string hubId) => hubId != null && _flags.Contains(hubId);

        /// <summary>거점에 배치된 아이템 태그의 합. 모르는 거점이나 아이템은 무시한다.</summary>
        public IReadOnlyDictionary<string, int> TagTotals(string hubId)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<IInteriorCell> cells;
            try { cells = hubId == null ? null : _interior.Cells(hubId); }
            catch (ArgumentException) { cells = null; }
            if (cells == null) return totals;
            foreach (IInteriorCell cell in cells)
            {
                if (cell == null || cell.State != InteriorCellState.ItemPlaced) continue;
                ItemDefinition item = _catalog.Get(cell.ItemId);
                if (item == null) continue;
                foreach (string tag in item.Tags) totals[tag] = (totals.TryGetValue(tag, out int n) ? n : 0) + 1;
            }
            return totals;
        }

        /// <summary>태그별 진행도(데이터 순서).</summary>
        public IReadOnlyList<TagProgress> Progress(string hubId)
        {
            var list = new List<TagProgress>();
            if (hubId == null || !_rules.TryGetValue(hubId, out HubTagRequirement rule)) return list;
            IReadOnlyDictionary<string, int> totals = TagTotals(hubId);
            foreach (string tag in rule.Order)
                list.Add(new TagProgress(tag, totals.TryGetValue(tag, out int n) ? n : 0, rule.Required[tag]));
            return list;
        }

        /// <summary>거점을 다시 판정해 완성 상태를 갱신하고 플래그가 처음 켜지면 EventFlagRaised를 낸다.</summary>
        public void Evaluate(string hubId)
        {
            if (_disposed || hubId == null || !_rules.TryGetValue(hubId, out HubTagRequirement rule)) return;
            IReadOnlyDictionary<string, int> totals = TagTotals(hubId);
            bool complete = true;
            foreach (var need in rule.Required)
                if (!totals.TryGetValue(need.Key, out int have) || have < need.Value) { complete = false; break; }
            if (complete) _complete.Add(hubId); else _complete.Remove(hubId);
            if (complete && _flags.Add(hubId)) EventFlagRaised?.Invoke(hubId);
        }

        /// <summary>CellChanged 구독을 해제한다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _interior.CellChanged -= OnCellChanged;
        }

        private void OnCellChanged(string hubId, int cellIndex) => Evaluate(hubId);
    }
}
