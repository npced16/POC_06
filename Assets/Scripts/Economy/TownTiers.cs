using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 042 (design/game-brief.md MVP 3 "주민·노동·마을 등급"). Resources/Economy/TownTiers.json 수치.
    /// 주민 증감·빛 기준·붕괴 조건·결과 등급·등급 표를 담는다. 여기 기본값은 파일에 키가 없을 때만 쓴다.
    /// </summary>
    [Serializable]
    public sealed class TownSettings
    {
#pragma warning disable 0649
        [SerializeField] private string _hubId = "hub-1";
        [SerializeField] private int _startResidents = 3;
        [SerializeField] private int _minResidents = 1;
        [SerializeField] private int _baseHousing = 3;
        [SerializeField] private string _hubName = "황동골";
        [SerializeField] private int _growPerLap = 1;
        [SerializeField] private int _growPerLapLit = 2;
        [SerializeField] private int _shrinkPerLap = 1;
        [SerializeField] private float _lightPerResident = 0.5f;
        [SerializeField] private int _collapseResidents = 1;
        [SerializeField] private float _collapseProsperity;
        [SerializeField] private int _collapseLaps = 2;
        [SerializeField] private int _collapseMinTier = 1;
        [SerializeField] private int _victoryTier = 3;
        [SerializeField] private int _partialTier = 1;
        [SerializeField] private TownTierData[] _tiers;
        [SerializeField] private int _demoSettles = 3;
        [SerializeField] private string[] _demoGrowBuildings;
        [SerializeField] private int _demoTierResidents = 5;
        [SerializeField] private int _demoTierProsperity = 20;
#pragma warning restore 0649

        /// <summary>마을 거점 id(창고 거점과 같다).</summary>
        public string HubId => _hubId;
        /// <summary>시작 주민.</summary>
        public int StartResidents => Math.Max(MinResidents, _startResidents);
        /// <summary>최소 주민(이 아래로 줄지 않는다).</summary>
        public int MinResidents => Math.Max(0, _minResidents);
        /// <summary>건물 없이 있는 주거(폐허에 남은 집).</summary>
        public int BaseHousing => Math.Max(0, _baseHousing);
        /// <summary>마을 거점 표시 이름(토스트용).</summary>
        public string HubName => _hubName;
        /// <summary>빛이 충분할 때 증가량(빛 ≥ 주민 × _lightPerResident).</summary>
        public int GrowPerLapLit => Math.Max(0, _growPerLapLit);
        /// <summary>기본 증가량(주거 &gt; 주민).</summary>
        public int GrowPerLap => Math.Max(0, _growPerLap);
        /// <summary>감소량.</summary>
        public int ShrinkPerLap => Math.Max(0, _shrinkPerLap);
        /// <summary>주민 1명에 필요한 빛(_light) 양.</summary>
        public float LightPerResident => Math.Max(0f, _lightPerResident);
        /// <summary>붕괴 판정: 주민이 이 값 이하.</summary>
        public int CollapseResidents => _collapseResidents;
        /// <summary>붕괴 판정: 번영이 이 값 이하.</summary>
        public float CollapseProsperity => _collapseProsperity;
        /// <summary>붕괴 판정: 두 조건이 이어진 정산 수.</summary>
        public int CollapseLaps => Math.Max(1, _collapseLaps);
        /// <summary>붕괴는 이 등급에 한 번이라도 오른 뒤에만 일어난다.</summary>
        public int CollapseMinTier => _collapseMinTier;
        /// <summary>승리 등급(도달하면 승리).</summary>
        public int VictoryTier => _victoryTier;
        /// <summary>기한 종료 때 부분 성공 최소 등급.</summary>
        public int PartialTier => _partialTier;
        /// <summary>등급 표(0부터).</summary>
        public IReadOnlyList<TownTierData> Tiers => _tiers ?? Array.Empty<TownTierData>();
        /// <summary>데모: 정산 횟수.</summary>
        public int DemoSettles => Math.Max(1, _demoSettles);
        /// <summary>데모 grow: 가상으로 세우는 건물 종류.</summary>
        public IReadOnlyList<string> DemoGrowBuildings => _demoGrowBuildings ?? Array.Empty<string>();
        /// <summary>데모 tier: 주민 시드.</summary>
        public int DemoTierResidents => _demoTierResidents;
        /// <summary>데모 tier: 번영 시드.</summary>
        public int DemoTierProsperity => _demoTierProsperity;

        /// <summary>JSON을 읽는다. 등급이 하나도 없으면 예외.</summary>
        public static TownSettings Parse(string json)
        {
            TownSettings s = JsonUtility.FromJson<TownSettings>(json) ?? throw new ArgumentException("TownTiers.json is empty.");
            if (s.Tiers.Count == 0) throw new ArgumentException("TownTiers.json has no _tiers.");
            return s;
        }
    }

    /// <summary>스토리 042. 등급 한 줄(TownTiers.json _tiers).</summary>
    [Serializable]
    public sealed class TownTierData
    {
#pragma warning disable 0649
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private int _residents;
        [SerializeField] private int _prosperity;
        [SerializeField] private string _phase;
        [SerializeField] private string[] _unlockRecipes;
        [SerializeField] private string[] _unlockUpgrades;
        [SerializeField] private string[] _unlockStops;
        [SerializeField] private string[] _unlockHubs;
#pragma warning restore 0649
        /// <summary>등급 id.</summary>
        public string Id => _id;
        /// <summary>표시 이름.</summary>
        public string Name => _name;
        /// <summary>필요 주민.</summary>
        public int Residents => _residents;
        /// <summary>필요 번영.</summary>
        public int Prosperity => _prosperity;
        /// <summary>사상되는 HubPhase(이름이 틀리면 Ruined).</summary>
        public HubPhase Phase => Enum.TryParse(_phase, out HubPhase p) ? p : HubPhase.Ruined;
        /// <summary>옛 데이터(스토리 042). 스토리 050부터 TownTiers는 이 목록을 무시한다.</summary>
        public IReadOnlyList<string> UnlockRecipes => _unlockRecipes ?? Array.Empty<string>();
        /// <summary>이 등급이 여는 열차 업그레이드(043).</summary>
        public IReadOnlyList<string> UnlockUpgrades => _unlockUpgrades ?? Array.Empty<string>();
        /// <summary>이 등급이 여는 휴식지(043).</summary>
        public IReadOnlyList<string> UnlockStops => _unlockStops ?? Array.Empty<string>();
        /// <summary>스토리 048. 이 등급(첫 마을)이 여는 역(ILineOpener.Open).</summary>
        public IReadOnlyList<string> UnlockHubs => _unlockHubs ?? Array.Empty<string>();
    }

    /// <summary>
    /// 스토리 042·048. 마을 등급(ITownTier), 열린 마을마다 따로. 순수 C#. 정산마다 마을별 EvaluateAt(거점, 주민, 번영)을 부른다.
    /// 상승: 다음 등급의 주민·번영을 모두 채우면 한 단계씩 오른다(한 정산에 여러 단계 가능, 단계마다 HubTierChanged; 첫 마을은 TierChanged도). 내려가지 않는다.
    /// 해금: 업그레이드·휴식지는 어느 마을이 올라도 전역으로 쌓인다(스토리 050: 레시피는 열지 않음)(등급 0의 해금은 생성 때 열림). _unlockHubs는 첫 마을(_hubId) 등급에서만
    /// HubUnlockRequested로 낸다(설치자가 ILineOpener.Open).
    /// 붕괴(마을마다): 등급이 CollapseMinTier 이상인데 주민 ≤ CollapseResidents이고 번영 ≤ CollapseProsperity인 정산이 CollapseLaps번 이어지면 그 마을 붕괴(끝).
    /// 첫 마을 붕괴만 Collapsing(RunState 패배), 모든 마을은 HubCollapsing.
    /// 048: Tier·TierId·TierName·IsMax·NextTier·Next*Need는 "현재 마을"(CurrentHub, 없으면 _hubId). Collapsed·Phase는 첫 마을(옛 의미; 마을별은 *At).
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class TownTiers : ITownTier, ISaveParticipant
    {
        private sealed class HubTier { public int Tier; public int LowLaps; public bool Collapsed; }

        private readonly TownSettings _settings;
        private readonly List<string> _recipes = new List<string>();
        private readonly List<string> _upgrades = new List<string>();
        private readonly List<string> _stops = new List<string>();
        private readonly List<string> _unlockedHubs = new List<string>();
        private readonly Dictionary<string, HubTier> _hubs = new Dictionary<string, HubTier>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        /// <summary>스토리 048. "현재 마을" 공급자(설치자가 건다). null이거나 모르는 id면 _hubId.</summary>
        public Func<string> CurrentHub { get; set; }
        /// <inheritdoc />
        public string CurrentHubId { get { string id = CurrentHub?.Invoke(); return id != null && _hubs.ContainsKey(id) ? id : _settings.HubId; } }
        /// <summary>스토리 048. 등급을 세는 마을 id(열린 순서).</summary>
        public IReadOnlyList<string> HubIds => _order;
        /// <inheritdoc />
        public int Tier => TierAt(CurrentHubId);
        /// <inheritdoc />
        public string TierId => Data(Tier).Id;
        /// <inheritdoc />
        public string TierName => Data(Tier).Name;
        /// <inheritdoc />
        public bool IsMax => Tier >= _settings.Tiers.Count - 1;
        /// <inheritdoc />
        public int NextTier => IsMax ? Tier : Tier + 1;
        /// <inheritdoc />
        public int NextResidentsNeed => IsMax ? 0 : Data(Tier + 1).Residents;
        /// <inheritdoc />
        public int NextProsperityNeed => IsMax ? 0 : Data(Tier + 1).Prosperity;
        /// <summary>스토리 050: 등급은 레시피를 열지 않으므로 늘 비어 있다(계약 호환용).</summary>
        public IReadOnlyList<string> UnlockedRecipeIds => _recipes;
        /// <inheritdoc />
        public IReadOnlyList<string> UnlockedUpgradeIds => _upgrades;
        /// <summary>지금까지 연 휴식지(043).</summary>
        public IReadOnlyList<string> UnlockedStopIds => _stops;
        /// <summary>스토리 048. 첫 마을 등급이 연 역(_unlockHubs 누적).</summary>
        public IReadOnlyList<string> UnlockedHubIds => _unlockedHubs;
        /// <summary>첫 마을이 붕괴했는지(끝).</summary>
        public bool Collapsed => CollapsedAt(_settings.HubId);
        /// <summary>첫 마을 거점 단계 사상.</summary>
        public HubPhase Phase => PhaseAt(_settings.HubId);
        /// <summary>승리 등급(데이터).</summary>
        public int VictoryTier => _settings.VictoryTier;
        /// <summary>부분 성공 최소 등급(데이터).</summary>
        public int PartialTier => _settings.PartialTier;
        /// <summary>등급 이름(번호로).</summary>
        public string NameOf(int tier) => Data(tier).Name;
        /// <summary>등급 데이터(번호로, 범위 밖은 양 끝).</summary>
        public TownTierData Data(int tier) => _settings.Tiers[Math.Max(0, Math.Min(_settings.Tiers.Count - 1, tier))];
        /// <inheritdoc />
        public event Action<int> TierChanged;
        /// <inheritdoc />
        public event Action<string, int> HubTierChanged;
        /// <summary>첫 마을이 붕괴했을 때 한 번.</summary>
        public event Action Collapsing;
        /// <summary>스토리 048. 어느 마을이든 붕괴했을 때 한 번(거점 id).</summary>
        public event Action<string> HubCollapsing;
        /// <summary>스토리 048. 첫 마을 등급이 _unlockHubs의 역을 열라고 할 때(거점 id, 한 번씩).</summary>
        public event Action<string> HubUnlockRequested;

        /// <summary>수치를 주입하고 첫 마을을 등급 0으로 열고 등급 0의 해금을 누적 목록에 넣는다.</summary>
        public TownTiers(TownSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            AddUnlocks(Data(0), false);
            AddHub(settings.HubId);
        }

        /// <summary>스토리 048. hubId 마을을 등급 0으로 세기 시작한다(이미 있으면 무시).</summary>
        public void AddHub(string hubId)
        {
            if (string.IsNullOrEmpty(hubId) || _hubs.ContainsKey(hubId)) return;
            _hubs.Add(hubId, new HubTier());
            _order.Add(hubId);
        }

        /// <summary>스토리 048. hubId 마을을 세고 있는지.</summary>
        public bool Tracks(string hubId) => hubId != null && _hubs.ContainsKey(hubId);
        /// <inheritdoc />
        public int TierAt(string hubId) => hubId != null && _hubs.TryGetValue(hubId, out HubTier h) ? h.Tier : 0;
        /// <inheritdoc />
        public string TierNameAt(string hubId) => Data(TierAt(hubId)).Name;
        /// <inheritdoc />
        public bool NextNeedsAt(string hubId, out int residents, out int prosperity)
        {
            int tier = TierAt(hubId);
            bool more = tier < _settings.Tiers.Count - 1;
            residents = more ? Data(tier + 1).Residents : 0;
            prosperity = more ? Data(tier + 1).Prosperity : 0;
            return more;
        }
        /// <summary>스토리 048. hubId 마을이 붕괴했는지.</summary>
        public bool CollapsedAt(string hubId) => hubId != null && _hubs.TryGetValue(hubId, out HubTier h) && h.Collapsed;
        /// <summary>스토리 048. hubId 마을의 거점 단계 사상(붕괴면 Collapsed, 아니면 등급 표의 _phase).</summary>
        public HubPhase PhaseAt(string hubId) => CollapsedAt(hubId) ? HubPhase.Collapsed : Data(TierAt(hubId)).Phase;

        /// <summary>첫 마을 정산 판정(옛 경로).</summary>
        public void Evaluate(int residents, float prosperity) => EvaluateAt(_settings.HubId, residents, prosperity);

        /// <summary>스토리 048. hubId 마을의 정산 판정. 오른 등급마다 HubTierChanged(첫 마을은 TierChanged도), 붕괴하면 HubCollapsing 한 번.</summary>
        public void EvaluateAt(string hubId, int residents, float prosperity)
        {
            if (!Tracks(hubId)) return;
            HubTier h = _hubs[hubId];
            if (h.Collapsed) return;
            bool primary = hubId == _settings.HubId;
            while (h.Tier < _settings.Tiers.Count - 1 && residents >= Data(h.Tier + 1).Residents && prosperity >= Data(h.Tier + 1).Prosperity)
            {
                h.Tier++;
                HubTierChanged?.Invoke(hubId, h.Tier);
                if (primary) TierChanged?.Invoke(h.Tier);
                AddUnlocks(Data(h.Tier), primary);
            }
            bool low = h.Tier >= _settings.CollapseMinTier && residents <= _settings.CollapseResidents && prosperity <= _settings.CollapseProsperity;
            h.LowLaps = low ? h.LowLaps + 1 : 0;
            if (h.LowLaps < _settings.CollapseLaps) return;
            h.Collapsed = true;
            HubCollapsing?.Invoke(hubId);
            if (primary) Collapsing?.Invoke();
        }

        /// <summary>구독을 푼다.</summary>
        public void Clear() { TierChanged = null; HubTierChanged = null; Collapsing = null; HubCollapsing = null; HubUnlockRequested = null; CurrentHub = null; }

        private void AddUnlocks(TownTierData data, bool primary)
        {
            // 스토리 050: 등급은 레시피를 열지 않는다(_unlockRecipes는 무시, UnlockedRecipeIds는 늘 비어 있음). 레시피는 조합 해금(HabitatCodex).
            foreach (string id in data.UnlockUpgrades) if (!_upgrades.Contains(id)) _upgrades.Add(id);
            foreach (string id in data.UnlockStops) if (!_stops.Contains(id)) _stops.Add(id);
            if (!primary) return;
            foreach (string id in data.UnlockHubs)
            {
                if (_unlockedHubs.Contains(id)) continue;
                _unlockedHubs.Add(id);
                HubUnlockRequested?.Invoke(id);
            }
        }

        // 스펙 003: 저장 슬롯용 마을 단계·업그레이드·정류장·열린 거점 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "townTiers";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 72;

        /// <summary>
        /// 열린 순서 _order대로 마을마다 등급·낮은 정산 연속 수·붕괴 여부와, 누적 해금 목록(업그레이드·정류장·열린 거점)을 JSON으로 돌려준다.
        /// 레시피 목록 _recipes는 스토리 050부터 늘 비어 있어 저장하지 않는다. 상태를 바꾸지 않는다.
        /// </summary>
        public string Capture()
        {
            TierSave state = new TierSave
            {
                Upgrades = _upgrades.ToArray(),
                Stops = _stops.ToArray(),
                UnlockedHubs = _unlockedHubs.ToArray(),
            };
            foreach (string hubId in _order)
            {
                HubTier h = _hubs[hubId];
                state.Hubs.Add(new HubTierSave { HubId = hubId, Tier = h.Tier, LowLaps = h.LowLaps, Collapsed = h.Collapsed });
            }
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 마을을 열고(없으면 AddHub) 등급·붕괴 기록과 누적 해금 목록을 바꾼다. 해금 요청(HubUnlockRequested)은 내지 않는다.
        /// 등급이 달라진 마을마다 HubTierChanged를, 첫 마을이면 TierChanged도 낸다.
        /// </summary>
        public void Restore(string json)
        {
            TierSave state = UnityEngine.JsonUtility.FromJson<TierSave>(json);
            List<KeyValuePair<string, int>> changed = new List<KeyValuePair<string, int>>();
            foreach (HubTierSave saved in state.Hubs)
            {
                if (saved == null || string.IsNullOrEmpty(saved.HubId)) continue;
                AddHub(saved.HubId);
                HubTier h = _hubs[saved.HubId];
                int before = h.Tier;
                h.Tier = Math.Max(0, Math.Min(_settings.Tiers.Count - 1, saved.Tier));
                h.LowLaps = saved.LowLaps;
                h.Collapsed = saved.Collapsed;
                if (h.Tier != before) changed.Add(new KeyValuePair<string, int>(saved.HubId, h.Tier));
            }
            _upgrades.Clear();
            _upgrades.AddRange(state.Upgrades);
            _stops.Clear();
            _stops.AddRange(state.Stops);
            _unlockedHubs.Clear();
            _unlockedHubs.AddRange(state.UnlockedHubs);
            foreach (KeyValuePair<string, int> pair in changed)
            {
                HubTierChanged?.Invoke(pair.Key, pair.Value);
                if (pair.Key == _settings.HubId) TierChanged?.Invoke(pair.Value);
            }
        }

        /// <summary>스펙 003. 저장 형식: 마을별 등급 기록과 누적 해금 목록.</summary>
        [Serializable]
        private sealed class TierSave
        {
            public List<HubTierSave> Hubs = new List<HubTierSave>();
            public string[] Upgrades = Array.Empty<string>();
            public string[] Stops = Array.Empty<string>();
            public string[] UnlockedHubs = Array.Empty<string>();
        }

        /// <summary>스펙 003. 저장 형식: 마을 하나의 등급·낮은 정산 연속 수·붕괴 여부.</summary>
        [Serializable]
        private sealed class HubTierSave
        {
            public string HubId;
            public int Tier;
            public int LowLaps;
            public bool Collapsed;
        }
    }
}
