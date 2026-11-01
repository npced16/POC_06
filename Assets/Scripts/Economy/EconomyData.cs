using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 040 (design/game-brief.md "돈(마르크)"). Resources/Economy/Economy.json의 수치. 모든 경제 규칙 수치는 여기서 온다.
    /// </summary>
    [Serializable]
    public sealed class EconomySettings
    {
        [SerializeField] private int _startBalance = 200;
        [SerializeField] private int _trainUpkeep = 20;
        [SerializeField] private int _fare = 8;
        [SerializeField] private int _bankruptLaps = 3;
        [SerializeField] private int _cargoCapacity = 12;
        [SerializeField] private float _autoDepartSeconds = 20f;
        [SerializeField] private float _buyPriceStep = 0.05f;
        [SerializeField] private float _sellPriceStep = 0.05f;
        [SerializeField] private float _minPriceFactor = 0.4f;
        [SerializeField] private float _maxPriceFactor = 3f;
        [SerializeField] private float _sellRatio = 0.8f;
        [SerializeField] private float _recoverRatio = 1f;
        [SerializeField] private float _settleToastSeconds = 4f;
        [SerializeField] private int _demoBankruptStart = 10;
        [SerializeField] private int _demoBankruptUpkeep = 50;
        [SerializeField] private string _demoTradeStop = "REST-1";
        [SerializeField] private string _demoTradeGoods = "coal";
        [SerializeField] private int _demoTradeCount = 4;
        [SerializeField] private string _demoSellStop = "REST-2";
        [SerializeField] private float _demoHubDwellSeconds = 1f;

        /// <summary>시작 잔고(마르크). 데모 bankrupt 단계만 덮어쓴다.</summary>
        public int StartBalance { get => _startBalance; set => _startBalance = value; }
        /// <summary>바퀴마다 정산 때 빠지는 열차 유지비. 데모 bankrupt 단계만 덮어쓴다.</summary>
        public int TrainUpkeep { get => _trainUpkeep; set => _trainUpkeep = value; }
        /// <summary>승객 한 명이 거점에서 내릴 때 운임.</summary>
        public int Fare => _fare;
        /// <summary>잔고 &lt; 0 정산이 이만큼 연속이면 파산.</summary>
        public int BankruptLaps => _bankruptLaps;
        /// <summary>화물칸 칸 수.</summary>
        public int CargoCapacity => _cargoCapacity;
        /// <summary>휴식지 거래 세션 자동 출발 초.</summary>
        public float AutoDepartSeconds => _autoDepartSeconds;
        /// <summary>1개 살 때마다 변동 계수 증가량.</summary>
        public float BuyPriceStep => _buyPriceStep;
        /// <summary>1개 팔 때마다 변동 계수 감소량.</summary>
        public float SellPriceStep => _sellPriceStep;
        /// <summary>변동 계수 하한.</summary>
        public float MinPriceFactor => _minPriceFactor;
        /// <summary>변동 계수 상한.</summary>
        public float MaxPriceFactor => _maxPriceFactor;
        /// <summary>팔 때 가격 = 살 때 가격 × 이 비율(같은 곳 되팔기 차익 방지).</summary>
        public float SellRatio => _sellRatio;
        /// <summary>바퀴마다 변동 계수가 1로 돌아가는 비율(1 = 완전 복귀).</summary>
        public float RecoverRatio => _recoverRatio;
        /// <summary>정산 토스트 표시 초.</summary>
        public float SettleToastSeconds => _settleToastSeconds;
        /// <summary>데모 bankrupt 단계 시작 잔고.</summary>
        public int DemoBankruptStart => _demoBankruptStart;
        /// <summary>데모 bankrupt 단계 열차 유지비.</summary>
        public int DemoBankruptUpkeep => _demoBankruptUpkeep;
        /// <summary>데모 trade/ledger 단계에서 사는 휴식지.</summary>
        public string DemoTradeStop => _demoTradeStop;
        /// <summary>데모에서 사는 품목.</summary>
        public string DemoTradeGoods => _demoTradeGoods;
        /// <summary>데모에서 사는 수량.</summary>
        public int DemoTradeCount => _demoTradeCount;
        /// <summary>데모 ledger 단계에서 파는 휴식지.</summary>
        public string DemoSellStop => _demoSellStop;
        /// <summary>데모에서 거점 정차 후 자동 출발까지 초.</summary>
        public float DemoHubDwellSeconds => _demoHubDwellSeconds;

        /// <summary>JSON을 읽고 범위를 검사한다.</summary>
        public static EconomySettings Parse(string json)
        {
            EconomySettings s = JsonUtility.FromJson<EconomySettings>(json) ?? throw new ArgumentException("Economy.json is empty.");
            if (s._cargoCapacity < 0 || s._fare < 0 || s._trainUpkeep < 0 || s._bankruptLaps < 1) throw new ArgumentException("Economy.json: invalid economy numbers.");
            if (s._sellRatio <= 0f || s._minPriceFactor <= 0f || s._maxPriceFactor < s._minPriceFactor) throw new ArgumentException("Economy.json: invalid price numbers.");
            return s;
        }
    }

    /// <summary>스토리 040. 품목 하나(Goods.json).</summary>
    [Serializable]
    public sealed class GoodsDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private int _basePrice;
        [SerializeField] private string _iconKey;
        [SerializeField] private string _color = "#ffffff";

        /// <summary>품목 id.</summary>
        public string Id => _id;
        /// <summary>한국어 이름.</summary>
        public string Name => _name;
        /// <summary>기준가(마르크).</summary>
        public int BasePrice => _basePrice;
        /// <summary>아이콘 키(UI가 스프라이트를 찾는 키).</summary>
        public string IconKey => _iconKey;
        /// <summary>아이콘 자리표시 색(#rrggbb).</summary>
        public string ColorHex => _color;
    }

    [Serializable]
    internal sealed class GoodsFile
    {
        public GoodsDefinition[] _goods;
    }

    /// <summary>스토리 040. 한 휴식지의 한 품목 시장 수치(Markets.json).</summary>
    [Serializable]
    public sealed class MarketGoodsData
    {
        [SerializeField] private string _id;
        [SerializeField] private float _multiplier = 1f;
        [SerializeField] private int _stock;
        [SerializeField] private int _demand;

        /// <summary>품목 id.</summary>
        public string Id => _id;
        /// <summary>이 휴식지의 가격 배율.</summary>
        public float Multiplier => _multiplier;
        /// <summary>바퀴마다 복귀하는 재고(살 수 있는 수).</summary>
        public int Stock => _stock;
        /// <summary>바퀴마다 복귀하는 수요(팔 수 있는 수).</summary>
        public int Demand => _demand;
    }

    /// <summary>스토리 040. 한 휴식지 시장(Markets.json).</summary>
    [Serializable]
    public sealed class MarketStopData
    {
        [SerializeField] private string _stopId;
        [SerializeField] private string _note;
        [SerializeField] private MarketGoodsData[] _goods;

        /// <summary>선로 노드 id(REST-1 등).</summary>
        public string StopId => _stopId;
        /// <summary>디자이너 메모(규칙에 쓰지 않음).</summary>
        public string Note => _note;
        /// <summary>품목별 수치.</summary>
        public IReadOnlyList<MarketGoodsData> Goods => _goods ?? Array.Empty<MarketGoodsData>();
    }

    [Serializable]
    internal sealed class MarketsFile
    {
        public MarketStopData[] _markets;
    }

    /// <summary>스토리 040. Markets.json 전체.</summary>
    public static class MarketsData
    {
        /// <summary>JSON을 읽는다. 빈 정차지 id·중복이면 예외.</summary>
        public static IReadOnlyList<MarketStopData> Parse(string json)
        {
            MarketsFile file = JsonUtility.FromJson<MarketsFile>(json);
            if (file?._markets == null) throw new ArgumentException("Markets.json has no _markets.");
            var seen = new HashSet<string>();
            foreach (MarketStopData stop in file._markets)
                if (string.IsNullOrEmpty(stop.StopId) || !seen.Add(stop.StopId)) throw new ArgumentException("Markets.json: blank or duplicate stop " + stop.StopId);
            return file._markets;
        }
    }

    /// <summary>스토리 040. 품목 6종 카탈로그(Resources/Economy/Goods.json). 데이터 순서를 지킨다.</summary>
    public sealed class GoodsCatalog
    {
        private readonly Dictionary<string, GoodsDefinition> _byId = new Dictionary<string, GoodsDefinition>();
        private readonly List<string> _ids = new List<string>();

        /// <summary>품목 id(데이터 순서).</summary>
        public IReadOnlyList<string> Ids => _ids;

        /// <summary>JSON을 읽는다. 빈·중복 id, 기준가 &lt;= 0이면 예외.</summary>
        public static GoodsCatalog Parse(string json)
        {
            GoodsFile file = JsonUtility.FromJson<GoodsFile>(json);
            if (file?._goods == null || file._goods.Length == 0) throw new ArgumentException("Goods.json has no _goods.");
            var catalog = new GoodsCatalog();
            foreach (GoodsDefinition g in file._goods)
            {
                if (string.IsNullOrEmpty(g.Id) || catalog._byId.ContainsKey(g.Id) || g.BasePrice <= 0) throw new ArgumentException("Goods.json: bad goods " + g.Id);
                catalog._byId.Add(g.Id, g);
                catalog._ids.Add(g.Id);
            }
            return catalog;
        }

        /// <summary>id의 정의(없으면 null).</summary>
        public GoodsDefinition Get(string id) => id != null && _byId.TryGetValue(id, out GoodsDefinition g) ? g : null;

        /// <summary>id의 한국어 이름(없으면 id).</summary>
        public string NameOf(string id) => Get(id)?.Name ?? id;

        /// <summary>아이콘 자리표시 색(없거나 잘못된 값이면 흰색).</summary>
        public Color ColorOf(string id) => ColorUtility.TryParseHtmlString(Get(id)?.ColorHex ?? "", out Color c) ? c : Color.white;
    }
}
