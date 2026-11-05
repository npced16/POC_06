using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Presentation.HubPhase;

namespace IronRing.Npc.Demo
{
    /// <summary>
    /// 스토리 017·023 캡처용 스크립트 데모: `--npc-demo [--npc-stage homes|panel|panel-end|crowd|board|arrival|hub2|passengers|codex]`. 실행 인자 또는 진행 중인
    /// 브리지 캡처의 extraArgs(SessionState "IronRing.Bridge.Capture.ExtraArgs")에서 읽는다. 설정은 Resources/Npc/NpcHomesDemo.json.
    /// homes|panel: 데모 거점 내부를 열고 데이터 배치를 놓은 뒤, 대상 NPC를 승객으로 태우고(IPassengers.TryBoard force)
    /// INpcRecruiter.OnHubVisited로 정차를 일으켜 내리게 한다(합류는 승객 하차로만). 승객 시스템이 없으면 예전처럼 방문을 반복한다.
    /// board: 열차를 출발시켜 경유지 조우를 WaypointResolver.Advance로 끝내고, 승객이 타면(안 타면 강제 탑승 + 로그) StageReady.
    /// arrival: hub-1에 서식지를 완성하고 `_arrivalNpcId`를 태워 내리게 한다. 걸음은 `_arrivalSpeed`로 느리게(InteriorNpcWalker.DemoArrivalSpeed).
    /// hub2: hub-2 내부를 열어 같은 서식지(배치 앞 `_hub2PlacementCount`개)를 완성하고 hub-2 명단의 NPC(`_hub2NpcId`)가 내리는 것을 보인다.
    /// passengers(스토리 025): `_passengerIds`를 IPassengers.TryBoard(force)로 태우고 StageReady → PassengersScreen이 열린다(내부 화면을 열지 않음).
    /// codex(스토리 025): homes와 같이 hub-1에 합류시킨 뒤 StageReady → CodexScreen이 열린다("찾아오는 이: 점등부, ???").
    /// panel-end(스토리 029): panel과 같이 준비한 뒤 대화 화면이 [계속하기]를 끝까지 눌러 선택지 줄을 보인다.
    /// crowd(스토리 029): homes 뒤 `_crowdNpcIds`를 같은 거점에 강제 합류시켜 한 건물 문 앞에 둘이 서게 한다(대화 화면은 닫힘).
    /// 로그 접두사 "NPC DEMO:".
    /// </summary>
    public sealed class NpcHomesDemoDriver : MonoBehaviour
    {
        private const string FLAG = "--npc-demo";
        private const string STAGE_FLAG = "--npc-stage";
        private const string SETTINGS_PATH = "Npc/NpcHomesDemo";
        private static NpcHomesDemoDriver _instance;
        private static Action<string> _stageReady;
        private Settings _settings;
        private int _waited;
        private bool _opened;
        private bool _done;
        private bool _departed;
        private int _boardFrames;

        /// <summary>데모 단계(homes|panel|board|arrival|hub2|passengers|codex). --npc-demo가 없으면 null, 단계 인자가 없으면 "homes".</summary>
        public static string Stage { get; private set; }

        /// <summary>단계 준비가 끝나 StageReady를 낸 뒤 true.</summary>
        public static bool IsStageReady { get; private set; }

        /// <summary>패널을 열 NPC(합류·탑승한 주인공). StageReady 전에는 null.</summary>
        public static string FirstNpcId { get; private set; }

