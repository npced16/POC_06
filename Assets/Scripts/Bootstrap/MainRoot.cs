using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

using IronRing.Combat;
using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Interior;
using IronRing.Presentation.Interior;
using IronRing.Presentation.Map;
using IronRing.Track;

namespace IronRing.Bootstrap
{
    /// <summary>Scene-owned composition and presentation driver for the real playable MVP.</summary>
    public sealed class MainRoot : MonoBehaviour
    {
        [Header("Production data: track, NPC, hub, combat, settings")]
        [SerializeField] private TextAsset _trackData;
        [SerializeField] private TextAsset _npcData;
        [SerializeField] private TextAsset _hubData;
        [SerializeField] private TextAsset _combatData;
        [SerializeField] private TextAsset _settingsData;
        [Header("Story 012 interior data (falls back to Resources/Interior/HubInterior when empty)")]
        [SerializeField] private TextAsset _interiorData;
        [Header("Scene views")]
        [SerializeField] private TrainController _train;
        [SerializeField] private MapView _map;
        private GameDefinition _definition;
        private GameSession _game;
        private EncounterPresentationSource _encounters;
        private InteriorViewMode _viewMode;
        private HubInteriorView _interiorView;
        private bool _demo;
        private bool _interiorDemo;
        private bool _wasHeld;
        private bool _capturing;
        private bool _npcCaptured;
        private bool _finished;
        private float _timer;
        private float _previousTimeScale;
        private bool _battleActive;
        private BattlePocDriver _battleDriver;
        private readonly List<Renderer> _hiddenBattleRenderers = new List<Renderer>();
        private readonly List<Canvas> _hiddenBattleCanvases = new List<Canvas>();
        private readonly List<Camera> _hiddenBattleCameras = new List<Camera>();
        private readonly List<EventSystem> _hiddenBattleEventSystems = new List<EventSystem>();
        private HashSet<string> _battleWaypoints;
        /// <summary>Live real-system session owned by this scene.</summary>
        public GameSession Session => _game;

        /// <summary>Stores authored data and view references once during Main scene creation.</summary>
        public void Configure(TextAsset trackData, TextAsset npcData, TextAsset hubData,
            TextAsset combatData, TextAsset settingsData, TrainController train, MapView map)
        {
            _trackData = trackData;
            _npcData = npcData;
            _hubData = hubData;
            _combatData = combatData;
            _settingsData = settingsData;
            _train = train;
            _map = map;
        }

