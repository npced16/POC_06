using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Train.Equipment
{
    /// <summary>
    /// 스토리 034 규칙. 열차 칸 Cars개 × (Columns × Rows) 장비 칸의 상태를 소유하고 ITrainLoadout으로 놓기·떼기를 받는다.
    /// IRaidLoadout·IRaidLoadoutStats로 습격에 포탑·중포 수, 방벽판 내구도, 부스터 속도, 수리 장치 회복, 무기 수치를 넘긴다(수치는 TrainEquipmentData).
    /// 순수 C#이고 런(GameServices) 동안 유지된다.
    /// </summary>
    public sealed class TrainLoadout : ITrainLoadout, IRaidLoadout, IRaidLoadoutStats
    {
        /// <summary>포탑 장비 id.</summary>
        public const string TURRET = "turret";
        /// <summary>중포 장비 id.</summary>
        public const string HEAVY_TURRET = "heavy_turret";

        private readonly TrainEquipmentData _data;
        private readonly EquipmentSlot[] _slots;

        /// <summary>데이터로 빈 그리드를 만든다.</summary>
        public TrainLoadout(TrainEquipmentData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _slots = new EquipmentSlot[data.Cars * data.Columns * data.Rows];
            for (int car = 0; car < data.Cars; car++)
                for (int row = 0; row < data.Rows; row++)
                    for (int column = 0; column < data.Columns; column++)
                        _slots[Index(car, column, row)] = new EquipmentSlot { Car = car, Column = column, Row = row };
        }

        /// <summary>읽은 데이터.</summary>
        public TrainEquipmentData Data => _data;
        public int Cars => _data.Cars;
        public int Columns => _data.Columns;
        public int Rows => _data.Rows;
        public IReadOnlyList<EquipmentSlot> Slots => _slots;
        public int UsedCells { get { int n = 0; foreach (EquipmentSlot slot in _slots) if (slot.EquipmentId != null) n++; return n; } }
        public event Action<EquipmentSlot> EquipmentPlaced;
        public event Action<EquipmentSlot> EquipmentRemoved;
        /// <summary>IRaidLoadout: 장비가 바뀌었을 때.</summary>
        public event Action Changed;

        /// <summary>(car, column, row)의 평면 색인. 범위 밖이면 -1.</summary>
        public int Index(int car, int column, int row)
        {
            if (car < 0 || car >= Cars || column < 0 || column >= Columns || row < 0 || row >= Rows) return -1;
            return (car * Rows + row) * Columns + column;
        }

        public bool CanPlace(int car, int column, int row, string equipmentId)
        {
            EquipmentDefinition definition = _data.Get(equipmentId);
            if (definition == null || Index(car, column, row) < 0 || column + definition.Width > Columns) return false;
            for (int k = 0; k < definition.Width; k++) if (_slots[Index(car, column + k, row)].EquipmentId != null) return false;
            return true;
        }

        public bool TryPlace(int car, int column, int row, string equipmentId)
        {
            if (!CanPlace(car, column, row, equipmentId)) return false;
            int width = _data.Get(equipmentId).Width;
            for (int k = 0; k < width; k++)
            {
                EquipmentSlot slot = _slots[Index(car, column + k, row)];
                slot.EquipmentId = equipmentId; slot.IsAnchor = k == 0; slot.Width = width;
            }
            EquipmentSlot anchor = _slots[Index(car, column, row)];
            UnityEngine.Debug.Log($"EQUIP: place {equipmentId} car={car} col={column} row={row} used={UsedCells}/{_slots.Length}");
            EquipmentPlaced?.Invoke(anchor);
            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(int car, int column, int row)
        {
            int index = Index(car, column, row);
            if (index < 0 || _slots[index].EquipmentId == null) return false;
            int start = column;
            while (start > 0 && !_slots[Index(car, start, row)].IsAnchor) start--;
            EquipmentSlot anchor = _slots[Index(car, start, row)];
            var removed = new EquipmentSlot { Car = car, Column = start, Row = row, EquipmentId = anchor.EquipmentId, IsAnchor = true, Width = anchor.Width };
            for (int k = 0; k < Math.Max(1, removed.Width); k++)
            {
                EquipmentSlot slot = _slots[Index(car, start + k, row)];
                slot.EquipmentId = null; slot.IsAnchor = false; slot.Width = 0;
            }
            UnityEngine.Debug.Log($"EQUIP: remove {removed.EquipmentId} car={car} col={start} row={row} used={UsedCells}/{_slots.Length}");
            EquipmentRemoved?.Invoke(removed);
            Changed?.Invoke();
            return true;
        }

        public int CountOf(string equipmentId)
        {
            int n = 0;
            foreach (EquipmentSlot slot in _slots) if (slot.IsAnchor && slot.EquipmentId == equipmentId) n++;
            return n;
        }

        /// <summary>모든 장비를 뗀다(각각 EquipmentRemoved).</summary>
        public void Clear()
        {
            foreach (EquipmentSlot slot in _slots) if (slot.IsAnchor) TryRemove(slot.Car, slot.Column, slot.Row);
        }

        /// <summary>데이터의 stage 데모 배치로 바꾼다(기존 장비를 모두 뗀 뒤 순서대로 놓기). 단계가 없으면 false.</summary>
        public bool ApplyDemo(string stage)
        {
            DemoLoadout demo = _data.Demo(stage);
            if (demo == null) return false;
            Clear();
            foreach (DemoPiece piece in demo.Pieces)
                if (!TryPlace(piece.Car, piece.Column, piece.Row, piece.Id))
                    UnityEngine.Debug.LogWarning($"EQUIP: demo {stage} piece {piece.Id} car={piece.Car} col={piece.Column} row={piece.Row} refused");
            UnityEngine.Debug.Log($"EQUIP: demo loadout {stage} turrets={Turrets} heavy={HeavyTurrets} plates={ArmorPlates} speed={SpeedBonus:F2} repair={RepairPerSecond:F2} used={UsedCells}/{_slots.Length}");
            return true;
        }

        // ----- IRaidLoadout -----

        public int Turrets => CountOf(TURRET);
        public int HeavyTurrets => CountOf(HEAVY_TURRET);
        /// <summary>내구도를 더하는 장비 개수(방벽판).</summary>
        public int ArmorPlates { get { int n = 0; foreach (EquipmentSlot slot in Anchors()) if (_data.Get(slot.EquipmentId).Hull > 0) n++; return n; } }
        public float SpeedBonus => Sum(d => d.SpeedBonus);

        // ----- IRaidLoadoutStats -----

        public int HullBonus => (int)Sum(d => d.Hull);
        public float RepairPerSecond => Sum(d => d.RepairPerSecond);
        public float TurretShotsPerSecond => _data.Get(TURRET)?.ShotsPerSecond ?? 0f;
        public int TurretDamage => _data.Get(TURRET)?.Damage ?? 0;
        public float HeavyTurretShotsPerSecond => _data.Get(HEAVY_TURRET)?.ShotsPerSecond ?? 0f;
        public int HeavyTurretDamage => _data.Get(HEAVY_TURRET)?.Damage ?? 0;

        /// <summary>id 장비들의 시작 칸을 Slots 순서로(습격 발사 위치 배정에 쓴다).</summary>
        public List<EquipmentSlot> AnchorsOf(string equipmentId)
        {
            var list = new List<EquipmentSlot>();
            foreach (EquipmentSlot slot in _slots) if (slot.IsAnchor && slot.EquipmentId == equipmentId) list.Add(slot);
            return list;
        }

        private IEnumerable<EquipmentSlot> Anchors()
        {
            foreach (EquipmentSlot slot in _slots) if (slot.IsAnchor && slot.EquipmentId != null) yield return slot;
        }

        private float Sum(Func<EquipmentDefinition, float> effect)
        {
            float total = 0f;
            foreach (EquipmentSlot slot in Anchors()) total += effect(_data.Get(slot.EquipmentId));
            return total;
        }
    }
}
