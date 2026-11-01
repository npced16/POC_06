using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Train.Equipment
{
    /// <summary>
    /// 스토리 034 조립기. GameServices.Composing에서 Resources/Train/TrainEquipment.json으로 TrainLoadout을 만들어 ITrainLoadout과
    /// IRaidLoadout(033의 임시 FacilityLoadoutAdapter를 덮어씀)으로 등록한다. RaidSystem은 습격 시작마다 IRaidLoadout을 늦게 읽으므로
    /// 설치 순서와 관계없이 열차 장비가 쓰인다. --equipment-demo가 있으면 EquipmentDemoDriver를 붙인다.
    /// </summary>
    public static class TrainEquipmentInstaller
    {
        /// <summary>이번 런의 열차 장비(설치 전이면 null).</summary>
        public static TrainLoadout Loadout { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Composing -= Install; Loadout = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe() { GameServices.Composing -= Install; GameServices.Composing += Install; }

        private static void Install()
        {
            TrainEquipmentData data = TrainEquipmentData.Load();
            if (data == null) return;
            Loadout = new TrainLoadout(data);
            GameServices.Register<ITrainLoadout>(Loadout);
            GameServices.Register<IRaidLoadout>(Loadout);
            Debug.Log($"EQUIP: installed cars={data.Cars} grid={data.Columns}x{data.Rows} kinds={data.Equipment.Count}");
            if (IronRing.Combat.Raid.RaidInstaller.HasArgument("--equipment-demo"))
                new GameObject("EquipmentDemo").AddComponent<Demo.EquipmentDemoDriver>().Bind(Loadout, IronRing.Combat.Raid.RaidInstaller.Argument("--equipment-stage") ?? "grid");
        }
    }
}
