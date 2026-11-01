using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Presentation.Effects.Demo
{
    public sealed class EffectsDemo : MonoBehaviour, IMapAnchors
    {
        [Header("Authored map")]
        [SerializeField] private Camera _mapCamera;
        [SerializeField] private Transform[] _hubs;
        [SerializeField] private Transform _waypoint;
        [SerializeField] private Transform _train;
        [Header("Scenario")]
        [SerializeField] private int _stageFrames = 100;
        private DemoSystems _systems;
        private int _frame;
        private int _stage;
        private int _captureStage;
        public Transform TrainAnchor => _train;
        public Camera MapCamera => _mapCamera;

        void Awake() { GameServices.Reset(); }

        void Start()
        {
            _systems = new DemoSystems(_hubs.Length);
            GameServices.Register<IMapAnchors>(this);
            GameServices.Register<ITrackRuntime>(_systems);
            GameServices.Register<IResourceInventory>(_systems);
            GameServices.Register<IHabitatSystem>(_systems);
            GameServices.Register<INpcRecruiter>(_systems);
            GameServices.Register<IHubStateSystem>(_systems);
            GameServices.Register<IWaypointResolver>(_systems);
            GameServices.Register<IFacilityBuilder>(_systems);
            GameServices.Register<IEncounterPresentationSource>(_systems);
            string args = string.Join(" ", Environment.GetCommandLineArgs());
#if UNITY_EDITOR
            args += " " + UnityEditor.SessionState.GetString("IronRing.Bridge.Capture.ExtraArgs", "");
#endif
            if (args.Contains("--fx-stage=ruin")) _captureStage = 1;
            if (args.Contains("--fx-stage=facilities")) _captureStage = 2;
            if (args.Contains("--fx-stage=combat")) _captureStage = 3;
            if (args.Contains("--fx-stage=defeat")) _captureStage = 5;
            GameServices.MarkReady();
        }

        void Update()
        {
            _frame++;
            if (_captureStage > 0)
            {
                if (_frame == 5) while (_stage < _captureStage) Advance();
            }
            else if (_stage < 5 && _frame % _stageFrames == 0) Advance();
        }

        /// <summary>폐허, 시설과 NPC, 전투, 종료, 패배 순서로 페이크 이벤트를 진행하고 현재 단계를 변경한다.</summary>
        private void Advance()
        {
            _stage++;
            if (_stage == 1) _systems.ApplyDelta("hub-1", -100, "demo");
            if (_stage == 2)
            {
                _systems.TryBuild("hub-2", "wall"); _systems.TryBuild("hub-2", "turret"); _systems.TryBuild("hub-2", "workshop");
                _systems.OnHubVisited("hub-2"); _systems.ApplyDelta("hub-2", 35, "demo");
            }
            if (_stage == 3) { _systems.OnWaypointArrived(_systems.CurrentNode); _systems.ResolveRound(); }
            if (_stage == 4) _systems.FinishEncounter();
            if (_stage == 5) foreach (string hub in _systems.HubIds) _systems.ApplyDelta(hub, -100, "demo");
        }

        /// <summary>nodeId가 가리키는 경유지 또는 거점의 제작된 Transform을 반환한다.</summary>
        public Transform NodeAnchor(string nodeId) { return nodeId == "waypoint-north" ? _waypoint : HubAnchor(nodeId); }

        /// <summary>hubId에 해당하는 거점 Transform을 반환하고 일치하지 않으면 null을 반환한다.</summary>
        public Transform HubAnchor(string hubId)
        {
            for (int index = 0; index < _hubs.Length; index++) if (hubId == "hub-" + (index + 1)) return _hubs[index];
            return null;
        }

        private sealed class DemoNode : ITrackNode
        {
            public string Id => "waypoint-north";
            public int Index => 1;
            public TrackNodeKind Kind => TrackNodeKind.Waypoint;
            public string HubId => null;
        }

        private sealed class DemoSystems : ITrackRuntime, IResourceInventory, IHabitatSystem, INpcRecruiter, IHubStateSystem, IWaypointResolver, IFacilityBuilder, IEncounterPresentationSource
        {
            private readonly List<string> _ids = new List<string>();
            private readonly Dictionary<string, float> _prosperity = new Dictionary<string, float>();
            private readonly Dictionary<string, List<string>> _facilities = new Dictionary<string, List<string>>();
            private readonly Dictionary<string, List<string>> _npcs = new Dictionary<string, List<string>>();
            private readonly ITrackNode _node = new DemoNode();
            public IReadOnlyList<string> HubIds => _ids;
            public IReadOnlyList<ITrackNode> Nodes => new[] { _node };
            public ITrackNode CurrentNode => _node;
            public int Lap => 1;
            public bool IsHeld => IsEncounterActive;
            public bool IsRunLost { get; private set; }
            public bool IsEncounterActive { get; private set; }
            public bool IsCombat => true;
            public int TotalRounds => 3;
            public (int train, int enemy) Strengths => (10, 8);
            public event Action<int, bool> RoundResolved;
            public event Action<string> EventResolved { add { } remove { } }
            public event Action<string, float> ProsperityChanged;
            public event Action<string> HubRuined;
            public event Action RunLost;
            public event Action<string, string> FacilityBuilt;
            public event Action<string, string> NpcJoined;
            public event Action<ITrackNode> EncounterStarted;
            public event Action<ITrackNode, EncounterOutcome> EncounterEnded;
            public event Action<ITrackNode> Arrived { add { } remove { } }
            public event Action<int> LapCompleted { add { } remove { } }
            public event Action<string, int> Changed { add { } remove { } }
            public event Action<string> EventFlagRaised { add { } remove { } }

            /// <summary>count개의 거점 상태를 초기화하고 데모 전용 저장소를 만든다.</summary>
            public DemoSystems(int count)
            {
                for (int index = 1; index <= count; index++)
                {
                    string id = "hub-" + index; _ids.Add(id); _prosperity[id] = 60;
                    _facilities[id] = new List<string>(); _npcs[id] = new List<string>();
                }
            }
            /// <summary>hubId의 번영 값을 반환한다.</summary>
            public float GetProsperity(string hubId) { return _prosperity[hubId]; }
            /// <summary>hubId의 번영이 영인지 반환한다.</summary>
            public bool IsRuined(string hubId) { return _prosperity[hubId] <= 0; }
            /// <summary>hubId에 delta를 적용하고 번영, 폐허, 전체 패배 이벤트를 발생시킨다.</summary>
            public void ApplyDelta(string hubId, float delta, string reason)
            {
                if (IsRuined(hubId)) return;
                _prosperity[hubId] = Mathf.Clamp(_prosperity[hubId] + delta, 0, 100);
                ProsperityChanged?.Invoke(hubId, _prosperity[hubId]);
                if (IsRuined(hubId)) HubRuined?.Invoke(hubId);
                bool allRuined = true;
                foreach (string id in _ids) allRuined &= IsRuined(id);
                if (allRuined && !IsRunLost) { IsRunLost = true; RunLost?.Invoke(); }
            }
            /// <summary>hubId의 시설 목록을 반환한다.</summary>
            public IReadOnlyList<string> FacilitiesAt(string hubId) { return _facilities[hubId]; }
            /// <summary>hubId에 facilityId를 추가하고 건설 이벤트를 발생시킨 결과를 반환한다.</summary>
            public bool TryBuild(string hubId, string facilityId) { _facilities[hubId].Add(facilityId); FacilityBuilt?.Invoke(hubId, facilityId); return true; }
            /// <summary>데모에서 hubId의 facilityId 건설 허용 여부를 반환한다.</summary>
            public bool CanBuild(string hubId, string facilityId) { return !IsRuined(hubId); }
            /// <summary>hubId의 데모 전투 보너스 영을 반환한다.</summary>
            public float CombatBonus(string hubId) { return 0; }
            /// <summary>hubId의 NPC 목록을 반환한다.</summary>
            public IReadOnlyList<string> NpcsAt(string hubId) { return _npcs[hubId]; }
            /// <summary>hubId에 기술자를 합류시키고 NPC 이벤트를 발생시킨다.</summary>
            public void OnHubVisited(string hubId) { _npcs[hubId].Add("engineer"); NpcJoined?.Invoke(hubId, "engineer"); }
            /// <summary>node에서 전투 시작 이벤트를 발생시키고 조우를 활성화한다.</summary>
            public void OnWaypointArrived(ITrackNode node) { IsEncounterActive = true; EncounterStarted?.Invoke(node); }
            /// <summary>데모 첫 라운드의 열차 승리를 발생시켜 실제 연출 구독자를 갱신한다.</summary>
            public void ResolveRound() { RoundResolved?.Invoke(1, true); }
            /// <summary>현재 조우를 승리로 종료하고 종료 이벤트를 발생시킨다.</summary>
            public void FinishEncounter() { IsEncounterActive = false; EncounterEnded?.Invoke(_node, EncounterOutcome.Victory); }
            /// <summary>데모 출발 입력을 받으며 상태는 유지한다.</summary>
            public void Depart() { }
            /// <summary>reason 입력을 받으며 데모 정차 상태를 유지한다.</summary>
            public void RequestHold(string reason) { }
            /// <summary>reason 입력을 받으며 데모 정차 상태를 유지한다.</summary>
            public void ReleaseHold(string reason) { }
            /// <summary>lap 입력을 받으며 데모 번영을 유지한다.</summary>
            public void OnLapCompleted(int lap) { }
            /// <summary>resourceId의 데모 자원 수량 영을 반환한다.</summary>
            public int Get(string resourceId) { return 0; }
            /// <summary>resourceId와 amount를 받으며 데모 자원을 유지한다.</summary>
            public void Add(string resourceId, int amount) { }
            /// <summary>resourceId와 amount의 소비 요청에 데모 거절 결과를 반환한다.</summary>
            public bool TrySpend(string resourceId, int amount) { return false; }
            /// <summary>빈 데모 자원 사본을 반환한다.</summary>
            public IReadOnlyDictionary<string, int> Snapshot() { return new Dictionary<string, int>(); }
            /// <summary>hubId와 slotIndex의 자원 배치 요청을 거절한다.</summary>
            public bool TryPlace(string hubId, int slotIndex, string resourceId) { return false; }
            /// <summary>hubId와 slotIndex의 자원 제거 요청을 거절한다.</summary>
            public bool TryRemove(string hubId, int slotIndex) { return false; }
            /// <summary>hubId와 slotIndex의 빈 슬롯 상태를 반환한다.</summary>
            public string SlotContent(string hubId, int slotIndex) { return null; }
            /// <summary>hubId의 데모 서식지 미완성 상태를 반환한다.</summary>
            public bool IsHabitatComplete(string hubId) { return false; }
            /// <summary>hubId의 데모 이벤트 미설정 상태를 반환한다.</summary>
            public bool IsEventFlagSet(string hubId) { return false; }
        }
    }
}
