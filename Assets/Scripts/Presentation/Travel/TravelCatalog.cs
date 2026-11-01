using UnityEngine;

namespace IronRing.Presentation.Travel
{
    /// <summary>
    /// 스토리 037. 횡스크롤 여정 화면의 색·크기·시차 배율·구간 길이·카메라(디자이너 조정값). 생성기(TravelSideBuild)가
    /// Assets/Prefabs/Travel/TravelCatalog.asset을 만들고 프리미티브 프리팹·스프라이트를 채운다. 습격 규칙 수치는 Raid.json에 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/Travel Catalog", fileName = "TravelCatalog")]
    public sealed class TravelCatalog : ScriptableObject
    {
        [Header("Primitives")]
        /// <summary>색 사각형 프리팹(1×1 흰 스프라이트).</summary>
        public GameObject RectPrefab;
        /// <summary>색 원 프리팹(지름 1 흰 원 스프라이트).</summary>
        public GameObject CirclePrefab;
        /// <summary>월드 TMP 라벨 프리팹.</summary>
        public GameObject LabelPrefab;
        /// <summary>1×1 흰 스프라이트.</summary>
        public Sprite WhiteSprite;
        /// <summary>지름 1 흰 원 스프라이트.</summary>
        public Sprite CircleSprite;

        [Header("Camera / layout")]
        /// <summary>여정 화면 카메라 직교 크기.</summary>
        public float CameraSize = 4.5f;
        /// <summary>카메라 중심의 루트 기준 세로 보정.</summary>
        public float CameraOffsetY = 0.6f;
        /// <summary>열차 중심이 놓이는 화면 가로 비율(1/3).</summary>
        public float TrainScreenFraction = 1f / 3f;
        /// <summary>지면(선로) 높이(루트 기준).</summary>
        public float GroundY = -2.2f;
        /// <summary>구간 하나의 월드 길이(진행도 0→1). Raid enroute 좌표의 구간 길이로도 넣는다.</summary>
        public float SegmentWorldLength = 40f;
        /// <summary>먼 층 시차 배율.</summary>
        public float ParallaxFar = 0.2f;
        /// <summary>중간 층 시차 배율.</summary>
        public float ParallaxMid = 0.5f;
        /// <summary>가까운 층 시차 배율.</summary>
        public float ParallaxNear = 1f;
        /// <summary>먼 층 실루엣 간격.</summary>
        public float FarSpacing = 3.2f;
        /// <summary>중간 층 건물·전신주 간격.</summary>
        public float MidSpacing = 2.6f;
        /// <summary>침목 간격.</summary>
        public float SleeperSpacing = 0.7f;
        /// <summary>울타리 기둥 간격.</summary>
        public float FenceSpacing = 1.6f;
        /// <summary>층을 화면 밖까지 채우는 여유 길이.</summary>
        public float LayerMargin = 20f;

        [Header("Train")]
        /// <summary>한 칸 길이.</summary>
        public float CarWidth = 2.1f;
        /// <summary>한 칸 높이.</summary>
        public float CarHeight = 1.0f;
        /// <summary>칸 사이 틈.</summary>
        public float CarGap = 0.15f;
        /// <summary>바퀴 지름.</summary>
        public float WheelSize = 0.42f;
        /// <summary>달릴 때 차체 떨림 폭.</summary>
        public float WobbleAmplitude = 0.03f;
        /// <summary>달릴 때 차체 떨림 빈도(Hz).</summary>
        public float WobbleHz = 7f;
        /// <summary>바퀴 회전 속도(도/초, 달릴 때).</summary>
        public float WheelSpinDegrees = 540f;
        /// <summary>연기 원 간격(초, 달릴 때).</summary>
        public float SmokeInterval = 0.18f;
        /// <summary>연기 원 수명(초).</summary>
        public float SmokeSeconds = 1.1f;
        /// <summary>장비 칸 점 크기.</summary>
        public float SlotDotSize = 0.1f;
        /// <summary>장착 장비 조각 크기(칸 하나).</summary>
        public float EquipSize = 0.3f;
        /// <summary>승객 창 점 크기.</summary>
        public float PassengerDotSize = 0.12f;
        /// <summary>한 칸에 그리는 승객 점 최대 수.</summary>
        public int PassengersPerCar = 6;

        [Header("Raid")]
        /// <summary>습격 열 Y(데이터 단위) → 지면 위 높이 배율.</summary>
        public float LaneScale = 0.3f;
        /// <summary>습격 열 0의 지면 위 높이.</summary>
        public float LaneBase = 0.35f;
        /// <summary>둥지 지름.</summary>
        public float NestSize = 0.9f;
        /// <summary>거미 지름.</summary>
        public float SpiderSize = 0.42f;
        /// <summary>자동인형 크기.</summary>
        public float AutomatonSize = 0.6f;
        /// <summary>포탄 지름.</summary>
        public float ShellSize = 0.14f;
        /// <summary>포탄 비행 시간(초).</summary>
        public float ShellSeconds = 0.15f;
        /// <summary>명중 번쩍임 시간(초).</summary>
        public float FlashSeconds = 0.1f;
        /// <summary>죽을 때 조각 수.</summary>
        public int DebrisCount = 3;
        /// <summary>조각 수명(초).</summary>
        public float DebrisSeconds = 0.3f;
        /// <summary>클릭이 콜라이더를 비켰을 때 가장 가까운 몬스터를 찾는 반경(월드).</summary>
        public float ClickRadius = 0.6f;

