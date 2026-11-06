using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;
using IronRing.Hud.Codex;
using IronRing.Hud.HubPhase;
using IronRing.Hud.Hotbar;
using IronRing.Hud.Npc;
using IronRing.Presentation.Interior;
using IronRing.Presentation.Map;
using IronRing.Track;

namespace IronRing.Onboarding
{
    /// <summary>
    /// 스펙 001 FR-002~FR-011. 게임 이벤트로 학습 항목의 발생과 완료를 판정하고, 표시 큐를 order 순으로 하나씩 내보낸다.
    /// 화면(OnboardingScreen)은 Shown·Closed만 받아 그리고, 매 프레임 Tick을 불러 이벤트가 없는 상태 변화를 살핀다.
    /// </summary>
    public sealed class OnboardingDirector
    {
        private const int HOTBAR_SLOT_COUNT = 9;

        private readonly Dictionary<string, OnboardingTopic> _topics = new();
        private readonly List<string> _queue = new();
        private readonly Dictionary<string, float> _prosperity = new();
        private readonly HashSet<string> _doneEarly = new();
        private string _current;
        private string _devTopic;
        private bool _devPending;
        private bool _subscribed;
        private bool _ghostSubscribed;
        private bool _loopSubscribed;
        private bool _anyBuilt;
        private bool _codexOpen;
        private bool _passengersOpen;
        // 스펙 001 FR-035: 잿골이 열린 뒤 개통 배너와 지도 연출이 끝나기를 기다리는 중
        private bool _lineOpenPending;
        private int _filledSlots;
        private IViewMode _view;
        private IPlacementCost _cost;

        /// <summary>항목이 화면에 떠야 할 때(항목).</summary>
        public event Action<OnboardingTopic> Shown;
        /// <summary>행동형 항목의 행동을 마쳤을 때(항목 id). 곧바로 Closed가 이어진다.</summary>
        public event Action<string> Completed;
        /// <summary>항목이 닫혔을 때(항목 id).</summary>
        public event Action<string> Closed;

        public int TopicCount => _topics.Count;
        public string Current => _current;
        /// <summary>기본 학습 묶음의 모든 항목을 완료했는지 기록을 확인해 반환한다.</summary>
        public bool BasicTutorialCompleted
        {
            get
            {
                foreach (OnboardingTopic topic in _topics.Values)
                    if (topic.Chain && !OnboardingProgress.IsSeen(topic.Id)) return false;
                return true;
            }
        }
        /// <summary>마지막으로 해금된 재료 id(퀵슬롯 강조용). 없으면 null.</summary>
        public string LastUnlockedTile { get; private set; }
        public IEnumerable<OnboardingTopic> Topics => _topics.Values;

        /// <summary>data의 항목을 id로 등록한다. 같은 id가 있으면 뒤의 것을 버린다.</summary>
        public OnboardingDirector(OnboardingTopicsData data)
        {
            foreach (OnboardingTopic topic in data.Topics)
            {
                if (!_topics.ContainsKey(topic.Id)) _topics.Add(topic.Id, topic);
            }
        }

        /// <summary>id 항목 정의를 돌려준다. 없으면 null.</summary>
        public OnboardingTopic Find(string id) => id != null && _topics.TryGetValue(id, out OnboardingTopic topic) ? topic : null;

        /// <summary>
        /// GameServices에 등록된 서비스의 이벤트를 구독한다(FR-002).
        /// 아직 없는 MonoBehaviour 쪽(PlacementGhost, PhaseLoop)은 Tick에서 늦게 구독한다.
        /// </summary>
        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _view = GameServices.Get<IViewMode>();
            _cost = GameServices.Get<IPlacementCost>();
            if (_view != null) _view.InteriorChanged += OnInteriorChanged;

            IItemPlacement placement = GameServices.Get<IItemPlacement>();
            if (placement != null)
            {
                placement.ItemPlaced += OnItemPlaced;
                placement.ItemRemoved += OnItemRemoved;
            }

            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            if (clusters != null) clusters.HabitatCompleted += OnHabitatCompleted;

