# 방어시설 코드 보관 검증 (2026-11-02)

스토리 039/045로 전투·방어시설이 게임에서 빠졌지만 시설 코드가 계속 컴파일되던 문제를 정리했다.

## 1. 보관한 경로 (`git mv`, `.meta` 포함 → `archive/combat-2026-11-02/<같은 경로>`)

| 원래 경로 | 내용 |
|---|---|
| `Assets/Scripts/Defense/**` (+ `Defense.meta`) | FacilityBuilder, FacilityCatalog, FacilityDefinition, Data/Facilities.json, Demo/(DefenseDemoFakes, FacilityDemoView), Editor/FacilityDemoBuild |
| `Assets/Scripts/Hud/FacilityRow.cs` | 거점 패널 시설 줄 컴포넌트 |
| `Assets/Scripts/Presentation/HubPhase/InteriorFacilityView.cs` | 내부 화면 시설 조각 뷰 |
| `Assets/Scripts/Hub/IInteriorFacilityPlots.cs` | 부지 칸 시설 건설 래퍼(시설 전용) |
| `Assets/Prefabs/UI/Parts/FacilityRow.prefab` | 시설 줄 부품 프리팹 |
| `Assets/Prefabs/HubPhase/HubPhaseFacility_{wall,turret,workshop}.prefab` | 시설 조각 프리팹 |
| `Assets/Scenes/Demo/Story006/` (+ `.meta`) | FacilityDemo 데모 씬 |

## 2. 삭제한 계약·필드

- `Contracts/IronRingContracts.cs`: `IFacilityBuilder`, `IInteriorFacilityCells` 삭제, `InteriorCellState.FacilityBuilt` 삭제, `IInteriorCell.FacilityId` 삭제. (`InteriorCellState.FacilityPlot`·`InvestigationResult.FacilityPlot`은 조사 결과 확률표·데이터에 묶여 있어 유지.)
- `Bootstrap`: `GameSession.Facilities`·`FacilityLink`·`IsDocked`, `GameDefinition.Facilities`·`Parse(facilities)` 인자·시설 자원 검증, `MainSettings._demoFacilityId`, `MainRoot._facilityData`·`Configure(facilityData)` 인자·`IFacilityBuilder`/`IInteriorFacilityCells` 등록, `BuildTopDownMain`의 Facilities.json 로드.
- 번영 공식: `RunState`의 `IFacilityBuilder` 의존과 `PerFacility × 시설 수` 항, `HubDefinition.PerFacility`(생성자 인자 포함), `HubData._perFacility`/`_facilityScale`, `HubProfiles.json`의 두 키(JSON 유효).
- `HubDevelopmentInstaller`: 시설 서비스 대기 가드와 `IInteriorFacilityPlots` 등록 삭제.
- `HubInterior`/`InteriorCell`: `TryBuildFacility`, `FacilityId`, 전이표의 시설 전이 삭제.
- HUD: `HubPanelScreen` 시설 줄·건설 명령, `HubPanelScreenBuilder` 시설 목록 섹션·FacilityRow 부품, `HudCatalog._facilities`(+ `HudCatalog.json` 키), `TopBarScreen` 준비 가드의 `IFacilityBuilder` 조건.
- 배치: `PlacementKind.Facility`, `PlacementGhost` 시설 분기, `HotbarData._facilities`(+ `Hotbar.json` 키), `HotbarScreen`/`HotbarScreenBuilder` 시설 분기.
- 표현: `InteriorCatalog` 시설 아이콘 표·`FacilityBuilt` 타일, `Story012Build` 시설 아이콘 채우기, `HubPhasePrefabCatalog` 시설 항목(+ 에셋 YAML), `HubPhasePrefabBuild` 시설 조각 생성, `HubPhaseViewSettings` 시설 값(+ `hub_phase_view.json` 키), `EffectsSettings.FacilityOffset`, `RuinDressingView` 시설 칸 조건.
- 데모: `HubDemoPopulation`의 `IFacilityBuilder` 구현, `PhaseDemoDriver` 시설 건설 단계(+ `hub_phase_demo.json` `_facilityId`).
- `Assets/Scenes/Main.unity`: `MainRoot._facilityData`(Facilities.json GUID) 줄 삭제.
- `ko.json`의 `facility_*` 키는 남김(미사용 허용).

## 3. 보관 GUID 잔존 검사 (`Assets/Scenes/Main.unity`, `Assets/Prefabs/**`, `Assets/Scripts/**/*.asset`)

- 없음: Defense 스크립트·폴더 전부, Facilities.json, InteriorFacilityView, IInteriorFacilityPlots, FacilityDemo.unity, HubPhaseFacility_* (카탈로그 YAML 정리 후).
- 남음(재생성 대기): `FacilityRow.cs`(1f11feb5…)·`FacilityRow.prefab`(5769ae13…) → `Assets/Prefabs/UI/Screens/HubPanel.prefab`. HubPanel 재생성으로 사라질 예정.

## 4. 브리지 결과

- `refresh`(id aa4c5909…): 21:14:05Z 시작 후 응답 없음. Editor.log는 JSON 재임포트·`Main.unity` 재임포트 뒤 멈춤 — 외부에서 고친 `Main.unity` 때문에 "씬이 바깥에서 바뀜/다시 불러오기" 모달 창이 뜬 것으로 보인다. `compile-status`도 대기열에 걸림.
- HubPanel 표식 + `BuildMainCanvas.Generate` ×2, `scene-status`: **미실행**(에디터 응답 대기).
- 컴파일 오류(이전 refresh 기준, 편집 도중 상태): GameDefinition/GameSession/HubPanelScreen의 CS0246 — 모두 소스에서 제거됨. 다시 확인 필요.
