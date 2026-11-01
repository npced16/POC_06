using System;
using System.Collections;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Hub.Demo
{
    /// <summary>
    /// Story 014 scripted capture demo, active only with --phase-demo or --deadline-demo (command line or the bridge's
    /// in-progress capture ExtraArgs, same SessionState pattern as MainRoot's --integration-demo).
    /// --phase-demo: hub Recovering with placed habitat items, one NPC and one facility in the interior, hold, then tick laps until Settled.
    /// --deadline-demo: tick laps while docked until the deadline ends the run (Story 028: result screen, 패배 with nothing settled).
    /// --result-demo [--result-stage partial|win|lose] (Story 028): raise prosperity to the maximum on 2 (partial), every (win)
    /// or no (lose) hub so they settle (data ResultSettleProsperity), then tick every remaining lap in one frame so the deadline ends
    /// the run at once (RunEnded opens the RunResult screen). stage restart: partial, then after ResultRestartFrames frames calls the
    /// result screen's restart (RunResultScreen.Restart) and, after the reload, logs whether services and HUD screens came back.
    /// Laps are ticked by calling RunState.OnLapCompleted(LastLap + 1) while the train stays docked (approved for the demo).
    /// Wire format: Resources/HubPhase/hub_phase_demo.json (seconds are real time, unaffected by timeScale).
    /// </summary>
    public sealed class PhaseDemoDriver : MonoBehaviour
    {
        [Serializable]
        private sealed class Settings
        {
            [SerializeField] private string _hubId = "hub-1";
            [SerializeField] private float _startDelaySeconds = 1f;
            [SerializeField] private float _seedProsperity = 20f;
            [SerializeField] private int _npcVisitAttempts = 20;
            [SerializeField] private float _recoveringHoldSeconds = 6f;
            [SerializeField] private float _lapIntervalSeconds = 0.5f;
            [SerializeField] private int _maxLaps = 30;
            [SerializeField] private float _deadlineLapIntervalSeconds = 0.5f;
            [SerializeField] private string[] _habitatItems = { "brazier", "brazier", "gear" };
            [SerializeField] private int _extraEmptyCells = 3;
            [SerializeField] private int _maxInvestigations = 36;
            [SerializeField] private int _resultPartialHubs = 2;
            [SerializeField] private float _resultSettleProsperity = 100f;
            [SerializeField] private int _resultRestartFrames = 60;
            public int ResultRestartFrames => _resultRestartFrames;
            public int ResultPartialHubs => _resultPartialHubs;
            public float ResultSettleProsperity => _resultSettleProsperity;
            public string HubId => _hubId;
            public float StartDelaySeconds => _startDelaySeconds;
            public float SeedProsperity => _seedProsperity;
            public int NpcVisitAttempts => _npcVisitAttempts;
            public float RecoveringHoldSeconds => _recoveringHoldSeconds;
            public float LapIntervalSeconds => _lapIntervalSeconds;
            public int MaxLaps => _maxLaps;
            public float DeadlineLapIntervalSeconds => _deadlineLapIntervalSeconds;
            public string[] HabitatItems => _habitatItems ?? Array.Empty<string>();
            public int ExtraEmptyCells => _extraEmptyCells;
            public int MaxInvestigations => _maxInvestigations;
        }

        private static PhaseDemoDriver _instance;
        private Settings _settings;
        private bool _deadline;
        private string _resultStage;
        /// <summary>Set just before the restart demo reloads the scene; deliberately not cleared by ResetStatics (the restart replays it).</summary>
        private static bool _restartIssued;

        /// <summary>Clears statics for a new play session (domain reload disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        /// <summary>Subscribes to Ready only when a demo flag is present.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            GameServices.Ready -= Create;
            string[] args = Arguments();
            if (args.Contains("--phase-demo") || args.Contains("--deadline-demo") || args.Contains("--result-demo")) GameServices.Ready += Create;
        }

        /// <summary>Starts the requested scripted demo once the real systems are ready.</summary>
        private static void Create()
        {
            if (_instance != null || !(GameServices.Get<IHubStateSystem>() is RunState)) return;
            _instance = new GameObject(nameof(PhaseDemoDriver)).AddComponent<PhaseDemoDriver>();
            _instance._deadline = Arguments().Contains("--deadline-demo") && !Arguments().Contains("--phase-demo");
            TextAsset json = UnityEngine.Resources.Load<TextAsset>("HubPhase/hub_phase_demo");
            _instance._settings = json != null ? JsonUtility.FromJson<Settings>(json.text) : new Settings();
            string[] args = Arguments();
            if (args.Contains("--result-demo") && !args.Contains("--phase-demo"))
            {
                int at = Array.IndexOf(args, "--result-stage");
                _instance._resultStage = at >= 0 && at + 1 < args.Length ? args[at + 1] : "partial";
                if (_instance._resultStage == "restart" && _restartIssued) { _restartIssued = false; _instance.StartCoroutine(_instance.AfterRestart()); return; }
                _instance.StartCoroutine(_instance.ResultDemo());
                return;
            }
            _instance.StartCoroutine(_instance._deadline ? _instance.DeadlineDemo() : _instance.PhaseDemo());
        }

        /// <summary>
        /// Drives the hub to Recovering (data seed), reveals cells until a facility plot and enough empty cells exist,
        /// places the data's habitat items through Story 013 (completing the tag habitat), recruits one NPC through the
        /// real recruiter, builds one facility on the plot, opens the interior and holds; then ticks laps until Settled.
        /// </summary>
        private IEnumerator PhaseDemo()
        {
            yield return new WaitForSecondsRealtime(_settings.StartDelaySeconds);
            var hubs = (RunState)GameServices.Get<IHubStateSystem>();
            string hub = _settings.HubId;
            hubs.ApplyDelta(hub, _settings.SeedProsperity, "phase-demo seed");
            RevealCells(hub);
            PlaceHabitatItems(hub);
            INpcRecruiter npcs = GameServices.Get<INpcRecruiter>();
            IronRing.Npc.NpcInstaller.Passengers?.BoardForHub(hub); // 스토리 023: 합류는 승객 하차로만
            for (int i = 0; i < _settings.NpcVisitAttempts && npcs.NpcsAt(hub).Count == 0; i++) npcs.OnHubVisited(hub);
            OpenInterior(hub);
            IHabitatSystem habitat = GameServices.Get<IHabitatSystem>();
            UnityEngine.Debug.Log($"PHASE-DEMO recovering: phase={hubs.GetPhase(hub)}, prosperity={hubs.GetProsperity(hub)}, habitat={habitat?.IsHabitatComplete(hub)}, npcs={npcs.NpcsAt(hub).Count}");
            yield return new WaitForSecondsRealtime(_settings.RecoveringHoldSeconds);
            for (int lap = 0; lap < _settings.MaxLaps && hubs.GetPhase(hub) != HubPhase.Settled && !hubs.IsRunLost; lap++)
            {
                hubs.OnLapCompleted(hubs.LastLap + 1);
                yield return new WaitForSecondsRealtime(_settings.LapIntervalSeconds);
            }
            UnityEngine.Debug.Log($"PHASE-DEMO settled: phase={hubs.GetPhase(hub)}, prosperity={hubs.GetProsperity(hub)}, lap={hubs.LastLap}, lost={hubs.IsRunLost}");
        }

        /// <summary>Investigates rubble (re-docking when the per-dock count runs out) until a plot and enough empty cells exist.</summary>
        private void RevealCells(string hub)
        {
            IHubInterior interior = GameServices.Get<IHubInterior>();
            if (interior == null) { UnityEngine.Debug.LogWarning("PHASE-DEMO: IHubInterior 미등록 - 칸 조사를 건너뜁니다."); return; }
            int wantEmpty = _settings.HabitatItems.Length + _settings.ExtraEmptyCells;
            for (int step = 0; step < _settings.MaxInvestigations; step++)
            {
                int empty = 0, rubble = -1;
                foreach (IInteriorCell cell in interior.Cells(hub))
                {
                    if (cell.State == InteriorCellState.Empty || cell.State == InteriorCellState.ItemPlaced) empty++;
                    else if (cell.State == InteriorCellState.Rubble && rubble < 0) rubble = cell.Index;
                }
                if (empty >= wantEmpty || rubble < 0) return;
                if (interior.InvestigationsLeft(hub) <= 0) interior.OnDocked(hub);
                if (!interior.CanInvestigate(hub, rubble)) return;
                interior.Investigate(hub, rubble, out _);
            }
        }

        /// <summary>Places the data's habitat items on Empty cells through IItemPlacement, granting any missing item (logged demo grant).</summary>
        private void PlaceHabitatItems(string hub)
        {
            IItemInventory items = GameServices.Get<IItemInventory>();
            IItemPlacement placement = GameServices.Get<IItemPlacement>();
            IHubInterior interior = GameServices.Get<IHubInterior>();
            if (items == null || placement == null || interior == null)
            {
                UnityEngine.Debug.LogWarning("PHASE-DEMO: 013 아이템 서비스 미등록 - 서식지 아이템 배치를 건너뜁니다.");
                return;
            }
            foreach (string itemId in _settings.HabitatItems)
            {
                if (items.Count(itemId) <= 0) { items.Add(itemId, 1); UnityEngine.Debug.Log("PHASE-DEMO: 데모용 아이템 지급 " + itemId); }
                bool placed = false;
                foreach (IInteriorCell cell in interior.Cells(hub))
                    if (cell.State == InteriorCellState.Empty && placement.TryPlace(hub, cell.Index, itemId)) { placed = true; break; }
                if (!placed) UnityEngine.Debug.LogWarning("PHASE-DEMO: 아이템 배치 실패 " + itemId);
            }
        }

        /// <summary>Ticks laps with nothing settled until the deadline defeat fires.</summary>
        private IEnumerator DeadlineDemo()
        {
            yield return new WaitForSecondsRealtime(_settings.StartDelaySeconds);
            var hubs = (RunState)GameServices.Get<IHubStateSystem>();
            for (int lap = 0; lap < _settings.MaxLaps && !hubs.IsRunLost; lap++)
            {
                hubs.OnLapCompleted(hubs.LastLap + 1);
                yield return new WaitForSecondsRealtime(_settings.DeadlineLapIntervalSeconds);
            }
            UnityEngine.Debug.Log($"DEADLINE-DEMO: over={hubs.IsRunLost}, outcome={hubs.Outcome}, reason={hubs.LastLossReason}, lapsLeft={hubs.LapsUntilDeadline}, lap={hubs.LastLap}");
        }

        /// <summary>Story 028. Settles the stage's hub count through ApplyDelta, then ticks laps until the deadline shows the result screen.</summary>
        private IEnumerator ResultDemo()
        {
            UnityEngine.Debug.Log($"RESULT DEMO: stage={_resultStage}");
            yield return null; // 한 프레임: 화면들이 Ready에서 RunEnded를 구독한 뒤에 끝낸다
            var hubs = (RunState)GameServices.Get<IHubStateSystem>();
            int settle = _resultStage == "win" ? hubs.HubIds.Count : _resultStage == "lose" ? 0 : Math.Min(_settings.ResultPartialHubs, hubs.HubIds.Count);
            if (_resultStage == "partial" && settle >= hubs.HubIds.Count)
            {
                // Story 032: 역이 1곳이면 부분 성공이 있을 수 없다(전부 정착 = 승리). 하나 덜 정착시키고 기한 종료로 보인다.
                settle = hubs.HubIds.Count - 1;
                UnityEngine.Debug.LogWarning($"RESULT DEMO: 거점 {hubs.HubIds.Count}곳이라 부분 성공을 만들 수 없어 {settle}곳만 정착시킵니다.");
            }
            for (int i = 0; i < settle; i++) hubs.ApplyDelta(hubs.HubIds[i], _settings.ResultSettleProsperity, "result-demo settle");
            UnityEngine.Debug.Log($"RESULT DEMO: settled now={hubs.SettledCount}, lapsLeft={hubs.LapsUntilDeadline}");
            for (int lap = 0; lap < _settings.MaxLaps && !hubs.IsOver; lap++) hubs.OnLapCompleted(hubs.LastLap + 1);
            UnityEngine.Debug.Log($"RESULT DEMO: over={hubs.IsOver}, outcome={hubs.Outcome}, settled={hubs.SettledCount}/{hubs.HubCount}, laps={hubs.Laps}, early={hubs.EndedEarly}, lapsLeft={hubs.LapsUntilDeadline}");
            if (_resultStage != "restart") yield break;
            for (int i = 0; i < _settings.ResultRestartFrames; i++) yield return null;
            UnityEngine.Debug.Log("RESULT DEMO: pressing restart");
            _restartIssued = true;
            IronRing.Hud.RunResultScreen.Restart();
        }

        /// <summary>restart stage, second scene: logs that the new run is alive and the HUD screens reattached.</summary>
        private IEnumerator AfterRestart()
        {
            for (int i = 0; i < _settings.ResultRestartFrames; i++) yield return null;
            var hubs = GameServices.Get<IHubStateSystem>() as RunState;
            IronRing.Hud.Ui.UiScreens screens = IronRing.Hud.Ui.UiScreens.Instance;
            string Open(string id) => screens == null ? "no-UiScreens" : screens.IsOpen(id).ToString();
            UnityEngine.Debug.Log($"RESULT DEMO: after restart ready={GameServices.IsReady}, over={hubs?.IsOver}, lap={hubs?.LastLap}, lapsLeft={hubs?.LapsUntilDeadline}, "
                + $"forecast={GameServices.Get<IProsperityForecast>() != null}, TopBar={Open("TopBar")}, PhaseBanner={Open("PhaseBanner")}, Hotbar={Open("Hotbar")}, RunResult={Open("RunResult")}");
        }


        /// <summary>Opens Story 012's interior view through IInteriorNavigation; logs once and skips when it is not registered.</summary>
        private static void OpenInterior(string hub)
        {
            IInteriorNavigation navigation = GameServices.Get<IInteriorNavigation>();
            if (navigation == null)
            {
                UnityEngine.Debug.LogWarning("PHASE-DEMO: IInteriorNavigation 미등록 - 내부 화면을 열지 않고 건너뜁니다.");
                return;
            }
            navigation.Open(hub);
        }

        /// <summary>Command-line arguments plus the bridge capture's ExtraArgs while a capture is in progress.</summary>
        private static string[] Arguments()
        {
            string extra = "";
#if UNITY_EDITOR
            string phase = UnityEditor.SessionState.GetString("IronRing.Bridge.Phase", "");
            if (phase == "enter-play" || phase == "capture")
                extra = UnityEditor.SessionState.GetString("IronRing.Bridge.Capture.ExtraArgs", "");
#endif
            return Environment.GetCommandLineArgs().Concat(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
