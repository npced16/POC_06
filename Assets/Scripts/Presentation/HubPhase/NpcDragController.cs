using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Npc;
using IronRing.Presentation.Interior;

namespace IronRing.Presentation.HubPhase
{
    /// <summary>
    /// 스펙 005. 마을 NPC 끌어 옮기기의 드래그 상태와 놓기를 맡는다.
    /// 놓을 곳은 정차한 열차 범위와 자기 서식지 종류의 완성 묶음이며, 실제 상태 변경은 IPassengers와 INpcHomes에 맡긴다.
    /// </summary>
    public sealed class NpcDragController : MonoBehaviour
    {
        private const int DRAG_SORTING_BOOST = 100;

        private static NpcDragController _instance;
        private static int _endedFrame = -1;

        private enum DropTarget { None, Train, Home }

        private HubPhaseViewSettings _settings;
        private IViewMode _view;
        private IInteriorAnchors _anchors;
        private IPhaseLoop _loop;
        private ITrackRuntime _track;
        private IPassengers _passengers;
        private INpcHomes _homes;
        private IHabitatClusters _clusters;
        private string _dragNpcId;
        private string _dragHub;
        private SpriteRenderer _body;
        private Color _bodyColor;
        private int _bodySorting;
        private Vector3 _startPosition;
        private bool _fromPassengers;

        /// <summary>승객 줄 끌기가 놓기나 취소로 끝났을 때. 인자는 승객을 건물에 내렸는지.</summary>
        public event Action<bool> PassengerDragEnded;

        /// <summary>드래그 컨트롤러. 조립 전이면 null.</summary>
        public static NpcDragController Instance => _instance;

        /// <summary>지금 끌고 있는 NPC id. 없으면 null.</summary>
        public static string DraggingNpc => _instance != null ? _instance._dragNpcId : null;

        /// <summary>드래그 중이거나 이번 프레임에 끝났으면 true. 같은 입력이 철거·메뉴로 이어지지 않게 다른 입력이 확인한다.</summary>
        public static bool IsInputHeld => DraggingNpc != null || _endedFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; _endedFrame = -1; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>내부 화면과 승객 서비스가 있으면 컨트롤러를 한 번 만든다. _instance를 채운다.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            IViewMode view = GameServices.Get<IViewMode>();
            IInteriorAnchors anchors = GameServices.Get<IInteriorAnchors>();
            IPassengers passengers = GameServices.Get<IPassengers>();
            if (view == null || anchors == null || passengers == null) return;
            _instance = new GameObject(nameof(NpcDragController)).AddComponent<NpcDragController>();
            _instance._settings = HubPhaseViewSettings.Load();
            _instance._view = view;
            _instance._anchors = anchors;
            _instance._passengers = passengers;
            _instance._track = GameServices.Get<ITrackRuntime>();
        }

        /// <summary>
        /// hubId 내부가 열려 있고 열차가 그 마을에 정차해 출발할 수 있으면 true를 반환한다.
        /// 늦게 등록되는 서비스는 여기서 얻는다.
        /// </summary>
        public bool CanDrag(string hubId)
        {
            if (_loop == null) _loop = GameServices.Get<IPhaseLoop>();
            return hubId != null && _view.InteriorOpen && _view.InteriorHubId == hubId
                && _loop != null && _loop.CanDepart && _track?.CurrentNode?.HubId == hubId;
        }

        /// <summary>hubId의 npcId를 몸 스프라이트 body로 끌기 시작한다. 드래그 상태와 그림의 앞쪽 표시를 켠다.</summary>
        public void BeginField(string hubId, string npcId, SpriteRenderer body)
        {
            _fromPassengers = false;
            StartDrag(hubId, npcId, body);
        }

        /// <summary>
        /// 승객 npcId를 지금 열린 내부 화면에서 끌기 시작한다. 끌리는 그림을 NPC 프리팹으로 만들어 끝날 때 없앤다.
        /// 끌 수 없으면 아무것도 바꾸지 않고 false를 반환한다.
        /// </summary>
        public bool BeginPassenger(string npcId)
        {
            string hubId = _view.InteriorHubId;
            if (_dragNpcId != null || !CanDrag(hubId)) return false;
            HubPhasePrefabCatalog catalog = HubPhasePrefabCatalog.Load();
            SpriteRenderer body = HubPhaseSprites.Spawn(catalog.Npc, transform, Vector3.zero, "npc-drag-" + npcId);
            HubPhaseSprites.FitWidth(body, HubPhaseSprites.CellSpacing(_anchors, hubId) * _settings.NpcCellFraction);
            body.sortingOrder = _settings.NpcSortingOrder;
            _fromPassengers = true;
            StartDrag(hubId, npcId, body);
            return true;
        }

        /// <summary>hubId의 npcId를 body로 끌기 시작한다. 드래그 상태를 채우고 그림을 앞쪽에 표시한다.</summary>
        private void StartDrag(string hubId, string npcId, SpriteRenderer body)
        {
            _dragNpcId = npcId;
            _dragHub = hubId;
            _body = body;
            _bodyColor = body.color;
            _bodySorting = body.sortingOrder;
            _startPosition = body.transform.position;
            body.sortingOrder = _bodySorting + DRAG_SORTING_BOOST;
            UnityEngine.Debug.Log($"NPC DRAG: begin {npcId} at {hubId}");
        }

