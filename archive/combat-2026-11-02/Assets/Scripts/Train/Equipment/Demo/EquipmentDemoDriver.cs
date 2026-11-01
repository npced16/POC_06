using System.Collections;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Presentation.Interior;

namespace IronRing.Train.Equipment.Demo
{
    /// <summary>
    /// 스토리 034 캡처 데모(--equipment-demo --equipment-stage grid|placed). hub-1 내부를 열고 열차 장비 모드를 켠다.
    /// grid: 빈 3×2 그리드 3량, placed: TrainEquipment.json _demoLoadouts "placed"(포탑 2·중포 1·방벽판 2·부스터 1) 배치 + 핫바 장비 목록.
    /// 준비되면 "EQUIP: capture-ready stage=…"를 남긴다(캡처는 코디네이터).
    /// </summary>
    public sealed class EquipmentDemoDriver : MonoBehaviour
    {
        private const string HUB = "hub-1";
        private TrainLoadout _loadout;
        private string _stage;

        /// <summary>장비 상태와 단계 이름을 받는다.</summary>
        public void Bind(TrainLoadout loadout, string stage)
        {
            _loadout = loadout;
            _stage = stage;
            Debug.Log("EQUIP: demo stage=" + stage);
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            while (!GameServices.IsReady) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            IInteriorNavigation navigation = GameServices.Get<IInteriorNavigation>();
            if (navigation == null) { Debug.LogError("EQUIP: demo needs IInteriorNavigation"); yield break; }
            navigation.Open(HUB);
            for (int i = 0; i < 10; i++) yield return null;
            if (_stage == "placed") _loadout.ApplyDemo("placed");
            TrainEquipmentMode.Set(true);
            for (int i = 0; i < 20; i++) yield return null;
            Debug.Log($"EQUIP: capture-ready stage={_stage} mode={TrainEquipmentMode.Active} used={_loadout.UsedCells}/{_loadout.Slots.Count} turrets={_loadout.Turrets} heavy={_loadout.HeavyTurrets} plates={_loadout.ArmorPlates}");
        }
    }
}
