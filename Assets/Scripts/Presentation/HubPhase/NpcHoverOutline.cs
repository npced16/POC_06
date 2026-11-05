using UnityEngine;

namespace IronRing.Presentation.HubPhase
{
    /// <summary>
    /// 마우스가 올라간 마을 NPC의 둘레에 단색 윤곽선을 표시한다.
    /// 본체 스프라이트의 단색 실루엣을 여덟 방향으로 밀어 본체 뒤에 그린다. 게임 상태는 바꾸지 않는다.
    /// </summary>
    public sealed class NpcHoverOutline : MonoBehaviour
    {
        private const string SHADER_PATH = "HubPhase/NpcOutline";
        private const string COLOR_PROPERTY = "_OutlineColor";
        private static readonly Vector2[] DIRECTIONS =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f), new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f)
        };
        private static Material _material;

        private SpriteRenderer _body;
        private SpriteRenderer[] _copies;
        private Color _color;
        private float _pixels;

        /// <summary>윤곽선 색 color와 스프라이트 픽셀 단위 두께 pixels를 저장하고 숨긴 상태로 시작한다.</summary>
        public void Bind(Color color, float pixels)
        {
            _body = GetComponent<SpriteRenderer>();
            _color = color;
            _pixels = pixels;
            enabled = false;
        }

        /// <summary>visible에 따라 윤곽선을 켜거나 끈다. 처음 켤 때 실루엣 렌더러를 만든다.</summary>
        public void SetVisible(bool visible)
        {
            if (visible && _copies == null) CreateCopies();
            if (_copies == null) return;
            foreach (SpriteRenderer copy in _copies) copy.enabled = visible;
            enabled = visible;
            if (visible) Sync();
        }

        void LateUpdate() => Sync();

        /// <summary>본체 자식으로 실루엣 렌더러를 만들어 _copies에 저장한다. 위치는 본체 로컬 좌표라 크기 변경을 따라간다.</summary>
        private void CreateCopies()
        {
            if (_material == null) _material = new Material(UnityEngine.Resources.Load<Shader>(SHADER_PATH));
            _material.SetColor(COLOR_PROPERTY, _color);
            float offset = _pixels / _body.sprite.pixelsPerUnit;
            _copies = new SpriteRenderer[DIRECTIONS.Length];
            for (int i = 0; i < DIRECTIONS.Length; i++)
            {
                var piece = new GameObject("Outline" + i);
                piece.transform.SetParent(transform, false);
                piece.transform.localPosition = DIRECTIONS[i] * offset;
                SpriteRenderer copy = piece.AddComponent<SpriteRenderer>();
                copy.sharedMaterial = _material;
                _copies[i] = copy;
            }
        }

        /// <summary>본체의 스프라이트, 좌우 반전, 정렬을 실루엣에 복사한다. 정렬은 본체 바로 뒤로 둔다.</summary>
        private void Sync()
        {
            foreach (SpriteRenderer copy in _copies)
            {
                copy.sprite = _body.sprite;
                copy.flipX = _body.flipX;
                copy.sortingLayerID = _body.sortingLayerID;
                copy.sortingOrder = _body.sortingOrder - 1;
            }
        }
    }
}
