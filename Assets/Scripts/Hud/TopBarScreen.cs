using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Npc;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud
{
    /// <summary>
    /// 스토리 022(KoreanHud 상단 바의 UGUI 포팅). 항상 켜진 화면 "TopBar": 현재 위치·바퀴 수 라벨, 도감 (C) 버튼, 왼쪽 아래 출발·다음 라운드 버튼.
    /// 스토리 025: 승객 "승객 N/정원" 버튼(IPassengers, 정원은 NpcPool.json) → PassengersScreen.Toggle(P 키는 PassengersScreen이 읽는다).
    /// 다음 라운드는 전투 조우 중에만 보인다. 명령은 ITrackRuntime.Depart·ICombatRoundCommand로만 보내고 게임 상태를 직접 바꾸지 않는다.
    /// 참조는 인스펙터로 꽂고, 캔버스·서비스가 없으면 조용히 아무것도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class TopBarScreen : MonoBehaviour
    {
        private static TopBarScreen _instance;

        [SerializeField] private TMP_Text _location;
        [SerializeField] private TMP_Text _lap;
        [SerializeField] private Button _depart;
        [SerializeField] private Button _nextRound;
        [SerializeField] private Button _codex;
        [Tooltip("스토리 025: 승객 수 버튼(누르면 승객 화면).")]
        [SerializeField] private Button _passengers;
        [Tooltip("노드 id → 문자열 키 표(HudCatalog.json).")]
        [SerializeField] private TextAsset _catalogJson;

        private ITrackRuntime _track;
        private IHubStateSystem _hubs;
        private IWaypointResolver _waypoints;
        private ICombatRoundCommand _rounds;
        private IEncounterPresentationSource _encounter;
        private IPassengers _riders;
        private HudCatalog _catalog;
        private UiTheme _theme;

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
            if (placed == null || !placed.enabled || placed._location == null || placed._lap == null
                || placed._depart == null || placed._nextRound == null || placed._catalogJson == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        private void Initialize(UiScreens screens)
        {
            _theme = screens.Theme;
            _track = GameServices.Get<ITrackRuntime>();
            _hubs = GameServices.Get<IHubStateSystem>();
            _waypoints = GameServices.Get<IWaypointResolver>();
            _rounds = GameServices.Get<ICombatRoundCommand>();
            _encounter = GameServices.Get<IEncounterPresentationSource>();
            _catalog = JsonUtility.FromJson<HudCatalog>(_catalogJson.text);
            _depart.onClick.AddListener(Depart);
            _nextRound.onClick.AddListener(AdvanceRound);
            // 스토리 021: 도감 버튼(C 키는 CodexScreen이 읽는다).
            if (_codex != null) _codex.onClick.AddListener(IronRing.Hud.Codex.CodexScreen.Toggle);
            // 스토리 025: 승객 버튼(P 키는 PassengersScreen이 읽는다).
            if (_passengers != null) _passengers.onClick.AddListener(PassengersScreen.Toggle);
            _track.Arrived += OnArrived; _track.LapCompleted += OnLap;
            _hubs.RunLost += Refresh;
            _waypoints.EncounterStarted += OnArrived; _waypoints.EncounterEnded += OnEncounterEnded;
            if (_encounter != null) _encounter.RoundResolved += OnRound;
            Refresh();
        }

        /// <summary>위치·바퀴 수와 출발·다음 라운드 버튼의 표시·사용 가능 여부를 서비스 스냅샷으로 갱신한다.</summary>
        private void Refresh()
        {
            if (_track == null) return;
            _location.text = Strings.Get("hud_location") + "  " + Strings.Get(_catalog.NodeKey(_track.CurrentNode?.Id));
            _lap.text = Strings.Get("hud_turn") + "  " + _track.Lap;
            SetInteractable(_depart, !_hubs.IsRunLost && _track.IsHeld && _track.CurrentNode?.Kind == TrackNodeKind.Hub && !_waypoints.IsEncounterActive);
            bool combat = _waypoints.IsEncounterActive && _encounter != null && _encounter.IsCombat;
            if (_nextRound.gameObject.activeSelf != combat) _nextRound.gameObject.SetActive(combat);
            SetInteractable(_nextRound, !_hubs.IsRunLost && _rounds != null && _rounds.CanAdvance);
            RefreshPassengers();
        }

        /// <summary>승객 서비스를 늦게라도 얻고 탑승·하차·이탈을 구독한다.</summary>
        private void ResolvePassengers()
        {
            if (_riders != null) return;
            _riders = GameServices.Get<IPassengers>();
            if (_riders == null) return;
            _riders.Boarded += OnBoarded; _riders.Disembarked += OnDisembarked; _riders.Left += OnLeft;
        }

        /// <summary>"승객 N/정원" 라벨. 승객 서비스가 없는 데모 씬에서는 버튼을 숨긴다.</summary>
        private void RefreshPassengers()
        {
            if (_passengers == null) return;
            ResolvePassengers();
            bool on = _riders != null;
            if (_passengers.gameObject.activeSelf != on) _passengers.gameObject.SetActive(on);
            if (!on) return;
            TMP_Text label = _passengers.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = string.Format(Strings.Get("hud_passengers"), _riders.PassengerIds.Count, PassengersScreen.Capacity);
        }

        /// <summary>버튼을 켜고 끄며, 꺼진 버튼의 글자는 테마의 흐린 색으로 바꾼다.</summary>
        private void SetInteractable(Button button, bool on)
        {
            button.interactable = on;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && _theme != null) label.color = on ? _theme.Text : _theme.Muted;
        }

        private void Depart()
        {
            if (_hubs.IsRunLost || !_track.IsHeld || _waypoints.IsEncounterActive || _track.CurrentNode?.Kind != TrackNodeKind.Hub) return;
            _track.Depart(); Refresh();
        }

        private void AdvanceRound()
        {
            if (_hubs.IsRunLost || !_waypoints.IsEncounterActive || _encounter == null || !_encounter.IsCombat || _rounds == null || !_rounds.CanAdvance) return;
            _rounds.AdvanceCombatRound(); Refresh();
        }

        private void OnArrived(ITrackNode node) { Refresh(); }
        private void OnLap(int lap) { Refresh(); }
        private void OnRound(int round, bool won) { Refresh(); }
        private void OnEncounterEnded(ITrackNode node, EncounterOutcome outcome) { Refresh(); }
        private void OnBoarded(string npcId, string waypointId) { RefreshPassengers(); }
        private void OnDisembarked(string npcId, string hubId) { RefreshPassengers(); }
        private void OnLeft(string npcId) { RefreshPassengers(); }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_instance == this) _instance = null;
            if (_riders != null) { _riders.Boarded -= OnBoarded; _riders.Disembarked -= OnDisembarked; _riders.Left -= OnLeft; }
            if (_track == null) return;
            _track.Arrived -= OnArrived; _track.LapCompleted -= OnLap;
            _hubs.RunLost -= Refresh;
            _waypoints.EncounterStarted -= OnArrived; _waypoints.EncounterEnded -= OnEncounterEnded;
            if (_encounter != null) _encounter.RoundResolved -= OnRound;
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
            && GameServices.Get<IFacilityBuilder>() != null && GameServices.Get<IHubClickSource>() != null;
    }
}
