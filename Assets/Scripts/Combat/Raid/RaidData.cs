using System;

using UnityEngine;

namespace IronRing.Combat.Raid
{
    /// <summary>
    /// 스토리 033. 점령된 종점 습격의 모든 수치(Resources/Raid.json). 규칙(RaidSystem)과 표현(RaidView)과 데모가 함께 읽는다.
    /// 설계 근거: design/game-brief.md MVP 2, production/epics/iron-ring-train/story-033-raid-occupied-outpost.md.
    /// </summary>
    [Serializable]
    public sealed class RaidData
    {
        [SerializeField] private int _seed = 33;
        [SerializeField] private string _waypointId = "TERMINAL";
        [SerializeField] private string _hubId = "hub-1";
        [SerializeField] private float _segmentSeconds = 12f;
        [SerializeField] private float _crawlEdgeFraction = 0.3f;
        [SerializeField] private int _hullBase = 10;
        [SerializeField] private int _hullPerWall = 5;
        [SerializeField] private string _wallFacilityId = "wall";
        [SerializeField] private string _turretFacilityId = "turret";
        [SerializeField] private float _turretShotsPerSecond = 0.8f;
        [SerializeField] private int _turretDamage = 1;
        [SerializeField] private int _heavyTurretDamage = 3;
        [SerializeField] private float _turretRange = 7f;
        [SerializeField] private int _playerDamage = 2;
        [SerializeField] private float _playerCooldown = 0.4f;
        [SerializeField] private float _clickRadius = 0.6f;
        [SerializeField] private float _contactRadius = 0.45f;
        [SerializeField] private int _nestHp = 16;
        [SerializeField] private float _firstSpawnDelay = 0.6f;
        [SerializeField] private float _spawnStagger = 0.5f;
        [SerializeField] private float _spawnJitter = 0.3f;
        [SerializeField] private RaidOffset[] _nestOffsets;
        [SerializeField] private float _trainFallbackLength = 4f;
        [SerializeField] private int _trainCars = 3;
        [SerializeField] private float _cameraOffsetX = 1.8f;
        [SerializeField] private float _nestLabelGap = 1f;
        [SerializeField] private RaidMonsterData[] _monsters;
        [SerializeField] private RaidWave[] _waves;
        [SerializeField] private float _liberatedBoardBonus = 0.15f;
        [SerializeField] private float _raidCameraSize = 5f;
        [SerializeField] private float _cameraLerpPerSecond = 4f;
        [SerializeField] private float _shellSeconds = 0.15f;
        [SerializeField] private float _hitSeconds = 0.18f;
        [SerializeField] private float _smokeSeconds = 0.9f;
        [SerializeField] private float _smokeScale = 0.5f;
        [SerializeField] private float _monsterScale = 1f;
        [SerializeField] private float _nestScale = 1f;
        [Header("Demo (--raid-demo)")]
        [SerializeField] private int _demoTurretsMid = 2;
        [SerializeField] private int _demoWallsMid = 1;
        [SerializeField] private int _demoTurretsWin = 6;
        [SerializeField] private int _demoWallsWin = 3;
        [SerializeField] private int _demoHullLose = 4;
        [SerializeField] private float _demoMidSeconds = 5f;
        [SerializeField] private float _demoStartSeconds = 0.8f;

