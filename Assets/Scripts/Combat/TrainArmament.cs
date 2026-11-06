using System;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Combat
{
    /// <summary>거점에서 설정한 네 거치대의 포탑과 강화 단계를 현재 게임 동안 보관한다.</summary>
    public static class TrainArmament
    {
        public const int SLOT_COUNT = 4;

        private static string[] _ids;
        private static int[] _levels;
        public static event Action Changed;

        /// <summary>새 플레이나 다시 시작에서 _ids와 _levels를 비워 기본 장비로 돌아가게 한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _ids = null;
            _levels = null;
        }

        // 스펙 003: 저장 슬롯용 포탑 장비 꺼내기·되돌리기·새 게임 초기화 추가
        /// <summary>스펙 003. 지금 포탑 id와 단계를 JSON 문자열로 꺼낸다.</summary>
        public static string CaptureState()
        {
            EnsureInitialized();
            return JsonUtility.ToJson(new ArmamentSave { Ids = (string[])_ids.Clone(), Levels = (int[])_levels.Clone() });
        }

        /// <summary>스펙 003. CaptureState가 만든 json으로 포탑 장비를 되돌리고 Changed를 낸다. 칸 수가 맞지 않으면 기본 장비로 둔다.</summary>
        public static void RestoreState(string json)
        {
            ArmamentSave state = JsonUtility.FromJson<ArmamentSave>(json);
            ResetStatics();
            EnsureInitialized();
            if (state.Ids != null && state.Levels != null && state.Ids.Length == SLOT_COUNT && state.Levels.Length == SLOT_COUNT)
            {
                _ids = state.Ids;
                _levels = state.Levels;
            }
            Changed?.Invoke();
        }

        /// <summary>스펙 003. 새 게임 시작 때 기본 장비로 되돌리고 Changed를 낸다.</summary>
        public static void ResetForNewGame()
        {
            ResetStatics();
            Changed?.Invoke();
        }

        /// <summary>스펙 003. 저장 형식: 칸별 포탑 id와 단계.</summary>
        [Serializable]
        private sealed class ArmamentSave
        {
            public string[] Ids;
            public int[] Levels;
        }

        /// <summary>장비가 비어 있으면 _ids와 _levels를 만들고 첫 칸에 기본 포탑을 제공한다.</summary>
        private static void EnsureInitialized()
        {
            if (_ids != null) return;
            _ids = new string[SLOT_COUNT];
            _levels = new int[SLOT_COUNT];
            _ids[0] = "scrap";
            _levels[0] = 1;
        }

        /// <summary>slot에 설치된 포탑 id를 반환한다.</summary>
        public static string TurretId(int slot) { EnsureInitialized(); return _ids[slot]; }

        /// <summary>slot에 설치된 포탑의 강화 단계를 반환한다.</summary>
        public static int Level(int slot) { EnsureInitialized(); return _levels[slot]; }

        /// <summary>slot의 포탑을 환불 없이 철거하고 장비 변경을 알린다.</summary>
        public static void Remove(int slot)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(_ids[slot])) return;
            _ids[slot] = null;
            _levels[slot] = 0;
            Changed?.Invoke();
        }

        /// <summary>from과 to의 포탑과 강화 단계를 비용 없이 교환한다.</summary>
        public static void Move(int from, int to)
        {
            EnsureInitialized();
            if (from == to || string.IsNullOrEmpty(_ids[from])) return;
            (_ids[from], _ids[to]) = (_ids[to], _ids[from]);
            (_levels[from], _levels[to]) = (_levels[to], _levels[from]);
            Changed?.Invoke();
        }

        /// <summary>slot에 id를 설치하거나 강화하고 rules의 비용을 money에서 지불한 뒤 성공 여부를 반환한다.</summary>
        public static bool Apply(int slot, string id, IMoney money, BattleEconomyRule rules)
        {
            EnsureInitialized();
            if (slot < 0 || slot >= SLOT_COUNT || string.IsNullOrEmpty(id) || money == null) return false;
            bool upgrade = _ids[slot] == id;
            int cost = upgrade ? rules.TurretUpgradeMarks : rules.TurretInstallMarks;
            if (!money.TrySpend(cost, "train-armament")) return false;
            _ids[slot] = id;
            _levels[slot] = upgrade ? _levels[slot] + 1 : 1;
            Changed?.Invoke();
            return true;
        }
    }
}