        IEnumerator Start()
        {
            TextAsset interiorData = _interiorData != null ? _interiorData : UnityEngine.Resources.Load<TextAsset>("Interior/HubInterior");
            if (interiorData == null) throw new InvalidOperationException("Missing Story 012 interior data: Resources/Interior/HubInterior.json");
            _definition = GameDefinition.Parse(_trackData.text, _npcData.text,
                _hubData.text, _combatData.text, _settingsData.text, interiorData.text);
            _demo = Arguments().Contains("--integration-demo");
            _interiorDemo = Arguments().Contains("--interior-demo");
            _battleWaypoints = new HashSet<string>(StringComparer.Ordinal);
            if (!_demo && !_interiorDemo && !DefenseVisibility.Hidden) // 스펙 006: 디펜스 숨김 동안 전투 경유지를 모으지 않음
            {
                foreach (MapNode node in _map.GetComponentsInChildren<MapNode>(true))
                    if (node.Hub == null && node.Content == WaypointContent.Battle) _battleWaypoints.Add(node.Id);
            }
            _game = new GameSession(_definition, _demo, _train.Track, !_demo && !_interiorDemo);
            _encounters = new EncounterPresentationSource(_game.Waypoints, _definition.Settings.EventKeys);
            _map.Bind(_game.Track, () => _train.TravelProgress);
            _viewMode = new InteriorViewMode();
            _interiorView = HubInteriorView.Create(_game.Interior, _game.Interior.HubIds, _viewMode, _map);
            GameServices.Register<ITrackRuntime>(_game.Track);
            GameServices.Register<IResourceInventory>(_game.Inventory);
            GameServices.Register<IHabitatSystem>(_game.Habitat);
            GameServices.Register<INpcRecruiter>(_game.Npcs);
            GameServices.Register<IHubStateSystem>(_game.Hubs);
            GameServices.Register<IWaypointResolver>(_game.Waypoints);
            GameServices.Register<IMapAnchors>(_map);
            GameServices.Register<IHubClickSource>(_map);
            GameServices.Register<IEncounterPresentationSource>(_encounters);
            // Story 012: 거점 내부 규칙, 조사 보고, 화면 전환 상태, 내부 칸 앵커와 클릭 소스.
            GameServices.Register<IHubInterior>(_game.Interior);
            GameServices.Register<IInvestigationReport>(_game.Interior);
            GameServices.Register<IViewMode>(_viewMode);
            GameServices.Register<IInteriorAnchors>(_interiorView);
            GameServices.Register<IInteriorCellClickSource>(_interiorView);
            GameServices.Register<IInteriorNavigation>(_interiorView);
            // 다른 스토리(013/014)의 설치자가 여기서 자기 서비스를 등록한다. 정확히 한 번, MarkReady 전에 부른다.
            GameServices.RaiseComposing();
            LoopInstaller.Loop?.SetBattleWaypoints(_battleWaypoints);
            // Composing에서 이긴 구현을 뒤에서 연결한다: 서식지(013이 IHabitatSystem을 교체하면 NpcRecruiter가 그것을 쓴다), 아이템 인벤토리.
            _game.BindHabitat(GameServices.Get<IHabitatSystem>());
            _game.Interior.AttachItemInventory(GameServices.Get<IItemInventory>());
            _game.Track.Arrived += OnArrived;
            _game.Waypoints.EncounterStarted += OnEncounter;
            _wasHeld = _game.Track.IsHeld;
            GameServices.MarkReady();
            if (_game.Track.IsStopped) _viewMode.Open(_game.Track.CurrentNode.HubId);
            if (Arguments().Contains("--map-depart")) _train.Depart();
            if (_interiorDemo) RunInteriorDemo();
            if (_demo)
            {
                Application.runInBackground = true;
                _previousTimeScale = Time.timeScale;
                Time.timeScale = _definition.Settings.DemoTimeScale;
                _capturing = true;
                yield return Capture("01-main-hud.png", false);
            }
        }

        void Update()
        {
            if (_game == null || _finished) return;
            // 출발 순간(정차 해제 엣지)에 내부 화면을 닫아 카메라를 선로 화면으로 되돌린다. 출발 명령은 HUD 버튼이나 TrainController가 낸다.
            // 엣지로 감지하므로 IInteriorNavigation.Open으로 이동 중에 연 화면(데모)은 다음 출발까지 유지된다.
            bool held = _game.Track.IsHeld;
            if (_wasHeld && !held && _viewMode.InteriorOpen) _viewMode.Close();
            _wasHeld = held;
            if (_capturing) return;
            _timer += Time.deltaTime;
            if (_game.Waypoints.IsEncounterActive)
            {
                if (_timer >= _definition.Settings.CombatStepSeconds + _game.Waypoints.ExtraHoldSeconds) Advance();
                return;
            }
            if (!_demo) return;
            if (_npcCaptured && _game.Hubs.HubIds.Any(_game.Hubs.IsRuined))
            {
                _capturing = true;
                StartCoroutine(Capture("04-ruin.png", true));
                return;
            }
            if (_game.Hubs.IsRunLost || _game.Track.Lap > _definition.Settings.MaxDemoLaps)
            {
                UnityEngine.Debug.LogError("Integration demo did not reach all required checkpoints.");
                _finished = true; Application.Quit(1); return;
            }
            if (_game.Track.IsStopped)
            {
                string hub = _game.Track.CurrentNode.HubId;
                if (hub == _definition.Settings.DemoHubId)
                {
                    if (!_game.Habitat.IsHabitatComplete(hub)) _game.PlaceRecipe(hub);
                    if (!_npcCaptured && _game.Npcs.NpcsAt(hub).Count > 0)
                    {
                        // 거점이 폐허이거나 건설 조건이 안 되면 건너뛴다(Story 014 이후 거점은 폐허로 시작한다).
                        _npcCaptured = true; _capturing = true;
                        StartCoroutine(Capture("03-habitat-npc.png", false)); return;
                    }
                }
                if (_timer >= _definition.Settings.StationDwellSeconds) Depart();
            }
        }

