using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Track
{
    /// <summary>
    /// 스토리 051(production/epics/iron-ring-train/story-051-phase-loop-events.md). 한 바퀴 = 건설(선로 첫 역 정차) → 순환(출발~복귀) → 다시 건설.
    /// 순환 중 휴식지에 닿으면 hold "rest-event"로 열차를 세우고 사건 하나(RestEvents.json, _weight 가중, 같은 바퀴 중복 없음, _minLap)를 연다.
    /// Choose가 효과를 적용하고, Continue(또는 _autoContinueSeconds)가 hold를 푼다. delay 효과는 그 초만큼 hold "rest-event-delay"를 더 건다.
    /// 잠긴 역은 RingTrack이 Arrived 없이 통과시키므로 사건이 없다. 열린 다른 마을은 Arrived(Hub)라 사건 없이 기존 정차 규칙.
    /// 의존은 지연 조회(Func)로 받아 설치 순서와 무관하다. Unity 시간은 Tick(dt)으로만 들어온다.
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class PhaseLoop : IPhaseLoop, IRestEvents, IDisposable, ISaveParticipant
    {
        /// <summary>사건 창 동안의 열차 hold 이유.</summary>
        public const string HOLD = "rest-event";
        /// <summary>delay 효과의 hold 이유.</summary>
        public const string DELAY_HOLD = "rest-event-delay";

        private readonly RestEventData _data;
        private readonly ITrackRuntime _track;
        private readonly ITileStock _stock;
        private readonly Func<string, string> _text;
        private readonly Func<string, string> _itemName;
        private readonly Func<IHabitatCodex> _codex;
        private readonly Func<IMoney> _money;
        private readonly Func<IHubStateSystem> _hubs;
        private readonly Func<IPassengers> _passengers;
        private readonly System.Random _random;
        private readonly HashSet<string> _usedThisLap = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _forced = new Queue<string>();
        private readonly List<string> _choices = new List<string>();
        private RestEvent _event;
        private float _sinceStart;
        private float _sinceResolved;
        private float _delayLeft;
        private bool _delayHeld;
        private bool _holding;
        private int _pendingMoney;
        private bool _disposed;

        /// <summary>지금 페이즈.</summary>
        public LoopPhase Phase { get; private set; }
        /// <summary>바퀴 번호(1부터).</summary>
        public int Lap { get; private set; }
        /// <summary>(새 페이즈, 바퀴).</summary>
        public event Action<LoopPhase, int> PhaseChanged;

        /// <summary>사건 창을 띄워야 하는지.</summary>
        public bool IsActive { get; private set; }
        /// <summary>지금(마지막) 사건 id.</summary>
        public string EventId => _event?._id;
        /// <summary>사건이 난 휴식지 id.</summary>
        public string NodeId { get; private set; }
        /// <summary>제목(한국어).</summary>
        public string Title { get; private set; } = "";
        /// <summary>상황 글(한국어).</summary>
        public string Text { get; private set; } = "";
        /// <summary>선택지 문구(한국어).</summary>
        public IReadOnlyList<string> Choices => _choices;
        /// <summary>선택을 마쳤는지.</summary>
        public bool HasResolved { get; private set; }
        /// <summary>고른 번호(-1 = 아직).</summary>
        public int ChosenIndex { get; private set; } = -1;
        /// <summary>결과 한 줄.</summary>
        public string ResultText { get; private set; } = "";
        /// <summary>얻고 잃은 것 요약.</summary>
        public string ResultDetail { get; private set; } = "";
        /// <summary>사건 창 열기.</summary>
        public event Action EventStarted;
        /// <summary>선택 직후.</summary>
        public event Action EventResolved;
        /// <summary>사건 창 닫기.</summary>
        public event Action EventClosed;

        /// <summary>읽은 데이터(데모가 _demoQueue 등을 읽는다).</summary>
        public RestEventData Data => _data;
        /// <summary>거점(선로 첫 역) id. 번영 효과와 페이즈 판정 기준.</summary>
        public string BaseHubId => _track.Nodes[0].HubId;
        /// <summary>다음 거점 도착 때 받을 마르크(우편 행낭 등).</summary>
        public int PendingMoney => _pendingMoney;

        /// <summary>text: ko.json 키 → 한국어, itemName: 아이템 id → 이름. 나머지는 지연 조회(null 반환 허용).</summary>
        public PhaseLoop(RestEventData data, ITrackRuntime track, ITileStock stock, Func<string, string> text, Func<string, string> itemName,
            Func<IHabitatCodex> codex, Func<IMoney> money, Func<IHubStateSystem> hubs, Func<IPassengers> passengers)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _stock = stock ?? throw new ArgumentNullException(nameof(stock));
            _text = text ?? (k => k ?? "");
            _itemName = itemName ?? (id => id);
            _codex = codex ?? (() => null);
            _money = money ?? (() => null);
            _hubs = hubs ?? (() => null);
            _passengers = passengers ?? (() => null);
            _random = new System.Random(data._seed);
            _track.Arrived += OnArrived;
            bool docked = _track.IsHeld && _track.CurrentNode != null && _track.CurrentNode.Index == 0;
            Phase = docked ? LoopPhase.Build : LoopPhase.Circulate;
            Lap = _track.Lap + 1;
            UnityEngine.Debug.Log($"LOOP: phase {Phase} lap={Lap}");
        }

        /// <summary>역(거점·열린 마을)에 서 있고 사건 창이 없을 때.</summary>
        public bool CanDepart => !_disposed && !IsActive && _track.IsHeld && _track.CurrentNode != null
            && _track.CurrentNode.Kind == TrackNodeKind.Hub && !_track.CurrentNode.IsLocked;

        /// <summary>출발(CanDepart가 false면 무시).</summary>
        public void Depart()
        {
            if (!CanDepart) return;
            _track.Depart();
            Tick(0f);
        }

        /// <summary>데모: 다음 휴식지 사건을 이 id로 고정한다(차례로 쓰인다).</summary>
        public void ForceNext(string eventId)
        {
            if (_data.Find(eventId) != null) _forced.Enqueue(eventId);
            else UnityEngine.Debug.LogWarning("LOOP: unknown demo event " + eventId);
        }

        /// <summary>매 프레임: 출발 감지(건설 → 순환), 자동 선택·계속, 지연 hold.</summary>
        public void Tick(float dt)
        {
            if (_disposed) return;
            dt = Math.Max(0f, dt);
            if (Phase == LoopPhase.Build && !_track.IsHeld) SetPhase(LoopPhase.Circulate, Lap);
            if (_delayHeld)
            {
                _delayLeft -= dt;
                if (_delayLeft <= 0f) { _delayHeld = false; _track.ReleaseHold(DELAY_HOLD); }
            }
            if (!IsActive) return;
            if (!HasResolved)
            {
                _sinceStart += dt;
                if (_data._autoChooseSeconds > 0f && _sinceStart >= _data._autoChooseSeconds) Choose(FirstAvailable());
            }
            else
            {
                _sinceResolved += dt;
                if (_data._autoContinueSeconds > 0f && _sinceResolved >= _data._autoContinueSeconds) Continue();
            }
        }

        /// <summary>고를 수 있는 첫 선택지 번호(없으면 0).</summary>
        public int FirstAvailable()
        {
            for (int i = 0; i < _choices.Count; i++) if (IsChoiceAvailable(i)) return i;
            return 0;
        }

        /// <summary>돈이 드는 선택지는 잔고가 비용 이상일 때만.</summary>
        public bool IsChoiceAvailable(int index)
        {
            if (!IsActive || _event == null || index < 0 || index >= _event._choices.Length) return false;
            int cost = CostOf(_event._choices[index]);
            IMoney money = _money();
            return cost <= 0 || money == null || money.Balance >= cost;
        }

        /// <summary>고를 수 없는 이유("마르크 부족"), 고를 수 있으면 "".</summary>
        public string UnavailableReason(int index) =>
            IsActive && index >= 0 && index < _choices.Count && !IsChoiceAvailable(index) ? _text("rest_choice_no_money") : "";

        /// <summary>선택지를 고르고 효과를 적용한다.</summary>
        public bool Choose(int index)
        {
            if (!IsActive || HasResolved || !IsChoiceAvailable(index)) return false;
            RestChoice choice = _event._choices[index];
            var log = new List<string>();
            var detail = new List<string>();
            bool failed = RollFailed(choice);
            float delay = 0f;
            if (choice._effects != null)
                foreach (RestEffect e in choice._effects)
                {
                    if (failed && e._chance > 0f && e._chance < 1f) continue;
                    delay += Apply(e, log, detail);
                }
            if (log.Count == 0) log.Add(RestEventData.NOTHING);
            _delayLeft = delay;
            ChosenIndex = index;
            HasResolved = true;
            _sinceResolved = 0f;
            ResultText = _text(failed && !string.IsNullOrEmpty(choice._resultFail) ? choice._resultFail : choice._result);
            ResultDetail = string.Join(_text("rest_detail_sep"), detail);
            UnityEngine.Debug.Log($"LOOP: event {_event._id} choice {index} -> {string.Join(", ", log)}{(failed ? " (chance failed)" : "")}");
            EventResolved?.Invoke();
            return true;
        }

        /// <summary>결과를 닫고 hold를 푼다(지연 효과가 있으면 그만큼 더 선다).</summary>
        public void Continue()
        {
            if (!IsActive || !HasResolved) return;
            IsActive = false;
            if (_delayLeft > 0f && !_delayHeld) { _delayHeld = true; _track.RequestHold(DELAY_HOLD); }
            if (_holding) { _holding = false; _track.ReleaseHold(HOLD); }
            EventClosed?.Invoke();
        }

        /// <summary>구독과 hold를 푼다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _track.Arrived -= OnArrived;
            if (_holding) _track.ReleaseHold(HOLD);
            if (_delayHeld) _track.ReleaseHold(DELAY_HOLD);
            _holding = false;
            _delayHeld = false;
            IsActive = false;
        }

        private void OnArrived(ITrackNode node)
        {
            if (_disposed || node == null) return;
            if (node.Index == 0)
            {
                PayPending();
                _usedThisLap.Clear();
                SetPhase(LoopPhase.Build, _track.Lap + 1);
                return;
            }
            if (Phase == LoopPhase.Build) SetPhase(LoopPhase.Circulate, Lap);
            if (node.Kind == TrackNodeKind.Waypoint) StartEvent(node);
        }

        private void SetPhase(LoopPhase phase, int lap)
        {
            if (Phase == phase && Lap == lap) return;
            Phase = phase;
            Lap = lap;
            UnityEngine.Debug.Log($"LOOP: phase {phase} lap={lap}");
            PhaseChanged?.Invoke(phase, lap);
        }

        private void StartEvent(ITrackNode node)
        {
            if (IsActive) return;
            RestEvent e = Pick();
            if (e == null) return;
            _event = e;
            _usedThisLap.Add(e._id);
            NodeId = node.Id;
            Title = _text(e._title);
            Text = _text(e._text);
            _choices.Clear();
            foreach (RestChoice c in e._choices) _choices.Add(_text(c._label));
            HasResolved = false;
            ChosenIndex = -1;
            ResultText = "";
            ResultDetail = "";
            _sinceStart = 0f;
            _sinceResolved = 0f;
            _delayLeft = 0f;
            IsActive = true;
            if (!_holding) { _holding = true; _track.RequestHold(HOLD); }
            UnityEngine.Debug.Log($"LOOP: event {e._id} at {node.Id} lap={Lap} choices={_choices.Count}");
            EventStarted?.Invoke();
        }

        /// <summary>강제 큐 → (_minLap ≤ 바퀴, 이번 바퀴 미사용) 가중 → 다 썼으면 _minLap만 보고 가중.</summary>
        private RestEvent Pick()
        {
            while (_forced.Count > 0)
            {
                RestEvent forced = _data.Find(_forced.Dequeue());
                if (forced != null) return forced;
            }
            var pool = new List<RestEvent>();
            foreach (RestEvent e in _data._events) if (e._minLap <= Lap && !_usedThisLap.Contains(e._id)) pool.Add(e);
            if (pool.Count == 0) foreach (RestEvent e in _data._events) if (e._minLap <= Lap) pool.Add(e);
            if (pool.Count == 0) return null;
            int total = 0;
            foreach (RestEvent e in pool) total += Math.Max(0, e._weight);
            if (total <= 0) return pool[_random.Next(pool.Count)];
            int roll = _random.Next(total);
            foreach (RestEvent e in pool)
            {
                int w = Math.Max(0, e._weight);
                if (roll < w) return e;
                roll -= w;
            }
            return pool[pool.Count - 1];
        }

        private static int CostOf(RestChoice choice)
        {
            int cost = 0;
            if (choice._effects != null)
                foreach (RestEffect e in choice._effects)
                    if (e._type == RestEventData.MONEY && !e._onReturn && e._amount < 0) cost -= e._amount;
            return cost;
        }

        /// <summary>선택지의 첫 확률 효과로 한 번 굴린다. 확률 효과가 없으면 false.</summary>
        private bool RollFailed(RestChoice choice)
        {
            if (choice._effects == null) return false;
            foreach (RestEffect e in choice._effects)
                if (e._chance > 0f && e._chance < 1f) return _random.NextDouble() >= e._chance;
            return false;
        }

        /// <summary>효과 하나를 적용하고 지연 초를 돌려준다.</summary>
        private float Apply(RestEffect e, List<string> log, List<string> detail)
        {
            switch (e._type)
            {
                case RestEventData.TILES:
                    ApplyTiles(e, log, detail);
                    return 0f;
                case RestEventData.PASSENGER:
                    ApplyPassenger(e, log, detail);
                    return 0f;
                case RestEventData.MONEY:
                    if (e._onReturn)
                    {
                        _pendingMoney += e._amount;
                        log.Add($"money{Signed(e._amount)}@return");
                        detail.Add(string.Format(_text("rest_detail_on_return"), Signed(e._amount)));
                    }
                    else ChangeMoney(e._amount, "rest-event:" + _event._id, log, detail);
                    return 0f;
                case RestEventData.PROSPERITY:
                    IHubStateSystem hubs = _hubs();
                    if (e._amount != 0 && hubs != null && BaseHubId != null) hubs.ApplyDelta(BaseHubId, e._amount, "rest-event:" + _event._id);
                    log.Add($"prosperity{Signed(e._amount)}");
                    detail.Add(string.Format(_text("rest_detail_prosperity"), Signed(e._amount)));
                    return 0f;
                case RestEventData.DELAY:
                    float s = Math.Max(0f, e._seconds);
                    log.Add($"delay {s:0.#}s");
                    detail.Add(string.Format(_text("rest_detail_delay"), s.ToString("0.#")));
                    return s;
                default:
                    log.Add(RestEventData.NOTHING);
                    return 0f;
            }
        }

        private void ApplyTiles(RestEffect e, List<string> log, List<string> detail)
        {
            var gained = new List<KeyValuePair<string, int>>();
            if (e._items != null)
                foreach (RestTileCount item in e._items)
                {
                    if (item == null || string.IsNullOrEmpty(item._id)) continue;
                    int count = Math.Max(1, item._count);
                    string id = item._id;
                    if (!IsUnlocked(id))
                    {
                        if (e._lockedMoney > 0) { ChangeMoney(e._lockedMoney, "rest-event:" + _event._id, log, detail); continue; }
                        id = RandomUnlocked(); // 스펙: 잠긴 타일이 지정되면 열린 타일로 대체
                        if (id == null) continue;
                    }
                    Merge(gained, id, count);
                }
            for (int i = 0; i < e._randomUnlocked; i++)
            {
                string id = RandomUnlocked();
                if (id != null) Merge(gained, id, 1);
            }
            foreach (KeyValuePair<string, int> g in gained)
            {
                _stock.Add(g.Key, g.Value);
                log.Add($"tiles {g.Key}+{g.Value}");
                detail.Add(string.Format(_text("rest_detail_tile"), _itemName(g.Key), g.Value));
            }
            for (int i = 0; i < e._loseRandom; i++)
            {
                var have = new List<string>();
                foreach (KeyValuePair<string, int> kv in _stock.Snapshot()) if (kv.Value > 0) have.Add(kv.Key);
                if (have.Count == 0) { log.Add("tiles lose none"); break; }
                string lost = have[_random.Next(have.Count)];
                if (_stock.TryTake(lost, 1))
                {
                    log.Add($"tiles {lost}-1");
                    detail.Add(string.Format(_text("rest_detail_tile_lost"), _itemName(lost)));
                }
            }
        }

        private void ApplyPassenger(RestEffect e, List<string> log, List<string> detail)
        {
            IPassengers passengers = _passengers();
            int count = Math.Max(1, e._count);
            int boarded = 0;
            for (int i = 0; i < count && passengers != null; i++)
            {
                string id = string.IsNullOrEmpty(e._npc) ? passengers.TryBoardAny(NodeId)
                    : passengers.TryBoard(e._npc, NodeId, true) ? e._npc : null;
                if (id == null) break;
                boarded++;
                log.Add("passenger " + id);
            }
            if (boarded == 0) log.Add("passenger none");
            else detail.Add(string.Format(_text("rest_detail_passenger"), boarded));
        }

        private void ChangeMoney(int amount, string reason, List<string> log, List<string> detail)
        {
            if (amount == 0) return;
            IMoney money = _money();
            if (money != null)
            {
                if (amount > 0) money.TryGain(amount, reason);
                else if (!money.TrySpend(-amount, reason)) money.Charge(-amount, reason);
            }
            log.Add($"money{Signed(amount)}");
            detail.Add(string.Format(_text("rest_detail_money"), Signed(amount)));
        }

        private void PayPending()
        {
            if (_pendingMoney == 0) return;
            int amount = _pendingMoney;
            _pendingMoney = 0;
            IMoney money = _money();
            if (money != null)
            {
                if (amount > 0) money.TryGain(amount, "rest-event:return");
                else money.Charge(-amount, "rest-event:return");
            }
            UnityEngine.Debug.Log($"LOOP: pending money{Signed(amount)} paid at {BaseHubId}");
        }

        private bool IsUnlocked(string itemId)
        {
            IHabitatCodex codex = _codex();
            return codex == null || codex.IsTileUnlocked(itemId);
        }

        private string RandomUnlocked()
        {
            IReadOnlyList<string> open = _codex()?.UnlockedTileIds;
            if (open == null || open.Count == 0) return null;
            return open[_random.Next(open.Count)];
        }

        private static void Merge(List<KeyValuePair<string, int>> list, string id, int count)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Key == id) { list[i] = new KeyValuePair<string, int>(id, list[i].Value + count); return; }
            list.Add(new KeyValuePair<string, int>(id, count));
        }

        private static string Signed(int v) => v >= 0 ? "+" + v : "-" + (-v);

        // 스펙 003: 저장 슬롯용 바퀴 번호·돌아올 때 받을 마르크 꺼내기·되돌리기 추가(복원 때 건설 페이즈로 둔다)
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "phaseLoop";

        /// <summary>스펙 003. 복원 순서(맨 끝).</summary>
        public int Order => 91;

        /// <summary>바퀴 번호 Lap과 거점 도착 때 받을 마르크 _pendingMoney를 JSON으로 돌려준다. 상태를 바꾸지 않는다.</summary>
        public string Capture()
        {
            return UnityEngine.JsonUtility.ToJson(new LoopSave { Lap = Lap, PendingMoney = _pendingMoney });
        }

        /// <summary>
        /// Capture가 만든 json으로 Lap과 _pendingMoney를 두고, 이번 바퀴 사용 사건을 비운 뒤 건설 페이즈로 둔다.
        /// PhaseChanged(Build, Lap)를 낸다.
        /// </summary>
        public void Restore(string json)
        {
            LoopSave state = UnityEngine.JsonUtility.FromJson<LoopSave>(json);
            _pendingMoney = state.PendingMoney;
            _usedThisLap.Clear();
            Phase = LoopPhase.Build;
            Lap = Math.Max(1, state.Lap);
            UnityEngine.Debug.Log($"LOOP: phase {Phase} lap={Lap} (저장 슬롯 복원)");
            PhaseChanged?.Invoke(Phase, Lap);
        }

        /// <summary>스펙 003. 저장 형식: 바퀴 번호와 돌아올 때 받을 마르크.</summary>
        [Serializable]
        private sealed class LoopSave
        {
            public int Lap;
            public int PendingMoney;
        }
    }
}
