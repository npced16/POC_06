using UnityEngine;

namespace IronRing.Presentation.Effects
{
    [CreateAssetMenu(menuName = "IronRing/Effects Settings")]
    public sealed class EffectsSettings : ScriptableObject
    {
        [Header("Artwork")]
        [SerializeField] private Sprite[] _sprites;
        [SerializeField] private Material _spriteMaterial;
        [Header("Hub")]
        [SerializeField] private float _prosperityMaximum = 100f;
        [SerializeField] private Vector3 _gaugeOffset = new Vector3(0, 0.85f, 0);
        [SerializeField] private Vector3 _gaugeScale = new Vector3(1.6f, 1f, 1);
        [SerializeField] private Vector3 _npcOffset = new Vector3(1, 0.2f);
        [SerializeField] private float _npcSpacing = 0.45f;
        [Header("Animation")]
        [SerializeField] private float _messageSeconds = 3f;
        [SerializeField] private float _riseSpeed = 0.25f;
        [SerializeField] private float _debrisSeconds = 2f;
        [SerializeField] private int _debrisCount = 8;
        [SerializeField] private float _debrisSpeed = 0.6f;
        [SerializeField] private Vector3 _raiderOffset = new Vector3(1.1f, 0, 0);
        [Header("Korean overlay")]
        [SerializeField] private int _fontSize = 20;
        [SerializeField] private float _messageWidth = 300;
        [SerializeField] private Color _ink = new Color(0.96f, 0.9f, 0.73f);
        [SerializeField] private Color _bubble = new Color(0.08f, 0.1f, 0.13f, 0.94f);
        [SerializeField] private Color _defeatShade = new Color(0.02f, 0.025f, 0.04f, 0.88f);

        public Material SpriteMaterial => _spriteMaterial;
        public float ProsperityMaximum => _prosperityMaximum;
        public Vector3 GaugeOffset => _gaugeOffset;
        public Vector3 GaugeScale => _gaugeScale;
        public Vector3 NpcOffset => _npcOffset;
        public float NpcSpacing => _npcSpacing;
        public float MessageSeconds => _messageSeconds;
        public float RiseSpeed => _riseSpeed;
        public float DebrisSeconds => _debrisSeconds;
        public int DebrisCount => _debrisCount;
        public float DebrisSpeed => _debrisSpeed;
        public Vector3 RaiderOffset => _raiderOffset;
        public int FontSize => _fontSize;
        public float MessageWidth => _messageWidth;
        public Color Ink => _ink;
        public Color Bubble => _bubble;
        public Color DefeatShade => _defeatShade;

        /// <summary>name과 일치하는 등록 스프라이트를 반환하며 등록이 없으면 null을 반환한다.</summary>
        public Sprite Sprite(string name)
        {
            foreach (Sprite sprite in _sprites) if (sprite.name == name) return sprite;
            return null;
        }


        void OnValidate()
        {
            _prosperityMaximum = Mathf.Max(1, _prosperityMaximum);
            _messageSeconds = Mathf.Max(0.1f, _messageSeconds);
            _debrisSeconds = Mathf.Max(0.1f, _debrisSeconds);
        }
    }
}
