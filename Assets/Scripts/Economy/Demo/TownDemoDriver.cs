using System;
using System.Linq;

using IronRing.Hub;

namespace IronRing.Economy.Demo
{
    /// <summary>
    /// 스토리 042 데모(UI 없이 로그 "TOWN: …"). 인자: --town-demo --town-stage grow|tier (단계 없으면 grow).
    ///   grow: 가상 건물(TownTiers.json _demoGrowBuildings: 막사·온실·가스등 길)을 더하고 정산 _demoSettles번 → 주민 증가 로그.
    ///   tier: 주민 _demoTierResidents, 번영 _demoTierProsperity로 시드하고 정산 한 번 → "TOWN: tier up 정착촌" + 해금 목록.
    /// 가상 건물은 Population.ExtraBuildings에만 들어간다(타일 배치 없음; 수입·주거·빛 계산에만 쓰인다).
    /// </summary>
    public sealed class TownDemoDriver
    {
        /// <summary>단계 이름.</summary>
        public const string GROW = "grow", TIER = "tier";
        private const float START_DELAY = 1f;

        private readonly string _stage;
        private readonly TownSettings _settings;
        private readonly Settlement _settlement;
        private readonly Population _population;
        private readonly TownTiers _tiers;
        private readonly RunState _run;
        private float _wait;
        private bool _done;

        /// <summary>실행 인자에서 단계를 읽는다. --town-demo가 없으면 null.</summary>
        public static string ReadStage()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("--town-demo")) return null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--town-stage") return args[i + 1];
            return GROW;
        }

        /// <summary>설치자가 만든 서비스를 주입한다. run은 없을 수 있다(번영 시드만 빠진다).</summary>
        public TownDemoDriver(string stage, TownSettings settings, Settlement settlement, Population population, TownTiers tiers, RunState run)
        {
            _stage = stage;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            _population = population ?? throw new ArgumentNullException(nameof(population));
            _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            _run = run;
            UnityEngine.Debug.Log($"TOWN: demo stage={stage}");
        }

        /// <summary>매 프레임.</summary>
        public void Tick(float dt)
        {
            if (_done) return;
            _wait += Math.Max(0f, dt);
            if (_wait < START_DELAY) return;
            _done = true;
            if (_stage == GROW) Grow();
            else if (_stage == TIER) Tier();
            else UnityEngine.Debug.LogWarning($"TOWN: unknown demo stage {_stage}");
        }

        private void Grow()
        {
            _population.ExtraBuildings.AddRange(_settings.DemoGrowBuildings);
            _population.Refresh();
            int before = _population.Residents;
            for (int i = 0; i < _settings.DemoSettles; i++) _settlement.Settle(_settlement.SettledLap + 1);
            UnityEngine.Debug.Log($"TOWN: demo grow residents {before}->{_population.Residents} housing={_population.Housing} light={_population.Light} income={_population.Income}");
        }

        private void Tier()
        {
            _population.SetResidents(_settings.DemoTierResidents);
            string hub = _settings.HubId;
            if (_run != null) _run.ApplyDelta(hub, _settings.DemoTierProsperity - _run.GetProsperity(hub), "town-demo");
            _settlement.Settle(_settlement.SettledLap + 1);
            UnityEngine.Debug.Log($"TOWN: demo tier tier={_tiers.Tier} {_tiers.TierName} recipes=[{string.Join(",", _tiers.UnlockedRecipeIds)}] upgrades=[{string.Join(",", _tiers.UnlockedUpgradeIds)}] phase={(_run != null ? _run.GetPhase(hub).ToString() : "?")}");
        }
    }
}
