using UnityEngine;

using IronRing.Contracts.Presentation;

namespace IronRing.Onboarding
{
    /// <summary>
    /// 스펙 001 FR-001, FR-002. GameServices.Composing에서 OnboardingDirector를 만들어 등록하고, Ready 뒤에 이벤트 구독을 시작한다.
    /// </summary>
    public static class OnboardingInstaller
    {
        private const string TOPICS_PATH = "Onboarding/OnboardingTopics";

        public static OnboardingDirector Director { get; private set; }

        /// <summary>도메인 재로드가 꺼져 있으므로 플레이 진입마다 정적 상태와 구독을 지운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Ready -= OnReady;
            Director = null;
        }

        /// <summary>MainRoot의 GameServices.Reset()보다 뒤에 구독하도록 씬 로드 직전에 건다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Composing += OnComposing;
            GameServices.Ready -= OnReady;
            GameServices.Ready += OnReady;
        }

        /// <summary>OnboardingTopics.json을 읽어 Director를 만들고 GameServices에 등록한다(FR-001).</summary>
        private static void OnComposing()
        {
            if (Director != null) return;
            TextAsset text = Resources.Load<TextAsset>(TOPICS_PATH);
            Director = new OnboardingDirector(OnboardingTopicsData.Parse(text != null ? text.text : null));
            GameServices.Register(Director);
        }

        /// <summary>모든 서비스가 등록된 뒤 Director의 이벤트 구독을 시작한다(FR-002). Composing이 없었으면 여기서 만든다.</summary>
        private static void OnReady()
        {
            OnComposing();
            Director.Subscribe();
        }
    }
}
