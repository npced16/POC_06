using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Track;

using RaidCatalog = IronRing.Combat.Raid.RaidCatalog;
using RaidData = IronRing.Combat.Raid.RaidData;
using RaidInstaller = IronRing.Combat.Raid.RaidInstaller;
using RaidSystem = IronRing.Combat.Raid.RaidSystem;

namespace IronRing.Presentation.Raid
{
    /// <summary>
    /// 스토리 033 표현. 선로 지도 루트(MapView와 같은 오브젝트)에 붙는다. 종점에 점령 거점 배경·둥지·"둥지 N"/"해방" 라벨을 늘 보이고,
    /// 습격 중에는 몬스터 프리팹을 IRaid 상태대로 움직이고, 좌클릭을 열차 대포 사격으로 넘기고, 포탄·명중 번쩍임·사망 연기를 그리고,
    /// 카메라를 종점으로 확대(데이터 _raidCameraSize)했다가 끝나면 되돌린다. 열차는 TrainController.HeldProgressSource로 구간을 기어간다.
    /// 스프라이트가 비어 있으면 카탈로그 색의 사각형으로 대신 그린다(아트 없이도 동작).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class RaidView : MonoBehaviour
    {
        [SerializeField] private RaidCatalog _catalog;
        [Tooltip("종점 장식 루트(비면 종점 앵커 아래에 런타임으로 만든다).")]
        [SerializeField] private Transform _site;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private int _sortingOffset = 5;

        private static Sprite _white;
        private RaidSystem _raid;
        private RaidData _data;
        private IMapAnchors _anchors;
        private TrainController _train;
        private Transform _terminal;
        private int _baseOrder;
        private readonly List<SpriteRenderer> _nests = new List<SpriteRenderer>();
        private readonly Dictionary<int, GameObject> _monsters = new Dictionary<int, GameObject>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private Vector3 _savedCameraPosition;
        private float _savedCameraSize;
        private float _restoreLeft;

        private IEnumerator Start()
        {
            while (!GameServices.IsReady) yield return null;
            _raid = RaidInstaller.System;
            _anchors = GameServices.Get<IMapAnchors>();
            if (_raid == null || _anchors == null) yield break;
            _data = _raid.Data;
            _terminal = _anchors.NodeAnchor(_data.WaypointId);
            if (_terminal == null) yield break;
            _raid.SetOrigin(_terminal.position.x, _terminal.position.y);
            _raid.TrainPosition = () => _anchors.TrainAnchor != null ? ((float, float))(_anchors.TrainAnchor.position.x, _anchors.TrainAnchor.position.y) : (_terminal.position.x, _terminal.position.y);
            _train = FindFirstObjectByType<TrainController>();
            if (_train != null) _train.HeldProgressSource = () => _raid.IsActive ? _raid.Progress * _data.CrawlEdgeFraction : 0f;
            BuildSite();
            _raid.RaidStarted += OnStarted;
            _raid.RaidEnded += OnEnded;
            _raid.MonsterSpawned += OnSpawned;
            _raid.MonsterKilled += OnKilled;
            _raid.MonsterReached += OnReached;
            _raid.NestDestroyed += OnNestDestroyed;
            _raid.ShotFired += OnShot;
            RefreshLabel();
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
            if (_train != null) _train.HeldProgressSource = null;
        }

        /// <summary>종점 배경·둥지·라벨을 만든다(씬에 _site가 있으면 그 아래에).</summary>
        private void BuildSite()
        {
            _baseOrder = 0;
            foreach (SpriteRenderer renderer in _terminal.GetComponentsInChildren<SpriteRenderer>(true))
                _baseOrder = Mathf.Max(_baseOrder, renderer.sortingOrder);
            if (_site == null)
            {
                _site = new GameObject("RaidSite").transform;
                _site.SetParent(_terminal, false);
            }
            SpriteRenderer backdrop = NewRenderer("Backdrop", _site, _catalog != null ? _catalog.TerminalOccupied : null,
                _catalog != null ? _catalog.BackdropColor : Color.gray, 3f, _baseOrder - 1);
            backdrop.transform.position = _terminal.position;
            for (int i = 0; i < _raid.Nests.Count; i++)
            {
                RaidUnit nest = _raid.Nests[i];
                GameObject go = _catalog != null && _catalog.NestPrefab != null ? Instantiate(_catalog.NestPrefab, _site) : NewPiece("Nest", 1.2f);
                go.name = "Nest" + i;
                go.transform.SetParent(_site, true);
                go.transform.position = new Vector3(nest.X, nest.Y, _terminal.position.z);
                go.transform.localScale *= _data.NestScale;
                RaidPiece piece = go.GetComponent<RaidPiece>() ?? go.AddComponent<RaidPiece>();
                piece.Configure(true, i);
                SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
                renderer.sortingOrder = _baseOrder + _sortingOffset;
                _nests.Add(renderer);
                PaintNest(i);
            }
            if (_label == null)
            {
                var labelGo = new GameObject("NestLabel");
                labelGo.transform.SetParent(_site, false);
                labelGo.transform.position = _terminal.position + new Vector3(0f, -1.4f, 0f);
                var text = labelGo.AddComponent<TextMeshPro>();
                TMP_Text sample = _terminal.GetComponentInChildren<TMP_Text>(true);
                if (sample != null) text.font = sample.font;
                text.fontSize = 3f; text.alignment = TextAlignmentOptions.Center;
                text.color = new Color32(255, 207, 106, 255);
                text.sortingOrder = _baseOrder + _sortingOffset + 3;
                _label = text;
            }
        }

        private void PaintNest(int index)
        {
            SpriteRenderer renderer = _nests[index];
            bool alive = _raid.Nests[index].Alive;
            Sprite sprite = _catalog == null ? null : alive ? _catalog.Nest : _catalog.NestBroken;
            renderer.sprite = sprite != null ? sprite : White();
            renderer.color = sprite != null ? Color.white : _catalog != null ? (alive ? _catalog.NestColor : _catalog.NestBrokenColor) : Color.gray;
            Collider2D collider = renderer.GetComponent<Collider2D>();
            if (collider != null) collider.enabled = alive;
        }

        private void RefreshLabel()
        {
            if (_label == null) return;
            _label.text = _raid.TerminalLiberated ? Strings("raid_liberated", "해방") : string.Format(Strings("raid_nests_label", "둥지 {0}"), _raid.NestsAlive);
        }

        private void Update()
        {
            if (_raid == null || !_raid.IsActive || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Camera camera = _anchors.MapCamera;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (camera == null || IsPointerOverUi(pointer)) return;
            Ray ray = camera.ScreenPointToRay(pointer);
            foreach (RaycastHit2D hit in Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity))
            {
                if (hit.collider == null || !hit.collider.TryGetComponent(out RaidPiece piece)) continue;
                if (piece.IsNest ? _raid.ShootNest(piece.Id) : _raid.ShootMonster(piece.Id)) return;
            }
            Vector3 world = camera.ScreenToWorldPoint(pointer);
            int nearest = _raid.NearestMonster(world.x, world.y, _data.ClickRadius);
            if (nearest >= 0) _raid.ShootMonster(nearest);
        }

        private void LateUpdate()
        {
            if (_raid == null) return;
            foreach (RaidUnit unit in _raid.Monsters)
            {
                if (!_monsters.TryGetValue(unit.Id, out GameObject go) || go == null) continue;
                Vector3 previous = go.transform.position;
                go.transform.position = new Vector3(unit.X, unit.Y, previous.z);
                SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
                if (renderer != null && Mathf.Abs(unit.X - previous.x) > 0.0001f) renderer.flipX = unit.X < previous.x;
            }
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            Camera camera = _anchors.MapCamera;
            if (camera == null) return;
            float t = 1f - Mathf.Exp(-_data.CameraLerpPerSecond * Time.unscaledDeltaTime);
            if (_raid.IsActive)
            {
                Vector3 target = new Vector3(_terminal.position.x, _terminal.position.y, camera.transform.position.z);
                camera.transform.position = Vector3.Lerp(camera.transform.position, target, t);
                camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, _data.RaidCameraSize, t);
            }
            else if (_restoreLeft > 0f)
            {
                _restoreLeft -= Time.unscaledDeltaTime;
                camera.transform.position = _restoreLeft <= 0f ? _savedCameraPosition : Vector3.Lerp(camera.transform.position, _savedCameraPosition, t);
                camera.orthographicSize = _restoreLeft <= 0f ? _savedCameraSize : Mathf.Lerp(camera.orthographicSize, _savedCameraSize, t);
            }
        }

