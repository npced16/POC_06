using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Combat
{
    /// <summary>정차역 POC의 수치와 건물별 포탑 해금을 JSON에서 읽는다.</summary>
    [Serializable]
    public sealed class BattleRules
    {
        [SerializeField] private float _targetDurationSeconds;
        [SerializeField] private BattleStageRule _stage;
        [SerializeField] private MainGunRule _mainGun;
        [SerializeField] private BattleLevelRule[] _levels;
        [SerializeField] private BattleEconomyRule _economy;
        [SerializeField] private TurretRule[] _turrets;
        [SerializeField] private BuildingTurretRule[] _buildings;
        [SerializeField] private EnemyRule[] _enemies;
        [SerializeField] private BattleStageLineup[] _lineups;

        public float TargetDurationSeconds => _targetDurationSeconds;
        public IReadOnlyList<EnemyRule> Enemies => _enemies;
        public IReadOnlyList<BattleStageLineup> Lineups => _lineups;
        public BattleStageRule Stage => _stage;
        public MainGunRule MainGun => _mainGun;
        public IReadOnlyList<BattleLevelRule> Levels => _levels;
        public BattleEconomyRule Economy => _economy;
        public IReadOnlyList<TurretRule> Turrets => _turrets;
        public IReadOnlyList<BuildingTurretRule> Buildings => _buildings;

        /// <summary>Resources의 BattleRules JSON을 읽고 유효성을 검사해 전투 설정을 반환한다.</summary>
        public static BattleRules Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("Battle/BattleRules");
            if (asset == null) throw new InvalidOperationException("Missing Battle/BattleRules resource.");
            BattleRules rules = JsonUtility.FromJson<BattleRules>(asset.text);
            rules.Validate();
            return rules;
        }

        /// <summary>건물 id에 연결된 포탑 규칙을 찾아 반환하며 연결이 없으면 null을 반환한다.</summary>
        public BuildingTurretRule Building(string habitatId)
        {
            foreach (BuildingTurretRule building in _buildings)
                if (building.HabitatId == habitatId) return building;
            return null;
        }

        /// <summary>포탑 id에 대응하는 기본 수치를 찾아 반환하며 알 수 없는 id면 null을 반환한다.</summary>
        public TurretRule Turret(string turretId)
        {
            foreach (TurretRule turret in _turrets)
                if (turret.Id == turretId) return turret;
            return null;
        }

        /// <summary>
        /// 적 id에 대응하는 적 유형 규칙을 찾는다.
        /// enemyId를 사용하며, 알 수 없는 id면 null을 반환한다.
        /// </summary>
        public EnemyRule Enemy(string enemyId)
        {
            foreach (EnemyRule enemy in _enemies)
                if (enemy.Id == enemyId) return enemy;
            return null;
        }

        /// <summary>
        /// 스테이지 번호에 적용할 출현 구성을 찾는다.
        /// stage를 사용하며, _fromStage가 stage 이하인 마지막 구성을 반환한다.
        /// </summary>
        public BattleStageLineup LineupAt(int stage)
        {
            BattleStageLineup lineup = _lineups[0];
            foreach (BattleStageLineup entry in _lineups)
                if (entry.FromStage <= stage) lineup = entry;
            return lineup;
        }

        /// <summary>전투 레벨에 맞는 설치 칸 수를 반환하며 아직 도달하지 않은 레벨은 제외한다.</summary>
        public int SlotsAt(int level)
        {
            int slots = _levels[0].Slots;
            foreach (BattleLevelRule entry in _levels)
                if (entry.Level <= level) slots = entry.Slots;
            return slots;
        }

        /// <summary>level의 누적 경험치 기준을 반환하며 표 이후에는 마지막 증가량으로 계속 늘린다.</summary>
        public int ExperienceAt(int level)
        {
            if (level <= 1) return _levels[0].Experience;
            if (level <= _levels.Length) return _levels[level - 1].Experience;
            int increment = _levels.Length > 1 ? _levels[_levels.Length - 1].Experience - _levels[_levels.Length - 2].Experience : 1;
            long threshold = (long)_levels[_levels.Length - 1].Experience + (long)(level - _levels.Length) * increment;
            return (int)Math.Min(int.MaxValue, threshold);
        }

        /// <summary>JSON의 레벨·포탑·건물 연결을 검사하고 잘못된 설정이면 예외를 발생시킨다.</summary>
        public void Validate()
        {
            if (_targetDurationSeconds <= 0 || _stage == null || _stage.TrainHealth <= 0 ||
                _stage.SpawnIntervalSeconds <= 0 || _stage.ProjectileSpeed <= 0 || _stage.BossSpawnSeconds <= 0 ||
                _stage.BossSpawnSeconds >= _targetDurationSeconds || _stage.HealthGrowthPerStage < 0 ||
                _stage.SpawnGrowthPerStage < 0 || _stage.MinSpawnIntervalSeconds <= 0 ||
                _mainGun == null || _mainGun.CooldownSeconds <= 0 ||
                _mainGun.MaxLevel < 1 || _levels == null || _levels.Length == 0 ||
                _economy == null || _turrets == null || _turrets.Length == 0 || _buildings == null)
                throw new ArgumentException("Invalid battle rules.");
            int lastLevel = 0;
            int lastExperience = -1;
            int lastSlots = 0;
            foreach (BattleLevelRule entry in _levels)
            {
                if (entry.Level != lastLevel + 1 || entry.Experience <= lastExperience || entry.Slots < lastSlots)
                    throw new ArgumentException("Invalid battle level progression.");
                lastLevel = entry.Level;
                lastExperience = entry.Experience;
                lastSlots = entry.Slots;
            }
            var turretIds = new HashSet<string>();
            foreach (TurretRule turret in _turrets)
                if (string.IsNullOrWhiteSpace(turret.Id) || !turretIds.Add(turret.Id) ||
                    turret.MaxLevel < 1 || turret.BaseDamage <= 0 || turret.AttackInterval <= 0 || turret.Range <= 0)
                    throw new ArgumentException("Invalid turret rule.");
            var buildingIds = new HashSet<string>();
            foreach (BuildingTurretRule building in _buildings)
                if (string.IsNullOrWhiteSpace(building.HabitatId) || !buildingIds.Add(building.HabitatId) ||
                    !turretIds.Contains(building.TurretId) || building.Bonus < 0 ||
                    (building.Stat != "damage" && building.Stat != "range" && building.Stat != "interval"))
                    throw new ArgumentException("Invalid building turret rule.");
            if (_economy.HintChance < 0 || _economy.HintChance > 1 ||
                _economy.PassengerChance < 0 || _economy.PassengerChance > 1 ||
                _economy.VictoryMarks <= _economy.DefeatMarks || _economy.DefeatMarks < _economy.RepairMarks)
                throw new ArgumentException("Invalid battle economy.");
            ValidateEnemies();
        }

        /// <summary>
        /// 적 유형과 스테이지 출현 구성의 id 연결과 수치를 검사한다.
        /// _enemies와 _lineups를 사용하며, 잘못된 설정이면 예외를 발생시킨다.
        /// </summary>
        private void ValidateEnemies()
        {
            if (_enemies == null || _enemies.Length == 0 || _lineups == null || _lineups.Length == 0)
                throw new ArgumentException("Invalid battle enemy rules.");
            var enemyIds = new HashSet<string>();
            foreach (EnemyRule enemy in _enemies)
                if (string.IsNullOrWhiteSpace(enemy.Id) || !enemyIds.Add(enemy.Id) || !enemy.IsValid())
                    throw new ArgumentException($"Invalid enemy rule: {enemy.Id}");
            if (_stage.RoamingGuardMin < 0 || _stage.RoamingGuardMax < _stage.RoamingGuardMin)
                throw new ArgumentException("Invalid roaming guard count.");
            if (_stage.RoamingGuardMax > 0 && (Enemy(_stage.RoamingGuardId) == null || Enemy(_stage.RoamingGuardId).Role != BattleEnemyRole.Guard))
                throw new ArgumentException($"Invalid roaming guard: {_stage.RoamingGuardId}");
            int lastStage = 0;
            foreach (BattleStageLineup lineup in _lineups)
            {
                // 첫 구성은 1스테이지부터 시작하고 이후 구성은 시작 스테이지가 늘어나야 한다.
                bool ordered = lastStage == 0 ? lineup.FromStage == 1 : lineup.FromStage > lastStage;
                EnemyRule boss = Enemy(lineup.BossId);
                EnemyRule guard = lineup.GuardCount > 0 ? Enemy(lineup.GuardId) : null;
                if (!ordered || boss == null || boss.Role != BattleEnemyRole.Boss || lineup.GuardCount < 0 ||
                    lineup.GuardsPerStage < 0 || (lineup.GuardCount > 0 && lineup.MaxGuardCount < lineup.GuardCount) ||
                    (lineup.GuardCount > 0 && (guard == null || guard.Role != BattleEnemyRole.Guard)) ||
                    lineup.Soldiers == null || lineup.Soldiers.Count == 0)
                    throw new ArgumentException($"Invalid battle lineup from stage {lineup.FromStage}.");
                foreach (string extraBossId in lineup.ExtraBossIds)
                {
                    EnemyRule extraBoss = Enemy(extraBossId);
                    if (extraBoss == null || extraBoss.Role != BattleEnemyRole.Boss)
                        throw new ArgumentException($"Invalid lineup extra boss: {extraBossId}");
                }
                foreach (EnemyWeight soldier in lineup.Soldiers)
                {
                    EnemyRule rule = Enemy(soldier.EnemyId);
                    if (rule == null || rule.Role != BattleEnemyRole.Soldier || soldier.Weight <= 0)
                        throw new ArgumentException($"Invalid lineup soldier: {soldier.EnemyId}");
                }
                lastStage = lineup.FromStage;
            }
        }
    }

    /// <summary>적 유형이 전장에서 맡는 역할이다.</summary>
    public enum BattleEnemyRole { Soldier, Guard, Boss }

    /// <summary>적 유형별 체력·이동·돌진·원거리 공격·외형 배율이다.</summary>
    [Serializable]
    public sealed class EnemyRule
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private string _role;
        [SerializeField] private string _spriteId;
        [SerializeField] private float _health;
        [SerializeField] private float _speed;
        [SerializeField] private int _contactDamage;
        [SerializeField] private int _experience;
        [SerializeField] private float _scale;
        [SerializeField] private Color _tint;
        [SerializeField] private float _damageTaken;
        [SerializeField] private float _chargeAtProgress;
        [SerializeField] private float _chargeSpeed;
        [SerializeField] private float _attackRange;
        [SerializeField] private int _attackDamage;
        [SerializeField] private float _attackInterval;
        [SerializeField] private float _attackProjectileSpeed;
        public string Id => _id;
        public string Name => _name;
        public BattleEnemyRole Role => _role == "boss" ? BattleEnemyRole.Boss : _role == "guard" ? BattleEnemyRole.Guard : BattleEnemyRole.Soldier;
        // 다른 유형과 같은 그림을 쓸 때 그 유형 id를 적고, 비어 있으면 자기 id의 그림을 쓴다.
        public string SpriteId => string.IsNullOrEmpty(_spriteId) ? _id : _spriteId;
        public float Health => _health;
        public float Speed => _speed;
        public int ContactDamage => _contactDamage;
        public int Experience => _experience;
        public float Scale => _scale;
        public Color Tint => _tint;
        public float DamageTaken => _damageTaken;
        public float ChargeAtProgress => _chargeAtProgress;
        public float ChargeSpeed => _chargeSpeed;
        public float AttackRange => _attackRange;
        public int AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float AttackProjectileSpeed => _attackProjectileSpeed;

        /// <summary>
        /// 역할 문자열과 수치가 사용 가능한 범위인지 검사한다.
        /// 모든 필드를 사용하며, 유효하면 true를 반환한다.
        /// </summary>
        public bool IsValid()
        {
            bool role = _role == "soldier" || _role == "guard" || _role == "boss";
            // 돌진 지점은 출현 위치(0)에서 열차(1)까지 경로의 비율이며 0이면 돌진하지 않는다.
            bool charge = _chargeAtProgress == 0 || (_chargeAtProgress > 0 && _chargeAtProgress < 1 && _chargeSpeed > 0);
            bool attack = _attackRange == 0 || (_attackRange > 0 && _attackDamage > 0 && _attackInterval > 0 && _attackProjectileSpeed > 0);
            return role && charge && attack && _health > 0 && _speed > 0 && _contactDamage >= 0 && _scale > 0 && _damageTaken > 0;
        }
    }

    /// <summary>스테이지 구간마다 나오는 일반 적 가중치와 보스·방패수호병 구성이다.</summary>
    [Serializable]
    public sealed class BattleStageLineup
    {
        [SerializeField] private int _fromStage;
        [SerializeField] private EnemyWeight[] _soldiers;
        [SerializeField] private string _bossId;
        [SerializeField] private string _guardId;
        [SerializeField] private int _guardCount;
        [SerializeField] private int _guardsPerStage;
        [SerializeField] private int _maxGuardCount;
        [SerializeField] private string[] _extraBossIds;
        public int FromStage => _fromStage;
        public IReadOnlyList<string> ExtraBossIds => _extraBossIds ?? Array.Empty<string>();
        public IReadOnlyList<EnemyWeight> Soldiers => _soldiers;
        public string BossId => _bossId;
        public string GuardId => _guardId;
        public int GuardCount => _guardCount;
        public int GuardsPerStage => _guardsPerStage;
        public int MaxGuardCount => _maxGuardCount;

        /// <summary>
        /// 스테이지에 맞는 방패수호병 수를 계산한다.
        /// stage와 시작 수·스테이지당 증가 수·최대 수를 사용하며, 구성의 시작 스테이지에서 _guardCount인 값을 반환한다.
        /// </summary>
        public int GuardCountAt(int stage)
        {
            if (_guardCount <= 0) return 0;
            int count = _guardCount + _guardsPerStage * Mathf.Max(0, stage - _fromStage);
            return Mathf.Min(count, _maxGuardCount);
        }

        /// <summary>
        /// 가중치에 따라 이번에 출현할 일반 적 id를 고른다.
        /// random을 사용하며, 선택한 적 id를 반환한다.
        /// </summary>
        public string PickSoldier(System.Random random)
        {
            float total = 0;
            foreach (EnemyWeight soldier in _soldiers) total += soldier.Weight;
            float roll = (float)random.NextDouble() * total;
            foreach (EnemyWeight soldier in _soldiers)
            {
                roll -= soldier.Weight;
                if (roll < 0) return soldier.EnemyId;
            }
            return _soldiers[_soldiers.Length - 1].EnemyId;
        }
    }

    /// <summary>출현 구성에서 일반 적 하나의 id와 출현 가중치다.</summary>
    [Serializable]
    public sealed class EnemyWeight
    {
        [SerializeField] private string _enemyId;
        [SerializeField] private float _weight;
        public string EnemyId => _enemyId;
        public float Weight => _weight;
    }

    /// <summary>전투 POC의 적 출현 간격·보스 출현 시각·탄 속도·열차 내구도를 조절한다. 적 수치는 EnemyRule에 둔다.</summary>
    [Serializable]
    public sealed class BattleStageRule
    {
        [SerializeField] private int _trainHealth;
        [SerializeField] private float _spawnIntervalSeconds;
        [SerializeField] private float _bossSpawnSeconds;
        [SerializeField] private float _projectileSpeed;
        [SerializeField] private float _healthGrowthPerStage;
        [SerializeField] private float _spawnGrowthPerStage;
        [SerializeField] private float _minSpawnIntervalSeconds;
        [SerializeField] private string _roamingGuardId;
        [SerializeField] private int _roamingGuardMin;
        [SerializeField] private int _roamingGuardMax;
        public int TrainHealth => _trainHealth;
        public string RoamingGuardId => _roamingGuardId;
        public int RoamingGuardMin => _roamingGuardMin;
        public int RoamingGuardMax => _roamingGuardMax;
        public float SpawnIntervalSeconds => _spawnIntervalSeconds;
        public float BossSpawnSeconds => _bossSpawnSeconds;
        public float ProjectileSpeed => _projectileSpeed;
        public float HealthGrowthPerStage => _healthGrowthPerStage;
        public float SpawnGrowthPerStage => _spawnGrowthPerStage;
        public float MinSpawnIntervalSeconds => _minSpawnIntervalSeconds;

        /// <summary>
        /// 스테이지에 맞는 적 체력 배율을 계산한다.
        /// stage와 _healthGrowthPerStage를 사용하며, 1스테이지에서 1인 배율을 반환한다.
        /// </summary>
        public float HealthMultiplierAt(int stage)
        {
            return 1f + _healthGrowthPerStage * Mathf.Max(0, stage - 1);
        }

        /// <summary>
        /// 스테이지에 맞는 일반 적 출현 간격을 계산한다.
        /// stage와 출현 증가율, 최소 간격을 사용하며, 출현 수가 늘어난 만큼 줄어든 간격을 반환한다.
        /// </summary>
        public float SpawnIntervalAt(int stage)
        {
            float rate = 1f + _spawnGrowthPerStage * Mathf.Max(0, stage - 1);
            return Mathf.Max(_minSpawnIntervalSeconds, _spawnIntervalSeconds / rate);
        }
    }

    /// <summary>클릭 포격 주포의 기본 피해·범위·쿨타임과 레벨당 증가량이다.</summary>
    [Serializable]
    public sealed class MainGunRule
    {
        [SerializeField] private float _damage;
        [SerializeField] private float _cooldownSeconds;
        [SerializeField] private float _blastRadius;
        [SerializeField] private int _maxLevel;
        [SerializeField] private float _damagePerLevel;
        [SerializeField] private float _radiusPerLevel;
        public float Damage => _damage;
        public float CooldownSeconds => _cooldownSeconds;
        public float BlastRadius => _blastRadius;
        public int MaxLevel => _maxLevel;
        public float DamagePerLevel => _damagePerLevel;
        public float RadiusPerLevel => _radiusPerLevel;
    }

    /// <summary>경험치 경계와 그 레벨에서 허용하는 포탑 칸 수다.</summary>
    [Serializable]
    public sealed class BattleLevelRule
    {
        [SerializeField] private int _level;
        [SerializeField] private int _experience;
        [SerializeField] private int _slots;
        public int Level => _level;
        public int Experience => _experience;
        public int Slots => _slots;
    }

    /// <summary>승패 마르크와 설계도 힌트·승객 보상의 확률이다.</summary>
    [Serializable]
    public sealed class BattleEconomyRule
    {
        [SerializeField] private int _victoryMarks;
        [SerializeField] private int _defeatMarks;
        [SerializeField] private int _repairMarks;
        [SerializeField] private int _duplicateHintMarks;
        [SerializeField] private int _turretInstallMarks;
        [SerializeField] private int _turretUpgradeMarks;
        [SerializeField] private float _hintChance;
        [SerializeField] private float _passengerChance;
        public int VictoryMarks => _victoryMarks;
        public int DefeatMarks => _defeatMarks;
        public int RepairMarks => _repairMarks;
        public int DuplicateHintMarks => _duplicateHintMarks;
        public int TurretInstallMarks => _turretInstallMarks;
        public int TurretUpgradeMarks => _turretUpgradeMarks;
        public float HintChance => _hintChance;
        public float PassengerChance => _passengerChance;
    }

    /// <summary>자동 포탑의 공격 기본값과 매판 레벨당 피해 증가량이다.</summary>
    [Serializable]
    public sealed class TurretRule
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private float _baseDamage;
        [SerializeField] private float _attackInterval;
        [SerializeField] private float _range;
        [SerializeField] private int _maxLevel;
        [SerializeField] private float _damagePerLevel;
        [SerializeField] private bool _starting;
        public string Id => _id;
        public string Name => _name;
        public float BaseDamage => _baseDamage;
        public float AttackInterval => _attackInterval;
        public float Range => _range;
        public int MaxLevel => _maxLevel;
        public float DamagePerLevel => _damagePerLevel;
        public bool Starting => _starting;
    }

    /// <summary>완성 건물이 카드에 추가하는 포탑과 승객 합류 시 두 배가 되는 능력치 강화량이다.</summary>
    [Serializable]
    public sealed class BuildingTurretRule
    {
        [SerializeField] private string _habitatId;
        [SerializeField] private string _turretId;
        [SerializeField] private string _stat;
        [SerializeField] private float _bonus;
        public string HabitatId => _habitatId;
        public string TurretId => _turretId;
        public string Stat => _stat;
        public float Bonus => _bonus;
    }
}