        /// <summary>결정적 난수 시드.</summary>
        public int Seed => _seed;
        /// <summary>점령된 경유지 id(빈 문자열이면 모든 경유지).</summary>
        public string WaypointId => _waypointId;
        /// <summary>방벽·포탑 수를 읽는 거점 id.</summary>
        public string HubId => _hubId;
        /// <summary>구간 통과 시간(초).</summary>
        public float SegmentSeconds => _segmentSeconds;
        /// <summary>습격 동안 열차가 다음 선로 구간에서 기어가는 비율(0..0.95). 출발은 그 지점부터 이어진다.</summary>
        public float CrawlEdgeFraction => _crawlEdgeFraction;
        /// <summary>기본 내구도.</summary>
        public int HullBase => _hullBase;
        /// <summary>방벽 하나당 내구도.</summary>
        public int HullPerWall => _hullPerWall;
        /// <summary>방벽 시설 id.</summary>
        public string WallFacilityId => _wallFacilityId;
        /// <summary>포탑 시설 id.</summary>
        public string TurretFacilityId => _turretFacilityId;
        /// <summary>포탑 하나의 초당 발수.</summary>
        public float TurretShotsPerSecond => _turretShotsPerSecond;
        /// <summary>포탑 한 발 피해.</summary>
        public int TurretDamage => _turretDamage;
        /// <summary>중포탑 한 발 피해(스토리 034 장비).</summary>
        public int HeavyTurretDamage => _heavyTurretDamage;
        /// <summary>포탑 사거리(열차 기준, 월드 단위).</summary>
        public float TurretRange => _turretRange;
        /// <summary>열차 대포 한 발 피해.</summary>
        public int PlayerDamage => _playerDamage;
        /// <summary>열차 대포 재장전(초).</summary>
        public float PlayerCooldown => _playerCooldown;
        /// <summary>콜라이더를 못 맞췄을 때 가장 가까운 몬스터를 고르는 반경(월드).</summary>
        public float ClickRadius => _clickRadius;
        /// <summary>몬스터가 열차에 닿는 거리.</summary>
        public float ContactRadius => _contactRadius;
        /// <summary>둥지 체력.</summary>
        public int NestHp => _nestHp;
        /// <summary>습격 시작 뒤 첫 스폰까지(초).</summary>
        public float FirstSpawnDelay => _firstSpawnDelay;
        /// <summary>둥지 순번마다 첫 스폰을 늦추는 간격(초).</summary>
        public float SpawnStagger => _spawnStagger;
        /// <summary>스폰 간격 흔들림 비율(±).</summary>
        public float SpawnJitter => _spawnJitter;
        /// <summary>종점 기준 둥지 위치(개수 = 둥지 수).</summary>
        public RaidOffset[] NestOffsets => _nestOffsets;
        /// <summary>표현이 열차 위치를 주지 않을 때 쓰는 가상 구간 길이.</summary>
        public float TrainFallbackLength => _trainFallbackLength;
        /// <summary>열차 칸 수(표현: 칸 위치 조회·발사 위치. 0번 = 맨 앞 칸).</summary>
        public int TrainCars => Mathf.Max(1, _trainCars);
        /// <summary>습격 카메라 중심을 종점에서 둥지 쪽(+X)으로 미는 거리.</summary>
        public float CameraOffsetX => _cameraOffsetX;
        /// <summary>"둥지 N" 라벨을 가장 아래 둥지보다 얼마나 아래에 둘지.</summary>
        public float NestLabelGap => _nestLabelGap;
        /// <summary>몬스터 종류 표.</summary>
        public RaidMonsterData[] Monsters => _monsters;
        /// <summary>바퀴별 스폰 표.</summary>
        public RaidWave[] Waves => _waves;
        /// <summary>해방 뒤 승객 탑승 확률 가산.</summary>
        public float LiberatedBoardBonus => _liberatedBoardBonus;
        /// <summary>습격 중 선로 카메라 크기.</summary>
        public float RaidCameraSize => _raidCameraSize;
        /// <summary>카메라 이동 보간 속도(초당).</summary>
        public float CameraLerpPerSecond => _cameraLerpPerSecond;
        /// <summary>포탄 비행 시간.</summary>
        public float ShellSeconds => _shellSeconds;
        /// <summary>명중 번쩍임 시간.</summary>
        public float HitSeconds => _hitSeconds;
        /// <summary>사망 연기 시간.</summary>
        public float SmokeSeconds => _smokeSeconds;
        /// <summary>사망 연기 크기 배율.</summary>
        public float SmokeScale => _smokeScale;
        /// <summary>몬스터 표시 배율.</summary>
        public float MonsterScale => _monsterScale;
        /// <summary>둥지 표시 배율.</summary>
        public float NestScale => _nestScale;
        /// <summary>데모 mid/start 포탑 수.</summary>
        public int DemoTurretsMid => _demoTurretsMid;
        /// <summary>데모 mid/start 방벽 수.</summary>
        public int DemoWallsMid => _demoWallsMid;
        /// <summary>데모 win 포탑 수.</summary>
        public int DemoTurretsWin => _demoTurretsWin;
        /// <summary>데모 win 방벽 수.</summary>
        public int DemoWallsWin => _demoWallsWin;
        /// <summary>데모 lose 최대 내구도.</summary>
        public int DemoHullLose => _demoHullLose;
        /// <summary>데모 mid 캡처 시점(습격 경과 초).</summary>
        public float DemoMidSeconds => _demoMidSeconds;
        /// <summary>데모 start 캡처 시점(습격 경과 초).</summary>
        public float DemoStartSeconds => _demoStartSeconds;