        private void OnStarted()
        {
            Camera camera = _anchors.MapCamera;
            if (camera != null && _restoreLeft <= 0f) { _savedCameraPosition = camera.transform.position; _savedCameraSize = camera.orthographicSize; }
            _restoreLeft = 0f;
        }

        private void OnEnded(bool victory)
        {
            if (_train != null) _train.SetDepartureHeadStart(_raid.Progress * _data.CrawlEdgeFraction);
            foreach (GameObject go in _monsters.Values) if (go != null) { Smoke(go.transform.position); Destroy(go); }
            _monsters.Clear();
            _restoreLeft = 1.2f;
            RefreshLabel();
        }

        private void OnSpawned(int id)
        {
            RaidUnit unit = Find(id);
            if (unit == null) return;
            bool spider = unit.Kind == "spider";
            GameObject go = _catalog != null && _catalog.MonsterPrefab != null ? Instantiate(_catalog.MonsterPrefab, _site) : NewPiece("Monster", 0.6f);
            go.name = unit.Kind + "#" + id;
            go.transform.SetParent(_site, true);
            go.transform.position = new Vector3(unit.X, unit.Y, _terminal.position.z);
            go.transform.localScale *= _data.MonsterScale;
            RaidPiece piece = go.GetComponent<RaidPiece>() ?? go.AddComponent<RaidPiece>();
            piece.Configure(false, id);
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = _baseOrder + _sortingOffset + 1;
            Sprite[] frames = _catalog == null ? null : spider ? _catalog.SpiderFrames : _catalog.AutomatonFrames;
            bool hasArt = frames != null && frames.Length > 0 && frames[0] != null;
            renderer.sprite = hasArt ? frames[0] : White();
            renderer.color = hasArt ? Color.white : _catalog != null ? (spider ? _catalog.SpiderColor : _catalog.AutomatonColor) : Color.red;
            if (!hasArt && !spider) go.transform.localScale *= 1.4f;
            var loop = go.GetComponent<IronRing.Presentation.Effects.SpriteFrameLoop>();
            if (loop != null) { if (hasArt) loop.Configure(frames, _catalog.FramesPerSecond); else loop.enabled = false; }
            _monsters[id] = go;
        }

