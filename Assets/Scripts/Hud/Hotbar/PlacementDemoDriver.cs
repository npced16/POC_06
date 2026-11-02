using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Presentation.Interior;

namespace IronRing.Hud.Hotbar
{
    /// <summary>
    /// 스토리 015 캡처용 스크립트 데모(--placement-demo, 단계 --placement-stage ghost|blocked|drag|placed|demolish|undo, 기본 placed).
    /// 스토리 031: demolish = placed와 같은 L자 네 칸(가스등 거리 완성)을 놓고 _demolishDelayFrames 뒤 PlacementGhost.TryDemolish로 철거,
    /// undo = L자 앞 두 칸을 놓고 PlacementGhost.TryUndo 한 번(두 번째 칸이 회수된다). 둘 다 플레이어 입력과 같은 고스트 경로를 탄다.
    /// 브리지 캡처 중에만 extraArgs를 읽는다(MainRoot의 --integration-demo와 같은 SessionState 방식). 내부 화면이 열리면 데이터(Hotbar.json _demo)의 칸으로:
    /// ghost = 슬롯 1 선택 + 빈 칸 위 고스트 고정(초록), blocked = 그 칸에 슬롯 1을 놓은 뒤 같은 칸 위 고스트(붉음),
    /// drag = 슬롯 _dragSlot 항목을 끌고 있는 상태로 빈 칸 위 고정, placed = 슬롯 1 항목을 L자 네 칸에 놓고 선택 해제.
    /// 실제 시스템 API(PlacementGhost → IItemPlacement)만 호출한다. 무제한 플래그(016) 전에는 수량이 0이면 데모 지급 로그를 남긴다.
    /// </summary>
    public sealed class PlacementDemoDriver : MonoBehaviour
    {
        private static PlacementDemoDriver _instance;
        private string _stage;
        private int _waited;
        private bool _done;
        private int _demolishIn = -1;
        private string _demolishHub;
        private int _demolishCell = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        private static void Create()
        {
            if (_instance != null) return;
            string[] args = Arguments();
            if (!args.Contains("--placement-demo")) return;
            _instance = new GameObject(nameof(PlacementDemoDriver)).AddComponent<PlacementDemoDriver>();
            _instance._stage = Argument(args, "--placement-stage") ?? "placed";
            Application.runInBackground = true;
        }

