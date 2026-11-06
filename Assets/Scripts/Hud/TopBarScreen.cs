using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Npc;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Onboarding;
using IronRing.Presentation.Interior;
using IronRing.Habitats;
using IronRing.Items;
using IronRing.Npc;

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
        private static readonly string[] RESOURCE_IDS = { "tile_stone", "tile_timber", "tile_water_can", "tile_grain", "tile_coal", "tile_iron_stock", "tile_copper", "tile_steam_pipe", "tile_gear", "tile_wire" };

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
        [SerializeField] private GameObject _resourceRow;
        [SerializeField] private GameObject[] _resourceItems;
        [SerializeField] private TMP_Text[] _resourceCounts;
        [SerializeField] private RectTransform _habitatInfo;
        [SerializeField] private TMP_Text _habitatInfoTitle;
        [SerializeField] private TMP_Text _habitatInfoBody;
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
        private ITileStock _stock;
        private IHabitatCodex _codexState;
        private HabitatProductionData _habitatProduction;
        private readonly int[] _productionTotals = new int[RESOURCE_IDS.Length];
        private readonly HashSet<string> _productionHubs = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>itemId의 보유량 표시에 연결된 아이콘을 반환하며 해당 재료나 화면이 없으면 null을 반환한다.</summary>
        public static Sprite ResourceIcon(string itemId)
        {
            if (_instance == null || _instance._resourceItems == null) return null;
            int index = Array.IndexOf(RESOURCE_IDS, itemId);
            if (index < 0 || index >= _instance._resourceItems.Length || _instance._resourceItems[index] == null) return null;
            UnityEngine.UI.Image icon = _instance._resourceItems[index].GetComponentInChildren<UnityEngine.UI.Image>(true);
            return icon != null ? icon.sprite : null;
        }

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
            if (_coin != null) _coin.gameObject.SetActive(_money != null && _coin.sprite != null);
            if (_money != null) { _money.Changed += RefreshBalance; RefreshBalance(); }
            _track = GameServices.Get<ITrackRuntime>();
            _view = GameServices.Get<IViewMode>();
            if (_view != null) _view.InteriorChanged += OnInteriorChanged;
            _stock = GameServices.Get<ITileStock>();
            _codexState = GameServices.Get<IHabitatCodex>();
            TextAsset production = Resources.Load<TextAsset>("Habitats/HabitatProduction");
            if (production != null) _habitatProduction = JsonUtility.FromJson<HabitatProductionData>(production.text);
            if (_stock != null) _stock.Changed += OnResourceChanged;
            if (_codexState != null) _codexState.TileUnlocked += OnResourcesUnlocked;
            RefreshResources();
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
            // 스펙 001: 기본 학습 전처럼 눌 수 없을 때 버튼 전체(배경·아이콘)를 반투명하게 보이게 함
            foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                Color color = graphic.color;
                color.a = on ? 1f : 0.45f;
                graphic.color = color;
            }
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
        private void OnInteriorChanged(bool open) { RefreshPeople(); RefreshResources(); }

        /// <summary>재고 품목과 수량 변경을 받아 재료 표시를 현재 상태로 갱신한다.</summary>
        private void OnResourceChanged(string itemId, int count) => RefreshResources();

        /// <summary>해금된 품목 목록과 건물 id를 받아 재료 표시를 현재 상태로 갱신한다.</summary>
        private void OnResourcesUnlocked(IReadOnlyList<string> itemIds, string typeId) => RefreshResources();

        /// <summary>도감 해금 상태와 재고를 읽어 마을 재료 목록의 표시와 수량을 갱신한다.</summary>
        private void RefreshResources()
        {
            if (_resourceRow == null || _resourceItems == null || _resourceCounts == null) return;
            bool visible = _view != null && _view.InteriorOpen && _stock != null && _codexState != null;
            if (_resourceRow.activeSelf != visible) _resourceRow.SetActive(visible);
            if (!visible) return;
            RefreshProductionTotals();
            int count = Mathf.Min(RESOURCE_IDS.Length, Mathf.Min(_resourceItems.Length, _resourceCounts.Length));
            for (int i = 0; i < count; i++)
            {
                bool unlocked = _codexState.IsTileUnlocked(RESOURCE_IDS[i]);
                if (_resourceItems[i].activeSelf != unlocked) _resourceItems[i].SetActive(unlocked);
                if (unlocked)
                {
                    string value = _stock.CountOf(RESOURCE_IDS[i]) + " (+" + _productionTotals[i] + ")";
                    if (_resourceCounts[i].text != value) _resourceCounts[i].text = value;
                }
            }
        }

        /// <summary>모든 마을의 완성 서식지와 생산 데이터를 집계해 재료별 바퀴당 생산량을 갱신한다.</summary>
        private void RefreshProductionTotals()
        {
            Array.Clear(_productionTotals, 0, _productionTotals.Length);
            _productionHubs.Clear();
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            if (_hubs == null || clusters == null || _habitatProduction?._entries == null) return;
            foreach (string hubId in _hubs.HubIds)
            {
                if (string.IsNullOrEmpty(hubId) || !_productionHubs.Add(hubId)) continue;
                foreach (HabitatCluster cluster in clusters.CompletedAt(hubId))
                {
                    foreach (HabitatProductionEntry entry in _habitatProduction._entries)
                    {
                        if (entry == null || entry._habitatId != cluster.TypeId || entry._amount <= 0 || string.IsNullOrEmpty(entry._itemId)) continue;
                        int index = Array.IndexOf(RESOURCE_IDS, entry._itemId);
                        if (index >= 0) _productionTotals[index] += entry._amount;
                    }
                }
            }
        }

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
            RefreshResources();
            RefreshHabitatInfo();
            if (_track == null || !_depart.gameObject.activeSelf) return;
            bool tutorialComplete = OnboardingInstaller.Director == null || OnboardingInstaller.Director.BasicTutorialCompleted;
            if (_depart.interactable != tutorialComplete) Refresh();
        }

        /// <summary>현재 포인터와 마을 상태를 읽어 서식지 정보창을 표시하거나 숨기고 위치를 화면 안으로 제한한다.</summary>
        private void RefreshHabitatInfo()
        {
            if (_habitatInfo == null) return;
            PlacementGhost ghost = PlacementGhost.Instance;
            Mouse mouse = Mouse.current;
            bool blocked = mouse == null || _view == null || !_view.InteriorOpen || ghost == null || ghost.IsDragging || ghost.IsMovingHabitat || ghost.Surface != null || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            UiScreens screens = UiScreens.Instance;
            if (!blocked && screens != null)
                foreach (UiScreen screen in screens.Screens)
                    if (screen != null && !screen.AlwaysOn && screen.gameObject.activeInHierarchy) { blocked = true; break; }
            HabitatCluster cluster = blocked ? null : ghost.BuildingAt(_view.InteriorHubId, ghost.HoverCell);
            HabitatType type = cluster == null ? null : HabitatsInstaller.Catalog?.Get(cluster.TypeId);
            if (type == null) { if (_habitatInfo.gameObject.activeSelf) _habitatInfo.gameObject.SetActive(false); return; }
            _habitatInfoTitle.text = type.Name;
            _habitatInfoBody.text = HabitatResidents(_view.InteriorHubId, cluster) + "\n" + HabitatYield(type);
            RectTransform area = _habitatInfo.parent as RectTransform;
            if (area != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, mouse.position.ReadValue(), null, out Vector2 point))
            {
                Rect bounds = area.rect;
                Vector2 size = _habitatInfo.rect.size;
                float x = Mathf.Clamp(point.x + 16f, bounds.xMin, bounds.xMax - size.x);
                float y = Mathf.Clamp(point.y - 16f, bounds.yMin + size.y, bounds.yMax);
                _habitatInfo.anchoredPosition = new Vector2(x, y);
            }
            if (!_habitatInfo.gameObject.activeSelf) _habitatInfo.gameObject.SetActive(true);
        }

        /// <summary>hubId의 실제 집 배정 중 cluster와 칸이 같은 주민을 찾아 이름 또는 빈집 문구를 반환한다.</summary>
        private static string HabitatResidents(string hubId, HabitatCluster cluster)
        {
            INpcHomes homes = GameServices.Get<INpcHomes>();
            var names = new List<string>();
            if (homes != null)
                foreach (NpcHome home in homes.HomesAt(hubId))
                    if (home.Cells != null && home.Cells.Count == cluster.Cells.Count && new HashSet<int>(home.Cells).SetEquals(cluster.Cells))
                        names.Add(NpcInstaller.Pool?.Find(home.NpcId)?.Name ?? home.NpcId);
            return names.Count == 0 ? Strings.Get("habitat_info_empty") : string.Format(Strings.Get("habitat_info_residents"), string.Join(", ", names));
        }

        /// <summary>type의 생산 데이터와 마르크 수입을 읽어 바퀴당 산출 문구를 반환한다.</summary>
        private string HabitatYield(HabitatType type)
        {
            var parts = new List<string>();
            if (_habitatProduction?._entries != null)
                foreach (HabitatProductionEntry entry in _habitatProduction._entries)
                    if (entry != null && entry._habitatId == type.Id && entry._amount > 0 && !string.IsNullOrEmpty(entry._itemId))
                        parts.Add((ItemsInstaller.Catalog?.Get(entry._itemId)?.Name ?? entry._itemId) + " " + entry._amount);
            if (type.Income > 0) parts.Add(string.Format(Strings.Get("habitat_info_marks"), type.Income));
            return parts.Count == 0 ? Strings.Get("habitat_info_no_yield") : string.Format(Strings.Get("habitat_info_yield"), string.Join(" · ", parts));
        }

        [Serializable]
        private sealed class HabitatProductionData { public HabitatProductionEntry[] _entries; }

        [Serializable]
        private sealed class HabitatProductionEntry { public string _habitatId; public string _itemId; public int _amount; }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_money != null) _money.Changed -= RefreshBalance;
            if (_stock != null) _stock.Changed -= OnResourceChanged;
            if (_codexState != null) _codexState.TileUnlocked -= OnResourcesUnlocked;
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