            IHabitatCodex codex = GameServices.Get<IHabitatCodex>();
            if (codex != null) codex.TileUnlocked += OnTileUnlocked;
            if (codex is HabitatCodex concrete) concrete.HintUnlocked += OnHintUnlocked;

            IPassengers passengers = GameServices.Get<IPassengers>();
            if (passengers != null) passengers.Boarded += OnBoarded;

            ILineOpener opener = GameServices.Get<ILineOpener>();
            if (opener != null) opener.HubOpened += OnHubOpened;

            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            if (hubs != null)
            {
                hubs.ProsperityChanged += OnProsperityChanged;
                // 첫 하락도 감지하도록 시작 번영도를 기억해 둔다.
                foreach (string hubId in hubs.HubIds) _prosperity[hubId] = hubs.GetProsperity(hubId);
            }

            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            if (track != null) track.LapCompleted += OnLapCompleted;

            SubscribeLate();
            Debug.Log($"ONBOARDING: topics={_topics.Count} subscribed=true");
        }

        /// <summary>
        /// 매 프레임 불린다. 늦게 생기는 구독 대상을 붙이고, 이벤트가 없는 상태(도감·승객 열림, 대화, 휠, 퀵슬롯 칸 수)를 살핀다.
        /// 상태가 바뀌면 해당 항목의 발생·완료를 처리한다.
        /// </summary>
        public void Tick()
        {
            if (!_subscribed) return;
            SubscribeLate();
            // 스펙 001 FR-033: 도감·승객 창이 열리는 순간 그 설명형 안내를 닫고, 창이 닫히면 다음 안내를 띄운다.
            bool codexOpen = CodexScreen.IsOpen;
            bool passengersOpen = PassengersScreen.IsOpen;
            bool windowClosed = (!codexOpen && _codexOpen) || (!passengersOpen && _passengersOpen);
            if (codexOpen && !_codexOpen && _current == "codex") Confirm();
            _codexOpen = codexOpen;
            if (passengersOpen && !_passengersOpen && _current == "passengers") Confirm();
            _passengersOpen = passengersOpen;
            if (windowClosed) ShowNext();
            // 스펙 001 FR-035: 개통 배너와 지도 잿골 등장 연출이 모두 끝나면 잿골 안내를 띄운다.
            if (_lineOpenPending && !PhaseBannerScreen.IsBusy && !AnyHubRevealing()) { _lineOpenPending = false; Trigger("line_open"); }

            bool interiorOpen = _view != null && _view.InteriorOpen;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (interiorOpen && !overUi && Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0f) Complete("camera_zoom");

            int filled = HotbarScreen.Instance != null ? HotbarScreen.Instance.FilledSlotCount : 0;
            if (filled >= HOTBAR_SLOT_COUNT && _filledSlots < HOTBAR_SLOT_COUNT) Trigger("clear_slot");
            if (filled < _filledSlots) Complete("clear_slot");
            _filledSlots = filled;
        }

        /// <summary>
        /// id 항목의 발생 조건이 맞았을 때 부른다(FR-003~FR-005).
        /// 이미 봤거나 떠 있거나 큐에 있으면 무시하고, 아니면 큐에 넣은 뒤 빈 자리가 있으면 띄운다.
        /// </summary>
        public void Trigger(string id)
        {
            if (Find(id) == null) return;
            // 저장 불러오기 중 보드 복원이 일으키는 완성·해금은 학습 상황이 아니므로 무시한다.
            if (IronRing.Save.SaveSlots.IsRestoring) return;
            // 이미 본 항목이면 띄우지 않되, 그 뒤에 이어지는 항목은 발생시킨다.
            if (OnboardingProgress.IsSeen(id)) { OnClosed(id); return; }
            if (_current == id || _queue.Contains(id)) return;
            _queue.Add(id);
            ShowNext();
        }

        /// <summary>
        /// 기록과 큐를 무시하고 id 항목을 바로 띄운다(FR-016, 개발자 메뉴용).
        /// 떠 있던 다른 항목은 닫지 않고 큐 맨 앞으로 되돌린다.
        /// </summary>
        public void ForceShow(string id)
        {
            OnboardingTopic topic = Find(id);
            if (topic == null) return;
            _queue.Remove(id);
            if (_current != null && _current != id) _queue.Insert(0, _current);
            _current = id;
            if (id == _devTopic) _devPending = false;
            Debug.Log("ONBOARDING: shown " + id + " (forced)");
            Shown?.Invoke(topic);
        }

