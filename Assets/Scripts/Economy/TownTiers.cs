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
        /// <summary>이 등급이 여는 레시피.</summary>
        public IReadOnlyList<string> UnlockRecipes => _unlockRecipes ?? Array.Empty<string>();
        /// <summary>이 등급이 여는 열차 업그레이드(043).</summary>
        public IReadOnlyList<string> UnlockUpgrades => _unlockUpgrades ?? Array.Empty<string>();
        /// <summary>이 등급이 여는 휴식지(043).</summary>
        public IReadOnlyList<string> UnlockStops => _unlockStops ?? Array.Empty<string>();
    }

    /// <summary>
    /// 스토리 042. 마을 등급(ITownTier). 순수 C#. 정산마다 Evaluate(주민, 번영)를 부른다(건물 수입·운임·유지비 뒤, 주민 증감 앞).
    /// 상승: 다음 등급의 주민·번영을 모두 채우면 한 단계씩 오른다(한 정산에 여러 단계 가능, 단계마다 TierChanged). 등급은 내려가지 않는다.
    /// 등급 0의 해금은 생성 때 이미 열린 것으로 친다(누적 목록에 들어 있다).
    /// 붕괴: 등급이 CollapseMinTier 이상인데 주민 ≤ CollapseResidents이고 번영 ≤ CollapseProsperity인 정산이 CollapseLaps번 이어지면 Collapsed(끝).
    /// Phase = Collapsed면 HubPhase.Collapsed, 아니면 등급 표의 _phase. RunState(TownLink)가 이 값을 거점 단계로 쓴다.
    /// </summary>
    public sealed class TownTiers : ITownTier
    {
        private readonly TownSettings _settings;
        private readonly List<string> _recipes = new List<string>();
        private readonly List<string> _upgrades = new List<string>();
        private readonly List<string> _stops = new List<string>();
        private int _lowLaps;

        /// <inheritdoc />
        public int Tier { get; private set; }
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
        /// <inheritdoc />
        public IReadOnlyList<string> UnlockedRecipeIds => _recipes;
        /// <inheritdoc />
        public IReadOnlyList<string> UnlockedUpgradeIds => _upgrades;
        /// <summary>지금까지 연 휴식지(043).</summary>
        public IReadOnlyList<string> UnlockedStopIds => _stops;
        /// <summary>붕괴했는지(끝).</summary>
        public bool Collapsed { get; private set; }
        /// <summary>거점 단계 사상.</summary>
        public HubPhase Phase => Collapsed ? HubPhase.Collapsed : Data(Tier).Phase;
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
        /// <summary>붕괴했을 때 한 번.</summary>
        public event Action Collapsing;

        /// <summary>수치를 주입하고 등급 0의 해금을 누적 목록에 넣는다.</summary>
        public TownTiers(TownSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            AddUnlocks(Data(0));
        }

        /// <summary>정산 판정. 오른 등급마다 TierChanged, 붕괴하면 Collapsing 한 번. 붕괴 뒤에는 아무것도 하지 않는다.</summary>
        public void Evaluate(int residents, float prosperity)
        {
            if (Collapsed) return;
            while (!IsMax && residents >= NextResidentsNeed && prosperity >= NextProsperityNeed)
            {
                Tier++;
                AddUnlocks(Data(Tier));
                TierChanged?.Invoke(Tier);
            }
            bool low = Tier >= _settings.CollapseMinTier && residents <= _settings.CollapseResidents && prosperity <= _settings.CollapseProsperity;
            _lowLaps = low ? _lowLaps + 1 : 0;
            if (_lowLaps < _settings.CollapseLaps) return;
            Collapsed = true;
            Collapsing?.Invoke();
        }

        /// <summary>구독을 푼다.</summary>
        public void Clear() { TierChanged = null; Collapsing = null; }

        private void AddUnlocks(TownTierData data)
        {
            foreach (string id in data.UnlockRecipes) if (!_recipes.Contains(id)) _recipes.Add(id);
            foreach (string id in data.UnlockUpgrades) if (!_upgrades.Contains(id)) _upgrades.Add(id);
            foreach (string id in data.UnlockStops) if (!_stops.Contains(id)) _stops.Add(id);
        }
    }
}
