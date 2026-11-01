# 스토리 028 검증 기록: 런의 끝과 번영 규칙

- 날짜: 2026-11-01
- 범위: 점검 보고서 `production/qa/playtest-2026-11-01.md`의 S1-1, S1-2, S2-1, S2-2, S2-3, S2-4, S3-1
- 검증 방법: 열린 편집기 브리지(`refresh`, `execute-method`, `scene-status`). Play 캡처는 코디네이터가 맡는다(아래 플래그).
- 테스트: 이번 단계에서는 쓰지 않았다(지시: 테스트·커밋 없음). Logic 스토리라 닫기 전에 유닛 테스트가 필요하다(열린 문제 참고).

## 1. 새 번영 공식

바퀴당 Δ (붕괴 거점만 건너뜀, **폐허도 적용**, 바닥 `_minimum`=0):

```
Δ = _basePerLap × _decayScale
  + _perPlacedItem × 놓인 칸 수            (0: 아무 타일이나 깐 칸은 번영 없음)
  + _perHabitatCell × 완성 서식지에 속한 칸 수 (1, 새 키)
  + Σ 완성 서식지 보너스                     (카탈로그 값, 없으면 _habitatBonus)
  + _perNpc × _npcScale × 합류 NPC 수
  + _perFacility × _facilityScale × 시설 수
```

`HubProfiles.json` 값: `_basePerLap -6`(이전 -3), `_perPlacedItem 0`(이전 1), `_perHabitatCell 1`(새로 추가), `_decayScale` hub-3 0.9(이전 0.5)·hub-4 1.2(이전 1.5), 나머지는 그대로다.
따라서 거점별 감소는 hub-1과 hub-2가 -6, hub-3이 -5.4, hub-4가 -7.2다.

- 점검 보고서는 `_decayScale`·`_npcScale`·`_facilityScale`을 "읽는 코드 없음"으로 적었는데, 이는 틀렸다. `HubData.ToDefinition`이 실제로 곱한다.
- 그래서 hub-3(0.5)과 hub-4(1.5)의 배율을 목표 범위 안으로 좁혔다. 배율이 0.5면 hub-3에서 보일러실 하나만으로 7바퀴에 정착하고, 1.5면 hub-4에서 서식지 2개와 NPC 1명으로도 5바퀴가 걸렸다.
- 폐허도 감소를 받는다(`HubState.ApplyDelta`에서 "Ruined는 감소 무시"를 제거). 따라서 초반 전투 패배 −10이 폐허 거점에도 그대로 들어간다(S2-3).

## 2. 다시 계산한 밸런스 표

조건: 시작 0, 정착 40, 기한 8바퀴. NPC는 2바퀴째부터 센다(하차 뒤).
서식지는 가스등 거리(3칸, +4), 보일러실(4칸, +5), 석탄 야적장(4칸, +5)을 쓴다. 서식지 하나의 바퀴 기여는 "칸 수 + 보너스"다(가스등 7, 보일러실 9, 석탄 야적장 9).

### 경유지 결과 제외(EV 0)

| 구성 | hub-1 번영 추이 | 정착 바퀴 hub-1 / hub-2 / hub-3 / hub-4 |
|---|---|---|
| 가스등 거리 1 | 1, 2, 3 … 8 | 못함 / 못함 / 못함 / 못함 |
| 보일러실 1 | 3, 6, 9 … 24 | 못함 / 못함 / 못함 / 못함 |
| 보일러실 1 + NPC 1 | 3, 9, 15 … 45 | 8 / 6 / 7 / 못함 |
| 보일러실 + 석탄 야적장 | 12, 24, 36, 48 | 4 / 4 / 4 / 4 |
| **보일러실 + 석탄 야적장 + NPC 1** | 12, 27, 42 | **3 / 3 / 3 / 4** |
| **가스등 거리 + 보일러실 + NPC 1** | 10, 23, 36, 49 | **4 / 4 / 4 / 4** |
| 서식지 3 | 19, 38, 57 | 3 / 3 / 3 / 3 |
| 아무 타일 40칸(서식지 아님) | 0, 0, 0 … | 못함(이전: 1바퀴) |

### 경유지 기대값 +1.0/바퀴 포함(점검 보고서 계산: 이벤트 +2.5, 전투 −1.5)

