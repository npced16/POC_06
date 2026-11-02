# Story 012: 거점 내부 탑뷰 맵과 조사

> **상태 메모(2026-11-02 정리)**: 부분 유효 — 거점 내부 탑뷰 맵은 살아 있으나(칸 수는 020의 위·아래 4×12) 잔해 조사는 015에서 제거. 현재 규칙은 design/game-brief.md §Core loop 과 story-015·story-020 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 16:04 KST 코디네이터 브리지 검증 — EditMode 154/154, 스크린샷 2장 확보·육안 확인)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01 16:10 KST (코디네이터, 검증 완료 → Done)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3 — 거점 내부 탑뷰(위·아래 3×6칸)에서 잔해 조사 (아이템 배치는 013, 거점 단계는 014)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Core loop 2단계, 목표), scoped to this story:*

- [x] `IronRing.Contracts.IHubInterior`를 구현한다. 거점마다 칸 36개(위 3×6, 아래 3×6)가 있고 모두 잔해(Rubble)로 시작한다. 칸 배치와 조사 결과 확률(빈 터·묻힌 아이템·묻힌 자원·시설 부지)은 데이터 파일에서 읽으며, 거점마다 시설 부지가 최소 3칸 보장된다. — `IronRing.Interior.HubInterior`, 데이터 `Assets/Scripts/Interior/Resources/Interior/HubInterior.json`(3×6, 확률 55/20/15/10, 부지 최소 3). 테스트 `Construct_AllCellsStartAsRubbleWithUpperThenLowerLayout`, `EveryHub_RevealsAtLeastMinimumFacilityPlots`, `ProductionData_MatchesBriefAndIsConsistent`.
- [x] 정차(`OnDocked`)마다 조사 횟수가 데이터값(기본 3)으로 초기화되고, 조사할 때마다 1 줄며 0이면 조사할 수 없다. 다음 바퀴에 다시 정차하면 초기화된다. — 테스트 `OnDocked_ResetsInvestigationsToDataValueAndRaisesEvent`, `Investigate_DecrementsAndRefusesAtZero`, `Redock_RestoresInvestigationsForNextLap`, `GameSession_DocksInteriorAtStartAndOnEveryHubArrival`.
- [x] 조사는 결정적이다. 주입된 `IRandomSource`로 같은 시드에서 같은 결과가 나온다. 묻힌 자원은 `IResourceInventory`에, 묻힌 아이템은 `IItemInventory`(013이 구현, 없으면 무시하고 FoundItem만 보고)에 들어가고 칸은 빈 터가 된다. — 테스트 `SameSeed_ProducesIdenticalLayoutOutcomesAndInventory`, `FoundResource_AddsDataAmountToInventoryAndClearsCell`, `FoundItem_WithoutItemInventory_ReportsIdAndClearsCell`, `FoundItem_WithItemInventory_AddsOneItem`, `FoundItem_AttachedLater_UsesAttachedInventory`.
- [x] 열차가 거점에 정차하면 화면이 그 거점의 내부 탑뷰로 전환된다. 선로가 가운데를 가로지르고 열차 스프라이트가 1자로 서 있으며, 위·아래 구역의 칸이 잔해 타일로 보인다. 조사한 칸은 상태별 타일(빈 터·부지)로 바뀐다. 출발하면 선로 화면으로 돌아간다. — 구현 `IronRing.Presentation.Interior.HubInteriorView`(정차 시 `IViewMode.Open`, 출발 시 `MainRoot.Update`가 `Close`, 카메라 이동·복귀). 컴파일·전환 상태 테스트(`InteriorViewModeTests`) 통과. 화면 확인: `01-interior-docked.png`(선로가 가운데를 가로지르고 열차가 1자로 서 있으며 위·아래 3×6 잔해 칸)·`02-interior-investigated.png`(드러난 칸 3개) — 코디네이터 육안 확인 2026-11-01 16:04 KST.
- [x] 내부 화면에는 한국어 HUD가 있다. 남은 조사 횟수, 거점 이름, 자원·아이템 수량이 보이고, 잔해 칸을 클릭하면 "조사" 메뉴가 뜨며 결과가 한국어 문구로 표시된다. 기존 상단 자원 바는 유지하고 거점 관리 패널은 내부 화면에서 숨긴다. — 구현 `IronRing.Hud.Interior.InteriorHud` + `KoreanHud` 최소 수정(InteriorOpen이면 `#panel` 숨김). 화면 확인: `01-interior-docked.png`("남은 조사 3", 거점 이름, 자원·아이템 수량, 상단 자원 바 유지)·`02-interior-investigated.png`("묻힌 자원을 찾았습니다 석탄" / "빈 터였습니다", "남은 조사 0"). 클릭→"조사" 메뉴 경로는 스크립트 데모(`--interior-demo`, 같은 `IInteriorCellClickSource` 경유)로 실행됨 — 실제 마우스 조작 기록은 011과 함께 사용자 확인 항목으로 남김.
- [x] 내부 맵의 바닥·잔해·빈 터·부지 타일은 Story 008 생성기(`SpriteGen`)에 추가해 코드로 생성한다. — `interior_floor/rubble/empty/plot` 32×32, 브리지 `SpriteGen.Generate` 실행, manifest 27개, contact_sheet 240×200.
- [x] `IHubInterior`와 내부 뷰 전환 상태(`IronRing.Contracts.Presentation.IViewMode`)를 Bootstrap에서 등록한다. — `MainRoot.Start`: `IHubInterior`, `IInvestigationReport`, `IInteriorFacilityCells`, `IViewMode`, `IInteriorAnchors`, `IInteriorCellClickSource`, `IInteriorNavigation` 등록 → `GameServices.RaiseComposing()` 1회 → `GameSession.BindHabitat(GameServices.Get<IHabitatSystem>())`(NpcRecruiter가 Composing에서 이긴 서식지 구현을 쓰도록 `HabitatLink` 재바인딩) → `MarkReady()`.
- [x] EditMode 테스트가 조사 횟수 초기화·감소, 확률표 결정성, 부지 최소 보장, 묻힌 자원 획득을 검증한다. — `IronRing.Interior.Tests` 29/29 통과, 전체 `IronRing` 154/154 통과(브리지 run-tests, 013/014 테스트 포함).
- [x] 스크린샷: 정차 직후 내부 화면(잔해), 조사 3회 후 화면(드러난 칸과 문구). — 확보: `production/qa/evidence/story-012-hub-interior-map/01-interior-docked.png`, `02-interior-investigated.png` (2026-11-01 16:04 KST, 프리팹·씬 배치 전환 뒤 캡처, 코디네이터 육안 확인). 인자는 `verification.md`.

