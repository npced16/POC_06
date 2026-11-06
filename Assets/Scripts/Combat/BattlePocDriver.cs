using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;

namespace IronRing.Combat
{
    /// <summary>독립 전투 POC 씬에서 클릭 포격·자동 포탑·세 장 카드·보스 승패를 구동한다.</summary>
    public sealed class BattlePocDriver : MonoBehaviour
    {
        [Header("스테이지")]
        [SerializeField, Min(1)] private int _defaultStage = 1;
        private int _stageNumber;
        private BattleStageLineup _lineup;
        private float _healthMultiplier = 1f;
        private float _spawnInterval;
        [Header("적 유형")]
        [SerializeField] private EnemySpriteBinding[] _enemySprites;
        [SerializeField] private float _guardForwardOffset = 0.9f;
        [SerializeField] private float _guardSpacing = 0.8f;
        [Header("전장 배치")]
        [SerializeField] private Camera _battleCamera;
        [SerializeField] private Transform _enemyRoot;
        [SerializeField] private Sprite _enemySprite;
        [SerializeField] private Transform[] _turretSlots;
        [SerializeField] private GameObject[] _extraCars;
        [SerializeField] private float _spawnY = 4.3f;
        [SerializeField] private float _bottomSpawnY = -5f;
        [SerializeField] private float _trainY = -3.2f;
        [SerializeField] private float _spawnHalfWidth = 6f;
        [SerializeField] private bool _perspectiveEnemies;
        [SerializeField] private float _distantEnemyHeight = 0.65f;
        [SerializeField] private float _nearEnemyHeight = 1.15f;
        [SerializeField] private float _enemySway = 0.08f;
        [SerializeField] private string _nextHubId = "hub-2";
        [Header("UI")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private GameObject _cardPanel;
        [SerializeField] private Button[] _cardButtons;
        [SerializeField] private TMP_Text[] _cardTexts;
        [SerializeField] private Image _healthFill;
        [SerializeField] private Image _experienceFill;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private TMP_Text _experienceText;
        [SerializeField] private TMP_Text _speedText;
        [SerializeField] private Button _speedButton;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private TMP_Text _pauseText;
        [SerializeField] private TMP_Text _currencyText;
        [SerializeField] private GameObject _placementPanel;
        [SerializeField] private Button[] _placementButtons;
        [SerializeField] private TMP_Text[] _placementTexts;
        [Header("단독 POC 성장 상태")]
        [SerializeField] private string[] _demoBuiltHabitats;
        [SerializeField] private string[] _demoJoinedHabitats;

        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly List<EnemyShot> _enemyShots = new List<EnemyShot>();
        private readonly List<float> _trainPointsX = new List<float>();
        private Sprite _effectSprite;
        private Sprite _turretSprite;
        private BattleRules _rules;
        private BattleRun _run;
        private float[] _turretCooldowns;
        private float _spawnClock;
        private bool _bossSpawned;
        private readonly List<float> _roamingGuardTimes = new List<float>();
        private bool _finished;
        private int _trainHealth;
        private System.Random _random;
        private BattleLoadout _loadout;
        private float _speed = 1f;
        private bool _paused;

        public event Action<bool> Completed;

        private sealed class Enemy
        {
            public GameObject Object;
            public EnemyRule Rule;
            public float Health;
            public float MaxHealth;
            public Transform HealthFill;
            public float SpriteHeight;
            public float LaneX;
            public float Phase;
            public Enemy Leader;
            public float Side;
            public bool Charging;
            public bool ContactedTrain;
            public Vector2 ChargeTarget;
            public float AttackCooldown;
        }

        private sealed class Projectile
        {
            public GameObject Object;
            public Enemy Target;
            public float Damage;
        }

        private sealed class EnemyShot
        {
            public GameObject Object;
            public Vector3 Target;
            public int Damage;
            public float Speed;
        }

        [Serializable]
        private struct EnemySpriteBinding
        {
            [SerializeField] private string _enemyId;
            [SerializeField] private Sprite _sprite;
            public string EnemyId => _enemyId;
            public Sprite Sprite => _sprite;
        }

        void Start()
        {
            _effectSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _turretSprite = _turretSlots[0].parent.Find("Main Gun").GetComponent<SpriteRenderer>().sprite;
            _rules = BattleRules.Load();
            // 진행 중인 루프 번호가 스테이지다. 완료한 바퀴 수에 1을 더하고, 단독 실행이면 Inspector 기본값을 쓴다.
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            _stageNumber = track != null ? track.Lap + 1 : _defaultStage;
            _random = new System.Random(Environment.TickCount);
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            INpcRecruiter recruiter = GameServices.Get<INpcRecruiter>();
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            _loadout = clusters != null && hubs != null
                ? BattleIntegration.CreateLoadout(_rules, clusters, recruiter, hubs.HubIds)
                : new BattleLoadout(_rules, _demoBuiltHabitats, _demoJoinedHabitats);
            _turretCooldowns = new float[_turretSlots.Length];
            if (_speedButton != null) _speedButton.onClick.AddListener(ToggleSpeed);
            if (_pauseButton != null) _pauseButton.onClick.AddListener(TogglePause);
            if (_placementButtons != null)
                for (int i = 0; i < _placementButtons.Length; i++)
                {
                    int slot = i;
                    _placementButtons[i].onClick.AddListener(() => PlaceAt(slot));
                }
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                int index = i;
                _cardButtons[i].onClick.AddListener(() => ChooseCard(index));
            }
            BeginBattle();
        }

