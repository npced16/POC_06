# 스토리 037 검증 — 횡스크롤 여정·달리면서 치르는 전투(프리미티브, 1차)

## 상태
- 컴파일: 브리지 `refresh` → `failed=false`, `errors=""`.
- 생성기 `IronRing.Presentation.Travel.Editor.TravelSideBuild.Generate`: 1회차 자산·프리팹·씬 인스턴스 생성, 2회차 로그 `TravelSideBuild: unchanged`(멱등).
- `scene-status`: `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}`.
- 플레이 캡처는 아직 안 함(코디네이터 몫). 스크린샷 4장은 이 폴더에 들어와야 한다.

## 역할 분담 변경(사용자 02:45 지시 반영)
037 = **달리면서 치르는 전투**(RaidSystem `enroute` 모드). 038 = 정차 디펜스+파밍(탑뷰).
`MainSettings._travelView == "side"`면 TravelSideView가 `RaidSystem.Mode = "enroute"`로 바꾼다. `"map"`이면 예전 그대로(arrival 습격, RaidView, 선로 지도).

## 화면 배치 규칙
- 루트 `TravelSide`(프리팹 인스턴스, 월드 (0, −200)) 자식: Backdrop(하늘·노을·지면·레일, 고정), Far(0.2배), Mid(0.5배), Near(1.0배), Train, RaidLayer(실행 중).
- 카메라: 여정 화면이 켜지면 `IMapAnchors.MapCamera`를 루트 + (0, `CameraOffsetY`)로, 직교 크기 `CameraSize`(4.5)로. 켜지는 조건 = 내부 화면이 닫혀 있고 (달리는 중 또는 역이 아닌 노드에 정차). 꺼지면(내부 화면 없이) 시작 때 저장한 지도 카메라로 되돌림. 역 도착 → 내부 화면이 지금처럼 열림(HubInteriorView가 카메라를 옮김).
- 열차: 중심 x = 화면 왼쪽 1/3(`TrainScreenFraction`), 3량(0 = 기관차·굴뚝·배장기), 바퀴 원 + 회전 살, 달릴 때 차체 떨림·굴뚝 연기 원. 칸마다 3×2 장비 점, 장착 장비: 포탑(빨강 사각+돔+포신), 중포(2칸 가로), 방벽판(테두리 사각), 부스터(파랑+뒤 불꽃 원), 수리 장치(톱니 원). 승객 수만큼 객차 창에 점.
- 층 위치: `층.x = 열차오른끝 − 위치 × 구간길이(40) × 시차`. 위치 = 나가는 구간 진행도, 되돌아오는 구간은 `1 − 진행도` + 열차 좌우 반전, 종점 정차는 1.
- 지형지물(`travel-side.json`): 주조소 역 승강장(0, 열차 아래), 신호기(0.16), 폐역 — 잿더미(0.36), 녹슨 다리(0.55), 급수탑(0.72), 종점 전초(1.0, 폐허 블록 + 둥지 표시 3 + "종점" 표지). 라벨은 표지판 사각 + 월드 TMP(NotoSansKR).
- 토스트: `_toast: true` 지형지물이 열차 중심을 지나면 "{라벨}{을/를} 지나는 중"(`travel_passing`, 조사 자동). `_passToasts`로 끔.
- 횡스크롤 모드의 구간 시간: `travel-side.json _edgeSeconds` = 24초(`TrainController.EdgeSecondsOverride`). 선로 데이터 2초는 지도 모드에서만.

