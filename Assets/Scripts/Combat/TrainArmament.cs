using System;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Combat
{
    /// <summary>거점에서 설정한 네 거치대의 포탑과 강화 단계를 저장한다.</summary>
    public static class TrainArmament
    {
        private const string SAVE_KEY = "IronRing.TrainArmament.v1";
        public const int SLOT_COUNT = 4;

        [Serializable]
        private sealed class SaveData
        {
            public string[] ids = new string[SLOT_COUNT];
            public int[] levels = new int[SLOT_COUNT];
        }

        private static SaveData _data;
        public static event Action Changed;

        /// <summary>저장된 장비를 읽고 저장이 없으면 첫 칸에 기본 포탑을 제공한다.</summary>
        private static void EnsureLoaded()
        {
            if (_data != null) return;
            string raw = PlayerPrefs.GetString(SAVE_KEY, "");
            _data = string.IsNullOrEmpty(raw) ? null : JsonUtility.FromJson<SaveData>(raw);
            if (_data != null && _data.ids != null && _data.levels != null && _data.ids.Length == SLOT_COUNT && _data.levels.Length == SLOT_COUNT) return;
            _data = new SaveData();
            _data.ids[0] = "scrap";
            _data.levels[0] = 1;
        }

        /// <summary>slot에 저장된 포탑 id를 반환한다.</summary>
        public static string TurretId(int slot) { EnsureLoaded(); return _data.ids[slot]; }

        /// <summary>slot에 저장된 포탑의 강화 단계를 반환한다.</summary>
        public static int Level(int slot) { EnsureLoaded(); return _data.levels[slot]; }

        /// <summary>slot의 포탑을 환불 없이 철거하고 변경 상태를 저장한다.</summary>
        public static void Remove(int slot)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(_data.ids[slot])) return;
            _data.ids[slot] = null;
            _data.levels[slot] = 0;
            Save();
        }

        /// <summary>from과 to의 포탑과 강화 단계를 비용 없이 교환한다.</summary>
        public static void Move(int from, int to)
        {
            EnsureLoaded();
            if (from == to || string.IsNullOrEmpty(_data.ids[from])) return;
            (_data.ids[from], _data.ids[to]) = (_data.ids[to], _data.ids[from]);
            (_data.levels[from], _data.levels[to]) = (_data.levels[to], _data.levels[from]);
            Save();
        }

        /// <summary>slot에 id를 설치하거나 강화하고 rules의 비용을 money에서 지불해 저장한다.</summary>
        public static bool Apply(int slot, string id, IMoney money, BattleEconomyRule rules)
        {
            EnsureLoaded();
            if (slot < 0 || slot >= SLOT_COUNT || string.IsNullOrEmpty(id) || money == null) return false;
            bool upgrade = _data.ids[slot] == id;
            int cost = upgrade ? rules.TurretUpgradeMarks : rules.TurretInstallMarks;
            if (!money.TrySpend(cost, "train-armament")) return false;
            _data.ids[slot] = id;
            _data.levels[slot] = upgrade ? _data.levels[slot] + 1 : 1;
            Save();
            return true;
        }

        /// <summary>장비 상태를 저장하고 장착 표시 갱신을 알린다.</summary>
        private static void Save()
        {
            PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
