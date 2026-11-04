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

    /// <summary>
    /// 서식지 종류 하나. 조합 조건은 HabitatRecipes.json, 이름·경제·외관은 HabitatTypes.json에서 온다.
    /// </summary>
    public sealed class HabitatType
    {
        /// <summary>패턴 템플릿에서 "어떤 칸이든 상관없음"을 뜻하는 토큰.</summary>
        public const string DontCare = ".";
        /// <summary>완성된 시설 조합이 차지할 수 있는 최대 재료 칸 수.</summary>
        public const int MAX_RECIPE_CELLS = 9;

        /// <summary>고정 id(카탈로그 안에서 유일).</summary>
        public string Id { get; }
        /// <summary>한국어 표시 이름.</summary>
        public string Name { get; }
        /// <summary>이 시설이 마을에서 해결하는 일.</summary>
        public string VillageWork { get; private set; }
        /// <summary>핵심 재료가 그 일을 돕는 이유.</summary>
        public string MaterialRole { get; private set; }
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

        /// <summary>스토리 045(가벼운 경제). 완성된 건물 하나가 바퀴 정산마다 버는 마르크(_income).</summary>
        public int Income { get; private set; }
        /// <summary>스토리 042. 주거 칸(사는 사람 수, _housing).</summary>
        public int Housing { get; private set; }
        /// <summary>스토리 042. 빛(가스등 계열, _light).</summary>
        public int Light { get; private set; }

        /// <summary>QA 후속(건물 장식). 완성 건물 꾸밈(_decor, 없으면 _buildingStyle 기본값). 파서를 거치지 않은 종류는 null.</summary>
        public HabitatDecor Decor { get; private set; }

        /// <summary>QA 후속(건물 장식). 카탈로그 파서가 꾸밈을 붙인다.</summary>
        internal void SetDecor(HabitatDecor decor) => Decor = decor;

        /// <summary>recipe의 마을 일과 재료 역할을 도감에 표시할 설명으로 저장한다.</summary>
        internal void SetPurpose(string villageWork, string materialRole)
        {
            VillageWork = villageWork ?? string.Empty;
            MaterialRole = materialRole ?? string.Empty;
        }

        /// <summary>스토리 045. 카탈로그 파서가 수입·주거·빛을 붙인다.</summary>
        internal void SetEconomy(int income, int housing, int light)
        {
            if (income < 0 || housing < 0 || light < 0) throw new ArgumentException("Habitat economy numbers must be >= 0: " + Id);
            Income = income; Housing = housing; Light = light;
        }

        /// <summary>스토리 027. 카탈로그 파서가 건물 외관 데이터를 붙인다.</summary>
        internal void SetBuilding(string style, string signProp)
        {
            BuildingStyle = string.IsNullOrWhiteSpace(style) ? null : style.Trim();
            SignProp = string.IsNullOrWhiteSpace(signProp) ? null : signProp.Trim();
        }

        /// <summary>
        /// id와 tileItemId를 포함한 묶음 설정을 받아 최대 9칸의 동일 재료 시설을 만든다.
        /// 검증된 HabitatType을 반환한다.
        /// </summary>
        public static HabitatType CreateCluster(string id, string name, string tileItemId, int requiredCount, float prosperityBonus, string npcTypeId)
        {
            if (string.IsNullOrWhiteSpace(tileItemId)) throw new ArgumentException("Cluster habitat needs a tile item id: " + id);
            if (requiredCount < 1) throw new ArgumentException("Cluster habitat needs requiredCount >= 1: " + id);
            if (requiredCount > MAX_RECIPE_CELLS) throw new ArgumentException("Cluster habitat exceeds 9 cells: " + id);
            var type = new HabitatType(id, name, HabitatKind.Cluster, tileItemId, requiredCount, Array.Empty<IReadOnlyList<string>>(), prosperityBonus, npcTypeId);
            type.SetTiles = Array.AsReadOnly(new[] { new HabitatTileRequirement(tileItemId, requiredCount) });
            return type;
        }

        /// <summary>
        /// 스토리 020. Set(혼합 묶음) 종류를 만든다. tiles는 서로 다른 타일 id여야 하며, requiredCount(총 칸 수)는 1 이상이고
        /// 타일별 최소 수의 합보다 작을 수 없고 최대 9칸이다. 검증된 HabitatType을 반환한다.
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
            if (requiredCount > MAX_RECIPE_CELLS) throw new ArgumentException("Set habitat exceeds 9 cells: " + id);
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

        /// <summary>
        /// rows의 공백 구분 타일을 격자로 읽고 최대 9개 재료 칸의 패턴 시설을 만든다.
        /// 검증된 HabitatType을 반환한다.
        /// </summary>
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
            if (required > MAX_RECIPE_CELLS) throw new ArgumentException("Pattern habitat exceeds 9 cells: " + id);
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

    /// <summary>서식지 종류 카탈로그(레시피 JSON의 데이터 순서 유지). Parse가 두 JSON을 id로 결합한다.</summary>
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

        /// <summary>
        /// types 목록을 읽어 시설 ID 중복을 검증하고 카탈로그에 등록한다.
        /// 반환값은 없으며 콘텐츠 리셋 중에는 빈 목록을 그대로 유지한다.
        /// </summary>
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

        /// <summary>건물 설정과 별도 레시피 리소스를 읽어 카탈로그를 만든다.</summary>
        public static HabitatCatalog Parse(string json)
        {
            TextAsset recipes = Resources.Load<TextAsset>("Habitats/HabitatRecipes");
            if (recipes == null) throw new InvalidOperationException("Missing habitat recipes resource: Habitats/HabitatRecipes");
            return Parse(json, recipes.text);
        }

        /// <summary>건물 설정 json과 recipeJson을 id로 결합해 카탈로그를 만든다.</summary>
        public static HabitatCatalog Parse(string json, string recipeJson)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Habitat catalog json is required.", nameof(json));
            if (string.IsNullOrWhiteSpace(recipeJson)) throw new ArgumentException("Habitat recipes json is required.", nameof(recipeJson));
            var data = JsonUtility.FromJson<CatalogData>(json);
            if (data == null || data._types == null) throw new ArgumentException("Invalid habitat catalog json.", nameof(json));
            var recipeData = JsonUtility.FromJson<RecipeCatalogData>(recipeJson);
            if (recipeData == null || recipeData._recipes == null) throw new ArgumentException("Invalid habitat recipes json.", nameof(recipeJson));
            var metadata = new Dictionary<string, TypeData>(StringComparer.Ordinal);
            foreach (TypeData type in data._types)
            {
                if (type == null || string.IsNullOrWhiteSpace(type._id) || !metadata.TryAdd(type._id, type))
                    throw new ArgumentException("Invalid or duplicate habitat metadata id.", nameof(json));
            }
            var types = new List<HabitatType>();
            foreach (RecipeData recipe in recipeData._recipes)
            {
                if (recipe == null || !metadata.TryGetValue(recipe._id, out TypeData t))
                    throw new ArgumentException("Recipe has no matching habitat metadata: " + recipe?._id, nameof(recipeJson));
                string kind = string.IsNullOrWhiteSpace(recipe._kind) ? "cluster" : recipe._kind.Trim().ToLowerInvariant();
                if (kind == "cluster") types.Add(HabitatType.CreateCluster(recipe._id, t._name, recipe._tileItemId, recipe._requiredCount, t._prosperityBonus, t._npcTypeId));
                else if (kind == "pattern") types.Add(HabitatType.CreatePattern(recipe._id, t._name, recipe._pattern, t._prosperityBonus, t._npcTypeId));
                else if (kind == "set")
                {
                    var tiles = new List<HabitatTileRequirement>();
                    foreach (TileData tile in recipe._tiles ?? Array.Empty<TileData>())
                    {
                        if (tile == null) throw new ArgumentException("Null set tile on " + recipe._id, nameof(recipeJson));
                        tiles.Add(new HabitatTileRequirement(tile._tileItemId, tile._minCount));
                    }
                    types.Add(HabitatType.CreateSet(recipe._id, t._name, tiles, recipe._requiredCount, t._prosperityBonus, t._npcTypeId));
                }
                else throw new ArgumentException("Unknown recipe kind " + recipe._kind + " on " + recipe._id, nameof(recipeJson));
                types[types.Count - 1].SetPurpose(recipe._villageWork, recipe._machineRole);
                types[types.Count - 1].SetBuilding(t._buildingStyle, t._signProp);
                types[types.Count - 1].SetEconomy(t._income, t._housing, t._light);
                bool hasDecor = t._decor != null && (!string.IsNullOrWhiteSpace(t._decor._roofTint) || t._decor._windows > 0 || (t._decor._props != null && t._decor._props.Length > 0));
                types[types.Count - 1].SetDecor(hasDecor ? new HabitatDecor(t._decor._roofTint, t._decor._windows, t._decor._props) : HabitatDecor.DefaultFor(types[types.Count - 1].BuildingStyle));
            }
            return new HabitatCatalog(types) { LockCompleted = data._lockCompleted };
        }

#pragma warning disable 0649
        [Serializable] private sealed class CatalogData { public TypeData[] _types; public bool _lockCompleted = true; }
        [Serializable]
        private sealed class TypeData
        {
            public string _id; public string _name; public float _prosperityBonus; public string _npcTypeId;
            public string _buildingStyle; public string _signProp;
            public int _income; public int _housing; public int _light;
            public DecorData _decor;
        }
        [Serializable] private sealed class RecipeCatalogData { public RecipeData[] _recipes; }
        [Serializable] private sealed class RecipeData
        {
            public string _id; public string _kind; public string _tileItemId; public int _requiredCount;
            public string _villageWork; public string _machineRole;
            public string[] _pattern; public TileData[] _tiles;
        }
        [Serializable] private sealed class DecorData { public string _roofTint; public int _windows; public string[] _props; }
        [Serializable] private sealed class TileData { public string _tileItemId; public int _minCount; }
#pragma warning restore 0649
    }
}
