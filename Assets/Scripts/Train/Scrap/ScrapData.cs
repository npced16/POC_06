using System;

using UnityEngine;

namespace IronRing.Train.Scrap
{
    /// <summary>
    /// 스토리 038(design/game-brief.md MVP 4·5, production/epics/iron-ring-train/story-038). 고철 경제·정차 파밍 수치
    /// (Resources/Train/Scrap.json). 좌표(더미·인부 집)는 정차 화면 좌표에서 열차 머리 기준 오프셋이다(X = 선로 방향, Y = 옆).
    /// </summary>
    [Serializable]
    public sealed class ScrapData
    {
        /// <summary>Resources 경로.</summary>
        public const string RESOURCE = "Train/Scrap";

        [SerializeField] private int _startScrap = 6;
        [SerializeField] private float _refundRatio = 1f;
        [SerializeField] private float _holdBonusPerSecond = 0.25f;
        [SerializeField] private bool _fleeKeepCarried;
        [SerializeField] private float _demoHoldSeconds = 12f;
        [SerializeField] private float _scrapPerSecond = 1f;
        [SerializeField] private int _carryMax = 3;
        [SerializeField] private int _workers = 2;
        [SerializeField] private float _workerSpeed = 1.8f;
        [SerializeField] private float _fleeSpeedFactor = 1.4f;
        [SerializeField] private float _fleeDistance = 1.6f;
        [SerializeField] private bool _keepCarryOnFlee = true;
        [SerializeField] private float _arriveDistance = 0.12f;
        [SerializeField] private bool _respawnPiles = true;
        [SerializeField] private float _homeX = -3.3f;
        [SerializeField] private float _homeY = -1f;
        [SerializeField] private float _workerSpacing = 0.4f;
        [SerializeField] private ScrapPileData[] _piles = Array.Empty<ScrapPileData>();

        /// <summary>런 시작 고철.</summary>
        public int StartScrap => Mathf.Max(0, _startScrap);
        /// <summary>장비를 뗄 때 돌려받는 비율(1 = 전액, 내림).</summary>
        public float RefundRatio => Mathf.Clamp01(_refundRatio);
        /// <summary>03:30 지시: 정차에 끝 시간은 없다. 출발할 때 버틴 초 × 이 값(내림)을 보너스 고철로 받는다(패배면 없음).</summary>
        public float HoldBonusPerSecond => Mathf.Max(0f, _holdBonusPerSecond);
        /// <summary>출발하는 순간 인부가 들고 있던 고철을 창고에 넣을지(false = 버림).</summary>
        public bool FleeKeepCarried => _fleeKeepCarried;
        /// <summary>--stop-demo가 "출발" 단계에서 버티는 초(데모 전용).</summary>
        public float DemoHoldSeconds => Mathf.Max(1f, _demoHoldSeconds);
        /// <summary>인부 한 명이 초당 캐는 고철.</summary>
        public float ScrapPerSecond => Mathf.Max(0.01f, _scrapPerSecond);
        /// <summary>인부 한 명이 한 번에 나르는 최대 고철.</summary>
        public int CarryMax => Mathf.Max(1, _carryMax);
        /// <summary>수집 인부 수.</summary>
        public int Workers => Mathf.Max(0, _workers);
        /// <summary>인부 걷는 속도(정차 화면 단위/초).</summary>
        public float WorkerSpeed => Mathf.Max(0.05f, _workerSpeed);
        /// <summary>도망칠 때 속도 배율.</summary>
        public float FleeSpeedFactor => Mathf.Max(0.1f, _fleeSpeedFactor);
        /// <summary>적이 이 거리 안에 오면 인부가 열차로 도망친다.</summary>
        public float FleeDistance => Mathf.Max(0f, _fleeDistance);
        /// <summary>도망쳐 돌아와도 들고 있던 고철을 넣을지.</summary>
        public bool KeepCarryOnFlee => _keepCarryOnFlee;
        /// <summary>도착 판정 거리.</summary>
        public float ArriveDistance => Mathf.Max(0.01f, _arriveDistance);
        /// <summary>정차마다 더미를 데이터 양으로 다시 채울지(false면 남은 양 유지).</summary>
        public bool RespawnPiles => _respawnPiles;
        /// <summary>인부 집(열차 승강장 문) X 오프셋(열차 머리 기준).</summary>
        public float HomeX => _homeX;
        /// <summary>인부 집 Y 오프셋.</summary>
        public float HomeY => _homeY;
        /// <summary>인부끼리 집에서 벌어진 간격(X).</summary>
        public float WorkerSpacing => _workerSpacing;
        /// <summary>고철 더미 목록.</summary>
        public ScrapPileData[] Piles => _piles ?? Array.Empty<ScrapPileData>();

        /// <summary>JSON을 읽는다(빈 배열 보정).</summary>
        public static ScrapData Parse(string json)
        {
            ScrapData data = JsonUtility.FromJson<ScrapData>(json) ?? new ScrapData();
            data._piles = data._piles ?? Array.Empty<ScrapPileData>();
            return data;
        }

        /// <summary>Resources에서 읽는다(없으면 기본값).</summary>
        public static ScrapData Load()
        {
            TextAsset json = Resources.Load<TextAsset>(RESOURCE);
            return json != null ? Parse(json.text) : new ScrapData();
        }
    }

    /// <summary>고철 더미 하나(열차 머리 기준 x, y와 양).</summary>
    [Serializable]
    public sealed class ScrapPileData
    {
        /// <summary>X 오프셋(선로 방향).</summary>
        public float x;
        /// <summary>Y 오프셋(옆).</summary>
        public float y;
        /// <summary>고철 양.</summary>
        public int amount = 4;
    }
}
