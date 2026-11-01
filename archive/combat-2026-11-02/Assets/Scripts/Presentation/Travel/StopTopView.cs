using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Track;
using IronRing.Train.Equipment;

using RaidInstaller = IronRing.Combat.Raid.RaidInstaller;

namespace IronRing.Presentation.Travel
{
    /// <summary>
    /// 스토리 038. 정차 화면을 MainSettings._stopView("top" | "none")로 고른다. StopTopView.Awake가 정한다.
    /// --raid-demo가 있으면 꺼서 033 지도·도착 습격 경로를 그대로 쓴다.
    /// </summary>
    public static class StopViewMode
    {
        /// <summary>탑뷰 정차 화면이 켜져 있으면 true(중간역 정차 = 정차 디펜스 + 파밍).</summary>
        public static bool IsTop { get; set; }
        /// <summary>정차 화면이 지금 카메라를 쥐고 있으면 true(TravelSideView는 이때 물러난다).</summary>
        public static bool Active { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsTop = false; Active = false; }

        /// <summary>MainSettings.json 텍스트에서 _stopView를 읽는다(없으면 "top").</summary>
        public static string Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return "top";
            var settings = JsonUtility.FromJson<Settings>(json);
            return string.IsNullOrEmpty(settings?._stopView) ? "top" : settings._stopView;
        }

