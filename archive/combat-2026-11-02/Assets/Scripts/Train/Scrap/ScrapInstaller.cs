using UnityEngine;

using IronRing.Contracts.Presentation;
using IronRing.Contracts;

namespace IronRing.Train.Scrap
{
    /// <summary>
    /// 스토리 038 조립기. GameServices.Composing에서 Scrap.json으로 ScrapStore(IScrap 등록)와 정차 파밍 규칙(ScrapFarming)을 만든다.
    /// 열차 장비(TrainLoadout)는 IScrap을 늦게 조회하므로 설치 순서와 관계없다.
    /// </summary>
    public static class ScrapInstaller
    {
        /// <summary>이번 런의 고철 저장소(설치 전이면 null).</summary>
        public static ScrapStore Store { get; private set; }
        /// <summary>이번 런의 정차 파밍 규칙(설치 전이면 null).</summary>
        public static ScrapFarming Farming { get; private set; }
        /// <summary>읽은 데이터(설치 전이면 null).</summary>
        public static ScrapData Data { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Composing -= Install; Store = null; Farming = null; Data = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe() { GameServices.Composing -= Install; GameServices.Composing += Install; }

        private static void Install()
        {
            Data = ScrapData.Load();
            Store = new ScrapStore(Data.StartScrap);
            Farming = new ScrapFarming(Data, Store);
            GameServices.Register<IScrap>(Store);
            Debug.Log($"SCRAP: installed start={Store.Amount} workers={Data.Workers} piles={Data.Piles.Length} holdBonus={Data.HoldBonusPerSecond}/s keepCarried={Data.FleeKeepCarried}");
        }
    }
}
