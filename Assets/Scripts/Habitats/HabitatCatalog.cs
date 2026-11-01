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
        Pattern,
        /// <summary>스토리 020. 혼합 묶음: 집합에 속한 타일 칸들의 상하좌우 묶음이 총 RequiredCount칸 이상이고 타일별 최소 수를 채움(모양 무관).</summary>
        Set
    }

    /// <summary>스토리 020. 혼합 묶음(Set)의 타일 하나와 그 최소 칸 수.</summary>
    public sealed class HabitatTileRequirement
    {
        /// <summary>타일 아이템 id.</summary>
        public string TileItemId { get; }
        /// <summary>묶음 안에 있어야 하는 이 타일의 최소 칸 수(0 이상, 0이면 집합에 속하기만 함).</summary>
        public int MinCount { get; }

        /// <summary>요구 하나를 만든다.</summary>
        public HabitatTileRequirement(string tileItemId, int minCount)
        {
            if (string.IsNullOrWhiteSpace(tileItemId)) throw new ArgumentException("Set tile needs a tile item id.");
            if (minCount < 0) throw new ArgumentException("Set tile minCount must be >= 0: " + tileItemId);
            TileItemId = tileItemId;
            MinCount = minCount;
        }
    }

    /// <summary>스토리 041. 생산·소비 품목 한 줄(품목 id, 바퀴당 수량).</summary>
    public sealed class HabitatGoodsAmount
    {
        /// <summary>품목 id(Economy/Goods.json).</summary>
        public string GoodsId { get; }
        /// <summary>바퀴당 수량(1 이상).</summary>
        public int Amount { get; }

        /// <summary>한 줄을 만든다.</summary>
        public HabitatGoodsAmount(string goodsId, int amount)
        {
            if (string.IsNullOrWhiteSpace(goodsId)) throw new ArgumentException("Goods amount needs a goods id.");
            if (amount < 1) throw new ArgumentException("Goods amount must be >= 1: " + goodsId);
            GoodsId = goodsId.Trim();
            Amount = amount;
        }
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
        /// <summary>Cluster·Set: 묶음에 필요한 총 칸 수. Pattern: 템플릿에서 "."이 아닌 칸 수.</summary>
        public int RequiredCount { get; }
        /// <summary>Pattern일 때 템플릿(줄 × 칸, 토큰은 타일 아이템 id 또는 "."). Cluster면 빈 목록.</summary>
        public IReadOnlyList<IReadOnlyList<string>> Pattern { get; }
        /// <summary>Set일 때 타일 집합과 타일별 최소 수(데이터 순서). Cluster면 그 타일 하나(최소 = RequiredCount), Pattern이면 빈 목록.</summary>
        public IReadOnlyList<HabitatTileRequirement> SetTiles { get; private set; }
        /// <summary>완성된 서식지 하나당 바퀴마다 더하는 번영. 0이면 거점 데이터의 기본값(_habitatBonus)을 쓴다.</summary>
        public float ProsperityBonus { get; }
        /// <summary>이 서식지를 찾아오는 NPC id(없으면 null).</summary>
        public string NpcTypeId { get; }
        /// <summary>스토리 027. 건물 지붕 계열(machine|housing|storage|glass). 데이터에 없으면 null(표현이 첫 계열을 쓴다).</summary>
        public string BuildingStyle { get; private set; }
        /// <summary>스토리 027. 건물 지붕 위 상징물 id(SpriteGenBuildings.SIGNS). 없으면 null.</summary>
        public string SignProp { get; private set; }

        /// <summary>스토리 041. 바퀴 정산마다 창고에 넣는 품목(가동 중일 때). 없으면 빈 목록.</summary>
        public IReadOnlyList<HabitatGoodsAmount> Produces { get; private set; } = Array.Empty<HabitatGoodsAmount>();
        /// <summary>스토리 041. 바퀴 정산마다 창고에서 꺼내는 입력 품목. 모자라면 가동 중단(NoInput).</summary>
        public IReadOnlyList<HabitatGoodsAmount> Consumes { get; private set; } = Array.Empty<HabitatGoodsAmount>();
        /// <summary>스토리 041. 가동에 필요한 노동자 수(042가 노동 공급을 붙인다).</summary>
        public int Workers { get; private set; }
        /// <summary>스토리 041. 가동한 바퀴마다 장부에 적는 건물 유지비(마르크).</summary>
        public int Upkeep { get; private set; }
        /// <summary>스토리 041. 가동 중인 증기 공급 건물이 필요한지.</summary>
        public bool NeedsSteam { get; private set; }
        /// <summary>스토리 041. 가동하면 거점에 증기를 공급하는지(보일러실 등).</summary>
        public bool ProvidesSteam { get; private set; }
        /// <summary>스토리 041. 물 공급 건물이 필요한지(온실).</summary>
        public bool NeedsWater { get; private set; }
        /// <summary>스토리 041. 가동하면 거점에 물을 공급하는지(저수조·급수탑).</summary>
        public bool ProvidesWater { get; private set; }
        /// <summary>스토리 041(042용). 주거 칸(사는 사람 수).</summary>
        public int Housing { get; private set; }
        /// <summary>스토리 041(042용). 빛(가스등 계열 만족도).</summary>
        public int Light { get; private set; }

        /// <summary>스토리 041. 카탈로그 파서가 생산 데이터를 붙인다.</summary>
        internal void SetProduction(IReadOnlyList<HabitatGoodsAmount> produces, IReadOnlyList<HabitatGoodsAmount> consumes, int workers, int upkeep,
            bool needsSteam, bool providesSteam, bool needsWater, bool providesWater, int housing, int light)
        {
            if (workers < 0 || upkeep < 0 || housing < 0 || light < 0) throw new ArgumentException("Habitat production numbers must be >= 0: " + Id);
            Produces = produces ?? Array.Empty<HabitatGoodsAmount>();
            Consumes = consumes ?? Array.Empty<HabitatGoodsAmount>();
            Workers = workers; Upkeep = upkeep; NeedsSteam = needsSteam; ProvidesSteam = providesSteam;
            NeedsWater = needsWater; ProvidesWater = providesWater; Housing = housing; Light = light;
        }

        /// <summary>스토리 027. 카탈로그 파서가 건물 외관 데이터를 붙인다.</summary>
        internal void SetBuilding(string style, string signProp)
        {
            BuildingStyle = string.IsNullOrWhiteSpace(style) ? null : style.Trim();
            SignProp = string.IsNullOrWhiteSpace(signProp) ? null : signProp.Trim();
        }

        /// <summary>Cluster 종류를 만든다.</summary>
        public static HabitatType CreateCluster(string id, string name, string tileItemId, int requiredCount, float prosperityBonus, string npcTypeId)
        {
            if (string.IsNullOrWhiteSpace(tileItemId)) throw new ArgumentException("Cluster habitat needs a tile item id: " + id);
            if (requiredCount < 1) throw new ArgumentException("Cluster habitat needs requiredCount >= 1: " + id);
            var type = new HabitatType(id, name, HabitatKind.Cluster, tileItemId, requiredCount, Array.Empty<IReadOnlyList<string>>(), prosperityBonus, npcTypeId);
            type.SetTiles = Array.AsReadOnly(new[] { new HabitatTileRequirement(tileItemId, requiredCount) });
            return type;
        }

        /// <summary>
        /// 스토리 020. Set(혼합 묶음) 종류를 만든다. tiles는 서로 다른 타일 id여야 하며, requiredCount(총 칸 수)는 1 이상이고
        /// 타일별 최소 수의 합보다 작을 수 없다.
        /// </summary>
        public static HabitatType CreateSet(string id, string name, IEnumerable<HabitatTileRequirement> tiles, int requiredCount, float prosperityBonus, string npcTypeId)
        {
            if (tiles == null) throw new ArgumentException("Set habitat needs tiles: " + id);
            var list = new List<HabitatTileRequirement>();
            int minimumSum = 0;
            foreach (HabitatTileRequirement tile in tiles)
            {
                if (tile == null) throw new ArgumentException("Null set tile in " + id);
                if (list.Exists(t => t.TileItemId == tile.TileItemId)) throw new ArgumentException("Duplicate set tile " + tile.TileItemId + " in " + id);
                list.Add(tile);
                minimumSum += tile.MinCount;
            }
            if (list.Count == 0) throw new ArgumentException("Set habitat needs at least one tile: " + id);
            if (requiredCount < 1 || requiredCount < minimumSum)
                throw new ArgumentException("Set habitat requiredCount must be >= 1 and >= the sum of minCounts: " + id);
            var type = new HabitatType(id, name, HabitatKind.Set, null, requiredCount, Array.Empty<IReadOnlyList<string>>(), prosperityBonus, npcTypeId);
            type.SetTiles = list.AsReadOnly();
            return type;
        }

        /// <summary>스토리 020. Cluster·Set 묶음에 이 타일이 들어갈 수 있는지(Pattern이면 false).</summary>
        public bool AcceptsTile(string tileItemId)
        {
            if (tileItemId == null) return false;
            foreach (HabitatTileRequirement tile in SetTiles) if (tile.TileItemId == tileItemId) return true;
            return false;
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
            var type = new HabitatType(id, name, HabitatKind.Pattern, null, required, grid.AsReadOnly(), prosperityBonus, npcTypeId);
            type.SetTiles = Array.Empty<HabitatTileRequirement>();
            return type;
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

        /// <summary>이 종류가 쓰는 타일 아이템 id 전부(Set이면 집합의 타일, Pattern이면 템플릿의 "."이 아닌 토큰).</summary>
        public IEnumerable<string> TileIds()
        {
            if (Kind == HabitatKind.Cluster) { yield return TileItemId; yield break; }
            if (Kind == HabitatKind.Set) { foreach (HabitatTileRequirement tile in SetTiles) yield return tile.TileItemId; yield break; }
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
        /// <summary>스토리 035. true(기본)면 한 번 완성된 서식지는 칸이 고정되어 더 커지지 않는다. false면 매번 전체 재판정(예전 규칙).</summary>
        public bool LockCompleted { get; set; } = true;

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
                else if (kind == "set")
                {
                    var tiles = new List<HabitatTileRequirement>();
                    foreach (TileData tile in t._tiles ?? Array.Empty<TileData>())
                    {
                        if (tile == null) throw new ArgumentException("Null set tile on " + t._id, nameof(json));
                        tiles.Add(new HabitatTileRequirement(tile._tileItemId, tile._minCount));
                    }
                    types.Add(HabitatType.CreateSet(t._id, t._name, tiles, t._requiredCount, t._prosperityBonus, t._npcTypeId));
                }
                else throw new ArgumentException("Unknown habitat kind " + t._kind + " on " + t._id, nameof(json));
                types[types.Count - 1].SetBuilding(t._buildingStyle, t._signProp);
                types[types.Count - 1].SetProduction(Amounts(t._produces, t._id), Amounts(t._consumes, t._id), t._workers, t._upkeep,
                    t._needsSteam, t._providesSteam, t._needsWater, t._providesWater, t._housing, t._light);
            }
            return new HabitatCatalog(types) { LockCompleted = data._lockCompleted };
        }

        private static IReadOnlyList<HabitatGoodsAmount> Amounts(GoodsData[] data, string typeId)
        {
            if (data == null || data.Length == 0) return Array.Empty<HabitatGoodsAmount>();
            var list = new List<HabitatGoodsAmount>();
            foreach (GoodsData g in data)
            {
                if (g == null) throw new ArgumentException("Null goods amount on " + typeId);
                list.Add(new HabitatGoodsAmount(g._goodsId, g._amount));
            }
            return list.AsReadOnly();
        }

#pragma warning disable 0649
        [Serializable] private sealed class CatalogData { public TypeData[] _types; public bool _lockCompleted = true; }
        [Serializable]
        private sealed class TypeData
        {
            public string _id; public string _name; public string _kind; public string _tileItemId; public int _requiredCount;
            public string[] _pattern; public TileData[] _tiles; public float _prosperityBonus; public string _npcTypeId;
            public string _buildingStyle; public string _signProp;
            public GoodsData[] _produces; public GoodsData[] _consumes; public int _workers; public int _upkeep;
            public bool _needsSteam; public bool _providesSteam; public bool _needsWater; public bool _providesWater; public int _housing; public int _light;
        }
        [Serializable] private sealed class GoodsData { public string _goodsId; public int _amount; }
        [Serializable] private sealed class TileData { public string _tileItemId; public int _minCount; }
#pragma warning restore 0649
    }
}
