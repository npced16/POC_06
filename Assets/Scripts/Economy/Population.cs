using System;
using System.Collections.Generic;

using IronRing.Contracts;
using IronRing.Habitats;
using IronRing.Npc;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 042·045·048(가벼운 경제, design/game-brief.md "주민·등급"). 열린 마을(거점)마다의 일반 주민(IPopulation). 순수 C#, 결정적.
    /// 마을마다: 주거 = _baseHousing + 그 마을 완성 건물 _housing 합. 빛 = 완성 건물 _light 합. 수입 = 완성 건물 _income 합.
    /// 정산(SettleAt): 주거 &gt; 주민 → +_growPerLap(빛 ≥ 주민 × _lightPerResident면 +_growPerLapLit), 주거를 넘지 않음.
    ///               주거 &lt; 주민 → −_shrinkPerLap(최소 _minResidents). 같으면 그대로.
    /// 048: 마을은 AddHub로 늘어난다(선로가 열린 역). Residents·Housing·Light는 "현재 마을"(CurrentHub, 없으면 _hubId) 값,
    /// Income은 열린 모든 마을의 건물 수입 합(정산이 쓴다). ExtraBuildings·SetResidents·HubId는 첫 마을(_hubId, 황동골) 기준(데모용).
    /// 고유 NPC(INpcRecruiter)는 여기서 세지 않는다.
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class Population : IPopulation, IDisposable, ISaveParticipant
    {
        private sealed class Town { public int Residents, Housing, Light, Income; }

        private readonly TownSettings _settings;
        private readonly HabitatCatalog _catalog;
        private readonly IHabitatClusters _clusters;
        private readonly INpcRecruiter _npcs;
        private readonly NpcPoolData _npcPool;
        private readonly Dictionary<string, Town> _towns = new Dictionary<string, Town>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        /// <summary>첫 마을 거점 id(황동골).</summary>
        public string HubId => _settings.HubId;
        /// <summary>스토리 048. 주민을 세는 마을 id(열린 순서).</summary>
        public IReadOnlyList<string> HubIds => _order;
        /// <summary>스토리 048. "현재 마을" 공급자(설치자가 건다). null이거나 모르는 id면 HubId.</summary>
        public Func<string> CurrentHub { get; set; }
        /// <inheritdoc />
        public string CurrentHubId { get { string id = CurrentHub?.Invoke(); return id != null && _towns.ContainsKey(id) ? id : HubId; } }
        /// <inheritdoc />
        public int Residents => ResidentsAt(CurrentHubId);
        /// <inheritdoc />
        public int Housing => HousingAt(CurrentHubId);
        /// <inheritdoc />
        public int Light => LightAt(CurrentHubId);
        /// <summary>이번 바퀴 건물 수입(열린 모든 마을의 완성 건물 _income 합).</summary>
        public int Income { get { int sum = 0; foreach (Town t in _towns.Values) sum += t.Income; return sum; } }
        /// <summary>데모용 가상 건물 종류(첫 마을의 완성 건물 뒤에 더한다). 비어 있으면 실제 건물만.</summary>
        public List<string> ExtraBuildings { get; } = new List<string>();
        /// <inheritdoc />
        public event Action Changed;

        /// <summary>
        /// 마을 수치, 시설, 완성 묶음과 NPC 데이터 및 합류 상태를 받아 첫 마을을 연다.
        /// 반환값은 없으며 시설과 직무가 반영된 마을 수치를 유지하고 합류 이벤트를 구독한다.
        /// </summary>
        public Population(TownSettings settings, HabitatCatalog catalog, IHabitatClusters clusters,
            INpcRecruiter npcs = null, NpcPoolData npcPool = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _clusters = clusters ?? throw new ArgumentNullException(nameof(clusters));
            _npcs = npcs;
            _npcPool = npcPool;
            _clusters.HabitatCompleted += OnClusterChanged;
            _clusters.HabitatBroken += OnClusterChanged;
            if (_npcs != null) _npcs.NpcJoined += OnNpcJoined;
            if (_npcs != null) _npcs.NpcWithdrawn += OnNpcWithdrawn;
            AddHub(settings.HubId);
        }

        /// <summary>스토리 048. hubId 마을을 시작 주민으로 세기 시작한다(이미 있으면 무시). Changed.</summary>
        public void AddHub(string hubId)
        {
            if (string.IsNullOrEmpty(hubId) || _towns.ContainsKey(hubId)) return;
            _towns.Add(hubId, new Town { Residents = _settings.StartResidents });
            _order.Add(hubId);
            Recompute(hubId);
            Changed?.Invoke();
        }

        /// <summary>스토리 048. hubId 마을을 세고 있는지.</summary>
        public bool Tracks(string hubId) => hubId != null && _towns.ContainsKey(hubId);
        /// <inheritdoc />
        public int ResidentsAt(string hubId) => hubId != null && _towns.TryGetValue(hubId, out Town t) ? t.Residents : 0;
        /// <inheritdoc />
        public int HousingAt(string hubId) => hubId != null && _towns.TryGetValue(hubId, out Town t) ? t.Housing : 0;
        /// <inheritdoc />
        public int LightAt(string hubId) => hubId != null && _towns.TryGetValue(hubId, out Town t) ? t.Light : 0;
        /// <summary>스토리 048. hubId 마을의 건물 수입.</summary>
        public int IncomeAt(string hubId) => hubId != null && _towns.TryGetValue(hubId, out Town t) ? t.Income : 0;

        private void OnClusterChanged(string hubId, string typeId) { if (Tracks(hubId)) Refresh(); }

        private void OnNpcJoined(string hubId, string npcId) { if (Tracks(hubId)) Refresh(); }

        /// <summary>스펙 005. hubId에서 합류가 취소되면 직무 효과가 빠지도록 마을 수치를 다시 계산한다.</summary>
        private void OnNpcWithdrawn(string hubId, string npcId) { if (Tracks(hubId)) Refresh(); }

        /// <summary>모든 마을의 주거·빛·수입을 다시 세고 Changed.</summary>
        public void Refresh()
        {
            foreach (string id in _order) Recompute(id);
            Changed?.Invoke();
        }

        /// <summary>모든 마을의 정산 주민 단계(옛 경로). 설치자는 마을마다 SettleAt을 부른다.</summary>
        public void Settle(int lap)
        {
            foreach (string id in _order.ToArray()) SettleAt(id, lap);
        }

        /// <summary>스토리 048. hubId 마을의 정산 주민 단계. 로그 "TOWN: lap=… hub=…".</summary>
        public void SettleAt(string hubId, int lap)
        {
            if (!Tracks(hubId)) return;
            Recompute(hubId);
            Town t = _towns[hubId];
            int before = t.Residents;
            bool lit = t.Light >= t.Residents * _settings.LightPerResident;
            if (t.Housing > t.Residents)
                t.Residents = Math.Min(t.Housing, t.Residents + (lit ? _settings.GrowPerLapLit : _settings.GrowPerLap));
            else if (t.Housing < t.Residents)
                t.Residents = Math.Max(_settings.MinResidents, t.Residents - _settings.ShrinkPerLap);
            UnityEngine.Debug.Log($"TOWN: lap={lap} hub={hubId} residents {before}->{t.Residents} housing={t.Housing} light={t.Light} lit={lit} income={t.Income}");
            Changed?.Invoke();
        }

        /// <summary>데모용: 첫 마을 주민 수를 바로 정한다(최소값 적용).</summary>
        public void SetResidents(int residents) => SetResidentsAt(HubId, residents);

        /// <summary>스토리 048. 데모용: hubId 마을 주민 수를 바로 정한다(최소값 적용).</summary>
        public void SetResidentsAt(string hubId, int residents)
        {
            if (!Tracks(hubId)) return;
            _towns[hubId].Residents = Math.Max(_settings.MinResidents, residents);
            Changed?.Invoke();
        }

        /// <summary>
        /// hubId의 완성 시설과 합류 NPC 직무를 읽어 주거, 빛, 바퀴 수입을 다시 계산한다.
        /// 반환값은 없으며 해당 마을의 집계 상태를 갱신한다.
        /// </summary>
        private void Recompute(string hubId)
        {
            Town t = _towns[hubId];
            int housing = _settings.BaseHousing, light = 0, income = 0;
            var ids = new List<string>();
            foreach (HabitatCluster c in _clusters.CompletedAt(hubId) ?? Array.Empty<HabitatCluster>()) ids.Add(c?.TypeId);
            if (hubId == HubId) ids.AddRange(ExtraBuildings);
            foreach (string id in ids)
            {
                HabitatType type = _catalog.Get(id);
                if (type == null) continue;
                housing += type.Housing;
                light += type.Light;
                income += type.Income;
            }
            if (_npcs != null && _npcPool != null)
                foreach (string npcId in _npcs.NpcsAt(hubId))
                {
                    NpcDefinition npc = _npcPool.Find(npcId);
                    if (npc == null) continue;
                    switch (npc.WorkKind)
                    {
                        case "income": income += npc.WorkBonus; break;
                        case "housing": housing += npc.WorkBonus; break;
                        case "light": light += npc.WorkBonus; break;
                    }
                }
            t.Housing = housing; t.Light = light; t.Income = income;
        }

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            _clusters.HabitatCompleted -= OnClusterChanged;
            _clusters.HabitatBroken -= OnClusterChanged;
            if (_npcs != null) _npcs.NpcJoined -= OnNpcJoined;
            if (_npcs != null) _npcs.NpcWithdrawn -= OnNpcWithdrawn;
            Changed = null;
            CurrentHub = null;
        }

        // 스펙 003: 저장 슬롯용 마을별 주민 기록 꺼내기·되돌리기 추가(주거·빛·수입은 다시 계산)
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "population";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 71;

        /// <summary>
        /// 열린 순서 _order대로 마을 id와 주민 수를 JSON으로 돌려준다. 주거·빛·수입은 완성 건물과 NPC에서 다시 계산하므로 저장하지 않는다.
        /// 상태를 바꾸지 않는다.
        /// </summary>
        public string Capture()
        {
            TownSave state = new TownSave { HubIds = _order.ToArray(), Residents = new int[_order.Count] };
            for (int i = 0; i < _order.Count; i++) state.Residents[i] = _towns[_order[i]].Residents;
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 마을을 열고(없으면 AddHub) 주민 수를 둔 뒤, 모든 마을의 주거·빛·수입을 다시 계산하고 Changed를 낸다.
        /// </summary>
        public void Restore(string json)
        {
            TownSave state = UnityEngine.JsonUtility.FromJson<TownSave>(json);
            for (int i = 0; i < state.HubIds.Length && i < state.Residents.Length; i++)
            {
                string hubId = state.HubIds[i];
                if (string.IsNullOrEmpty(hubId)) continue;
                AddHub(hubId);
                _towns[hubId].Residents = state.Residents[i];
            }
            Refresh();
        }

        /// <summary>스펙 003. 저장 형식: 열린 순서의 마을 id와 주민 수의 짝(같은 순번).</summary>
        [Serializable]
        private sealed class TownSave
        {
            public string[] HubIds = Array.Empty<string>();
            public int[] Residents = Array.Empty<int>();
        }
    }
}
