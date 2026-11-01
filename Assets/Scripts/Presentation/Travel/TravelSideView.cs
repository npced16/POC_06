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
    /// 스토리 037(design/game-brief.md MVP 1·4, production/epics/iron-ring-train/story-037). 씬 루트 TravelSide(y = −200)에 붙는다.
    /// 출발하면 선로 지도 카메라(IMapAnchors.MapCamera)를 이 구역으로 옮겨 옆에서 본 열차를 화면 왼쪽 1/3에 고정하고,
    /// 시차 3층(먼 0.2·중간 0.5·가까운 1.0, 카탈로그)과 travel-side.json의 지형지물을 진행도 × 구간 길이만큼 왼쪽으로 흘린다.
    /// 되돌아오는 구간(종점 → 역)은 풍경을 반대로 흘리고 열차를 좌우 반전한다. 역에 정차해 내부 화면이 열리면 물러나고,
    /// 정차·내부가 닫히면 저장한 지도 카메라로 되돌린다. 모든 그림은 프리미티브(색 사각형·원·라벨)다.
    /// MainSettings _travelView가 "map"이면 아무것도 하지 않는다(예전 선로 지도 + RaidView).
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class TravelSideView : MonoBehaviour
    {
        [SerializeField] private TravelCatalog _catalog;
        [Tooltip("MainSettings.json(_travelView \"side\" | \"map\").")]
        [SerializeField] private TextAsset _settings;
        [SerializeField] private Transform _far;
        [SerializeField] private Transform _mid;
        [SerializeField] private Transform _near;
        [SerializeField] private Transform _backdrop;
        [SerializeField] private Transform _train;

        private TravelPrimitives _prims;
        private TravelSideData _data;
        private TrainController _controller;
        private IMapAnchors _anchors;
        private IViewMode _viewMode;
        private Transform _body;
        private readonly List<Transform> _wheels = new List<Transform>();
        private readonly List<Transform> _equipment = new List<Transform>();
        private Transform _chimney;
        private Vector3 _mapCameraPosition;
        private float _mapCameraSize;
        private bool _ready, _active, _interiorWasOpen;
        private float _smokeTimer;
        private int _loadoutSignature = -1;
        private float _trainLength;
        private readonly Dictionary<TravelLandmark, float> _lastSide = new Dictionary<TravelLandmark, float>();

        /// <summary>여정 화면이 지금 카메라를 쥐고 있는지.</summary>
        public bool Active => _active;
        /// <summary>지금 풍경 위치(출발 구간 진행도 기준 0..1; 되돌아오는 구간은 1 − 진행도).</summary>
        public float Position { get; private set; }
        /// <summary>열차가 오른쪽(종점 쪽)을 보고 있으면 true.</summary>
        public bool FacingRight { get; private set; } = true;
        /// <summary>카탈로그.</summary>
        public TravelCatalog Catalog => _catalog;
        /// <summary>프리미티브 헬퍼.</summary>
        public TravelPrimitives Prims => _prims;
        /// <summary>가까운 층(지형지물·선로, 시차 1.0). 습격 좌표 X는 이 층의 로컬 x다.</summary>
        public Transform Near => _near;
        /// <summary>구간 데이터.</summary>
        public TravelSideData Data => _data;
        /// <summary>열차 전체 길이(월드).</summary>
        public float TrainLength => _trainLength;
        /// <summary>지면(선로) 월드 높이.</summary>
        public float GroundWorldY => transform.position.y + (_catalog != null ? _catalog.GroundY : -2f);
        /// <summary>습격 좌표의 열차 머리 X(가까운 층 로컬, 출발 구간 진행도 × 구간 길이).</summary>
        public float RaidFrontX => Position * SegmentLength;
        /// <summary>구간 월드 길이.</summary>
        public float SegmentLength => _catalog != null ? _catalog.SegmentWorldLength : 40f;

        private void Awake()
        {
            TravelViewMode.IsSide = TravelViewMode.Parse(_settings != null ? _settings.text : null) == "side";
            _prims = new TravelPrimitives(_catalog);
            _data = TravelSideData.Load();
        }

        private IEnumerator Start()
        {
            if (!TravelViewMode.IsSide) { SetChildrenActive(false); yield break; }
            while (!GameServices.IsReady) yield return null;
            _anchors = GameServices.Get<IMapAnchors>();
            _viewMode = GameServices.Get<IViewMode>();
            _controller = FindFirstObjectByType<TrainController>();
            if (_anchors == null || _controller == null || _catalog == null) { Debug.LogWarning("TRAVEL: missing map anchors/train/catalog; side view off."); yield break; }
            Camera camera = _anchors.MapCamera;
            if (camera != null) { _mapCameraPosition = camera.transform.position; _mapCameraSize = camera.orthographicSize; }
            if (_data._edgeSeconds > 0f) _controller.EdgeSecondsOverride = _data._edgeSeconds;
            if (RaidInstaller.System != null) RaidInstaller.System.Mode = "enroute";
            EnsureLayers();
            BuildBackdrop();
            BuildLayers();
            BuildTrain();
            _ready = true;
            Debug.Log($"TRAVEL: side view ready landmarks={_data._landmarks.Length} edgeSeconds={_data._edgeSeconds} segment={SegmentLength}");
        }

        private void SetChildrenActive(bool on)
        {
            foreach (Transform child in transform) child.gameObject.SetActive(on);
        }

        private void EnsureLayers()
        {
            _backdrop = _backdrop != null ? _backdrop : Child("Backdrop");
            _far = _far != null ? _far : Child("Far");
            _mid = _mid != null ? _mid : Child("Mid");
            _near = _near != null ? _near : Child("Near");
            _train = _train != null ? _train : Child("Train");
        }

        private Transform Child(string name)
        {
            Transform found = transform.Find(name);
            if (found != null) return found;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        // ----- 화면 폭 -----

        private float HalfWidth()
        {
            Camera camera = _anchors?.MapCamera;
            float aspect = camera != null ? camera.aspect : 16f / 9f;
            return _catalog.CameraSize * aspect;
        }

        /// <summary>열차 중심 로컬 x(화면 왼쪽 1/3).</summary>
        private float TrainCenterX() => -HalfWidth() + 2f * HalfWidth() * _catalog.TrainScreenFraction;

        /// <summary>열차 머리 로컬 x(오른쪽을 볼 때; 반전되면 왼쪽 끝).</summary>
        private float FrontLocalX() => TrainCenterX() + (FacingRight ? 1f : -1f) * _trainLength * 0.5f;

        // ----- 만들기 -----

        private void BuildBackdrop()
        {
            TravelPrimitives.Clear(_backdrop);
            int o = _catalog.SortingBase;
            float width = HalfWidth() * 2f + 4f;
            float top = _catalog.CameraSize + _catalog.CameraOffsetY;
            _prims.Rect(_backdrop, "Sky", 0f, _catalog.CameraOffsetY, width, top * 2f + 2f, _catalog.Sky, o - 50);
            _prims.Rect(_backdrop, "Glow", 0f, _catalog.GroundY + 1.2f, width, 2.4f, _catalog.SkyGlow, o - 49);
            _prims.Rect(_backdrop, "Ground", 0f, _catalog.GroundY - 2.5f, width, 5f, _catalog.Ground, o - 10);
            _prims.Rect(_backdrop, "Rail", 0f, _catalog.GroundY + 0.02f, width, 0.06f, _catalog.Rail, o - 4);
        }

        private void BuildLayers()
        {
            foreach (Transform layer in new[] { _far, _mid, _near }) TravelPrimitives.Clear(layer);
            int o = _catalog.SortingBase;
            float g = _catalog.GroundY;
            float margin = _catalog.LayerMargin;
            // 먼 층: 산·굴뚝 실루엣.
            float farLength = SegmentLength * _catalog.ParallaxFar;
            int i = 0;
            for (float x = -margin; x <= farLength + margin; x += _catalog.FarSpacing, i++)
            {
                float h = 1.2f + (i * 37 % 10) * 0.18f;
                if (i % 3 == 2)
                {
                    _prims.Rect(_far, "Stack", x, g + 0.4f + h * 0.8f, 0.35f, h * 1.6f, _catalog.FarColor, o - 40);
                    _prims.Circle(_far, "StackSmoke", x + 0.3f, g + 0.6f + h * 1.6f, 0.7f, new Color(_catalog.FarColor.r, _catalog.FarColor.g, _catalog.FarColor.b, 0.5f), o - 41);
                }
                else _prims.Circle(_far, "Hill", x, g + 0.2f, h * 2.6f, _catalog.FarColor, o - 40);
            }
            // 중간 층: 건물·전신주.
            float midLength = SegmentLength * _catalog.ParallaxMid;
            i = 0;
            for (float x = -margin; x <= midLength + margin; x += _catalog.MidSpacing, i++)
            {
                if (i % 2 == 0)
                {
                    float h = 0.9f + (i * 53 % 7) * 0.2f;
                    _prims.Rect(_mid, "House", x, g + 0.3f + h * 0.5f, 1.3f, h, _catalog.MidColor, o - 30);
                    if (i % 4 == 0) _prims.Rect(_mid, "Lamp", x + 0.25f, g + 0.3f + h * 0.6f, 0.18f, 0.18f, _catalog.MidWindow, o - 29);
                }
                else
                {
                    _prims.Rect(_mid, "Pole", x, g + 1.2f, 0.08f, 2.2f, _catalog.MidColor, o - 30);
                    _prims.Rect(_mid, "PoleArm", x, g + 2.2f, 0.5f, 0.06f, _catalog.MidColor, o - 30);
                }
            }
            // 가까운 층: 침목·울타리·지형지물.
            for (float x = -margin; x <= SegmentLength + margin; x += _catalog.SleeperSpacing)
                _prims.Rect(_near, "Sleeper", x, g - 0.05f, 0.35f, 0.1f, _catalog.Sleeper, o - 5);
            for (float x = -margin; x <= SegmentLength + margin; x += _catalog.FenceSpacing)
                _prims.Rect(_near, "Fence", x, g - 0.55f, 0.08f, 0.4f, _catalog.Fence, o + 40);
            foreach (TravelLandmark landmark in _data._landmarks) BuildLandmark(landmark);
        }

        private void BuildLandmark(TravelLandmark lm)
        {
            var root = new GameObject("Landmark_" + lm._kind).transform;
            root.SetParent(_near, false);
            root.localPosition = new Vector3(lm._progress * SegmentLength + lm._offset, _catalog.GroundY, 0f);
            int o = _catalog.SortingBase;
            float labelY = 2.2f;
            switch (lm._kind)
            {
                case "station":
                    _prims.Rect(root, "Platform", 0f, -0.2f, 8f, 0.3f, _catalog.Platform, o + 1);
                    _prims.Rect(root, "Roof", 0f, 1.9f, 6f, 0.2f, _catalog.CarRoof, o - 8);
                    for (int k = -1; k <= 1; k++) _prims.Rect(root, "Pillar", k * 2.6f, 0.85f, 0.12f, 2f, _catalog.CarRoof, o - 8);
                    labelY = 2.5f; break;
                case "signal":
                    _prims.Rect(root, "Pole", 0f, 0.9f, 0.1f, 1.8f, _catalog.Signal, o - 3);
                    _prims.Circle(root, "Light", 0f, 1.8f, 0.28f, _catalog.SignalLight, o - 2);
                    labelY = 2.3f; break;
                case "halt":
                    _prims.Rect(root, "Platform", 0f, -0.2f, 5f, 0.3f, _catalog.PlatformRuined, o + 1);
                    _prims.Rect(root, "Shed", -1.2f, 0.6f, 1.6f, 1.2f, _catalog.Ruin, o - 8);
                    _prims.Rect(root, "Broken", 1.3f, 0.3f, 0.9f, 0.6f, _catalog.Ruin, o - 8);
                    labelY = 1.9f; break;
                case "bridge":
                    _prims.Rect(root, "Water", 0f, -1.2f, 6f, 1.6f, _catalog.Water, o - 6);
                    _prims.Rect(root, "Deck", 0f, -0.15f, 6.4f, 0.22f, _catalog.Bridge, o - 1);
                    for (int k = -2; k <= 2; k++) _prims.Rect(root, "Truss", k * 1.4f, 0.6f, 0.1f, 1.4f, _catalog.Bridge, o - 7);
                    _prims.Rect(root, "Top", 0f, 1.3f, 6f, 0.12f, _catalog.Bridge, o - 7);
                    labelY = 1.8f; break;
                case "watertower":
                    _prims.Rect(root, "LegL", -0.5f, 0.8f, 0.1f, 1.6f, _catalog.Tower, o - 8);
                    _prims.Rect(root, "LegR", 0.5f, 0.8f, 0.1f, 1.6f, _catalog.Tower, o - 8);
                    _prims.Rect(root, "Tank", 0f, 2.0f, 1.4f, 1.0f, _catalog.Tower, o - 8);
                    labelY = 2.9f; break;
                case "outpost":
                    _prims.Rect(root, "RuinA", -1.5f, 0.7f, 1.6f, 1.4f, _catalog.Ruin, o - 8);
                    _prims.Rect(root, "RuinB", 0.6f, 1.0f, 1.2f, 2.0f, _catalog.Ruin, o - 8);
                    _prims.Rect(root, "RuinC", 2.2f, 0.45f, 1.4f, 0.9f, _catalog.Ruin, o - 8);
                    for (int k = 0; k < 3; k++)
                    {
                        _prims.Rect(root, "NestMound" + k, -1.2f + k * 1.6f, 0.15f, 0.9f, 0.3f, _catalog.NestMound, o - 2);
                        _prims.Circle(root, "NestMark" + k, -1.2f + k * 1.6f, 0.45f, 0.6f, _catalog.Nest, o - 1);
                    }
                    _prims.Rect(root, "SignPost", -2.8f, 0.7f, 0.1f, 1.4f, _catalog.SignBoard, o - 3);
                    labelY = 2.6f; break;
            }
            if (!string.IsNullOrEmpty(lm._label))
            {
                float x = lm._kind == "outpost" ? -2.8f : 0f;
                _prims.Rect(root, "Board", x, labelY, Mathf.Max(1.2f, lm._label.Length * 0.32f + 0.4f), 0.5f, _catalog.SignBoard, o + 2);
                _prims.Label(root, "Label", x, labelY, lm._label, 3f, _catalog.SignText, o + 3);
            }
        }

        private void BuildTrain()
        {
            TravelPrimitives.Clear(_train);
            _wheels.Clear(); _equipment.Clear();
            int cars = CarCount();
            float w = _catalog.CarWidth, h = _catalog.CarHeight, gap = _catalog.CarGap;
            _trainLength = cars * w + (cars - 1) * gap;
            _body = new GameObject("Body").transform;
            _body.SetParent(_train, false);
            int o = _catalog.SortingBase + 10;
            float bottom = _catalog.WheelSize * 0.6f;
            for (int car = 0; car < cars; car++)
            {
                float cx = CarCenterX(car);
                bool loco = car == 0;
                _prims.Rect(_body, "Car" + car, cx, bottom + h * 0.5f, w, h, loco ? _catalog.Locomotive : _catalog.CarBody, o);
                _prims.Rect(_body, "Roof" + car, cx, bottom + h + 0.06f, w + 0.08f, 0.12f, _catalog.CarRoof, o + 1);
                _prims.Rect(_body, "Trim" + car, cx, bottom + 0.08f, w, 0.06f, _catalog.Brass, o + 1);
                for (int k = 0; k < 2; k++)
                {
                    SpriteRenderer wheel = _prims.Circle(_train, "Wheel" + car + "_" + k, cx + (k == 0 ? -0.55f : 0.55f) * w * 0.6f, bottom, _catalog.WheelSize, _catalog.Wheel, o + 2);
                    _prims.Rect(wheel.transform, "Spoke", 0f, 0f, 0.9f, 0.12f, _catalog.Brass, o + 3);
                    _wheels.Add(wheel.transform);
                }
                if (loco)
                {
                    _chimney = _prims.Rect(_body, "Chimney", cx + w * 0.3f, bottom + h + 0.3f, 0.22f, 0.5f, _catalog.Chimney, o + 1).transform;
                    _prims.Rect(_body, "Cowcatcher", cx + w * 0.5f + 0.1f, bottom + 0.1f, 0.2f, 0.25f, _catalog.Brass, o + 1);
                }
                // 승객 창 줄(점은 RefreshTrainDetails가 칠한다).
                if (!loco) _prims.Rect(_body, "Windows" + car, cx, bottom + h * 0.72f, w * 0.85f, 0.2f, _catalog.Window, o + 1);
            }
            _loadoutSignature = -1;
            RefreshTrainDetails();
        }

        private int CarCount()
        {
            ITrainLoadout loadout = GameServices.Get<ITrainLoadout>();
            return loadout != null && loadout.Cars > 0 ? loadout.Cars : 3;
        }

        /// <summary>칸 index(0 = 기관차, 맨 앞)의 열차 로컬 중심 x(오른쪽을 볼 때).</summary>
        private float CarCenterX(int car) => _trainLength * 0.5f - _catalog.CarWidth * 0.5f - car * (_catalog.CarWidth + _catalog.CarGap);

        private Vector3 SlotLocal(int car, int column, int row, int width)
        {
            ITrainLoadout loadout = GameServices.Get<ITrainLoadout>();
            int columns = loadout != null && loadout.Columns > 0 ? loadout.Columns : 3;
            int rows = loadout != null && loadout.Rows > 0 ? loadout.Rows : 2;
            float w = _catalog.CarWidth, h = _catalog.CarHeight, bottom = _catalog.WheelSize * 0.6f;
            float cell = w * 0.8f / columns;
            // 열 0이 칸의 앞쪽(오른쪽)이다.
            float x = CarCenterX(car) + w * 0.4f - (column + Mathf.Max(1, width) * 0.5f) * cell;
            float y = bottom + h * 0.28f + row * (h * 0.44f / Mathf.Max(1, rows - 1));
            return new Vector3(x, y, 0f);
        }

        /// <summary>장비 칸(car, column, row)의 월드 위치(습격 발사 위치). 열차 반전도 반영한다.</summary>
        public Vector3 SlotWorld(int car, int column, int row, int width = 1) => _body != null ? _body.TransformPoint(SlotLocal(car, column, row, width)) : transform.position;

        /// <summary>칸 index의 월드 중심.</summary>
        public Vector3 CarWorld(int car) => _body != null ? _body.TransformPoint(new Vector3(CarCenterX(car), _catalog.WheelSize * 0.6f + _catalog.CarHeight * 0.6f, 0f)) : transform.position;

        /// <summary>장비 점·장착 장비·승객 점을 지금 ITrainLoadout·IPassengers대로 다시 그린다(바뀌었을 때만).</summary>
        private void RefreshTrainDetails()
        {
            ITrainLoadout loadout = GameServices.Get<ITrainLoadout>();
            IPassengers passengers = GameServices.Get<IPassengers>();
            int signature = passengers?.PassengerIds.Count ?? 0;
            if (loadout != null) foreach (EquipmentSlot slot in loadout.Slots) signature = signature * 31 + (slot.EquipmentId == null ? 1 : slot.EquipmentId.GetHashCode() + (slot.IsAnchor ? 7 : 3));
            if (signature == _loadoutSignature) return;
            _loadoutSignature = signature;
            foreach (Transform t in _equipment) if (t != null) Destroy(t.gameObject);
            _equipment.Clear();
            var holder = new GameObject("Details").transform;
            holder.SetParent(_body, false);
            _equipment.Add(holder);
            int o = _catalog.SortingBase + 14;
            float s = _catalog.EquipSize;
            if (loadout != null)
            {
                foreach (EquipmentSlot slot in loadout.Slots)
                {
                    Vector3 p = SlotLocal(slot.Car, slot.Column, slot.Row, 1);
                    _prims.Circle(holder, "Slot", p.x, p.y, _catalog.SlotDotSize, _catalog.SlotDot, o);
                    if (!slot.IsAnchor || slot.EquipmentId == null) continue;
                    Vector3 c = SlotLocal(slot.Car, slot.Column, slot.Row, slot.Width);
                    switch (slot.EquipmentId)
                    {
                        case TrainLoadout.TURRET:
                            _prims.Rect(holder, "Turret", c.x, c.y, s, s * 0.7f, _catalog.Turret, o + 1);
                            _prims.Circle(holder, "TurretDome", c.x, c.y + s * 0.3f, s * 0.6f, _catalog.Turret, o + 1);
                            _prims.Rect(holder, "Barrel", c.x + s * 0.45f, c.y + s * 0.35f, s * 0.6f, s * 0.14f, _catalog.Chimney, o + 2);
                            break;
                        case TrainLoadout.HEAVY_TURRET:
                            _prims.Rect(holder, "Heavy", c.x, c.y, s * 2.2f, s * 0.75f, _catalog.HeavyTurret, o + 1);
                            _prims.Rect(holder, "HeavyBarrel", c.x + s * 1.3f, c.y + s * 0.15f, s * 0.9f, s * 0.2f, _catalog.Chimney, o + 2);
                            break;
                        case "armor_plate":
                            _prims.Rect(holder, "PlateEdge", c.x, c.y, s * 1.1f, s * 1.1f, _catalog.Chimney, o + 1);
                            _prims.Rect(holder, "Plate", c.x, c.y, s * 0.85f, s * 0.85f, _catalog.ArmorPlate, o + 2);
                            break;
                        case "steam_booster":
                            _prims.Rect(holder, "Booster", c.x, c.y, s, s * 0.6f, _catalog.Booster, o + 1);
                            _prims.Circle(holder, "Flame", c.x - s * 0.7f, c.y, s * 0.5f, _catalog.Flame, o + 1);
                            break;
                        case "repair_rig":
                            _prims.Circle(holder, "Gear", c.x, c.y, s * 0.9f, _catalog.Repair, o + 1);
                            _prims.Circle(holder, "GearHub", c.x, c.y, s * 0.35f, _catalog.Chimney, o + 2);
                            break;
                        default:
                            _prims.Rect(holder, "Equip", c.x, c.y, s, s, _catalog.Brass, o + 1);
                            break;
                    }
                }
            }
            int count = passengers?.PassengerIds.Count ?? 0;
            int cars = CarCount();
            float bottom = _catalog.WheelSize * 0.6f;
            for (int k = 0; k < count && cars > 1; k++)
            {
                int car = 1 + k / Mathf.Max(1, _catalog.PassengersPerCar) % (cars - 1);
                int index = k % Mathf.Max(1, _catalog.PassengersPerCar);
                float x = CarCenterX(car) + _catalog.CarWidth * 0.35f - index * (_catalog.CarWidth * 0.7f / Mathf.Max(1, _catalog.PassengersPerCar - 1));
                _prims.Circle(holder, "Passenger", x, bottom + _catalog.CarHeight * 0.72f, _catalog.PassengerDotSize, _catalog.PassengerDot, o + 3);
            }
        }

        // ----- 매 프레임 -----

        private void LateUpdate()
        {
            if (!_ready) return;
            RingTrack track = _controller.Track;
            bool interior = _viewMode != null && _viewMode.InteriorOpen;
            bool atStation = track.IsStopped;
            // 위치·방향: 역에서 나가는 구간 = 진행도, 종점에서 나가는 구간 = 1 − 진행도(반전). 종점 정차 = 1.
            float progress = _controller.TravelProgress;
            if (track.IsMoving) { bool outbound = track.CurrentNode.IsStation; Position = outbound ? progress : 1f - progress; FacingRight = outbound; }
            else if (!track.CurrentNode.IsStation) { Position = 1f; FacingRight = true; }
            else { Position = 0f; FacingRight = true; }

            bool active = !interior && (track.IsMoving || !atStation);
            Camera camera = _anchors.MapCamera;
            if (active && camera != null)
            {
                camera.transform.position = new Vector3(transform.position.x, transform.position.y + _catalog.CameraOffsetY, camera.transform.position.z);
                camera.orthographicSize = _catalog.CameraSize;
            }
            else if (_active && !interior && camera != null) RestoreMapCamera(camera);
            if (_interiorWasOpen && !interior && !active && camera != null) RestoreMapCamera(camera);
            if (active != _active) Debug.Log($"TRAVEL: view {(active ? "on" : "off")} position={Position:F2} facingRight={FacingRight}");
            _active = active;
            _interiorWasOpen = interior;
            if (!active) return;

            float half = HalfWidth();
            float trainCenter = TrainCenterX();
            _train.localPosition = new Vector3(trainCenter, _catalog.GroundY, 0f);
            _train.localScale = new Vector3(FacingRight ? 1f : -1f, 1f, 1f);
            float front = FrontLocalX();
            // 층 x = 열차 머리 − 위치 × 구간 길이 × 시차. 가까운 층 로컬 x = 습격 좌표 X.
            float anchor = FacingRight ? front : trainCenter + _trainLength * 0.5f;
            _near.localPosition = new Vector3(anchor - Position * SegmentLength * _catalog.ParallaxNear, 0f, 0f);
            _mid.localPosition = new Vector3(anchor - Position * SegmentLength * _catalog.ParallaxMid, 0f, 0f);
            _far.localPosition = new Vector3(anchor - Position * SegmentLength * _catalog.ParallaxFar, 0f, 0f);
            _backdrop.localPosition = Vector3.zero;

            bool moving = track.IsMoving;
            float bob = moving ? Mathf.Sin(Time.time * _catalog.WobbleHz * Mathf.PI * 2f) * _catalog.WobbleAmplitude : 0f;
            _body.localPosition = new Vector3(0f, bob, 0f);
            if (moving) foreach (Transform wheel in _wheels) wheel.Rotate(0f, 0f, -_catalog.WheelSpinDegrees * Time.deltaTime);
            if (moving) EmitSmoke();
            RefreshTrainDetails();
            PassingToasts(trainCenter, half);
        }

        private void RestoreMapCamera(Camera camera)
        {
            camera.transform.position = _mapCameraPosition;
            camera.orthographicSize = _mapCameraSize;
        }

        private void EmitSmoke()
        {
            if (_chimney == null) return;
            _smokeTimer -= Time.deltaTime;
            if (_smokeTimer > 0f) return;
            _smokeTimer = _catalog.SmokeInterval;
            Vector3 local = transform.InverseTransformPoint(_chimney.position + Vector3.up * 0.3f);
            SpriteRenderer puff = _prims.Circle(transform, "Smoke", local.x, local.y, 0.25f, _catalog.Smoke, _catalog.SortingBase + 30);
            puff.gameObject.AddComponent<TravelTransient>().Drift(new Vector3(FacingRight ? -1.6f : 1.6f, 0.7f, 0f), _catalog.SmokeSeconds, 0.6f, 0f);
        }

        private void PassingToasts(float trainCenter, float half)
        {
            if (!_data._passToasts) return;
            foreach (TravelLandmark lm in _data._landmarks)
            {
                if (!lm._toast || string.IsNullOrEmpty(lm._label)) continue;
                float x = _near.localPosition.x + lm._progress * SegmentLength + lm._offset - trainCenter;
                float side = Mathf.Sign(x);
                if (_lastSide.TryGetValue(lm, out float previous) && previous != side && Mathf.Abs(x) < half)
                {
                    string format = Strings("travel_passing", "{0}{1} 지나는 중");
                    IronRing.Hud.Ui.ToastScreen.Show(string.Format(format, lm._label, KoreanParticle.ObjectParticle(lm._label)), _data._toastSeconds);
                    Debug.Log($"TRAVEL: passing {lm._label}");
                }
                _lastSide[lm] = side;
            }
        }

        private static string Strings(string key, string fallback)
        {
            string value = IronRing.Localization.Strings.Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
    }
}
