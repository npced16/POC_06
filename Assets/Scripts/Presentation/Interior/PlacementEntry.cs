using System;

namespace IronRing.Presentation.Interior
{
    /// <summary>스토리 015. 핫바·팔레트가 고르는 배치 대상의 종류.</summary>
    public enum PlacementKind
    {
        /// <summary>아이템 카탈로그의 타일·아이템(IItemPlacement로 놓는다).</summary>
        Item,
        /// <summary>방어시설(IFacilityBuilder + IInteriorFacilityCells로 짓는다).</summary>
        Facility,
        /// <summary>스토리 034. 열차 장비(ITrainLoadout, 장비 모드의 IPlacementSurface로 놓는다).</summary>
        Equipment
    }

    /// <summary>스토리 015. 핫바 슬롯 하나에 담기는 배치 대상(종류 + id). 값 형식이다.</summary>
    public readonly struct PlacementEntry : IEquatable<PlacementEntry>
    {
        public PlacementKind Kind { get; }
        public string Id { get; }

        public PlacementEntry(PlacementKind kind, string id)
        {
            Kind = kind;
            Id = id;
        }

        /// <summary>id가 있으면 true(빈 슬롯은 false).</summary>
        public bool IsValid => !string.IsNullOrEmpty(Id);

        public bool Equals(PlacementEntry other) => Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PlacementEntry other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Id != null ? Id.GetHashCode() : 0);
        public override string ToString() => Kind + ":" + Id;
    }
}
