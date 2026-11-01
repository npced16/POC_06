using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Train.Equipment;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 034. 거점 내부의 "열차 장비 모드" 전역 상태(T 키·핫바 "열차" 버튼으로 켜고 T·Esc·내부 화면 닫힘으로 끈다)와 장비 되돌리기 기록.
    /// 켜지면 열린 거점의 TrainEquipmentView가 그리드 틀을 보이고 PlacementGhost의 배치 면이 되며, HotbarScreen이 장비 목록으로 바뀐다.
    /// </summary>
    public static class TrainEquipmentMode
    {
        private static bool _active;
        private static readonly LinkedList<UndoRecord> _undo = new LinkedList<UndoRecord>();

        /// <summary>장비 모드인지.</summary>
        public static bool Active => _active;
        /// <summary>모드가 바뀔 때(true = 장비 모드).</summary>
        public static event Action<bool> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _active = false; Changed = null; _undo.Clear(); }

        /// <summary>모드를 켜거나 끈다. 끄면 되돌리기 기록을 지운다.</summary>
        public static void Set(bool active)
        {
            if (_active == active) return;
            _active = active;
            if (!active) _undo.Clear();
            Debug.Log("EQUIP: mode " + (active ? "on" : "off"));
            Changed?.Invoke(active);
        }

        /// <summary>모드를 뒤집는다.</summary>
        public static void Toggle() => Set(!_active);

        /// <summary>핫바에 올릴 장비 목록(TrainEquipment.json 순서). 설치 전이면 빈 목록.</summary>
        public static List<PlacementEntry> Entries()
        {
            var list = new List<PlacementEntry>();
            TrainLoadout loadout = TrainEquipmentInstaller.Loadout;
            if (loadout == null) return list;
            foreach (EquipmentDefinition definition in loadout.Data.Equipment) list.Add(new PlacementEntry(PlacementKind.Equipment, definition.Id));
            return list;
        }

        /// <summary>놓기(placed = true) 또는 떼기 기록을 쌓는다(카탈로그 _undoDepth건까지).</summary>
        public static void Record(bool placed, EquipmentSlot anchor, string equipmentId)
        {
            _undo.AddLast(new UndoRecord { Placed = placed, Car = anchor.Car, Column = anchor.Column, Row = anchor.Row, Id = equipmentId });
            int depth = TrainEquipmentCatalog.Load() != null ? TrainEquipmentCatalog.Load().UndoDepth : 8;
            while (_undo.Count > depth) _undo.RemoveFirst();
        }

        /// <summary>가장 최근 기록의 역동작을 loadout에 적용한다(기록하지 않음). 적용하면 true.</summary>
        public static bool Undo(ITrainLoadout loadout)
        {
            if (loadout == null || _undo.Count == 0) return false;
            UndoRecord record = _undo.Last.Value;
            _undo.RemoveLast();
            bool ok = record.Placed ? loadout.TryRemove(record.Car, record.Column, record.Row) : loadout.TryPlace(record.Car, record.Column, record.Row, record.Id);
            Debug.Log($"UNDO: equipment {(record.Placed ? "place" : "remove")} {record.Id} car={record.Car} col={record.Column} row={record.Row} ok={ok} left={_undo.Count}");
            return ok;
        }

        private struct UndoRecord
        {
            public bool Placed;
            public int Car, Column, Row;
            public string Id;
        }
    }
}