        /// <summary>
        /// 개발자 메뉴가 id 항목의 상황을 만들기 직전에 부른다(FR-015). id의 본 기록을 지우고 큐에서 빼며,
        /// 떠 있던 팝업은 본 기록 없이 숨기고, 기본 학습 묶음 순서 제한에서 id만 예외로 둔다(_devTopic).
        /// </summary>
        public void Rehearse(string id)
        {
            if (Find(id) == null) return;
            OnboardingProgress.Reset(id);
            _queue.Remove(id);
            _doneEarly.Remove(id);
            if (_current != null)
            {
                string shown = _current;
                _current = null;
                Closed?.Invoke(shown);
                // 숨긴 팝업은 연습이 끝난 뒤 다시 뜨도록 큐 맨 앞에 되돌린다.
                if (shown != id) _queue.Insert(0, shown);
            }
            _devTopic = id;
            _devPending = true;
        }

        /// <summary>떠 있는 안내형 항목의 확인 버튼을 눌렀을 때 그 항목을 닫는다(FR-008).</summary>
        public void Confirm()
        {
            OnboardingTopic topic = Find(_current);
            if (topic != null && !topic.IsAction) Close(topic.Id);
        }

        /// <summary>떠 있는 행동형 항목의 건너뛰기 버튼을 눌렀을 때, 완료로 내보내지 않고 본 항목으로 기록해 닫는다(FR-027).</summary>
        public void Skip()
        {
            OnboardingTopic topic = Find(_current);
            if (topic == null || !topic.IsAction) return;
            Debug.Log("ONBOARDING: skipped " + topic.Id);
            Close(topic.Id);
        }

        /// <summary>
        /// 마을 내부가 열려 있으면 아직 보지 않은 기본 학습 묶음 항목 중 order가 가장 작은 것을 발생시킨다(FR-028).
        /// 건너뛰기나 이어하기로 다음 항목의 발생 이벤트가 사라져도 기본 학습이 끝까지 이어지게 한다.
        /// </summary>
        private void AdvanceChain()
        {
            // 개발자 메뉴 연습 중에는 기본 학습을 끼워 넣지 않는다.
            if (_devPending || _view == null || !_view.InteriorOpen) return;
            OnboardingTopic next = null;
            foreach (OnboardingTopic topic in _topics.Values)
            {
                if (!topic.Chain || OnboardingProgress.IsSeen(topic.Id)) continue;
                if (next == null || topic.Order < next.Order) next = topic;
            }
            if (next != null) Trigger(next.Id);
        }

        /// <summary>
        /// id 행동형 항목이 떠 있으면 완료로 내보내고 닫는다(FR-009~FR-011).
        /// 떠 있지 않거나 안내형이면 아무것도 하지 않는다.
        /// </summary>
        public void Complete(string id)
        {
            OnboardingTopic topic = Find(id);
            if (topic == null || !topic.IsAction) return;
            // 큐에서 기다리는 중에 행동을 먼저 했으면 기억해 두었다가, 뜨자마자 완료한다.
            // 기본 학습 묶음 항목은 뜬 뒤에 다시 해야 닫힌다(FR-024).
            if (_current != id) { if (_queue.Contains(id) && !topic.Chain) _doneEarly.Add(id); return; }
            Debug.Log("ONBOARDING: completed " + id);
            Completed?.Invoke(id);
            Close(id);
        }

        /// <summary>id 항목을 본 항목으로 저장하고 Closed를 내보낸 뒤 큐의 다음 항목을 띄운다(FR-011).</summary>
        private void Close(string id)
        {
            OnboardingProgress.MarkSeen(id);
            _current = null;
            // 연습한 항목이 닫히면 순서 제한 예외를 푼다.
            if (id == _devTopic) _devTopic = null;
            Closed?.Invoke(id);
            OnClosed(id);
            if (Find(id).Chain) AdvanceChain();
            ShowNext();
        }

