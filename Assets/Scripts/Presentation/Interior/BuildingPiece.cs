using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 027. 건물 지붕 조각(BuildingPiece.prefab, SpriteRenderer + 칸 크기 트리거 BoxCollider2D)의 신원. 클릭한 조각에서
    /// 거점 id와 서식지 묶음(종류 id, 칸들)을 찾아 그 집에 사는 NPC의 대화를 연다(InteriorNpcWalker가 처리).
    /// </summary>
    public sealed class BuildingPiece : MonoBehaviour
    {
        /// <summary>건물이 선 거점 id.</summary>
        public string HubId { get; private set; }
        /// <summary>서식지 종류 id.</summary>
        public string TypeId { get; private set; }
        /// <summary>건물이 덮는 칸 번호들.</summary>
        public IReadOnlyList<int> Cells { get; private set; }

        /// <summary>생성 직후 신원을 한 번 정한다.</summary>
        public void Bind(string hubId, string typeId, IReadOnlyList<int> cells)
        {
            HubId = hubId;
            TypeId = typeId;
            Cells = cells;
        }
    }
}
