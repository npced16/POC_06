using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Habitats
{
    /// <summary>
    /// 해금 데이터(Resources/Habitats/CodexUnlocks.json). 시작 타일과 건물 첫 완성으로 여는 타일을 읽는다.
    /// 스토리 021의 시작 레시피·해금 순서·바퀴 개수(_starters/_unlockOrder/_perLap)는 읽기만 하고 HabitatCodex가 쓰지 않는다.
    /// </summary>
    public sealed class CodexUnlockSettings
    {
        /// <summary>옛 데이터(스토리 021). 스토리 050부터 무시.</summary>
        public IReadOnlyList<string> Starters { get; }
        /// <summary>옛 데이터(스토리 021). 스토리 050부터 무시.</summary>
        public IReadOnlyList<string> UnlockOrder { get; }
        /// <summary>옛 데이터(스토리 021). 스토리 050부터 무시.</summary>
        public int PerLap { get; }
        /// <summary>경유지 이벤트 하나를 해결할 때 여는 개수(스토리 039 떠돌이 상인 몫은 따로 더한다).</summary>
        public int PerEvent { get; }
        /// <summary>전투 하나를 이길 때 여는 개수(코드 경로 없음, 데이터 호환용).</summary>
        public int PerVictory { get; }

        /// <summary>스토리 050. 처음부터 열린 타일(_starterTiles). 비어 있으면 타일 잠금을 쓰지 않는다(모든 타일 열림).</summary>
        public IReadOnlyList<string> StarterTiles { get; }
        /// <summary>스토리 050. 건물(레시피) id → 처음 완성하면 여는 타일(_tileUnlocks, 데이터 순서).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> TileUnlocks { get; }
        /// <summary>옛 레시피 선행 건물 데이터. 발견 판정에는 사용하지 않는다.</summary>
        public IReadOnlyDictionary<string, string> RecipeRequires { get; }
        /// <summary>스토리 050. 해금 토스트 표시 시간(초).</summary>
        public float ToastSeconds { get; }

        /// <summary>값으로 직접 만든다(옛 생성자, 타일 잠금 없음).</summary>
        public CodexUnlockSettings(IEnumerable<string> starters, IEnumerable<string> unlockOrder, int perLap, int perEvent, int perVictory)
            : this(starters, unlockOrder, perLap, perEvent, perVictory, null, null, null, 4f) { }

        /// <summary>스토리 050. 조합 해금 값까지 직접 만든다.</summary>
        public CodexUnlockSettings(IEnumerable<string> starters, IEnumerable<string> unlockOrder, int perLap, int perEvent, int perVictory,
            IEnumerable<string> starterTiles, IDictionary<string, IReadOnlyList<string>> tileUnlocks, IDictionary<string, string> recipeRequires, float toastSeconds)
        {
            Starters = new List<string>(starters ?? Array.Empty<string>());
            UnlockOrder = new List<string>(unlockOrder ?? Array.Empty<string>());
            PerLap = Math.Max(0, perLap);
            PerEvent = Math.Max(0, perEvent);
            PerVictory = Math.Max(0, perVictory);
            StarterTiles = new List<string>(starterTiles ?? Array.Empty<string>());
            TileUnlocks = tileUnlocks != null ? new Dictionary<string, IReadOnlyList<string>>(tileUnlocks) : new Dictionary<string, IReadOnlyList<string>>();
            RecipeRequires = recipeRequires != null ? new Dictionary<string, string>(recipeRequires) : new Dictionary<string, string>();
            ToastSeconds = Math.Max(0.5f, toastSeconds);
        }

        /// <summary>
        /// json의 시작 재료와 시설별 첫 완성 보상을 읽고 시설당 보상이 한 종류인지 검증한다.
        /// 검증된 CodexUnlockSettings를 반환하며 보상이 여러 종류면 예외를 던진다.
        /// </summary>
        public static CodexUnlockSettings Parse(string json)
        {
            Data data = JsonUtility.FromJson<Data>(json) ?? new Data();
            var tiles = new Dictionary<string, IReadOnlyList<string>>();
            foreach (TileUnlockData t in data._tileUnlocks ?? Array.Empty<TileUnlockData>())
            {
                if (t == null || string.IsNullOrEmpty(t._recipe)) continue;
                if (t._tiles != null && t._tiles.Length > 1)
                    throw new ArgumentException("Each facility may unlock only one material: " + t._recipe, nameof(json));
                tiles[t._recipe] = new List<string>(t._tiles ?? Array.Empty<string>());
            }
            var requires = new Dictionary<string, string>();
            foreach (RequireData r in data._recipeRequires ?? Array.Empty<RequireData>())
                if (r != null && !string.IsNullOrEmpty(r._recipe) && !string.IsNullOrEmpty(r._requires)) requires[r._recipe] = r._requires;
            return new CodexUnlockSettings(data._starters, data._unlockOrder, data._perLap, data._perEvent, data._perVictory,
                data._starterTiles, tiles, requires, data._toastSeconds);
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class Data
        {
            public string[] _starters;
            public string[] _unlockOrder;
            public int _perLap;
            public int _perEvent;
            public int _perVictory;
            public string[] _starterTiles;
            public TileUnlockData[] _tileUnlocks;
            public RequireData[] _recipeRequires;
            public float _toastSeconds = 4f;
        }

        [Serializable]
        private sealed class TileUnlockData
        {
            public string _recipe;
            public string[] _tiles;
        }

        [Serializable]
        private sealed class RequireData
        {
            public string _recipe;
            public string _requires;
        }
#pragma warning restore 0649
    }

    /// <summary>
    /// 레시피 도감 + 타일 해금(IHabitatCodex). design/game-brief.md, 스토리 050 "조합 해금".
    /// 타일: 시작 타일(_starterTiles)만 열려 있고, 건물을 (어느 마을에서든) 처음 완성하면(IHabitatClusters.HabitatCompleted) 그 건물의 _tileUnlocks 타일이
    /// 열린다(TileUnlocked 1회). 같은 건물을 두 번째로 지으면 아무 일도 없다. 해금 데이터가 다루지 않는 아이템(장식 등)은 잠기지 않는다.
    /// 레시피는 건물을 처음 직접 완성했을 때만 "discover"로 알려진다. 열린 재료, 승객, NPC, 사건은 레시피를 등록하지 않는다.
    /// 스토리 021의 시작 레시피·바퀴 해금과 스토리 042의 등급 해금은 더 쓰지 않는다(OnLapCompleted·BindTiers는 호환용으로 남음).
    /// </summary>
    public sealed class HabitatCodex : IHabitatCodex, IDisposable
    {
        public const string SOURCE_START = "start";
        public const string SOURCE_LAP = "lap";
        public const string SOURCE_EVENT = "event";
        public const string SOURCE_VICTORY = "victory";
        public const string SOURCE_DISCOVER = "discover";
        public const string SOURCE_BOARD = "board";
        public const string SOURCE_TEACH = "teach";
        /// <summary>스토리 042. 마을 등급이 연 레시피(스토리 050부터 쓰지 않음).</summary>
        public const string SOURCE_TIER = "tier";
        /// <summary>옛 자동 레시피 등록 출처. 현재 사용하지 않는다.</summary>
        public const string SOURCE_COMBO = "combo";

        private readonly HabitatCatalog _catalog;
        private readonly CodexUnlockSettings _settings;
        private readonly ITrackRuntime _track;
        private readonly IWaypointResolver _waypoints;
        private readonly IHabitatClusters _clusters;
        private readonly List<string> _unlocked = new List<string>();
        private readonly HashSet<string> _known = new HashSet<string>();
        private readonly Dictionary<string, string> _sourceOf = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _npcOf = new Dictionary<string, string>();
        private readonly List<string> _managedTiles = new List<string>();
        private readonly HashSet<string> _managedSet = new HashSet<string>();
        private readonly List<string> _openTiles = new List<string>();
        private readonly HashSet<string> _openSet = new HashSet<string>();
        private readonly List<string> _builtOrder = new List<string>();
        private readonly HashSet<string> _built = new HashSet<string>();
        private readonly List<string> _hinted = new List<string>();
        private readonly HashSet<string> _hintedSet = new HashSet<string>();
        private IPassengers _passengers;
        private INpcRecruiter _recruiter;
        private Func<string, string> _wantedOf;
        private Func<string, string> _teachesOf;

        /// <inheritdoc />
        public event Action<string, string> RecipeUnlocked;

        /// <summary>한 바퀴를 돌아 typeId의 설계 단서를 얻었을 때 알린다.</summary>
        public event Action<string> HintUnlocked;

        /// <inheritdoc />
        public event Action<IReadOnlyList<string>, string> TileUnlocked;

        /// <inheritdoc />
        public event Action<IReadOnlyList<string>, string> RecipesLearned { add { } remove { } }

        /// <inheritdoc />
        public IReadOnlyList<string> UnlockedTypeIds => _unlocked;

        /// <summary>카탈로그 종류 수(도감 분모).</summary>
        public int TotalCount => _catalog.Types.Count;

        /// <inheritdoc />
        public IReadOnlyList<string> UnlockedTileIds => _openTiles;

        /// <inheritdoc />
        public IReadOnlyList<string> ManagedTileIds => _managedTiles;

        /// <summary>한 번이라도 완성한 건물 id(처음 완성 순서).</summary>
        public IReadOnlyList<string> BuiltTypeIds => _builtOrder;

        /// <summary>선로에서 얻은 설계 단서 id를 획득 순서로 돌려준다.</summary>
        public IReadOnlyList<string> HintedTypeIds => _hinted;

        /// <summary>typeId의 제작 단서를 이미 얻었으면 true를 반환한다.</summary>
        public bool HasHint(string typeId) => typeId != null && _hintedSet.Contains(typeId);

        /// <summary>해금 데이터(토스트 시간 등).</summary>
        public CodexUnlockSettings Settings => _settings;

        /// <summary>track·waypoints·clusters는 없으면 그 출처만 빠진다.</summary>
        public HabitatCodex(HabitatCatalog catalog, CodexUnlockSettings settings, ITrackRuntime track, IWaypointResolver waypoints, IHabitatClusters clusters)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _track = track;
            _waypoints = waypoints;
            _clusters = clusters;
            BuildTileTable();
            ValidateData();
            foreach (string id in _settings.StarterTiles) OpenTile(id);
            if (_track != null) _track.LapCompleted += OnLapCompleted;
            if (_clusters != null) _clusters.HabitatCompleted += OnHabitatCompleted;
        }

        private void BuildTileTable()
        {
            foreach (string id in _settings.StarterTiles) AddManaged(id);
            foreach (KeyValuePair<string, IReadOnlyList<string>> pair in _settings.TileUnlocks)
                foreach (string id in pair.Value) AddManaged(id);
        }

        private void AddManaged(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId) && _managedSet.Add(itemId)) _managedTiles.Add(itemId);
        }

        private void ValidateData()
        {
            var unknown = new List<string>();
            foreach (string id in _settings.TileUnlocks.Keys) if (_catalog.Get(id) == null) unknown.Add(id);
            foreach (KeyValuePair<string, string> pair in _settings.RecipeRequires)
            {
                if (_catalog.Get(pair.Key) == null) unknown.Add(pair.Key);
                if (_catalog.Get(pair.Value) == null) unknown.Add(pair.Value);
            }
            if (unknown.Count > 0) UnityEngine.Debug.LogWarning("HabitatCodex: 카탈로그에 없는 레시피 id: " + string.Join(", ", unknown));
            if (_managedTiles.Count == 0) return;
            var unreachable = new List<string>();
            foreach (HabitatType type in _catalog.Types)
                foreach (string tile in type.TileIds())
                    if (!_managedSet.Contains(tile) && !unreachable.Contains(tile)) unreachable.Add(tile);
            if (unreachable.Count > 0) UnityEngine.Debug.LogWarning("HabitatCodex: 해금 데이터에 없는 레시피 타일(잠기지 않음): " + string.Join(", ", unreachable));
        }

        /// <inheritdoc />
        public bool IsUnlocked(string typeId) => typeId != null && _known.Contains(typeId);

        /// <inheritdoc />
        public bool IsTileUnlocked(string itemId) => itemId != null && (!_managedSet.Contains(itemId) || _openSet.Contains(itemId));

        /// <inheritdoc />
        public bool HasBuilt(string typeId) => typeId != null && _built.Contains(typeId);

        /// <inheritdoc />
        public IReadOnlyList<string> TilesUnlockedBy(string typeId) =>
            typeId != null && _settings.TileUnlocks.TryGetValue(typeId, out IReadOnlyList<string> tiles) ? tiles : Array.Empty<string>();

        /// <inheritdoc />
        public string RequiredRecipeOf(string typeId) => typeId != null && _settings.RecipeRequires.TryGetValue(typeId, out string r) ? r : null;

        /// <inheritdoc />
        public bool TryUnlock(string typeId, string source) => TryUnlock(typeId, source, null);

        /// <summary>typeId를 직접 완성해 발견했을 때만 도감에 등록한다. 새로 등록했으면 true를 반환한다.</summary>
        public bool TryUnlock(string typeId, string source, string npcId)
        {
            if (source != SOURCE_DISCOVER) return false;
            if (!Add(typeId)) return false;
            _sourceOf[typeId] = source;
            if (npcId != null) _npcOf[typeId] = npcId;
            RecipeUnlocked?.Invoke(typeId, source);
            return true;
        }

        /// <inheritdoc />
        public string UnlockSourceOf(string typeId) => typeId != null && _sourceOf.TryGetValue(typeId, out string s) ? s : null;

        /// <inheritdoc />
        public string UnlockNpcOf(string typeId) => typeId != null && _npcOf.TryGetValue(typeId, out string n) ? n : null;

        /// <summary>
        /// typeId 건물의 첫 완성을 기록하고 도감에 "discover"로 등록한 뒤 관련 타일을 연다.
        /// HabitatCompleted와 데모가 같은 경로로 호출하며, 두 번째 완성 또는 모르는 id는 false를 반환한다.
        /// </summary>
        public bool MarkBuilt(string typeId)
        {
            if (typeId == null || _catalog.Get(typeId) == null || !_built.Add(typeId)) return false;
            _builtOrder.Add(typeId);
            TryUnlock(typeId, SOURCE_DISCOVER);
            var opened = new List<string>();
            foreach (string tile in TilesUnlockedBy(typeId)) if (OpenTile(tile)) opened.Add(tile);
            if (opened.Count > 0) TileUnlocked?.Invoke(opened, typeId);
            return true;
        }

        private bool OpenTile(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_managedSet.Contains(itemId) || !_openSet.Add(itemId)) return false;
            _openTiles.Add(itemId);
            return true;
        }

        /// <summary>
        /// 스토리 030. NPC 해금 출처를 연결한다(다시 부르면 이전 연결을 푼다). wantedOf(npcId) = 원하는 서식지 종류, teachesOf(npcId) = 가르치는 종류.
        /// passengers·recruiter는 없으면 그 출처만 빠진다.
        /// </summary>
        public void BindNpcs(IPassengers passengers, INpcRecruiter recruiter, Func<string, string> wantedOf, Func<string, string> teachesOf)
        {
            UnbindNpcs();
            _passengers = passengers;
            _recruiter = recruiter;
            _wantedOf = wantedOf;
            _teachesOf = teachesOf;
        }

        private void UnbindNpcs()
        {
            if (_passengers != null) _passengers.Boarded -= OnBoarded;
            if (_recruiter != null) _recruiter.NpcJoined -= OnNpcJoined;
            _passengers = null;
            _recruiter = null;
        }

        /// <summary>승객이 탔다: 원하는 종류가 잠겨 있으면 "board"로 연다.</summary>
        private void OnBoarded(string npcId, string waypointId)
        {
            string typeId = _wantedOf?.Invoke(npcId);
            if (typeId != null && !IsUnlocked(typeId)) TryUnlock(typeId, SOURCE_BOARD, npcId);
        }

        /// <summary>NPC가 거점에 합류했다: 가르치는 종류가 잠겨 있으면 "teach"로 연다.</summary>
        private void OnNpcJoined(string hubId, string npcId)
        {
            string typeId = _teachesOf?.Invoke(npcId);
            if (typeId != null && !IsUnlocked(typeId)) TryUnlock(typeId, SOURCE_TEACH, npcId);
        }

        /// <summary>lap을 마친 뒤 만들 수 있는 미발견 시설의 설계 단서 하나를 공개한다.</summary>
        public void OnLapCompleted(int lap)
        {
            HabitatType next = null;
            foreach (HabitatType type in _catalog.Types)
            {
                if (_built.Contains(type.Id) || _hintedSet.Contains(type.Id)) continue;
                if (next == null) next = type;
                bool available = true;
                foreach (string tile in type.TileIds())
                    if (!IsTileUnlocked(tile)) { available = false; break; }
                if (!available) continue;
                next = type;
                break;
            }
            if (next == null || !_hintedSet.Add(next.Id)) return;
            _hinted.Add(next.Id);
            HintUnlocked?.Invoke(next.Id);
        }

        /// <summary>옛 자동 해금 경로. 레시피는 직접 완성해야 등록되므로 항상 0을 반환한다.</summary>
        public int UnlockNext(int count, string source)
        {
            return 0;
        }

        /// <summary>다음에 열릴 레시피(스토리 050: 카탈로그 순, _unlockOrder 무시). 다 알면 null.</summary>
        public string NextLocked()
        {
            foreach (HabitatType type in _catalog.Types) if (!_known.Contains(type.Id)) return type.Id;
            return null;
        }

        private bool Add(string typeId)
        {
            if (typeId == null || _catalog.Get(typeId) == null || !_known.Add(typeId)) return false;
            _unlocked.Add(typeId);
            return true;
        }

        private void OnEncounterEnded(ITrackNode node, EncounterOutcome outcome)
        {
            // 스토리 039: 휴식지 이벤트마다 PerEvent개, "떠돌이 상인"은 IWaypointResolver.CodexUnlocks만큼 더 연다.
            int count = _settings.PerEvent + (_waypoints?.CodexUnlocks ?? 0);
            if (outcome == EncounterOutcome.EventResolved && count > 0) UnlockNext(count, SOURCE_EVENT);
        }

        private void OnHabitatCompleted(string hubId, string typeId)
        {
            MarkBuilt(typeId);
        }

        /// <summary>
        /// 스토리 042 등급 해금 연결. 스토리 050: 등급은 레시피를 열지 않으므로 구독만 기록하고 아무것도 열지 않는다(호환용).
        /// </summary>
        public void BindTiers(ITownTier tiers) { }

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            if (_track != null) _track.LapCompleted -= OnLapCompleted;
            if (_clusters != null) _clusters.HabitatCompleted -= OnHabitatCompleted;
            UnbindNpcs();
            RecipeUnlocked = null;
            HintUnlocked = null;
            TileUnlocked = null;
        }
    }
}
