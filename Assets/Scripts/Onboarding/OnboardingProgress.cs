using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Onboarding
{
    /// <summary>현재 게임에서 완료한 학습 항목을 메모리에만 기록한다.</summary>
    public static class OnboardingProgress
    {
        private static readonly HashSet<string> _seen = new();

        /// <summary>새 플레이 시작 시 이전 플레이의 학습 기록을 지운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => _seen.Clear();

        /// <summary>topicId 항목을 현재 게임에서 이미 봤는지 반환한다.</summary>
        public static bool IsSeen(string topicId) => _seen.Contains(topicId);

        /// <summary>topicId 항목의 현재 게임 기록을 지운다(개발자 메뉴 재현용).</summary>
        public static void Reset(string topicId)
        {
            _seen.Remove(topicId);
        }

        /// <summary>topicIds 항목들의 현재 게임 기록을 모두 지운다.</summary>
        public static void ResetAll(IEnumerable<string> topicIds)
        {
            foreach (string topicId in topicIds) _seen.Remove(topicId);
        }

        /// <summary>topicId 항목을 현재 게임의 본 항목으로 기록한다.</summary>
        public static void MarkSeen(string topicId)
        {
            _seen.Add(topicId);
        }
    }
}
