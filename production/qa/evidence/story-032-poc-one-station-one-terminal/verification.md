# Story 032 검증 — POC 역 1곳·종점 1곳 왕복

상태: **코드·데이터 완료, 편집기 검증 미완.** 편집기가 Play 모드여서 브리지가 `Command requires Edit mode`로 거절했다(3회). 그래서 `refresh`(컴파일 확인)와 씬 재생성은 아직 돌리지 않았다. `Main.unity`는 손대지 않았고 4거점 씬 그대로다.

## 1. 데이터 변경

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Track/Resources/Track/ring-track-4hubs.json` | 새 파일. 원래 8노드 데이터를 그대로 보관한다. `TrainController`는 `Resources.Load("Track/ring-track")`, 편집기 도구는 `ring-track.json` 경로로만 읽으므로 이 파일을 읽는 코드는 없다. |
| `Assets/Scripts/Track/Resources/Track/ring-track.json` | 노드 2개. `hub-1`(역, `_position` 320,360, `_mapPosition` -8,2, 라벨 `hub_1`)과 `TERMINAL`(`_isStation:false`, `_position` 960,360, `_mapPosition` 8,2, 라벨 `waypoint_terminal`). 굽힘은 없다. `_map._cameraSize`를 11.25에서 7.5로, `_cameraPosition`을 (0,1,-10)으로 바꿨다. 나머지 `_map`·타이밍은 그대로다. |
| `Assets/Scripts/Npc/Data/NpcPool.json` | `_ignoreHubRoster: true`, `_ignoreOrigin: true` 추가 |
| `Assets/Scripts/Bootstrap/Data/MainSettings.json` | `_demoHubId` hub-2를 hub-1로 바꾸고(통합 데모), `_names`에 TERMINAL 추가 |
| `Assets/Scripts/Hud/Data/HudCatalog.json` | `_nodes`에 `TERMINAL` → `waypoint_terminal` 추가 |
| `Assets/Scripts/Localization/Resources/ko.json` | 끝에 추가: `waypoint_terminal`="종점", `result_victory_count`="승리 (정착 {0}/{1})", `result_body_victory_all`="모든 거점이 정착했다. 철환은 다시 숨을 쉰다." |
| `design/game-brief.md` | 목표 줄과 MVP 1에 "(POC는 역 1곳·종점 1곳 왕복; 거점 4곳 데이터는 보관)" 추가 |
| `Hub/Data/HubProfiles.json`, `habitats.json` | 바꾸지 않았다. 4곳 그대로 두고, 선로에 없는 거점은 런타임에서 무시한다. |

맵 좌표 설명: 선을 y=2에 둔 것은 `BuildTopDownMain`의 거점 안쪽 이동 때문이다. 그 코드는 `y -= Sign(y)*HubInset`으로 건물을 옮긴다. 선이 y=2이면 건물은 0.2로 선 아래에 오고, 라벨은 그보다 더 아래(-1.15)에 붙는다. 선을 y=0에 두면 라벨이 선로 위에 겹친다.

## 2. 왕복 동작 (코드 읽기로 확인)

- `RingTrack`: `NextIndex = (i+1) % 2`라서 0, 1, 0, 1… 순서로 간다. `CurrentIndex == 0`에 도착할 때마다 `LapCount++`와 `LapCompleted`가 일어난다. 즉 hub-1로 돌아올 때마다 1바퀴다. TERMINAL은 역이 아니므로 `WaypointReached`가 일어나고 `WaypointResolver`가 이벤트·전투를 처리한다. 조우가 끝나면 `EncounterEnded`가 나고 `Passengers.RollBoarding`이 승객을 태운다.
- `BuildTopDownMain.CreateRailPlan`: i=0(hub-1→TERMINAL)과 i=1(TERMINAL→hub-1)이 같은 칸을 `Dictionary<Vector2Int,string>`에 쓴다. 그래서 레일은 한 번만 놓이고 중복이 없다. 코드는 바꾸지 않았다.
- `MapView.LateUpdate`: 방향은 `to - from`으로 구한다. 돌아오는 길에는 (-1,0)이므로 스프라이트가 180° 돌아 왼쪽을 본다. `IsHeld`(종점 조우 hold)인 동안에는 회전을 유지하고, 출발하면 뒤집힌다. 코드 변경은 없다.
- 시작 위치는 노드 0(hub-1)이고, 처음 방향은 노드 1 쪽(오른쪽)이다.

## 3. 런 규칙 (거점 수 = 선로의 역 수)

- `Bootstrap/GameDefinition.cs`: 예전에는 거점 프로필, 선로 역, 레시피 id가 모두 같아야(SetEquals) 했다. 이제 규칙은 이렇다. 선로 역은 프로필과 레시피 양쪽에 다 있어야 한다(부분집합). `HubDefinitions`와 `Recipes`는 선로 역만 남기고 거른다. NPC `_hubIds`는 프로필 전체(4곳)를 기준으로 검증한다.
- 그 결과 `RunState.HubIds`는 [hub-1] 하나다. 조기 승리는 `CountSettled() >= 1`로 판정되고, 기한 종료 판정, 결과 화면 거점 줄(`_result.Hubs`), `HubCount`도 모두 1 기준이 된다. 기한 8바퀴는 그대로다.
- `Hud/RunResultScreen.cs`: 승리 제목을 `result_victory_count`로 바꿔 "승리 (정착 1/1)"로 보이게 했다. 본문은 `result_body_victory_all`이다.

## 4. 승객 플래그

- `Npc/NpcPoolData.cs`에 `_ignoreHubRoster`, `_ignoreOrigin`을 추가했다(기본 false, JSON에서 true). `IgnoreHubRoster`와 `IgnoreOrigin` 프로퍼티로 읽는다.
- `Npc/Passengers.cs`:
  - 하차(`CanDisembark`): `IgnoreHubRoster`이면 `AllowsHub`(명단) 검사를 건너뛴다. 서식지 종류 완성과 숨은 조건(`_conditions`)은 그대로 본다.
  - 탑승(`IsBoardCandidate`): `IgnoreOrigin`이면 출신 경유지 검사를 건너뛴다. 출신이 NORTH/EAST/SOUTH/WEST PASS인 NPC 18명은 선로에 그 경유지가 없어서 이 플래그가 없으면 영영 탈 수 없다. 탑승 조건은 `ConditionHub`에서 판단한다. NPC의 첫 거점이 런에 있으면 그 거점, 없으면 런의 첫 거점(hub-1)이다. 이렇게 해야 RunState에 없는 hub-3 같은 거점의 번영도·단계를 읽다가 예외가 나지 않는다.
  - 탑승 순환 우선(`PriorityHub`)은 `_hubs.HubIds`(=[hub-1])를 순환하므로 hub-1로 고정된다.
  - 가르침·설계도 해금 코드는 건드리지 않았다.

## 5. 씬 재생성 (미실행)

- `Presentation/Interior/Editor/Story012Build.cs`: `Generate`가 먼저 `RemoveOffTrackInteriors`를 돌리도록 했다. 거점 id가 선로 역이 아닌 HubInterior 인스턴스를 지우는 함수다. 로그는 "제거 n, 교체 n, 추가 n"이다.
- `Presentation/Map/Editor/PropsBuild.cs`: 코드는 이미 씬의 `HubTarget` 개수만큼 소품을 놓는다. 그래서 문서 주석만 고쳤다. Rebuild 뒤에는 Props 루트가 새로 생기므로 hub-1 소품 4개(`Prop_1_0..3`)만 놓인다.
- 실행할 순서 (Edit 모드에서):
  1. `refresh` → `failed:false`
  2. `execute-method --method IronRing.Bootstrap.Editor.BuildTopDownMain.Rebuild`
  3. `... IronRing.Presentation.Interior.Editor.Story012Build.Generate`
  4. `... IronRing.Hud.Editor.BuildMainCanvas.Generate`
  5. `... IronRing.Presentation.Map.Editor.MapLabelsBuild.Generate`
  6. `... IronRing.Presentation.Map.Editor.PropsBuild.Generate`
  7. `scene-status` (isDirty=false). 이어서 3~6을 한 번 더 돌려 변경 0인지 본다(멱등).
- 기대 루트: `Main Camera`, `EventSystem`, `Iron Ring / Real Systems`, `Top Down Map`(Rails 17칸 x=-8..8, `hub-1`/`hub_1` 건물+라벨, `TERMINAL`/`waypoint_terminal` 표지+라벨 "종점", Train), `HubInteriors`(`HubInterior hub-1` 하나), `MainCanvas`, `Props`.

## 6. 데모

- `--npc-demo --npc-stage hub2`: `NpcHomesDemoDriver.Create`가 `ITrackRuntime`에서 `_hub2Id`(hub-2)가 역인지 본다. 역이 아니면 경고 로그를 남기고 `homes`로 대신한다.
- `--result-demo --result-stage partial`: 역이 1곳이면 `Min(partialHubs,1)=1`이 되어 승리가 돼 버린다. 그래서 `HubIds.Count - 1`(=0)곳만 정착시키고 경고 로그를 남긴다. 결과는 기한 종료 패배다. `win`은 1곳을 정착시켜 조기 승리 "승리 (정착 1/1)"로 끝나고, `lose`는 그대로다.
- `--integration-demo`: `_demoHubId`를 hub-1로 바꿨다.
- `--habitat-demo`, `--placement-demo`, `--codex-demo`, `--deadline-demo`, `--map-depart`: hub-1만 쓰거나(코드 grep 결과 hub-2~4를 하드코딩한 런타임 코드는 없음) 거점 수에 맞춰 돈다. 아직 실행해서 확인하지는 않았다.

## 7. 브리지 결과

| 명령 | 결과 |
|---|---|
| `refresh` ×3 (약 75초 간격 포함) | `ok:false`, `Command requires Edit mode` (편집기 Play 중) |
| Rebuild / Generate 4종 / scene-status | 미실행 |

## 8. 캡처 플래그 (코디네이터)

- `--map-depart` 약 200프레임: 출발 직후, 역·종점·열차
- `--map-depart` 약 600프레임: 종점 정차, 이벤트/전투 문구 (시점은 `_edgeSeconds` 2초 기준 추정이라 조정이 필요할 수 있다)
- `--result-demo --result-stage win`: "승리 (정착 1/1)", 거점 줄 1개
- `--npc-demo --npc-stage board`: 종점에서 승객 탑승

## 9. 남은 문제

- 컴파일 확인, 씬 재생성, 멱등 확인을 하지 못했다(편집기 Play 모드).
- `_ignoreOrigin`은 지시에 없던 추가 플래그다. 출신 경유지가 선로에 없는 NPC도 탈 수 있게 하려고 넣었다. 기획 확인이 필요하다.
- `Assets/Tests`에 거점 id SetEquals 불일치를 기대하는 테스트가 있으면 GameDefinition 규칙 변경으로 실패할 수 있다(테스트는 범위 밖이라 실행하지 않음).
- 결과 본문 키 `result_body_victory`("네 거점이…")는 남아 있지만 더는 쓰지 않는다.
