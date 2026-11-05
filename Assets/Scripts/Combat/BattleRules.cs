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

        public float TargetDurationSeconds => _targetDurationSeconds;
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
                _stage.BossSpawnSeconds >= _targetDurationSeconds || _mainGun == null || _mainGun.CooldownSeconds <= 0 ||
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
        }
    }

    /// <summary>전투 POC의 적 출현·체력·속도·경험치·열차 내구도를 조절한다.</summary>
    [Serializable]
    public sealed class BattleStageRule
    {
        [SerializeField] private int _trainHealth;
        [SerializeField] private float _enemyHealth;
        [SerializeField] private float _enemySpeed;
        [SerializeField] private int _enemyContactDamage;
        [SerializeField] private int _enemyExperience;
        [SerializeField] private float _spawnIntervalSeconds;
        [SerializeField] private float _bossSpawnSeconds;
        [SerializeField] private float _bossHealth;
        [SerializeField] private float _bossSpeed;
        [SerializeField] private int _bossContactDamage;
        [SerializeField] private int _bossExperience;
        [SerializeField] private float _projectileSpeed;
        public int TrainHealth => _trainHealth;
        public float EnemyHealth => _enemyHealth;
        public float EnemySpeed => _enemySpeed;
        public int EnemyContactDamage => _enemyContactDamage;
        public int EnemyExperience => _enemyExperience;
        public float SpawnIntervalSeconds => _spawnIntervalSeconds;
        public float BossSpawnSeconds => _bossSpawnSeconds;
        public float BossHealth => _bossHealth;
        public float BossSpeed => _bossSpeed;
        public int BossContactDamage => _bossContactDamage;
        public int BossExperience => _bossExperience;
        public float ProjectileSpeed => _projectileSpeed;
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
