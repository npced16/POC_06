using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using IronRing.Contracts.Presentation;
using IronRing.Contracts;
using IronRing.Train.Equipment;
using IronRing.Train.Scrap;

using RaidInstaller = IronRing.Combat.Raid.RaidInstaller;
using RaidSystem = IronRing.Combat.Raid.RaidSystem;

namespace IronRing.Presentation.Travel
{
    /// <summary>
    /// 스토리 038(+03:30 지시). 중간역 정차 디펜스(RaidSystem StopMode, 끝 시간 없음)를 탑뷰 프리미티브로 그리고 파밍을 돌린다.
    /// 좌표 사상: 습격 X/Y = StopTopView 로컬(X = 선로 방향, Y = 옆). 입구(둥지)는 Raid.json _stopEntrances(열차 머리 기준),
    /// 몬스터는 IRaid.Monsters, 포탄은 장착 칸(ITrainLoadout)에서, 명중 흰 원·죽으면 조각. 클릭: 고철 더미 → 인부 보내기,
    /// 몬스터·입구 → 열차 대포. 정차 시작에 ScrapFarming.BeginStop, 끝(출발 = 승리, 내구도 0 = 패배)에 EndStop(버틴 초 보너스).
    /// 적을 죽여도 고철은 없다. 출발은 HUD 출발 버튼이 RaidSystem.Leave()로 한다.
    /// </summary>
    [DefaultExecutionOrder(140)]
    public sealed class StopRaidTopView : MonoBehaviour
    {
        [SerializeField] private StopTopView _view;

        private RaidSystem _raid;
        private ScrapFarming _farming;
        private TravelCatalog _catalog;
        private Transform _root;
        private bool _running;
        private readonly Dictionary<int, Transform> _monsters = new Dictionary<int, Transform>();
        private readonly List<Transform> _nests = new List<Transform>();
        private readonly List<EquipmentSlot> _guns = new List<EquipmentSlot>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        private IEnumerator Start()
        {
            if (_view == null) _view = GetComponent<StopTopView>();
            if (!StopViewMode.IsTop || _view == null) yield break;
            while (!GameServices.IsReady) yield return null;
            _raid = RaidInstaller.System;
            _farming = ScrapInstaller.Farming;
            _catalog = _view.Catalog;
            if (_raid == null || _catalog == null || !StopViewMode.IsTop) yield break;
            _raid.StopMode = true;
            _raid.StopFront = () => (_view.FrontX, 0f);
            _raid.StopTail = () => (_view.FrontX - _view.TrainLength, 0f);
            _root = new GameObject("StopRaidLayer").transform;
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
            _raid.StopFront = null; _raid.StopTail = null; _raid.StopMode = false;
        }

        private int Order => _catalog.SortingBase + 30;

        private void OnStarted()
        {
            if (!_raid.ActiveStop) return;
            _running = true;
            Clear();
            TravelPrimitives prims = _view.Prims;
            for (int i = 0; i < _raid.Nests.Count; i++)
            {
                RaidUnit nest = _raid.Nests[i];
                var go = new GameObject("Entrance" + i);
                go.transform.SetParent(_root, false);
                go.transform.localPosition = new Vector3(nest.X, nest.Y, 0f);
                float s = _catalog.StopEntranceSize;
                prims.Circle(go.transform, "Mound", 0f, 0f, s * 1.35f, _catalog.NestMound, Order - 6);
                prims.Circle(go.transform, "Hole", 0f, 0f, s, _catalog.Entrance, Order - 5);
                prims.Circle(go.transform, "Sac", 0f, 0f, s * 0.45f, _catalog.Nest, Order - 4);
                var target = go.AddComponent<TravelRaidTarget>();
                target.IsNest = true; target.Id = i;
                go.AddComponent<CircleCollider2D>().radius = s * 0.6f;
                _nests.Add(go.transform);
                PaintNest(i);
            }
            _guns.Clear();
            ITrainLoadout loadout = GameServices.Get<ITrainLoadout>();
            if (loadout != null)
                foreach (string id in new[] { TrainLoadout.TURRET, TrainLoadout.HEAVY_TURRET })
                    foreach (EquipmentSlot slot in loadout.Slots)
                        if (slot.IsAnchor && slot.EquipmentId == id) _guns.Add(slot);
            _farming?.BeginStop(_view.FrontX, 0f);
            IronRing.Hud.Ui.ToastScreen.Show(StopTopView.Strings("stop_toast_arrive", "중간역 정차 — 고철 더미를 눌러 인부를 보내라. 언제든 출발할 수 있다"), 2.4f);
            Debug.Log($"STOP: defense start entrances={_raid.Nests.Count} guns={_guns.Count}");
        }

