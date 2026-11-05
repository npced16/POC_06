using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Onboarding
{
    /// <summary>스펙 001 FR-004, FR-011. 본 학습 항목 기록(PlayerPrefs onboarding_seen_&lt;id&gt;).</summary>
    public static class OnboardingProgress
    {
        private const string KEY_PREFIX = "onboarding_seen_";

        /// <summary>topicId 항목을 이미 봤는지 PlayerPrefs에서 읽어 돌려준다.</summary>
        public static bool IsSeen(string topicId) => PlayerPrefs.GetInt(KEY_PREFIX + topicId, 0) == 1;

        /// <summary>topicId 항목의 본 기록을 PlayerPrefs에서 지운다(개발자 메뉴 재현용).</summary>
        public static void Reset(string topicId)
        {
            PlayerPrefs.DeleteKey(KEY_PREFIX + topicId);
            PlayerPrefs.Save();
        }

        /// <summary>topicIds 항목들의 본 기록을 PlayerPrefs에서 모두 지운다(FR-025).</summary>
        public static void ResetAll(IEnumerable<string> topicIds)
        {
            foreach (string topicId in topicIds) PlayerPrefs.DeleteKey(KEY_PREFIX + topicId);
            PlayerPrefs.Save();
        }

        /// <summary>topicId 항목을 본 항목으로 PlayerPrefs에 1로 저장한다.</summary>
        public static void MarkSeen(string topicId)
        {
            PlayerPrefs.SetInt(KEY_PREFIX + topicId, 1);
            PlayerPrefs.Save();
        }
    }
}
