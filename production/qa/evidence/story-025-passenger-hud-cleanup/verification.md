# 스토리 025 검증: 승객 화면 반영과 UI 잔재 정리

날짜: 2026-11-01 · 편집기 6000.3.27f1(열린 상태, 브리지만 사용) · 자동 테스트 없음(사용자 결정)

## 1. 바뀐 것

| AC | 구현 |
|---|---|
| 상단 바 "승객 N/3" | `TopBarScreen`: `_passengers` 버튼(도감 버튼 왼쪽, 160×38). 라벨은 `hud_passengers` = "승객 {0}/{1} (P)", N은 `IPassengers.PassengerIds.Count`, 정원은 `PassengersScreen.Capacity` = `NpcInstaller.Pool.MaxPassengers`(NpcPool.json `_maxPassengers`). `Boarded/Disembarked/Left`가 오고 기존 Refresh가 돌 때 갱신한다. IPassengers가 없는 데모 씬에서는 버튼을 숨긴다. 클릭하면 `PassengersScreen.Toggle` |
| 승객 화면 | 새 상황 화면 `Passengers`(`SCREEN_IDS`에서 TopBar 다음). `PassengersScreen`: 승객마다 부품 `PassengerRow` 한 줄(직업명 `INpcInteraction.DisplayName`, "<출신지> 출신" `OriginName`, "탄 지 N바퀴" `LapsAboard`, “제안” `Proposal`, 힌트 `Hint`). 승객이 없으면 "승객이 없다". 열려 있을 때 탑승·하차·이탈이 오면 다시 그린다. 닫기 버튼, Esc, P 키(`ScreenTicker`)를 지원한다. 프리팹에는 미리보기 줄 2개(점등부, 가스관 기술자)가 있고 초기화 때 지운다. 숨은 조건은 표시하지 않는다(힌트 문장만 보인다). 탑승 토스트(`PassengerAnnouncer`)는 그대로 둔다 |
| 대화 패널 출신지 | `NpcDialogScreen._origin`: 머리줄의 집 라벨 옆에 "<출신지> 출신"(`npc_dialog_origin`, `INpcInteraction.Origin`)을 쓴다. 값이 null이면 라벨을 끈다. 패널 너비를 320에서 390으로 넓혔다 |
| 도감 "찾아오는 이" | `CodexScreen.Visitors`: `CandidatesOf(typeId)` 가운데 `JoinedOf`에 든 사람은 직업명으로, 나머지는 `codex_visitor_unknown`("???")으로 쓴다. 합류한 사람을 먼저 적는다(예: "점등부, ???"). 후보가 없으면 예전처럼 NPC 종류 이름을 쓴다. 열려 있을 때 `NpcJoined`가 오면 다시 그린다. 카드 Npc 라벨은 한 줄 고정(NoWrap), 자동 축소 0.7배, 그래도 넘치면 말줄임(`CodexCard` 부품 재생성) |
| 잔재 정리 | `PlacementGhost.UiHitTests`, 그 Clear, 판정 루프를 지웠다. 이제 `EventSystem.IsPointerOverGameObject`만 쓴다. `InteriorNpcWalker.IsPointerOverUi`는 EventSystem `RaycastAll`만 쓴다. `HubPhaseViewSettings._hudSortingOrder`와 `HudSortingOrder`, 문서 주석, `hub_phase_view.json`의 `_hudSortingOrder`를 지웠다. `grep -rn "UiHitTests\|HudSortingOrder" Assets/Scripts`는 0건이다. `ItemsDemoDriver` 등에는 죽은 참조가 없었다(022 3단계에서 정리됨) |
| 데모 | `NpcHomesDemoDriver`: `--npc-stage passengers`는 `_passengerIds`(기본값 lamplighter, gas_fitter, `NpcHomesDemo.json`)를 `TryBoard(id, "demo", force:true)`로 태운 뒤 StageReady를 낸다. 그러면 `PassengersScreen`이 열린다. 내부 화면은 열지 않는다. `--npc-stage codex`(추가)는 homes와 같이 hub-1에 점등부 등을 합류시킨 뒤 StageReady를 내고, 그러면 `CodexScreen`이 열린다. 기존 단계는 그대로다 |
| 문자열 | `ko.json`에 키 18개를 추가했다: `hud_passengers`, `passengers_title/hint/empty/close/origin/laps`, `npc_dialog_origin`, `codex_visitor_unknown`, `ui_sample_codex_visitors`, `ui_sample_passenger_{1,2}_{name,origin,proposal,hint}` |

색과 스프라이트는 모두 `UiTheme` 값만 쓴다(024 Reskin 대상). 승객 패널은 720×470, 안쪽 여백 18이고 줄은 664×112, 여백 16이다.

## 2. 파일