        void Update()
        {
            if (_demolishIn >= 0 && _demolishIn-- == 0) RunDemolish();
            if (_done) return;
            IViewMode view = GameServices.Get<IViewMode>();
            IHotbarDemoTarget hotbar = HotbarScreen.Instance; // 스토리 022
            PlacementGhost ghost = PlacementGhost.Instance;
            if (view == null || !view.InteriorOpen || hotbar == null || ghost == null) return;
            HotbarData.DemoData demo = HotbarData.Load().Demo;
            if (_waited++ < demo.WaitFrames) return;
            _done = true;
            try { Run(view.InteriorHubId, demo, hotbar, ghost); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        private void Run(string hubId, HotbarData.DemoData demo, IHotbarDemoTarget hotbar, PlacementGhost ghost)
        {
            IHubInterior interior = GameServices.Get<IHubInterior>();
            PlacementEntry first = hotbar.Entry(0);
            switch (_stage)
            {
                case "ghost":
                {
                    int cell = Find(interior, hubId, demo.GhostCell);
                    hotbar.SelectSlot(0);
                    ghost.Park(cell);
                    Log(hubId, cell, interior, ghost.CanPlace(hubId, cell, first));
                    break;
                }
                case "blocked":
                {
                    int cell = Find(interior, hubId, demo.BlockedCell);
                    Grant(first);
                    UnityEngine.Debug.Log($"PLACEMENT DEMO: 배치 {first} → 칸 {cell} = {ghost.TryPlace(hubId, cell, first)}");
                    hotbar.SelectSlot(0);
                    ghost.Park(cell);
                    Log(hubId, cell, interior, ghost.CanPlace(hubId, cell, first));
                    break;
                }
                case "drag":
                {
                    int cell = Find(interior, hubId, demo.DragCell);
                    PlacementEntry dragged = hotbar.Entry(demo.DragSlot);
                    Grant(dragged);
                    ghost.Park(cell);
                    Vector2? screen = ghost.CellScreenPosition(hubId, cell);
                    hotbar.BeginDemoDrag(demo.DragSlot, screen ?? new Vector2(Screen.width / 2f, Screen.height / 2f));
                    Log(hubId, cell, interior, ghost.CanPlace(hubId, cell, dragged));
                    break;
                }
                case "demolish":
                {
                    PlaceL(hubId, demo, interior, ghost, first);
                    hotbar.SelectSlot(-1);
                    _demolishHub = hubId;
                    _demolishCell = demo.LCells.Length > 0 ? Find(interior, hubId, demo.LCells[0]) : -1;
                    _demolishIn = demo.DemolishDelayFrames;
                    UnityEngine.Debug.Log($"PLACEMENT DEMO demolish: 건물 {ghost.BuildingAt(hubId, _demolishCell)?.TypeId ?? "(없음)"} 칸 {_demolishCell}, {_demolishIn}프레임 뒤 철거");
                    break;
                }
                case "undo":
                {
                    HotbarData.CellRef[] two = demo.LCells.Take(2).ToArray();
                    foreach (HotbarData.CellRef reference in two)
                    {
                        int cell = Find(interior, hubId, reference);
                        Grant(first);
                        UnityEngine.Debug.Log($"PLACEMENT DEMO: 배치 {first} → 칸 {cell} = {ghost.TryPlace(hubId, cell, first)}");
                    }
                    hotbar.SelectSlot(-1);
                    bool undone = ghost.TryUndo();
                    UnityEngine.Debug.Log($"PLACEMENT DEMO undo: 되돌림 {undone}, 남은 기록 {ghost.UndoCount}");
                    Log(hubId, -1, interior, false);
                    break;
                }
                default:
                {
                    foreach (HotbarData.CellRef reference in demo.LCells)
                    {
                        int cell = Find(interior, hubId, reference);
                        Grant(first);
                        UnityEngine.Debug.Log($"PLACEMENT DEMO: 배치 {first} → 칸 {cell} = {ghost.TryPlace(hubId, cell, first)}");
                    }
                    hotbar.SelectSlot(-1);
                    ghost.Park(-1);
                    Log(hubId, -1, interior, false);
                    break;
                }
            }
        }

        /// <summary>L자 칸들에 first를 놓는다(placed·demolish 단계).</summary>
        private static void PlaceL(string hubId, HotbarData.DemoData demo, IHubInterior interior, PlacementGhost ghost, PlacementEntry first)
        {
            foreach (HotbarData.CellRef reference in demo.LCells)
            {
                int cell = Find(interior, hubId, reference);
                Grant(first);
                UnityEngine.Debug.Log($"PLACEMENT DEMO: 배치 {first} → 칸 {cell} = {ghost.TryPlace(hubId, cell, first)}");
            }
        }

        /// <summary>스토리 031 demolish 단계의 철거(플레이어 우클릭과 같은 PlacementGhost.TryDemolish).</summary>
        private void RunDemolish()
        {
            PlacementGhost ghost = PlacementGhost.Instance;
            IHubInterior interior = GameServices.Get<IHubInterior>();
            if (ghost == null || interior == null) return;
            bool ok = ghost.TryDemolish(_demolishHub, _demolishCell);
            UnityEngine.Debug.Log($"PLACEMENT DEMO demolish: 철거 {ok}, 남은 기록 {ghost.UndoCount}");
            Log(_demolishHub, _demolishCell, interior, false);
        }

        /// <summary>016의 무제한 플래그 전에는 아이템 수량이 0이면 배치가 거부되므로 데모 한정으로 지급하고 로그를 남긴다.</summary>
        private static void Grant(PlacementEntry entry)
        {
            if (entry.Kind != PlacementKind.Item || !entry.IsValid) return;
            IItemInventory items = GameServices.Get<IItemInventory>();
            if (items == null || items.Count(entry.Id) > 0) return;
            items.Add(entry.Id, 4);
            UnityEngine.Debug.Log($"PLACEMENT DEMO: 데모 지급 {entry.Id} +4 (무제한 데이터 플래그 전)");
        }

        /// <summary>reference(구역·줄·칸)에 맞는 칸 색인, 없으면 0.</summary>
        private static int Find(IHubInterior interior, string hubId, HotbarData.CellRef reference)
        {
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hubId);
            foreach (IInteriorCell cell in cells)
                if (cell.IsUpper == reference.Upper && cell.Row == reference.Row && cell.Column == reference.Column) return cell.Index;
            UnityEngine.Debug.LogWarning($"PLACEMENT DEMO: 칸(위={reference.Upper}, 줄={reference.Row}, 칸={reference.Column})이 없어 0번을 씁니다.");
            return 0;
        }

        private void Log(string hubId, int cell, IHubInterior interior, bool canPlace)
        {
            string states = string.Join(",", interior.Cells(hubId).Select(c => c.State == InteriorCellState.ItemPlaced ? c.ItemId : c.State.ToString()));
            UnityEngine.Debug.Log($"PLACEMENT DEMO {_stage} {hubId}: cell={cell} canPlace={canPlace} cells=[{states}]");
        }

        private static string Argument(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>실행 인자와 진행 중인 브리지 캡처의 extraArgs를 합친다(캡처가 끝난 뒤의 값은 무시).</summary>
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
            GameServices.Ready -= Create;
            if (_instance == this) _instance = null;
        }
    }
}
