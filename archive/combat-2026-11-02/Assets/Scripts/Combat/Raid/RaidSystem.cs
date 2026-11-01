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
        private List<RaidUnit> _nests = new List<RaidUnit>();
        // 스토리 038: 길가/종점 둥지(_roadNests)와 정차 입구(_stopNests). _nests는 이번 습격이 쓰는 쪽을 가리킨다.
        private readonly List<RaidUnit> _roadNests;
        private readonly List<RaidUnit> _stopNests = new List<RaidUnit>();
        private bool _stop;
        private bool _leaveRequested;
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
        // 스토리 037: 달리면서 치르는 습격(enroute) 상태.
        private bool _enroute;
        private bool _enrouteDone;
        private float _edgeSpawnTimer;
        private bool _edgeAhead;
        private float _lastFrontX;
        private bool _hasLastFront;
        private ITrackNode _enrouteNode;
        private string _mode;
        private float _enrouteLength;

        /// <summary>스토리 037: 습격 방식("enroute" | "arrival"). 기본은 데이터 _mode. 횡스크롤 여정 화면이 "enroute"로 바꾼다.</summary>
        public string Mode { get => _mode ?? _data.Mode; set => _mode = value; }
        /// <summary>스토리 037: 지금 방식이 달리면서 치르는 습격인지.</summary>
        public bool IsEnroute => Mode == "enroute";
        /// <summary>스토리 037: 이번 습격이 달리면서 치르는 습격으로 시작되었는지.</summary>
        public bool ActiveEnroute => IsActive && _enroute;
        /// <summary>스토리 037: enroute 구간 길이(습격 좌표 단위, 0 이하면 데이터 _enrouteLength). 길가 둥지 X = 진행도 × 이 값.</summary>
        public float EnrouteLength { get => _enrouteLength > 0f ? _enrouteLength : _data.EnrouteLength; set => _enrouteLength = value; }
        /// <summary>스토리 037: 출발 구간 진행도 0..1(표현이 TrainController.TravelProgress를 넣는다). null이면 enroute 습격은 시작되지 않는다.</summary>
        public Func<float> EdgeProgress { get; set; }
        /// <summary>스토리 037: 열차 꼬리 위치(습격 좌표). null이면 머리에서 _trainFallbackLength 뒤.</summary>
        public Func<(float x, float y)> TrainTailPosition { get; set; }

        /// <summary>스토리 038: 중간역(_waypointId)에 서면 정차 디펜스(끝 시간 없음, 출발은 Leave)를 연다. 탑뷰 정차 화면이 켠다.</summary>
        public bool StopMode { get; set; }
        /// <summary>스토리 038: 이번 습격이 정차 디펜스인지(진행 중일 때만 true).</summary>
        public bool ActiveStop => IsActive && _stop;
        /// <summary>스토리 038: 정차 디펜스의 열차 머리 위치(정차 화면 좌표). 정차 화면이 넣는다.</summary>
        public Func<(float x, float y)> StopFront { get; set; }
        /// <summary>스토리 038: 정차 디펜스의 열차 꼬리 위치(정차 화면 좌표).</summary>
        public Func<(float x, float y)> StopTail { get; set; }
        /// <summary>스토리 038: 지금 정차 파도 번호(버틴 초 ÷ 간격 + 바퀴 보너스).</summary>
        public int StopWave => (int)(_elapsed / _data.StopWaveEverySeconds) + (_track?.Lap ?? 0) * _data.StopLapWaveBonus;

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
        public float Progress => _enroute && EdgeProgress != null
            ? Math.Max(0f, Math.Min(1f, (EdgeProgress() - _data.StartProgress) / Math.Max(0.01f, _data.EndProgress - _data.StartProgress)))
            : Math.Min(1f, _elapsed / SegmentSeconds);
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
            _roadNests = _nests;
            for (int i = 0; i < data.StopEntrances.Length; i++)
                _stopNests.Add(new RaidUnit { Id = i, Kind = "entrance", Hp = data.StopEntranceHp, HpMax = data.StopEntranceHp });
        }

        /// <summary>종점(점령 거점)의 월드 위치를 정한다. 둥지가 그 둘레(데이터 오프셋)로 옮겨진다.</summary>
        public void SetOrigin(float x, float y)
        {
            _originX = x; _originY = y;
            for (int i = 0; i < _roadNests.Count; i++) { _roadNests[i].X = x + _data.NestOffsets[i].x; _roadNests[i].Y = y + _data.NestOffsets[i].y; }
        }

        /// <summary>node가 아직 점령된 경유지(데이터 _waypointId, 해방 전)인지.</summary>
        public bool IsOccupied(ITrackNode node)
        {
            // 스토리 038: 정차 디펜스는 해방 여부와 관계없이 중간역에서 늘 열린다(고철 파밍).
            if (StopMode && _stopNests.Count > 0 && node != null && (string.IsNullOrEmpty(_data.WaypointId) || node.Id == _data.WaypointId)) return true;
            if (TerminalLiberated || node == null || IsEnroute) return false;
            return string.IsNullOrEmpty(_data.WaypointId) || node.Id == _data.WaypointId;
        }

        /// <summary>습격을 시작한다: 내구도를 (기본 + 방벽 × 방벽당)으로 채우고 둥지 스폰 시계와 포탑 시계를 맞춘다.</summary>
        public void Begin(ITrackNode node)
        {
            _enroute = false;
            _stop = StopMode && _stopNests.Count > 0;
            _nests = _stop ? _stopNests : _roadNests;
            _leaveRequested = false;
            if (_stop)
            {
                (float fx, float fy) = Train();
                RaidOffset[] entrances = _data.StopEntrances;
                for (int i = 0; i < _stopNests.Count; i++)
                {
                    _stopNests[i].X = fx + entrances[i].x; _stopNests[i].Y = fy + entrances[i].y;
                    if (_data.StopEntranceRespawn) _stopNests[i].Hp = _stopNests[i].HpMax;
                }
            }
            BeginCore();
        }

        /// <summary>
        /// 스토리 038(+03:30): 정차 디펜스 중 플레이어가 "출발"을 누르면 다음 Tick에서 승리로 끝내 열차를 떠나보낸다(언제든 가능).
        /// 정차 디펜스가 아니면 false.
        /// </summary>
        public bool Leave()
        {
            if (!ActiveStop) return false;
            _leaveRequested = true;
            UnityEngine.Debug.Log($"RAID: stop leave requested t={_elapsed:F1} wave={StopWave}");
            return true;
        }

        /// <summary>
        /// 스토리 037: 달리면서 치르는 습격을 시작한다. 둥지를 길가(진행도 × EnrouteLength)에 놓고 가장자리 스폰 시계를 맞춘다.
        /// 열차는 멈추지 않으며, 끝나면 WaypointResolver.CompleteEnrouteRaid(승패, 종점 노드)로 보상·벌칙이 적용된다.
        /// </summary>
        public void BeginEnroute(ITrackNode node)
        {
            _enroute = true;
            _stop = false;
            _nests = _roadNests;
            _enrouteDone = true;
            _enrouteNode = node;
            float length = EnrouteLength;
            for (int i = 0; i < _nests.Count; i++) { _nests[i].X = _data.RoadsideNest(i) * length; _nests[i].Y = _data.NestOffsets[i].y; }
            _edgeSpawnTimer = _data.EdgeSpawnDelay;
            _edgeAhead = true;
            _hasLastFront = false;
            BeginCore();
        }

        private void BeginCore()
        {
            IRaidLoadout loadout = _loadout?.Invoke();
            int walls = Math.Max(0, OverrideWalls ?? loadout?.ArmorPlates ?? 0);
            _turrets = Math.Max(0, OverrideTurrets ?? loadout?.Turrets ?? 0);
            _heavyTurrets = OverrideTurrets.HasValue ? 0 : Math.Max(0, loadout?.HeavyTurrets ?? 0);
            _segment = _data.SegmentSeconds / (1f + Math.Max(0f, loadout?.SpeedBonus ?? 0f));
            if (_stop) _segment = float.MaxValue; // 스토리 038: 정차 디펜스는 시간으로 끝나지 않는다.
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
            if (dt <= 0f) return;
            if (!IsActive) { CheckEnrouteStart(); if (!IsActive) return; }
            _elapsed += dt;
            _playerTimer -= dt;
            Spawn(dt);
            Move(dt);
            if (!IsActive) return;
            FireTurrets(dt);
            Repair(dt);
            if (Hull <= 0) End(false);
            else if (_stop) { if (_leaveRequested) End(true); }
            else if (_enroute) { if (EnrouteFinished()) End(true); }
            else if (_elapsed >= SegmentSeconds) End(true);
        }

        /// <summary>스토리 037: enroute 방식이면 출발 거점(_hubId)에서 종점으로 가는 구간의 진행도가 _startProgress에 닿을 때 한 번 시작한다.</summary>
        private void CheckEnrouteStart()
        {
            if (!IsEnroute || EdgeProgress == null || _track == null || TerminalLiberated || NestsAlive == 0) return;
            float progress = EdgeProgress();
            bool outbound = _track.CurrentNode != null && _track.CurrentNode.Id == _data.HubId && !_track.IsHeld;
            if (!outbound || progress < _data.StartProgress * 0.5f) { _enrouteDone = false; return; }
            if (_enrouteDone || progress < _data.StartProgress || progress >= _data.EndProgress) return;
            ITrackNode terminal = null;
            foreach (ITrackNode node in _track.Nodes) if (node.Id == _data.WaypointId) terminal = node;
            if (terminal == null) return;
            BeginEnroute(terminal);
        }

        private bool EnrouteFinished()
        {
            if (EdgeProgress == null || _track == null) return true;
            return _track.CurrentNode == null || _track.CurrentNode.Id != _data.HubId || EdgeProgress() >= _data.EndProgress;
        }

        private (float x, float y) Tail()
        {
            if (_stop && StopTail != null) return StopTail();
            if (TrainTailPosition != null) return TrainTailPosition();
            (float x, float y) = Train();
            return (x - _data.TrainFallbackLength, y);
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
            if (NestsAlive == 0 && _stop)
            {
                // 스토리 038: 정차 입구를 모두 막으면 이번 정차의 스폰만 멈춘다(종점 해방·길가 습격과는 별개).
                UnityEngine.Debug.Log("RAID: stop entrances all destroyed");
            }
            else if (NestsAlive == 0)
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
            if (_stop && StopFront != null) return StopFront();
            if (StopMode && !IsActive && StopFront != null && !_enroute) return StopFront();
            if (TrainPosition != null) return TrainPosition();
            float half = _data.TrainFallbackLength * 0.5f;
            return (_originX - half + _data.TrainFallbackLength * Progress, _originY);
        }

        private void Spawn(float dt)
        {
            RaidWave wave = _data.WaveFor(_track?.Lap ?? 0);
            // 스토리 038: 정차 디펜스는 버틴 시간의 파도 번호로 간격·상한·자동인형 비율을 정한다.
            int stopWave = _stop ? StopWave : 0;
            float interval = _stop ? _data.StopSpawnInterval(stopWave) : wave.SpawnInterval;
            int maxAlive = _stop ? _data.StopMaxAlive(stopWave) : wave.MaxAlive;
            float automaton = _stop ? _data.StopAutomatonChance(stopWave) : wave.AutomatonChance;
            for (int i = 0; i < _nests.Count; i++)
            {
                if (!_nests[i].Alive) continue;
                if (_enroute && !NearTrain(_nests[i].X)) continue;
                _spawnTimers[i] -= dt;
                if (_spawnTimers[i] > 0f) continue;
                float jitter = 1f + (Next() * 2f - 1f) * _data.SpawnJitter;
                _spawnTimers[i] += Math.Max(0.1f, interval * jitter);
                if (_monsters.Count >= maxAlive) continue;
                RaidMonsterData kind = _data.Monster(Next() < automaton ? "automaton" : "spider");
                var monster = new RaidUnit
                {
                    Id = _nextId++, Kind = kind.Id, Hp = kind.Hp, HpMax = kind.Hp,
                    X = _nests[i].X + (Next() - 0.5f) * 0.4f, Y = _nests[i].Y + (Next() - 0.5f) * 0.4f,
                };
                _monsters.Add(monster);
                _kinds[monster.Id] = kind;
                MonsterSpawned?.Invoke(monster.Id);
            }
            if (_enroute) SpawnAtEdge(dt, wave);
        }

        /// <summary>스토리 037: 길가 둥지가 열차(머리~꼬리)에서 _nestSpawnRange 안인지.</summary>
        private bool NearTrain(float x)
        {
            float fx = Train().x, bx = Tail().x;
            float range = _data.NestSpawnRange;
            return x <= Math.Max(fx, bx) + range && x >= Math.Min(fx, bx) - range;
        }

        /// <summary>스토리 037: 화면 가장자리(앞·뒤 번갈아)에서 몬스터를 낸다.</summary>
        private void SpawnAtEdge(float dt, RaidWave wave)
        {
            if (_data.EdgeSpawnInterval <= 0f) return;
            _edgeSpawnTimer -= dt;
            if (_edgeSpawnTimer > 0f) return;
            _edgeSpawnTimer += Math.Max(0.1f, _data.EdgeSpawnInterval * (1f + (Next() * 2f - 1f) * _data.SpawnJitter));
            if (_monsters.Count >= wave.MaxAlive) return;
            (float fx, float fy) = Train();
            (float bx, float by) = Tail();
            RaidMonsterData kind = _data.Monster(Next() < wave.AutomatonChance ? "automaton" : "spider");
            bool ahead = _edgeAhead || !_data.TailContact;
            _edgeAhead = !_edgeAhead;
            var monster = new RaidUnit
            {
                Id = _nextId++, Kind = kind.Id, Hp = kind.Hp, HpMax = kind.Hp,
                X = ahead ? fx + _data.EdgeSpawnAhead : bx - _data.EdgeSpawnBehind,
                Y = (ahead ? fy : by) + (Next() * 2f - 1f) * _data.EdgeSpawnLane,
            };
            _monsters.Add(monster);
            _kinds[monster.Id] = kind;
            MonsterSpawned?.Invoke(monster.Id);
        }

        private void Move(float dt)
        {
            (float fx, float fy) = Train();
            (float bx, float by) = Tail();
            // 스토리 037: enroute에서는 열차 속도를 머리 위치 변화로 재서, 열차를 쫓는 몬스터가 그만큼(× _chaseSpeedFactor) 더 달린다.
            float vx = 0f;
            if (_enroute && _hasLastFront) vx = (fx - _lastFrontX) / dt;
            _lastFrontX = fx; _hasLastFront = true;
            for (int i = _monsters.Count - 1; i >= 0; i--)
            {
                RaidUnit monster = _monsters[i];
                RaidMonsterData kind = _kinds[monster.Id];
                bool tail = _enroute && _data.TailContact && monster.X < (fx + bx) * 0.5f;
                float tx = tail ? bx : fx, ty = tail ? by : fy;
                if (_stop) Closest(monster.X, monster.Y, fx, fy, bx, by, out tx, out ty); // 스토리 038: 탑뷰에서는 열차 몸통 가장 가까운 곳으로
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
                if (_enroute && vx > 0f && dx > 0f) monster.X += vx * _data.ChaseSpeedFactor * dt;
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
                    if (_enroute) { (float bx, float by) = Tail(); d = Math.Min(d, (monster.X - bx) * (monster.X - bx) + (monster.Y - by) * (monster.Y - by)); }
                    if (_stop) { (float bx, float by) = Tail(); Closest(monster.X, monster.Y, tx, ty, bx, by, out float cx, out float cy); d = (monster.X - cx) * (monster.X - cx) + (monster.Y - cy) * (monster.Y - cy); }
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
            if (_stop) { _stop = false; _nests = _roadNests; }
            if (_enroute) _resolver?.CompleteEnrouteRaid(victory, _enrouteNode);
            else _resolver?.CompleteRaid(victory);
        }

        /// <summary>스토리 038: 점 (x, y)에서 선분 (ax, ay)–(bx, by) 위 가장 가까운 점.</summary>
        private static void Closest(float x, float y, float ax, float ay, float bx, float by, out float cx, out float cy)
        {
            float vx = bx - ax, vy = by - ay;
            float len = vx * vx + vy * vy;
            float t = len > 0f ? Math.Max(0f, Math.Min(1f, ((x - ax) * vx + (y - ay) * vy) / len)) : 0f;
            cx = ax + vx * t; cy = ay + vy * t;
        }

        /// <summary>결정적 LCG(Bootstrap.SeededRandom과 같은 식). [0,1).</summary>
        private float Next()
        {
            _state = unchecked(_state * 1664525u + 1013904223u);
            return (_state >> 8) / 16777216f;
        }
    }
}
