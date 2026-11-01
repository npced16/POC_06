# 스토리 039 검증 — 전투 전부 제거, 원형 선로(황동골 + 휴식지 3), 휴식지 이벤트

- 날짜: 2026-11-02
- 방법: 열린 편집기 브리지(`refresh`, `execute-method`, `scene-status`)만 사용. 캡처 없음(사용자 지시). 플레이 모드 확인은 사용자가 편집기에서 직접 한다.

## 1. 보관함으로 옮긴 것 (`archive/combat-2026-11-02/`, `git mv`, .meta 포함)

| 원래 경로 | 내용 |
|---|---|
| `Assets/Scripts/Combat/Raid/**` | 033 실시간 습격 |
| `Assets/Scripts/Combat/CombatEncounter.cs` | 005 주사위 전투 |
| `Assets/Scripts/Combat/Demo/**`, `Assets/Scripts/Combat/Editor/Story005Build.cs` | 005 전투 데모·빌더 |
| `Assets/Scripts/Presentation/Raid/**` | 습격 연출 |
| `Assets/Scripts/Presentation/Travel/**` | 037 횡스크롤 여정 |
| `Assets/Scripts/Train/**` | 034 열차 장비, 038 고철·정차 디펜스 |
| `Assets/Scripts/Presentation/Effects/StateCombatEffects.cs`, `Effects/Demo/**`, `Effects/Editor/Story010Build.cs` | 010 전투 연출 |
| `Assets/Scripts/Presentation/Interior/TrainEquipmentMode.cs`, `TrainEquipmentView.cs`, `TrainCarGridView.cs`, `Editor/TrainEquipmentBuild.cs` | 장비 모드 |
| `Assets/Scripts/Hud/RaidScreen.cs`, `CombatFxScreen.cs`, `Editor/Screens/RaidScreenBuilder.cs`, `CombatFxScreenBuilder.cs` | 습격·전투 연출 화면 |
| `Assets/Scripts/Art/Editor/SpriteGenRaid.cs`, `SpriteGenEquipment.cs` | 전투 스프라이트 생성기 |
| `Assets/Scripts/Bootstrap/Editor/MapCameraSync.cs` | 카메라는 Rebuild가 정한다 |
| `Assets/Scripts/Track/Resources/Track/travel-side.json` | 037 데이터 |
| `Assets/Prefabs/Raid/**`, `Assets/Prefabs/Travel/**`, `Assets/Prefabs/UI/Screens/Raid.prefab`, `CombatFx.prefab`, `Assets/Prefabs/Interior/TrainCarGrid.prefab`, `EquipmentPiece.prefab` | 프리팹 |
| `Assets/Art/Generated/raid/**`, `equipment/**`, `travel/**` | 아트 |
| `Assets/Scenes/Demo/Story005/**`, `Assets/Scenes/Demo/Story010/**` | 보관된 스크립트만 쓰던 데모 씬(빌드 설정에 없음) |

`IPlacementSurface.cs`는 `PlacementGhost`가 쓰는 범용 면이라 그대로 두었다(장비 분기만 지움).

## 2. 지운 계약·코드 경로

- `IronRingContracts.cs`: `RaidUnit`, `IRaidLoadout`, `IRaid`, `EquipmentSlot`, `ITrainLoadout`, `IRaidLoadoutStats`, `IScrap` 삭제. `EncounterOutcome`에는 `EventResolved`만 남김. `IWaypointResolver`에 기본 멤버 `BoardChanceBonus`(승객 합승), `CodexUnlocks`(떠돌이 상인) 추가.
- `IronRingPresentationContracts.cs`: `ICombatRoundCommand` 삭제. `IEncounterPresentationSource`에는 `EventResolved`만 남김.
- `WaypointResolver`: 주사위 전투, 습격, 달리면서 치르는 습격 경로 삭제. 휴식지에 닿으면 반드시 이벤트 하나를 고르고, 로그 `EVENT: <stopId> <eventId>`를 남긴다.
- `EncounterData`: `_eventRatio`, `_rounds`, `_trainStrength`, `_enemy*`, `_reward*`, `_penalty*`, `_prosperityPenalty`, `_victoryProsperity`, `_eventProsperity`, `_raidMode` 삭제. 이벤트 표와 휴식지 가중치 표만 남김.
- `TopBarScreen`: "다음 라운드" 버튼, 고철 표시, 정차 출발 삭제. 이벤트가 끝나면 토스트(`_eventToastSeconds`, 기본 2.5초).
- `HotbarScreen`: "열차" 버튼, 장비 모드, 장비 비용·구매 가능 표시 삭제.
- `PlacementGhost`, `PlacementEntry`: `PlacementKind.Equipment`와 그 분기 삭제.
- `RunResultScreen`: 장비·고철 줄 삭제. `Passengers`: `IRaid` 보너스 대신 `IWaypointResolver.BoardChanceBonus`, 패배 분기 삭제.
- `TrainController`: `EdgeSecondsOverride`, `HeldProgressSource` 삭제. `MapView`: 습격 중 클릭 차단 삭제.
- `HabitatCodex`: 승리 해금 삭제. 이벤트마다 `PerEvent + CodexUnlocks`개 해금. `EncounterItemRewards`: 승리 보상 분기 삭제.
- `MainRoot`: `ICombatRoundCommand` 등록과 전투 캡처 삭제. 이벤트 진행 대기 시간은 `CombatStepSeconds + ExtraHoldSeconds`(선로 보수).
- `BuildMainCanvas`: SCREEN_IDS에서 Raid·CombatFx 삭제. 캔버스에 남은 두 인스턴스는 생성기가 지움(`REMOVED_SCREEN_IDS`). TopBar·Hotbar 화면 프리팹은 재생성 표식으로 다시 만듦.
- `MainSettings.json`: `_travelView`, `_stopView` 삭제.
- `Story012Build`: 예전 `Presentation/Interior/Resources` 폴더 삭제 동작을 지움. 036의 `RuinDressing.json`을 지워 `RuinBuild`를 실패시키던 버그였다.