        /// <summary>
        /// 떠 있는 항목이 없으면 큐에서 띄울 수 있는 항목 중 order가 가장 작은 것을 Shown으로 내보낸다(FR-005).
        /// 기본 학습 묶음 항목은 앞 순서 묶음 항목을 모두 봐야 띄울 수 있다(FR-024). 띄울 항목이 없으면 그대로 둔다.
        /// </summary>
        private void ShowNext()
        {
            if (_current != null || _queue.Count == 0) return;
            // 도감·승객 창이 떠 있으면 건너뛰기로 닫아도 다음 안내를 창 위에 띄우지 않고 창이 닫힐 때까지 미룬다(FR-033).
            if (CodexScreen.IsOpen || PassengersScreen.IsOpen) return;
            int best = -1;
            for (int i = 0; i < _queue.Count; i++)
            {
                if (!Ready(_topics[_queue[i]])) continue;
                if (best < 0 || _topics[_queue[i]].Order < _topics[_queue[best]].Order) best = i;
            }
            // 개발자 메뉴로 고른 항목이 뜰 때까지 다른 항목은 띄우지 않고, 큐에 들어오면 order와 관계없이 먼저 띄운다(FR-016).
            if (_devPending) best = _queue.IndexOf(_devTopic);
            if (best < 0) return;
            _current = _queue[best];
            _queue.RemoveAt(best);
            if (_current == _devTopic) _devPending = false;
            Debug.Log("ONBOARDING: shown " + _current);
            Shown?.Invoke(_topics[_current]);
            if (_doneEarly.Remove(_current)) Complete(_current);
        }

        /// <summary>topic이 기본 학습 묶음이 아니거나, order가 더 작은 묶음 항목을 모두 봤으면 true를 돌려준다(FR-024).</summary>
        private bool Ready(OnboardingTopic topic)
        {
            if (topic.Id == _devTopic) return true;
            // 마을 화면에서만 할 수 있는 항목은 마을 내부가 열릴 때까지 기다린다(FR-030).
            if (topic.Town && (_view == null || !_view.InteriorOpen)) return false;
            if (!topic.Chain) return true;
            foreach (OnboardingTopic other in _topics.Values)
            {
                if (other.Chain && other.Order < topic.Order && !OnboardingProgress.IsSeen(other.Id)) return false;
            }
            return true;
        }

        /// <summary>PlacementGhost·PhaseLoop이 생겼으면 한 번씩 구독한다.</summary>
        private void SubscribeLate()
        {
            if (!_ghostSubscribed && PlacementGhost.Instance != null)
            {
                _ghostSubscribed = true;
                PlacementGhost.Instance.SelectionChanged += OnSelectionChanged;
                PlacementGhost.Instance.Undone += OnUndone;
            }
            if (!_loopSubscribed && LoopInstaller.Loop != null)
            {
                _loopSubscribed = true;
                LoopInstaller.Loop.PhaseChanged += OnPhaseChanged;
            }
        }

        /// <summary>id 항목이 닫힌 뒤 이어지는 항목(되돌리기 다음 휠 확대, 합류 다음 주민 상호작용)을 발생시킨다.</summary>
        private void OnClosed(string id)
        {
            if (id == "undo" && _view != null && _view.InteriorOpen) Trigger("camera_zoom");
        }

        /// <summary>내부 화면이 열리면 재료 고르기·마을 들어가기를, 닫히면 출발 안내를 처리한다.</summary>
        private void OnInteriorChanged(bool open)
        {
            if (open)
            {
                Complete("return_town");
                Trigger("select_material");
                AdvanceChain();
                // 마을 밖에서 발생해 기다리던 항목을 띄운다(FR-030).
                ShowNext();
                return;
            }
            if (_anyBuilt && LoopInstaller.Loop != null && LoopInstaller.Loop.Phase == LoopPhase.Build) Trigger("depart");
        }