| 구성 | hub-1 번영 추이 | 정착 바퀴 hub-1 / hub-2 / hub-3 / hub-4 |
|---|---|---|
| 가스등 거리 1 | 2 … 16 | 못함 ×4 |
| 보일러실 1 | 4 … 32 | 못함 ×4 |
| 보일러실 1 + NPC 1 | 4, 11 … 46 | 7 / 6 / 6 / 8 |
| 보일러실 + 석탄 야적장 + NPC 1 | 13, 29, 45 | 3 / 3 / 3 / 3 |
| 가스등 거리 + 보일러실 + NPC 1 | 11, 25, 39, 53 | 4 / 3 / 3 / 4 |
| 서식지 3 | 20, 40 | 2 / 2 / 2 / 3 |

목표 확인:
- 서식지 2개 + NPC 1명이면 모든 거점이 3~4바퀴에 정착한다(목표대로).
- 서식지 하나뿐이면 기대값을 더해도 8바퀴 안에 정착하지 못한다(목표대로).
- 서식지 하나 + NPC 1명은 6~8바퀴로, 기한에 겨우 닿는 중간 구간이다.
- 정착한 거점도 바퀴마다 -6 안팎이 빠진다. 서식지를 치우면 다시 내려가고 0이 되면 붕괴한다.

## 3. 런의 끝(S1-1)

- 기한(`_deadlineLaps` 8)은 정착 여부와 상관없이 0이 되면 런을 끝낸다. 이전에는 한 곳이 정착하면 기한이 꺼졌다.
- 결과는 끝난 순간 Settled 단계인 거점 수로 정한다. 4곳(전부)이면 Victory, 1~3곳이면 Partial, 0곳이면 Defeat(`DeadlineExpired`)다. 붕괴한 거점은 정착 수에 넣지 않는다.
- "한 번 정착했던 거점이 모두 붕괴"하면 그 자리에서 Defeat(`AllSettledCollapsed`)로 끝난다(기존 규칙 유지).
- `IHubStateSystem.IsRunLost`는 이제 결과와 상관없이 "런 종료"를 뜻한다. 열차 정지(`GameSession`의 `run-lost` 홀드)와 버튼 비활성은 기존 경로가 그대로 처리한다. `RunLost` 다음에 `RunEnded`를 낸다.
- 새 계약 `IRunResult`를 추가했다(append-only). `IsOver`, `Outcome`, `SettledCount`, `HubCount`, `Laps`, 끝난 순간의 거점별 `Hubs`(단계·번영·NPC 수) 스냅숏, `RunEnded` 이벤트를 준다. `RunState`가 구현하고 `HubDevelopmentInstaller`가 등록한다. `IRunLossInfo`는 패배 사유만 그대로 준다(승리·부분 성공에서는 None).
- 결과 화면 `RunResult`(UGUI, Simple 스킨, `UiKit.Panel/TitlePlate/Label/Button`)를 추가했다.
  - 내용: 제목(승리 / 부분 성공 (정착 N/4) / 패배), 사유 본문, 거점별 줄(이름 · 단계 · 번영 · 합류 N명), "총 N바퀴", "다시 시작" 버튼.
  - 다시 시작: `GameServices.Reset()` 뒤 활성 씬을 다시 읽는다.
  - `GameServices.Reset()`은 `Ready`/`Composing` 구독을 지운다. 그런데 화면과 설치자는 `AfterSceneLoad` 부트 훅에서 한 번만 구독한다. 그래서 씬을 다시 읽으면 `sceneLoaded`(새 Awake 뒤, `MainRoot.Start` 전)에서 Assembly-CSharp의 `AfterSceneLoad` 훅을 리플렉션으로 다시 부른다. 로그는 "RUN RESTART: re-ran N AfterSceneLoad hooks"다.
  - EventSystem은 씬에 하나만 있고 `DontDestroyOnLoad`가 아니어서 중복되지 않는다. `ScreenshotOnArg`와 `CoordinatorCapture`만 `DontDestroyOnLoad`로 남는다.
- `Defeat` 화면은 `SCREEN_IDS`와 프리팹에 그대로 남겨 두었지만, `IRunResult`가 등록되어 있으면 열리지 않는다(`DefeatScreen.Wanted()`).

## 4. 승객 탑승 공정성(S2-2)

`Passengers.RollBoarding`이 탑승자를 고르는 순서는 다음과 같다.
1. 조건을 만족하는 후보를 모은다.
2. 풀 시드(`_passengerSeed`)로 Fisher-Yates 셔플을 한다.
3. 이번 굴림의 우선 거점 명단(`EligibleHubIds[0]`)을 앞으로 옮긴다(안정 분할).
4. 그 순서로 확률을 굴린다.