        /// <summary>JSON을 읽고 범위를 검사한다. 잘못된 값이면 ArgumentException.</summary>
        public static RaidData Parse(string json)
        {
            RaidData data = JsonUtility.FromJson<RaidData>(json);
            if (data._segmentSeconds <= 0 || data._hullBase < 1 || data._hullPerWall < 0 || data._playerCooldown < 0 || data._nestHp < 1 ||
                data._nestOffsets == null || data._nestOffsets.Length == 0 || data._monsters == null || data._monsters.Length == 0 ||
                data._waves == null || data._waves.Length == 0 || data._turretShotsPerSecond < 0 || data._crawlEdgeFraction < 0 || data._crawlEdgeFraction > 0.95f)
                throw new ArgumentException("Invalid raid data (Raid.json).");
            foreach (RaidMonsterData monster in data._monsters)
                if (monster == null || string.IsNullOrEmpty(monster.Id) || monster.Hp < 1 || monster.Speed <= 0) throw new ArgumentException("Invalid raid monster.");
            return data;
        }

        /// <summary>id가 같은 몬스터 정의(없으면 첫 정의).</summary>
        public RaidMonsterData Monster(string id)
        {
            foreach (RaidMonsterData monster in _monsters) if (monster.Id == id) return monster;
            return _monsters[0];
        }

        /// <summary>lap 이하에서 가장 큰 _fromLap을 가진 스폰 표 줄.</summary>
        public RaidWave WaveFor(int lap)
        {
            RaidWave chosen = _waves[0];
            foreach (RaidWave wave in _waves) if (wave.FromLap <= lap && wave.FromLap >= chosen.FromLap) chosen = wave;
            return chosen;
        }
    }

    /// <summary>종점 기준 오프셋(월드 단위).</summary>
    [Serializable]
    public sealed class RaidOffset
    {
        /// <summary>X 오프셋.</summary>
        public float x;
        /// <summary>Y 오프셋.</summary>
        public float y;
    }

    /// <summary>몬스터 종류 하나.</summary>
    [Serializable]
    public sealed class RaidMonsterData
    {
        [SerializeField] private string _id;
        [SerializeField] private int _hp;
        [SerializeField] private float _speed;
        [SerializeField] private int _damage;
        /// <summary>종류 id("spider", "automaton").</summary>
        public string Id => _id;
        /// <summary>체력.</summary>
        public int Hp => _hp;
        /// <summary>이동 속도(월드 단위/초).</summary>
        public float Speed => _speed;
        /// <summary>열차에 닿을 때 깎는 내구도.</summary>
        public int Damage => _damage;
    }

    /// <summary>바퀴 수에 따른 스폰 표 한 줄.</summary>
    [Serializable]
    public sealed class RaidWave
    {
        [SerializeField] private int _fromLap;
        [SerializeField] private float _spawnInterval;
        [SerializeField] private float _automatonChance;
        [SerializeField] private int _maxAlive;
        /// <summary>이 줄이 적용되는 최소 바퀴.</summary>
        public int FromLap => _fromLap;
        /// <summary>둥지 하나의 스폰 간격(초).</summary>
        public float SpawnInterval => _spawnInterval;
        /// <summary>스폰이 녹슨 자동인형일 확률(나머지는 증기 거미).</summary>
        public float AutomatonChance => _automatonChance;
        /// <summary>동시에 살아 있을 수 있는 몬스터 수.</summary>
        public int MaxAlive => _maxAlive;
    }
}
