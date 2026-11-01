using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Hud.Demo
{
    /// <summary>Story011 전용 계약 페이크. 제작된 데모 씬에서만 등록되며 실제 게임 시스템과 분리된다.</summary>
    public sealed class HudDemoWorld : MonoBehaviour, ITrackRuntime, IResourceInventory, IHabitatSystem,
        INpcRecruiter, IHubStateSystem, IWaypointResolver, IFacilityBuilder, IHubClickSource,
        ICombatRoundCommand, IEncounterPresentationSource
    {
        [Header("데모 초기 상태")]
        [SerializeField] private int _initialStock = 24;
        [SerializeField] private float _initialProsperity = 70;
        [SerializeField] private int _initialLap = 3;
        [SerializeField] private int _buildCost = 4;
        [SerializeField] private int _roundCount = 3;
        [Header("데모 씬 참조")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform[] _hubMarkers;
        [SerializeField] private Transform _train;
        private HudCatalog _catalog;
        private readonly List<ITrackNode> _nodes = new List<ITrackNode>();
        private readonly List<string> _hubIds = new List<string>();
        private readonly Dictionary<string, int> _stock = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _slots = new Dictionary<string, string>();
        private readonly Dictionary<string, List<string>> _built = new Dictionary<string, List<string>>();
        private readonly HashSet<string> _ruins = new HashSet<string>();
        private int _round;
        public IReadOnlyList<ITrackNode> Nodes => _nodes;
        public ITrackNode CurrentNode { get; private set; }
        public int Lap { get; private set; }
        public bool IsHeld { get; private set; }
        public IReadOnlyList<string> HubIds => _hubIds;
        public bool IsRunLost => _ruins.Count == _hubIds.Count;
        public bool IsEncounterActive { get; private set; }
        public bool IsCombat => IsEncounterActive;
        public bool CanAdvance => IsEncounterActive && !IsRunLost;
        public int TotalRounds => _roundCount;
        public (int train, int enemy) Strengths => (0, 0);
        public event Action<ITrackNode> Arrived;
        public event Action<int> LapCompleted;
        public event Action<string, int> Changed;
        public event Action<string> EventFlagRaised;
        public event Action<string, string> NpcJoined;
        public event Action<string, float> ProsperityChanged;
        public event Action<string> HubRuined;
        public event Action RunLost;
        public event Action<ITrackNode> EncounterStarted;
        public event Action<ITrackNode, EncounterOutcome> EncounterEnded;
        public event Action<string, string> FacilityBuilt;
        public event Action<string> HubClicked;
        public event Action<int, bool> RoundResolved;
        public event Action<string> EventResolved;

        void Awake()
        {
            GameServices.Reset();
            _catalog = JsonUtility.FromJson<HudCatalog>(Resources.Load<TextAsset>("Hud/HudCatalog").text);
            foreach (string id in _catalog.Resources) _stock[id] = _initialStock;
            foreach (HudCatalog.HubEntry hub in _catalog.Hubs)
            {
                _hubIds.Add(hub.Id); _nodes.Add(new DemoNode(hub.Id, _nodes.Count));
                _built[hub.Id] = new List<string>();
            }
            CurrentNode = _nodes[0]; Lap = _initialLap; IsHeld = true;
            GameServices.Register<ITrackRuntime>(this); GameServices.Register<IResourceInventory>(this);
            GameServices.Register<IHabitatSystem>(this); GameServices.Register<INpcRecruiter>(this);
            GameServices.Register<IHubStateSystem>(this); GameServices.Register<IWaypointResolver>(this);
            GameServices.Register<IFacilityBuilder>(this); GameServices.Register<IHubClickSource>(this);
            GameServices.Register<ICombatRoundCommand>(this); GameServices.Register<IEncounterPresentationSource>(this);
        }

        void Start()
        {
            GameServices.MarkReady();
            string args = string.Join(" ", Environment.GetCommandLineArgs());
#if UNITY_EDITOR
            args += " " + UnityEditor.SessionState.GetString("IronRing.Bridge.Capture.ExtraArgs", "");
#endif
            if (args.Contains("--hud-demo-panel")) HubClicked?.Invoke(_hubIds[0]);
            if (args.Contains("--hud-demo-defeat")) Lose();
            if (args.Contains("--hud-demo-combat")) OnWaypointArrived(CurrentNode);
        }

        void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Vector2 screen = Mouse.current.position.ReadValue();
            UIDocument document = FindFirstObjectByType<UIDocument>();
            if (document != null && document.rootVisualElement.panel != null)
            {
                Vector2 point = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel, new Vector2(screen.x, Screen.height - screen.y));
                VisualElement hit = document.rootVisualElement.panel.Pick(point);
                if (hit != null) return;
            }
            Vector3 world = _camera.ScreenToWorldPoint(screen);
            Collider2D target = Physics2D.OverlapPoint(world);
            if (target == null) return;
            for (int i = 0; i < _hubMarkers.Length; i++)
                if (target.transform == _hubMarkers[i])
                {
                    Debug.Log("STORY011 MOUSE HUB " + _hubIds[i]); HubClicked?.Invoke(_hubIds[i]); return;
                }
        }

        /// <summary>생성 도구에서 camera, hubs, train 참조를 받아 씬에 저장할 편집 설정을 지정한다.</summary>
        public void Configure(Camera camera, Transform[] hubs, Transform train) { _camera = camera; _hubMarkers = hubs; _train = train; }
        /// <summary>resourceId에 저장된 데모 수량을 반환한다.</summary>
        public int Get(string resourceId) => _stock.TryGetValue(resourceId, out int amount) ? amount : 0;
        /// <summary>resourceId와 amount로 데모 재고를 늘리고 변경 이벤트를 발행한다.</summary>
        public void Add(string resourceId, int amount) { _stock[resourceId] = Get(resourceId) + amount; Changed?.Invoke(resourceId, Get(resourceId)); }
        /// <summary>resourceId에서 amount를 소비하고 성공 여부 및 변경 이벤트를 반환한다.</summary>
        public bool TrySpend(string resourceId, int amount)
        {
            if (amount < 0 || Get(resourceId) < amount) return false;
            Add(resourceId, -amount); return true;
        }
        /// <summary>현재 재고를 복사해 읽기 전용 스냅샷으로 반환한다.</summary>
        public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_stock);
        /// <summary>hubId, slotIndex, resourceId를 검증해 데모 슬롯에 배치하고 성공 여부를 반환한다.</summary>
        public bool TryPlace(string hubId, int slotIndex, string resourceId)
        {
            if (IsRuined(hubId) || slotIndex < 0 || slotIndex >= _catalog.SlotCount(hubId) || SlotContent(hubId, slotIndex) != null) return false;
            if (!TrySpend(resourceId, 1)) return false;
            _slots[hubId + ":" + slotIndex] = resourceId;
            EventFlagRaised?.Invoke(hubId); Debug.Log("STORY011 PLACE " + hubId + " " + slotIndex); return true;
        }
        /// <summary>hubId와 slotIndex에 배치한 자원을 재고로 회수하고 성공 여부를 반환한다.</summary>
        public bool TryRemove(string hubId, int slotIndex)
        {
            string resource = SlotContent(hubId, slotIndex); if (resource == null) return false;
            _slots.Remove(hubId + ":" + slotIndex); Add(resource, 1);
            Debug.Log("STORY011 REMOVE " + hubId + " " + slotIndex); return true;
        }
        /// <summary>hubId와 slotIndex에서 배치된 자원 식별자 또는 null을 반환한다.</summary>
        public string SlotContent(string hubId, int slotIndex) => _slots.TryGetValue(hubId + ":" + slotIndex, out string resource) ? resource : null;
        /// <summary>hubId의 모든 데모 슬롯이 채워졌는지 반환한다.</summary>
        public bool IsHabitatComplete(string hubId)
        {
            for (int i = 0; i < _catalog.SlotCount(hubId); i++) if (SlotContent(hubId, i) == null) return false;
            return true;
        }
        /// <summary>hubId의 데모 서식지 완성 여부를 이벤트 플래그로 반환한다.</summary>
        public bool IsEventFlagSet(string hubId) => IsHabitatComplete(hubId);
        /// <summary>hubId 방문을 받아 데모 동료 이벤트를 발행한다.</summary>
        public void OnHubVisited(string hubId) { NpcJoined?.Invoke(hubId, "npc_engineer"); }
        /// <summary>hubId의 데모 동료 목록을 빈 목록으로 반환한다.</summary>
        public IReadOnlyList<string> NpcsAt(string hubId) => Array.Empty<string>();
        /// <summary>hubId 폐허 여부에 따라 초기 번영 또는 0을 반환한다.</summary>
        public float GetProsperity(string hubId) => IsRuined(hubId) ? 0 : _initialProsperity;
        /// <summary>hubId가 데모 폐허 집합에 속하는지 반환한다.</summary>
        public bool IsRuined(string hubId) => _ruins.Contains(hubId);
        /// <summary>hubId, delta, reason을 받아 번영이 0 이하가 되면 데모 폐허와 패배 이벤트를 발행한다.</summary>
        public void ApplyDelta(string hubId, float delta, string reason)
        {
            if (GetProsperity(hubId) + delta > 0 || !_ruins.Add(hubId)) return;
            ProsperityChanged?.Invoke(hubId, 0); HubRuined?.Invoke(hubId); if (IsRunLost) RunLost?.Invoke();
        }
        /// <summary>lap 값을 데모 순환 수에 저장하고 순환 이벤트를 발행한다.</summary>
        public void OnLapCompleted(int lap) { Lap = lap; LapCompleted?.Invoke(lap); }
        /// <summary>출발 입력으로 다음 거점에 데모 열차를 이동하고 도착 이벤트를 발행한다.</summary>
        public void Depart()
        {
            int index = (CurrentNode.Index + 1) % _nodes.Count; CurrentNode = _nodes[index];
            _train.position = _hubMarkers[index].position;
            if (index == 0) OnLapCompleted(Lap + 1);
            Arrived?.Invoke(CurrentNode); Debug.Log("STORY011 DEPART " + CurrentNode.Id);
        }
        /// <summary>reason 정차 요청을 받아 데모 정차 상태를 활성화한다.</summary>
        public void RequestHold(string reason) { IsHeld = true; }
        /// <summary>reason 해제 요청을 받아 데모 정차 상태를 해제한다.</summary>
        public void ReleaseHold(string reason) { IsHeld = false; }
        /// <summary>node를 받아 데모 전투를 시작하고 시작 이벤트를 발행한다.</summary>
        public void OnWaypointArrived(ITrackNode node) { IsEncounterActive = true; _round = 0; EncounterStarted?.Invoke(node); }
        /// <summary>활성 데모 전투의 라운드를 하나 진행하고 마지막 라운드에서 승리 이벤트를 발행한다.</summary>
        public void AdvanceCombatRound()
        {
            if (!IsEncounterActive) return;
            _round++; RoundResolved?.Invoke(_round, true); Debug.Log("STORY011 ROUND " + _round);
            if (_round >= _roundCount) { IsEncounterActive = false; EncounterEnded?.Invoke(CurrentNode, EncounterOutcome.Victory); }
        }
        /// <summary>hubId에 건설된 데모 시설 식별자 목록을 반환한다.</summary>
        public IReadOnlyList<string> FacilitiesAt(string hubId) => _built[hubId];
        /// <summary>hubId와 facilityId를 받아 폐허, 중복 및 데모 비용 조건 충족 여부를 반환한다.</summary>
        public bool CanBuild(string hubId, string facilityId) => !IsRuined(hubId) && !_built[hubId].Contains(facilityId) && Get(_catalog.Resources[0]) >= _buildCost;
        /// <summary>hubId와 facilityId로 데모 시설을 건설하고 비용 소비와 이벤트 이후 성공 여부를 반환한다.</summary>
        public bool TryBuild(string hubId, string facilityId)
        {
            if (!CanBuild(hubId, facilityId) || !TrySpend(_catalog.Resources[0], _buildCost)) return false;
            _built[hubId].Add(facilityId); FacilityBuilt?.Invoke(hubId, facilityId);
            Debug.Log("STORY011 BUILD " + hubId + " " + facilityId); return true;
        }
        /// <summary>hubId를 받아 데모의 전투 보너스 0을 반환한다.</summary>
        public float CombatBonus(string hubId) => 0;
        /// <summary>모든 제작 거점을 폐허로 전환하여 패배 데모 이벤트를 발행한다.</summary>
        private void Lose() { foreach (string hub in _hubIds) ApplyDelta(hub, -_initialProsperity, nameof(Lose)); }

        private sealed class DemoNode : ITrackNode
        {
            public string Id { get; }
            public int Index { get; }
            public TrackNodeKind Kind => TrackNodeKind.Hub;
            public string HubId => Id;
            /// <summary>id와 index를 받아 변경되지 않는 데모 노드 정보를 저장한다.</summary>
            public DemoNode(string id, int index) { Id = id; Index = index; }
        }
    }
}
