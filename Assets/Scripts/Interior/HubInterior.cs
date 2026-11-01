using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using IronRing.Contracts;

namespace IronRing.Interior
{
    /// <summary>
    /// 조사 한 건의 전체 결과를 알리는 보고 계약. IHubInterior.CellChanged는 칸만 알리므로 HUD가 발견한 것(아이템·자원 id)을
    /// 한국어 문구로 보여 주려면 이 이벤트를 쓴다. HubInterior가 구현하고 Bootstrap이 등록한다.
    /// </summary>
    public interface IInvestigationReport
    {
        /// <summary>hubId, cellIndex, 결과, 발견한 아이템/자원 id(없으면 null).</summary>
        event Action<string, int, InvestigationResult, string> Investigated;
    }

    /// <summary>
    /// 스토리 012. 거점 내부 칸과 조사 규칙의 순수 C# 구현(design/game-brief.md Core loop 2단계).
    /// 격자·횟수·확률은 InteriorData에서, 무작위는 주입된 IRandomSource에서 와서 같은 시드면 같은 결과가 난다.
    /// 묻힌 자원은 IResourceInventory에, 묻힌 아이템은 IItemInventory(스토리 013, null이면 보고만)에 들어간다.
    /// </summary>
    public sealed class HubInterior : IHubInterior, IInvestigationReport, IInteriorFacilityCells
    {
        private readonly InteriorData _data;
        private readonly IResourceInventory _resources;
        private IItemInventory _items;
        private readonly IRandomSource _random;
        private readonly Dictionary<string, InteriorCell[]> _cells = new Dictionary<string, InteriorCell[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReadOnlyCollection<IInteriorCell>> _views = new Dictionary<string, ReadOnlyCollection<IInteriorCell>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _left = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly ReadOnlyCollection<string> _hubIds;

        /// <summary>hubId, cellIndex.</summary>
        public event Action<string, int> CellChanged;
        /// <summary>hubId, 남은 횟수.</summary>
        public event Action<string, int> InvestigationsChanged;
        /// <summary>hubId, cellIndex, 결과, 발견 id.</summary>
        public event Action<string, int, InvestigationResult, string> Investigated;

        /// <summary>내부가 있는 거점 id 목록(생성 순서).</summary>
        public IReadOnlyList<string> HubIds => _hubIds;
        /// <summary>이 내부가 따르는 제작 데이터.</summary>
        public InteriorData Data => _data;

        /// <summary>
        /// data와 hubIds로 거점마다 칸을 만들고 random으로 시설 부지 최소 칸을 고정 배치한다.
        /// items는 스토리 013이 들어오기 전까지 null일 수 있다.
        /// </summary>
        public HubInterior(InteriorData data, IEnumerable<string> hubIds, IResourceInventory resources, IItemInventory items, IRandomSource random)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (hubIds == null) throw new ArgumentNullException(nameof(hubIds));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _items = items;
            var ids = new List<string>();
            foreach (string hubId in hubIds)
            {
                if (string.IsNullOrWhiteSpace(hubId) || _cells.ContainsKey(hubId)) throw new ArgumentException("Hub ids must be unique and non-blank.", nameof(hubIds));
                InteriorCell[] cells = CreateCells();
                PlaceGuaranteedPlots(cells);
                _cells.Add(hubId, cells);
                _views.Add(hubId, new ReadOnlyCollection<IInteriorCell>(cells));
                _left.Add(hubId, 0);
                ids.Add(hubId);
            }
            _hubIds = ids.AsReadOnly();
        }

        /// <summary>
        /// 스토리 013의 아이템 인벤토리를 뒤늦게 연결한다(설치자가 GameServices.Composing에서 등록하므로 생성 시점에는 없을 수 있다).
        /// 이후 발견한 아이템은 여기에 들어간다. null이면 연결을 해제한다.
        /// </summary>
        public void AttachItemInventory(IItemInventory items) => _items = items;

