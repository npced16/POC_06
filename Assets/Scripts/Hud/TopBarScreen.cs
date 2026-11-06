using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Npc;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Onboarding;

namespace IronRing.Hud
{
    /// <summary>
    /// 스토리 022(KoreanHud 상단 바의 UGUI 포팅). 항상 켜진 화면 "TopBar": 현재 위치·바퀴 수 라벨, 도감 (C) 버튼, 왼쪽 아래 출발·다음 라운드 버튼.
    /// 스토리 025: 승객 "승객 N/정원" 버튼(IPassengers, 정원은 NpcPool.json) → PassengersScreen.Toggle(P 키는 PassengersScreen이 읽는다).
    /// 스토리 039: 전투가 사라져 다음 라운드·고철 표시를 지웠다. 휴식지 이벤트가 끝나면 그 문구를 토스트로 띄운다.
    /// 출발 명령은 IPhaseLoop가 있으면 그 서비스를 통해 보내고 게임 상태를 직접 바꾸지 않는다.
    /// 참조는 인스펙터로 꽂고, 캔버스·서비스가 없으면 조용히 아무것도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class TopBarScreen : MonoBehaviour
    {
        private static TopBarScreen _instance;

        [Header("상단 상태") ]
        [SerializeField] private TMP_Text _people;
        [SerializeField] private UnityEngine.UI.Image _peopleIcon;
        [SerializeField] private TMP_Text _balance;
        [SerializeField] private UnityEngine.UI.Image _coin;
        [SerializeField] private IconCatalog _icons;
        [SerializeField] private Button _depart;
        [SerializeField] private Button _codex;
        [Tooltip("스토리 025: 승객 수 버튼(누르면 승객 화면).")]
        [SerializeField] private Button _passengers;
        [SerializeField] private TMP_Text _passengerCount;
        [Tooltip("스펙 003: 게임 메뉴 톱니 버튼(누르면 게임 메뉴 열기·한 단계 닫기).")] // 스펙 003: 톱니 버튼 설명 추가
        [SerializeField] private Button _gameMenu; // 스펙 003: 톱니 버튼 참조 추가
        [Tooltip("스토리 039: 휴식지 이벤트 토스트 표시 시간(초).")]
        [SerializeField] private float _eventToastSeconds = 2.5f;

        private IPopulation _population;
        private IViewMode _view;
        private IMoney _money;
        private ITrackRuntime _track;
        private IHubStateSystem _hubs;
        private IWaypointResolver _waypoints;
        private IEncounterPresentationSource _encounter;
        private IPassengers _riders;
        private UiTheme _theme;
        private IPhaseLoop _loop;
        private IRestEvents _restEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>KoreanHud와 같은 서비스 조건(HUD 계약 8개)이 갖춰지고 MainCanvas에 화면이 있을 때 한 번 바인딩한다.</summary>
        private static void Create()
        {
            if (_instance != null || !KoreanHudServices.Ready()) return;
            UiScreens screens = UiScreens.Instance;
            TopBarScreen placed = screens != null ? screens.Find<TopBarScreen>() : null;
            if (placed == null || !placed.enabled || placed._balance == null || placed._depart == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        /// <summary>screens의 테마와 준비된 게임 서비스를 저장하고 상태 및 잔고 이벤트를 구독한다.</summary>
        private void Initialize(UiScreens screens)
        {
            _theme = screens.Theme;
            // 스토리 045: 옛 프리팹(주민·잔고 칸이 없는 것)에서도 Play가 막히지 않게 모든 참조를 null 가드한다.
            if (_people != null) _people.gameObject.SetActive(false);
            if (_peopleIcon != null) { _peopleIcon.gameObject.SetActive(false); if (_icons != null) _peopleIcon.sprite = _icons.Get("ui_people"); }
            if (_people != null)
            {
                ScreenTicker.Add(ResolvePopulation);
                ResolvePopulation();
            }
            _money = _balance != null ? GameServices.Get<IMoney>() : null;
            if (_balance != null) _balance.gameObject.SetActive(_money != null);
            if (_coin != null) _coin.gameObject.SetActive(_money != null);
            if (_money != null) { _money.Changed += RefreshBalance; if (_coin != null && _icons != null) _coin.sprite = _icons.Get("ui_coin"); RefreshBalance(); }
            if (_coin != null && _coin.sprite == null) _coin.gameObject.SetActive(false);
            _track = GameServices.Get<ITrackRuntime>();
            _view = GameServices.Get<IViewMode>();
            if (_view != null) _view.InteriorChanged += OnInteriorChanged;
            _hubs = GameServices.Get<IHubStateSystem>();
            _waypoints = GameServices.Get<IWaypointResolver>();
            _encounter = GameServices.Get<IEncounterPresentationSource>();
            _loop = GameServices.Get<IPhaseLoop>();
            _restEvents = GameServices.Get<IRestEvents>();
            if (_loop != null) _loop.PhaseChanged += OnLoopPhaseChanged;
            if (_restEvents != null)
            {
                _restEvents.EventStarted += Refresh;
                _restEvents.EventClosed += Refresh;
            }
            _depart.onClick.AddListener(Depart);
            // 스토리 021: 도감 버튼(C 키는 CodexScreen이 읽는다).
            if (_codex != null) _codex.onClick.AddListener(IronRing.Hud.Codex.CodexScreen.Toggle);
            // 스토리 025: 승객 버튼(P 키는 PassengersScreen이 읽는다).
            if (_passengers != null) _passengers.onClick.AddListener(PassengersScreen.Toggle);
            if (_gameMenu != null) _gameMenu.onClick.AddListener(GameMenuScreen.Toggle); // 스펙 003: 톱니 버튼 클릭 시 게임 메뉴 열기
            _track.Arrived += OnArrived; _track.LapCompleted += OnLap;
            _hubs.RunLost += Refresh;
            _waypoints.EncounterStarted += OnArrived; _waypoints.EncounterEnded += OnEncounterEnded;
            if (_encounter != null) _encounter.EventResolved += OnEventResolved;
            Refresh();
        }

        /// <summary>현재 진행 상태에서 출발 버튼과 주민·승객 표시를 갱신한다.</summary>
        private void Refresh()
        {
            if (_track == null) return;
            bool canDepart = _loop != null ? _loop.CanDepart : _track.IsHeld && _track.CurrentNode?.Kind == TrackNodeKind.Hub && !_waypoints.IsEncounterActive;
            _depart.gameObject.SetActive(!_hubs.IsRunLost && canDepart);
            SetInteractable(_depart, !_hubs.IsRunLost && canDepart && (OnboardingInstaller.Director == null || OnboardingInstaller.Director.BasicTutorialCompleted));
            RefreshPeople();
            RefreshPassengers();
        }

        /// <summary>승객 서비스를 늦게라도 얻고 탑승·하차·이탈을 구독한다.</summary>
        private void ResolvePassengers()
        {
            if (_riders != null) return;
            _riders = GameServices.Get<IPassengers>();
            if (_riders == null) return;
            _riders.Boarded += OnBoarded; _riders.Disembarked += OnDisembarked; _riders.Left += OnLeft; _riders.Returned += OnDisembarked;
        }

        /// <summary>승객 아이콘의 숫자를 현재 탑승 인원으로 갱신한다. 승객 서비스가 없으면 버튼을 숨긴다.</summary>
        private void RefreshPassengers()
        {
            if (_passengers == null) return;
            ResolvePassengers();
            bool on = _riders != null;
            if (_passengers.gameObject.activeSelf != on) _passengers.gameObject.SetActive(on);
            if (!on) return;
            if (_passengerCount != null) _passengerCount.text = _riders.PassengerIds.Count.ToString();
            else
            {
                TMP_Text label = _passengers.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = string.Format(Strings.Get("hud_passengers"), _riders.PassengerIds.Count, PassengersScreen.Capacity);
            }
        }

        /// <summary>버튼을 켜고 끄며, 꺼진 버튼의 글자는 테마의 흐린 색으로 바꾼다.</summary>
        private void SetInteractable(Button button, bool on)
        {
            button.interactable = on;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && _theme != null) label.color = on ? _theme.Text : _theme.Muted;
        }

        /// <summary>늦게 등록되는 주민 서비스를 찾아 이벤트에 연결하고 준비 후 탐색을 중단한다.</summary>
        private void ResolvePopulation()
        {
            if (_population != null) return;
            _population = GameServices.Get<IPopulation>();
            if (_population == null) return;
            _population.Changed += RefreshPeople;
            _people.gameObject.SetActive(true); if (_peopleIcon != null) _peopleIcon.gameObject.SetActive(_peopleIcon.sprite != null); // 카탈로그에 ui_people이 없으면 흰 사각형 대신 숨김
            RefreshPeople(); ScreenTicker.Remove(ResolvePopulation);
        }

        /// <summary>현재 일반 주민 수를 읽어 상단 주민 라벨을 갱신한다.</summary>
        private void RefreshPeople()
        {
            if (_people == null || _population == null) return;
            string hubId = _population.CurrentHubId;
            string town = Strings.Get(hubId.Replace('-', '_'));
            _people.text = string.Format(Strings.Get("ui_topbar_people_at"), town, _population.ResidentsAt(hubId));
        }

        /// <summary>열린 거점 내부 변경을 받아 현재 마을 주민 표시를 갱신한다.</summary>
        private void OnInteriorChanged(bool open) { RefreshPeople(); }

        /// <summary>현재 잔고를 읽어 상단 라벨을 갱신하고 음수 잔고를 테마 강조색으로 표시한다.</summary>
        private void RefreshBalance()
        {
            _balance.text = _money.Balance.ToString();
            if (_theme != null) _balance.color = _money.Balance < 0 ? _theme.Accent : _theme.Text;
        }

        /// <summary>현재 거점 정차 상태를 확인하고 열차에 출발을 요청한 뒤 상태 표시를 갱신한다.</summary>
        private void Depart()
        {
            if (OnboardingInstaller.Director != null && !OnboardingInstaller.Director.BasicTutorialCompleted) return;
            if (_hubs.IsRunLost) return;
            if (_loop != null) { if (!_loop.CanDepart) return; _loop.Depart(); }
            else { if (!_track.IsHeld || _waypoints.IsEncounterActive || _track.CurrentNode?.Kind != TrackNodeKind.Hub) return; _track.Depart(); }
            Refresh();
        }

        /// <summary>새 phase와 lap을 받은 뒤 상단 짧은 표시와 출발 버튼을 갱신한다.</summary>
        private void OnLoopPhaseChanged(LoopPhase phase, int lap) { Refresh(); }

        private void OnArrived(ITrackNode node) { Refresh(); }
        private void OnLap(int lap) { Refresh(); }
        /// <summary>스토리 039: 휴식지 이벤트 문구(문자열 키)를 토스트로 보이고 버튼 상태를 다시 그린다.</summary>
        private void OnEventResolved(string key) { if (_restEvents == null) IronRing.Hud.Ui.ToastScreen.Show(Strings.Get(key), _eventToastSeconds); Refresh(); }
        private void OnEncounterEnded(ITrackNode node, EncounterOutcome outcome) { Refresh(); }
        private void OnBoarded(string npcId, string waypointId) { RefreshPassengers(); }
        private void OnDisembarked(string npcId, string hubId) { RefreshPassengers(); }
        private void OnLeft(string npcId) { RefreshPassengers(); }

        void Update()
        {
            if (_track == null || !_depart.gameObject.activeSelf) return;
            bool tutorialComplete = OnboardingInstaller.Director == null || OnboardingInstaller.Director.BasicTutorialCompleted;
            if (_depart.interactable != tutorialComplete) Refresh();
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_money != null) _money.Changed -= RefreshBalance;
            ScreenTicker.Remove(ResolvePopulation);
            if (_population != null) _population.Changed -= RefreshPeople;
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_loop != null) _loop.PhaseChanged -= OnLoopPhaseChanged;
            if (_restEvents != null) { _restEvents.EventStarted -= Refresh; _restEvents.EventClosed -= Refresh; }
            if (_instance == this) _instance = null;
            if (_riders != null) { _riders.Boarded -= OnBoarded; _riders.Disembarked -= OnDisembarked; _riders.Left -= OnLeft; _riders.Returned -= OnDisembarked; }
            if (_track == null) return;
            _track.Arrived -= OnArrived; _track.LapCompleted -= OnLap;
            _hubs.RunLost -= Refresh;
            _waypoints.EncounterStarted -= OnArrived; _waypoints.EncounterEnded -= OnEncounterEnded;
            if (_encounter != null) _encounter.EventResolved -= OnEventResolved;
        }
    }

    /// <summary>스토리 022. 옛 KoreanHud가 화면을 만들던 서비스 조건(다른 스토리의 독립 데모는 이 계약을 다 등록하지 않는다).</summary>
    internal static class KoreanHudServices
    {
        /// <summary>HUD 계약 8개가 모두 등록되어 있는지.</summary>
        public static bool Ready() =>
            GameServices.Get<ITrackRuntime>() != null && GameServices.Get<IResourceInventory>() != null
            && GameServices.Get<IHabitatSystem>() != null && GameServices.Get<INpcRecruiter>() != null
            && GameServices.Get<IHubStateSystem>() != null && GameServices.Get<IWaypointResolver>() != null
            && GameServices.Get<IHubClickSource>() != null;
    }
}