        private void OnEnded(bool victory)
        {
            if (!_running) return;
            _running = false;
            float held = _raid.Elapsed;
            int bonus = _farming != null ? _farming.EndStop(victory, held) : 0;
            foreach (Transform t in _monsters.Values) if (t != null) { Debris(t.position, _catalog.Debris); Destroy(t.gameObject); }
            _monsters.Clear();
            foreach (Transform t in _nests) if (t != null) Destroy(t.gameObject);
            _nests.Clear();
            _guns.Clear();
            string format = StopTopView.Strings(victory ? "stop_toast_depart" : "stop_toast_lost", victory ? "출발! {0}초 버팀 · 보너스 고철 +{1}" : "열차가 버티지 못했다 — {0}초");
            IronRing.Hud.Ui.ToastScreen.Show(string.Format(format, Mathf.FloorToInt(held), bonus), 2.4f);
            Debug.Log($"STOP: defense end victory={victory} held={held:F1}s bonus={bonus}");
        }

        private void Clear()
        {
            foreach (Transform t in _monsters.Values) if (t != null) Destroy(t.gameObject);
            _monsters.Clear();
            foreach (Transform t in _nests) if (t != null) Destroy(t.gameObject);
            _nests.Clear();
        }

        private void PaintNest(int index)
        {
            if (index < 0 || index >= _nests.Count || _nests[index] == null) return;
            bool alive = _raid.Nests[index].Alive;
            foreach (SpriteRenderer r in _nests[index].GetComponentsInChildren<SpriteRenderer>())
                if (r.name == "Sac") r.color = alive ? _catalog.Nest : _catalog.NestBroken;
            Collider2D c = _nests[index].GetComponent<Collider2D>();
            if (c != null) c.enabled = alive;
        }

        private void OnSpawned(int id)
        {
            if (!_raid.ActiveStop) return;
            RaidUnit unit = null;
            foreach (RaidUnit u in _raid.Monsters) if (u.Id == id) unit = u;
            if (unit == null) return;
            var go = new GameObject(unit.Kind + "#" + id);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(unit.X, unit.Y, 0f);
            TravelPrimitives prims = _view.Prims;
            if (unit.Kind == "spider")
            {
                float s = _catalog.SpiderSize;
                prims.Rect(go.transform, "LegsA", 0f, 0f, s * 1.6f, s * 0.12f, _catalog.Chimney, Order);
                prims.Rect(go.transform, "LegsB", 0f, 0f, s * 0.12f, s * 1.6f, _catalog.Chimney, Order);
                prims.Circle(go.transform, "Body", 0f, 0f, s, _catalog.Spider, Order + 1);
                prims.Circle(go.transform, "Eye", 0f, 0f, s * 0.3f, _catalog.AutomatonEye, Order + 2);
                go.AddComponent<CircleCollider2D>().radius = s * 0.8f;
            }
            else
            {
                float s = _catalog.AutomatonSize;
                prims.Rect(go.transform, "Body", 0f, 0f, s, s, _catalog.Automaton, Order + 1);
                prims.Rect(go.transform, "Eye", 0f, 0f, s * 0.3f, s * 0.3f, _catalog.AutomatonEye, Order + 2);
                go.AddComponent<BoxCollider2D>().size = new Vector2(s * 1.2f, s * 1.2f);
            }
            var target = go.AddComponent<TravelRaidTarget>();
            target.IsNest = false; target.Id = id;
            _monsters[id] = go.transform;
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
            if (!_running) return;
            PaintNest(index);
            if (index >= 0 && index < _nests.Count && _nests[index] != null) Debris(_nests[index].position, _catalog.NestBroken);
        }

