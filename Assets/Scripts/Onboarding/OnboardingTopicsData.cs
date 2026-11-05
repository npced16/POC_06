using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Onboarding
{
    /// <summary>스펙 001 FR-001. 학습 항목 하나(OnboardingTopics.json의 한 행).</summary>
    [Serializable]
    public sealed class OnboardingTopic
    {
        [SerializeField] private string _id;
        [SerializeField] private int _order;
        [SerializeField] private string _kind;
        [SerializeField] private string _textKey;
        [SerializeField] private string _mascot;
        [SerializeField] private string[] _guide;
        [SerializeField] private bool _chain;

        public string Id => _id;
        public int Order => _order;
        /// <summary>true = 행동형(행동하면 닫힘), false = 안내형(확인 버튼으로 닫힘).</summary>
        public bool IsAction => _kind == "action";
        public string TextKey => _textKey;
        /// <summary>마스코트 표정: default, happy, alert.</summary>
        public string Mascot => string.IsNullOrEmpty(_mascot) ? "default" : _mascot;
        /// <summary>안내 배치 패턴(왼쪽부터 칸마다 놓을 재료 id). 없으면 빈 배열.</summary>
        public string[] Guide => _guide ?? System.Array.Empty<string>();
        /// <summary>true = 기본 학습 묶음. 앞 순서 묶음 항목을 모두 본 뒤에만 뜬다(FR-024).</summary>
        public bool Chain => _chain;
    }

    /// <summary>스펙 001 FR-001. OnboardingTopics.json 전체.</summary>
    [Serializable]
    public sealed class OnboardingTopicsData
    {
        [SerializeField] private List<OnboardingTopic> _topics = new();

        public IReadOnlyList<OnboardingTopic> Topics => _topics;

        /// <summary>
        /// json 문자열을 읽어 항목 목록을 만들고 order 순으로 정렬한다.
        /// 비었거나 id가 없는 항목은 버리며, 읽은 데이터(실패하면 빈 목록)를 돌려준다.
        /// </summary>
        public static OnboardingTopicsData Parse(string json)
        {
            OnboardingTopicsData data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<OnboardingTopicsData>(json);
            data ??= new OnboardingTopicsData();
            data._topics ??= new List<OnboardingTopic>();
            data._topics.RemoveAll(topic => topic == null || string.IsNullOrEmpty(topic.Id));
            data._topics.Sort((a, b) => a.Order.CompareTo(b.Order));
            return data;
        }
    }
}
