using System;

using UnityEngine;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 040·045(가벼운 경제, design/game-brief.md "돈(마르크)"). Resources/Economy/Economy.json의 수치.
    /// 바퀴 정산 = +건물 수입(HabitatTypes.json _income 합) +승객 운임 −열차 유지비. 시장·품목·화물·창고는 045에서 제거했다
    /// (archive/economy-trade-2026-11-02/).
    /// </summary>
    [Serializable]
    public sealed class EconomySettings
    {
#pragma warning disable 0649
        [SerializeField] private int _startBalance = 200;
        [SerializeField] private int _trainUpkeep = 10;
        [SerializeField] private int _fare = 8;
        [SerializeField] private int _bankruptLaps;
        [SerializeField] private float _settleToastSeconds = 4f;
        [SerializeField] private float _refundRatio = 1f;
#pragma warning restore 0649

        /// <summary>시작 잔고(마르크).</summary>
        public int StartBalance => _startBalance;
        /// <summary>바퀴마다 정산 때 빠지는 열차 유지비.</summary>
        public int TrainUpkeep => _trainUpkeep;
        /// <summary>승객 한 명이 거점에서 내릴 때 운임.</summary>
        public int Fare => _fare;
        /// <summary>잔고 &lt; 0 정산이 이만큼 연속이면 파산. 0이면 파산 판정을 하지 않는다(045 기본).</summary>
        public int BankruptLaps => _bankruptLaps;
        /// <summary>정산 토스트 표시 초.</summary>
        public float SettleToastSeconds => _settleToastSeconds;
        /// <summary>스토리 041. 회수·철거·되돌리기 때 배치 가격에서 돌려받는 비율(1 = 전액).</summary>
        public float RefundRatio => _refundRatio;

        /// <summary>JSON을 읽고 범위를 검사한다.</summary>
        public static EconomySettings Parse(string json)
        {
            EconomySettings s = JsonUtility.FromJson<EconomySettings>(json) ?? throw new ArgumentException("Economy.json is empty.");
            if (s._fare < 0 || s._trainUpkeep < 0 || s._bankruptLaps < 0 || s._refundRatio < 0f) throw new ArgumentException("Economy.json: invalid economy numbers.");
            return s;
        }
    }
}