## 3. 데이터 키

### 원형 선로 `Assets/Scripts/Track/Resources/Track/ring-track.json`
반지름 7, 중심 (0, 1). 모든 노드가 `_hasBend:true`이고, 굽음 점은 사분면 모서리다.

| 순서 | `_id` | `_isStation` | `_labelKey` | `_mapPosition` | `_bend` | 이름(ko) |
|---|---|---|---|---|---|---|
| 0 | `hub-1` | true | `hub_1` | (-7, 1) | (-7, 8) | 황동골 |
| 1 | `REST-1` | false | `rest_1` | (0, 8) | (7, 8) | 잿더미 쉼터 |
| 2 | `REST-2` | false | `rest_2` | (7, 1) | (7, -6) | 녹슨 다리 |
| 3 | `REST-3` | false | `rest_3` | (0, -6) | (-7, -6) | 급수탑 쉼터 |

- `_map._cameraSize` 9.5, `_cameraPosition` (0, 1, -10).
- 예전 2노드 버전은 `ring-track-2nodes.json`으로 보관했고, `ring-track-4hubs.json`은 그대로 두었다.
- 지명: `HubProfiles.json` hub-1 `_name: "황동골"`, ko.json `hub_1` 값을 "주조소"에서 "황동골"로 바꿈. `Strings`가 키 중복을 허용하지 않아 이 한 줄만 고쳤고, 나머지 키는 덧붙이기만 했다. `MainSettings._names` hub-1은 "Brassvale"이다.
- `HudCatalog.json`, `MainSettings._names`에 REST-1~3 항목을 추가했다.

### 휴식지 이벤트 `Assets/Scripts/Combat/Resources/Story005Encounters.json`
`_events[]`: `{_id, _title, _effect, _amount, _resource, _delta}`. 효과 종류:

| `_effect` | 동작 |
|---|---|
| `prosperity` | 다음 거점 번영에 `_amount`를 더함 |
| `codex` | 설계도 `_amount`개를 추가로 해금 |
| `boarding` | 이번 정차의 탑승 확률 +`_amount` |
| `delay` | `_amount`초 더 멈춤 |
| `resource` | `_resource`에 `_delta`를 더함 |

| `_id` | 효과 | ko 키 |
|---|---|---|
| abandoned_freight / trackside_provisions / coal_cache / leaking_water_tank | prosperity +5 (기존 `_eventProsperity` 5를 옮김) | 기존 키 |
| wandering_merchant | codex 1 | event_wandering_merchant |
| hitchhikers | boarding 0.3 | event_hitchhikers |
| dust_storm | prosperity −5 | event_dust_storm |
| abandoned_warehouse | prosperity +4 | event_abandoned_warehouse |
| quiet_rest | prosperity +1 | event_quiet_rest |
| track_repair | delay 3 | event_track_repair |

`_restStops[]`: `{_id, _events:[{_eventId,_weight}]}`. 이벤트는 시드가 고정된 난수(`_combatSeed`)로 뽑는다.

| 휴식지 | 가중치 |
|---|---|
| REST-1 | 떠돌이 상인 3, 버려진 창고 3, 휴식 2, 폭풍 1, 버려진 화물 1 |
| REST-2 | 선로 보수 3, 승객 합승 2, 폭풍 2, 석탄 저장고 1, 휴식 1 |
| REST-3 | 승객 합승 3, 휴식 3, 새는 물탱크 2, 떠돌이 상인 1, 선로변 보급품 1 |

## 4. 브리지 결과

| 단계 | 결과 |
|---|---|
| `refresh` (보관 직후) | failed:true (`PlacementGhost`의 `IronRing.Train` 참조). 참조를 정리한 뒤 다시 시도 |
| `refresh` (정리 후) | failed:false |
| 1회차 `BuildTopDownMain.Rebuild` | ok |
| `Story012Build.Generate` | ok |
| `BuildMainCanvas.Generate` | ok |
| `MapLabelsBuild.Generate` | ok |
| `PropsBuild.Generate` | ok |
| `RuinBuild.Generate`, `GenerateMap` | 처음엔 fail(`RuinDressing.json` 없음, 위 `Story012Build` 버그). 고친 뒤 ok |
| 2회차(Rebuild 뒤 6단계) | 모두 ok |
| 3회차 생성기 6단계(캔버스 정리 뒤) | 모두 ok |
| `scene-status` | `Assets/Scenes/Main.unity`, isDirty:false |
| 마지막 `refresh` | failed:false |
| 남은 참조 확인 | 보관된 .cs/.prefab GUID가 `Main.unity`, `MainCanvas.prefab`, 기타 Assets 프리팹·에셋에 없음 |

## 5. 남은 일

- 방어시설(`Assets/Scripts/Defense/**`, `FacilityRow`, `IFacilityBuilder` 사용처, 번영의 시설 항목, `MainSettings._demoFacilityId`)은 아직 보관하지 않았다. `GameSession`, `HubInterior` 시설 칸, `HubPhase` 시설 뷰에 넓게 얽혀 있어 다음 실행으로 넘긴다.
- `--map-depart` 한 바퀴와 휴식지 3곳의 `EVENT:` 로그는 플레이 모드에서 확인하지 않았다(파이프라인 검증만, 사용자가 편집기에서 확인).
- `design/game-brief.md`는 코디네이터 지시에 따라 건드리지 않았다.
