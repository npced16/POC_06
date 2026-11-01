# Story 039: 전투 전부 제거, 원형 선로와 중간 휴식지(이벤트), 첫 거점에 지명

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 1·4 재정의. 사용자 지시(2026-11-02 04:05): "둘 다 빼자, 전투 별로다. 전투 싹 다 빼고 종점 말고 중간중간 이벤트 생기도록. 지금 테스트맵도 원형으로 만들어서 중간 휴식지(이벤트)로 만들고, 맨 처음 거점을 지명으로 따줘." 즉 033(습격)·034(열차 장비)·037(횡스크롤)·038(정차 디펜스·고철)과 005의 주사위 전투까지 **전투와 그 부속(장비·고철·방어시설 UI)을 모두 제거**하고, 선로는 **원형**(거점 1 + 휴식지 3)으로 되돌려 휴식지마다 **이벤트**만 일어나게 하며, 거점 이름은 시설명("주조소")이 아니라 **지명**으로 바꾼다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 코드 제거는 `archive/combat-2026-11-02/`로 옮겨 Assets 밖에 보관(컴파일 제외). 씬은 `BuildTopDownMain.Rebuild` + 생성기로 재생성.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 전투 제거: `Assets/Scripts/Combat/Raid/**`, `Assets/Scripts/Presentation/Raid/**`, `Assets/Scripts/Presentation/Travel/**`, `Assets/Scripts/Train/**`(장비·고철), `Assets/Scripts/Hud/RaidScreen.cs`+빌더, `Assets/Scripts/Hud/CombatFxScreen.cs`+빌더(전투 연출), `Assets/Scripts/Art/Editor/SpriteGenRaid.cs`·`SpriteGenEquipment.cs`, `Assets/Scripts/Bootstrap/Editor/MapCameraSync.cs`, `Assets/Scripts/Presentation/Interior/TrainEquipment*.cs`·`TrainCarGridView.cs`·`IPlacementSurface.cs`(장비 전용이면), 프리팹 `Assets/Prefabs/Raid/**`·`Travel/**`·`UI/Screens/Raid.prefab`·`UI/Screens/CombatFx.prefab`·`Interior/TrainCarGrid.prefab`·`EquipmentPiece.prefab`, 아트 `Assets/Art/Generated/raid|equipment|travel/**`를 `archive/combat-2026-11-02/`로 옮긴다(.meta 포함). 계약의 `IRaid`·`IRaidLoadout`·`ITrainLoadout`·`IScrap`·`RaidUnit`·`ICombatRoundCommand` 등 전투 전용 계약과 `Story005Encounters.json`의 주사위 전투(`_eventRatio`→이벤트 100%, `_rounds`…)도 제거해 **경유지는 이벤트만** 일어난다(`WaypointResolver`는 이벤트 경로만 남김, `EncounterEnded`는 EventResolved만). `StateCombatEffects`·"다음 라운드" 버튼·상단 고철·핫바 "열차" 탭·결과 화면 장비/고철 줄·`HubPanel`의 전투 문구를 지운다. `Assets/Scripts/Defense/**`(방벽·포탑·공방 시설)와 `FacilityRow`도 함께 보관함으로(번영의 시설 항목은 0으로). 컴파일 통과.
- [ ] 원형 선로: `ring-track.json`을 거점 1 + 휴식지 3의 원형(네 점이 원 위, 완만한 곡선 `_hasBend`)으로 바꾼다. 거점 `hub-1`은 지명 **"황동골"**(HubProfiles `_name`, 라벨 키 `hub_1`), 휴식지는 `REST-1` "잿더미 쉼터", `REST-2` "녹슨 다리", `REST-3` "급수탑 쉼터"(`_isStation:false`, 라벨 키 `rest_1..3`, ko.json). 바퀴 = 거점에 돌아올 때. 카메라 크기는 원이 다 보이게. `ring-track-4hubs.json`은 보관 그대로, 2노드 버전은 `ring-track-2nodes.json`으로 보관.
- [ ] 휴식지 이벤트: 각 휴식지에 닿으면 이벤트가 **반드시** 하나 일어난다(이벤트 표 `Story005Encounters.json` `_events`에 휴식지 성격의 이벤트 4~6개 추가·정리: 떠돌이 상인이 설계도를 건넴(도감 해금), 승객이 올라탐(기존 탑승 확률 +), 폭풍으로 다음 거점 번영 −, 버려진 창고에서 타일 발견 → 번영 +, 휴식(아무 일 없음, 번영 +1), 선로 보수로 한 바퀴 지연 등). 휴식지마다 고정 가중치 표(데이터). 지도 위 휴식지 표지는 기존 `WaypointMarker`, 이벤트 문구는 기존 토스트·`HubPanel`. 승객 탑승 규칙(023)은 휴식지에서 그대로.
- [ ] 씬: `BuildTopDownMain.Rebuild` → `Story012Build.Generate` → `BuildMainCanvas.Generate`(SCREEN_IDS에서 Raid·CombatFx 제거) → `MapLabelsBuild.Generate` → `PropsBuild.Generate` → `RuinBuild.Generate`·`GenerateMap` 순으로 재생성, 두 번째 실행 isDirty=false. `MainSettings.json`의 `_travelView`·`_stopView` 제거.
- [ ] 데모: `--raid-demo`·`--equipment-demo`·`--travel-demo`·`--stop-demo` 제거, `--map-depart`는 원형 한 바퀴(휴식지 3곳 이벤트 로그 `EVENT: REST-1 …`)로 동작, 나머지 데모 유지.
- [ ] 문서: 브리프 — 선로 문장(원형 거점 1 + 휴식지 3), MVP 4 "경유지 이벤트"로 바꾸고 전투·방어시설·장비·고철 문장 삭제, 거점 지명. EPIC 표 033·034·037·038 행 "Removed(039)". 검증은 파이프라인(컴파일·생성기·씬 저장)만, `production/qa/evidence/story-039-no-combat-ring-rest-stops/verification.md`에 제거 목록·데이터 키·결과 기록. 캡처 없음.