        [Header("Sorting")]
        /// <summary>여정 화면 정렬 기준 순번.</summary>
        public int SortingBase = 200;

        [Header("Palette")]
        public Color Sky = new Color32(52, 40, 52, 255);
        public Color SkyGlow = new Color32(120, 70, 50, 255);
        public Color Ground = new Color32(48, 38, 34, 255);
        public Color FarColor = new Color32(78, 58, 66, 255);
        public Color MidColor = new Color32(92, 72, 64, 255);
        public Color MidWindow = new Color32(230, 170, 80, 255);
        public Color Rail = new Color32(150, 140, 130, 255);
        public Color Sleeper = new Color32(84, 60, 44, 255);
        public Color Fence = new Color32(110, 88, 70, 255);
        public Color Platform = new Color32(140, 120, 100, 255);
        public Color PlatformRuined = new Color32(90, 80, 74, 255);
        public Color SignBoard = new Color32(60, 46, 40, 255);
        public Color SignText = new Color32(255, 214, 140, 255);
        public Color Signal = new Color32(80, 80, 80, 255);
        public Color SignalLight = new Color32(220, 70, 50, 255);
        public Color Bridge = new Color32(110, 70, 50, 255);
        public Color Water = new Color32(50, 70, 90, 255);
        public Color Tower = new Color32(120, 96, 80, 255);
        public Color Ruin = new Color32(70, 58, 56, 255);
        public Color Locomotive = new Color32(70, 60, 58, 255);
        public Color CarBody = new Color32(120, 78, 50, 255);
        public Color CarRoof = new Color32(56, 44, 40, 255);
        public Color Brass = new Color32(200, 150, 70, 255);
        public Color Wheel = new Color32(30, 26, 26, 255);
        public Color Chimney = new Color32(40, 36, 36, 255);
        public Color Smoke = new Color32(180, 175, 170, 200);
        public Color Window = new Color32(40, 34, 40, 255);
        public Color PassengerDot = new Color32(255, 220, 160, 255);
        public Color SlotDot = new Color32(230, 210, 170, 140);
        public Color Turret = new Color32(200, 70, 60, 255);
        public Color HeavyTurret = new Color32(150, 40, 40, 255);
        public Color ArmorPlate = new Color32(160, 170, 180, 255);
        public Color Booster = new Color32(90, 110, 140, 255);
        public Color Flame = new Color32(255, 150, 40, 255);
        public Color Repair = new Color32(110, 170, 90, 255);
        public Color Nest = new Color32(120, 40, 70, 255);
        public Color NestMound = new Color32(70, 40, 40, 255);
        public Color NestBroken = new Color32(60, 54, 50, 255);
        public Color Spider = new Color32(150, 30, 40, 255);
        public Color Automaton = new Color32(150, 140, 120, 255);
        public Color AutomatonEye = new Color32(255, 80, 60, 255);
        public Color Shell = new Color32(255, 230, 140, 255);
        public Color Flash = new Color32(255, 255, 255, 255);
        public Color Debris = new Color32(90, 30, 30, 255);

        [Header("Stop top view (story 038)")]
        /// <summary>정차 탑뷰 카메라 직교 크기.</summary>
        public float StopCameraSize = 6f;
        /// <summary>탑뷰 열차 칸 폭(옆 방향 두께).</summary>
        public float StopCarDepth = 1.1f;
        /// <summary>정차 입구(굴) 지름.</summary>
        public float StopEntranceSize = 1.4f;
        /// <summary>고철 조각 한 개 크기.</summary>
        public float ScrapPieceSize = 0.32f;
        /// <summary>인부 지름.</summary>
        public float WorkerSize = 0.34f;
        /// <summary>정차 바닥.</summary>
        public Color StopGround = new Color32(58, 50, 46, 255);
        /// <summary>선로 자갈.</summary>
        public Color StopGravel = new Color32(82, 74, 68, 255);
        /// <summary>재(흩어진 얼룩).</summary>
        public Color StopAsh = new Color32(70, 64, 62, 255);
        /// <summary>역 건물.</summary>
        public Color StopStation = new Color32(96, 72, 58, 255);
        /// <summary>고철.</summary>
        public Color Scrap = new Color32(150, 112, 82, 255);
        /// <summary>녹슨 고철.</summary>
        public Color ScrapRust = new Color32(170, 88, 52, 255);
        /// <summary>인부.</summary>
        public Color Worker = new Color32(240, 205, 130, 255);
        /// <summary>도망치는 인부.</summary>
        public Color WorkerFlee = new Color32(255, 120, 90, 255);
        /// <summary>정차 입구 구멍.</summary>
        public Color Entrance = new Color32(28, 20, 22, 255);
    }
}