우선 거점은 빈 좌석이 있는 굴림마다 hub-1 → hub-2 → hub-3 → hub-4 → hub-1 순으로 돈다. 좌석이 차 있으면 순번도 넘기지 않는다. 경유지마다 `PASSENGER: roll at <wp> priority=<hub> candidates=N` 로그를 남긴다. 순서 함수는 `Passengers.BoardingOrder(candidates, priorityHub, random)`(public static)다.

브리지 로그 `execute-method IronRing.Npc.Demo.BoardingFairnessProbe.Run`의 조건: 실제 `NpcPool` 사용, 48경유지(12바퀴), 탑승 조건은 통과로 근사, 하차 없음, 인내 뒤 떠남.

```
BOARD-PROBE new: hub-1=3 hub-2=3 hub-3=3 hub-4=3 | wp1(hub-1):lamplighter@hub-1 wp1(hub-1):clockmaker@hub-1 wp1(hub-1):stoker@hub-1 wp13(hub-2):gas_fitter@hub-2 wp13(hub-2):coal_merchant@hub-2 wp13(hub-2):telegrapher@hub-2 wp25(hub-3):boilermaker@hub-3 wp25(hub-3):night_watch@hub-3 wp25(hub-3):ironworker@hub-3 wp37(hub-4):pump_operator@hub-4 wp37(hub-4):tower_keeper@hub-4 wp37(hub-4):electrician@hub-4
BOARD-PROBE old: hub-1=11 hub-2=1 hub-3=0 hub-4=0 | ...
```

- 성채(hub-4) 명단 NPC(pump_operator, tower_keeper, electrician)는 hub-4가 우선이 되는 네 번째 탑승 굴림에서 탔다.
- 이 근사는 하차를 넣지 않아 좌석이 3바퀴씩 막힌다. 실제 게임에서는 하차가 일어나므로 순번이 더 빨리 돈다.
- 실제 플레이 로그는 `--npc-demo --npc-stage board` 캡처에서 확인한다(코디네이터).

## 5. 핫바 기본 칸(S2-4)

`Hotbar.json`의 1~9번 칸: 가스등(tile_gaslamp), 석탄더미(tile_coal), 보일러(tile_boiler), 증기관, 톱니장치, 철골, 벽돌길, 벽(wall), 포탑(turret).

- 9칸을 유지하려고 공방(workshop)을 핫바에서 뺐다. `_facilities` 목록에는 남아 있다.
- `_demo._dragSlot: 1`은 이제 석탄더미 칸을 가리킨다.

## 6. 묶음 구성 변화 신호(S3-1)

- 새 계약은 `IHabitatClusterChanges { event Action<string, string> HabitatChanged; }`(hubId, 종류 id)다. append-only이고 `IHabitatClusters`는 바꾸지 않았다.
- `ClusterHabitatSystem`이 구현하고 `HabitatsInstaller`가 등록한다.
- `Evaluate`에서 종류별 완성 수가 같고 0보다 크지만 그 종류 묶음들의 칸 집합(정렬 키)이 바뀌면, Completed/Broken 다음에 종류당 1회 낸다.
- Broken→Completed를 다시 내지 않은 이유: 핫바 토스트, 도감 발견, NPC 집 판정이 `HabitatCompleted`를 구독하고 있어서 가짜 "완성" 알림이 생긴다.
- `ClusterFrameView.cs`·`BuildingView.cs`는 고치지 않았다(스토리 027 소유). 027이 `GameServices.Get<IHabitatClusterChanges>().HabitatChanged`를 구독하면 테두리가 갱신된다.

## 7. 정보 표시

- 배너: "<거점> · <단계> · 번영 N/40 · 다음 바퀴 +M · 서식지 H".
  - M은 `IProsperityForecast.NextLapDelta`로 구한 지금 공식의 값이다. 경유지 결과는 들어가지 않는다.
  - 기한 줄은 "결산까지: N바퀴 남음"이 되었고, 정착해도 사라지지 않는다.
- 거점 패널: 서식지 줄 뒤에 "· 정착까지 번영 K"가 붙는다(정착 단계면 "· 정착"). 빌더는 고치지 않았고 기존 라벨을 썼다.
- 새 계약 `IProsperityForecast`(append-only)는 `RunState`가 구현하고 `HubDevelopmentInstaller`가 등록한다.