        void OnDestroy()
        {
            if (_effectSprite != null) Destroy(_effectSprite);
        }

        void Update()
        {
            if (_finished && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) { BeginBattle(); return; }
            if (_finished) return;
            if (_paused) return;
            if (_run.IsPlacingTurret) { HighlightSlots(); HandlePlacement(); return; }
            if (_run.HasCardChoice) return;
            ShowTurretHover();
            HandleMainGun();
            float dt = Time.deltaTime * _speed;
            _run.Advance(dt);
            Spawn(dt);
            MoveEnemies(dt);
            FireTurrets(dt);
            MoveProjectiles(dt);
            MoveEnemyShots(dt);
            RefreshUi();
        }

        /// <summary>
        /// 적이 돌진하고 포격할 열차 지점으로 주포와 현재 활성화된 거치대의 가로 위치를 모은다.
        /// _turretSlots와 Main Gun 위치를 사용하며, _trainPointsX를 갱신한다.
        /// </summary>
        private void CacheTrainPoints()
        {
            _trainPointsX.Clear();
            _trainPointsX.Add(_turretSlots[0].parent.Find("Main Gun").position.x);
            // 아직 열리지 않은 객차의 거치대는 화면에 없으므로 목표에서 뺀다.
            foreach (Transform slot in _turretSlots)
                if (slot.gameObject.activeInHierarchy) _trainPointsX.Add(slot.position.x);
        }

        /// <summary>
        /// x에서 가장 가까운 열차 지점의 가로 위치를 찾는다.
        /// x와 _trainPointsX를 사용하며, 가장 가까운 지점의 x를 반환한다.
        /// </summary>
        private float NearestTrainX(float x)
        {
            float nearest = _trainPointsX[0];
            foreach (float pointX in _trainPointsX)
                if (Mathf.Abs(pointX - x) < Mathf.Abs(nearest - x)) nearest = pointX;
            return nearest;
        }

        /// <summary>전투 진행 배속을 1배, 2배, 3배 순서로 바꾸고 버튼 문구를 갱신한다.</summary>
        private void ToggleSpeed()
        {
            _speed = _speed >= 3f ? 1f : _speed + 1f;
            _speedText.text = $"{_speed:0}x";
        }

        /// <summary>전투의 일시정지 상태를 전환하고 버튼 문구를 갱신한다.</summary>
        private void TogglePause()
        {
            _paused = !_paused;
            _pauseText.text = _paused ? "▶" : "Ⅱ";
        }

        /// <summary>적과 탄환, 포탑, 시간 및 내구도를 새 전투 상태로 초기화한다.</summary>
        private void BeginBattle()
        {
            foreach (Enemy enemy in _enemies) if (enemy.Object != null) Destroy(enemy.Object);
            foreach (Projectile projectile in _projectiles) if (projectile.Object != null) Destroy(projectile.Object);
            foreach (EnemyShot shot in _enemyShots) if (shot.Object != null) Destroy(shot.Object);
            _enemies.Clear();
            _projectiles.Clear();
            _enemyShots.Clear();
            _lineup = _rules.LineupAt(_stageNumber);
            _healthMultiplier = _rules.Stage.HealthMultiplierAt(_stageNumber);
            _spawnInterval = _rules.Stage.SpawnIntervalAt(_stageNumber);
            foreach (Transform slot in _turretSlots)
            {
                Transform visual = slot.Find("Installed Turret");
                if (visual != null) { visual.SetParent(null); Destroy(visual.gameObject); }
            }
            _run = new BattleRun(_rules, _loadout, _random.Next());
            _run.RestoreArmament();
            RefreshCars();
            CacheTrainPoints();
            Array.Clear(_turretCooldowns, 0, _turretCooldowns.Length);
            _trainHealth = _rules.Stage.TrainHealth;
            _spawnClock = 0;
            _bossSpawned = false;
            ScheduleRoamingGuards();
            _finished = false;
            _paused = false;
            if (_pauseText != null) _pauseText.text = "Ⅱ";
            _resultPanel.SetActive(false);
            _cardPanel.SetActive(false);
            if (_placementPanel != null) _placementPanel.SetActive(false);
            RefreshTurretVisuals();
            RefreshUi();
        }