---

## Implementation Notes

- 옮기기는 `git mv`로(이력 보존). Assets 밖으로 나가면 컴파일에서 빠진다. 옮긴 뒤 참조가 남는 곳은 컴파일 오류로 드러나므로 refresh를 반복하며 정리.
- `GameServices` 설치자 중 전투 쪽(RaidInstaller·TrainEquipmentInstaller·ScrapInstaller)이 사라져도 Composing 훅 순서에 영향 없게 확인. `Passengers`의 `IRaid.BoardChanceBonus` 한 줄 제거.
- 검증은 열린 편집기 브리지(`refresh`, `execute-method`, `scene-status`).

---

## Out of Scope

- 새 이벤트 연출 화면, 거점 추가(데이터로 가능), 픽셀 아트.

---

## Files

- 보관함으로 이동: 위 AC 목록. 수정: `Assets/Scripts/Contracts/IronRingContracts.cs`·`IronRingPresentationContracts.cs`, `Assets/Scripts/Combat/WaypointResolver.cs`·`EncounterData.cs`·`Resources/Story005Encounters.json`, `Assets/Scripts/Track/Resources/Track/ring-track.json`(+보관 `ring-track-2nodes.json`), `Assets/Scripts/Hub/Data/HubProfiles.json`, `Assets/Scripts/Track/TrainController.cs`(EdgeSecondsOverride·HeldProgressSource 제거), `Assets/Scripts/Presentation/Map/MapView.cs`, `Assets/Scripts/Presentation/Interior/PlacementGhost.cs`·`PlacementEntry.cs`·`Editor/Story012Build.cs`, `Assets/Scripts/Hud/TopBarScreen.cs`·`HubPanelScreen.cs`·`RunResultScreen.cs`·`Hotbar/**`·`Editor/BuildMainCanvas.cs`·`Editor/Screens/*`, `Assets/Scripts/Npc/Passengers.cs`, `Assets/Scripts/Bootstrap/Data/MainSettings.json`·`GameDefinition.cs`, `Assets/Scripts/Hub/*`(시설 번영 항목), `ko.json`, `Assets/Scenes/Main.unity`, `design/game-brief.md`, `production/epics/iron-ring-train/EPIC.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 생성기 멱등 + 씬 저장: `production/qa/evidence/story-039-no-combat-ring-rest-stops/verification.md`(캡처 없음 — 사용자 지시)

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-032-poc-one-station-one-terminal.md`
- Unlocks: None
