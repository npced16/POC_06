using UnityEngine;

namespace IronRing.Presentation.Effects
{
    /// <summary>
    /// 스토리 019. 같은 오브젝트의 SpriteRenderer에 프레임 스프라이트를 차례로 돌려 끼운다(굴뚝 연기 루프). 프레임과 속도는 프리팹에 저장된 값이다.
    /// 활성/비활성은 상태 뷰(RingHubPhaseView 등)가 바꾼다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFrameLoop : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 8f;
        private float _phase;
        private SpriteRenderer _renderer;

        /// <summary>편집기 제작 도구가 frames와 초당 프레임 수 fps를 프리팹에 저장할 때 쓴다.</summary>
        public void Configure(Sprite[] frames, float fps)
        {
            _frames = frames;
            _framesPerSecond = fps;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _phase = Mathf.Abs(transform.position.x * 0.37f + transform.position.y * 0.61f) % 1f;
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0 || _framesPerSecond <= 0f || _renderer == null) return;
            int index = (int)((Time.time * _framesPerSecond) + _phase * _frames.Length) % _frames.Length;
            if (_renderer.sprite != _frames[index]) _renderer.sprite = _frames[index];
        }
    }
}
