using System;
using System.Collections.Generic;

using IronRing.Contracts;
using IronRing.Habitats;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 042·045(가벼운 경제, design/game-brief.md MVP 3). 마을 거점의 일반 주민(IPopulation). 순수 C#, 결정적.
    /// 주거 = _baseHousing + 완성 건물 _housing 합. 빛 = 완성 건물 _light 합. 수입 = 완성 건물 _income 합(Settlement.Income).
    /// 정산(Settle): 주거 &gt; 주민 → +_growPerLap(빛 ≥ 주민 × _lightPerResident면 +_growPerLapLit), 주거를 넘지 않음.
    ///               주거 &lt; 주민 → −_shrinkPerLap(최소 _minResidents). 같으면 그대로. 노동·식량·만족도는 없다.
    /// 고유 NPC(INpcRecruiter)는 여기서 세지 않는다(번영 _perNpc는 RunState가 따로 더한다).
    /// </summary>
    public sealed class Population : IPopulation, IDisposable
    {
        private readonly TownSettings _settings;
        private readonly HabitatCatalog _catalog;
        private readonly IHabitatClusters _clusters;

        /// <summary>마을 거점 id.</summary>
        public string HubId => _settings.HubId;
        /// <inheritdoc />
        public int Residents { get; private set; }
        /// <inheritdoc />
        public int Housing { get; private set; }
        /// <inheritdoc />
        public int Light { get; private set; }
        /// <summary>이번 바퀴 건물 수입(완성 건물 _income 합).</summary>
        public int Income { get; private set; }
        /// <summary>데모용 가상 건물 종류(완성 건물 뒤에 더한다). 비어 있으면 실제 건물만.</summary>
        public List<string> ExtraBuildings { get; } = new List<string>();
        /// <inheritdoc />
        public event Action Changed;

        /// <summary>수치·카탈로그·완성 서식지를 주입한다.</summary>
        public Population(TownSettings settings, HabitatCatalog catalog, IHabitatClusters clusters)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _clusters = clusters ?? throw new ArgumentNullException(nameof(clusters));
            Residents = settings.StartResidents;
            _clusters.HabitatCompleted += OnClusterChanged;
            _clusters.HabitatBroken += OnClusterChanged;
            Recompute();
        }

        private void OnClusterChanged(string hubId, string typeId) { if (hubId == HubId) Refresh(); }

        /// <summary>주거·빛·수입을 다시 세고 Changed.</summary>
        public void Refresh()
        {
            Recompute();
            Changed?.Invoke();
        }

        /// <summary>정산 주민 단계. 로그 "TOWN: lap=…".</summary>
        public void Settle(int lap)
        {
            Recompute();
            int before = Residents;
            bool lit = Light >= Residents * _settings.LightPerResident;
            if (Housing > Residents)
                Residents = Math.Min(Housing, Residents + (lit ? _settings.GrowPerLapLit : _settings.GrowPerLap));
            else if (Housing < Residents)
                Residents = Math.Max(_settings.MinResidents, Residents - _settings.ShrinkPerLap);
            UnityEngine.Debug.Log($"TOWN: lap={lap} residents {before}->{Residents} housing={Housing} light={Light} lit={lit} income={Income}");
            Changed?.Invoke();
        }

        /// <summary>데모용: 주민 수를 바로 정한다(최소값 적용).</summary>
        public void SetResidents(int residents)
        {
            Residents = Math.Max(_settings.MinResidents, residents);
            Changed?.Invoke();
        }

        private void Recompute()
        {
            int housing = _settings.BaseHousing, light = 0, income = 0;
            var ids = new List<string>();
            foreach (HabitatCluster c in _clusters.CompletedAt(HubId) ?? Array.Empty<HabitatCluster>()) ids.Add(c?.TypeId);
            ids.AddRange(ExtraBuildings);
            foreach (string id in ids)
            {
                HabitatType type = _catalog.Get(id);
                if (type == null) continue;
                housing += type.Housing;
                light += type.Light;
                income += type.Income;
            }
            Housing = housing; Light = light; Income = income;
        }

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            _clusters.HabitatCompleted -= OnClusterChanged;
            _clusters.HabitatBroken -= OnClusterChanged;
            Changed = null;
        }
    }
}
