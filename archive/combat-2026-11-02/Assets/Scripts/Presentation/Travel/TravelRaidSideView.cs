using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Train.Equipment;

using RaidInstaller = IronRing.Combat.Raid.RaidInstaller;
using RaidSystem = IronRing.Combat.Raid.RaidSystem;

namespace IronRing.Presentation.Travel
{
    /// <summary>
    /// 스토리 037. 달리면서 치르는 습격(RaidSystem enroute)을 횡스크롤 화면에 프리미티브로 그린다.
    /// 좌표 사상: 습격 X = 가까운 층(TravelSideView.Near)의 로컬 x(= 출발 구간 진행도 × 구간 길이, 풍경과 함께 흐른다),
    /// 열차 머리 X = 진행도 × 구간 길이, 꼬리 = 머리 − 열차 길이, 습격 Y = 열(lane) → 월드 y = 지면 + LaneBase + Y × LaneScale.
    /// 둥지(원+사각)는 길가에, 몬스터는 IRaid.Monsters를 따라, 포탄(작은 원)은 쏜 칸(ITrainLoadout)에서, 명중 흰 원, 죽으면 조각이 튄다.
    /// 클릭은 몬스터·둥지의 BoxCollider2D로 IRaid.ShootMonster/ShootNest를 부른다. HUD(RaidScreen)·토스트·결과 흐름은 그대로다.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class TravelRaidSideView : MonoBehaviour
    {
        [SerializeField] private TravelSideView _view;

        private RaidSystem _raid;
        private IMapAnchors _anchors;
        private TravelCatalog _catalog;
        private Transform _root;
        private readonly Dictionary<int, Transform> _monsters = new Dictionary<int, Transform>();
        private readonly List<Transform> _nests = new List<Transform>();
        private readonly List<EquipmentSlot> _guns = new List<EquipmentSlot>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        private IEnumerator Start()
        {
            if (_view == null) _view = GetComponent<TravelSideView>();
            if (!TravelViewMode.IsSide || _view == null) yield break;
            while (!GameServices.IsReady) yield return null;
            _raid = RaidInstaller.System;
            _anchors = GameServices.Get<IMapAnchors>();
            _catalog = _view.Catalog;
            if (_raid == null || _catalog == null) yield break;
            var train = FindFirstObjectByType<IronRing.Track.TrainController>();
            _raid.Mode = "enroute";
            _raid.EnrouteLength = _view.SegmentLength;
            _raid.TrainPosition = () => (_view.RaidFrontX, 0f);
            _raid.TrainTailPosition = () => (_view.RaidFrontX - _view.TrainLength, 0f);
            _raid.EdgeProgress = () => train != null ? train.TravelProgress : 0f;
            _root = new GameObject("RaidLayer").transform;
            _root.SetParent(transform, false);
            _raid.RaidStarted += OnStarted;
            _raid.RaidEnded += OnEnded;
            _raid.MonsterSpawned += OnSpawned;
            _raid.MonsterKilled += OnKilled;
            _raid.MonsterReached += OnReached;
            _raid.NestDestroyed += OnNestDestroyed;
            _raid.ShotFired += OnShot;
            _raid.GunFired += OnGunFired;
        }

        private void OnDestroy()
        {
            if (_raid == null) return;
            _raid.RaidStarted -= OnStarted;
            _raid.RaidEnded -= OnEnded;
            _raid.MonsterSpawned -= OnSpawned;
            _raid.MonsterKilled -= OnKilled;
            _raid.MonsterReached -= OnReached;
            _raid.NestDestroyed -= OnNestDestroyed;
            _raid.ShotFired -= OnShot;
            _raid.GunFired -= OnGunFired;
            _raid.TrainPosition = null; _raid.TrainTailPosition = null; _raid.EdgeProgress = null;
        }

        /// <summary>습격 좌표 (x, y) → 월드 위치.</summary>
        public Vector3 ToWorld(float x, float y)
        {
            float wx = _view.Near.position.x + x;
            float wy = _view.GroundWorldY + _catalog.LaneBase + y * _catalog.LaneScale;
            return new Vector3(wx, wy, transform.position.z);
        }

        /// <summary>월드 위치 → 습격 좌표(클릭 보조용).</summary>
        public Vector2 ToRaid(Vector3 world) => new Vector2(world.x - _view.Near.position.x, (world.y - _view.GroundWorldY - _catalog.LaneBase) / Mathf.Max(0.01f, _catalog.LaneScale));

        /// <summary>열(lane) Y가 작을수록(앞 열) 위에 그린다.</summary>
        private int Order(float laneY) => _catalog.SortingBase + 20 - Mathf.RoundToInt(laneY * 4f);

        private void OnStarted()
        {
            if (!_raid.ActiveEnroute) return;
            ClearAll();
            for (int i = 0; i < _raid.Nests.Count; i++)
            {
                RaidUnit nest = _raid.Nests[i];
                var go = new GameObject("Nest" + i);
                go.transform.SetParent(_root, false);
                int o = Order(nest.Y) - 2;
                TravelPrimitives prims = _view.Prims;
                prims.Rect(go.transform, "Mound", 0f, -_catalog.NestSize * 0.25f, _catalog.NestSize * 1.3f, _catalog.NestSize * 0.4f, _catalog.NestMound, o);
                prims.Circle(go.transform, "Sac", 0f, _catalog.NestSize * 0.1f, _catalog.NestSize, _catalog.Nest, o + 1);
                AddTarget(go, true, i, new Vector2(_catalog.NestSize * 1.3f, _catalog.NestSize * 1.2f));
                _nests.Add(go.transform);
                PaintNest(i);
            }
            BuildGuns();
            IronRing.Hud.Ui.ToastScreen.Show(Strings("travel_raid_start", "습격! 달리면서 막아라"), 1.6f);
            Debug.Log($"TRAVEL: raid side view start nests={_raid.Nests.Count} guns={_guns.Count}");
        }

        private void OnEnded(bool victory)
        {
            foreach (Transform t in _monsters.Values) if (t != null) { Debris(t.position, _catalog.Debris); Destroy(t.gameObject); }
            _monsters.Clear();
            foreach (Transform t in _nests) if (t != null) Destroy(t.gameObject);
            _nests.Clear();
            _guns.Clear();
        }

        private void ClearAll()
        {
            foreach (Transform t in _monsters.Values) if (t != null) Destroy(t.gameObject);
            _monsters.Clear();
            foreach (Transform t in _nests) if (t != null) Destroy(t.gameObject);
            _nests.Clear();
        }

        private void BuildGuns()
        {
            _guns.Clear();
            ITrainLoadout loadout = GameServices.Get<ITrainLoadout>();
            if (loadout == null) return;
            // RaidSystem의 포 순번: 포탑 먼저, 그다음 중포(RaidView와 같은 규칙).
            foreach (string id in new[] { TrainLoadout.TURRET, TrainLoadout.HEAVY_TURRET })
                foreach (EquipmentSlot slot in loadout.Slots)
                    if (slot.IsAnchor && slot.EquipmentId == id) _guns.Add(slot);
        }

        private void PaintNest(int index)
        {
            if (index < 0 || index >= _nests.Count || _nests[index] == null) return;
            bool alive = _raid.Nests[index].Alive;
            foreach (SpriteRenderer renderer in _nests[index].GetComponentsInChildren<SpriteRenderer>())
                if (renderer.name == "Sac") renderer.color = alive ? _catalog.Nest : _catalog.NestBroken;
            Collider2D collider = _nests[index].GetComponent<Collider2D>();
            if (collider != null) collider.enabled = alive;
        }

        private void OnSpawned(int id)
        {
            if (!_raid.ActiveEnroute) return;
            RaidUnit unit = Find(id);
            if (unit == null) return;
            var go = new GameObject(unit.Kind + "#" + id);
            go.transform.SetParent(_root, false);
            go.transform.position = ToWorld(unit.X, unit.Y);
            int o = Order(unit.Y);
            TravelPrimitives prims = _view.Prims;
            if (unit.Kind == "spider")
            {
                float s = _catalog.SpiderSize;
                prims.Rect(go.transform, "Legs", 0f, -s * 0.3f, s * 1.5f, s * 0.12f, _catalog.Chimney, o);
                prims.Circle(go.transform, "Body", 0f, 0f, s, _catalog.Spider, o + 1);
                prims.Circle(go.transform, "Eye", -s * 0.3f, s * 0.1f, s * 0.22f, _catalog.AutomatonEye, o + 2);
                AddTarget(go, false, id, new Vector2(s * 1.5f, s * 1.2f));
            }
            else
            {
                float s = _catalog.AutomatonSize;
                prims.Rect(go.transform, "Body", 0f, 0f, s * 0.7f, s, _catalog.Automaton, o + 1);
                prims.Circle(go.transform, "Head", 0f, s * 0.65f, s * 0.45f, _catalog.Automaton, o + 1);
                prims.Rect(go.transform, "Eye", -s * 0.1f, s * 0.68f, s * 0.15f, s * 0.08f, _catalog.AutomatonEye, o + 2);
                AddTarget(go, false, id, new Vector2(s * 0.9f, s * 1.6f));
            }
            _monsters[id] = go.transform;
        }

        private static void AddTarget(GameObject go, bool nest, int id, Vector2 size)
        {
            var target = go.AddComponent<TravelRaidTarget>();
            target.IsNest = nest; target.Id = id;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        private void OnKilled(int id)
        {
            if (!_monsters.TryGetValue(id, out Transform t)) return;
            _monsters.Remove(id);
            if (t == null) return;
            Debris(t.position, _catalog.Debris);
            Destroy(t.gameObject);
        }

        private void OnReached(int id)
        {
            if (!_monsters.TryGetValue(id, out Transform t)) return;
            _monsters.Remove(id);
            if (t == null) return;
            Flash(t.position, 0.9f);
            Destroy(t.gameObject);
        }

        private void OnNestDestroyed(int index)
        {
            PaintNest(index);
            if (index >= 0 && index < _nests.Count && _nests[index] != null) Debris(_nests[index].position, _catalog.NestBroken);
        }

        private void OnShot(bool player, float x, float y)
        {
            if (!_raid.ActiveEnroute) return;
            if (!player && _guns.Count > 0) return; // 포탑 발사는 GunFired가 자기 칸에서 그린다.
            Shell(_view.CarWorld(0), ToWorld(x, y));
        }

        private void OnGunFired(int gun, float x, float y)
        {
            if (!_raid.ActiveEnroute || _guns.Count == 0) return;
            EquipmentSlot slot = _guns[Mathf.Clamp(gun, 0, _guns.Count - 1)];
            Shell(_view.SlotWorld(slot.Car, slot.Column, slot.Row, slot.Width), ToWorld(x, y));
        }

        private void Shell(Vector3 from, Vector3 to)
        {
            SpriteRenderer shell = _view.Prims.Circle(_root, "Shell", 0f, 0f, _catalog.ShellSize, _catalog.Shell, _catalog.SortingBase + 40);
            shell.gameObject.AddComponent<TravelTransient>().Fly(from, to, _catalog.ShellSeconds);
            StartCoroutine(FlashLater(to, _catalog.ShellSeconds));
        }

        private IEnumerator FlashLater(Vector3 at, float delay)
        {
            yield return new WaitForSeconds(delay);
            Flash(at, 0.5f);
        }

        private void Flash(Vector3 world, float size)
        {
            Vector3 local = _root.InverseTransformPoint(world);
            SpriteRenderer flash = _view.Prims.Circle(_root, "Flash", local.x, local.y, size, _catalog.Flash, _catalog.SortingBase + 41);
            flash.gameObject.AddComponent<TravelTransient>().Drift(Vector3.zero, _catalog.FlashSeconds, 1.5f, 0f);
        }

        private void Debris(Vector3 world, Color color)
        {
            Vector3 local = _root.InverseTransformPoint(world);
            for (int i = 0; i < _catalog.DebrisCount; i++)
            {
                float angle = (40f + i * 50f) * Mathf.Deg2Rad;
                SpriteRenderer piece = _view.Prims.Rect(_root, "Debris", local.x, local.y, 0.14f, 0.1f, color, _catalog.SortingBase + 39);
                piece.gameObject.AddComponent<TravelTransient>().Drift(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 3f, _catalog.DebrisSeconds, 0f, 720f);
            }
            Flash(world, 0.6f);
        }

        private void LateUpdate()
        {
            if (_raid == null || !_raid.ActiveEnroute) return;
            foreach (RaidUnit unit in _raid.Monsters)
            {
                if (!_monsters.TryGetValue(unit.Id, out Transform t) || t == null) continue;
                Vector3 target = ToWorld(unit.X, unit.Y);
                // 위아래로 조금 뛰는 걸음.
                target.y += Mathf.Abs(Mathf.Sin(Time.time * 12f + unit.Id)) * 0.06f;
                float dx = target.x - t.position.x;
                if (Mathf.Abs(dx) > 0.0001f) t.localScale = new Vector3(dx < 0f ? 1f : -1f, 1f, 1f);
                t.position = target;
            }
            for (int i = 0; i < _nests.Count && i < _raid.Nests.Count; i++)
                if (_nests[i] != null) _nests[i].position = ToWorld(_raid.Nests[i].X, _raid.Nests[i].Y);
        }

        private void Update()
        {
            if (_raid == null || !_raid.ActiveEnroute || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Camera camera = _anchors?.MapCamera;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (camera == null || IsPointerOverUi(pointer)) return;
            Ray ray = camera.ScreenPointToRay(pointer);
            foreach (RaycastHit2D hit in Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity))
            {
                if (hit.collider == null || !hit.collider.TryGetComponent(out TravelRaidTarget target)) continue;
                if (target.IsNest ? _raid.ShootNest(target.Id) : _raid.ShootMonster(target.Id)) return;
            }
            Vector3 world = camera.ScreenToWorldPoint(pointer);
            Vector2 raid = ToRaid(world);
            int nearest = _raid.NearestMonster(raid.x, raid.y, _catalog.ClickRadius);
            if (nearest >= 0) _raid.ShootMonster(nearest);
        }

        private bool IsPointerOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _uiHits);
            return _uiHits.Count > 0;
        }

        private RaidUnit Find(int id)
        {
            foreach (RaidUnit unit in _raid.Monsters) if (unit.Id == id) return unit;
            return null;
        }

        private static string Strings(string key, string fallback)
        {
            string value = IronRing.Localization.Strings.Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
    }
}