        /// <summary>Uses committed arrival state to reset dwell timing and opens the hub interior view when the train docks at a hub.</summary>
        private void OnArrived(ITrackNode node)
        {
            _timer = 0;
            if (node.Kind == TrackNodeKind.Hub) _viewMode.Open(node.HubId);
            else if (_battleWaypoints.Contains(node.Id) && !_battleActive)
            {
                _battleActive = true;
                _game.Track.RequestHold("battle-stage");
                StartCoroutine(OpenBattle());
            }
        }

        /// <summary>경유지에 도착한 열차를 붙잡고 BattleReference 씬을 추가로 열어 전투 결과를 기다린다.</summary>
        private IEnumerator OpenBattle()
        {
            AsyncOperation loading = SceneManager.LoadSceneAsync("BattleReference", LoadSceneMode.Additive);
            if (loading == null)
            {
                UnityEngine.Debug.LogError("BattleReference scene is missing from Build Settings.");
                _game.Track.ReleaseHold("battle-stage");
                _battleActive = false;
                yield break;
            }
            yield return loading;
            Scene battle = SceneManager.GetSceneByName("BattleReference");
            foreach (GameObject root in battle.GetRootGameObjects())
                if ((_battleDriver = root.GetComponentInChildren<BattlePocDriver>(true)) != null) break;
            if (_battleDriver == null)
            {
                UnityEngine.Debug.LogError("BattleReference scene has no BattlePocDriver.");
                yield return SceneManager.UnloadSceneAsync(battle);
                _game.Track.ReleaseHold("battle-stage");
                _battleActive = false;
                yield break;
            }
            HideMainForBattle(battle);
            _battleDriver.Completed += OnBattleCompleted;
        }

        /// <summary>전투 씬 이외의 현재 렌더러·캔버스·카메라·입력 시스템을 숨기고 복원 목록에 기록한다.</summary>
        private void HideMainForBattle(Scene battle)
        {
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (renderer.gameObject.scene != battle && renderer.enabled)
                {
                    _hiddenBattleRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.gameObject.scene != battle && canvas.enabled)
                {
                    _hiddenBattleCanvases.Add(canvas);
                    canvas.enabled = false;
                }
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.gameObject.scene != battle && camera.enabled)
                {
                    _hiddenBattleCameras.Add(camera);
                    camera.enabled = false;
                }
            foreach (EventSystem events in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (events.gameObject.scene != battle && events.enabled)
                {
                    _hiddenBattleEventSystems.Add(events);
                    events.enabled = false;
                }
        }

        /// <summary>전투 승패가 확정되면 결과 문구를 잠시 보여준 뒤 열차 화면으로 복귀한다.</summary>
        private void OnBattleCompleted(bool victory)
        {
            StartCoroutine(ReturnFromBattle());
        }

        /// <summary>전투 씬을 내리고 원래 화면과 열차 이동을 복원한다.</summary>
        private IEnumerator ReturnFromBattle()
        {
            yield return new WaitForSecondsRealtime(3f);
            if (_battleDriver != null) _battleDriver.Completed -= OnBattleCompleted;
            Scene battle = SceneManager.GetSceneByName("BattleReference");
            if (battle.IsValid() && battle.isLoaded) yield return SceneManager.UnloadSceneAsync(battle);
            RestoreMainAfterBattle();
            _game.Track.ReleaseHold("battle-stage");
            _battleDriver = null;
            _battleActive = false;
        }

