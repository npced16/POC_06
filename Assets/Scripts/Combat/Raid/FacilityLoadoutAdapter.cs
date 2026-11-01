using System;

using IronRing.Contracts;

namespace IronRing.Combat.Raid
{
    /// <summary>
    /// 스토리 033 임시 제공자. 거점(데이터 _hubId)에 지은 방벽·포탑 수를 IRaidLoadout으로 감싼다(방벽 = 방벽판, 포탑 = 포탑, 중포탑·속도 0).
    /// 스토리 034가 열차 장비 그리드 제공자를 IRaidLoadout으로 등록하면 이 어댑터는 쓰이지 않는다.
    /// </summary>
    public sealed class FacilityLoadoutAdapter : IRaidLoadout
    {
        private readonly IFacilityBuilder _facilities;
        private readonly RaidData _data;

        /// <summary>시설 빌더와 시설 id·거점 id가 든 습격 데이터를 받는다.</summary>
        public FacilityLoadoutAdapter(IFacilityBuilder facilities, RaidData data)
        {
            _facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _facilities.FacilityBuilt += (hub, facility) => { if (hub == _data.HubId) Changed?.Invoke(); };
        }

        public int Turrets => Count(_data.TurretFacilityId);
        public int HeavyTurrets => 0;
        public int ArmorPlates => Count(_data.WallFacilityId);
        public float SpeedBonus => 0f;
        public event Action Changed;

        private int Count(string facilityId)
        {
            if (string.IsNullOrEmpty(facilityId)) return 0;
            int n = 0;
            foreach (string id in _facilities.FacilitiesAt(_data.HubId)) if (id == facilityId) n++;
            return n;
        }
    }
}
