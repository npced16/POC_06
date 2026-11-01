using System;
using System.Collections.Generic;
using System.Linq;

using IronRing.Contracts;

namespace IronRing.Presentation.Interior
{
    /// <summary>되돌리기 기록 하나의 종류(스토리 031).</summary>
    public enum PlacementActionKind
    {
        /// <summary>타일 하나를 놓았다 → 되돌리면 회수.</summary>
        Place,
        /// <summary>타일 하나를 회수했다 → 되돌리면 다시 놓기.</summary>
        PickUp,
        /// <summary>건물(완성 서식지 묶음)을 철거했다 → 되돌리면 모든 타일을 다시 놓기.</summary>
        Demolish
    }

    /// <summary>되돌리기 기록 한 건. Tiles는 (칸, 아이템 id) 목록이다(배치·회수는 한 칸, 철거는 묶음 전체).</summary>
    public sealed class PlacementAction
    {
        /// <summary>동작 종류.</summary>
        public PlacementActionKind Kind;
        /// <summary>동작이 일어난 거점 id.</summary>
        public string HubId;
        /// <summary>철거일 때 허문 서식지 종류 id(그 밖에는 null).</summary>
        public string TypeId;
        /// <summary>영향을 받은 칸과 그 칸의 아이템 id.</summary>
        public IReadOnlyList<KeyValuePair<int, string>> Tiles;
    }

    /// <summary>
    /// 스토리 031 A. 플레이어가 PlacementGhost로 한 배치·회수·철거를 최근 것부터 되돌리는 기록(최대 depth건, InteriorCatalog._undoDepth).
    /// 되돌리기는 역동작을 IItemPlacement로 보내므로 서식지 판정·건물·번영·NPC 집이 일반 배치와 같은 경로로 다시 계산된다.
    /// 역동작 자체는 기록하지 않는다(다시 하기는 범위 밖). 내부 화면이 닫히면(거점을 떠나면) PlacementGhost가 Clear한다.
    /// </summary>
    public sealed class PlacementHistory
    {
        private readonly IItemPlacement _placement;
        private readonly IHubInterior _interior;
        private readonly int _depth;
        private readonly LinkedList<PlacementAction> _actions = new LinkedList<PlacementAction>();

        /// <summary>placement·interior는 역동작과 칸 확인에 쓴다. depth가 1보다 작으면 1.</summary>
        public PlacementHistory(IItemPlacement placement, IHubInterior interior, int depth)
        {
            _placement = placement ?? throw new ArgumentNullException(nameof(placement));
            _interior = interior;
            _depth = Math.Max(1, depth);
        }

        /// <summary>남아 있는 기록 수.</summary>
        public int Count => _actions.Count;

        /// <summary>action을 맨 위에 쌓는다. 한도를 넘으면 가장 오래된 기록을 버린다.</summary>
        public void Record(PlacementAction action)
        {
            if (action?.Tiles == null || action.Tiles.Count == 0 || action.HubId == null) return;
            _actions.AddLast(action);
            while (_actions.Count > _depth) _actions.RemoveFirst();
        }

        /// <summary>기록을 모두 지운다.</summary>
        public void Clear() => _actions.Clear();

        /// <summary>
        /// 가장 최근 기록을 꺼내 역동작을 적용한다. 적용한 칸이 하나라도 있으면 true와 그 기록을 돌려준다.
        /// 칸 상태가 기록과 달라졌으면(다른 경로로 바뀜) 그 칸은 건너뛴다. 기록이 없으면 false.
        /// </summary>
        public bool TryUndo(out PlacementAction undone)
        {
            undone = null;
            if (_actions.Count == 0) return false;
            PlacementAction action = _actions.Last.Value;
            _actions.RemoveLast();
            int applied = 0;
            foreach (KeyValuePair<int, string> tile in action.Tiles)
            {
                if (action.Kind == PlacementActionKind.Place)
                {
                    IInteriorCell cell = Cell(action.HubId, tile.Key);
                    if (cell != null && cell.State == InteriorCellState.ItemPlaced && cell.ItemId == tile.Value
                        && _placement.TryPickUp(action.HubId, tile.Key)) applied++;
                }
                else if (_placement.TryPlace(action.HubId, tile.Key, tile.Value)) applied++;
            }
            UnityEngine.Debug.Log($"UNDO: {action.Kind} {action.HubId} {action.TypeId ?? ""} 칸 {string.Join(",", action.Tiles.Select(t => t.Key + "=" + t.Value))} 적용 {applied}/{action.Tiles.Count} 남은 기록 {_actions.Count}");
            if (applied == 0) return false;
            undone = action;
            return true;
        }

        private IInteriorCell Cell(string hubId, int cellIndex)
        {
            if (_interior == null) return null;
            IReadOnlyList<IInteriorCell> cells;
            try { cells = _interior.Cells(hubId); }
            catch (ArgumentException) { return null; }
            return cells != null && cellIndex >= 0 && cellIndex < cells.Count ? cells[cellIndex] : null;
        }
    }
}
