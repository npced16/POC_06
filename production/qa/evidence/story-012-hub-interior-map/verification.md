# 스토리 012 검증 기록 — 거점 내부 탑뷰 맵과 조사

작성: gameplay-programmer, 2026-11-01. 편집기: 사용자가 연 Unity 6000.3.27f1(브리지 `tools/bridge`만 사용, Unity 실행·종료·batchmode 없음).

## 구현 범위

- **로직(순수 C#, `IronRing.Interior`)**: `HubInterior`가 `IHubInterior`를 구현한다. 거점마다 위 3×6 + 아래 3×6 = 36칸이 잔해로 시작하고, 정차(`OnDocked`)마다 조사 횟수가 데이터값 3으로 초기화되며 조사마다 1 줄고 0이면 `CanInvestigate`가 false·`Investigate`는 예외다. 결과는 주입된 `IRandomSource` 한 번의 `NextFloat`로 확률표(빈 터 55 / 아이템 20 / 자원 15 / 부지 10, 합으로 정규화)에서 고르고, 생성 시 부분 Fisher-Yates로 고른 3칸은 고정 부지라 항상 부지가 드러난다. 묻힌 자원은 `IResourceInventory.Add`(데이터 수량), 묻힌 아이템은 `IItemInventory.Add`(없으면 보고만)로 들어가고 칸은 빈 터가 된다. 조사 한 건의 결과·발견 id는 `IInvestigationReport.Investigated`로 알린다. `IInteriorFacilityCells.TryBuildFacility`(014 호출)로 부지 칸을 FacilityBuilt로 바꾼다(부지가 아니거나 id가 비면 false).
- **데이터**: `Assets/Scripts/Interior/Resources/Interior/HubInterior.json` — 격자, 횟수, 부지 최소, 시드, 확률표, 묻힌 아이템(`lantern`, `gear`, `seedling`), 묻힌 자원(석탄 2, 고철 2, 물 1, 곡물 1). 코드에 수치 없음.
- **화면 전환**: `InteriorViewMode`(`IViewMode`). `MainRoot`가 시작 정차 거점과 `ITrackRuntime.Arrived`(Hub)에서 `Open`, `Update`에서 `IsHeld`가 풀리면(출발) `Close`.
- **뷰(`IronRing.Presentation.Interior.HubInteriorView`)**: Main 씬 안에서 거점마다 (1000 + 200·n, 0) 오프셋에 바닥·가로 선로·열차 3량·칸 타일을 런타임 생성한다. 내부가 열리면 `IMapAnchors.MapCamera`를 그 중심으로 옮기고(크기 5.5) 닫히면 저장한 선로 화면 위치·크기로 되돌린다. 칸 클릭은 격자 계산으로 찾고(UI 위 클릭은 EventSystem 레이캐스트로 무시) `IInteriorCellClickSource.CellClicked(hubId, cellIndex)`로 알린다. `IInteriorAnchors`(칸·열차 앵커, 중심)와 `IInteriorNavigation`(공개 Open/Close, 정차·출발과 같은 `InteriorViewMode` 경로)도 구현한다. 출발 감지는 `IsHeld` true→false 엣지이므로 이동 중 `IInteriorNavigation.Open`으로 연 화면(014 데모)은 다음 출발까지 유지된다. 연출 값은 `Resources/InteriorViewSettings.asset`(ScriptableObject)에서 읽는다.
- **HUD(`IronRing.Hud.Interior.InteriorHud`)**: UI Toolkit + `KoreanUi`(Story 008 한글 폰트). 거점 이름, 「남은 조사 n」, 자원 4종·아이템 수량, 결과 문구. 잔해 칸 클릭 → 칸 위에 「조사」 메뉴. 다른 상태의 칸은 무시(013/014 담당). 횟수 0이면 「이번 정차의 조사 횟수를 모두 썼습니다」. `KoreanHud`는 `IViewMode.InteriorOpen`이면 거점 관리 패널을 숨기고 상단 자원 바·출발 버튼은 유지한다.
- **아트**: `SpriteGen.Generate`에 `interior_floor / interior_rubble / interior_empty / interior_plot` 32px 타일 추가, 브리지로 재생성(manifest 27개, contact_sheet 240×200).
- **등록(Bootstrap)**: `MainRoot.Start` — 핵심 7 시스템과 `IHubInterior`, `IInvestigationReport`, `IInteriorFacilityCells`, `IViewMode`, `IInteriorAnchors`, `IInteriorCellClickSource`, `IInteriorNavigation` 등록 → `GameServices.RaiseComposing()` 1회 → `GameSession.BindHabitat(GameServices.Get<IHabitatSystem>())`와 `HubInterior.AttachItemInventory(GameServices.Get<IItemInventory>())` → `MarkReady()`.
- **서식지 교체 순서(코디네이터 요청)**: 요청대로 NpcRecruiter를 Composing 이후에 만들면 014 설치자가 Composing에서 요구하는 RunState(`INpcRecruiter` 생성자 의존)가 없어진다. 그래서 NpcRecruiter에는 `GameSession.HabitatLink`(IHabitatSystem 전달 프록시, 기존 `FacilityLink`와 같은 방식)를 주입하고 Composing 직후 `BindHabitat`으로 이긴 구현(013의 태그 서식지)에 재바인딩한다. 013의 `_habitats` 리플렉션 교체는 더 이상 필요 없다.

## 검증 상태

| 항목 | 결과 |
|---|---|
| 브리지 `refresh` | `failed=false`, 오류 없음 (구현 직후, 에셋 생성 후, 계약 추가 후 — 마지막 2026-11-01 06:35 UTC) |
| `run-tests --filter IronRing.Interior.Tests` | passed=29, failed=0 → `editmode.xml` |
| `run-tests --filter IronRing` (전체) | passed=154, failed=0 → `editmode-all.xml` (013/014 테스트 포함) |
| `execute-method IronRing.Art.Editor.SpriteGen.Generate` | 성공, 타일 4종 32×32 생성 |
| `execute-method IronRing.Presentation.Interior.Editor.Story012Build.Generate` | 성공, `InteriorViewSettings.asset`에 스프라이트 6개·URP Sprite-Unlit 재질 참조 |
| 스크린샷 2장 | **미확보** — 코디네이터 캡처(아래 인자) |

테스트가 덮는 것: 36칸 잔해 시작과 색인 배치, 정차 초기화·감소·0 거부·재정차 복원, 같은 시드 → 같은 배치·결과·재고, 거점마다 부지 ≥ 3(빈 터만 나오는 굴림에서 정확히 3), 자원 획득 수량, 아이템 보고/인벤토리 추가/후속 연결, 확률 띠 선택, 전이표(Empty↔ItemPlaced, FacilityPlot→FacilityBuilt, 그 외 거부), 모르는 거점·칸 거부, 데이터 파싱·검증 8종, 제작 데이터가 브리프와 일치, `GameSession`이 시작·도착·다음 바퀴에 `OnDocked`하는지, `IViewMode` 전이.

## 스크린샷 캡처 인자 (코디네이터 실행)

씬 `Assets/Scenes/Main.unity`. 열차는 시작부터 hub-1(주조소)에 정차해 있으므로 추가 인자 없이도 내부 화면이 열린다. `--interior-demo`는 브리지 캡처 중(`IronRing.Bridge.Phase`가 enter-play/capture)에만 읽히며 시작 정차 거점에서 조사 3회를 수행한다.

```powershell
# 1) 정차 직후 내부 화면(잔해 36칸, 선로·열차, 우측 패널 "남은 조사 3")
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 90 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-012-hub-interior-map/01-interior-docked.png

# 2) 조사 3회 후(드러난 칸 3개 — 색인 2, 15, 28 — 와 마지막 결과 문구, "남은 조사 0")
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 90 --extra-args "--interior-demo" --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-012-hub-interior-map/02-interior-investigated.png
```

확인할 것: (1) 어두운 석판 바닥 위 가로 선로와 열차 3량이 가운데, 위·아래 3×6 잔해 타일, 상단 자원 바 유지, 거점 관리 패널 없음, 우측 「주조소 / 남은 조사 3 / 자원 … / 아이템 0」. (2) 세 칸이 빈 터 또는 부지 타일로 바뀌고 결과 문구(「빈 터였습니다」 / 「묻힌 자원을 찾았습니다 석탄」 / 「옛 시설 부지를 발견했습니다」 등)가 보이며 「남은 조사 0」.

## 프리팹·씬 구조 (2026-11-01 변경: 코드 배치 → 씬의 프리팹 인스턴스)

사용자 요구 "코드로 배치하지 말고 씬에 직접 배치", "프리팹 기반으로 편집 쉽게"에 따라 정적 배치를 모두 프리팹 인스턴스로 옮겼다. 런타임 코드는 씬 오브젝트를 찾아 바인딩하고(`HubInteriorView`), 동적 조각만 프리팹으로 인스턴스화한다(013/014).

**거점 내부 (`Assets/Prefabs/Interior/`)**
- `HubInterior.prefab`: 루트 `HubInteriorRoot`(hubId, 카탈로그, 카메라 크기 5.5, 열차 앵커) → `Floor`(FloorTile 108), `Track`(TrackTile 12, 가운데 가로 선로), `TrainAnchor`(TrainCar 3량, 1자), `UpperGrid`(InteriorCell 18, 3×6), `LowerGrid`(InteriorCell 18, 3×6). 모두 중첩 프리팹 인스턴스.
- `InteriorCell.prefab`: SpriteRenderer(타일) + BoxCollider2D 1×1(클릭) + `InteriorCellView`(hubId/index/row/column/isUpper) + 자식 `ItemSlot`, `FacilitySlot`.
- `FloorTile.prefab`, `TrackTile.prefab`, `TrainCar.prefab`(회전 -90°, 1.25배), `ItemPiece.prefab`(SpriteRenderer, 013이 ItemSlot 아래에 인스턴스화).
- `InteriorCatalog.asset`: 잔해/빈 터/부지/건설됨 타일(건설됨은 현재 부지 타일 재사용), 열차·선로·바닥 스프라이트, URP Sprite-Unlit 재질, ItemPiece 프리팹(시설·NPC 조각 칸은 014가 채움), 최초 배치 기본값(원점 1000,0 / 간격 200 / 카메라 5.5 / 타일 1 / 바닥 여백 3·1 / 열차 3량 1.3 간격).
- Main 씬: `HubInteriors` 아래 `HubInterior hub-1..hub-4` 인스턴스(위치 1000+200n, 0; 루트와 칸 36개의 hubId 덮어쓰기). 선택·이동·수정은 Hierarchy/Inspector에서 자유롭게.

**선로 화면 (`Assets/Prefabs/Map/`)**
- `HubBuilding.prefab`: 루트(MapNode.Hub 앵커) → `Sprite`(hub_n, 2배, BoxCollider2D, HubTarget) + `Name`(한글 TextMesh). 인스턴스마다 그림·hubId·라벨 문구·라벨 위치를 덮어쓴다.
- `WaypointMarker.prefab`: `Sprite`(waypoint) + `Name`(한글 TextMesh). `Train.prefab`: 열차 스프라이트 + `MapTrain` 표식. `TrackTile.prefab`: 선로 한 장(인스턴스마다 가로·세로·곡선 그림 덮어쓰기).
- `MapCatalog.asset`: Art/Generated 스프라이트 전체, 재질, NotoSansKR 폰트, 프리팹 4종 참조.
- Main 씬: `Top Down Map/Rails`(TrackTile 88), 노드 앵커 `hub-n`/`… PASS`(MapNode) 아래 건물·표식 인스턴스, `Train` 인스턴스. `MapView`는 `Configure`로 저장된 참조를 쓰고, 비면 씬에서 찾는다.

**무엇을 바꾸려면 어디를 편집하나**: 모든 거점 공통 배치 → `HubInterior.prefab`; 특정 거점만 → 씬 인스턴스 덮어쓰기; 칸 모양·충돌체·슬롯 → `InteriorCell.prefab`; 그림·조각 프리팹 → `InteriorCatalog.asset`; 선로 화면 건물/표식/열차/선로 → `Assets/Prefabs/Map/*.prefab`; 선로 화면 그림·폰트 → `MapCatalog.asset`; 최초 좌표·정렬 값 → `ring-track.json` `_map`(재생성 때만); 격자·조사 규칙 → `HubInterior.json`.

**재생성(브리지 execute-method, 모두 멱등)**
- `IronRing.Bootstrap.Editor.BuildTopDownMain.Rebuild`: Main 씬 전체를 JSON + 프리팹 인스턴스로 다시 만들고 내부 인스턴스까지 놓고 저장. (기존 씬 루트는 지우므로 수동 편집을 보존하려면 쓰지 않는다.)
- `IronRing.Presentation.Interior.Editor.Story012Build.Generate`: 내부 프리팹·카탈로그 보장 + Main에 빠진 거점 인스턴스만 추가(거점 id로 찾음) + 저장.
- `IronRing.Presentation.Map.Editor.MapPrefabs.Generate`: 선로 프리팹·카탈로그만 보장.
- 프리팹 파일이 이미 있으면 손대지 않는다. 처음부터 다시 만들려면 해당 `.prefab`을 지우고 실행한다.

**검증(2026-11-01 07:00 UTC)**: refresh `failed=false`; `Rebuild` 성공 → `Generate` 재실행 시 추가 0(중복 없음); `scene-status` = `Assets/Scenes/Main.unity`, `isDirty=false`; 씬 파일에 `HubInterior hub-1..4` 각 1개, `_hubId` 덮어쓰기 37×4(+HubTarget 3), 최상위 프리팹 인스턴스 101개; `HubInterior.prefab` 중첩 인스턴스 159개(칸 색인 0..35 고유); 테스트 `IronRing.Interior.Tests` 29/29, 전체 `IronRing` 154/154.

**남은 정리**: `Assets/Scripts/Presentation/Interior/InteriorViewSettings.cs`는 주석만 남은 스텁이다(에이전트 셸의 삭제 권한 거부). 삭제해도 된다. 구 `Resources/InteriorViewSettings.asset` 폴더는 `Story012Build`가 `AssetDatabase.DeleteAsset`으로 제거했다.

## 주의·미해결

- `--integration-demo`의 첫 캡처(`01-main-hud.png`)는 이제 hub-1 내부 화면을 찍는다(정차 중이면 내부가 열리는 설계의 결과). 선로 화면이 필요하면 `--map-depart`를 함께 주거나 캡처 시점을 바꾼다.
- 묻힌 아이템 id(`lantern`, `gear`, `seedling`)와 이름 키 `interior_item_*`는 013 카탈로그에 맞춰 조정할 수 있다. HUD는 `item_<id>`(013) → `interior_item_<id>` → id 순으로 이름을 찾으므로 키가 없어도 멈추지 않는다.
- `IItemInventory`는 013 설치자가 `Composing`에서 등록하므로 생성자 주입 대신 `AttachItemInventory`로 연결한다(생성자 인자도 유지).
- 열차 3량 표시는 연출 값(`InteriorViewSettings._trainCars`)이며 1로 두면 선로 화면과 같은 단일 스프라이트가 된다.
- 실제 마우스 조작(잔해 클릭 → 「조사」 → 결과) 기록은 없다. 스크린샷 2는 데모 인자로 상태를 재현한 것이다.
