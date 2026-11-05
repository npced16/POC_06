using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Hub
{
    /// <summary>
    /// Story 042: the town hub link to the economy town tier (TownTiers.json), attached by EconomyInstaller.
    /// Phase gives the tier mapped HubPhase (Collapsed after the town collapse rule), Tier the current tier number,
    /// Residents the town residents (prosperity _perResident each).
    /// </summary>
    public sealed class TownLink
    {
        /// <summary>Town hub id (hub-1).</summary>
        public string HubId;
        /// <summary>Mapped phase of the current tier.</summary>
        public Func<HubPhase> Phase;
        /// <summary>Current tier number (0 = ruins).</summary>
        public Func<int> Tier;
        /// <summary>Current residents.</summary>
        public Func<int> Residents;
        /// <summary>Tier that wins the run.</summary>
        public int VictoryTier;
        /// <summary>Lowest tier that counts as partial success at the deadline.</summary>
        public int PartialTier;
    }

    /// <summary>
    /// Pure C# hub simulation (Stories 004, 014 and 028): prosperity, development phases, deadline and run ending.
    /// Composition wires track LapCompleted to OnLapCompleted; Story 014's installer attaches item/habitat sources.
    /// Per lap per hub (Collapsed skipped; Story 028: ruins also take the base decay, floored at Minimum):
    ///   BasePerLap + PerPlacedItem * items + PerHabitatCell * cells of completed habitats
    ///   + Σ completed habitat clusters (catalog bonus, or HabitatBonus when the catalog gives none; Story 016)
    ///   + PerNpc * npcs. Settlement is judged before the deadline in the same lap.
    /// Story 028 run ending: when the deadline reaches 0 the run always ends: every hub Settled = Victory, some = Partial,
    /// none = Defeat (DeadlineExpired). "Every once-settled hub collapsed" still ends it early as Defeat. IsRunLost means
    /// "the run is over" for every outcome so the train and controls freeze; IRunResult tells the outcome.
    /// Story 030: with data `_earlyWin` the run ends as Victory the moment every hub is Settled (Laps = laps applied so far);
    /// the deadline still ends it otherwise.
    /// Story 042: with `_useTownTiers` and an attached TownLink, the town hub phase follows the town tier, the deadline ends the run by tier
    /// (victory tier = Victory, partial tier = Partial, below = Defeat), reaching the victory tier ends it early as Victory (_earlyWin),
    /// and the town collapsing is Defeat. Other hubs keep the threshold rules but no longer decide the outcome. Prosperity adds _perResident x residents.
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class RunState : IHubStateSystem, IHubDevelopment, IRunLossInfo, IRunResult, IProsperityForecast, ISaveParticipant
    {
        private static readonly IReadOnlyList<RunHubSummary> NoSummary = Array.Empty<RunHubSummary>();

        private readonly Dictionary<string, HubState> _states = new Dictionary<string, HubState>();
        private readonly INpcRecruiter _npcs;
        private readonly HubRunRules _rules;
        private IProsperitySources _sources = NullProsperitySources.Instance;
        private int _lastLap;
        private int _endSettled = -1;
        private IReadOnlyList<RunHubSummary> _summary;
        private readonly HashSet<string> _closed = new HashSet<string>();
        private readonly Dictionary<string, TownLink> _towns = new Dictionary<string, TownLink>();

        /// <summary>Permanent read-only hub identities in data order, including ruins.</summary>
        public IReadOnlyList<string> HubIds { get; }
        /// <summary>Story 028: true once the run is over for any outcome (deadline reached, or every ever-settled hub collapsed).</summary>
        public bool IsRunLost { get; private set; }
        /// <summary>IRunLossInfo: why the run was lost (contract enum); None while alive and for Victory/Partial.</summary>
        public RunLossReason LastLossReason { get; private set; }
        /// <summary>IRunResult: same as IsRunLost.</summary>
        public bool IsOver => IsRunLost;
        /// <summary>IRunResult: outcome, None while the run continues.</summary>
        public RunOutcome Outcome { get; private set; }
        /// <summary>IRunResult: hubs in Settled when the run ended (live count while it continues).</summary>
        public int SettledCount => _endSettled >= 0 ? _endSettled : CountSettled();
        /// <summary>IRunResult: number of hubs.</summary>
        public int HubCount => OpenHubIds.Count;
        /// <summary>IRunResult: laps applied so far.</summary>
        public int Laps => _lastLap;
        /// <summary>IRunResult: per-hub snapshot taken when the run ended; empty while it continues.</summary>
        public IReadOnlyList<RunHubSummary> Hubs => _summary ?? NoSummary;
        /// <summary>IRunResult: raised once when the run ends, after RunLost.</summary>
        public event Action RunEnded;
        /// <summary>Story 030: true when the run ended through the early-win rule (every hub Settled before the deadline).</summary>
        public bool EndedEarly { get; private set; }
        /// <summary>Laps left before the run ends (deadline); int.MaxValue when the data defines no deadline. Never below 0.</summary>
        public int LapsUntilDeadline { get; private set; }
        /// <summary>Highest lap number already applied; scripted demos tick LastLap + 1.</summary>
        public int LastLap => _lastLap;
        /// <summary>Reports hub identity and new prosperity after a value change.</summary>
        public event Action<string, float> ProsperityChanged;
        /// <summary>Reports a hub identity whenever it enters Ruined (from Recovering) or Collapsed.</summary>
        public event Action<string> HubRuined;
        /// <summary>Reports the run loss once per run.</summary>
        public event Action RunLost;
        /// <summary>hubId and the phase it just entered, once per transition in order.</summary>
        public event Action<string, HubPhase> PhaseChanged;

        /// <summary>Copies hub definitions and injects the NPC interface; rejects empty or duplicate hub data.</summary>
        public RunState(IEnumerable<HubDefinition> definitions, INpcRecruiter npcs)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            _npcs = npcs ?? throw new ArgumentNullException(nameof(npcs));
            var ids = new List<string>();
            foreach (HubDefinition definition in definitions)
            {
                _states.Add(definition.Id, new HubState(definition));
                ids.Add(definition.Id);
                if (_rules == null) _rules = definition.Rules;
            }
            if (ids.Count == 0) throw new ArgumentException("A run requires at least one hub.", nameof(definitions));
            HubIds = ids.AsReadOnly();
            LapsUntilDeadline = _rules == null || _rules.UsesProsperityClear ? int.MaxValue : _rules.DeadlineLaps; // 스펙 004: 클리어 규칙이면 기한 없음 추가
        }

        /// <summary>Story 042: attached town link (null until EconomyInstaller attaches it). Story 048: the first attached link (hub-1) decides the run.</summary>
        public TownLink Town { get; private set; }

        /// <summary>
        /// Story 048: hubs whose line is open, in data order. Closed (locked) hubs keep their state but take no lap rule,
        /// are not counted in HubCount/SettledCount and are left out of the result summary.
        /// </summary>
        public IReadOnlyList<string> OpenHubIds
        {
            get
            {
                if (_closed.Count == 0) return HubIds;
                var open = new List<string>();
                foreach (string id in HubIds) if (!_closed.Contains(id)) open.Add(id);
                return open;
            }
        }

        /// <summary>Story 048: true unless hubId's line is still locked.</summary>
        public bool IsHubOpen(string hubId) => !_closed.Contains(hubId);

        /// <summary>Story 048: opens or closes hubId's line (unknown ids are ignored).</summary>
        public void SetHubOpen(string hubId, bool open)
        {
            if (!_states.ContainsKey(hubId)) return;
            if (open) _closed.Remove(hubId); else _closed.Add(hubId);
        }

        /// <summary>Story 042: true when the town link is attached and HubProfiles.json `_useTownTiers` is on.</summary>
        public bool TownActive => Town != null && _rules != null && _rules.UseTownTiers;

        /// <summary>Story 042: attaches the town tier link and re-evaluates the town hub phase.</summary>
        public void AttachTown(TownLink link)
        {
            if (link == null || link.HubId == null) return;
            if (Town == null) Town = link;
            _towns[link.HubId] = link;
            if (_states.ContainsKey(link.HubId)) RefreshPhase(link.HubId);
        }

        /// <summary>QA 2026-11-01: HubProfiles.json `_cellCapRatio` (1.5 before any definition is loaded).</summary>
        public float CellCapRatio => _rules?.CellCapRatio ?? 1.5f;

        /// <summary>Attaches placed-item and habitat readers (Story 014 installer); until then both read as 0/false.</summary>
        public void AttachSources(IProsperitySources sources)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        }

        /// <summary>Looks up hubId and returns its current prosperity; unknown ids throw.</summary>
        public float GetProsperity(string hubId) => _states[hubId].Prosperity;

        /// <summary>True when hubId is Ruined or Collapsed; unknown ids throw.</summary>
        public bool IsRuined(string hubId) => _states[hubId].IsRuined;

        /// <summary>Current development phase of hubId; unknown ids throw.</summary>
        public HubPhase GetPhase(string hubId) => _states[hubId].Phase;

        /// <summary>True when hubId has reached Settled at least once; unknown ids throw.</summary>
        public bool HasEverSettled(string hubId) => _states[hubId].HasEverSettled;

        /// <summary>Applies delta to hubId (Collapsed ignores it; Ruined takes decreases down to Minimum), then re-evaluates its phase.</summary>
        public void ApplyDelta(string hubId, float delta, string reason)
        {
            HubState state = _states[hubId];
            if (state.ApplyDelta(delta)) ProsperityChanged?.Invoke(hubId, state.Prosperity);
            Evaluate(hubId, state);
        }

        /// <summary>Re-evaluates hubId's phase without changing prosperity (for example after its habitat completed).</summary>
        public void RefreshPhase(string hubId) => Evaluate(hubId, _states[hubId]);

        /// <summary>For a new lap number, applies each hub's lap rule, then counts the deadline down; duplicate or old laps do nothing.</summary>
        public void OnLapCompleted(int lap)
        {
            if (lap <= _lastLap || IsRunLost) return;
            _lastLap = lap;
            foreach (string id in HubIds)
            {
                HubState state = _states[id];
                if (state.Phase == HubPhase.Collapsed || _closed.Contains(id)) continue;
                ApplyDelta(id, LapDelta(id, state), "Lap completed");
                if (IsRunLost) return;
            }
            if (_rules == null) return;
            // 스펙 004: 번영도 클리어 규칙이 켜지면 모든 마을 판정 후 기한 판정 없이 반환
            if (_rules.UsesProsperityClear)
            {
                if (AllHubsAtClearRatio()) End(RunOutcome.Victory, RunLossReason.None, true);
                return;
            }
            if (LapsUntilDeadline > 0) LapsUntilDeadline--;
            if (LapsUntilDeadline > 0) return;
            if (TownActive)
            {
                int tier = Town.Tier();
                if (tier >= Town.VictoryTier) End(RunOutcome.Victory, RunLossReason.None);
                else if (tier >= Town.PartialTier) End(RunOutcome.Partial, RunLossReason.None);
                else End(RunOutcome.Defeat, RunLossReason.DeadlineExpired);
                return;
            }
            int settled = CountSettled();
            if (settled == 0) End(RunOutcome.Defeat, RunLossReason.DeadlineExpired);
            else End(settled >= HubCount ? RunOutcome.Victory : RunOutcome.Partial, RunLossReason.None);
        }

        /// <summary>
        /// Story 040: bankruptcy (balance below 0 for Economy.json _bankruptLaps consecutive settlements, judged by Economy.Settlement)
        /// ends the run on the spot as Defeat with RunLossReason.Bankrupt. reasonText (e.g. "파산") is kept in LossReasonText for the result screen.
        /// Ignored once the run is over.
        /// </summary>
        public void DeclareBankrupt(string reasonText)
        {
            if (IsRunLost) return;
            LossReasonText = reasonText;
            End(RunOutcome.Defeat, RunLossReason.Bankrupt);
        }

        /// <summary>Story 040: display text of a special loss reason (e.g. "파산" for Bankrupt); null for other outcomes.</summary>
        public string LossReasonText { get; private set; }

        /// <summary>IProsperityForecast: the lap rule's delta for hubId right now (0 for Collapsed); unknown ids throw.</summary>
        public float NextLapDelta(string hubId)
        {
            HubState state = _states[hubId];
            return state.Phase == HubPhase.Collapsed ? 0f : LapDelta(hubId, state);
        }

        /// <summary>IProsperityForecast: hubId's data settle threshold; unknown ids throw.</summary>
        public float SettledThreshold(string hubId) => _states[hubId].Definition.SettledThreshold;

        /// <summary>The per-lap formula (see class summary) without encounter results.</summary>
        private float LapDelta(string id, HubState state)
        {
            HubDefinition data = state.Definition;
            return data.BasePerLap
                + data.PerPlacedItem * _sources.PlacedItemCount(id)
                + data.PerHabitatCell * _sources.HabitatCellCount(id)
                + _sources.HabitatBonus(id, data.HabitatBonus)
                + data.PerNpc * _npcs.NpcsAt(id).Count
                + (_rules != null && _towns.TryGetValue(id, out TownLink link) ? _rules.PerResident * (link.Residents?.Invoke() ?? 0) : 0f);
        }

        /// <summary>Runs hubId's transition table and raises PhaseChanged/HubRuined per transition, then checks the collapse loss.</summary>
        private void Evaluate(string hubId, HubState state)
        {
            var transitions = new List<HubPhase>(2);
            TownLink townLink = TownActive && _towns.TryGetValue(hubId, out TownLink found) ? found : null;
            bool town = townLink != null && townLink == Town;
            if (townLink != null) state.SetPhase(townLink.Phase(), transitions);
            else state.Evaluate(_sources.IsHabitatComplete(hubId), transitions);
            bool collapsed = false;
            foreach (HubPhase phase in transitions)
            {
                PhaseChanged?.Invoke(hubId, phase);
                if (phase == HubPhase.Ruined || phase == HubPhase.Collapsed) HubRuined?.Invoke(hubId);
                collapsed |= phase == HubPhase.Collapsed;
            }
            if (town)
            {
                // Story 042: the town collapsing is Defeat; reaching the victory tier is an early Victory (with _earlyWin).
                if (collapsed && !IsRunLost) End(RunOutcome.Defeat, RunLossReason.AllSettledCollapsed);
                if (!IsRunLost && _rules.EarlyWin && !_rules.UsesProsperityClear && Town.Tier() >= Town.VictoryTier) End(RunOutcome.Victory, RunLossReason.None, true); // 스펙 004: 클리어 규칙이면 등급 조기 승리 끔
                return;
            }
            if (TownActive) return; // Story 048: other town hubs (잿골) follow their own tier but never decide the run.
            if (collapsed && !IsRunLost && AllSettledCollapsed()) End(RunOutcome.Defeat, RunLossReason.AllSettledCollapsed);
            // Story 030: early win the moment the last hub enters Settled.
            if (!IsRunLost && _rules != null && _rules.EarlyWin && !_rules.UsesProsperityClear && transitions.Contains(HubPhase.Settled) && CountSettled() >= HubCount) // 스펙 004: 클리어 규칙이면 정착 조기 승리 끔
                End(RunOutcome.Victory, RunLossReason.None, true);
        }

        // 스펙 004: 모든 마을 번영도 클리어 판정 추가
        /// <summary>
        /// 스펙 004 FR-003: 데이터의 모든 마을(잠긴 마을 포함)이 번영도/최대치 ≥ _rules.ClearProsperityRatio인지 돌려준다.
        /// _states의 Prosperity와 Definition.Maximum을 읽기만 한다.
        /// </summary>
        private bool AllHubsAtClearRatio()
        {
            foreach (string id in HubIds)
            {
                HubState state = _states[id];
                // float 변화가 쌓여 80이 79.9999로 남는 경우를 클리어로 보도록 작은 허용 오차를 둔다.
                if (state.Prosperity + 0.0001f < _rules.ClearProsperityRatio * state.Definition.Maximum) return false;
            }
            return true;
        }

        /// <summary>True when at least one hub ever settled and every such hub is Collapsed.</summary>
        private bool AllSettledCollapsed()
        {
            bool any = false;
            foreach (HubState state in _states.Values)
            {
                if (!state.HasEverSettled) continue;
                if (state.Phase != HubPhase.Collapsed) return false;
                any = true;
            }
            return any;
        }

        /// <summary>Hubs currently in Settled.</summary>
        private int CountSettled()
        {
            int count = 0;
            foreach (var pair in _states) if (pair.Value.Phase == HubPhase.Settled && !_closed.Contains(pair.Key)) count++;
            return count;
        }

        /// <summary>Latches the outcome, snapshots every hub, then raises RunLost and RunEnded once.</summary>
        private void End(RunOutcome outcome, RunLossReason reason, bool early = false)
        {
            if (IsRunLost) return;
            IsRunLost = true;
            EndedEarly = early;
            Outcome = outcome;
            LastLossReason = reason;
            _endSettled = CountSettled();
            var summary = new List<RunHubSummary>(HubIds.Count);
            foreach (string id in OpenHubIds)
            {
                HubState state = _states[id];
                summary.Add(new RunHubSummary { HubId = id, Phase = state.Phase, Prosperity = state.Prosperity, NpcCount = _npcs.NpcsAt(id).Count });
            }
            _summary = summary.AsReadOnly();
            UnityEngine.Debug.Log($"RUN END: outcome={outcome}, settled={_endSettled}/{HubCount}, laps={_lastLap}, reason={reason}, early={early}");
            RunLost?.Invoke();
            RunEnded?.Invoke();
        }

        // 스펙 003: 저장 슬롯용 거점 상태·닫힌 거점·바퀴·기한 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "runState";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 70;

        /// <summary>
        /// 거점마다(데이터 순서) 번영·단계·정착 이력과 닫힌 거점(정렬), 적용한 바퀴 _lastLap, 기한 LapsUntilDeadline을 JSON으로 돌려준다.
        /// 마을 연결 _towns는 경제 설치기가 다시 붙이는 함수 묶음이라 저장하지 않는다. 런 종료 결과는 저장하지 않는다.
        /// </summary>
        public string Capture()
        {
            RunSave state = new RunSave { LastLap = _lastLap, LapsUntilDeadline = LapsUntilDeadline };
            foreach (string id in HubIds)
            {
                HubState hub = _states[id];
                state.Hubs.Add(new HubSave { HubId = id, Prosperity = hub.Prosperity, Phase = (int)hub.Phase, EverSettled = hub.HasEverSettled });
            }
            List<string> closed = new List<string>(_closed);
            closed.Sort(StringComparer.Ordinal);
            state.Closed = closed.ToArray();
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 거점 상태·닫힌 거점·바퀴·기한을 바꾼다. 거점마다 ProsperityChanged를,
        /// 단계가 달라진 거점은 PhaseChanged를 낸다.
        /// </summary>
        public void Restore(string json)
        {
            RunSave state = UnityEngine.JsonUtility.FromJson<RunSave>(json);
            _lastLap = state.LastLap;
            LapsUntilDeadline = state.LapsUntilDeadline;
            _closed.Clear();
            foreach (string id in state.Closed) if (_states.ContainsKey(id)) _closed.Add(id);
            foreach (HubSave saved in state.Hubs)
            {
                if (saved == null || saved.HubId == null || !_states.TryGetValue(saved.HubId, out HubState hub)) continue;
                HubPhase before = hub.Phase;
                hub.Restore(saved.Prosperity, (HubPhase)saved.Phase, saved.EverSettled);
                ProsperityChanged?.Invoke(saved.HubId, hub.Prosperity);
                if (hub.Phase != before) PhaseChanged?.Invoke(saved.HubId, hub.Phase);
            }
        }

        /// <summary>스펙 003. 저장 형식: 거점 목록, 닫힌 거점, 바퀴, 기한.</summary>
        [Serializable]
        private sealed class RunSave
        {
            public List<HubSave> Hubs = new List<HubSave>();
            public string[] Closed = Array.Empty<string>();
            public int LastLap;
            public int LapsUntilDeadline;
        }

        /// <summary>스펙 003. 저장 형식: 거점 하나의 번영·단계(정수)·정착 이력.</summary>
        [Serializable]
        private sealed class HubSave
        {
            public string HubId;
            public float Prosperity;
            public int Phase;
            public bool EverSettled;
        }
    }
}
