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
        [Header("전장 배치")]
        [SerializeField] private Camera _battleCamera;
        [SerializeField] private Transform _enemyRoot;
        [SerializeField] private Sprite _enemySprite;
        [SerializeField] private Transform[] _turretSlots;
        [SerializeField] private GameObject[] _extraCars;
        [SerializeField] private float _spawnY = 4.3f;
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
        private Sprite _effectSprite;
        private Sprite _turretSprite;
        private BattleRules _rules;
        private BattleRun _run;
        private float[] _turretCooldowns;
        private float _spawnClock;
        private bool _bossSpawned;
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
            public float Health;
            public float MaxHealth;
            public Transform HealthFill;
            public float Speed;
            public int ContactDamage;
            public int Experience;
            public bool Boss;
            public float LaneX;
            public float Phase;
        }

        private sealed class Projectile
        {
            public GameObject Object;
            public Enemy Target;
            public float Damage;
        }

        void Start()
        {
            _effectSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _turretSprite = _turretSlots[0].parent.Find("Main Gun").GetComponent<SpriteRenderer>().sprite;
            _rules = BattleRules.Load();
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
            RefreshUi();
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
            _enemies.Clear();
            _projectiles.Clear();
            foreach (Transform slot in _turretSlots)
            {
                Transform visual = slot.Find("Installed Turret");
                if (visual != null) { visual.SetParent(null); Destroy(visual.gameObject); }
            }
            _run = new BattleRun(_rules, _loadout, _random.Next());
            _run.RestoreArmament();
            RefreshCars();
            Array.Clear(_turretCooldowns, 0, _turretCooldowns.Length);
            _trainHealth = _rules.Stage.TrainHealth;
            _spawnClock = 0;
            _bossSpawned = false;
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
            if (point.y <= _trainY || point.y > _spawnY + 0.5f) return;
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

        /// <summary>전투 시간과 JSON 출현 간격에 따라 일반 적과 종료 보스를 전장 위쪽에 만든다.</summary>
        private void Spawn(float dt)
        {
            _spawnClock += dt;
            if (_run.Time < _rules.Stage.BossSpawnSeconds && _spawnClock >= _rules.Stage.SpawnIntervalSeconds)
            {
                _spawnClock = 0;
                CreateEnemy(false);
            }
            if (!_bossSpawned && _run.Time >= _rules.Stage.BossSpawnSeconds)
            {
                _bossSpawned = true;
                CreateEnemy(true);
            }
        }

        /// <summary>boss 여부에 따라 JSON 체력·속도·피해를 적용한 적 도형을 생성해 적 목록에 넣는다.</summary>
        private void CreateEnemy(bool boss)
        {
            GameObject shape = new GameObject(boss ? "Boss" : "Enemy", typeof(SpriteRenderer));
            shape.name = boss ? "Boss" : "Enemy";
            shape.transform.SetParent(_enemyRoot, false);
            shape.transform.position = new Vector3((float)(_random.NextDouble() * 2 - 1) * _spawnHalfWidth, _spawnY, 0);
            shape.transform.localScale = _perspectiveEnemies ? Vector3.one * (_distantEnemyHeight / _enemySprite.bounds.size.y) : boss ? Vector3.one * 1.8f : Vector3.one * 1.05f;
            SpriteRenderer enemyRenderer = shape.GetComponent<SpriteRenderer>();
            enemyRenderer.sprite = _enemySprite;
            enemyRenderer.color = boss ? new Color(0.95f, 0.57f, 0.48f) : Color.white;
            float maxHealth = boss ? _rules.Stage.BossHealth : _rules.Stage.EnemyHealth;
            GameObject bar = CreateEffect("Health Back", shape.transform.position, new Vector2(1.2f, 0.14f), new Color(0.12f, 0.08f, 0.08f), 3);
            bar.transform.SetParent(shape.transform, false);
            bar.transform.localPosition = new Vector3(0, 0.75f, -0.05f);
            bar.transform.localScale = new Vector3(1.2f, 0.14f, 1);
            if (_perspectiveEnemies)
            {
                bar.transform.localPosition = new Vector3(0f, _enemySprite.bounds.extents.y + 0.5f, -0.05f);
                bar.transform.localScale = new Vector3(_enemySprite.bounds.size.x * 0.65f, _enemySprite.bounds.size.y * 0.05f, 1f);
            }
            GameObject fill = CreateEffect("Health Fill", shape.transform.position, Vector2.one, new Color(0.85f, 0.22f, 0.13f), 4);
            fill.transform.SetParent(bar.transform, false);
            fill.transform.localPosition = new Vector3(0, 0, -0.05f);
            fill.transform.localScale = new Vector3(0.94f, 0.7f, 1);
            _enemies.Add(new Enemy
            {
                Object = shape,
                Health = maxHealth,
                MaxHealth = maxHealth,
                HealthFill = fill.transform,
                Speed = boss ? _rules.Stage.BossSpeed : _rules.Stage.EnemySpeed,
                ContactDamage = boss ? _rules.Stage.BossContactDamage : _rules.Stage.EnemyContactDamage,
                Experience = boss ? _rules.Stage.BossExperience : _rules.Stage.EnemyExperience,
                Boss = boss,
                LaneX = shape.transform.position.x,
                Phase = (float)_random.NextDouble() * 6.28f
            });
        }

        /// <summary>적을 열차 방향으로 이동시키고 도달한 적의 접촉 피해를 열차 내구도에 반영한다.</summary>
        private void MoveEnemies(float dt)
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = _enemies[i];
                enemy.Object.transform.position += Vector3.down * (enemy.Speed * dt);
                if (_perspectiveEnemies)
                {
                    float progress = Mathf.InverseLerp(_spawnY, _trainY, enemy.Object.transform.position.y);
                    float height = Mathf.Lerp(_distantEnemyHeight, _nearEnemyHeight, progress) * (enemy.Boss ? 1.6f : 1f);
                    enemy.Object.transform.localScale = Vector3.one * (height / _enemySprite.bounds.size.y);
                    Vector3 position = enemy.Object.transform.position;
                    position.x = enemy.LaneX + Mathf.Sin(_run.Time * 4f + enemy.Phase) * _enemySway * progress;
                    enemy.Object.transform.position = position;
                }
                if (enemy.Object.transform.position.y > _trainY + 0.45f) continue;
                _trainHealth -= enemy.ContactDamage;
                Destroy(enemy.Object);
                _enemies.RemoveAt(i);
                if (_trainHealth <= 0) Finish(false);
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
            enemy.Health -= damage;
            Vector3 hitPosition = enemy.Object.transform.position;
            SpawnDamageText(hitPosition, damage);
            enemy.HealthFill.localScale = new Vector3(0.94f * Mathf.Clamp01(enemy.Health / enemy.MaxHealth), 0.7f, 1);
            if (enemy.Health > 0) return;
            bool boss = enemy.Boss;
            Destroy(enemy.Object);
            _enemies.RemoveAt(index);
            if (boss) Finish(true);
            else
            {
                RefreshUi();
            }
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