        private void OnKilled(int id)
        {
            if (!_monsters.TryGetValue(id, out GameObject go)) return;
            _monsters.Remove(id);
            if (go == null) return;
            Smoke(go.transform.position);
            Destroy(go);
        }

        private void OnReached(int id)
        {
            if (!_monsters.TryGetValue(id, out GameObject go)) return;
            _monsters.Remove(id);
            if (go == null) return;
            Flash(go.transform.position);
            Destroy(go);
        }

        private void OnNestDestroyed(int index)
        {
            if (index >= 0 && index < _nests.Count) { PaintNest(index); Smoke(_nests[index].transform.position); }
            RefreshLabel();
        }

        private void OnShot(bool player, float x, float y)
        {
            Vector3 from = _anchors.TrainAnchor != null ? _anchors.TrainAnchor.position : _terminal.position;
            StartCoroutine(FlyShell(from, new Vector3(x, y, from.z)));
        }

        private IEnumerator FlyShell(Vector3 from, Vector3 to)
        {
            GameObject shell = _catalog != null && _catalog.ShellPrefab != null ? Instantiate(_catalog.ShellPrefab, _site) : NewPiece("Shell", 0.2f, false);
            SpriteRenderer renderer = shell.GetComponent<SpriteRenderer>();
            bool art = _catalog != null && _catalog.Shell != null;
            renderer.sprite = art ? _catalog.Shell : White();
            renderer.color = art ? Color.white : _catalog != null ? _catalog.ShellColor : Color.yellow;
            renderer.sortingOrder = _baseOrder + _sortingOffset + 2;
            for (float t = 0f; t < _data.ShellSeconds; t += Time.deltaTime)
            {
                shell.transform.position = Vector3.Lerp(from, to, t / _data.ShellSeconds);
                yield return null;
            }
            Destroy(shell);
            Flash(to);
        }

        private void Flash(Vector3 position)
        {
            GameObject hit = _catalog != null && _catalog.HitPrefab != null ? Instantiate(_catalog.HitPrefab, _site) : NewPiece("Hit", 0.5f, false);
            hit.transform.position = position;
            SpriteRenderer renderer = hit.GetComponent<SpriteRenderer>();
            bool art = _catalog != null && _catalog.Hit != null;
            renderer.sprite = art ? _catalog.Hit : White();
            renderer.color = art ? Color.white : _catalog != null ? _catalog.HitColor : Color.white;
            renderer.sortingOrder = _baseOrder + _sortingOffset + 3;
            Destroy(hit, _data.HitSeconds);
        }

        private void Smoke(Vector3 position)
        {
            if (_catalog == null || _catalog.SmokePrefab == null) { Flash(position); return; }
            GameObject smoke = Instantiate(_catalog.SmokePrefab, position, Quaternion.identity, _site);
            smoke.transform.localScale *= _data.SmokeScale;
            foreach (SpriteRenderer renderer in smoke.GetComponentsInChildren<SpriteRenderer>()) renderer.sortingOrder = _baseOrder + _sortingOffset + 2;
            Destroy(smoke, _data.SmokeSeconds);
        }

        private RaidUnit Find(int id)
        {
            foreach (RaidUnit unit in _raid.Monsters) if (unit.Id == id) return unit;
            return null;
        }

        private bool IsPointerOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _uiHits);
            return _uiHits.Count > 0;
        }

        private GameObject NewPiece(string name, float size, bool clickable = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_site, false);
            go.AddComponent<SpriteRenderer>().sprite = White();
            go.transform.localScale = Vector3.one * size;
            if (clickable) go.AddComponent<BoxCollider2D>().size = Vector2.one;
            return go;
        }

        private static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite, Color fallback, float fallbackSize, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : White();
            renderer.color = sprite != null ? Color.white : fallback;
            if (sprite == null) go.transform.localScale = Vector3.one * fallbackSize;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>1 월드 단위 흰 사각형(스프라이트가 없을 때의 대체).</summary>
        private static Sprite White()
        {
            if (_white == null) _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _white;
        }

        private static string Strings(string key, string fallback)
        {
            string value = IronRing.Localization.Strings.Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
    }
}