## 8. 브리프

`design/game-brief.md`에서 두 곳을 고쳤다.
- 거점 단계 문장: 번영의 출처, 폐허 감소, 3~4바퀴 목표.
- "실패" 줄: "런의 끝" 줄로 바꾸었다(기한 → 승리/부분 성공/패배, 다시 시작).

## 9. 바뀐 파일

- `Assets/Scripts/Contracts/IronRingContracts.cs`: append. `RunOutcome`, `RunHubSummary`, `IRunResult`, `IProsperityForecast`, `IHabitatClusterChanges`.
- `Assets/Scripts/Hub/RunState.cs`, `HubState.cs`, `HubDefinition.cs`, `HubData.cs`, `IProsperitySources.cs`(`HabitatCellCount`), `HubDevelopmentInstaller.cs`, `Data/HubProfiles.json`, `Demo/PhaseDemoDriver.cs`
- `Assets/Scripts/Npc/Passengers.cs`, `Assets/Scripts/Npc/Demo/BoardingFairnessProbe.cs`(새 파일)
- `Assets/Scripts/Habitats/ClusterHabitatSystem.cs`, `Assets/Scripts/Habitats/HabitatsInstaller.cs`(등록 한 줄)
- `Assets/Scripts/Hud/RunResultScreen.cs`(새 파일), `Hud/Editor/Screens/RunResultScreenBuilder.cs`(새 파일), `Hud/Editor/BuildMainCanvas.cs`(`SCREEN_IDS`에 "RunResult"만 추가), `Hud/DefeatScreen.cs`, `Hud/HubPhase/PhaseBannerScreen.cs`, `Hud/HubPanelScreen.cs`
- `Assets/Scripts/Hud/Hotbar/Resources/Hotbar/Hotbar.json`, `Assets/Scripts/Localization/Resources/ko.json`(13개 키 추가)
- 생성물: `Assets/Prefabs/UI/Screens/RunResult.prefab`, `MainCanvas.prefab` 갱신
- `design/game-brief.md`

## 10. 브리지 결과

| 명령 | 결과 |
|---|---|
| `refresh` 1회차 | 실패: `RunResultScreen.cs(99,41) CS0118 'HubPhase' is a namespace`(`IronRing.Hud.HubPhase` 네임스페이스와 충돌). `DevPhase` 별칭으로 고쳤다(HubPanelScreen도 같이 고침). |
| `refresh` 2·3회차 | `failed:false, errors:""` |
| `execute-method BuildMainCanvas.Generate`(표식 `.bridge/rebuild/RunResult`) | ok. 로그 "MAINCANVAS REBUILT SCREEN: RunResult", "canvasChanged=True sceneChanged=False" |
| `execute-method BoardingFairnessProbe.Run` | ok. 위 4절의 로그 |
| `scene-status` | Main.unity, isDirty=false, playing=false |

## 11. 1차 Play 캡처 검수 뒤 수정(2026-11-01)

코디네이터 검수(`028-result-*.png`) 결과, `--result-demo`로 정착까지는 되었지만(번영 94) "결산까지 7바퀴 남음"에서 런이 끝나지 않았다.
- 원인: 데모가 바퀴 사이마다 실시간 0.5초를 기다렸다(`DeadlineLapIntervalSeconds`). 그래서 200프레임 안에 1바퀴만 지났다. 로그 접두어도 `RESULT-DEMO`라 `RESULT DEMO` 검색에 걸리지 않았다.
- 수정: 데모는 `RESULT DEMO: stage=<x>`를 남긴다. 한 프레임 쉰 뒤(화면들이 `RunEnded`를 구독한 다음) 정착 수를 맞추고, 남은 바퀴를 **같은 프레임에 모두** 넘긴다. 결과 화면은 시작 후 몇 프레임 안에 뜬다.
- 로그 순서: `RESULT DEMO: stage=…` → `RESULT DEMO: settled now=…` → `RUN END: outcome=…` → `RESULT DEMO: over=True, outcome=…`