        void Update()
        {
            if (_dragNpcId == null) return;
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool cancel = mouse == null || _body == null || !CanDrag(_dragHub) || mouse.rightButton.wasPressedThisFrame
                || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
            if (cancel) { Finish(true); return; }
            Vector2 pointer = mouse.position.ReadValue();
            DropTarget target = FindTarget(pointer, out Vector3 world, out IReadOnlyList<int> cells);
            _body.transform.position = new Vector3(world.x, world.y, _startPosition.z);
            _body.color = target != DropTarget.None ? _settings.NpcDragValidColor : _settings.NpcDragInvalidColor;
            if (!mouse.leftButton.isPressed) Drop(target, cells);
        }

        /// <summary>
        /// pointer 아래의 놓을 곳을 찾는다. 자기 종류 완성 묶음이면 Home과 그 칸을 cells에, 열차 범위면 Train을 돌려준다.
        /// world에는 pointer의 월드 좌표를 담는다.
        /// </summary>
        private DropTarget FindTarget(Vector2 pointer, out Vector3 world, out IReadOnlyList<int> cells)
        {
            cells = null;
            int cell = PlacementGhost.Instance.CellAt(pointer, out world);
            string typeId = NpcInstaller.Pool?.Find(_dragNpcId)?.HabitatTypeId;
            if (_clusters == null) _clusters = GameServices.Get<IHabitatClusters>();
            if (cell >= 0 && typeId != null && _clusters != null)
                foreach (HabitatCluster cluster in _clusters.CompletedAt(_dragHub))
                {
                    if (cluster?.Cells == null || cluster.TypeId != typeId || !Contains(cluster.Cells, cell)) continue;
                    cells = cluster.Cells;
                    return DropTarget.Home;
                }
            return !_fromPassengers && IsOverTrain(world) ? DropTarget.Train : DropTarget.None;
        }

        /// <summary>world가 _dragHub 열차 기준점 둘레의 놓기 범위(hub_phase_view.json, 칸 단위) 안이면 true를 반환한다.</summary>
        private bool IsOverTrain(Vector3 world)
        {
            Transform train = _anchors.TrainAnchor(_dragHub);
            if (train == null) return false;
            float cell = HubPhaseSprites.CellSpacing(_anchors, _dragHub);
            Vector3 delta = world - train.position;
            return Mathf.Abs(delta.x) <= _settings.NpcDragTrainWidthCells * cell * 0.5f
                && Mathf.Abs(delta.y) <= _settings.NpcDragTrainHeightCells * cell * 0.5f;
        }

        /// <summary>target에 맞게 상태를 바꾼다. 실패하면 이유를 알리고 그림을 시작 위치로 돌린다.</summary>
        private void Drop(DropTarget target, IReadOnlyList<int> cells)
        {
            string npcId = _dragNpcId;
            string hubId = _dragHub;
            bool fromPassengers = _fromPassengers;
            if (_homes == null) _homes = GameServices.Get<INpcHomes>();
            bool done = false;
            string failure = null;
            switch (target)
            {
                case DropTarget.Train:
                    Finish(false);
                    done = _passengers.TryBoardResident(npcId, hubId);
                    if (!done) failure = Strings.Get("npc_drag_train_full");
                    break;
                case DropTarget.Home:
                    done = (!fromPassengers || _passengers.TryPlace(npcId, hubId)) && _homes.TryMoveHome(npcId, cells);
                    Finish(false, done);
                    if (!done) failure = WrongHomeText(npcId);
                    break;
                default:
                    failure = WrongHomeText(npcId);
                    break;
            }
            UnityEngine.Debug.Log($"NPC DRAG: drop {npcId} target={target} done={done}");
            if (done) return;
            if (_dragNpcId != null) Finish(true);
            else if (_body != null) _body.transform.position = _startPosition;
            ToastScreen.ShowNow(failure, _settings.NpcDragToastSeconds);
        }

        /// <summary>
        /// 드래그를 끝내고 그림의 색과 표시 순서를 되돌린다. returnToStart면 시작 위치로 옮긴다.
        /// 승객 끌기였다면 만든 그림을 없애고 placed를 담아 PassengerDragEnded를 낸다.
        /// </summary>
        private void Finish(bool returnToStart, bool placed = false)
        {
            if (_body != null)
            {
                _body.color = _bodyColor;
                _body.sortingOrder = _bodySorting;
                if (returnToStart) _body.transform.position = _startPosition;
            }
            _dragNpcId = null;
            _endedFrame = Time.frameCount;
            if (!_fromPassengers) return;
            _fromPassengers = false;
            if (_body != null) Destroy(_body.gameObject);
            PassengerDragEnded?.Invoke(placed);
        }

        /// <summary>npcId가 살 수 있는 서식지 이름을 넣은 안내 문구를 반환한다.</summary>
        private static string WrongHomeText(string npcId)
        {
            string typeId = NpcInstaller.Pool?.Find(npcId)?.HabitatTypeId;
            return string.Format(Strings.Get("npc_drag_wrong_home"), HabitatsInstaller.Catalog?.Get(typeId)?.Name ?? typeId);
        }

        /// <summary>cells에 cell이 있으면 true를 반환한다.</summary>
        private static bool Contains(IReadOnlyList<int> cells, int cell)
        {
            foreach (int each in cells) if (each == cell) return true;
            return false;
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_instance == this) _instance = null;
        }
    }
}
