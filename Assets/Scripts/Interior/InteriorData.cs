using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Interior
{
    /// <summary>
    /// 스토리 012. 거점 내부 격자와 조사 규칙의 제작 데이터(design/game-brief.md Core loop 2단계).
    /// 격자 크기, 정차당 조사 횟수, 결과 확률, 부지 최소 보장, 묻힌 아이템·자원은 모두 JSON에서 읽는다.
    /// </summary>
    [Serializable]
    public sealed class InteriorData
    {
        [Header("격자 (구역당 줄×칸, 선로 위·아래 두 구역)")]
        [SerializeField] private int _rows;
        [SerializeField] private int _columns;
        [Header("조사 규칙")]
        [SerializeField] private int _investigationsPerDocking;
        [SerializeField] private int _minimumFacilityPlots;
        [SerializeField] private int _seed;
        [Tooltip("스토리 016: true면 모든 칸이 잔해가 아닌 빈 터(Empty)로 시작한다(조사 비활성).")]
        [SerializeField] private bool _startEmpty;
        [SerializeField] private OutcomeWeights _outcomes;
        [Header("묻힌 것")]
        [SerializeField] private string[] _buriedItems;
        [SerializeField] private BuriedResource[] _buriedResources;

        /// <summary>구역 하나의 줄 수.</summary>
        public int Rows => _rows;
        /// <summary>구역 하나의 칸(열) 수.</summary>
        public int Columns => _columns;
        /// <summary>구역 하나의 칸 수.</summary>
        public int AreaCellCount => _rows * _columns;
        /// <summary>위·아래 두 구역의 전체 칸 수.</summary>
        public int CellCount => AreaCellCount * 2;
        /// <summary>정차마다 초기화되는 조사 횟수.</summary>
        public int InvestigationsPerDocking => _investigationsPerDocking;
        /// <summary>거점마다 생성 시 고정 배치로 보장하는 시설 부지 칸 수.</summary>
        public int MinimumFacilityPlots => _minimumFacilityPlots;
        /// <summary>컴포지션 루트가 IRandomSource를 만들 때 쓰는 시드.</summary>
        public int Seed => _seed;
        /// <summary>스토리 016: 모든 칸이 Empty로 시작하는지(조사 비활성). false면 012처럼 잔해로 시작한다.</summary>
        public bool StartEmpty => _startEmpty;
        /// <summary>조사 결과 가중치(빈 터·아이템·자원·부지).</summary>
        public OutcomeWeights Outcomes => _outcomes;
        /// <summary>묻힌 아이템 id 목록(스토리 013 아이템 카탈로그와 맞춘다).</summary>
        public IReadOnlyList<string> BuriedItems => _buriedItems;
        /// <summary>묻힌 자원과 발견 시 수량.</summary>
        public IReadOnlyList<BuriedResource> BuriedResources => _buriedResources;

        /// <summary>json을 읽어 격자·조사 값과 확률표를 검증한 뒤 사용 가능한 데이터를 반환한다.</summary>
        public static InteriorData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Interior data is required.", nameof(json));
            InteriorData data = JsonUtility.FromJson<InteriorData>(json);
            if (data == null) throw new ArgumentException("Invalid interior data.", nameof(json));
            if (data._rows < 1 || data._columns < 1) throw new ArgumentException("Interior grid requires positive rows and columns.", nameof(json));
            if (data._investigationsPerDocking < 0) throw new ArgumentException("Investigations per docking must not be negative.", nameof(json));
            if (data._minimumFacilityPlots < 0 || data._minimumFacilityPlots > data.CellCount)
                throw new ArgumentException("Minimum facility plots must fit inside the grid.", nameof(json));
            if (data._outcomes == null || !data._outcomes.IsValid) throw new ArgumentException("Outcome weights must be non-negative with a positive total.", nameof(json));
            data._buriedItems ??= Array.Empty<string>();
            data._buriedResources ??= Array.Empty<BuriedResource>();
            foreach (string item in data._buriedItems)
                if (string.IsNullOrWhiteSpace(item)) throw new ArgumentException("Buried item ids must not be blank.", nameof(json));
            foreach (BuriedResource resource in data._buriedResources)
                if (resource == null || string.IsNullOrWhiteSpace(resource.Id) || resource.Amount < 1)
                    throw new ArgumentException("Buried resources require an id and a positive amount.", nameof(json));
            if (data._outcomes.Item > 0 && data._buriedItems.Length == 0) throw new ArgumentException("Item outcome needs at least one buried item.", nameof(json));
            if (data._outcomes.Resource > 0 && data._buriedResources.Length == 0) throw new ArgumentException("Resource outcome needs at least one buried resource.", nameof(json));
            return data;
        }
    }

    /// <summary>조사 결과 가중치. 비율은 합계로 정규화되므로 백분율로 적어도 되고 임의 정수여도 된다.</summary>
    [Serializable]
    public sealed class OutcomeWeights
    {
        [SerializeField] private int _empty;
        [SerializeField] private int _item;
        [SerializeField] private int _resource;
        [SerializeField] private int _facilityPlot;

        /// <summary>빈 터 가중치.</summary>
        public int Empty => _empty;
        /// <summary>묻힌 아이템 가중치.</summary>
        public int Item => _item;
        /// <summary>묻힌 자원 가중치.</summary>
        public int Resource => _resource;
        /// <summary>시설 부지 가중치.</summary>
        public int FacilityPlot => _facilityPlot;
        /// <summary>네 가중치의 합.</summary>
        public int Total => _empty + _item + _resource + _facilityPlot;
        /// <summary>모든 가중치가 0 이상이고 합이 양수인지.</summary>
        public bool IsValid => _empty >= 0 && _item >= 0 && _resource >= 0 && _facilityPlot >= 0 && Total > 0;

        /// <summary>테스트와 도구용 생성자. 런타임 데이터는 JSON에서 온다.</summary>
        public OutcomeWeights(int empty, int item, int resource, int facilityPlot)
        {
            _empty = empty; _item = item; _resource = resource; _facilityPlot = facilityPlot;
        }
    }

    /// <summary>묻힌 자원 하나: 자원 id와 발견 시 재고에 더하는 수량.</summary>
    [Serializable]
    public sealed class BuriedResource
    {
        [SerializeField] private string _id;
        [SerializeField] private int _amount;

        /// <summary>IResourceInventory가 아는 자원 id.</summary>
        public string Id => _id;
        /// <summary>발견 시 더하는 양(1 이상).</summary>
        public int Amount => _amount;

        /// <summary>테스트와 도구용 생성자. 런타임 데이터는 JSON에서 온다.</summary>
        public BuriedResource(string id, int amount) { _id = id; _amount = amount; }
    }
}