다시 시작의 정적 상태 문제(코드 확인으로 수정):
- 설치자(`HubDevelopmentInstaller`, `ItemsInstaller`, `NpcInstaller`, `HabitatsInstaller`)는 `BeforeSceneLoad`에서 `Composing`을 구독한다. `_installed`/static 시스템은 `SubsystemRegistration`에서만 풀린다.
- 그래서 `AfterSceneLoad`만 다시 부르면 두 번째 런에 설치자가 붙지 않는다.
- `RunResultScreen.Restart`는 이제 새 플레이 진입 순서를 흉내 낸다.
  1. `GameServices.Reset()`
  2. `SubsystemRegistration` 훅 전부
  3. 씬 재로드
  4. `sceneLoaded`(새 Awake 뒤, Start 전)에서 `BeforeSceneLoad` 훅, 이어서 `AfterSceneLoad` 훅
- 캡처 도구(전역 네임스페이스 `ScreenshotOnArg`, `IronRing.Tools.CoordinatorCapture`)는 건너뛴다.
- 로그: `RUN RESTART: reload … (SubsystemRegistration hooks N)` → `RUN RESTART: re-ran A BeforeSceneLoad + B AfterSceneLoad hooks`

재시작 데모 `--result-demo --result-stage restart`:
- 부분 성공으로 끝낸 뒤 60프레임(`_resultRestartFrames`) 후 `RESULT DEMO: pressing restart` 로그를 남기고 `RunResultScreen.Restart()`(버튼과 같은 함수)를 부른다.
- 재로드 뒤 60프레임 후 `RESULT DEMO: after restart ready=True, over=False, lap=0, lapsLeft=8, forecast=True, TopBar=True, PhaseBanner=…, Hotbar=…, RunResult=False`를 남긴다(값은 기대치).
- 재로드가 끝없이 반복되지 않도록 static `_restartIssued`가 막는다.

브리지: `refresh` → `failed:false, errors:""`, `scene-status` → Main.unity, isDirty=false.

## 12. 캡처 플래그(코디네이터)

1. 결과 화면(부분 성공): `--result-demo --result-stage partial`. 기대 화면은 "부분 성공 (정착 2/4)", hub-1·hub-2 정착, 총 8바퀴다.
2. 결과 화면(승리): `--result-demo --result-stage win`
3. 결과 화면(패배): `--result-demo --result-stage lose`. `--deadline-demo`는 실시간 간격으로 바퀴를 넘기므로 약 5초 걸린다.
4. 다시 시작: `--result-demo --result-stage restart`. 재로드 뒤 프레임을 충분히(약 200프레임 이상) 주고, 위 로그와 예외 유무를 확인한다.
5. 배너 "번영 N/40 · 다음 바퀴 +M": `--map-depart`
6. hub-4 승객 탑승 로그: `--npc-demo --npc-stage board`. 로그의 `PASSENGER: roll at … priority=hub-N`을 확인한다. hub-4 명단이 타는 것은 4절 probe 로그로 보완한다.

## 13. 열린 문제

- **다시 시작은 아직 Play로 확인하지 않았다**(코드와 컴파일만 확인). 훅 재생 방식이 막지 못하는 경우가 남아 있다. static 상태를 훅이 아닌 곳(예: static 생성자, 필드 초기값)에서만 정하는 클래스는 두 번째 런에 이전 값이 남을 수 있다. 예외가 나면 그 클래스에 `SubsystemRegistration` 초기화를 추가해야 한다.
- 데모 플래그로 실행하면 재시작 뒤 다른 데모(예: `--phase-demo`)도 다시 부트된다. restart 단계는 두 번째 씬에서 결과 데모를 다시 돌리지 않는다.
- 결과 데모는 `ApplyDelta(+100)`로 정착을 강제한다. 정착 거점은 바퀴마다 감소하므로(hub-4 -7.2 × 8 = 57.6) 기한까지 Settled로 남는다.
- 런은 기한에만 끝난다. 네 곳이 모두 일찍 정착해도 기한까지 기다린다(스펙대로). 조기 승리가 필요한지는 디자이너가 판단할 일이다.
- 혼합 묶음이 재료 타일을 모두 흡수하는 문제(S3-2) 때문에, 큰 보일러실(석탄 20칸) 같은 묶음은 칸 수만큼 번영을 준다. `_perHabitatCell`의 우회로가 된다. 상한이 필요한지 디자이너가 판단할 일이다.
- 승객이 인내 바퀴 동안 좌석을 막는 문제는 그대로다(점검 보고서의 "떠난 NPC 휴식" 권고는 범위 밖).
- Logic 스토리 증거인 유닛 테스트가 아직 없다. 대상: `RunState` 결과 판정, `HubState` 폐허 감소, `Passengers.BoardingOrder`, `HabitatChanged`.
