using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud
{
    /// <summary>
    /// 스토리 022(KoreanHud #defeat의 UGUI 포팅). 상황 화면 "Defeat": IHubStateSystem.IsRunLost이면 전체 화면을 덮고 패배 제목·본문을 보인다.
    /// 제목은 IRunLossInfo의 사유(정착 기한·정착 거점 전부 붕괴)를 따르고, 서비스가 없으면 기본 제목을 쓴다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class DefeatScreen : MonoBehaviour
    {
        private static DefeatScreen _instance;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;

        private IHubStateSystem _hubs;
        private UiScreens _screens;
        private UiScreen _screen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        private static void Create()
        {
            if (_instance != null || !KoreanHudServices.Ready()) return;
            UiScreens screens = UiScreens.Instance;
            DefeatScreen placed = screens != null ? screens.Find<DefeatScreen>() : null;
            if (placed == null || !placed.enabled || placed._title == null || placed._body == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screen = GetComponent<UiScreen>();
            _hubs = GameServices.Get<IHubStateSystem>();
            _hubs.RunLost += Refresh;
            Refresh();
        }

        /// <summary>패배 상태면 사유별 제목·본문을 쓰고 화면을 켠다. 아니면 끈다.</summary>
        private void Refresh()
        {
            if (_screens == null) return;
            if (!_hubs.IsRunLost) { _screens.Hide(_screen.Id); return; }
            RunLossReason reason = GameServices.Get<IRunLossInfo>()?.LastLossReason ?? RunLossReason.None;
            _title.text = Strings.Get(reason == RunLossReason.DeadlineExpired ? "phase_loss_deadline"
                : reason == RunLossReason.AllSettledCollapsed ? "phase_loss_collapse" : "defeat_title");
            _body.text = Strings.Get("defeat_body");
            _screens.Show(_screen.Id);
        }

        /// <summary>RunLost 이벤트를 놓친 경우(구독 전 패배, 다른 경로로 IsRunLost가 바뀐 경우)에도 화면을 맞춘다.</summary>
        void LateUpdate()
        {
            if (_hubs != null && _screen != null && _hubs.IsRunLost != _screen.gameObject.activeSelf) Refresh();
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_hubs != null) _hubs.RunLost -= Refresh;
            if (_instance == this) _instance = null;
        }
    }
}
