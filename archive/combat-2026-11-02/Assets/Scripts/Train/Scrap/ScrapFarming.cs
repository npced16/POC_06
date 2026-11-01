using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Train.Scrap
{
    /// <summary>수집 인부 상태.</summary>
    public enum WorkerState
    {
        /// <summary>열차 옆에서 쉰다(지시를 받을 수 있다).</summary>
        Idle,
        /// <summary>배정된 더미로 걸어간다.</summary>
        ToPile,
        /// <summary>더미를 캔다.</summary>
        Mining,
        /// <summary>캔 고철을 들고 열차로 돌아온다.</summary>
        Returning,
        /// <summary>적을 피해 열차로 도망친다(배정 해제).</summary>
        Fleeing,
    }

    /// <summary>인부 상태 전이를 일으키는 사건.</summary>
    public enum WorkerEvent
    {
        /// <summary>플레이어가 더미로 보냄.</summary>
        Dispatch,
        /// <summary>더미에 도착.</summary>
        ReachPile,
        /// <summary>다 들었거나 더미가 비었음.</summary>
        Loaded,
        /// <summary>열차에 도착(배달) — 배정 더미에 남은 게 있으면 다시 간다.</summary>
        HomeAgain,
        /// <summary>열차에 도착(배달) — 쉰다.</summary>
        HomeRest,
        /// <summary>적이 가까이 옴.</summary>
        Threat,
        /// <summary>정차가 끝남(즉시 복귀).</summary>
        Recall,
    }

    /// <summary>인부 하나(정차 화면 좌표).</summary>
    public sealed class ScrapWorker
    {
        /// <summary>순번.</summary>
        public int Index { get; internal set; }
        /// <summary>상태.</summary>
        public WorkerState State { get; internal set; }
        /// <summary>X(선로 방향).</summary>
        public float X { get; internal set; }
        /// <summary>Y(옆).</summary>
        public float Y { get; internal set; }
        /// <summary>집(열차 승강장 문) X.</summary>
        public float HomeX { get; internal set; }
        /// <summary>집 Y.</summary>
        public float HomeY { get; internal set; }
        /// <summary>배정된 더미 순번(-1 = 없음).</summary>
        public int Pile { get; internal set; } = -1;
        /// <summary>들고 있는 고철.</summary>
        public int Carry { get; internal set; }
        /// <summary>캐는 중인 다음 1개까지의 진행 0..1.</summary>
        public float MineProgress { get; internal set; }
    }

    /// <summary>고철 더미 하나(정차 화면 좌표).</summary>
    public sealed class ScrapPile
    {
        /// <summary>순번.</summary>
        public int Index { get; internal set; }
        /// <summary>X.</summary>
        public float X { get; internal set; }
        /// <summary>Y.</summary>
        public float Y { get; internal set; }
        /// <summary>남은 양.</summary>
        public int Amount { get; internal set; }
        /// <summary>처음 양.</summary>
        public int Max { get; internal set; }
    }

    /// <summary>
    /// 스토리 038(+03:30 사용자 지시) 정차 파밍 규칙(순수 C#, 결정적). 중간역에 서 있는 동안 플레이어가 더미를 고르면 쉬는 인부 중 가장 가까운 이가
    /// 걸어가 초당 ScrapPerSecond로 캐고(최대 CarryMax), 열차로 돌아와야 고철이 창고(IScrap)에 들어간다. 적이 FleeDistance 안에 오면 도망친다(피해 없음).
    /// 적을 죽여도 고철은 없다. 정차를 끝내면(출발) 버틴 초 × HoldBonusPerSecond 보너스를 받고, 인부가 들고 있던 고철은 데이터(_fleeKeepCarried)대로 버리거나 넣는다.
    /// 패배(내구도 0)면 보너스 없음·운반분 버림. 상태 전이는 아래 표(Transitions)에 있는 것만 허용된다.
    /// </summary>
    public sealed class ScrapFarming
    {
        private static readonly Dictionary<(WorkerState, WorkerEvent), WorkerState> Transitions = new Dictionary<(WorkerState, WorkerEvent), WorkerState>
        {
            { (WorkerState.Idle, WorkerEvent.Dispatch), WorkerState.ToPile },
            { (WorkerState.ToPile, WorkerEvent.ReachPile), WorkerState.Mining },
            { (WorkerState.ToPile, WorkerEvent.Loaded), WorkerState.Returning },
            { (WorkerState.ToPile, WorkerEvent.Threat), WorkerState.Fleeing },
            { (WorkerState.Mining, WorkerEvent.Loaded), WorkerState.Returning },
            { (WorkerState.Mining, WorkerEvent.Threat), WorkerState.Fleeing },
            { (WorkerState.Returning, WorkerEvent.HomeAgain), WorkerState.ToPile },
            { (WorkerState.Returning, WorkerEvent.HomeRest), WorkerState.Idle },
            { (WorkerState.Returning, WorkerEvent.Threat), WorkerState.Fleeing },
            { (WorkerState.Fleeing, WorkerEvent.HomeRest), WorkerState.Idle },
            { (WorkerState.Idle, WorkerEvent.Recall), WorkerState.Idle },
            { (WorkerState.ToPile, WorkerEvent.Recall), WorkerState.Idle },
            { (WorkerState.Mining, WorkerEvent.Recall), WorkerState.Idle },
            { (WorkerState.Returning, WorkerEvent.Recall), WorkerState.Idle },
            { (WorkerState.Fleeing, WorkerEvent.Recall), WorkerState.Idle },
        };

        private readonly ScrapData _data;
        private readonly IScrap _store;
        private readonly List<ScrapWorker> _workers = new List<ScrapWorker>();
        private readonly List<ScrapPile> _piles = new List<ScrapPile>();
        private bool _pilesMade;

        /// <summary>데이터와 고철 저장소를 주입한다.</summary>
        public ScrapFarming(ScrapData data, IScrap store)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _store = store;
            for (int i = 0; i < data.Workers; i++) _workers.Add(new ScrapWorker { Index = i });
        }

        /// <summary>데이터.</summary>
        public ScrapData Data => _data;
        /// <summary>정차 중(파밍 가능)이면 true.</summary>
        public bool IsActive { get; private set; }
        /// <summary>인부 목록.</summary>
        public IReadOnlyList<ScrapWorker> Workers => _workers;
        /// <summary>더미 목록.</summary>
        public IReadOnlyList<ScrapPile> Piles => _piles;
        /// <summary>쉬지 않는 인부 수.</summary>
        public int Busy { get { int n = 0; foreach (ScrapWorker w in _workers) if (w.State != WorkerState.Idle) n++; return n; } }
        /// <summary>인부들이 지금 들고 있는(아직 창고에 안 들어간) 고철 합.</summary>
        public int Carried { get { int n = 0; foreach (ScrapWorker w in _workers) n += w.Carry; return n; } }
        /// <summary>이번 정차에서 배달로 번 고철.</summary>
        public int DeliveredThisStop { get; private set; }

        /// <summary>정차 시작(더미·인부 배치). 인부 첫 정차 사건 순번, 더미 순번.</summary>
        public event Action StopBegan;
        /// <summary>정차 끝(출발 true / 패배 false, 받은 버티기 보너스).</summary>
        public event Action<bool, int> StopEnded;
        /// <summary>인부가 더미로 감(인부, 더미).</summary>
        public event Action<int, int> Dispatched;
        /// <summary>인부가 열차에 고철을 넣음(인부, 양).</summary>
        public event Action<int, int> Delivered;
        /// <summary>인부가 도망치기 시작함(인부).</summary>
        public event Action<int> Fled;

        /// <summary>버틴 초에 대한 보너스(내림).</summary>
        public int HoldBonus(float seconds) => (int)Math.Floor(Math.Max(0f, seconds) * _data.HoldBonusPerSecond);

        /// <summary>정차를 시작한다. 좌표 원점 = 열차 머리(정차 화면 좌표 frontX, frontY). 더미는 데이터대로(첫 정차 또는 _respawnPiles) 채운다.</summary>
        public void BeginStop(float frontX, float frontY)
        {
            ScrapPileData[] piles = _data.Piles;
            if (!_pilesMade || _data.RespawnPiles || _piles.Count != piles.Length)
            {
                _piles.Clear();
                for (int i = 0; i < piles.Length; i++)
                    _piles.Add(new ScrapPile { Index = i, Amount = Math.Max(0, piles[i].amount), Max = Math.Max(1, piles[i].amount) });
                _pilesMade = true;
            }
            for (int i = 0; i < _piles.Count; i++) { _piles[i].X = frontX + piles[i].x; _piles[i].Y = frontY + piles[i].y; }
            float start = -(_workers.Count - 1) * 0.5f * _data.WorkerSpacing;
            foreach (ScrapWorker w in _workers)
            {
                w.HomeX = frontX + _data.HomeX + start + w.Index * _data.WorkerSpacing;
                w.HomeY = frontY + _data.HomeY;
                w.X = w.HomeX; w.Y = w.HomeY;
                w.State = WorkerState.Idle; w.Pile = -1; w.Carry = 0; w.MineProgress = 0f;
            }
            DeliveredThisStop = 0;
            IsActive = true;
            UnityEngine.Debug.Log($"SCRAP: stop begin piles={_piles.Count} workers={_workers.Count} store={_store?.Amount ?? 0}");
            StopBegan?.Invoke();
        }

        /// <summary>더미 pile로 가장 가까운 쉬는 인부를 보낸다. 정차 중이 아니거나, 더미가 비었거나, 쉬는 인부가 없으면 false.</summary>
        public bool Dispatch(int pile)
        {
            if (!IsActive || pile < 0 || pile >= _piles.Count || _piles[pile].Amount <= 0) return false;
            ScrapWorker best = null; float bestD = float.MaxValue;
            foreach (ScrapWorker w in _workers)
            {
                if (w.State != WorkerState.Idle) continue;
                float d = Sq(w.X - _piles[pile].X, w.Y - _piles[pile].Y);
                if (d < bestD) { bestD = d; best = w; }
            }
            if (best == null) return false;
            best.Pile = pile;
            Fire(best, WorkerEvent.Dispatch);
            UnityEngine.Debug.Log($"SCRAP: dispatch worker={best.Index} pile={pile} left={_piles[pile].Amount}");
            Dispatched?.Invoke(best.Index, pile);
            return true;
        }

        /// <summary>dt초 진행: 위협 판정(monsters, 같은 정차 좌표) → 이동·캐기·배달.</summary>
        public void Tick(float dt, IReadOnlyList<RaidUnit> monsters)
        {
            if (!IsActive || dt <= 0f) return;
            float flee2 = _data.FleeDistance * _data.FleeDistance;
            foreach (ScrapWorker w in _workers)
            {
                if (w.State == WorkerState.ToPile || w.State == WorkerState.Mining || w.State == WorkerState.Returning)
                {
                    if (monsters != null && flee2 > 0f)
                        foreach (RaidUnit m in monsters)
                            if (Sq(m.X - w.X, m.Y - w.Y) <= flee2)
                            {
                                Fire(w, WorkerEvent.Threat);
                                w.Pile = -1;
                                UnityEngine.Debug.Log($"SCRAP: worker {w.Index} flees carry={w.Carry}");
                                Fled?.Invoke(w.Index);
                                break;
                            }
                }
                switch (w.State)
                {
                    case WorkerState.ToPile:
                        {
                            ScrapPile pile = _piles[w.Pile];
                            if (pile.Amount <= 0) { Fire(w, WorkerEvent.Loaded); break; }
                            if (Walk(w, pile.X, pile.Y, _data.WorkerSpeed * dt)) Fire(w, WorkerEvent.ReachPile);
                            break;
                        }
                    case WorkerState.Mining:
                        {
                            ScrapPile pile = _piles[w.Pile];
                            w.MineProgress += _data.ScrapPerSecond * dt;
                            while (w.MineProgress >= 1f && w.Carry < _data.CarryMax && pile.Amount > 0)
                            {
                                w.MineProgress -= 1f; w.Carry++; pile.Amount--;
                            }
                            if (w.Carry >= _data.CarryMax || pile.Amount <= 0) { w.MineProgress = 0f; Fire(w, WorkerEvent.Loaded); }
                            break;
                        }
                    case WorkerState.Returning:
                    case WorkerState.Fleeing:
                        {
                            float speed = _data.WorkerSpeed * (w.State == WorkerState.Fleeing ? _data.FleeSpeedFactor : 1f);
                            if (!Walk(w, w.HomeX, w.HomeY, speed * dt)) break;
                            Deliver(w);
                            bool again = w.State == WorkerState.Returning && w.Pile >= 0 && _piles[w.Pile].Amount > 0;
                            if (!again) w.Pile = -1;
                            Fire(w, again ? WorkerEvent.HomeAgain : WorkerEvent.HomeRest);
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// 정차를 끝낸다. departed(출발)이면 버틴 초 × HoldBonusPerSecond 보너스를 넣고, 들고 있던 고철은 _fleeKeepCarried면 넣고 아니면 버린다.
        /// 패배면 보너스 없음·운반분 버림. 인부는 모두 열차로 돌아온다. 받은 보너스를 돌려준다.
        /// </summary>
        public int EndStop(bool departed, float heldSeconds)
        {
            if (!IsActive) return 0;
            int carried = Carried;
            if (departed && _data.FleeKeepCarried && carried > 0) _store?.TryGain(carried);
            int bonus = departed ? HoldBonus(heldSeconds) : 0;
            if (bonus > 0) _store?.TryGain(bonus);
            foreach (ScrapWorker w in _workers)
            {
                Fire(w, WorkerEvent.Recall);
                w.Carry = 0; w.Pile = -1; w.MineProgress = 0f; w.X = w.HomeX; w.Y = w.HomeY;
            }
            IsActive = false;
            UnityEngine.Debug.Log($"SCRAP: stop end departed={departed} held={heldSeconds:F1}s bonus={bonus} carried={carried} kept={(departed && _data.FleeKeepCarried)} delivered={DeliveredThisStop} store={_store?.Amount ?? 0}");
            StopEnded?.Invoke(departed, bonus);
            return bonus;
        }

        private void Deliver(ScrapWorker w)
        {
            if (w.Carry <= 0) return;
            int amount = w.Carry;
            w.Carry = 0;
            _store?.TryGain(amount);
            DeliveredThisStop += amount;
            Delivered?.Invoke(w.Index, amount);
        }

        private static void Fire(ScrapWorker w, WorkerEvent e)
        {
            if (!Transitions.TryGetValue((w.State, e), out WorkerState next))
                throw new InvalidOperationException($"Scrap worker {w.Index}: no transition {w.State} --{e}-->");
            w.State = next;
        }

        /// <summary>목표로 step만큼 걷는다. 도착하면 true.</summary>
        private bool Walk(ScrapWorker w, float tx, float ty, float step)
        {
            float dx = tx - w.X, dy = ty - w.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d <= Math.Max(step, _data.ArriveDistance)) { w.X = tx; w.Y = ty; return true; }
            w.X += dx / d * step; w.Y += dy / d * step;
            return false;
        }

        private static float Sq(float x, float y) => x * x + y * y;
    }
}