        /// <summary>전투 진입 전에 켜져 있던 렌더러·캔버스·카메라·입력 시스템을 다시 켠다.</summary>
        private void RestoreMainAfterBattle()
        {
            foreach (Renderer renderer in _hiddenBattleRenderers) if (renderer != null) renderer.enabled = true;
            foreach (Canvas canvas in _hiddenBattleCanvases) if (canvas != null) canvas.enabled = true;
            foreach (Camera camera in _hiddenBattleCameras) if (camera != null) camera.enabled = true;
            foreach (EventSystem events in _hiddenBattleEventSystems) if (events != null) events.enabled = true;
            _hiddenBattleRenderers.Clear();
            _hiddenBattleCanvases.Clear();
            _hiddenBattleCameras.Clear();
            _hiddenBattleEventSystems.Clear();
        }

        /// <summary>
        /// 캡처용 시나리오(--interior-demo): 시작 정차 거점의 내부가 열린 상태에서 남은 횟수만큼 서로 떨어진 잔해 칸을 조사해
        /// "조사 3회 후" 화면을 만든다. 칸 선택 공식은 연출용이며 게임 규칙이 아니다.
        /// </summary>
        private void RunInteriorDemo()
        {
            if (!_game.Track.IsStopped) return;
            string hub = _game.Track.CurrentNode.HubId;
            IReadOnlyList<IInteriorCell> cells = _game.Interior.Cells(hub);
            int count = _game.Interior.InvestigationsLeft(hub);
            for (int i = 0; i < count; i++)
            {
                int index = (i * 13 + 2) % cells.Count;
                if (_game.Interior.CanInvestigate(hub, index)) _game.Interior.Investigate(hub, index, out _);
            }
        }

        /// <summary>Starts the displayed encounter step clock and captures the first real combat in demo mode.</summary>
        private void OnEncounter(ITrackNode node)
        {
            _timer = 0;
        }

        /// <summary>Requests real departure from a docked hub and resets the presentation clock.</summary>
        private void Depart() { if (!_game.Hubs.IsRunLost) _train.Depart(); _timer = 0; }

        /// <summary>Advances one real event/combat step and refreshes its visible progress and outcome.</summary>
        private void Advance() { _encounters.Advance(); _timer = 0; }

        /// <summary>Captures a rendered real-system checkpoint to the requested evidence directory and optionally exits the demo player.</summary>
        private IEnumerator Capture(string filename, bool exit)
        {
           
            yield return new WaitForSecondsRealtime(_definition.Settings.CapturePauseSeconds);
            yield return new WaitForEndOfFrame();
            string folder = Argument("--evidence-dir") ?? Path.Combine(Application.persistentDataPath, "integration");
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, filename));
            UnityEngine.Debug.Log($"INTEGRATION {filename}: node={_game.Track.CurrentNode.Id}, lap={_game.Track.Lap}, encounter={_game.Waypoints.LastOutcome}, npcs={_game.Npcs.NpcsAt(_definition.Settings.DemoHubId).Count}, ruins={_game.Hubs.HubIds.Count(_game.Hubs.IsRuined)}");
            for (int i = 0; i < 5; i++) yield return null;
            _capturing = false;
            if (exit) { _finished = true; Application.Quit(); }
        }

        /// <summary>Returns the command-line value following name or null if no such argument was supplied.</summary>
        private static string Argument(string name)
        {
            string[] arguments = Arguments();
            for (int i = 0; i < arguments.Length - 1; i++) if (arguments[i] == name) return arguments[i + 1];
            return null;
        }

        /// <summary>실행 인자와 활성 편집기 캡처 인자를 합쳐 데모 설정을 반환하며 종료된 캡처 인자는 무시한다.</summary>
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

        /// <summary>플레이 진입 때 이전 세션 등록을 비워 새 서비스가 등록되도록 한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetServices() => GameServices.Reset();

        void OnDestroy()
        {
            if (_game == null) return;
            if (_battleDriver != null) _battleDriver.Completed -= OnBattleCompleted;
            RestoreMainAfterBattle();
            if (_battleActive) _game.Track.ReleaseHold("battle-stage");
            _game.Track.Arrived -= OnArrived;
            _game.Waypoints.EncounterStarted -= OnEncounter;
            _game.Dispose();
            GameServices.Reset();
            if (_demo) Time.timeScale = _previousTimeScale;
        }
    }
}
