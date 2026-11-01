using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Habitats
{
    /// <summary>스토리 021 해금 데이터(Resources/Habitats/CodexUnlocks.json): 시작 레시피, 해금 순서, 출처별 한 번에 여는 개수.</summary>
    public sealed class CodexUnlockSettings
    {
        /// <summary>런 시작 때 아는 레시피.</summary>
        public IReadOnlyList<string> Starters { get; }
        /// <summary>바퀴·이벤트·승리가 차례로 여는 순서.</summary>
        public IReadOnlyList<string> UnlockOrder { get; }
        /// <summary>바퀴 하나를 채울 때 여는 개수.</summary>
        public int PerLap { get; }
        /// <summary>경유지 이벤트 하나를 해결할 때 여는 개수.</summary>
        public int PerEvent { get; }
        /// <summary>전투 하나를 이길 때 여는 개수.</summary>
        public int PerVictory { get; }

        /// <summary>값으로 직접 만든다(테스트·데모용).</summary>
        public CodexUnlockSettings(IEnumerable<string> starters, IEnumerable<string> unlockOrder, int perLap, int perEvent, int perVictory)
        {
            Starters = new List<string>(starters ?? Array.Empty<string>());
            UnlockOrder = new List<string>(unlockOrder ?? Array.Empty<string>());
            PerLap = Math.Max(0, perLap);
            PerEvent = Math.Max(0, perEvent);
            PerVictory = Math.Max(0, perVictory);
        }

        /// <summary>JSON 텍스트를 읽는다.</summary>
        public static CodexUnlockSettings Parse(string json)
        {
            Data data = JsonUtility.FromJson<Data>(json) ?? new Data();
            return new CodexUnlockSettings(data._starters, data._unlockOrder, data._perLap, data._perEvent, data._perVictory);
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class Data
        {
            public string[] _starters;
            public string[] _unlockOrder;
            public int _perLap = 1;
            public int _perEvent = 1;
            public int _perVictory = 1;
        }
#pragma warning restore 0649
    }

    /// <summary>
    /// 스토리 021. 레시피 도감(IHabitatCodex). 생성 때 시작 레시피를 조용히 해금하고(토스트 없음), ITrackRuntime.LapCompleted마다 PerLap개,
    /// IWaypointResolver.EncounterEnded가 EventResolved면 PerEvent개, Victory면 PerVictory개를 순서 목록에서 차례로 연다(패배는 없음).
    /// 순서 목록이 다 열렸는데 아직 모르는 카탈로그 종류가 있으면 카탈로그 순서로 연다. 모든 종류를 알면 더 열지 않는다.
    /// IHabitatClusters.HabitatCompleted가 모르는 종류면 "discover"로 해금한다(완성 판정은 시스템이 이미 인정했다).
    /// </summary>
    public sealed class HabitatCodex : IHabitatCodex, IDisposable
    {
        public const string SOURCE_START = "start";
        public const string SOURCE_LAP = "lap";
        public const string SOURCE_EVENT = "event";
        public const string SOURCE_VICTORY = "victory";
        public const string SOURCE_DISCOVER = "discover";

        private readonly HabitatCatalog _catalog;
        private readonly CodexUnlockSettings _settings;
        private readonly ITrackRuntime _track;
        private readonly IWaypointResolver _waypoints;
        private readonly IHabitatClusters _clusters;
        private readonly List<string> _unlocked = new List<string>();
        private readonly HashSet<string> _known = new HashSet<string>();

        /// <inheritdoc />
        public event Action<string, string> RecipeUnlocked;

        /// <inheritdoc />
        public IReadOnlyList<string> UnlockedTypeIds => _unlocked;

        /// <summary>카탈로그 종류 수(도감 분모).</summary>
        public int TotalCount => _catalog.Types.Count;

        /// <summary>track·waypoints·clusters는 없으면 그 출처만 빠진다.</summary>
        public HabitatCodex(HabitatCatalog catalog, CodexUnlockSettings settings, ITrackRuntime track, IWaypointResolver waypoints, IHabitatClusters clusters)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _track = track;
            _waypoints = waypoints;
            _clusters = clusters;
            var unknown = new List<string>();
            foreach (string id in _settings.Starters) if (_catalog.Get(id) == null) unknown.Add(id);
            foreach (string id in _settings.UnlockOrder) if (_catalog.Get(id) == null) unknown.Add(id);
            if (unknown.Count > 0) Debug.LogWarning("HabitatCodex: 카탈로그에 없는 레시피 id: " + string.Join(", ", unknown));
            foreach (string id in _settings.Starters) Add(id);
            if (_track != null) _track.LapCompleted += OnLapCompleted;
            if (_waypoints != null) _waypoints.EncounterEnded += OnEncounterEnded;
            if (_clusters != null) _clusters.HabitatCompleted += OnHabitatCompleted;
        }

        /// <inheritdoc />
        public bool IsUnlocked(string typeId) => typeId != null && _known.Contains(typeId);

        /// <inheritdoc />
        public bool TryUnlock(string typeId, string source)
        {
            if (!Add(typeId)) return false;
            RecipeUnlocked?.Invoke(typeId, source);
            return true;
        }

        /// <summary>바퀴 하나를 채웠을 때(ITrackRuntime.LapCompleted). 데모는 같은 경로로 바퀴를 흉내 낸다.</summary>
        public void OnLapCompleted(int lap) => UnlockNext(_settings.PerLap, SOURCE_LAP);

        /// <summary>순서 목록에서 아직 모르는 다음 레시피 count개를 source로 연다. 연 개수를 돌려준다.</summary>
        public int UnlockNext(int count, string source)
        {
            int opened = 0;
            while (opened < count)
            {
                string next = NextLocked();
                if (next == null) break;
                TryUnlock(next, source);
                opened++;
            }
            return opened;
        }

        /// <summary>다음에 열릴 레시피(순서 목록, 그다음 카탈로그 순). 다 알면 null.</summary>
        public string NextLocked()
        {
            foreach (string id in _settings.UnlockOrder) if (_catalog.Get(id) != null && !_known.Contains(id)) return id;
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
            if (outcome == EncounterOutcome.EventResolved) UnlockNext(_settings.PerEvent, SOURCE_EVENT);
            else if (outcome == EncounterOutcome.Victory) UnlockNext(_settings.PerVictory, SOURCE_VICTORY);
        }

        private void OnHabitatCompleted(string hubId, string typeId)
        {
            if (!IsUnlocked(typeId)) TryUnlock(typeId, SOURCE_DISCOVER);
        }

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            if (_track != null) _track.LapCompleted -= OnLapCompleted;
            if (_waypoints != null) _waypoints.EncounterEnded -= OnEncounterEnded;
            if (_clusters != null) _clusters.HabitatCompleted -= OnHabitatCompleted;
            RecipeUnlocked = null;
        }
    }
}
