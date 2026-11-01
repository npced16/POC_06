using IronRing.Contracts;

namespace IronRing.Interior
{
    /// <summary>거점 내부 칸 하나. 상태 변경은 HubInterior만 수행한다(전이표는 HubInterior.Transition 참고).</summary>
    public sealed class InteriorCell : IInteriorCell
    {
        /// <summary>0부터, 위 구역 줄 우선 다음 아래 구역.</summary>
        public int Index { get; }
        /// <summary>구역 안의 줄. 두 구역 모두 화면 위에서 아래로 0, 1, 2: 위 구역 0은 선로에서 가장 먼 줄, 아래 구역 0은 선로 바로 아래 줄.</summary>
        public int Row { get; }
        /// <summary>구역 안의 칸(열).</summary>
        public int Column { get; }
        /// <summary>true면 선로 위 구역.</summary>
        public bool IsUpper { get; }
        /// <summary>현재 상태.</summary>
        public InteriorCellState State { get; internal set; }
        /// <summary>ItemPlaced일 때 아이템 id.</summary>
        public string ItemId { get; internal set; }
        /// <summary>FacilityBuilt일 때 시설 id.</summary>
        public string FacilityId { get; internal set; }
        /// <summary>생성 시 고정 배치된 시설 부지인지(조사 결과가 항상 FacilityPlot).</summary>
        internal bool GuaranteedPlot { get; set; }

        /// <summary>index, row, column, isUpper를 저장하고 잔해 상태로 시작한다.</summary>
        internal InteriorCell(int index, int row, int column, bool isUpper)
        {
            Index = index;
            Row = row;
            Column = column;
            IsUpper = isUpper;
            State = InteriorCellState.Rubble;
        }
    }
}