---

## Implementation Notes

- 거점 내부 핵심 로직은 순수 C#(`Assets/Scripts/Interior/`), 뷰는 `Assets/Scripts/Presentation/Interior/`. 내부 맵은 Main 씬 안에서 거점별로 떨어진 위치(1000+200n, 0)에 **프리팹 인스턴스로 놓고** 카메라만 옮긴다(씬 추가 로드 불필요). 사용자 요구(2026-11-01) "코드로 배치하지 말고 씬에 직접 배치, 프리팹 기반으로 편집 쉽게"에 따라 런타임 생성은 제거했고 코드는 씬 오브젝트를 찾아 바인딩하고 타일 스프라이트만 바꾼다.

**프리팹·씬 구조와 편집 방법 (Story 012·선로 화면)**

| 바꾸고 싶은 것 | 편집할 곳 |
|---|---|
| 모든 거점의 내부 배치(바닥 범위, 선로, 열차 량 수·간격, 칸 간격) | `Assets/Prefabs/Interior/HubInterior.prefab` (Floor / Track / TrainAnchor / UpperGrid / LowerGrid) |
| 특정 거점만 다르게(위치, 카메라 크기, 칸 하나 옮기기) | `Assets/Scenes/Main.unity` → `HubInteriors/HubInterior hub-n` 인스턴스의 덮어쓰기(Inspector) |
| 칸 하나의 모양(타일 렌더러, 클릭 충돌체, ItemSlot·FacilitySlot 위치) | `Assets/Prefabs/Interior/InteriorCell.prefab` |
| 칸 상태별 타일 그림, 열차·선로·바닥 그림, 아이템/시설/NPC 조각 프리팹 | `Assets/Prefabs/Interior/InteriorCatalog.asset` |
| 아이템 조각(013이 ItemSlot 아래에 인스턴스화) | `Assets/Prefabs/Interior/ItemPiece.prefab` |
| 선로 화면 거점 건물(그림·충돌체·한글 라벨), 경유지 표식, 열차, 선로 타일 | `Assets/Prefabs/Map/HubBuilding.prefab`, `WaypointMarker.prefab`, `Train.prefab`, `TrackTile.prefab` |
| 선로 화면 스프라이트·폰트·프리팹 참조 | `Assets/Prefabs/Map/MapCatalog.asset` |
| 노드 좌표·굽힘점·카메라·정렬 순서 등 최초 배치 값 | `Assets/Scripts/Track/Resources/Track/ring-track.json` `_map` (재생성 때만 반영) |
| 격자 크기·조사 횟수·확률(게임 규칙) | `Assets/Scripts/Interior/Resources/Interior/HubInterior.json` (프리팹 재생성 때 격자 반영) |

