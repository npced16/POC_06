using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using UnityEngine;

namespace IronRing.Items
{
    /// <summary>
    /// 스토리 013 (design/game-brief.md MVP 3). 아이템 하나의 정의: 한국어 이름, 태그 목록, 자원 제작 비용.
    /// 같은 태그를 두 번 적으면 그 태그를 2로 센다.
    /// </summary>
    public sealed class ItemDefinition
    {
        /// <summary>데이터의 고정 id(012의 묻힌 아이템 id와 같다).</summary>
        public string Id { get; }
        /// <summary>한국어 표시 이름(데이터).</summary>
        public string Name { get; }
        /// <summary>배치할 때 보여주는 재료의 짧은 쓰임.</summary>
        public string Description { get; }
        /// <summary>태그 id 목록. 중복은 가중치로 센다.</summary>
        public IReadOnlyList<string> Tags { get; }
        /// <summary>자원 id별 제작 비용(0인 항목은 뺀다).</summary>
        public IReadOnlyDictionary<string, int> Cost { get; }
        /// <summary>스토리 016: 분류(데이터). "tile"은 서식지를 이루는 바닥 타일, 그 밖은 장식("decor" 기본).</summary>
        public string Category { get; }
        /// <summary>스토리 041. 배치 가격(마르크, ItemCatalog.json _price, 0 이상).</summary>
        public int Price { get; internal set; }
        /// <summary>스토리 016: 서식지 타일인지(Category == "tile").</summary>
        public bool IsTile => Category == TileCategory;
        /// <summary>타일 분류 문자열.</summary>
        public const string TileCategory = "tile";