## 습격 좌표 사상(enroute)
- 습격 X = **가까운 층(Near) 로컬 x** = 출발 구간 진행도 × 구간 길이. 풍경과 함께 흐른다(열차 속도만큼 보정됨).
- 열차 머리 X = `위치 × 40`, 꼬리 X = 머리 − 열차 길이(6.6). `RaidSystem.TrainPosition`/`TrainTailPosition`을 TravelRaidSideView가 넣는다. `EdgeProgress` = `TrainController.TravelProgress`, `EnrouteLength` = 카탈로그 구간 길이.
- 습격 Y = 열(lane) → 월드 y = 지면 + `LaneBase`(0.35) + Y × `LaneScale`(0.3). 정렬은 Y가 작을수록 위.
- 둥지: 길가 X = `_roadsideNests[i] × 40`(0.45/0.62/0.8), Y = `_nestOffsets[i].y`. 열차에서 `_nestSpawnRange`(9) 안일 때만 몬스터를 낸다. 부수면 그 런에서 꺼짐(어두운 색, 콜라이더 끔).
- 가장자리 스폰: `_edgeSpawnInterval`(3초)마다 앞(머리 + 9)·뒤(꼬리 − 6) 번갈아, 열 ±1.2.
- 이동: 몬스터는 가까운 접촉선(머리/꼬리, `_tailContact`)으로 간다. 열차를 쫓는 쪽(목표가 앞)이면 자기 속도 + 열차 속도 × `_chaseSpeedFactor`(1.0) → 열차보다 자기 속도만큼 빠르게 따라붙음. 앞에서 오는 몬스터는 자기 속도만, 열차가 다가간다.
- 시작/끝: 출발 거점(hub-1)에서 나가는 구간 진행도 `_startProgress`(0.25)에 시작, `_endProgress`(0.97)에 끝(종점 도착 직전). 열차는 멈추지 않는다. 결과는 `WaypointResolver.CompleteEnrouteRaid(승패, 종점 노드)` → 기존 보상·벌칙·EncounterEnded. 종점 도착은 enroute 모드에서 `IsOccupied = false`라 지금처럼 이벤트(해방된 경유지 규칙)·승객 탑승.
- 연출: 포탑·중포 포탄(작은 원, 0.15초)은 `GunFired` 순번 → 포탑 먼저·중포 다음 칸 위치에서, 플레이어 사격은 기관차에서. 명중 흰 원 0.1초, 죽으면 조각 3개 0.3초. 클릭 = 몬스터·둥지 BoxCollider2D → `ShootMonster/ShootNest`, 비키면 `NearestMonster`(역사상 좌표).

## 바뀐 데이터 키
| 파일 | 키 | 값 | 뜻 |
|---|---|---|---|
| `Bootstrap/Data/MainSettings.json` | `_travelView` | `"side"` | `"map"` = 예전 선로 지도·arrival 습격 |
| `Combat/Raid/Resources/Raid.json` | `_mode` | `"arrival"` | 기본 방식(side 화면이 런타임에 enroute로 덮음) |
| 〃 | `_startProgress` / `_endProgress` | 0.25 / 0.97 | enroute 시작·끝 구간 진행도 |
| 〃 | `_roadsideNests` | [0.45, 0.62, 0.8] | 길가 둥지 진행도 |
| 〃 | `_enrouteLength` | 40 | 구간 길이 기본값(화면이 카탈로그 값으로 덮음) |
| 〃 | `_nestSpawnRange` | 9 | 둥지 스폰 거리 |
| 〃 | `_edgeSpawnDelay` / `_edgeSpawnInterval` | 1.5 / 3.0 | 가장자리 스폰 시계 |
| 〃 | `_edgeSpawnAhead` / `_edgeSpawnBehind` / `_edgeSpawnLane` | 9 / 6 / 1.2 | 가장자리 스폰 위치 |
| 〃 | `_chaseSpeedFactor` | 1.0 | 추격 시 열차 속도 배율 |
| 〃 | `_tailContact` | true | 꼬리도 접촉선 |
| `Track/Resources/Track/travel-side.json`(새) | `_edgeSeconds`, `_passToasts`, `_toastSeconds`, `_landmarks[]` | 24, true, 1.6, 6개 | 여정 시간·토스트·지형지물 |
| `Localization/Resources/ko.json` | `travel_passing`, `travel_raid_start` | "{0}{1} 지나는 중", "습격! 달리면서 막아라" | 추가만 |
| `Prefabs/Travel/TravelCatalog.asset`(새) | 색·크기·시차·카메라 | — | 화면 조정값 |