- 재생성 명령(브리지 execute-method): `IronRing.Bootstrap.Editor.BuildTopDownMain.Rebuild`(Main 전체를 프리팹 인스턴스로 다시 만들고 내부 인스턴스까지 배치) · `IronRing.Presentation.Interior.Editor.Story012Build.Generate`(내부 프리팹·카탈로그 보장 + Main에 빠진 거점 인스턴스만 추가, 중복 없음) · `IronRing.Presentation.Map.Editor.MapPrefabs.Generate`(선로 프리팹·카탈로그만). 프리팹 파일이 이미 있으면 건드리지 않으므로 프리팹을 처음부터 다시 만들려면 해당 .prefab을 지우고 실행한다.
- 런타임 바인딩: `HubInteriorView`가 `FindObjectsByType<HubInteriorRoot>`로 인스턴스를 찾아 거점 id로 묶고 자식 `InteriorCellView`(색인 0..35)로 앵커 표를 만든다. 칸 클릭은 `InteriorCell.prefab`의 `BoxCollider2D`에 Input System 레이캐스트. `MapView`는 직렬화 참조가 비면 `MapNode`·`MapTrain`을 씬에서 찾는다.
- `IViewMode { bool InteriorOpen; string InteriorHubId; event Action<bool> InteriorChanged; }`는 코디네이터가 `IronRingPresentationContracts.cs`에 추가한다. 011 HUD(`KoreanHud`)는 InteriorOpen이면 거점 패널을 숨긴다(이 스토리가 최소한으로 수정).
- 조사 결과 확률 기본값(데이터): 빈 터 55%, 묻힌 아이템 20%, 묻힌 자원 15%, 시설 부지 10%. 부지 최소 3칸은 생성 시 고정 배치로 보장.

---

## Out of Scope

- Story 013: 아이템 인벤토리·배치·태그 서식지
- Story 014: 거점 단계와 패배 규칙

---

## Files

