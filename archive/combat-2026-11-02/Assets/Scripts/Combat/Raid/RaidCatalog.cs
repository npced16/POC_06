using UnityEngine;

namespace IronRing.Combat.Raid
{
    /// <summary>
    /// 스토리 033 습격 아트·프리팹 카탈로그(Assets/Prefabs/Raid/RaidCatalog.asset). RaidBuild.Generate가 만들고 프리팹을 채우며,
    /// 아트 담당(SpriteGenRaid)은 스프라이트 필드만 이름으로 넣는다(_spiderFrames, _automatonFrames, _nest, _nestBroken, _shell, _hit, _terminalOccupied).
    /// 스프라이트가 비어 있으면 RaidView가 색 사각형으로 대신 그린다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/Raid Catalog")]
    public sealed class RaidCatalog : ScriptableObject
    {
        [Header("스프라이트 (Art/Generated/raid)")]
        [SerializeField] private Sprite[] _spiderFrames;
        [SerializeField] private Sprite[] _automatonFrames;
        [SerializeField] private Sprite _nest;
        [SerializeField] private Sprite _nestBroken;
        [SerializeField] private Sprite _shell;
        [SerializeField] private Sprite _hit;
        [SerializeField] private Sprite _terminalOccupied;
        [Header("프리팹 (Assets/Prefabs/Raid, 연기는 Fx)")]
        [SerializeField] private GameObject _monsterPrefab;
        [SerializeField] private GameObject _nestPrefab;
        [SerializeField] private GameObject _shellPrefab;
        [SerializeField] private GameObject _hitPrefab;
        [SerializeField] private GameObject _smokePrefab;
        [Header("스프라이트가 없을 때 대체 색")]
        [SerializeField] private Color _spiderColor = new Color32(196, 120, 64, 255);
        [SerializeField] private Color _automatonColor = new Color32(138, 92, 70, 255);
        [SerializeField] private Color _nestColor = new Color32(92, 64, 52, 255);
        [SerializeField] private Color _nestBrokenColor = new Color32(60, 52, 48, 160);
        [SerializeField] private Color _shellColor = new Color32(255, 214, 120, 255);
        [SerializeField] private Color _hitColor = new Color32(255, 240, 200, 220);
        [SerializeField] private Color _backdropColor = new Color32(70, 58, 50, 140);
        [SerializeField] private float _framesPerSecond = 6f;

        /// <summary>증기 거미 2프레임.</summary>
        public Sprite[] SpiderFrames => _spiderFrames;
        /// <summary>녹슨 자동인형 2프레임.</summary>
        public Sprite[] AutomatonFrames => _automatonFrames;
        /// <summary>둥지.</summary>
        public Sprite Nest => _nest;
        /// <summary>부서진 둥지.</summary>
        public Sprite NestBroken => _nestBroken;
        /// <summary>포탄.</summary>
        public Sprite Shell => _shell;
        /// <summary>명중 번쩍임.</summary>
        public Sprite Hit => _hit;
        /// <summary>점령된 종점 배경.</summary>
        public Sprite TerminalOccupied => _terminalOccupied;
        /// <summary>몬스터 프리팹(SpriteRenderer, SpriteFrameLoop, BoxCollider2D, RaidPiece).</summary>
        public GameObject MonsterPrefab => _monsterPrefab;
        /// <summary>둥지 프리팹(SpriteRenderer, BoxCollider2D, RaidPiece).</summary>
        public GameObject NestPrefab => _nestPrefab;
        /// <summary>포탄 프리팹.</summary>
        public GameObject ShellPrefab => _shellPrefab;
        /// <summary>명중 번쩍임 프리팹.</summary>
        public GameObject HitPrefab => _hitPrefab;
        /// <summary>사망 연기 프리팹(Fx/Smoke).</summary>
        public GameObject SmokePrefab => _smokePrefab;
        /// <summary>대체 색: 거미.</summary>
        public Color SpiderColor => _spiderColor;
        /// <summary>대체 색: 자동인형.</summary>
        public Color AutomatonColor => _automatonColor;
        /// <summary>대체 색: 둥지.</summary>
        public Color NestColor => _nestColor;
        /// <summary>대체 색: 부서진 둥지.</summary>
        public Color NestBrokenColor => _nestBrokenColor;
        /// <summary>대체 색: 포탄.</summary>
        public Color ShellColor => _shellColor;
        /// <summary>대체 색: 명중.</summary>
        public Color HitColor => _hitColor;
        /// <summary>대체 색: 종점 배경.</summary>
        public Color BackdropColor => _backdropColor;
        /// <summary>몬스터 2프레임 재생 속도.</summary>
        public float FramesPerSecond => _framesPerSecond;
    }
}
