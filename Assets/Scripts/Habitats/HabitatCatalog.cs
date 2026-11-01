using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using UnityEngine;

namespace IronRing.Habitats
{
    /// <summary>스토리 016. 서식지 종류의 정의 방식.</summary>
    public enum HabitatKind
    {
        /// <summary>같은 타일이 RequiredCount칸 이상 상하좌우로 이어진 묶음(모양 무관).</summary>
        Cluster,
        /// <summary>타일 격자 템플릿이 회전·거울 8변형 중 하나로 들어맞는 배치.</summary>
        Pattern
    }

    /// <summary>
    /// 스토리 016 (design/game-brief.md MVP 3). 서식지 종류 하나. 모든 값은 Resources/Habitats/HabitatTypes.json에서 온다.
    /// </summary>
    public sealed class HabitatType
    {
        /// <summary>패턴 템플릿에서 "어떤 칸이든 상관없음"을 뜻하는 토큰.</summary>
        public const string DontCare = ".";

        /// <summary>고정 id(카탈로그 안에서 유일).</summary>
        public string Id { get; }
        /// <summary>한국어 표시 이름.</summary>
        public string Name { get; }
        /// <summary>정의 방식.</summary>
        public HabitatKind Kind { get; }
        /// <summary>Cluster일 때 타일 아이템 id, Pattern이면 null.</summary>
        public string TileItemId { get; }
        /// <summary>Cluster: 필요한 칸 수. Pattern: 템플릿에서 "."이 아닌 칸 수.</summary>
        public int RequiredCount { get; }
        /// <summary>Pattern일 때 템플릿(줄 × 칸, 토큰은 타일 아이템 id 또는 "."). Cluster면 빈 목록.</summary>
        public IReadOnlyList<IReadOnlyList<string>> Pattern { get; }
        /// <summary>완성된 서식지 하나당 바퀴마다 더하는 번영. 0이면 거점 데이터의 기본값(_habitatBonus)을 쓴다.</summary>
        public float ProsperityBonus { get; }
        /// <summary>이 서식지를 찾아오는 NPC id(없으면 null).</summary>
        public string NpcTypeId { get; }

        /// <summary>Cluster 종류를 만든다.</summary>
        public static HabitatType CreateCluster(string id, string name, string tileItemId, int requiredCount, float prosperityBonus, string npcTypeId)
        {
            if (string.IsNullOrWhiteSpace(tileItemId)) throw new ArgumentException("Cluster habitat needs a tile item id: " + id);
            if (requiredCount < 1) throw new ArgumentException("Cluster habitat needs requiredCount >= 1: " + id);
            return new HabitatType(id, name, HabitatKind.Cluster, tileItemId, requiredCount, Array.Empty<IReadOnlyList<string>>(), prosperityBonus, npcTypeId);
        }

        /// <summary>Pattern 종류를 만든다. rows는 줄마다 공백으로 구분한 토큰 문자열이며 모든 줄의 토큰 수가 같아야 한다.</summary>
        public static HabitatType CreatePattern(string id, string name, IEnumerable<string> rows, float prosperityBonus, string npcTypeId)
        {
            if (rows == null) throw new ArgumentException("Pattern habitat needs rows: " + id);
            var grid = new List<IReadOnlyList<string>>();
            int width = -1, required = 0;
            foreach (string row in rows)
            {
                string[] tokens = (row ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) throw new ArgumentException("Empty pattern row in " + id);
                if (width >= 0 && tokens.Length != width) throw new ArgumentException("Pattern rows must have equal width: " + id);
                width = tokens.Length;
                foreach (string token in tokens) if (token != DontCare) required++;
                grid.Add(Array.AsReadOnly(tokens));
            }
            if (grid.Count == 0 || required == 0) throw new ArgumentException("Pattern needs at least one tile: " + id);
            return new HabitatType(id, name, HabitatKind.Pattern, null, required, grid.AsReadOnly(), prosperityBonus, npcTypeId);
        }

        private HabitatType(string id, string name, HabitatKind kind, string tileItemId, int requiredCount,
            IReadOnlyList<IReadOnlyList<string>> pattern, float prosperityBonus, string npcTypeId)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Habitat id is required.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Habitat name is required: " + id);
            if (float.IsNaN(prosperityBonus) || float.IsInfinity(prosperityBonus) || prosperityBonus < 0f)
                throw new ArgumentException("Habitat prosperity bonus must be finite and >= 0: " + id);
            Id = id;
            Name = name;
            Kind = kind;
            TileItemId = tileItemId;
            RequiredCount = requiredCount;
            Pattern = pattern;
            ProsperityBonus = prosperityBonus;
            NpcTypeId = string.IsNullOrWhiteSpace(npcTypeId) ? null : npcTypeId;
        }

