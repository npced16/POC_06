using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Npc
{
    /// <summary>한 번 탑승하거나 합류한 승객의 도감 발견 기록을 현재 게임 동안 보관한다.</summary>
    public static class PassengerDiscovery
    {
        private static readonly HashSet<string> _knownIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>새 플레이나 다시 시작에서 _knownIds를 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _knownIds.Clear();

        // 스펙 003: 타이틀에서 새 게임을 시작할 때 발견 기록을 비우는 메서드 추가
        /// <summary>새 게임 시작 때 _knownIds를 비운다. SaveInstaller가 새 게임 분기에서 부른다.</summary>
        public static void ResetForNewGame() => _knownIds.Clear();

        /// <summary>npcId가 현재 게임에서 한 번이라도 합류했는지 발견 기록에서 찾아 반환한다.</summary>
        public static bool IsKnown(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return false;
            return _knownIds.Contains(npcId);
        }

        /// <summary>합류한 npcId를 _knownIds에 추가한다.</summary>
        public static void Remember(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            _knownIds.Add(npcId);
        }
    }
}