        /// <summary>재료를 고르면 재료 고르기를 끝내고, 놓기·구매 안내를 발생시킨다.</summary>
        private void OnSelectionChanged(PlacementEntry entry)
        {
            if (!entry.IsValid) return;
            Complete("select_material");
            Trigger("place_material");
            if (PriceOf(entry.Id) > 0) Trigger("buy_material");
        }

        /// <summary>값이 있는 재료를 놓으면 구매 안내를 끝내고 회수 안내를 발생시킨다.</summary>
        private void OnItemPlaced(string hubId, int cellIndex, string itemId)
        {
            if (PriceOf(itemId) <= 0) return;
            Complete("buy_material");
            Trigger("pick_up");
        }

        /// <summary>재료를 회수하면 회수 안내를 끝내고 되돌리기 안내를 발생시킨다.</summary>
        private void OnItemRemoved(string hubId, int cellIndex, string itemId)
        {
            Complete("pick_up");
            if (OnboardingProgress.IsSeen("pick_up")) Trigger("undo");
        }

        /// <summary>Ctrl+Z 되돌리기를 하면 되돌리기 안내를 끝낸다.</summary>
        private void OnUndone(PlacementAction action) => Complete("undo");

        /// <summary>건물이 완성되면 놓기 안내를 끝내고 조합 안내를 발생시킨다.</summary>
        private void OnHabitatCompleted(string hubId, string typeId)
        {
            _anyBuilt = true;
            // 재료 해금이 먼저 큐에 들어와 있어도 조합 안내가 앞서도록, 놓기 안내를 닫기 전에 조합 안내를 큐에 넣는다.
            Trigger("build_combo");
            Complete("place_material");
        }

        /// <summary>새 재료가 해금되면 첫 재료 id를 LastUnlockedTile에 기억하고 해금 안내를 발생시킨다.</summary>
        private void OnTileUnlocked(IReadOnlyList<string> tileIds, string byRecipeId)
        {
            if (tileIds != null && tileIds.Count > 0) LastUnlockedTile = tileIds[0];
            Trigger("unlock_material");
        }

        /// <summary>설계 단서를 얻으면 도감 안내를 발생시킨다.</summary>
        private void OnHintUnlocked(string typeId) => Trigger("codex");

        /// <summary>승객이 타면 승객 목록 안내를 발생시킨다.</summary>
        private void OnBoarded(string npcId, string waypointId) => Trigger("passengers");


        /// <summary>
        /// 스펙 001 FR-035: 잠긴 마을이 열리면 선로 개통 안내를 바로 띄우지 않고 _lineOpenPending만 켠다.
        /// 불러오기 복원 중의 개통은 학습 상황이 아니므로 무시한다.
        /// </summary>
        private void OnHubOpened(string hubId) { if (!IronRing.Save.SaveSlots.IsRestoring) _lineOpenPending = true; }

        /// <summary>지도에서 새로 열린 마을이 나타나는 연출이 진행 중인 표지가 하나라도 있으면 true를 돌려준다.</summary>
        private static bool AnyHubRevealing()
        {
            foreach (MapLockedHub hub in UnityEngine.Object.FindObjectsByType<MapLockedHub>(FindObjectsSortMode.None))
                if (hub.Revealing) return true;
            return false;
        }

        /// <summary>마을 번영이 직전 값보다 떨어지면 위험 안내를 발생시킨다.</summary>
        private void OnProsperityChanged(string hubId, float value)
        {
            if (_prosperity.TryGetValue(hubId, out float previous) && value < previous) Trigger("run_danger");
            _prosperity[hubId] = value;
        }

        /// <summary>한 바퀴를 마치면 마을 귀환 안내를 발생시킨다.</summary>
        private void OnLapCompleted(int lap) => Trigger("return_town");

        /// <summary>순환 페이즈가 시작되면 출발 안내를 끝낸다.</summary>
        private void OnPhaseChanged(LoopPhase phase, int lap)
        {
            if (phase == LoopPhase.Circulate) Complete("depart");
        }

        /// <summary>itemId 재료의 배치 가격을 돌려준다. 비용 서비스가 없으면 0.</summary>
        private int PriceOf(string itemId) => _cost != null && itemId != null ? _cost.PriceOf(itemId) : 0;
    }
}