        /// <summary>마우스 클릭의 전장 위치를 계산해 주포를 발사하고 반경 안의 적에게 피해를 준다.</summary>
        private void HandleMainGun()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || EventSystem.current.IsPointerOverGameObject()) return;
            Vector3 point = _battleCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            if (point.y < _bottomSpawnY - 0.5f || point.y > _spawnY + 0.5f) return;
            if (!_run.TryFireMainGun(point, out float damage, out float radius)) return;
            StartCoroutine(FireMainShell(point, damage, radius));
            RefreshUi();
        }

        /// <summary>목표 point에 damage와 radius를 전달할 주포탄을 날리고 도착 시 범위 피해와 폭발을 표시한다.</summary>
        private IEnumerator FireMainShell(Vector3 point, float damage, float radius)
        {
            Vector3 origin = _turretSlots[0].parent.Find("Main Gun").position;
            GameObject shell = CreateEffect("Main Shell", origin, new Vector2(0.22f, 0.42f), new Color(1f, 0.72f, 0.2f), 12);
            Vector3 target = new Vector3(point.x, point.y, -0.6f);
            SpawnMarker(origin, 0.3f);
            while (Vector2.Distance(shell.transform.position, target) > 0.15f && !_finished)
            {
                if (!_paused && !_run.HasCardChoice && !_run.IsPlacingTurret)
                    shell.transform.position = Vector3.MoveTowards(shell.transform.position, target, 15f * Time.deltaTime * _speed);
                yield return null;
            }
            Destroy(shell);
            if (_finished) yield break;
            GameObject blast = CreateEffect("Blast Radius", target, Vector2.one * radius * 2f, new Color(1f, 0.45f, 0.08f, 0.25f), 7);
            Destroy(blast, 0.3f);
            SpawnMarker(target, radius);
            for (int i = _enemies.Count - 1; i >= 0; i--)
                if (Vector2.Distance(_enemies[i].Object.transform.position, target) <= radius) DamageEnemy(i, damage);
        }

        /// <summary>전투 시간과 JSON 출현 간격에 따라 스테이지 구성의 일반 적과 종료 보스 무리를 전장 위쪽 또는 아래쪽 끝에 만든다.</summary>
        private void Spawn(float dt)
        {
            _spawnClock += dt;
            if (_run.Time < _rules.Stage.BossSpawnSeconds && _spawnClock >= _spawnInterval)
            {
                _spawnClock = 0;
                EnemyRule soldier = _rules.Enemy(_lineup.PickSoldier(_random));
                float side = RandomSide();
                CreateEnemy(soldier, (float)(_random.NextDouble() * 2 - 1) * _spawnHalfWidth, SpawnYFor(side), side);
            }
            if (_roamingGuardTimes.Count > 0 && _run.Time >= _roamingGuardTimes[0])
            {
                _roamingGuardTimes.RemoveAt(0);
                EnemyRule guard = _rules.Enemy(_rules.Stage.RoamingGuardId);
                float side = RandomSide();
                CreateEnemy(guard, (float)(_random.NextDouble() * 2 - 1) * _spawnHalfWidth, SpawnYFor(side), side);
            }
            if (!_bossSpawned && _run.Time >= _rules.Stage.BossSpawnSeconds)
            {
                _bossSpawned = true;
                SpawnBossGroup();
            }
        }

        /// <summary>
        /// 적이 나올 방향을 무작위로 고른다.
        /// _random을 사용하며, 위쪽이면 1, 아래쪽이면 -1을 반환한다.
        /// </summary>
        private float RandomSide()
        {
            return _random.NextDouble() < 0.5 ? 1f : -1f;
        }

        /// <summary>
        /// 출현 방향에 맞는 출현 높이를 찾는다.
        /// side를 사용하며, 위쪽이면 _spawnY, 아래쪽이면 _bottomSpawnY를 반환한다.
        /// </summary>
        private float SpawnYFor(float side)
        {
            return side > 0f ? _spawnY : _bottomSpawnY;
        }

        /// <summary>
        /// 적이 출현 위치에서 열차까지 얼마나 다가왔는지 계산한다.
        /// enemy의 출현 방향과 높이 y를 사용하며, 출현 위치 0에서 열차 1까지의 비율을 반환한다.
        /// </summary>
        private float ApproachProgress(Enemy enemy, float y)
        {
            return Mathf.InverseLerp(SpawnYFor(enemy.Side), _trainY, y);
        }

        /// <summary>
        /// 이번 전투에서 보스 없이 따로 나올 방패수호병 수를 정하고 출현 시각을 고르게 배치한다.
        /// _stage의 최소·최대 수와 보스 출현 시각을 사용하며, _roamingGuardTimes를 오름차순으로 채운다.
        /// </summary>
        private void ScheduleRoamingGuards()
        {
            _roamingGuardTimes.Clear();
            int count = _random.Next(_rules.Stage.RoamingGuardMin, _rules.Stage.RoamingGuardMax + 1);
            for (int i = 0; i < count; i++)
                _roamingGuardTimes.Add(_rules.Stage.BossSpawnSeconds * (i + 1f) / (count + 1f));
        }

        /// <summary>
        /// 스테이지 구성의 보스와 그 앞을 막는 방패수호병 대열, 함께 나오는 추가 보스를 만든다.
        /// _lineup의 보스·수호병·추가 보스 id와 현재 스테이지의 수호병 수를 사용하며, 수호병의 Leader를 보스로 지정한다.
        /// </summary>
        private void SpawnBossGroup()
        {
            int guardCount = _lineup.GuardCountAt(_stageNumber);
            float spread = guardCount > 1 ? (guardCount - 1) * 0.5f * _guardSpacing : 0f;
            float x = (float)(_random.NextDouble() * 2 - 1) * Mathf.Max(0f, _spawnHalfWidth - spread);
            float side = RandomSide();
            Enemy boss = CreateEnemy(_rules.Enemy(_lineup.BossId), x, SpawnYFor(side), side);
            SpawnExtraBosses(x, side);
            if (guardCount <= 0) return;
            EnemyRule guardRule = _rules.Enemy(_lineup.GuardId);
            for (int i = 0; i < guardCount; i++)
            {
                // 수호병은 보스보다 열차 쪽에 가로로 늘어서고 보스와 같은 흔들림을 쓴다.
                float offset = i * _guardSpacing - spread;
                Enemy guard = CreateEnemy(guardRule, x + offset, SpawnYFor(side) - side * _guardForwardOffset, side);
                guard.Leader = boss;
                guard.Phase = boss.Phase;
            }
        }

        /// <summary>
        /// 구성의 추가 보스를 주 보스 무리와 반대 방향(위·아래), 반대쪽 가로 절반에 고르게 나눠 만든다.
        /// leaderX, leaderSide와 _lineup.ExtraBossIds를 사용하며, 생성한 보스를 적 목록에 넣는다.
        /// </summary>
        private void SpawnExtraBosses(float leaderX, float leaderSide)
        {
            int count = _lineup.ExtraBossIds.Count;
            if (count == 0) return;
            // 주 보스와 반대 방향에서 나오게 해 두 보스가 열차를 위아래에서 협공한다.
            float side = -leaderSide;
            float half = leaderX >= 0f ? -1f : 1f;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / (count + 1f);
                float x = half * Mathf.Lerp(_spawnHalfWidth * 0.3f, _spawnHalfWidth * 0.9f, t);
                CreateEnemy(_rules.Enemy(_lineup.ExtraBossIds[i]), x, SpawnYFor(side), side);
            }
        }

        /// <summary>
        /// 적 id에 연결된 스프라이트를 찾는다.
        /// enemyId와 _enemySprites를 사용하며, 연결이 없으면 기본 _enemySprite를 반환한다.
        /// </summary>
        private Sprite EnemySprite(string enemyId)
        {
            if (_enemySprites != null)
                foreach (EnemySpriteBinding binding in _enemySprites)
                    if (binding.EnemyId == enemyId && binding.Sprite != null) return binding.Sprite;
            return _enemySprite;
        }

        /// <summary>
        /// 적 유형 규칙의 체력·배율·색을 적용한 적 도형을 x, y에 생성해 적 목록에 넣는다.
        /// rule과 위치, 출현 방향 side(위 1, 아래 -1), 스테이지 체력 배율 _healthMultiplier를 사용하며, 생성한 적 상태를 반환한다.
        /// </summary>
        private Enemy CreateEnemy(EnemyRule rule, float x, float y, float side)
        {
            bool boss = rule.Role == BattleEnemyRole.Boss;
            Sprite sprite = EnemySprite(rule.SpriteId);
            GameObject shape = new GameObject(boss ? "Boss" : "Enemy", typeof(SpriteRenderer));
            shape.transform.SetParent(_enemyRoot, false);
            shape.transform.position = new Vector3(x, y, 0);
            shape.transform.localScale = _perspectiveEnemies ? Vector3.one * (_distantEnemyHeight * rule.Scale / sprite.bounds.size.y) : Vector3.one * (1.05f * rule.Scale);
            SpriteRenderer enemyRenderer = shape.GetComponent<SpriteRenderer>();
            enemyRenderer.sprite = sprite;
            enemyRenderer.color = rule.Tint;
            GameObject bar = CreateEffect("Health Back", shape.transform.position, new Vector2(1.2f, 0.14f), new Color(0.12f, 0.08f, 0.08f), 3);
            bar.transform.SetParent(shape.transform, false);
            bar.transform.localPosition = new Vector3(0, 0.75f, -0.05f);
            bar.transform.localScale = new Vector3(1.2f, 0.14f, 1);
            if (_perspectiveEnemies)
            {
                bar.transform.localPosition = new Vector3(0f, sprite.bounds.extents.y + 0.5f, -0.05f);
                bar.transform.localScale = new Vector3(sprite.bounds.size.x * 0.65f, sprite.bounds.size.y * 0.05f, 1f);
            }
            GameObject fill = CreateEffect("Health Fill", shape.transform.position, Vector2.one, new Color(0.85f, 0.22f, 0.13f), 4);
            fill.transform.SetParent(bar.transform, false);
            fill.transform.localPosition = new Vector3(0, 0, -0.05f);
            fill.transform.localScale = new Vector3(0.94f, 0.7f, 1);
            // 스테이지가 오를수록 같은 유형의 적도 체력이 늘어난다.
            float maxHealth = rule.Health * _healthMultiplier;
            Enemy enemy = new Enemy
            {
                Object = shape,
                Rule = rule,
                Health = maxHealth,
                MaxHealth = maxHealth,
                HealthFill = fill.transform,
                SpriteHeight = sprite.bounds.size.y,
                LaneX = x,
                Side = side,
                Phase = (float)_random.NextDouble() * 6.28f
            };
            _enemies.Add(enemy);
            return enemy;
        }

        /// <summary>
        /// 적을 위나 아래에서 열차 쪽으로 보내다가 출현 위치와 열차 사이 경로의 돌진 비율 지점에 도달하면 열차의 가장 가까운 지점으로 돌진시키고, 도달한 적의 접촉 피해를 열차 내구도에 반영한다.
        /// dt를 사용하며, 적 위치·크기·돌진 상태와 _trainHealth를 변경한다.
        /// </summary>
        private void MoveEnemies(float dt)
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = _enemies[i];
                if (enemy.ContactedTrain) continue;
                Vector3 position = enemy.Object.transform.position;
                bool guarding = enemy.Leader != null && _enemies.Contains(enemy.Leader);
                if (!enemy.Charging && !guarding && enemy.Rule.ChargeAtProgress > 0 &&
                    ApproachProgress(enemy, position.y) >= enemy.Rule.ChargeAtProgress)
                {
                    // 돌진을 시작한 순간의 열차 지점을 목표로 고정한다.
                    enemy.Charging = true;
                    enemy.ChargeTarget = new Vector2(NearestTrainX(position.x), _trainY);
                }
                if (enemy.Charging)
                {
                    Vector2 next = Vector2.MoveTowards(position, enemy.ChargeTarget, enemy.Rule.ChargeSpeed * dt);
                    position.x = next.x;
                    position.y = next.y;
                    enemy.LaneX = position.x;
                }
                else position.y -= enemy.Side * ApproachStep(enemy, position.y, guarding, dt);
                if (_perspectiveEnemies)
                {
                    float progress = ApproachProgress(enemy, position.y);
                    float height = Mathf.Lerp(_distantEnemyHeight, _nearEnemyHeight, progress) * enemy.Rule.Scale;
                    enemy.Object.transform.localScale = Vector3.one * (height / enemy.SpriteHeight);
                    if (!enemy.Charging) position.x = enemy.LaneX + Mathf.Sin(_run.Time * 4f + enemy.Phase) * _enemySway * progress;
                }
                enemy.Object.transform.position = position;
                if (!enemy.Charging) TickEnemyAttack(enemy, position, dt);
                if (enemy.Side * (position.y - _trainY) > 0.45f) continue;
                _trainHealth -= enemy.Rule.ContactDamage;
                if (enemy.Rule.Role == BattleEnemyRole.Boss) enemy.ContactedTrain = true;
                else
                {
                    Destroy(enemy.Object);
                    _enemies.RemoveAt(i);
                }
                if (_trainHealth <= 0) { Finish(false); return; }
            }
        }

        /// <summary>
        /// 이번 프레임에 적이 열차 쪽으로 다가갈 거리를 계산한다.
        /// 적의 열차까지 거리, 원거리 사거리, 보스 대열 위치를 사용하며, 정지선을 넘지 않는 이동 거리를 반환한다.
        /// </summary>
        private float ApproachStep(Enemy enemy, float y, bool guarding, float dt)
        {
            // 위아래 어느 쪽에서 오든 열차까지 남은 거리로 계산한다.
            float distance = enemy.Side * (y - _trainY);
            float hold = enemy.Rule.AttackRange > 0 ? enemy.Rule.AttackRange : float.NegativeInfinity;
            if (guarding)
            {
                float leaderDistance = enemy.Leader.Side * (enemy.Leader.Object.transform.position.y - _trainY);
                hold = Mathf.Max(hold, leaderDistance - _guardForwardOffset);
            }
            return Mathf.Clamp(distance - hold, 0f, enemy.Rule.Speed * dt);
        }

        /// <summary>
        /// 원거리 공격 적이 사거리에 멈춰 있으면 공격 간격마다 열차를 향한 포탄을 쏜다.
        /// enemy와 현재 position, dt를 사용하며, 공격 대기 시간과 _enemyShots를 변경한다.
        /// </summary>
        private void TickEnemyAttack(Enemy enemy, Vector3 position, float dt)
        {
            if (enemy.Rule.AttackRange <= 0 || enemy.Side * (position.y - _trainY) > enemy.Rule.AttackRange + 0.01f) return;
            enemy.AttackCooldown -= dt;
            if (enemy.AttackCooldown > 0) return;
            enemy.AttackCooldown = enemy.Rule.AttackInterval;
            Vector3 muzzle = new Vector3(position.x, position.y - enemy.Side * 0.3f, -0.5f);
            Vector3 target = new Vector3(NearestTrainX(position.x), _trainY, -0.5f);
            GameObject shell = CreateEffect("Enemy Shell", muzzle, new Vector2(0.16f, 0.32f), new Color(0.95f, 0.3f, 0.2f), 8);
            Vector3 direction = target - muzzle;
            shell.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            SpawnMarker(muzzle, 0.2f);
            _enemyShots.Add(new EnemyShot { Object = shell, Target = target, Damage = enemy.Rule.AttackDamage, Speed = enemy.Rule.AttackProjectileSpeed });
        }

        /// <summary>
        /// 적 포탄을 열차 목표 지점으로 이동시키고 도착하면 열차 내구도를 깎는다.
        /// dt를 사용하며, _enemyShots와 _trainHealth를 변경한다.
        /// </summary>
        private void MoveEnemyShots(float dt)
        {
            for (int i = _enemyShots.Count - 1; i >= 0; i--)
            {
                EnemyShot shot = _enemyShots[i];
                shot.Object.transform.position = Vector3.MoveTowards(shot.Object.transform.position, shot.Target, shot.Speed * dt);
                if (Vector2.Distance(shot.Object.transform.position, shot.Target) > 0.13f) continue;
                SpawnMarker(shot.Target, 0.25f);
                Destroy(shot.Object);
                _enemyShots.RemoveAt(i);
                _trainHealth -= shot.Damage;
                if (_trainHealth > 0) continue;
                Finish(false);
                return;
            }
        }

        /// <summary>각 설치 포탑의 공격 간격과 사거리로 가장 가까운 적을 골라 피해를 준다.</summary>
        private void FireTurrets(float dt)
        {
            for (int slot = 0; slot < _turretSlots.Length; slot++)
            {
                if (!_run.TurretStats(slot, out float damage, out float range, out float interval)) continue;
                _turretCooldowns[slot] -= dt;
                if (_turretCooldowns[slot] > 0) continue;
                int target = -1;
                float closest = float.MaxValue;
                for (int i = 0; i < _enemies.Count; i++)
                {
                    float distance = Vector2.Distance(_turretSlots[slot].position, _enemies[i].Object.transform.position);
                    if (distance > range || distance >= closest) continue;
                    closest = distance;
                    target = i;
                }
                if (target < 0) continue;
                _turretCooldowns[slot] = interval;
                LaunchProjectile(_turretSlots[slot].position, _enemies[target], damage);
            }
        }

        /// <summary>포탑 위치에서 목표 적을 향하는 탄환을 만들고 명중할 때 적용할 피해량을 저장한다.</summary>
        private void LaunchProjectile(Vector3 origin, Enemy target, float damage)
        {
            Vector3 muzzle = new Vector3(origin.x, origin.y + 0.35f, -0.5f);
            GameObject shot = CreateEffect("Turret Shot", muzzle, new Vector2(0.12f, 0.3f), new Color(1f, 0.65f, 0.08f), 8);
            Vector3 direction = target.Object.transform.position - muzzle;
            shot.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            SpawnMarker(muzzle, 0.16f);
            _projectiles.Add(new Projectile { Object = shot, Target = target, Damage = damage });
        }

        /// <summary>dt 동안 탄환을 목표로 이동시키고 명중 시 적 체력과 피격 표시를 갱신한다.</summary>
        private void MoveProjectiles(float dt)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile shot = _projectiles[i];
                if (shot.Target.Object == null || !_enemies.Contains(shot.Target))
                {
                    Destroy(shot.Object);
                    _projectiles.RemoveAt(i);
                    continue;
                }
                Vector3 destination = shot.Target.Object.transform.position;
                shot.Object.transform.position = Vector3.MoveTowards(shot.Object.transform.position, new Vector3(destination.x, destination.y, -0.3f), _rules.Stage.ProjectileSpeed * dt);
                if (Vector2.Distance(shot.Object.transform.position, destination) > 0.13f) continue;
                int index = _enemies.IndexOf(shot.Target);
                SpawnMarker(destination, 0.16f);
                DamageEnemy(index, shot.Damage);
                Destroy(shot.Object);
                _projectiles.RemoveAt(i);
            }
        }

        /// <summary>적 번호에 피해를 적용하고 처치 시 경험치·보스 승리를 처리한다.</summary>
        private void DamageEnemy(int index, float damage)
        {
            if (index >= _enemies.Count) return;
            Enemy enemy = _enemies[index];
            // 방패수호병처럼 받는 피해 배율이 낮은 적은 실제로 깎인 양을 표시한다.
            damage *= enemy.Rule.DamageTaken;
            enemy.Health -= damage;
            Vector3 hitPosition = enemy.Object.transform.position;
            SpawnDamageText(hitPosition, damage);
            enemy.HealthFill.localScale = new Vector3(0.94f * Mathf.Clamp01(enemy.Health / enemy.MaxHealth), 0.7f, 1);
            if (enemy.Health > 0) return;
            bool boss = enemy.Rule.Role == BattleEnemyRole.Boss;
            Destroy(enemy.Object);
            _enemies.RemoveAt(index);
            // 보스가 여럿이면 마지막 보스를 처치할 때 승리한다.
            if (boss && !AnyBossAlive()) Finish(true);
            else
            {
                RefreshUi();
            }
        }

        /// <summary>
        /// 전장에 아직 살아 있는 보스가 있는지 확인한다.
        /// _enemies를 사용하며, 보스가 하나라도 남아 있으면 true를 반환한다.
        /// </summary>
        private bool AnyBossAlive()
        {
            foreach (Enemy enemy in _enemies)
                if (enemy.Rule.Role == BattleEnemyRole.Boss) return true;
            return false;
        }

        /// <summary>피해량을 적 머리 위에 잠깐 표시하고 위로 떠오르게 한다.</summary>
        private void SpawnDamageText(Vector3 position, float damage)
        {
            GameObject popup = new GameObject("Damage", typeof(TextMeshPro));
            popup.transform.position = new Vector3(position.x, position.y + 0.65f, -0.5f);
            TextMeshPro label = popup.GetComponent<TextMeshPro>();
            label.text = Mathf.CeilToInt(damage).ToString();
            label.fontSize = 4;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.84f, 0.42f);
            popup.AddComponent<BattleDamagePopup>();
        }

        /// <summary>주포·포탑의 타격 위치에 잠시 보이는 도형을 생성해 공격 결과를 표시한다.</summary>
        private void SpawnMarker(Vector3 position, float radius)
        {
            float size = Mathf.Clamp(radius, 0.16f, 0.5f);
            GameObject flare = CreateEffect("Impact", new Vector3(position.x, position.y, -0.6f), new Vector2(size * 1.5f, size * 0.45f), new Color(1f, 0.35f, 0.03f), 9);
            GameObject core = CreateEffect("Impact Core", new Vector3(position.x, position.y, -0.61f), new Vector2(size * 0.42f, size * 1.2f), new Color(1f, 0.8f, 0.18f), 10);
            Destroy(flare, 0.16f);
            Destroy(core, 0.16f);
        }

        /// <summary>position에 size와 color를 적용한 스프라이트를 만들고 정렬 순서가 지정된 오브젝트를 반환한다.</summary>
        private GameObject CreateEffect(string name, Vector3 position, Vector2 size, Color color, int sortingOrder)
        {
            GameObject effect = new GameObject(name, typeof(SpriteRenderer));
            effect.transform.position = position;
            effect.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            renderer.sprite = _effectSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return effect;
        }
        /// <summary>선택한 세 장 중 카드 하나를 적용하고 설치·강화된 포탑의 전장 표시를 갱신한다.</summary>
        private void ChooseCard(int index)
        {
            if (!_run.ChooseCard(index)) return;
            RefreshTurretVisuals();
            ShowCards();
            RefreshUi();
        }

        /// <summary>마우스 클릭에 가장 가까운 빈 거치대를 골라 대기 중인 포탑을 설치한다.</summary>
        private void HandlePlacement()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || EventSystem.current.IsPointerOverGameObject()) return;
            Vector2 point = _battleCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            int selected = -1;
            float closest = 0.8f;
            for (int slot = 0; slot < _turretSlots.Length; slot++)
            {
                if (slot >= _run.SlotCount || slot >= _run.Turrets.Count) continue;
                float distance = Vector2.Distance(point, _turretSlots[slot].position);
                if (distance >= closest) continue;
                closest = distance;
                selected = slot;
            }
            PlaceAt(selected);
        }

        /// <summary>선택한 slot에 대기 포탑을 설치·교체·합치고 화면을 갱신한다.</summary>
        private void PlaceAt(int selected)
        {
            if (!_run.IsPlacingTurret || !_run.PlaceTurret(selected)) return;
            ClearSlotHighlights();
            _statusText.text = "";
            if (_placementPanel != null) _placementPanel.SetActive(false);
            RefreshTurretVisuals();
            ShowCards();
            RefreshUi();
        }

        /// <summary>선택한 포탑과 각 거치대의 상태에 따라 설치·합치기·교체 색을 표시한다.</summary>
        private void HighlightSlots()
        {
            int installed = 0;
            foreach (BattleTurret turret in _run.Turrets) if (turret != null) installed++;
            if (_placementPanel != null) _placementPanel.SetActive(false);
            for (int i = 0; i < _turretSlots.Length; i++)
            {
                if (i >= _run.SlotCount) continue;
                BattleTurret existing = _run.Turrets[i];
                Color color = existing == null ? new Color(0.48f, 0.91f, 0.45f) : existing.TurretId == _run.PendingTurretId ? new Color(0.4f, 0.85f, 1f) : new Color(1f, 0.65f, 0.3f);
                bool allowed = existing != null || installed < _run.SlotCount;
                _turretSlots[i].Find("Mount Base").GetComponent<Renderer>().material.color = color;
                LineRenderer outline = _turretSlots[i].Find("Selection Outline").GetComponent<LineRenderer>();
                outline.enabled = allowed;
                outline.startColor = outline.endColor = color;
                if (_placementButtons == null || i >= _placementButtons.Length) continue;
                _placementButtons[i].interactable = allowed;
                Color panelColor = existing == null ? new Color(0.20f, 0.37f, 0.26f) : existing.TurretId == _run.PendingTurretId ? new Color(0.18f, 0.34f, 0.38f) : new Color(0.43f, 0.27f, 0.16f);
                _placementButtons[i].GetComponent<Image>().color = allowed ? panelColor : new Color(0.22f, 0.19f, 0.2f);
                string action = existing == null ? "설치" : existing.TurretId == _run.PendingTurretId ? "합치기" : "교체";
                string name = existing == null ? "빈 거치대" : _rules.Turret(existing.TurretId).Name + $" Lv.{existing.Level}";
                _placementTexts[i].text = $"{i + 1}번 · {(allowed ? action : "불가")}\n{name}";
            }
            _statusText.text = "거치대를 클릭하세요   초록: 설치  파랑: 합치기  주황: 교체";
        }

        /// <summary>포탑 배치가 끝나면 거치대 색을 원래 황동색으로 되돌린다.</summary>
        private void ClearSlotHighlights()
        {
            foreach (Transform slot in _turretSlots)
            {
                slot.Find("Mount Base").GetComponent<Renderer>().material.color = new Color(0.72f, 0.48f, 0.25f);
                slot.Find("Selection Outline").GetComponent<LineRenderer>().enabled = false;
            }
        }

        /// <summary>현재 전투 레벨에 맞춰 추가 객차와 지붕 거치대의 활성 상태를 갱신한다.</summary>
        private void RefreshCars()
        {
            for (int i = 0; i < _extraCars.Length; i++) _extraCars[i].SetActive(_run.Level >= 3 + i * 3);
            for (int i = 0; i < _turretSlots.Length; i++) _turretSlots[i].gameObject.SetActive(i < _run.SlotCount);
        }

        /// <summary>마우스에 가까운 설치 포탑의 이름과 현재 공격 능력치를 상태 문구에 표시한다.</summary>
        private void ShowTurretHover()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 point = _battleCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            for (int i = 0; i < _turretSlots.Length; i++)
            {
                BattleTurret turret = _run.Turrets[i];
                if (turret == null || Vector2.Distance(point, _turretSlots[i].position) > 0.5f) continue;
                if (_run.TurretStats(i, out float damage, out float range, out float interval))
                    _statusText.text = $"{_rules.Turret(turret.TurretId).Name} Lv.{turret.Level}  피해 {damage:0.#}  사거리 {range:0.#}  간격 {interval:0.0}초";
                return;
            }
            _statusText.text = "";
        }

        /// <summary>설치 상태에 따라 각 거치대의 포탑 외형을 만들고 강화 단계의 크기를 갱신한다.</summary>
        private void RefreshTurretVisuals()
        {
            for (int slot = 0; slot < _turretSlots.Length && slot < _run.Turrets.Count; slot++)
            {
                BattleTurret turret = _run.Turrets[slot];
                if (turret == null) continue;
                Transform visual = _turretSlots[slot].Find("Installed Turret");
                if (visual == null)
                {
                    GameObject body = new GameObject("Installed Turret", typeof(SpriteRenderer));
                    body.transform.SetParent(_turretSlots[slot], false);
                    body.transform.localPosition = new Vector3(0, 0.11f, -0.05f);
                    SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
                    renderer.sprite = _turretSprite;
                    renderer.sortingOrder = 2;
                    visual = body.transform;
                }
                visual.localScale = Vector3.one * (0.9f / _turretSprite.bounds.size.x);
            }
        }
        /// <summary>현재 레벨 카드의 종류와 칸·포탑 이름을 씬에 연결된 버튼 세 개에 표시한다.</summary>
        private void ShowCards()
        {
            _cardPanel.SetActive(_run.HasCardChoice);
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                bool visible = i < _run.Cards.Count;
                _cardButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                BattleCard card = _run.Cards[i];
                TurretRule rule = _rules.Turret(card.TurretId);
                _cardTexts[i].text = $"{rule.Name}\n피해 {rule.BaseDamage:0.#}  사거리 {rule.Range:0.#}\n공격 간격 {rule.AttackInterval:0.0}초";
            }
        }

        /// <summary>전투 결과에 따라 마르크·설계도·승객 보상을 계산하고 결과 화면에 표시한다.</summary>
        private void Finish(bool victory)
        {
            if (_finished) return;
            _finished = true;
            if (victory && !string.IsNullOrWhiteSpace(_nextHubId))
                GameServices.Get<ILineOpener>()?.Open(_nextHubId);
            BattleReward reward = BattleIntegration.ResolveReward(_rules, victory, GameServices.Get<IMoney>(),
                GameServices.Get<IHabitatCodex>(), GameServices.Get<IHabitatClusters>(),
                GameServices.Get<IPassengers>(), "battle-poc", _random);
            _cardPanel.SetActive(false);
            _resultPanel.SetActive(true);
            _resultText.text = (victory ? "승리! 다음 마을이 열립니다." : "패배. 열차로 후퇴합니다.") +
                $"\n마르크 +{reward.Marks}" +
                (reward.HintId == null ? "" : "\n설계도 힌트: " + HintText(reward.HintId)) +
                (reward.PassengerId == null ? "" : $"\n승객: {reward.PassengerId}");
            _resultText.text += victory ? "\nR: 다음 전투" : "\nR: 다시 도전";
            Completed?.Invoke(victory);
        }

        /// <summary>보상 typeId에 대응하는 건물 이름·필요 칸 수·재료 역할을 카탈로그에서 읽어 힌트 문구를 반환한다.</summary>
        private static string HintText(string typeId)
        {
            HabitatCatalog catalog = HabitatsInstaller.Catalog;
            if (catalog == null)
            {
                TextAsset types = Resources.Load<TextAsset>("Habitats/HabitatTypes");
                TextAsset recipes = Resources.Load<TextAsset>("Habitats/HabitatRecipes");
                if (types == null || recipes == null) return typeId;
                catalog = HabitatCatalog.Parse(types.text, recipes.text);
            }
            HabitatType building = catalog.Get(typeId);
            return building == null ? typeId : $"{building.Name} ({building.RequiredCount}칸)\n{building.MaterialRole}";
        }

        /// <summary>시간·열차 내구도·레벨·주포 쿨타임을 씬의 상태 글자에 갱신한다.</summary>
        private void RefreshUi()
        {
            if (_healthFill == null || _experienceFill == null) return;
            if (_currencyText != null) _currencyText.text = $"마르크 {GameServices.Get<IMoney>()?.Balance ?? 0}";
            _healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)_trainHealth / _rules.Stage.TrainHealth), 1f);
            _healthText.text = $"체력  {Mathf.Max(0, _trainHealth)} / {_rules.Stage.TrainHealth}";
            _experienceFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_run.Time / _rules.Stage.BossSpawnSeconds), 1f);
            _experienceText.text = $"보스 출현까지 {Mathf.Max(0, Mathf.CeilToInt(_rules.Stage.BossSpawnSeconds - _run.Time))}초";
        }
    }
}
