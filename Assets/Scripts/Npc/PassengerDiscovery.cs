using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Npc
{
    /// <summary>한 번 탑승하거나 합류한 승객의 도감 발견 기록을 게임 실행 사이에도 보관한다.</summary>
    public static class PassengerDiscovery
    {
        private const string PREF_KEY = "IronRing.PassengerDiscovery.v1";
        private static HashSet<string> _knownIds;

        /// <summary>새 런타임 세션에서 저장된 승객 발견 기록을 다시 읽도록 메모리 캐시를 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _knownIds = null;

        /// <summary>npcId가 과거에 한 번이라도 합류했는지 저장된 발견 기록에서 반환한다.</summary>
        public static bool IsKnown(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return false;
            Load();
            return _knownIds.Contains(npcId);
        }

        /// <summary>합류한 npcId를 발견 기록에 추가하고 새 기록이면 PlayerPrefs에 저장한다.</summary>
        public static void Remember(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            Load();
            if (!_knownIds.Add(npcId)) return;
            var sorted = new List<string>(_knownIds);
            sorted.Sort(StringComparer.Ordinal);
            PlayerPrefs.SetString(PREF_KEY, string.Join("\n", sorted));
            PlayerPrefs.Save();
        }

        /// <summary>PlayerPrefs의 승객 id 목록을 처음 사용할 때 읽어 _knownIds에 보관한다.</summary>
        private static void Load()
        {
            if (_knownIds != null) return;
            _knownIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in PlayerPrefs.GetString(PREF_KEY, string.Empty).Split('\n'))
                if (!string.IsNullOrWhiteSpace(id)) _knownIds.Add(id);
        }
    }
}