        private void OnShot(bool player, float x, float y)
        {
            if (!_raid.ActiveStop) return;
            if (!player && _guns.Count > 0) return;
            Shell(_view.CarWorld(0), _view.ToWorld(x, y));
        }

        private void OnGunFired(int gun, float x, float y)
        {
            if (!_raid.ActiveStop || _guns.Count == 0) return;
            EquipmentSlot slot = _guns[Mathf.Clamp(gun, 0, _guns.Count - 1)];
            Shell(_view.SlotWorld(slot.Car, slot.Column, slot.Row, slot.Width), _view.ToWorld(x, y));
        }

        private void Shell(Vector3 from, Vector3 to)
        {
            SpriteRenderer shell = _view.Prims.Circle(_root, "Shell", 0f, 0f, _catalog.ShellSize * 1.4f, _catalog.Shell, Order + 10);
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
            SpriteRenderer flash = _view.Prims.Circle(_root, "Flash", local.x, local.y, size, _catalog.Flash, Order + 11);
            flash.gameObject.AddComponent<TravelTransient>().Drift(Vector3.zero, _catalog.FlashSeconds, 1.5f, 0f);
        }

        private void Debris(Vector3 world, Color color)
        {
            Vector3 local = _root.InverseTransformPoint(world);
            for (int i = 0; i < _catalog.DebrisCount + 2; i++)
            {
                float angle = (i * 72f + 15f) * Mathf.Deg2Rad;
                SpriteRenderer piece = _view.Prims.Rect(_root, "Debris", local.x, local.y, 0.16f, 0.12f, color, Order + 9);
                piece.gameObject.AddComponent<TravelTransient>().Drift(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 2.5f, _catalog.DebrisSeconds, 0f, 720f);
            }
            Flash(world, 0.6f);
        }

        private void Update()
        {
            if (_raid == null || !_raid.ActiveStop) return;
            _farming?.Tick(Time.deltaTime, _raid.Monsters);
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Camera camera = _view.Camera;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (camera == null || IsPointerOverUi(pointer)) return;
            Ray ray = camera.ScreenPointToRay(pointer);
            RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || !hit.collider.TryGetComponent(out ScrapPileTarget pile)) continue;
                if (_farming != null && !_farming.Dispatch(pile.Index))
                    IronRing.Hud.Ui.ToastScreen.Show(StopTopView.Strings("stop_no_worker", "쉬는 인부가 없거나 더미가 비었다"), 1.4f);
                return;
            }
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || !hit.collider.TryGetComponent(out TravelRaidTarget target)) continue;
                if (target.IsNest ? _raid.ShootNest(target.Id) : _raid.ShootMonster(target.Id)) return;
            }
            Vector2 local = _view.ToLocal(camera.ScreenToWorldPoint(pointer));
            int nearest = _raid.NearestMonster(local.x, local.y, _catalog.ClickRadius);
            if (nearest >= 0) _raid.ShootMonster(nearest);
        }

        private void LateUpdate()
        {
            if (_raid == null || !_raid.ActiveStop) return;
            foreach (RaidUnit unit in _raid.Monsters)
            {
                if (!_monsters.TryGetValue(unit.Id, out Transform t) || t == null) continue;
                Vector3 target = new Vector3(unit.X, unit.Y, 0f);
                Vector3 delta = target - t.localPosition;
                if (delta.sqrMagnitude > 0.000001f) t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                t.localPosition = target;
            }
        }

        private bool IsPointerOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