        /// <summary>위 구역 줄 우선, 그 다음 아래 구역 순서로 잔해 칸 배열을 만든다.</summary>
        private InteriorCell[] CreateCells()
        {
            var cells = new InteriorCell[_data.CellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                bool upper = index < _data.AreaCellCount;
                int local = upper ? index : index - _data.AreaCellCount;
                cells[index] = new InteriorCell(index, local / _data.Columns, local % _data.Columns, upper);
                if (_data.StartEmpty) cells[index].State = InteriorCellState.Empty;
            }
            return cells;
        }

        /// <summary>부분 Fisher-Yates로 서로 다른 칸을 MinimumFacilityPlots개 골라 고정 부지로 표시한다. 어떤 IRandomSource에도 종료한다.</summary>
        private void PlaceGuaranteedPlots(InteriorCell[] cells)
        {
            var order = new int[cells.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = 0; i < _data.MinimumFacilityPlots; i++)
            {
                int pick = Clamp(_random.NextInt(i, order.Length), i, order.Length - 1);
                (order[i], order[pick]) = (order[pick], order[i]);
                cells[order[i]].GuaranteedPlot = true;
            }
        }

        /// <summary>value를 [min, max]로 자른다. 주입된 난수가 범위를 어겨도 배치가 깨지지 않게 한다.</summary>
        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        /// <summary>hubId의 칸 36개(데이터 크기)를 읽기 전용으로 돌려준다.</summary>
        public IReadOnlyList<IInteriorCell> Cells(string hubId) => _views[Require(hubId)];

        /// <summary>이번 정차에서 hubId에 남은 조사 횟수.</summary>
        public int InvestigationsLeft(string hubId) => _left[Require(hubId)];

        /// <summary>hubId의 cellIndex가 잔해이고 남은 횟수가 있을 때만 true. 모르는 거점·범위 밖 칸은 false.</summary>
        public bool CanInvestigate(string hubId, int cellIndex)
        {
            if (hubId == null || !_cells.TryGetValue(hubId, out InteriorCell[] cells)) return false;
            if (cellIndex < 0 || cellIndex >= cells.Length) return false;
            return cells[cellIndex].State == InteriorCellState.Rubble && _left[hubId] > 0;
        }

        /// <summary>
        /// hubId의 cellIndex를 조사한다. 횟수를 1 줄이고 고정 부지면 FacilityPlot, 아니면 확률표로 결과를 정한다.
        /// 발견한 자원은 재고에, 아이템은 아이템 인벤토리(있으면)에 넣고 칸은 Empty가 된다. 조사할 수 없으면 예외.
        /// </summary>
        public InvestigationResult Investigate(string hubId, int cellIndex, out string foundId)
        {
            if (!CanInvestigate(hubId, cellIndex)) throw new InvalidOperationException($"Cannot investigate {hubId}[{cellIndex}].");
            InteriorCell cell = _cells[hubId][cellIndex];
            _left[hubId]--;
            InvestigationResult result = cell.GuaranteedPlot ? InvestigationResult.FacilityPlot : Roll();
            foundId = null;
            switch (result)
            {
                case InvestigationResult.FacilityPlot:
                    cell.State = InteriorCellState.FacilityPlot;
                    break;
                case InvestigationResult.FoundItem:
                    foundId = _data.BuriedItems[Clamp(_random.NextInt(0, _data.BuriedItems.Count), 0, _data.BuriedItems.Count - 1)];
                    _items?.Add(foundId, 1);
                    cell.State = InteriorCellState.Empty;
                    break;
                case InvestigationResult.FoundResource:
                    BuriedResource resource = _data.BuriedResources[Clamp(_random.NextInt(0, _data.BuriedResources.Count), 0, _data.BuriedResources.Count - 1)];
                    foundId = resource.Id;
                    _resources.Add(resource.Id, resource.Amount);
                    cell.State = InteriorCellState.Empty;
                    break;
                default:
                    cell.State = InteriorCellState.Empty;
                    break;
            }
            CellChanged?.Invoke(hubId, cellIndex);
            InvestigationsChanged?.Invoke(hubId, _left[hubId]);
            Investigated?.Invoke(hubId, cellIndex, result, foundId);
            return result;
        }

