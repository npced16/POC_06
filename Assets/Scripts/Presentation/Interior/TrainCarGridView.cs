using UnityEngine;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 034. TrainCarGrid 프리팹 루트(거점 내부 열차 칸 TrainCar 아래 씬 인스턴스, 회전·크기는 월드 기준 1로 보정).
    /// 자식 Frames(칸 틀 SpriteRenderer, 장비 모드에서만 켬)와 Pieces(장비 조각 부모)를 가진다. 칸 순번 Car는 0 = 맨 앞 칸.
    /// 로컬 좌표: 열 0이 진행 방향 앞(+x), 행 0이 위(+y).
    /// </summary>
    public sealed class TrainCarGridView : MonoBehaviour
    {
        [SerializeField] private int _car;
        [SerializeField] private GameObject _frames;
        [SerializeField] private Transform _pieces;

        /// <summary>열차 칸 순번(0 = 맨 앞).</summary>
        public int Car => _car;
        /// <summary>장비 조각 부모.</summary>
        public Transform Pieces => _pieces != null ? _pieces : transform;

        /// <summary>제작 도구가 칸 순번과 자식 참조를 연결한다.</summary>
        public void Configure(int car, GameObject frames, Transform pieces) { _car = car; _frames = frames; _pieces = pieces; }

        /// <summary>칸 틀을 보이거나 숨긴다.</summary>
        public void SetFramesVisible(bool visible) { if (_frames != null && _frames.activeSelf != visible) _frames.SetActive(visible); }

        /// <summary>column에서 시작하는 width칸 장비(행 row)의 로컬 중심. columns·rows는 그리드 크기, cell은 칸 크기.</summary>
        public static Vector3 LocalCenter(int column, int row, int width, int columns, int rows, float cell)
        {
            float center = column + (Mathf.Max(1, width) - 1) * 0.5f;
            return new Vector3(((columns - 1) * 0.5f - center) * cell, ((rows - 1) * 0.5f - row) * cell, 0f);
        }
    }
}
