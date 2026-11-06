using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Combat
{
    /// <summary>전투에서 획득한 설계도 단서 id를 현재 게임 동안 보관해 중복 보상을 판정한다.</summary>
    public static class BattleHintJournal
    {
        private static readonly List<string> _ids = new List<string>();

        /// <summary>새 플레이나 다시 시작에서 _ids를 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _ids.Clear();

        /// <summary>현재 게임에서 지금까지 얻은 설계도 단서 목록을 반환한다.</summary>
        public static IReadOnlyList<string> All() => _ids;

        // 스펙 003: 저장 슬롯용 단서 기록 되돌리기·새 게임 초기화 추가
        /// <summary>스펙 003. ids 목록으로 단서 기록을 통째로 바꾼다. null이면 비운다.</summary>
        public static void RestoreState(IEnumerable<string> ids)
        {
            _ids.Clear();
            if (ids != null) _ids.AddRange(ids);
        }

        /// <summary>스펙 003. 새 게임 시작 때 단서 기록을 비운다.</summary>
        public static void ResetForNewGame() => _ids.Clear();

        /// <summary>typeId 단서를 이전에 획득했는지 _ids에서 찾아 반환한다.</summary>
        public static bool Has(string typeId) => _ids.Contains(typeId);

        /// <summary>새 typeId를 _ids에 추가하고 true를 반환하며 이미 있거나 잘못된 id면 false를 반환한다.</summary>
        public static bool Record(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId) || Has(typeId)) return false;
            _ids.Add(typeId);
            return true;
        }
    }
}
