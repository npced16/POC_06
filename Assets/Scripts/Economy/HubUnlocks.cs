using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Economy
{
    /// <summary>
    /// 역 개통 규칙 묶음(Resources/Economy/HubUnlocks.json).
    /// 규칙마다 열 역과 모두 만족해야 하는 조건 목록을 가진다.
    /// </summary>
    [Serializable]
    public sealed class HubUnlockSettings
    {
        [SerializeField] private HubUnlockRule[] _rules;

        public IReadOnlyList<HubUnlockRule> Rules => _rules ?? Array.Empty<HubUnlockRule>();

        /// <summary>
        /// JSON 문자열을 읽어 규칙 묶음을 만들고 각 규칙을 검증한다.
        /// json을 사용하며, 검증된 HubUnlockSettings를 반환한다.
        /// </summary>
        public static HubUnlockSettings Parse(string json)
        {
            HubUnlockSettings settings = JsonUtility.FromJson<HubUnlockSettings>(json) ?? throw new ArgumentException("HubUnlocks.json is empty.");
            foreach (HubUnlockRule rule in settings.Rules) rule.Validate();
            return settings;
        }
    }

    /// <summary>역 하나를 여는 규칙. 조건을 모두 만족하면 _hubId 역을 연다.</summary>
    [Serializable]
    public sealed class HubUnlockRule
    {
        [SerializeField] private string _hubId;
        [SerializeField] private HubUnlockCondition[] _conditions;

        public string HubId => _hubId;
        public IReadOnlyList<HubUnlockCondition> Conditions => _conditions ?? Array.Empty<HubUnlockCondition>();

        /// <summary>
        /// 열 역 id와 조건 목록이 비어 있지 않은지, 각 조건이 올바른지 확인한다.
        /// _hubId와 _conditions를 사용하며, 잘못되면 ArgumentException을 던진다.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(_hubId)) throw new ArgumentException("HubUnlocks.json rule has no _hubId.");
            if (Conditions.Count == 0) throw new ArgumentException($"HubUnlocks.json rule {_hubId} has no _conditions.");
            foreach (HubUnlockCondition condition in Conditions) condition.Validate(_hubId);
        }
    }

    /// <summary>조건 종류. 새 종류는 여기와 HubUnlockTracker.IsMet에 추가한다.</summary>
    public enum HubUnlockConditionKind { Habitat, DistinctHabitats }

    /// <summary>개통 조건 하나. _kind에 따라 읽는 필드가 다르다.</summary>
    [Serializable]
    public sealed class HubUnlockCondition
    {
        private const string KIND_HABITAT = "habitat";
        private const string KIND_DISTINCT_HABITATS = "distinct_habitats";

        [SerializeField] private string _kind;
        [SerializeField] private string _habitatId;
        [SerializeField] private int _count;
        [SerializeField] private string _sourceHubId;

        public HubUnlockConditionKind Kind => ParseKind(_kind);
        public string HabitatId => _habitatId;
        /// <summary>distinct_habitats 조건에서 필요한 서로 다른 건물 종류 수.</summary>
        public int Count => _count;
        /// <summary>건물을 완성해야 하는 마을. 비어 있으면 어느 마을이든 된다.</summary>
        public string SourceHubId => _sourceHubId;
        public bool AnySource => string.IsNullOrWhiteSpace(_sourceHubId);

        /// <summary>
        /// 조건 종류와 종류별 필수 필드를 확인한다.
        /// hubId는 오류 문구에만 쓰며, 잘못되면 ArgumentException을 던진다.
        /// </summary>
        public void Validate(string hubId)
        {
            HubUnlockConditionKind kind = ParseKind(_kind);
            if (kind == HubUnlockConditionKind.Habitat && string.IsNullOrWhiteSpace(_habitatId))
                throw new ArgumentException($"HubUnlocks.json rule {hubId} habitat condition has no _habitatId.");
            if (kind == HubUnlockConditionKind.DistinctHabitats && _count <= 0)
                throw new ArgumentException($"HubUnlocks.json rule {hubId} distinct_habitats condition needs _count > 0.");
        }

        /// <summary>
        /// JSON의 종류 문자열을 조건 종류로 바꾼다.
        /// kind를 사용하며, 모르는 문자열이면 ArgumentException을 던진다.
        /// </summary>
        private static HubUnlockConditionKind ParseKind(string kind)
        {
            if (kind == KIND_HABITAT) return HubUnlockConditionKind.Habitat;
            if (kind == KIND_DISTINCT_HABITATS) return HubUnlockConditionKind.DistinctHabitats;
            throw new ArgumentException($"HubUnlocks.json unknown condition _kind: {kind}");
        }
    }

    /// <summary>
    /// 건물 완성 기록을 모아 HubUnlocks 규칙을 판정하고, 조건을 모두 만족한 역을 ILineOpener로 연다.
    /// 완성 기록은 이번 런 동안만 유지한다. 건물이 나중에 부서져도 이미 만족한 조건은 취소하지 않는다.
    /// </summary>
    public sealed class HubUnlockTracker : IDisposable
    {
        private readonly HubUnlockSettings _settings;
        private readonly IHabitatClusters _clusters;
        private readonly ILineOpener _line;
        private readonly HashSet<string> _completedAnywhere = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _completedByHub = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> _none = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 규칙, 서식지 완성 신호, 선로 개통 서비스를 받아 완성 신호를 구독한다.
        /// openHubIds 마을에서 이미 완성된 건물을 기록하고, 모르는 건물 id는 경고로 남긴 뒤 한 번 판정한다.
        /// </summary>
        public HubUnlockTracker(HubUnlockSettings settings, IHabitatClusters clusters, ILineOpener line, IEnumerable<string> openHubIds)
        {
            _settings = settings;
            _clusters = clusters;
            _line = line;
            WarnUnknownHabitats();
            SeedCompleted(openHubIds);
            _clusters.HabitatCompleted += OnHabitatCompleted;
            Evaluate();
        }

        /// <summary>완성 신호 구독을 해제한다.</summary>
        public void Dispose()
        {
            _clusters.HabitatCompleted -= OnHabitatCompleted;
        }

        /// <summary>
        /// 새로 완성된 건물을 기록하고 규칙을 다시 판정한다.
        /// hubId와 typeId를 사용하며, 완성 기록을 변경한다.
        /// </summary>
        private void OnHabitatCompleted(string hubId, string typeId)
        {
            Record(hubId, typeId);
            Evaluate();
        }

        /// <summary>
        /// 아직 열리지 않은 역의 규칙 중 조건을 모두 만족한 규칙의 역을 연다.
        /// _settings와 완성 기록을 사용하며, ILineOpener의 열린 역 상태를 변경한다.
        /// </summary>
        private void Evaluate()
        {
            foreach (HubUnlockRule rule in _settings.Rules)
            {
                if (_line.IsOpen(rule.HubId) || !AllMet(rule)) continue;
                Debug.Log($"LINE: unlock rule met hub={rule.HubId}");
                _line.Open(rule.HubId);
            }
        }

        /// <summary>
        /// 규칙의 조건을 모두 만족했는지 확인한다.
        /// rule을 사용하며, 모두 만족하면 true를 반환한다.
        /// </summary>
        private bool AllMet(HubUnlockRule rule)
        {
            foreach (HubUnlockCondition condition in rule.Conditions)
                if (!IsMet(condition)) return false;
            return true;
        }

        /// <summary>
        /// 조건 하나를 종류에 맞게 판정한다.
        /// condition과 완성 기록을 사용하며, 만족하면 true를 반환한다.
        /// </summary>
        private bool IsMet(HubUnlockCondition condition)
        {
            switch (condition.Kind)
            {
                case HubUnlockConditionKind.Habitat:
                    return CompletedTypes(condition).Contains(condition.HabitatId);
                case HubUnlockConditionKind.DistinctHabitats:
                    return CompletedTypes(condition).Count >= condition.Count;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 건물 완성을 마을 무관 기록과 마을별 기록에 추가한다.
        /// hubId와 typeId를 사용하며, 두 완성 기록을 변경한다.
        /// </summary>
        private void Record(string hubId, string typeId)
        {
            _completedAnywhere.Add(typeId);
            if (!_completedByHub.TryGetValue(hubId, out HashSet<string> types))
                _completedByHub[hubId] = types = new HashSet<string>(StringComparer.Ordinal);
            types.Add(typeId);
        }

        /// <summary>
        /// 조건이 보는 범위에서 한 번이라도 완성된 건물 종류 집합을 돌려준다.
        /// condition의 _sourceHubId를 사용하며, 비어 있으면 모든 마을 합산 집합을 반환한다.
        /// </summary>
        private HashSet<string> CompletedTypes(HubUnlockCondition condition)
        {
            if (condition.AnySource) return _completedAnywhere;
            return _completedByHub.TryGetValue(condition.SourceHubId, out HashSet<string> types) ? types : _none;
        }

        /// <summary>
        /// 조건에 나온 마을과 openHubIds 마을에서 지금 완성된 건물을 기록한다.
        /// IHabitatClusters.CompletedAt을 사용하며, 완성 기록을 변경한다.
        /// </summary>
        private void SeedCompleted(IEnumerable<string> openHubIds)
        {
            HashSet<string> hubs = new HashSet<string>(openHubIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (HubUnlockRule rule in _settings.Rules)
                foreach (HubUnlockCondition condition in rule.Conditions)
                    if (!condition.AnySource) hubs.Add(condition.SourceHubId);
            foreach (string hub in hubs)
                foreach (HabitatCluster cluster in _clusters.CompletedAt(hub) ?? Array.Empty<HabitatCluster>())
                    Record(hub, cluster.TypeId);
        }

        /// <summary>
        /// 조건의 건물 id가 서식지 카탈로그에 없으면 경고를 남긴다.
        /// IHabitatClusters.HabitatTypeIds를 사용하며, 상태는 바꾸지 않는다.
        /// </summary>
        private void WarnUnknownHabitats()
        {
            HashSet<string> known = new HashSet<string>(_clusters.HabitatTypeIds, StringComparer.Ordinal);
            foreach (HubUnlockRule rule in _settings.Rules)
                foreach (HubUnlockCondition condition in rule.Conditions)
                    if (condition.Kind == HubUnlockConditionKind.Habitat && !known.Contains(condition.HabitatId))
                        Debug.LogWarning($"LINE: HubUnlocks.json rule {rule.HubId} uses unknown habitat {condition.HabitatId}");
        }
    }
}