        /// <summary>id, 이름, 태그, 비용을 검증해 불변 정의를 만든다.</summary>
        public ItemDefinition(string id, string name, IEnumerable<string> tags, IReadOnlyDictionary<string, int> cost, string category = null, string description = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Item id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required: " + id, nameof(name));
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            if (cost == null) throw new ArgumentNullException(nameof(cost));
            var tagList = new List<string>();
            foreach (string tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Blank tag on item " + id, nameof(tags));
                tagList.Add(tag);
            }
            if (tagList.Count == 0) throw new ArgumentException("Item needs at least one tag: " + id, nameof(tags));
            var costCopy = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in cost)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0) throw new ArgumentException("Invalid cost on item " + id, nameof(cost));
                if (entry.Value > 0) costCopy[entry.Key] = entry.Value;
            }
            Id = id;
            Name = name;
            Description = description ?? string.Empty;
            Tags = new ReadOnlyCollection<string>(tagList);
            Cost = new ReadOnlyDictionary<string, int>(costCopy);
            Category = string.IsNullOrWhiteSpace(category) ? "decor" : category;
        }
    }

    /// <summary>아이템 정의와 태그 이름의 조회 테이블. 순수 C#이며 Parse만 JsonUtility를 쓴다.</summary>
    public sealed class ItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> _items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _tagNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<ItemDefinition> _ordered = new List<ItemDefinition>();
        private readonly List<string> _tagOrder = new List<string>();

        /// <summary>데이터 순서의 모든 아이템.</summary>
        public IReadOnlyList<ItemDefinition> Items => _ordered;
        /// <summary>데이터 순서의 모든 태그 id.</summary>
        public IReadOnlyList<string> TagIds => _tagOrder;
        /// <summary>스토리 016: 무제한 모드(데이터 _unlimited). 켜지면 인벤토리는 모든 아이템을 UnlimitedCount개 가진 것처럼 동작한다.</summary>
        public bool Unlimited { get; private set; }
        /// <summary>무제한 모드에서 Count가 돌려주는 수량(데이터 _unlimitedCount, 기본 999).</summary>
        public int UnlimitedCount { get; private set; } = 999;
        /// <summary>스토리 016: 자원 제작 비활성(데이터 _craftingDisabled). 켜지면 CanCraft/TryCraft는 false.</summary>
        public bool CraftingDisabled { get; private set; }

        /// <summary>items와 tagNames(태그 id → 한국어)를 검증해 카탈로그를 만든다. 아이템이 모르는 태그를 쓰면 거부한다.</summary>
        public ItemCatalog(IEnumerable<ItemDefinition> items, IEnumerable<KeyValuePair<string, string>> tagNames)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (tagNames == null) throw new ArgumentNullException(nameof(tagNames));
            foreach (var tag in tagNames)
            {
                if (string.IsNullOrWhiteSpace(tag.Key) || string.IsNullOrWhiteSpace(tag.Value)) throw new ArgumentException("Invalid tag entry.", nameof(tagNames));
                if (_tagNames.ContainsKey(tag.Key)) throw new ArgumentException("Duplicate tag: " + tag.Key, nameof(tagNames));
                _tagNames.Add(tag.Key, tag.Value);
                _tagOrder.Add(tag.Key);
            }
            foreach (ItemDefinition item in items)
            {
                if (item == null) throw new ArgumentException("Null item.", nameof(items));
                if (_items.ContainsKey(item.Id)) throw new ArgumentException("Duplicate item: " + item.Id, nameof(items));
                foreach (string tag in item.Tags)
                    if (!_tagNames.ContainsKey(tag)) throw new ArgumentException("Unknown tag " + tag + " on " + item.Id, nameof(items));
                _items.Add(item.Id, item);
                _ordered.Add(item);
            }
            if (_ordered.Count == 0) throw new ArgumentException("Catalog needs at least one item.", nameof(items));
        }

        /// <summary>id가 카탈로그에 있는지.</summary>
        public bool Contains(string itemId) => itemId != null && _items.ContainsKey(itemId);

        /// <summary>id의 정의, 없으면 null.</summary>
        public ItemDefinition Get(string itemId) => itemId != null && _items.TryGetValue(itemId, out var item) ? item : null;

        /// <summary>태그의 한국어 이름, 모르면 id 그대로.</summary>
        public string TagName(string tagId) => tagId != null && _tagNames.TryGetValue(tagId, out var name) ? name : tagId;

        /// <summary>태그 id → 한국어 이름 쌍. 카탈로그 생성자에 다시 넣을 수 있다.</summary>
        public IEnumerable<KeyValuePair<string, string>> TagNames()
        {
            foreach (string id in _tagOrder) yield return new KeyValuePair<string, string>(id, _tagNames[id]);
        }

        /// <summary>ItemCatalog.json 형식을 읽어 카탈로그를 만든다.</summary>
        public static ItemCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Item catalog json is required.", nameof(json));
            var data = JsonUtility.FromJson<CatalogData>(json);
            if (data == null || data._items == null || data._tags == null) throw new ArgumentException("Invalid item catalog json.", nameof(json));
            var tags = new List<KeyValuePair<string, string>>();
            foreach (TagData tag in data._tags) tags.Add(new KeyValuePair<string, string>(tag?._id, tag?._name));
            var items = new List<ItemDefinition>();
            foreach (ItemData item in data._items)
            {
                if (item == null) throw new ArgumentException("Null item entry.", nameof(json));
                var cost = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (CostData c in item._cost ?? Array.Empty<CostData>())
                {
                    if (c == null || string.IsNullOrWhiteSpace(c._resource)) throw new ArgumentException("Invalid cost on " + item._id, nameof(json));
                    cost[c._resource] = (cost.TryGetValue(c._resource, out int v) ? v : 0) + c._amount;
                }
                items.Add(new ItemDefinition(item._id, item._name, item._tags ?? Array.Empty<string>(), cost, item._category, item._description) { Price = Math.Max(0, item._price) });
            }
            if (data._unlimited && data._unlimitedCount < 1) throw new ArgumentException("_unlimitedCount must be positive in unlimited mode.", nameof(json));
            return new ItemCatalog(items, tags)
            {
                Unlimited = data._unlimited,
                UnlimitedCount = data._unlimitedCount > 0 ? data._unlimitedCount : 999,
                CraftingDisabled = data._craftingDisabled
            };
        }

#pragma warning disable 0649
        [Serializable] private sealed class CatalogData { public bool _unlimited; public int _unlimitedCount; public bool _craftingDisabled; public TagData[] _tags; public ItemData[] _items; }
        [Serializable] private sealed class TagData { public string _id; public string _name; }
        [Serializable] private sealed class ItemData { public string _id; public string _name; public string _description; public string _category; public string[] _tags; public CostData[] _cost; public int _price; }
        [Serializable] private sealed class CostData { public string _resource; public int _amount; }
#pragma warning restore 0649
    }
}