        [Serializable]
        private sealed class Settings { public string _stopView; }
    }

    /// <summary>
    /// 스토리 038(design/game-brief.md MVP 1·4·5, production/epics/iron-ring-train/story-038). 씬 루트 StopTop(y = −400)에 붙는다.
    /// 열차가 중간역(Raid.json _waypointId)에 서 있는 동안 선로 카메라를 이 구역으로 옮겨 위에서 본 정차 장면을 프리미티브로 그린다:
    /// 가운데 가로 선로, 화면 왼쪽 1/3에 열차(칸·장비 칸 점·장착 장비), 아래 승강장과 역 건물·라벨 "중간역 — 잿더미 전초".
    /// 좌표: 정차 좌표 = 이 루트의 로컬 (x = 선로 방향, y = 옆). RaidSystem 정차 디펜스와 ScrapFarming이 같은 좌표를 쓴다.
    /// 출발하면 저장한 카메라로 되돌리고 StopViewMode.Active를 끈다(횡스크롤이면 TravelSideView가 다시 잡는다).
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class StopTopView : MonoBehaviour
    {
        [SerializeField] private TravelCatalog _catalog;
        [Tooltip("MainSettings.json(_stopView \"top\" | \"none\").")]
        [SerializeField] private TextAsset _settings;
        [SerializeField] private Transform _ground;
        [SerializeField] private Transform _train;

        private TravelPrimitives _prims;
        private TrainController _controller;
        private IMapAnchors _anchors;
        private Transform _details;
        private Vector3 _savedPosition;
        private float _savedSize;
        private bool _ready;
        private int _signature = -1;

        /// <summary>카탈로그.</summary>
        public TravelCatalog Catalog => _catalog;
        /// <summary>프리미티브 헬퍼.</summary>
        public TravelPrimitives Prims => _prims;
        /// <summary>정차 화면이 카메라를 쥐고 있는지.</summary>
        public bool Active => StopViewMode.Active;
        /// <summary>선로 카메라.</summary>
        public Camera Camera => _anchors?.MapCamera;
        /// <summary>열차 칸 수.</summary>
        public int Cars { get { ITrainLoadout l = GameServices.Get<ITrainLoadout>(); return l != null && l.Cars > 0 ? l.Cars : 3; } }
        /// <summary>열차 길이(정차 좌표).</summary>
        public float TrainLength => _catalog == null ? 6f : Cars * _catalog.CarWidth + (Cars - 1) * _catalog.CarGap;
        /// <summary>화면 반폭(정차 좌표).</summary>
        public float HalfWidth { get { Camera c = Camera; return (_catalog != null ? _catalog.StopCameraSize : 6f) * (c != null ? c.aspect : 16f / 9f); } }
        /// <summary>열차 중심 x(화면 왼쪽 1/3).</summary>
        public float TrainCenterX => -HalfWidth + 2f * HalfWidth * (_catalog != null ? _catalog.TrainScreenFraction : 1f / 3f);
        /// <summary>열차 머리 x(오른쪽 끝, 정차 좌표). 입구·더미·인부 집 오프셋의 원점.</summary>
        public float FrontX => TrainCenterX + TrainLength * 0.5f;

        /// <summary>정차 좌표 (x, y) → 월드.</summary>
        public Vector3 ToWorld(float x, float y) => transform.TransformPoint(new Vector3(x, y, 0f));
        /// <summary>월드 → 정차 좌표.</summary>
        public Vector2 ToLocal(Vector3 world) => transform.InverseTransformPoint(world);

        private void Awake()
        {
            StopViewMode.IsTop = StopViewMode.Parse(_settings != null ? _settings.text : null) == "top" && !RaidInstaller.HasArgument("--raid-demo");
            _prims = new TravelPrimitives(_catalog);
        }

        private IEnumerator Start()
        {
            if (!StopViewMode.IsTop) { foreach (Transform child in transform) child.gameObject.SetActive(false); yield break; }
            while (!GameServices.IsReady) yield return null;
            _anchors = GameServices.Get<IMapAnchors>();
            _controller = FindFirstObjectByType<TrainController>();
            if (_anchors == null || _controller == null || _catalog == null) { Debug.LogWarning("STOP: missing map anchors/train/catalog; stop view off."); StopViewMode.IsTop = false; yield break; }
            _ground = _ground != null ? _ground : Child("Ground");
            _train = _train != null ? _train : Child("Train");
            BuildGround();
            BuildTrain();
            _ready = true;
            Debug.Log($"STOP: top view ready front={FrontX:F2} length={TrainLength:F2} half={HalfWidth:F2}");
        }

        private Transform Child(string name)
        {
            Transform found = transform.Find(name);
            if (found != null) return found;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void BuildGround()
        {
            TravelPrimitives.Clear(_ground);
            int o = _catalog.SortingBase;
            float half = HalfWidth + 2f, size = _catalog.StopCameraSize + 2f;
            _prims.Rect(_ground, "Ground", 0f, 0f, half * 2f, size * 2f, _catalog.StopGround, o - 50);
            for (int i = 0; i < 14; i++)
            {
                float x = -half + 1f + (i * 53 % 97) / 97f * (half * 2f - 2f);
                float y = (i % 2 == 0 ? 1f : -1f) * (2.2f + (i * 31 % 7) * 0.5f);
                _prims.Circle(_ground, "Ash", x, y, 1.2f + (i % 3) * 0.6f, _catalog.StopAsh, o - 48);
            }
            _prims.Rect(_ground, "Gravel", 0f, 0f, half * 2f, _catalog.StopCarDepth + 0.5f, _catalog.StopGravel, o - 40);
            for (float x = -half; x <= half; x += _catalog.SleeperSpacing)
                _prims.Rect(_ground, "Sleeper", x, 0f, 0.18f, _catalog.StopCarDepth + 0.2f, _catalog.Sleeper, o - 39);
            foreach (float y in new[] { 0.3f, -0.3f }) _prims.Rect(_ground, "Rail", 0f, y, half * 2f, 0.07f, _catalog.Rail, o - 38);
            // 승강장·역 건물(열차 가운데 칸 아래쪽).
            float cx = TrainCenterX;
            float platformY = -(_catalog.StopCarDepth * 0.5f + 0.75f);
            _prims.Rect(_ground, "Platform", cx, platformY, TrainLength + 2.4f, 1.0f, _catalog.Platform, o - 36);
            _prims.Rect(_ground, "PlatformEdge", cx, platformY + 0.47f, TrainLength + 2.4f, 0.08f, _catalog.Brass, o - 35);
            float stationY = platformY - 1.5f;
            _prims.Rect(_ground, "Station", cx, stationY, 4.6f, 1.8f, _catalog.StopStation, o - 34);
            _prims.Rect(_ground, "StationRoof", cx, stationY, 4.0f, 1.3f, _catalog.CarRoof, o - 33);
            _prims.Rect(_ground, "Chimney", cx + 1.4f, stationY + 0.3f, 0.35f, 0.35f, _catalog.Chimney, o - 32);
            string label = Strings("stop_station_label", "중간역 — 잿더미 전초");
            _prims.Rect(_ground, "Board", cx, stationY - 1.25f, Mathf.Max(2f, label.Length * 0.34f + 0.6f), 0.6f, _catalog.SignBoard, o - 31);
            _prims.Label(_ground, "Label", cx, stationY - 1.25f, label, 3.2f, _catalog.SignText, o - 30);
        }

        private void BuildTrain()
        {
            TravelPrimitives.Clear(_train);
            _train.localPosition = Vector3.zero;
            int o = _catalog.SortingBase + 10;
            float d = _catalog.StopCarDepth;
            for (int car = 0; car < Cars; car++)
            {
                float cx = CarCenterX(car);
                bool loco = car == 0;
                _prims.Rect(_train, "Car" + car, cx, 0f, _catalog.CarWidth, d, loco ? _catalog.Locomotive : _catalog.CarBody, o);
                _prims.Rect(_train, "Roof" + car, cx, 0f, _catalog.CarWidth * 0.86f, d * 0.7f, _catalog.CarRoof, o + 1);
                if (car > 0) _prims.Rect(_train, "Coupler" + car, cx + _catalog.CarWidth * 0.5f + _catalog.CarGap * 0.5f, 0f, _catalog.CarGap + 0.1f, 0.14f, _catalog.Chimney, o - 1);
                if (loco)
                {
                    _prims.Circle(_train, "Chimney", cx + _catalog.CarWidth * 0.28f, 0f, 0.36f, _catalog.Chimney, o + 3);
                    _prims.Rect(_train, "Cowcatcher", cx + _catalog.CarWidth * 0.5f + 0.1f, 0f, 0.2f, d * 0.8f, _catalog.Brass, o + 1);
                }
            }
            _details = new GameObject("Details").transform;
            _details.SetParent(_train, false);
            _signature = -1;
            RefreshDetails();
        }

        /// <summary>칸 index(0 = 기관차, 맨 앞 = 오른쪽)의 정차 좌표 중심 x.</summary>
        public float CarCenterX(int car) => FrontX - _catalog.CarWidth * 0.5f - car * (_catalog.CarWidth + _catalog.CarGap);

        private Vector3 SlotLocal(int car, int column, int row, int width)
        {
            ITrainLoadout loadout = GameServices.Get<ITrainLoadout>();
            int columns = loadout != null && loadout.Columns > 0 ? loadout.Columns : 3;
            int rows = loadout != null && loadout.Rows > 0 ? loadout.Rows : 2;
            float cell = _catalog.CarWidth * 0.8f / columns;
            float x = CarCenterX(car) + _catalog.CarWidth * 0.4f - (column + Mathf.Max(1, width) * 0.5f) * cell;
            float span = _catalog.StopCarDepth * 0.5f;
            float y = rows > 1 ? span * 0.5f - row * (span / (rows - 1)) : 0f;
            return new Vector3(x, y, 0f);
        }

        /// <summary>장비 칸의 월드 위치(포탄 출발점).</summary>
        public Vector3 SlotWorld(int car, int column, int row, int width = 1) { Vector3 p = SlotLocal(car, column, row, width); return ToWorld(p.x, p.y); }

        /// <summary>칸 index의 월드 중심.</summary>
        public Vector3 CarWorld(int car) => ToWorld(CarCenterX(car), 0f);

        private void RefreshDetails()
        {
            ITrainLoadout loadout = GameServices.Get<ITrainLoadout>();
            int signature = 17;
            if (loadout != null) foreach (EquipmentSlot slot in loadout.Slots) signature = signature * 31 + (slot.EquipmentId == null ? 1 : slot.EquipmentId.GetHashCode() + (slot.IsAnchor ? 7 : 3));
            if (signature == _signature || _details == null) return;
            _signature = signature;
            TravelPrimitives.Clear(_details);
            if (loadout == null) return;
            int o = _catalog.SortingBase + 14;
            float s = _catalog.EquipSize * 1.3f;
            foreach (EquipmentSlot slot in loadout.Slots)
            {
                Vector3 p = SlotLocal(slot.Car, slot.Column, slot.Row, 1);
                _prims.Circle(_details, "Slot", p.x, p.y, _catalog.SlotDotSize * 1.3f, _catalog.SlotDot, o);
                if (!slot.IsAnchor || slot.EquipmentId == null) continue;
                Vector3 c = SlotLocal(slot.Car, slot.Column, slot.Row, slot.Width);
                switch (slot.EquipmentId)
                {
                    case TrainLoadout.TURRET:
                        _prims.Circle(_details, "Turret", c.x, c.y, s, _catalog.Turret, o + 1);
                        _prims.Rect(_details, "Barrel", c.x + s * 0.5f, c.y, s * 0.7f, s * 0.18f, _catalog.Chimney, o + 2);
                        break;
                    case TrainLoadout.HEAVY_TURRET:
                        _prims.Rect(_details, "Heavy", c.x, c.y, s * 2f, s * 0.9f, _catalog.HeavyTurret, o + 1);
                        _prims.Rect(_details, "HeavyBarrel", c.x + s * 1.3f, c.y, s * 0.9f, s * 0.24f, _catalog.Chimney, o + 2);
                        break;
                    case "armor_plate":
                        _prims.Rect(_details, "PlateEdge", c.x, c.y, s * 1.05f, s * 1.05f, _catalog.Chimney, o + 1);
                        _prims.Rect(_details, "Plate", c.x, c.y, s * 0.8f, s * 0.8f, _catalog.ArmorPlate, o + 2);
                        break;
                    case "steam_booster":
                        _prims.Rect(_details, "Booster", c.x, c.y, s, s * 0.6f, _catalog.Booster, o + 1);
                        _prims.Circle(_details, "Flame", c.x - s * 0.7f, c.y, s * 0.5f, _catalog.Flame, o + 1);
                        break;
                    case "repair_rig":
                        _prims.Circle(_details, "Gear", c.x, c.y, s * 0.9f, _catalog.Repair, o + 1);
                        _prims.Circle(_details, "GearHub", c.x, c.y, s * 0.35f, _catalog.Chimney, o + 2);
                        break;
                    default:
                        _prims.Rect(_details, "Equip", c.x, c.y, s, s, _catalog.Brass, o + 1);
                        break;
                }
            }
        }

        private void Update()
        {
            if (!_ready) return;
            RingTrack track = _controller.Track;
            var raid = RaidInstaller.System;
            bool atStop = !track.IsMoving && track.CurrentNode != null && raid != null && track.CurrentNode.Id == raid.Data.WaypointId;
            bool on = atStop || (raid != null && raid.ActiveStop);
            if (on == StopViewMode.Active) return;
            Camera camera = Camera;
            if (on)
            {
                if (camera != null) { _savedPosition = camera.transform.position; _savedSize = camera.orthographicSize; }
                BuildTrain();
            }
            else if (camera != null) { camera.transform.position = _savedPosition; camera.orthographicSize = _savedSize; }
            StopViewMode.Active = on;
            Debug.Log($"STOP: view {(on ? "on" : "off")}");
        }

        private void LateUpdate()
        {
            if (!_ready || !StopViewMode.Active) return;
            Camera camera = Camera;
            if (camera != null)
            {
                camera.transform.position = new Vector3(transform.position.x, transform.position.y, camera.transform.position.z);
                camera.orthographicSize = _catalog.StopCameraSize;
            }
            RefreshDetails();
        }

        private void OnDestroy() { StopViewMode.Active = false; }

        /// <summary>ko.json 문자열(없으면 fallback).</summary>
        public static string Strings(string key, string fallback)
        {
            string value = IronRing.Localization.Strings.Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
    }
}