        /// <summary>확률표(빈 터→아이템→자원→부지 누적) 위에서 NextFloat 한 번으로 결과를 고른다.</summary>
        private InvestigationResult Roll()
        {
            OutcomeWeights weights = _data.Outcomes;
            float roll = _random.NextFloat() * weights.Total;
            float cumulative = weights.Empty;
            if (roll < cumulative) return InvestigationResult.Empty;
            cumulative += weights.Item;
            if (roll < cumulative) return InvestigationResult.FoundItem;
            cumulative += weights.Resource;
            if (roll < cumulative) return InvestigationResult.FoundResource;
            return InvestigationResult.FacilityPlot;
        }

        /// <summary>정차 시 호출. hubId의 조사 횟수를 데이터값으로 초기화하고 알린다.</summary>
        public void OnDocked(string hubId)
        {
            Require(hubId);
            _left[hubId] = _data.InvestigationsPerDocking;
            InvestigationsChanged?.Invoke(hubId, _left[hubId]);
        }

        // ----- 스토리 013/014가 쓰는 칸 상태 전이. 전이표:
        //   Empty        --TryPlaceItem(itemId)-->      ItemPlaced
        //   ItemPlaced   --TryRemoveItem-->             Empty
        //   FacilityPlot --TryBuildFacility(facilityId)--> FacilityBuilt
        //   Empty        --TryBuildFacility(facilityId)--> FacilityBuilt   (스토리 016: 부지 없이 빈 칸에도 시설을 놓는다)
        //   그 밖의 모든 (상태, 명령) 조합은 거부(false)하고 아무것도 바꾸지 않는다.

        /// <summary>빈 터에 itemId를 놓아 ItemPlaced로 바꾼다. 다른 상태면 false.</summary>
        public bool TryPlaceItem(string hubId, int cellIndex, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("Item id is required.", nameof(itemId));
            return Transition(hubId, cellIndex, InteriorCellState.Empty, InteriorCellState.ItemPlaced, itemId, null);
        }

        /// <summary>놓인 아이템을 치워 Empty로 되돌린다. ItemPlaced가 아니면 false.</summary>
        public bool TryRemoveItem(string hubId, int cellIndex)
        {
            return Transition(hubId, cellIndex, InteriorCellState.ItemPlaced, InteriorCellState.Empty, null, null);
        }

        /// <summary>
        /// IInteriorFacilityCells(014·015가 호출): 부지(FacilityPlot) 또는 빈 칸(Empty, 스토리 016)에 facilityId를 지어 FacilityBuilt로 바꾼다.
        /// 다른 상태이거나 id가 비면 false.
        /// </summary>
        public bool TryBuildFacility(string hubId, int cellIndex, string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId)) return false;
            return Transition(hubId, cellIndex, InteriorCellState.FacilityPlot, InteriorCellState.FacilityBuilt, null, facilityId)
                || Transition(hubId, cellIndex, InteriorCellState.Empty, InteriorCellState.FacilityBuilt, null, facilityId);
        }

        /// <summary>칸이 from 상태일 때만 to로 바꾸고 아이템·시설 id를 기록한 뒤 CellChanged를 알린다.</summary>
        private bool Transition(string hubId, int cellIndex, InteriorCellState from, InteriorCellState to, string itemId, string facilityId)
        {
            if (hubId == null || !_cells.TryGetValue(hubId, out InteriorCell[] cells)) return false;
            if (cellIndex < 0 || cellIndex >= cells.Length) return false;
            InteriorCell cell = cells[cellIndex];
            if (cell.State != from) return false;
            cell.State = to;
            cell.ItemId = itemId;
            cell.FacilityId = facilityId;
            CellChanged?.Invoke(hubId, cellIndex);
            return true;
        }

        /// <summary>등록된 거점 id인지 확인하고 돌려준다. 아니면 ArgumentException.</summary>
        private string Require(string hubId)
        {
            if (hubId == null || !_cells.ContainsKey(hubId)) throw new ArgumentException("Unknown hub: " + hubId, nameof(hubId));
            return hubId;
        }
    }
}
