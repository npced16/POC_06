using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Combat.Raid
{
    /// <summary>
    /// 스토리 033 규칙. 점령된 종점을 통과하는 실시간 습격: 둥지 스폰, 몬스터 이동·접촉 피해, 포탑 자동 사격, 플레이어 대포,
    /// 내구도, 승패, 둥지 파괴·해방. 시간은 Tick(dt)로만 흐르고 난수는 데이터 시드의 LCG라 같은 입력이면 같은 결과다.
    /// 결과는 WaypointResolver.CompleteRaid로 넘겨 번영·승객·도감이 기존 EncounterEnded 신호를 그대로 받는다.
    /// 좌표는 월드 좌표: 표현이 SetOrigin(종점 위치)과 TrainPosition(열차 위치)을 넣는다(없으면 가상 직선 구간).
    /// </summary>
    public sealed class RaidSystem : IRaid, IRaidRunner
    {
        private readonly RaidData _data;
        private readonly WaypointResolver _resolver;
        private readonly Func<IRaidLoadout> _loadout;
        private readonly ITrackRuntime _track;
        private readonly List<RaidUnit> _monsters = new List<RaidUnit>();
        private readonly List<RaidUnit> _nests = new List<RaidUnit>();
        private readonly Dictionary<int, RaidMonsterData> _kinds = new Dictionary<int, RaidMonsterData>();
        private float[] _spawnTimers = Array.Empty<float>();
        private float[] _turretTimers = Array.Empty<float>();
        private uint _state;
        private float _originX, _originY;
        private float _elapsed;
        private float _playerTimer;
        private int _nextId;
        private int _turrets;
        private int _heavyTurrets;
        private float _segment;
        private float _turretPeriod;
        private float _heavyPeriod;
        private int _turretDamage;
        private int _heavyDamage;
        private float _repairPerSecond;
        private float _repairCarry;

        /// <summary>표현이 넣는 열차 월드 위치. null이면 종점을 가로지르는 가상 직선 위 위치를 쓴다.</summary>
        public Func<(float x, float y)> TrainPosition { get; set; }
        /// <summary>데모 전용: 방벽 수 강제(null = 시설 빌더 값).</summary>
        public int? OverrideWalls { get; set; }
        /// <summary>데모 전용: 포탑 수 강제(null = 시설 빌더 값).</summary>
        public int? OverrideTurrets { get; set; }
        /// <summary>데모 전용: 최대 내구도 강제(null = 공식).</summary>
        public int? OverrideHullMax { get; set; }
        /// <summary>읽은 데이터.</summary>
        public RaidData Data => _data;
        /// <summary>습격 시작 뒤 지난 초.</summary>
        public float Elapsed => _elapsed;
        /// <summary>이번 습격에서 쓰는 포탑 수.</summary>
        public int Turrets => _turrets;
        /// <summary>이번 습격에서 쓰는 중포탑 수.</summary>
        public int HeavyTurrets => _heavyTurrets;
        /// <summary>이번 습격의 구간 통과 시간(데이터 ÷ (1 + 속도 가산)).</summary>
        public float SegmentSeconds => _segment > 0 ? _segment : _data.SegmentSeconds;
        /// <summary>플레이어 대포 재장전 남은 초.</summary>
        public float PlayerCooldownLeft => Math.Max(0f, _playerTimer);

        public bool IsActive { get; private set; }
        public float Progress => Math.Min(1f, _elapsed / SegmentSeconds);
        public int Hull { get; private set; }
        public int HullMax { get; private set; }
        public int MonstersAlive => _monsters.Count;
        public int NestsAlive { get { int n = 0; foreach (RaidUnit nest in _nests) if (nest.Alive) n++; return n; } }
        public bool TerminalLiberated { get; private set; }
        public float BoardChanceBonus => TerminalLiberated ? _data.LiberatedBoardBonus : 0f;
        public IReadOnlyList<RaidUnit> Monsters => _monsters;
        public IReadOnlyList<RaidUnit> Nests => _nests;
        public event Action RaidStarted;
        public event Action<bool> RaidEnded;
        public event Action<int> MonsterSpawned;
        public event Action<int> MonsterKilled;
        public event Action<int> MonsterReached;
        public event Action<int> NestDestroyed;
        public event Action HullChanged;
        public event Action<bool, float, float> ShotFired;
        /// <summary>스토리 034. 포탑 발사(포 순번: 0..Turrets-1 = 포탑, 그 뒤 = 중포, 맞은 지점 X, Y). ShotFired(false, …)보다 먼저 온다.</summary>
        public event Action<int, float, float> GunFired;
        /// <summary>스토리 034. 이번 습격의 초당 수리량.</summary>
        public float RepairPerSecond => _repairPerSecond;

        /// <summary>
        /// 데이터, 결과를 받을 resolver, 습격 시작마다 장비(포탑·중포탑·방벽판·속도)를 읽을 loadout 조회 함수(034가 제공자를 바꿀 수 있어 늦게 얻는다),
        /// 바퀴 수를 읽을 선로를 주입한다. 둥지는 런 동안 유지된다.
        /// </summary>
        public RaidSystem(RaidData data, WaypointResolver resolver, Func<IRaidLoadout> loadout, ITrackRuntime track)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _resolver = resolver;
            _loadout = loadout;
            _track = track;
            _state = unchecked((uint)data.Seed);
            for (int i = 0; i < data.NestOffsets.Length; i++)
                _nests.Add(new RaidUnit { Id = i, Kind = "nest", Hp = data.NestHp, HpMax = data.NestHp, X = data.NestOffsets[i].x, Y = data.NestOffsets[i].y });
        }

        /// <summary>종점(점령 거점)의 월드 위치를 정한다. 둥지가 그 둘레(데이터 오프셋)로 옮겨진다.</summary>
        public void SetOrigin(float x, float y)
        {
            _originX = x; _originY = y;
            for (int i = 0; i < _nests.Count; i++) { _nests[i].X = x + _data.NestOffsets[i].x; _nests[i].Y = y + _data.NestOffsets[i].y; }
        }

        /// <summary>node가 아직 점령된 경유지(데이터 _waypointId, 해방 전)인지.</summary>
        public bool IsOccupied(ITrackNode node)
        {
            if (TerminalLiberated || node == null) return false;
            return string.IsNullOrEmpty(_data.WaypointId) || node.Id == _data.WaypointId;
        }

        /// <summary>습격을 시작한다: 내구도를 (기본 + 방벽 × 방벽당)으로 채우고 둥지 스폰 시계와 포탑 시계를 맞춘다.</summary>
        public void Begin(ITrackNode node)
        {
            IRaidLoadout loadout = _loadout?.Invoke();
            int walls = Math.Max(0, OverrideWalls ?? loadout?.ArmorPlates ?? 0);
            _turrets = Math.Max(0, OverrideTurrets ?? loadout?.Turrets ?? 0);
            _heavyTurrets = OverrideTurrets.HasValue ? 0 : Math.Max(0, loadout?.HeavyTurrets ?? 0);
            _segment = _data.SegmentSeconds / (1f + Math.Max(0f, loadout?.SpeedBonus ?? 0f));
            // 스토리 034: 제공자가 장비 수치(IRaidLoadoutStats)를 주면 그것을, 아니면 Raid.json 값을 쓴다.
            var stats = OverrideWalls.HasValue || OverrideTurrets.HasValue ? null : loadout as IRaidLoadoutStats;
            int hullBonus = stats != null ? stats.HullBonus : walls * _data.HullPerWall;
            float turretRate = stats != null ? stats.TurretShotsPerSecond : _data.TurretShotsPerSecond;
            float heavyRate = stats != null ? stats.HeavyTurretShotsPerSecond : _data.TurretShotsPerSecond;
            _turretPeriod = turretRate > 0 ? 1f / turretRate : float.MaxValue;
            _heavyPeriod = heavyRate > 0 ? 1f / heavyRate : float.MaxValue;
            _turretDamage = stats != null ? stats.TurretDamage : _data.TurretDamage;
            _heavyDamage = stats != null ? stats.HeavyTurretDamage : _data.HeavyTurretDamage;
            _repairPerSecond = stats != null ? Math.Max(0f, stats.RepairPerSecond) : 0f;
            _repairCarry = 0f;
            HullMax = Math.Max(1, OverrideHullMax ?? _data.HullBase + hullBonus);
            Hull = HullMax;
            _elapsed = 0f; _playerTimer = 0f;
            _monsters.Clear(); _kinds.Clear();
            _spawnTimers = new float[_nests.Count];
            for (int i = 0; i < _spawnTimers.Length; i++) _spawnTimers[i] = _data.FirstSpawnDelay + i * _data.SpawnStagger;
            int guns = _turrets + _heavyTurrets;
            _turretTimers = new float[guns];
            for (int i = 0; i < guns; i++) _turretTimers[i] = (i < _turrets ? _turretPeriod : _heavyPeriod) * (i + 1) / (guns + 1);
            IsActive = true;
            UnityEngine.Debug.Log($"RAID: start hull={Hull}/{HullMax} walls={walls} turrets={_turrets}+{_heavyTurrets} repair={_repairPerSecond:F2}/s segment={SegmentSeconds:F1}s nests={NestsAlive} lap={_track?.Lap ?? 0}");
            RaidStarted?.Invoke();
            HullChanged?.Invoke();
        }

        /// <summary>dt초만큼 습격을 진행한다(스폰 → 이동·접촉 → 포탑 → 승패). 습격 중이 아니면 아무것도 하지 않는다.</summary>
        public void Tick(float dt)
        {
            if (!IsActive || dt <= 0f) return;
            _elapsed += dt;
            _playerTimer -= dt;
            Spawn(dt);
            Move(dt);
            if (!IsActive) return;
            FireTurrets(dt);
            Repair(dt);
            if (Hull <= 0) End(false);
            else if (_elapsed >= SegmentSeconds) End(true);
        }

        public bool ShootMonster(int monsterId)
        {
            if (!IsActive || _playerTimer > 0f) return false;
            RaidUnit target = _monsters.Find(m => m.Id == monsterId);
            if (target == null) return false;
            _playerTimer = _data.PlayerCooldown;
            ShotFired?.Invoke(true, target.X, target.Y);
            DamageMonster(target, _data.PlayerDamage, "cannon");
            return true;
        }

        public bool ShootNest(int nestIndex)
        {
            if (!IsActive || _playerTimer > 0f || nestIndex < 0 || nestIndex >= _nests.Count || !_nests[nestIndex].Alive) return false;
            RaidUnit nest = _nests[nestIndex];
            _playerTimer = _data.PlayerCooldown;
            ShotFired?.Invoke(true, nest.X, nest.Y);
            nest.Hp = Math.Max(0, nest.Hp - _data.PlayerDamage);
            if (nest.Alive) return true;
            UnityEngine.Debug.Log($"RAID: nest {nestIndex} destroyed, left={NestsAlive}");
            NestDestroyed?.Invoke(nestIndex);
            if (NestsAlive == 0)
            {
                TerminalLiberated = true;
                UnityEngine.Debug.Log("RAID: terminal liberated");
            }
            return true;
        }

        /// <summary>월드 점 (x, y)에서 반경 안의 가장 가까운 몬스터 id(없으면 -1). 클릭이 콜라이더를 비켜 갔을 때 쓴다.</summary>
        public int NearestMonster(float x, float y, float radius)
        {
            int best = -1; float bestDistance = radius * radius;
            foreach (RaidUnit monster in _monsters)
            {
                float d = (monster.X - x) * (monster.X - x) + (monster.Y - y) * (monster.Y - y);
                if (d <= bestDistance) { bestDistance = d; best = monster.Id; }
            }
            return best;
        }

        /// <summary>몬스터 id의 종류 정의(없으면 null).</summary>
        public RaidMonsterData KindOf(int monsterId) => _kinds.TryGetValue(monsterId, out RaidMonsterData kind) ? kind : null;

        private (float x, float y) Train()
        {
            if (TrainPosition != null) return TrainPosition();
            float half = _data.TrainFallbackLength * 0.5f;
            return (_originX - half + _data.TrainFallbackLength * Progress, _originY);
        }

        private void Spawn(float dt)
        {
            RaidWave wave = _data.WaveFor(_track?.Lap ?? 0);
            for (int i = 0; i < _nests.Count; i++)
            {
                if (!_nests[i].Alive) continue;
                _spawnTimers[i] -= dt;
                if (_spawnTimers[i] > 0f) continue;
                float jitter = 1f + (Next() * 2f - 1f) * _data.SpawnJitter;
                _spawnTimers[i] += Math.Max(0.1f, wave.SpawnInterval * jitter);
                if (_monsters.Count >= wave.MaxAlive) continue;
                RaidMonsterData kind = _data.Monster(Next() < wave.AutomatonChance ? "automaton" : "spider");
                var monster = new RaidUnit
                {
                    Id = _nextId++, Kind = kind.Id, Hp = kind.Hp, HpMax = kind.Hp,
                    X = _nests[i].X + (Next() - 0.5f) * 0.4f, Y = _nests[i].Y + (Next() - 0.5f) * 0.4f,
                };
                _monsters.Add(monster);
                _kinds[monster.Id] = kind;
                MonsterSpawned?.Invoke(monster.Id);
            }
        }

        private void Move(float dt)
        {
            (float tx, float ty) = Train();
            for (int i = _monsters.Count - 1; i >= 0; i--)
            {
                RaidUnit monster = _monsters[i];
                RaidMonsterData kind = _kinds[monster.Id];
                float dx = tx - monster.X, dy = ty - monster.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = kind.Speed * dt;
                if (distance - step <= _data.ContactRadius)
                {
                    _monsters.RemoveAt(i);
                    Hull = Math.Max(0, Hull - kind.Damage);
                    UnityEngine.Debug.Log($"RAID: hit {kind.Id}#{monster.Id} -{kind.Damage} hull={Hull}/{HullMax}");
                    MonsterReached?.Invoke(monster.Id);
                    HullChanged?.Invoke();
                    continue;
                }
                monster.X += dx / distance * step;
                monster.Y += dy / distance * step;
            }
        }

        private void FireTurrets(float dt)
        {
            if (_turretTimers.Length == 0) return;
            (float tx, float ty) = Train();
            for (int i = 0; i < _turretTimers.Length; i++)
            {
                _turretTimers[i] -= dt;
                if (_turretTimers[i] > 0f) continue;
                RaidUnit target = null; float best = _data.TurretRange * _data.TurretRange;
                foreach (RaidUnit monster in _monsters)
                {
                    float d = (monster.X - tx) * (monster.X - tx) + (monster.Y - ty) * (monster.Y - ty);
                    if (d <= best) { best = d; target = monster; }
                }
                if (target == null) { _turretTimers[i] = 0f; continue; } // 대상이 나타나면 바로 쏜다
                bool heavy = i >= _turrets;
                _turretTimers[i] += heavy ? _heavyPeriod : _turretPeriod;
                GunFired?.Invoke(i, target.X, target.Y);
                ShotFired?.Invoke(false, target.X, target.Y);
                DamageMonster(target, heavy ? _heavyDamage : _turretDamage, heavy ? "heavy-turret" : "turret");
            }
        }

        /// <summary>스토리 034. 수리 장치: 초당 _repairPerSecond를 모아 1이 될 때마다 내구도 1을 채운다(최대치까지).</summary>
        private void Repair(float dt)
        {
            if (_repairPerSecond <= 0f || Hull <= 0) return;
            if (Hull >= HullMax) { _repairCarry = 0f; return; }
            _repairCarry += _repairPerSecond * dt;
            if (_repairCarry < 1f) return;
            int heal = Math.Min(HullMax - Hull, (int)_repairCarry);
            _repairCarry -= heal;
            Hull += heal;
            UnityEngine.Debug.Log($"RAID: repair +{heal} hull={Hull}/{HullMax}");
            HullChanged?.Invoke();
        }

        private void DamageMonster(RaidUnit monster, int damage, string source)
        {
            monster.Hp -= damage;
            if (monster.Alive) return;
            _monsters.Remove(monster);
            UnityEngine.Debug.Log($"RAID: kill {monster.Kind}#{monster.Id} by {source}");
            MonsterKilled?.Invoke(monster.Id);
        }

        private void End(bool victory)
        {
            IsActive = false;
            _monsters.Clear();
            UnityEngine.Debug.Log($"RAID: end victory={victory} hull={Hull}/{HullMax} nests={NestsAlive} liberated={TerminalLiberated} t={_elapsed:F1}");
            RaidEnded?.Invoke(victory);
            _resolver?.CompleteRaid(victory);
        }

        /// <summary>결정적 LCG(Bootstrap.SeededRandom과 같은 식). [0,1).</summary>
        private float Next()
        {
            _state = unchecked(_state * 1664525u + 1013904223u);
            return (_state >> 8) / 16777216f;
        }
    }
}
