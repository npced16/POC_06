using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Combat
{
    /// <summary>전투 레벨업 카드가 설치·포탑 강화·주포 강화 중 무엇을 제공하는지 나타낸다.</summary>
    public enum BattleCardKind { Install, UpgradeTurret, UpgradeMainGun }

    /// <summary>카드 세 장 중 하나의 효과와 설치·강화 대상이다.</summary>
    public sealed class BattleCard
    {
        public BattleCardKind Kind { get; }
        public string TurretId { get; }
        public int Slot { get; }

        /// <summary>카드 종류와 포탑 id·칸을 저장해 선택 시 적용할 효과를 만든다.</summary>
        public BattleCard(BattleCardKind kind, string turretId, int slot)
        {
            Kind = kind;
            TurretId = turretId;
            Slot = slot;
        }
    }

    /// <summary>한 판에서 칸에 놓인 포탑 종류와 현재 강화 레벨이다.</summary>
    public sealed class BattleTurret
    {
        public string TurretId { get; }
        public int Level { get; private set; }

        /// <summary>설치 카드의 포탑 id를 받아 1레벨 포탑 상태를 만든다.</summary>
        public BattleTurret(string turretId)
        {
            TurretId = turretId;
            Level = 1;
        }

        /// <summary>강화 카드의 선택을 반영해 포탑 레벨을 한 단계 올린다.</summary>
        public void Upgrade() => Level++;
    }

    /// <summary>건설한 서식지와 합류 승객의 효과를 현재 전투 카드·포탑 수치에 적용한다.</summary>
    public sealed class BattleLoadout
    {
        private readonly BattleRules _rules;
        private readonly HashSet<string> _built;
        private readonly HashSet<string> _joined;

        /// <summary>건설한 habitatIds와 승객이 합류한 habitatIds를 복사해 한 판의 성장 상태를 고정한다.</summary>
        public BattleLoadout(BattleRules rules, IEnumerable<string> habitatIds, IEnumerable<string> joinedHabitatIds)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _built = new HashSet<string>(habitatIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            _joined = new HashSet<string>(joinedHabitatIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        /// <summary>건설된 건물의 포탑과 기본 포탑만 카드 후보로 반환한다.</summary>
        public IReadOnlyList<TurretRule> AvailableTurrets()
        {
            var list = new List<TurretRule>();
            foreach (TurretRule turret in _rules.Turrets)
            {
                bool available = turret.Starting;
                foreach (BuildingTurretRule building in _rules.Buildings)
                    if (building.TurretId == turret.Id && _built.Contains(building.HabitatId)) available = true;
                if (available) list.Add(turret);
            }
            return list;
        }

        /// <summary>포탑 id와 능력치 이름을 받아 건물 보너스의 합을 반환하고 합류 승객의 보너스를 두 배로 센다.</summary>
        public float Bonus(string turretId, string stat)
        {
            float total = 0;
            foreach (BuildingTurretRule building in _rules.Buildings)
            {
                if (building.TurretId != turretId || building.Stat != stat || !_built.Contains(building.HabitatId)) continue;
                total += building.Bonus * (_joined.Contains(building.HabitatId) ? 2 : 1);
            }
            return total;
        }
    }

    /// <summary>매판 초기화되는 카드·포탑·주포·레벨 상태를 관리한다.</summary>
    public sealed class BattleRun
    {
        private readonly BattleRules _rules;
        private readonly BattleLoadout _loadout;
        private readonly System.Random _random;
        private readonly BattleTurret[] _slots;
        private readonly List<BattleCard> _cards = new List<BattleCard>();
        private int _experience;
        private int _level = 1;
        private int _pendingLevels;
        private int _mainGunLevel = 1;
        private float _time;
        private float _lastMainShot = float.NegativeInfinity;

        public int Level => _level;
        public int Experience => _experience;
        public int MainGunLevel => _mainGunLevel;
        public int SlotCount => _rules.SlotsAt(_level);
        public IReadOnlyList<BattleCard> Cards => _cards;
        public IReadOnlyList<BattleTurret> Turrets => _slots;
        public bool HasCardChoice => _cards.Count > 0;
        public string PendingTurretId { get; private set; }
        public bool IsPlacingTurret => PendingTurretId != null;
        public float Time => _time;
        public float MainGunCooldownLeft => Mathf.Max(0, _rules.MainGun.CooldownSeconds - (_time - _lastMainShot));

        /// <summary>규칙과 마을 성장 상태로 새 판을 만들고 카드 선택·설치·주포 강화 상태를 초기화한다.</summary>
        public BattleRun(BattleRules rules, BattleLoadout loadout, int seed)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _random = new System.Random(seed);
            _slots = new BattleTurret[_rules.Levels[_rules.Levels.Count - 1].Slots];
        }

        /// <summary>deltaSeconds만큼 전투 시간을 진행해 주포 쿨타임 계산에 반영한다.</summary>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds > 0 && !HasCardChoice && !IsPlacingTurret) _time += deltaSeconds;
        }

        /// <summary>적 처치 경험치를 더하고 넘은 레벨 수만큼 순서대로 카드 선택을 준비한다.</summary>
        public void GainExperience(int amount)
        {
            if (amount <= 0) return;
            _experience += amount;
            while (_rules.ExperienceAt(_level + 1) < int.MaxValue && _experience >= _rules.ExperienceAt(_level + 1))
            {
                _level++;
                _pendingLevels++;
            }
            if (!HasCardChoice) DrawCards();
        }

        /// <summary>전장 클릭 위치로 쏠 수 있으면 주포 쿨타임을 시작하고 현재 피해와 폭발 반경을 반환한다.</summary>
        public bool TryFireMainGun(Vector2 target, out float damage, out float radius)
        {
            damage = 0;
            radius = 0;
            if (HasCardChoice || IsPlacingTurret || MainGunCooldownLeft > 0) return false;
            _lastMainShot = _time;
            damage = _rules.MainGun.Damage + (_mainGunLevel - 1) * _rules.MainGun.DamagePerLevel;
            radius = _rules.MainGun.BlastRadius + (_mainGunLevel - 1) * _rules.MainGun.RadiusPerLevel;
            return true;
        }

        /// <summary>카드 번호를 적용해 해당 칸 설치 또는 포탑·주포 강화를 수행하고 다음 레벨 카드가 있으면 연다.</summary>
        public bool ChooseCard(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= _cards.Count) return false;
            BattleCard card = _cards[cardIndex];
            if (card.Kind == BattleCardKind.Install) PendingTurretId = card.TurretId;
            else if (card.Kind == BattleCardKind.UpgradeTurret) _slots[card.Slot].Upgrade();
            else _mainGunLevel++;
            _cards.Clear();
            _pendingLevels--;
            if (!IsPlacingTurret) DrawCards();
            return true;
        }

        /// <summary>선택한 빈 slot에 대기 중인 포탑을 설치하고 다음 레벨 카드를 준비한다.</summary>
        public bool PlaceTurret(int slot)
        {
            int installed = 0;
            foreach (BattleTurret turret in _slots) if (turret != null) installed++;
            if (!IsPlacingTurret || slot < 0 || slot >= SlotCount || slot >= _slots.Length) return false;
            BattleTurret existing = _slots[slot];
            if (existing == null && installed >= SlotCount) return false;
            if (existing != null && existing.TurretId == PendingTurretId)
            {
                existing.Upgrade();
            }
            else _slots[slot] = new BattleTurret(PendingTurretId);
            PendingTurretId = null;
            DrawCards();
            return true;
        }
        /// <summary>이전 승리에서 보관한 포탑 ID를 각 칸에 복원하고 전투용 1레벨 상태를 만든다.</summary>
        public void RestoreTurrets(string[] turretIds)
        {
            for (int i = 0; i < _slots.Length && i < turretIds.Length; i++)
                if (!string.IsNullOrEmpty(turretIds[i]) && _rules.Turret(turretIds[i]) != null)
                    _slots[i] = new BattleTurret(turretIds[i]);
        }

        /// <summary>거점에 저장한 포탑 종류와 강화 단계를 각 거치대의 전투 상태에 반영한다.</summary>
        public void RestoreArmament()
        {
            for (int i = 0; i < TrainArmament.SLOT_COUNT && i < _slots.Length; i++)
            {
                string id = TrainArmament.TurretId(i);
                if (_rules.Turret(id) == null) continue;
                _slots[i] = new BattleTurret(id);
                for (int level = 1; level < TrainArmament.Level(i); level++) _slots[i].Upgrade();
            }
        }
        /// <summary>칸에 설치된 포탑의 현재 피해·사거리·공격 간격을 마을 보너스와 함께 계산한다.</summary>
        public bool TurretStats(int slot, out float damage, out float range, out float interval)
        {
            damage = range = interval = 0;
            if (slot < 0 || slot >= _slots.Length || _slots[slot] == null) return false;
            BattleTurret placed = _slots[slot];
            TurretRule rule = _rules.Turret(placed.TurretId);
            damage = rule.BaseDamage + (placed.Level - 1) * rule.DamagePerLevel + _loadout.Bonus(rule.Id, "damage");
            range = rule.Range + _loadout.Bonus(rule.Id, "range");
            interval = Mathf.Max(0.1f, rule.AttackInterval - _loadout.Bonus(rule.Id, "interval"));
            return true;
        }

        /// <summary>현재 설치 가능 칸과 해금 포탑으로 유효한 후보를 만든 뒤 세 장을 무작위로 제시한다.</summary>
        private void DrawCards()
        {
            if (_pendingLevels <= 0) return;
            var candidates = new List<BattleCard>();
            foreach (TurretRule turret in _loadout.AvailableTurrets())
            {
                bool canPlace = false;
                int installed = 0;
                foreach (BattleTurret placed in _slots) if (placed != null) installed++;
                foreach (BattleTurret placed in _slots)
                    if ((placed == null && installed < SlotCount) || placed != null) canPlace = true;
                if (canPlace) candidates.Add(new BattleCard(BattleCardKind.Install, turret.Id, -1));
            }
            while (_cards.Count < 3 && candidates.Count > 0)
            {
                int index = _random.Next(candidates.Count);
                _cards.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            while (_cards.Count < 3 && _cards.Count > 0)
                _cards.Add(_cards[_random.Next(_cards.Count)]);
            if (_cards.Count == 0) _pendingLevels = 0;
        }
    }
}