        /// <summary>이 종류가 쓰는 타일 아이템 id 전부(Pattern이면 템플릿의 "."이 아닌 토큰).</summary>
        public IEnumerable<string> TileIds()
        {
            if (Kind == HabitatKind.Cluster) { yield return TileItemId; yield break; }
            foreach (IReadOnlyList<string> row in Pattern)
                foreach (string token in row)
                    if (token != DontCare) yield return token;
        }
    }

    /// <summary>스토리 016. 서식지 종류 카탈로그(데이터 순서 유지). 순수 C#이며 Parse만 JsonUtility를 쓴다.</summary>
    public sealed class HabitatCatalog
    {
        private readonly List<HabitatType> _types = new List<HabitatType>();
        private readonly Dictionary<string, HabitatType> _byId = new Dictionary<string, HabitatType>(StringComparer.Ordinal);

        /// <summary>데이터 순서의 모든 종류.</summary>
        public IReadOnlyList<HabitatType> Types => _types;
        /// <summary>데이터 순서의 모든 종류 id.</summary>
        public IReadOnlyList<string> TypeIds { get; }

        /// <summary>종류 목록을 검증한다(id 중복·빈 목록 거부).</summary>
        public HabitatCatalog(IEnumerable<HabitatType> types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            var ids = new List<string>();
            foreach (HabitatType type in types)
            {
                if (type == null) throw new ArgumentException("Null habitat type.", nameof(types));
                if (_byId.ContainsKey(type.Id)) throw new ArgumentException("Duplicate habitat type: " + type.Id, nameof(types));
                _byId.Add(type.Id, type);
                _types.Add(type);
                ids.Add(type.Id);
            }
            if (_types.Count == 0) throw new ArgumentException("Habitat catalog needs at least one type.", nameof(types));
            TypeIds = new ReadOnlyCollection<string>(ids);
        }

        /// <summary>id의 종류, 없으면 null.</summary>
        public HabitatType Get(string typeId) => typeId != null && _byId.TryGetValue(typeId, out HabitatType t) ? t : null;

        /// <summary>모든 종류의 타일 id 중 isKnownTile을 만족하지 않는 것의 목록(비면 정상).</summary>
        public List<string> UnknownTiles(Func<string, bool> isKnownTile)
        {
            var unknown = new List<string>();
            if (isKnownTile == null) return unknown;
            foreach (HabitatType type in _types)
                foreach (string tile in type.TileIds())
                    if (!isKnownTile(tile) && !unknown.Contains(tile)) unknown.Add(tile);
            return unknown;
        }

        /// <summary>HabitatTypes.json 형식을 읽는다.</summary>
        public static HabitatCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Habitat catalog json is required.", nameof(json));
            var data = JsonUtility.FromJson<CatalogData>(json);
            if (data == null || data._types == null) throw new ArgumentException("Invalid habitat catalog json.", nameof(json));
            var types = new List<HabitatType>();
            foreach (TypeData t in data._types)
            {
                if (t == null) throw new ArgumentException("Null habitat entry.", nameof(json));
                string kind = string.IsNullOrWhiteSpace(t._kind) ? "cluster" : t._kind.Trim().ToLowerInvariant();
                if (kind == "cluster") types.Add(HabitatType.CreateCluster(t._id, t._name, t._tileItemId, t._requiredCount, t._prosperityBonus, t._npcTypeId));
                else if (kind == "pattern") types.Add(HabitatType.CreatePattern(t._id, t._name, t._pattern, t._prosperityBonus, t._npcTypeId));
                else throw new ArgumentException("Unknown habitat kind " + t._kind + " on " + t._id, nameof(json));
            }
            return new HabitatCatalog(types);
        }

#pragma warning disable 0649
        [Serializable] private sealed class CatalogData { public TypeData[] _types; }
        [Serializable]
        private sealed class TypeData
        {
            public string _id; public string _name; public string _kind; public string _tileItemId; public int _requiredCount;
            public string[] _pattern; public float _prosperityBonus; public string _npcTypeId;
        }
#pragma warning restore 0649
    }
}
