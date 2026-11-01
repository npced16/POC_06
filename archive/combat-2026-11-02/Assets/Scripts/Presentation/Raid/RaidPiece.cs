using UnityEngine;

namespace IronRing.Presentation.Raid
{
    /// <summary>스토리 033. 클릭할 수 있는 습격 조각(몬스터·둥지). BoxCollider2D와 함께 프리팹에 붙고 RaidView가 id를 넣는다.</summary>
    [DisallowMultipleComponent]
    public sealed class RaidPiece : MonoBehaviour
    {
        [SerializeField] private bool _isNest;
        [SerializeField] private int _id;

        /// <summary>둥지면 true(아니면 몬스터).</summary>
        public bool IsNest => _isNest;
        /// <summary>몬스터 id 또는 둥지 순번.</summary>
        public int Id => _id;

        /// <summary>종류와 id를 정한다(인스턴스 생성 직후 RaidView가 부른다).</summary>
        public void Configure(bool isNest, int id)
        {
            _isNest = isNest;
            _id = id;
        }
    }
}