**새 파일 — 로직(순수 C#, `IronRing.Interior`)**
- `Assets/Scripts/Interior/InteriorData.cs`: 격자·조사 횟수·부지 최소·시드·확률표·묻힌 아이템/자원의 JSON 파싱과 검증.
- `Assets/Scripts/Interior/InteriorCell.cs`: `IInteriorCell` 구현(상태 변경은 `HubInterior`만).
- `Assets/Scripts/Interior/HubInterior.cs`: `IHubInterior` + `IInvestigationReport`(조사 결과·발견 id 보고 이벤트) + `IInteriorFacilityCells`(014가 호출하는 `TryBuildFacility`). 생성자 주입(`IResourceInventory`, 선택 `IItemInventory`, `IRandomSource`), `AttachItemInventory`(013 설치자가 Composing에서 등록하므로 후속 연결), 고정 부지 부분 셔플, 013/014용 전이표 `TryPlaceItem/TryRemoveItem/TryBuildFacility`.
- `Assets/Scripts/Interior/InteriorViewMode.cs`: `IViewMode` 구현(`Open/Close`).
- `Assets/Scripts/Interior/Resources/Interior/HubInterior.json`: 3×6, 정차당 3회, 부지 최소 3, 확률 55/20/15/10, 묻힌 아이템 `lantern/gear/seedling`, 묻힌 자원 석탄2·고철2·물1·곡물1.

**새 파일 — 뷰(`IronRing.Presentation.Interior`, 프리팹·씬 배치)**
- `Assets/Scripts/Presentation/Interior/InteriorCatalog.cs`: 카탈로그 ScriptableObject(상태별 타일, 열차·선로·바닥 스프라이트, 재질, ItemPiece/Facility/Npc 조각 프리팹, 최초 배치 기본값·정렬).
- `Assets/Scripts/Presentation/Interior/InteriorCellView.cs`: 씬 칸 컴포넌트(hubId·index·row·column·isUpper, 타일 렌더러, ItemSlot·FacilitySlot).
- `Assets/Scripts/Presentation/Interior/HubInteriorRoot.cs`: 씬 거점 내부 루트(hubId, 카탈로그, 카메라 크기, 열차 앵커, `ApplyHubId`).
- `Assets/Scripts/Presentation/Interior/HubInteriorView.cs`: 런타임 바인더. 씬의 `HubInteriorRoot`/`InteriorCellView`를 찾아 앵커 표를 만들고 `CellChanged`에 타일만 교체, 카메라 이동·복귀, Collider2D 클릭, `IInteriorAnchors`·`IInteriorCellClickSource`·`IInteriorNavigation` 구현. 런타임 생성 없음.
- `Assets/Scripts/Presentation/Interior/Editor/Story012Build.cs`: 프리팹·카탈로그 보장(없을 때만 생성)과 Main 씬 배치(`PlaceInteriors`, 거점 id로 찾아 빠진 것만).
- `Assets/Prefabs/Interior/`: `HubInterior.prefab`(Floor 108·Track 12·TrainAnchor+TrainCar 3·UpperGrid 18·LowerGrid 18, 중첩 프리팹), `InteriorCell.prefab`, `FloorTile.prefab`, `TrackTile.prefab`, `TrainCar.prefab`, `ItemPiece.prefab`, `InteriorCatalog.asset`.
- `Assets/Scripts/Presentation/Interior/InteriorViewSettings.cs`: 사용 중지 스텁(주석만). 삭제 권장(에이전트 셸 삭제 권한 거부로 남김). 구 `Resources/InteriorViewSettings.asset`은 `Story012Build`가 제거함.

**새 파일 — 선로 화면 프리팹(`IronRing.Presentation.Map`)**
- `Assets/Scripts/Presentation/Map/MapCatalog.cs`: 스프라이트 전체·재질·한글 폰트·프리팹 참조 카탈로그.
- `Assets/Scripts/Presentation/Map/MapTrain.cs`: 열차 프리팹 표식(MapView 폴백 탐색용).
- `Assets/Scripts/Presentation/Map/Editor/MapPrefabs.cs`: `Assets/Prefabs/Map/` 프리팹·카탈로그 보장.
- `Assets/Prefabs/Map/`: `HubBuilding.prefab`(Sprite+BoxCollider2D+HubTarget, Name TextMesh), `WaypointMarker.prefab`, `Train.prefab`, `TrackTile.prefab`, `MapCatalog.asset`.

**새 파일 — HUD(`IronRing.Hud.Interior`)**
- `Assets/Scripts/Hud/Interior/InteriorHud.cs`: 거점 이름·남은 조사·자원/아이템 수량·결과 문구 패널, 잔해 칸 클릭 시 "조사" 메뉴(다른 상태 칸은 무시).
- `Assets/UI/Hud/Resources/Hud/InteriorHud.uxml`, `InteriorHudStyle.uss`.

**새 파일 — 테스트(`IronRing.Interior.Tests`)**
- `Assets/Tests/EditMode/Interior/Editor/HubInteriorTests.cs`, `InteriorDataTests.cs`, `InteriorViewModeTests.cs`.

**새 파일 — 아트**
- `Assets/Art/Generated/interior_floor.png`, `interior_rubble.png`, `interior_empty.png`, `interior_plot.png` (+ `contact_sheet.png`, `manifest.json` 갱신).

**수정 파일**
- `Assets/Scripts/Art/Editor/SpriteGen.cs`: `InteriorTile` 4종 추가, 콘택트 시트 줄 수를 개수에서 계산(구조 변경 없음).
- `Assets/Scripts/Localization/Resources/ko.json`: `interior_*` 10개, `interior_item_*` 3개 추가(기존 키 수정 없음).
- `Assets/Scripts/Bootstrap/GameDefinition.cs`: 선택 인자 `interior`로 `InteriorData` 파싱, 묻힌 자원 id 검증.
- `Assets/Scripts/Bootstrap/GameSession.cs`: `Interior` 생성(시드 `HubInterior.json._seed` ^ entropy), 시작 정차와 거점 도착 시 `OnDocked`. `HabitatLink`(전달 프록시)를 NpcRecruiter에 주입하고 `BindHabitat`으로 Composing 뒤 재바인딩 — 014 설치자가 Composing 시점에 RunState(`INpcRecruiter` 필요)를 요구해 NpcRecruiter를 Composing 이후에 만들 수 없으므로 기존 `FacilityLink`와 같은 방식으로 해결.
- `Assets/Scripts/Bootstrap/MainRoot.cs`: 내부 데이터 로드(Resources 폴백), 뷰 생성, 서비스 7종 등록, `RaiseComposing()` 1회, `BindHabitat`·`AttachItemInventory`, 정차 시 `Open`/출발 엣지(`IsHeld` true→false)에서 `Close`, `--interior-demo`, 통합 데모 시설 건설 가드(폐허·불가 시 건너뜀).
- `Assets/Scripts/Hud/KoreanHud.cs`: `IViewMode` 구독, InteriorOpen이면 거점 관리 패널 숨김.
- `Assets/Scripts/Bootstrap/Editor/BuildTopDownMain.cs`: 스프라이트 직접 생성 대신 `MapCatalog` 프리팹 인스턴스(`PrefabUtility.InstantiatePrefab`)를 놓고 종류·거점 id·라벨만 덮어쓴다. 끝에 `Story012Build.PlaceInteriors`로 내부 인스턴스를 같은 씬에 놓는다. 데이터 클래스는 `internal`(MapPrefabs 공유).
- `Assets/Scripts/Presentation/Map/MapView.cs`: 직렬화 참조가 비면 `MapNode`·`MapTrain`·`Camera.main`을 씬에서 찾는 폴백.
- `Assets/Scenes/Main.unity`: `BuildTopDownMain.Rebuild`로 프리팹 인스턴스 기반으로 재생성(최상위 프리팹 인스턴스 101개: 선로 88·거점 4·경유지 4·열차 1·내부 4). 내부 4개는 `HubInteriors` 루트 아래 `HubInterior hub-n` 인스턴스(`Assets/Prefabs/Interior/HubInterior.prefab`), 선로 화면 요소는 `Assets/Prefabs/Map/*.prefab` 인스턴스. 2026-11-01 16:04 KST 저장 확인(isDirty=false). 이후 편집은 씬/프리팹에서.
- `production/qa/evidence/story-012-hub-interior-map/`: `editmode.xml`, `editmode-all.xml`, `verification.md`, `01-interior-docked.png`, `02-interior-investigated.png`. 전환 뒤 전체 스위트 최종: `production/qa/evidence/integration/editmode-prefab.xml`.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- EditMode 테스트 통과 (브리지 run-tests)
- 스크린샷 2장: `production/qa/evidence/story-012-hub-interior-map/`

**Status**: [x] 확보 (2026-11-01 16:04 KST, 코디네이터 브리지 검증) — EditMode 통과, 스크린샷 2장 확보·육안 확인

**EditMode 테스트 (브리지 run-tests, 사용자가 연 편집기 6000.3.27f1)**
- `production/qa/evidence/integration/editmode-prefab.xml`: 프리팹·씬 배치 전환 뒤 전체 `IronRing` 스위트 — passed=154, failed=0, total=154 (2026-11-01 16:04 KST, 최종).
- `production/qa/evidence/story-012-hub-interior-map/editmode.xml`: 필터 `IronRing.Interior.Tests` — passed=29, failed=0, total=29.
- `production/qa/evidence/story-012-hub-interior-map/editmode-all.xml`: 필터 `IronRing` 전체 — passed=154, failed=0, total=154 (기존 테스트 전부 유지, 013/014 포함).
- 브리지 `refresh`: `failed=false`, 오류 없음. `Main.unity` 저장 상태 isDirty=false.

**스크린샷 (확보 — 코디네이터 캡처·육안 확인, 2026-11-01 16:04 KST)**
- `01-interior-docked.png`: 정차 직후 내부 화면. 선로가 가운데를 가로지르고 열차가 1자로 서 있으며, 위·아래 3×6 잔해 칸 36개, 내부 패널 "남은 조사 3".
- `02-interior-investigated.png`: `--interior-demo`로 조사 3회 후. 드러난 칸 3개, 결과 문구 "묻힌 자원을 찾았습니다 석탄" / "빈 터였습니다", "남은 조사 0".
- 캡처 인자: `verification.md` 참고.

**남은 항목(스토리 Done과 별개)**
- 내부 화면의 실제 마우스 조작 기록 없음(스크립트 데모만). 011의 실제 마우스 확인과 함께 사용자가 점검.
- `Assets/Scripts/Presentation/Interior/InteriorViewSettings.cs` 스텁(주석만)은 사용자가 삭제.

---

## Dependencies

- Depends on: `story-009-topdown-main-scene.md`, `story-011-korean-hud-mouse.md`
- Unlocks: `story-013-items-tag-habitat.md`, `story-014-hub-phases-defeat.md`