## 파일
- 새: `Assets/Scripts/Presentation/Travel/{TravelSideView,TravelRaidSideView,TravelPrimitives,TravelCatalog}.cs`, `Editor/TravelSideBuild.cs`, `Demo/TravelDemoDriver.cs`, `Assets/Scripts/Track/Resources/Track/travel-side.json`, `Assets/Prefabs/Travel/{PrimRect,PrimCircle,PrimLabel,TravelSide}.prefab`, `TravelCatalog.asset`, `Assets/Art/Generated/travel/{white,circle}.png`.
- 수정: `RaidSystem.cs`(enroute 모드), `RaidData.cs`, `Raid.json`, `WaypointResolver.cs`(`CompleteEnrouteRaid`), `TrainController.cs`(`EdgeSecondsOverride`), `RaidView.cs`(side면 쉼), `MainSettings.json`, `ko.json`, `Main.unity`(TravelSide 인스턴스).
- 부수 변경: `Assets/Fonts/NotoSansKR SDF.asset`(PrimLabel 생성 때 동적 아틀라스에 글자 추가) — 되돌려도 무방.

## 캡처 플래그(코디네이터)
| 장면 | 인자 | 권장 대기 |
|---|---|---|
| (1) 출발 직후 역 승강장 지나감 | `--travel-demo --travel-stage depart` | `TRAVEL: capture-ready stage=depart` 로그 후, 약 300프레임 |
| (2) 폐역 지나감 | `--travel-demo --travel-stage halt` | 약 600프레임 |
| 종점 도착 | `--travel-demo --travel-stage terminal` | 약 900프레임 |
| (3) 달리는 중 습격 5초(포탑 2 + 방벽판 1) | `--travel-demo --travel-stage raid` | 약 800프레임 |
| (4) 복귀(역 다가옴, 진행도 0.9) | `--travel-demo --travel-stage return` | 약 1500프레임 |
| 기존 출발 | `--map-depart` | 여정 화면이 보여야 함 |
`--travel-speed N`(기본 2)으로 멈추기 전까지 빨리 감는다. 모든 단계는 `Time.timeScale = 0`으로 멈춘 뒤 로그를 남긴다.

## 남은 문제
- 플레이 실행·스크린샷 미확인(레이아웃 수치는 카탈로그에서 조정).
- enroute 결과의 `EncounterEnded`는 EncounterStarted 없이 온다 — HUD(HubPanelScreen)·결과 토스트가 이 순서를 견디는지 플레이로 확인 필요.
- 종점 도착 이벤트는 HUD 클릭이 필요(데모 `return`은 자동으로 넘김).
- 습격 HUD 진척은 enroute에서 구간 진행도(0.25→0.97) 기준.
- `RaidSystem.Elapsed`는 게임 시간이라 `--travel-speed`로 빨리 감아도 5초 기준은 같다.


## 코디네이터 검증 (2026-11-02 03:15 KST)
- refresh failed=false, TravelSideBuild.Generate 2회(unchanged), scene-status isDirty=false.
- `037-depart.png`(--travel-demo --travel-stage depart 300프레임): 주조소 승강장·역명판을 떠나는 열차(3량, 바퀴살 회전, 연기), 시차 3층, "신호기" 라벨. `037-raid-v2.png`(stage raid 1400프레임): 달리는 중 습격 — 길가 둥지(원), 추격 거미(빨간 원), 내구도 14/14 패널, 토스트 "점령된 종점을 통과합니다", 열차 위 포탑 2·방벽판 1(로그 `RAID: kill spider#0~3 by turret`, `TRAVEL: capture-ready stage=raid position=0.46 monsters=1`). `037-terminal-v2.png`(stage terminal 2400프레임): 급수탑을 지나며 "급수탑을 지나는 중", 오른쪽 끝에 "종점" 표지 접근. `037-return-v2.png`(stage return 3600프레임): 복귀 구간 — 열차 좌우 반전(기관차가 왼쪽), 승객 3/3·창에 불, 복구 중·번영 5/40, 점등부 탑승 토스트.
- 메모: terminal/return 데모 단계는 권장 프레임(900/1500)보다 늦게(2400/3600) 잡아야 함. 장비 없는 단계(terminal/return)는 enroute 습격에서 패배(hull 0/10, t≈11s) — 포탑 2+방벽판 1이면 5초 시점 무피해. 둥지 원이 열차와 겹쳐 보일 때가 있음(길가 y 분리 후속). HUD 패널과 토스트가 살짝 겹침.