        /// <summary>단계 준비가 끝난 뒤 한 번, 첫 NPC id와 함께 발생한다. 이미 끝났으면 구독 즉시 호출된다.</summary>
        public static event Action<string> StageReady
        {
            add { _stageReady += value; if (IsStageReady) value?.Invoke(FirstNpcId); }
            remove { _stageReady -= value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Ready -= Create;
            _instance = null;
            _stageReady = null;
            Stage = null;
            IsStageReady = false;
            FirstNpcId = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            ReadStage();
            GameServices.Ready -= Create;
            GameServices.Ready += Create;
        }

        private static void ReadStage()
        {
            string[] args = Arguments();
            if (!args.Contains(FLAG)) return;
            int at = Array.IndexOf(args, STAGE_FLAG);
            Stage = at >= 0 && at + 1 < args.Length ? args[at + 1] : "homes";
        }

        private static void Create()
        {
            if (Stage == null) ReadStage();
            if (_instance != null || Stage == null) return;
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>(SETTINGS_PATH);
            if (asset == null) { UnityEngine.Debug.LogError("NPC DEMO: Resources/" + SETTINGS_PATH + ".json 없음"); return; }
            _instance = new GameObject(nameof(NpcHomesDemoDriver)).AddComponent<NpcHomesDemoDriver>();
            _instance._settings = JsonUtility.FromJson<Settings>(asset.text);
            Application.runInBackground = true;
            // Story 032: 선로에 hub2 단계의 거점이 역으로 없으면(POC 역 1곳) 건너뛰고 homes로 대신한다.
            if (Stage == "hub2" && !IsTrackStation(_instance._settings._hub2Id))
            {
                UnityEngine.Debug.LogWarning($"NPC DEMO: hub2 단계 건너뜀 — {_instance._settings._hub2Id}이(가) 선로의 역이 아닙니다(POC 역 1곳). homes 단계로 대신합니다.");
                Stage = "homes";
            }
            if (Stage == "arrival" || Stage == "hub2") InteriorNpcWalker.DemoArrivalSpeed = _instance._settings._arrivalSpeed;
            UnityEngine.Debug.Log("NPC DEMO: 단계 " + Stage);
        }

        /// <summary>Story 032: hubId가 현재 선로(ITrackRuntime)의 역이면 true. 선로 서비스가 없으면 판단하지 않고 true.</summary>
        private static bool IsTrackStation(string hubId)
        {
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            if (track == null) return true;
            foreach (ITrackNode node in track.Nodes) if (node.Kind == TrackNodeKind.Hub && node.Id == hubId) return true;
            return false;
        }

        void Update()
        {
            if (_done || _settings == null) return;
            if (Stage == "board") { UpdateBoard(); return; }
            if (Stage == "passengers") { UpdatePassengers(); return; }
            string hubId = Stage == "hub2" ? _settings._hub2Id : _settings._hubId;
            IViewMode view = GameServices.Get<IViewMode>();
            if (view == null) return;
            if (!view.InteriorOpen || view.InteriorHubId != hubId)
            {
                if (!_opened) { _opened = true; GameServices.Get<IInteriorNavigation>()?.Open(hubId); }
                return;
            }
            if (_waited++ < _settings._waitFrames) return;
            _done = true;
            try
            {
                if (Stage == "hub2") RunHub2(hubId);
                else if (Stage == "arrival") RunArrival(hubId);
                else Run(hubId);
            }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        /// <summary>board: depart, finish the waypoint encounter through the resolver, then report the boarded passenger.</summary>
        private void UpdateBoard()
        {
            if (_waited++ < _settings._waitFrames) return;
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            IWaypointResolver waypoints = GameServices.Get<IWaypointResolver>();
            Passengers passengers = NpcInstaller.Passengers;
            if (track == null || waypoints == null || passengers == null)
            {
                _done = true;
                UnityEngine.Debug.LogError("NPC DEMO: board 단계에 필요한 서비스가 없습니다(ITrackRuntime/IWaypointResolver/Passengers).");
                return;
            }
            if (!_departed) { _departed = true; track.Depart(); UnityEngine.Debug.Log("NPC DEMO: board 출발 " + track.CurrentNode?.Id); return; }
            if (waypoints.IsEncounterActive)
            {
                (waypoints as IronRing.Combat.WaypointResolver)?.Advance();
                return;
            }
            bool atWaypoint = track.CurrentNode != null && track.CurrentNode.Kind == TrackNodeKind.Waypoint;
            if (!atWaypoint && ++_boardFrames < _settings._boardMaxFrames) return;
            _done = true;
            string waypoint = track.CurrentNode?.Id ?? Passengers.DemoWaypoint;
            if (passengers.PassengerIds.Count == 0)
            {
                NpcDefinition pick = NpcInstaller.Pool.Definitions.FirstOrDefault(n => n.Passenger != null && (n.Passenger.Origin == waypoint || n.Passenger.Origin == "*"))
                    ?? NpcInstaller.Pool.Definitions.First(n => n.Passenger != null);
                passengers.TryBoard(pick.Id, waypoint, true);
                UnityEngine.Debug.Log($"NPC DEMO: board 굴림에서 아무도 타지 않아 {pick.Id}를 강제 탑승");
            }
            else UnityEngine.Debug.Log("NPC DEMO: board 굴림으로 탑승 " + string.Join(", ", passengers.PassengerIds));
            FirstNpcId = passengers.PassengerIds.FirstOrDefault();
            UnityEngine.Debug.Log($"NPC DEMO: board {FirstNpcId} at {waypoint} \"{passengers.Proposal(FirstNpcId)}\" {passengers.Hint(FirstNpcId)} (출신 {passengers.OriginName(FirstNpcId)})");
            Ready();
        }

        /// <summary>passengers: board the data passengers (force) without opening an interior, then StageReady (the HUD opens the Passengers screen).</summary>
        private void UpdatePassengers()
        {
            if (_waited++ < _settings._waitFrames) return;
            _done = true;
            IPassengers passengers = GameServices.Get<IPassengers>();
            if (passengers == null) { UnityEngine.Debug.LogError("NPC DEMO: passengers 단계에 IPassengers가 없습니다."); return; }
            foreach (string id in _settings._passengerIds ?? Array.Empty<string>())
                UnityEngine.Debug.Log($"NPC DEMO: passengers {id} 탑승 = {passengers.TryBoard(id, Passengers.DemoWaypoint, true)} \"{passengers.Proposal(id)}\" {passengers.Hint(id)} (출신 {passengers.OriginName(id)})");
            FirstNpcId = passengers.PassengerIds.FirstOrDefault();
            UnityEngine.Debug.Log($"NPC DEMO: passengers 탑승 {passengers.PassengerIds.Count}/{NpcInstaller.Pool?.MaxPassengers} [{string.Join(", ", passengers.PassengerIds)}]");
            Ready();
        }

        private void Run(string hubId)
        {
            if (!Services(out IHubInterior interior, out IItemPlacement placement, out IItemInventory items, out INpcRecruiter npcs,
                    out INpcHomes homes, out INpcInteraction interaction)) return;
            Place(hubId, interior, placement, items, _settings._placements);

            string[] targets = _settings._targetNpcIds ?? Array.Empty<string>();
            Passengers passengers = NpcInstaller.Passengers;
            if (passengers != null)
                foreach (string id in targets)
                    UnityEngine.Debug.Log($"NPC DEMO: 승객 {id} 탑승 = {passengers.TryBoard(id, Passengers.DemoWaypoint, true)}");
            int visits = 0;
            while (visits < _settings._maxVisits && !targets.All(id => npcs.NpcsAt(hubId).Contains(id)))
            {
                visits++;
                npcs.OnHubVisited(hubId);
            }
            IReadOnlyList<string> joined = npcs.NpcsAt(hubId);
            UnityEngine.Debug.Log($"NPC DEMO {hubId}: 방문 {visits}회, 합류 {string.Join(", ", joined)}");
            if (!targets.All(joined.Contains))
                UnityEngine.Debug.LogWarning($"NPC DEMO {hubId}: 방문 {_settings._maxVisits}회 안에 대상({string.Join(", ", targets)})이 모두 합류하지 않았습니다.");

            if (Stage == "crowd") JoinCrowd(hubId, npcs);
            foreach (NpcHome home in homes.HomesAt(hubId))
                UnityEngine.Debug.Log($"NPC DEMO: homes → {home.NpcId}:{home.HabitatTypeId}[{string.Join(",", home.Cells)}]");

            FirstNpcId = joined.FirstOrDefault(targets.Contains) ?? joined.FirstOrDefault();
            if (FirstNpcId != null)
                UnityEngine.Debug.Log($"NPC DEMO: 첫 NPC {FirstNpcId} '{interaction.DisplayName(FirstNpcId)}' 대사 \"{interaction.CurrentLine(FirstNpcId)}\" 출신 {interaction.Origin(FirstNpcId)}");

            Ready();
        }

        /// <summary>
        /// crowd(스토리 029): homes와 같이 합류시킨 뒤 `_crowdNpcIds`(같은 서식지 종류의 다른 거점 NPC)를 데모 강제로 이 거점에 합류시킨다
        /// (NpcRecruiter.TryJoin). 같은 종류 묶음이 하나뿐이라 NpcHomes 규칙대로 함께 살고, 한 건물 문 앞에 둘이 선다.
        /// </summary>
        private void JoinCrowd(string hubId, INpcRecruiter npcs)
        {
            var recruiter = npcs as NpcRecruiter;
            foreach (string id in _settings._crowdNpcIds ?? Array.Empty<string>())
            {
                bool ok = recruiter != null && recruiter.TryJoin(id, hubId);
                UnityEngine.Debug.Log($"NPC DEMO: crowd {id} → {hubId} 합류 = {ok}");
            }
        }

        /// <summary>arrival: complete the habitat, board the data NPC, dock (visit) so it disembarks and walks from the train car.</summary>
        private void RunArrival(string hubId)
        {
            if (!Services(out IHubInterior interior, out IItemPlacement placement, out IItemInventory items, out INpcRecruiter npcs, out _, out _)) return;
            Place(hubId, interior, placement, items, _settings._placements);
            Passengers passengers = NpcInstaller.Passengers;
            string id = passengers?.BoardForHub(hubId, _settings._arrivalNpcId);
            npcs.OnHubVisited(hubId);
            FirstNpcId = npcs.NpcsAt(hubId).Contains(id) ? id : null;
            UnityEngine.Debug.Log($"NPC DEMO: arrival {hubId} 하차 {FirstNpcId ?? "(없음)"} 걸음 속도 {_settings._arrivalSpeed}");
            Ready();
        }

        /// <summary>hub2: same habitat at hub-2; a hub-2 roster NPC disembarks (different from hub-1's).</summary>
        private void RunHub2(string hubId)
        {
            if (!Services(out IHubInterior interior, out IItemPlacement placement, out IItemInventory items, out INpcRecruiter npcs, out _, out _)) return;
            Placement[] list = (_settings._placements ?? Array.Empty<Placement>()).Take(Math.Max(0, _settings._hub2PlacementCount)).ToArray();
            Place(hubId, interior, placement, items, list);
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            string done = clusters == null ? "?" : string.Join(", ", clusters.CompletedAt(hubId).Select(c => c.TypeId));
            UnityEngine.Debug.Log($"NPC DEMO: hub2 {hubId} 완성 서식지 {done}");
            Passengers passengers = NpcInstaller.Passengers;
            string id = passengers?.BoardForHub(hubId, _settings._hub2NpcId);
            npcs.OnHubVisited(hubId);
            FirstNpcId = npcs.NpcsAt(hubId).Contains(id) ? id : null;
            string candidates = string.Join(", ", npcs.CandidatesOf(_settings._hub2TypeId));
            UnityEngine.Debug.Log($"NPC DEMO: hub2 {hubId} 하차 {FirstNpcId ?? "(없음)"} — {_settings._hub2TypeId} 후보 전체 [{candidates}], hub-1 쪽은 {_settings._arrivalNpcId}");
            if (FirstNpcId == null) UnityEngine.Debug.LogWarning($"NPC DEMO: hub2 {hubId}에서 아무도 내리지 않았습니다(배치 실패 또는 조건 불충족).");
            Ready();
        }

        private void Ready()
        {
            IsStageReady = true;
            _stageReady?.Invoke(FirstNpcId);
        }

        private static bool Services(out IHubInterior interior, out IItemPlacement placement, out IItemInventory items, out INpcRecruiter npcs,
            out INpcHomes homes, out INpcInteraction interaction)
        {
            interior = GameServices.Get<IHubInterior>();
            placement = GameServices.Get<IItemPlacement>();
            items = GameServices.Get<IItemInventory>();
            npcs = GameServices.Get<INpcRecruiter>();
            homes = GameServices.Get<INpcHomes>();
            interaction = GameServices.Get<INpcInteraction>();
            if (interior != null && placement != null && items != null && npcs != null && homes != null && interaction != null) return true;
            UnityEngine.Debug.LogError("NPC DEMO: 필요한 서비스가 없습니다(IHubInterior/IItemPlacement/IItemInventory/INpcRecruiter/INpcHomes/INpcInteraction).");
            return false;
        }

        /// <summary>데이터의 배치를 놓는다. 모자란 타일은 인벤토리에 넣고, 잔해 칸은 조사해 빈 터로 만든다(데모 준비).</summary>
        private static void Place(string hubId, IHubInterior interior, IItemPlacement placement, IItemInventory items, Placement[] list)
        {
            list = list ?? Array.Empty<Placement>();
            foreach (IGrouping<string, Placement> group in list.GroupBy(p => p._itemId))
            {
                int missing = group.Count() - items.Count(group.Key);
                if (missing > 0) items.Add(group.Key, missing);
            }
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hubId);
            foreach (Placement p in list)
            {
                IInteriorCell cell = cells.FirstOrDefault(c => c.IsUpper == p._upper && c.Row == p._row && c.Column == p._column);
                if (cell != null && cell.State == InteriorCellState.Rubble)
                {
                    if (!interior.CanInvestigate(hubId, cell.Index)) interior.OnDocked(hubId);
                    if (interior.CanInvestigate(hubId, cell.Index)) interior.Investigate(hubId, cell.Index, out _);
                }
                bool ok = cell != null && placement.TryPlace(hubId, cell.Index, p._itemId);
                UnityEngine.Debug.Log($"NPC DEMO: 배치 {hubId} {p._itemId} → {(p._upper ? "위" : "아래")}({p._row},{p._column}) 칸 {cell?.Index} = {ok}");
            }
        }

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

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class Settings
        {
            public string _hubId = "hub-1";
            public int _waitFrames = 20;
            public int _maxVisits = 24;
            public string[] _targetNpcIds;
            public Placement[] _placements;
            /// <summary>arrival·hub2: 캡처용 느린 도착 걸음 속도(월드 단위/초).</summary>
            public float _arrivalSpeed = 0.25f;
            public string _arrivalNpcId = "lamplighter";
            public string _hub2Id = "hub-2";
            public string _hub2NpcId = "gas_fitter";
            public string _hub2TypeId = "lamp_street";
            /// <summary>hub2에서 놓을 배치 수(_placements 앞부분, 가스등 L자 4칸).</summary>
            public int _hub2PlacementCount = 4;
            /// <summary>board: 경유지에 닿지 못해도 이 프레임 뒤에는 끝낸다.</summary>
            public int _boardMaxFrames = 1800;
            /// <summary>passengers(스토리 025): 강제로 태울 승객 id(제안이 있는 풀 NPC).</summary>
            public string[] _passengerIds = { "lamplighter", "gas_fitter" };
            /// <summary>crowd(스토리 029): hub-1에 강제로 더 합류시켜 한 건물에 함께 살게 할 NPC id.</summary>
            public string[] _crowdNpcIds = { "gas_fitter" };
        }

        [Serializable]
        private sealed class Placement
        {
            public string _itemId;
            public bool _upper;
            public int _row;
            public int _column;
        }
#pragma warning restore 0649
    }
}