- 새 파일: `Assets/Scripts/Hud/Npc/PassengersScreen.cs`, `Hud/Npc/PassengerRowView.cs`, `Hud/Editor/Screens/PassengersScreenBuilder.cs`, `Assets/Prefabs/UI/Screens/Passengers.prefab`, `Prefabs/UI/Parts/PassengerRow.prefab`(각 .meta 포함)
- 수정: `Hud/TopBarScreen.cs`, `Hud/Editor/Screens/TopBarScreenBuilder.cs`, `Hud/Npc/NpcDialogScreen.cs`, `Hud/Editor/Screens/NpcDialogScreenBuilder.cs`, `Hud/Codex/CodexScreen.cs`, `Hud/Editor/Screens/CodexScreenBuilder.cs`, `Hud/Editor/BuildMainCanvas.cs`(SCREEN_IDS만), `Presentation/Interior/PlacementGhost.cs`, `Presentation/HubPhase/InteriorNpcWalker.cs`, `HubPhaseViewSettings.cs`, `Resources/HubPhase/hub_phase_view.json`, `Npc/Demo/NpcHomesDemoDriver.cs`, `Npc/Resources/Npc/NpcHomesDemo.json`, `Localization/Resources/ko.json`
- 생성기 결과: `Prefabs/UI/MainCanvas.prefab`, `Screens/TopBar·NpcDialog·Codex.prefab`, `Parts/CodexCard.prefab`. `Assets/Fonts/NotoSansKR SDF.asset`은 TMP 동적 아틀라스 변경이다(022와 같은 현상)
- `ScreenTicker.cs`는 고치지 않았다. P 키는 `PassengersScreen.Tick`을 `ScreenTicker.Add`로 등록해 읽는다(C 키와 같은 방식)

## 3. 브리지 결과

- 첫 `refresh`는 `PassengersScreen.cs`에서 CS0103(`GameServices`)이 나 실패했다. `using IronRing.Contracts.Presentation`을 추가한 뒤 `refresh`는 `failed=false, errors=""`, `compile-status`는 깨끗하다
- 표식 `.bridge/rebuild/` TopBar, NpcDialog, Codex, CodexCard, Passengers, PassengerRow를 두고 `execute-method IronRing.Hud.Editor.BuildMainCanvas.Generate`를 실행했다. 로그는 `REBUILT SCREEN: TopBar`, `REBUILT PART: PassengerRow`, `REBUILT SCREEN: Passengers`, `REBUILT SCREEN: NpcDialog`, `REBUILT PART: CodexCard`, `REBUILT SCREEN: Codex`, `GENERATE: canvasChanged=True sceneChanged=False`이고 표식은 모두 소비됐다
- `Generate`를 다시 돌리면 `canvasChanged=False sceneChanged=False`(멱등)이다
- `scene-status`: `Assets/Scenes/Main.unity`, isDirty=false, playing=false
- 프리팹 바인딩: TopBar `_passengers`, NpcDialog `_origin`, Passengers `_empty`, `_rowPrefab`(PassengerRow GUID)이 연결되어 있다. MainCanvas.prefab이 Passengers 화면 GUID를 참조한다

## 4. 캡처 플래그(코디네이터)

| 스크린샷 | 플래그 | 볼 것 |
|---|---|---|
| (1) 승객 화면 | `--npc-demo --npc-stage passengers` | 줄 2개(점등부, 가스관 기술자): 출신지, “제안”, 힌트, "탄 지 0바퀴". 머리줄 "열차 승객  2/3" |
| (2) 상단 바 승객 수 | 같은 실행 또는 `--map-depart` | "승객 2/3 (P)"(passengers), 출발 화면은 "승객 0/3 (P)". 도감 버튼과 겹치지 않는지 |
| (3) 대화 패널 출신지 | `--npc-demo --npc-stage panel` | 집 줄 옆 "떠돌이 선로 출신"(점등부). 넓힌 패널(390) |
| (4) 도감 "점등부, ???" | `--npc-demo --npc-stage codex` (보조: `--codex-demo --codex-stage start`) | 가스등 거리 카드의 "찾아오는 이: 점등부, ???". codex-demo는 합류가 없어 "???, ???" 형태가 정상이다 |

## 5. 남은 것과 주의

- 스크린샷 4장은 코디네이터가 찍는다(이 에이전트는 play-capture를 쓰지 않는다)
- `--npc-stage codex`에서 가스등 거리 카드가 해금(도감 발견)되는지는 실행해 봐야 안다. 잠긴 채면 카드가 "???"만 보인다. 그때는 배치 완성이 도감 해금으로 이어지는지 확인하거나 데모에 해금 단계를 추가해야 한다
- Esc는 승객 화면과 도감이 둘 다 열려 있으면 둘 다 닫는다(같은 프레임에 각자 처리). 두 화면은 서로 배타적이지 않다
- 024 Reskin 이후 승객 패널과 줄의 틀 두께가 여백 16~18 안에 들어가는지 다시 볼 것
