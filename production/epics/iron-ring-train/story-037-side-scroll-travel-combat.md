# Story 037: 1차 — 횡스크롤 여정·전투 화면(프리미티브): 출발하면 옆에서 본 열차가 달리고, 역·풍경이 지나가고, 습격을 그 화면에서 치른다

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-02 03:15 KST 코디네이터 — 컴파일 통과, TravelSideBuild 멱등, 캡처 4장; 달리면서 전투(enroute) 포함)
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 1(선로 이동)·MVP 4(경유지 전투). 사용자 지시(2026-11-02 02:35): "전투는 프리미티브로 만들어주고, 기차가 이동하는 과정에서 지나가는 역이랑 등등 보여주면서 해주라. 횡스크롤뷰로 1차, 2차로 탑뷰로 만들어줘." 지금은 출발하면 선로 지도(탑뷰 점 이동)만 보이고 습격은 지도 위 작은 열차로 치른다. 1차는 **옆에서 본 횡스크롤 여정 화면**: 출발 → 열차가 오른쪽으로 달리며 떠나는 역·중간 지형지물·다가오는 종점(점령된 전초)이 시차 스크롤로 지나가고 → 종점에서 습격(둥지에서 적이 지면을 달려와 열차 머리에 닿음, 포탑이 자기 칸에서 쏨, 클릭 사격) → 되돌아오는 길 → 역 도착. 모든 그림은 **프리미티브**(색 사각형·원·선, 라벨 텍스트)로 그려 모양·속도·간격을 빨리 맞춘다. 픽셀 스프라이트는 나중에 교체.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 2D URP 직교 카메라. 프리미티브 = 단색 SpriteRenderer(1×1 흰 스프라이트에 색) 또는 Quad. 씬 배치는 프리팹 인스턴스 + 생성기(멱등).

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 여정 화면 전환: "출발"을 누르면 선로 지도 대신 **횡스크롤 여정 화면**(`TravelSideView`, 씬의 별도 구역 y=−200 부근에 프리팹 인스턴스 루트 `TravelSide`)으로 카메라가 간다. 열차는 화면 왼쪽 1/3에 고정된 채 달리는 모습(바퀴 회전·연기 대신 작은 사각형 떨림/연기 원)이고, 풍경이 `TrainController.TravelProgress`에 비례해 왼쪽으로 흐른다(진행도 0→1 = 구간 길이 데이터 `_segmentWorldLength`). 역 도착(거점 정차)이면 지금처럼 내부 화면이 열리고, 여정 화면은 숨는다. 선로 지도(탑뷰)는 데이터 플래그 `_travelView: "side"|"map"`(`MainSettings.json`)로 되돌릴 수 있다.
- [ ] 지나가는 것들: 시차 3층(먼 산/굴뚝 실루엣 0.2배, 중간 건물·전신주 0.5배, 가까운 울타리·선로 침목 1배). 구간 데이터(`Assets/Scripts/Track/Resources/Track/travel-side.json`)에 지형지물 목록(진행도 위치, 종류, 라벨): 출발 역 승강장+역명판("주조소"), 신호기, 폐역 승강장("폐역 — 잿더미"), 다리, 급수탑, 종점 전초(폐허 블록 + 둥지 3개 + "종점" 표지). 역·표지는 지나갈 때 라벨 텍스트가 보이고, 열차가 역 구간을 지날 때 상단에 "…을 지나는 중" 작은 토스트(데이터로 끌 수 있음).
- [ ] 습격을 이 화면에서: 종점에 닿으면 열차가 멈추고(`RaidSystem` 그대로), 둥지 3개는 화면 오른쪽 지면 위 프리미티브(원+사각), 몬스터(`IRaid.Monsters`의 X·Y를 횡스크롤 좌표로 사상: X = 열차 머리로부터의 거리, Y = 지면 위 높이/앞뒤 열)가 지면을 달려와 열차 머리에 닿으면 내구도가 깎이고, 포탑·중포는 **자기 칸 위치**(`ITrainLoadout` 칸 → 열차 사각형 위 작은 사각형)에서 포탄(작은 원)이 날아가고 명중 번쩍임(흰 원 0.1초), 죽으면 조각 2~3개가 튀며 사라진다. 클릭 사격은 몬스터·둥지 프리미티브의 콜라이더로. 기존 습격 HUD(내구도·둥지/몬스터·안내)와 토스트·결과 흐름은 그대로.
- [ ] 열차 프리미티브: 3량(기관차 + 2량)을 색 사각형으로, 칸마다 3×2 장비 칸 점을 찍고 장착된 장비를 색이 다른 작은 사각형(포탑 원형 포신, 중포 2칸 가로, 방벽판 테두리, 부스터 뒤 불꽃, 수리 톱니)으로 그린다. 승객 수만큼 창에 점.
- [ ] 되돌아오기: 종점에서 방향을 바꾸면 풍경이 반대로 흐르고(열차는 그대로 왼쪽 1/3, 좌우 반전), 역 승강장이 다가오며 도착.
- [ ] 데모·캡처: `--map-depart`는 그대로 쓰되 여정 화면이 보이고, `--travel-demo --travel-stage depart|halt|terminal|raid|return`(각 진행도에서 `Time.timeScale=0`으로 멈추고 `TRAVEL: capture-ready` 로그). 스크린샷 (1) 출발 직후 역 승강장 지나감, (2) 폐역 지나감, (3) 종점 습격 중(둥지·몬스터·포탄), (4) 복귀 도착. `production/qa/evidence/story-037-side-scroll-travel-combat/` + verification.md. 브리프 MVP 1 줄에 "여정은 횡스크롤 화면(1차 프리미티브)" 한 구절.

---

## Implementation Notes

- 새 폴더 `Assets/Scripts/Presentation/Travel/`: `TravelSideView.cs`(루트·시차 층·지형지물·열차·습격 사상), `TravelPrimitives.cs`(색 사각형/원/라벨 생성 헬퍼, 프리팹 `Assets/Prefabs/Travel/Prim*.prefab`), `TravelRaidSideView.cs`(IRaid → 횡스크롤 사상, 클릭 사격), `Editor/TravelSideBuild.cs`(멱등 생성: 루트·층·프리팹·카탈로그 `TravelCatalog.asset`(색·크기·시차 배율·구간 길이)). 카메라 전환은 `HubInteriorView`/`RaidView`가 하는 방식(`IInteriorAnchors.MapCamera` 이동·크기 보간·복귀)을 따르고, 여정 화면이 켜진 동안 `MapView`의 습격 표현(`RaidView`)은 끈다(플래그).
- 라벨은 TMP(`MapLabel` 프리팹 재사용 또는 월드 TMP). 한국어.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 픽셀 스프라이트 교체, 사운드, 2차 탑뷰(038).

---

## Files

- `Assets/Scripts/Presentation/Travel/**`(새), `Assets/Prefabs/Travel/**`(새), `Assets/Scripts/Track/Resources/Track/travel-side.json`(새), `Assets/Scripts/Bootstrap/Data/MainSettings.json`(`_travelView`), `Assets/Scripts/Presentation/Map/MapView.cs`·`RaidView.cs`(여정 화면 켜지면 숨김), `Assets/Scripts/Track/TrainController.cs`(필요한 진행도/방향 노출만), `Assets/Scenes/Main.unity`(생성기), `ko.json`, `design/game-brief.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 4장: `production/qa/evidence/story-037-side-scroll-travel-combat/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-033-raid-occupied-outpost.md`, `story-034-train-equipment-grid.md`
- Unlocks: `story-038-top-down-travel-combat.md`
