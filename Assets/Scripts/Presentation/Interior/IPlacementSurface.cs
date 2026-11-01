using UnityEngine;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 034. PlacementGhost가 거점 칸 대신 쓰는 배치 면(열차 장비 그리드). 설정되면 고스트 스냅·배치·우클릭 떼기·Ctrl+Z가 이 면으로 간다.
    /// 칸 키는 면이 정하는 정수(스토리 039: 열차 장비 제거 후 남은 범용 면).
    /// </summary>
    public interface IPlacementSurface
    {
        /// <summary>월드 점 world에서 entry(빈 값이면 1칸 기준)를 놓을 가장 가까운 칸 키와 그 조각 중심(snapped). 없으면 -1.</summary>
        int Nearest(Vector3 world, PlacementEntry entry, out Vector3 snapped);
        /// <summary>칸 key에 entry를 놓을 때 조각 중심 월드 위치.</summary>
        Vector3 Anchor(int key, PlacementEntry entry);
        /// <summary>칸 key에 entry를 놓을 수 있는지.</summary>
        bool CanPlace(int key, PlacementEntry entry);
        /// <summary>칸 key에 entry를 놓는다.</summary>
        bool TryPlace(int key, PlacementEntry entry);
        /// <summary>칸 key를 차지한 것을 뗀다.</summary>
        bool TryRemove(int key);
        /// <summary>이 면의 최근 놓기·떼기를 되돌린다.</summary>
        bool TryUndo();
        /// <summary>고스트 크기 배율(고스트 프리팹 기본 크기 대비).</summary>
        float GhostScale(PlacementEntry entry);
        /// <summary>고스트 정렬 순서(열차 위에 보이게).</summary>
        int GhostOrder { get; }
    }
}
