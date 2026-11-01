# 스토리 030 검증: 조기 승리와 NPC를 따라가는 레시피 해금

- 스토리: `production/epics/iron-ring-train/story-030-early-win-npc-unlocks.md`
- 검증 방법: 열려 있는 편집기 브리지(`refresh`, `compile-status`, `execute-method`)로 확인. 캡처(`play-capture`)는 코디네이터가 한다.
- 날짜: 2026-11-01

## 규칙

### 조기 승리
- 데이터: `Assets/Scripts/Hub/Data/HubProfiles.json`의 `"_earlyWin": true`. `HubData` → `HubRunRules.EarlyWin`으로 읽는다.
- `RunState.Evaluate`: 이번 판정에서 어떤 거점이 Settled로 들어갔고, 그 결과 정착한 거점이 전체 거점 수와 같으면 곧바로 `End(Victory, None, early: true)`를 부른다. `RunLost`와 `RunEnded`가 각각 한 번 나가고, 결과 화면(028)이 "승리"로 열린다.
- 총 바퀴(`Laps`)는 그때까지 적용된 바퀴 수다. 바퀴 처리 중에 정착하면 남은 거점 처리와 기한 감소를 건너뛴다(기존의 `if (IsRunLost) return`).
- 기한 규칙은 그대로다. 기한이 0이 되면 정착 수에 따라 승리, 부분 성공, 패배가 정해진다.
- 로그: `RUN END: outcome=Victory, settled=4/4, laps=N, reason=None, early=True`. `RunState.EndedEarly`도 추가했다.

### NPC를 따라가는 해금(`HabitatCodex`)
- (a) `IPassengers.Boarded(npcId, …)`: 승객이 원하는 종류(`_habitatTypeId`)가 잠겨 있으면 출처 `"board"`로 연다. 토스트는 "탑승: <직업>이 <서식지> 설계도를 건넸다"(`codex_toast_board`)다.
- (b) `INpcRecruiter.NpcJoined(hubId, npcId)`: 그 NPC가 가르치는 종류(`_teachesTypeId`)가 잠겨 있으면 출처 `"teach"`로 연다. 토스트는 "<직업>이 <서식지> 설계도를 가르쳐 줬다"(`codex_toast_teach`)다.
- 연결은 `HabitatsInstaller.BindCodexNpcs`가 한다. 설치 직후 한 번, 그리고 `GameServices.Ready`에서 다시 연결해 NpcInstaller와 설치 순서가 어떻든 동작한다. 원하는/가르치는 종류는 `NpcInstaller.Pool`에서 읽는다.
- `CodexUnlocks.json`의 `_perLap`, `_perEvent`, `_perVictory`는 모두 0이다. 각 구독은 개수가 0이면 아무것도 하지 않고, 코드 경로는 남아 있다.
- 시작 레시피 3개(가스등 거리, 석탄 야적장, 보일러실)와 우연히 완성했을 때의 `"discover"`는 그대로다.
- `IHabitatCodex`에 추가만 했다(기본 구현 포함): `UnlockSourceOf(typeId)`, `UnlockNpcOf(typeId)`.

### 화면
- 도감 카드에 Small 줄 "가르쳐 주는 이: <직업>"(`codex_teacher`)을 넣었다. board 또는 teach로 열린 종류에만 보인다. 카드 높이는 200에서 222가 됐다.
- 승객 줄에 Small 줄 "원하는 것: <서식지 이름>"(`passengers_wants`)을 넣었다. 줄 높이는 112에서 136이 됐다.
- 표식 `Codex`, `CodexCard`, `Passengers`, `PassengerRow`를 두고 `BuildMainCanvas.Generate`로 다시 만들었다. 프리팹에 `Teacher`, `Wants` 노드가 있는 것을 확인했다.

## 가르침 표 (npc → teaches, 자기 서식지와 모두 다름)

| 거점 | NPC(직업) | 원하는 것 | 가르치는 것 |
|---|---|---|---|
| hub-1 | lamplighter 점등부 | lamp_street | lamp_road |
| hub-1 | gardener 원예사 | greenhouse | water_tower |
| hub-1 | stoker 화부 | boiler_room | coal_yard |
| hub-1 | water_keeper 급수원 | cistern | pipe_gallery |
| hub-1 | tinker 수리공 | toolshop | cogworks |
| hub-1,3 | peddler 행상 | cobble_plaza | barracks |
| hub-1 | glass_blower 유리공 | greenhouse | lamp_street |
| hub-1 | lamp_oiler 등유 배달부 | lamp_road | engine_room |
| hub-1,4 | chimney_sweep 굴뚝 청소부 | boiler_room | iron_depot |
| hub-1 | clockmaker 시계공 | cogworks | grand_cogworks |
| hub-2 | mechanic 기계공 | cogworks | repair_bay |
| hub-2 | pipefitter 배관공 | pipe_gallery | cistern |
| hub-2,3 | coal_merchant 석탄 상인 | coal_yard | blast_furnace |
| hub-2 | telegrapher 전신 기사 | telegraph_line | power_house |
| hub-2 | gas_fitter 가스관 기술자 | lamp_street | boiler_room |
| hub-2 | engine_driver 기관사 | engine_room | water_tower |
| hub-2 | brass_polisher 황동 연마공 | toolshop | cogworks |
| hub-2 | signal_keeper 신호수 | lamp_road | telegraph_line |
| hub-2,4 | dirigible_rigger 비행선 삭구공 | repair_bay | iron_depot |
| hub-2 | steam_cook 증기 요리사 | barracks | greenhouse |
| hub-3 | ironworker 철공 | iron_depot | toolshop |
| hub-3 | smelter 제련공 | blast_furnace | iron_depot |
| hub-3 | night_watch 야간 경비 | barracks | lamp_street |
| hub-3 | riveter 리벳공 | iron_depot | repair_bay |
| hub-3 | foundry_hand 주조공 | blast_furnace | grand_cogworks |
| hub-3 | chief_engineer 수석 기술자 | grand_cogworks | power_house |
| hub-3 | boilermaker 보일러 제작공 | boiler_room | engine_room |
| hub-3 | scrap_dealer 고철상 | coal_yard | cobble_plaza |
| hub-4 | tower_keeper 급수탑지기 | water_tower | cistern |
| hub-4 | patrol 순찰대원 | lamp_road | barracks |
| hub-4 | repairer 정비사 | repair_bay | toolshop |
| hub-4 | electrician 전기 기사 | power_house | telegraph_line |
| hub-4 | lineman 전신 가설공 | telegraph_line | power_house |
| hub-4 | pump_operator 양수기 기사 | cistern | water_tower |
| hub-4 | valve_keeper 밸브 관리인 | pipe_gallery | grand_cogworks |

NpcPool.json에는 23:07:57(로컬)에 썼다. 텍스트를 다시 읽은 뒤 `_teachesTypeId` 키만 각 NPC의 `_habitatTypeId` 뒤에 끼워 넣었고, 나머지 키(아트의 `_portrait` 포함)는 그대로 두었다.

## 도달 가능성 로그 (`IronRing.Npc.Editor.NpcPoolValidate.Run`)

```
NPC POOL: lamp_street (start) hubs=hub-1,hub-2,hub-3 by=want:lamplighter teach:glass_blower want:gas_fitter teach:night_watch
NPC POOL: coal_yard (start) hubs=hub-1,hub-2,hub-3 by=teach:stoker want:coal_merchant want:scrap_dealer
NPC POOL: cobble_plaza hubs=hub-1,hub-3 by=want:peddler teach:scrap_dealer
NPC POOL: grand_cogworks hubs=hub-1,hub-3,hub-4 by=teach:clockmaker teach:foundry_hand want:chief_engineer teach:valve_keeper
NPC POOL: boiler_room (start) hubs=hub-1,hub-2,hub-3,hub-4 by=want:stoker want:chimney_sweep teach:gas_fitter want:boilermaker
NPC POOL: pipe_gallery hubs=hub-1,hub-2,hub-4 by=teach:water_keeper want:pipefitter want:valve_keeper
NPC POOL: cogworks hubs=hub-1,hub-2 by=teach:tinker want:clockmaker want:mechanic teach:brass_polisher
NPC POOL: iron_depot hubs=hub-1,hub-2,hub-3,hub-4 by=teach:chimney_sweep teach:dirigible_rigger want:ironworker teach:smelter want:riveter
NPC POOL: cistern hubs=hub-1,hub-2,hub-4 by=want:water_keeper teach:pipefitter teach:tower_keeper want:pump_operator
NPC POOL: greenhouse hubs=hub-1,hub-2 by=want:gardener want:glass_blower teach:steam_cook
NPC POOL: barracks hubs=hub-1,hub-2,hub-3,hub-4 by=teach:peddler want:steam_cook want:night_watch teach:patrol
NPC POOL: toolshop hubs=hub-1,hub-2,hub-3,hub-4 by=want:tinker want:brass_polisher teach:ironworker teach:repairer
NPC POOL: telegraph_line hubs=hub-2,hub-4 by=want:telegrapher teach:signal_keeper teach:electrician want:lineman
NPC POOL: water_tower hubs=hub-1,hub-2,hub-4 by=teach:gardener teach:engine_driver want:tower_keeper teach:pump_operator
NPC POOL: lamp_road hubs=hub-1,hub-2,hub-4 by=teach:lamplighter want:lamp_oiler want:signal_keeper want:patrol
NPC POOL: engine_room hubs=hub-1,hub-2,hub-3 by=teach:lamp_oiler want:engine_driver teach:boilermaker
NPC POOL: power_house hubs=hub-2,hub-3,hub-4 by=teach:telegrapher teach:chief_engineer want:electrician teach:lineman
NPC POOL: repair_bay hubs=hub-2,hub-3,hub-4 by=teach:mechanic want:dirigible_rigger teach:riveter want:repairer
NPC POOL: blast_furnace hubs=hub-2,hub-3 by=teach:coal_merchant want:smelter want:foundry_hand
NPC POOL: all 19 types reachable (npcs=35, warnings=0)
```

19종 모두 두 곳 이상의 거점 NPC로 열 수 있다("one hub only" 줄 없음). 가르치는 종류가 자기 서식지와 같거나 카탈로그에 없는 경우를 세는 경고는 0건이다.

## 브리지 결과

| 명령 | 결과 |
|---|---|
| refresh (1차) | 실패. 대화 UI 담당 파일 `NpcDialogScreen.cs`의 `InteriorNpcWalker.SetTalking` 없음(CS0117)이며 030 파일 오류는 아니다. 1분 뒤 재시도. |
| refresh (재시도) | ok, errors 없음 |
| execute-method `IronRing.Npc.Editor.NpcPoolValidate.Run` | ok (위 로그) |
| execute-method `IronRing.Hud.Editor.BuildMainCanvas.Generate` (표식 Codex·CodexCard·Passengers·PassengerRow) | ok, 표식 소비됨, 프리팹에 `Teacher`·`Wants` 노드 생성 |
| compile-status | ok, errors 없음 |

## 캡처 플래그 (코디네이터 실행)

1. `--codex-demo --codex-stage npc`: 데이터 `CodexDemo.json`의 `_npcId: "gardener"`(원예사)를 강제로 태운다. 그러면 온실이 board로 열리고 `CODEX TOAST: 탑승: 원예사이 온실 설계도를 건넸다`가 찍힌다. 이어 hub-1에 합류시키면 급수탑이 teach로 열리고 `CODEX TOAST: 원예사이 급수탑 설계도를 가르쳐 줬다`가 찍힌 뒤 도감이 열린다. 예상 로그는 `CODEX DEMO: 탑승 gardener = True, 해금 3 → 4/19`, `CODEX DEMO: 합류 gardener → hub-1 = True, 해금 4 → 5/19`이고, 종류별로 `출처=… npc=…` 줄이 나온다. 스크린샷 2장(토스트, 도감 카드의 "가르쳐 주는 이: 원예사")을 찍는다.
2. `--result-demo --result-stage win`: 거점 4곳을 ApplyDelta로 정착시킨다. 네 번째 거점이 정착하는 순간 `RUN END: outcome=Victory, settled=4/4, laps=0, reason=None, early=True`가 찍히고, 이어 `RESULT DEMO: over=True, outcome=Victory, … laps=0, early=True, lapsLeft=8`이 나온다. 바퀴를 쓰지 않고 결과 화면 "승리"가 뜬다.

## 남은 문제

- 데모의 합류는 `NpcRecruiter.TryJoin`(실제 하차와 같은 합류 경로라 `NpcJoined`가 발생한다)을 직접 부른다. 원하는 서식지(온실) 완성과 거점 정차를 통한 실제 하차 흐름은 거치지 않는다. 정차 하차 흐름으로 바꾸려면 온실 배치 데이터가 필요하다.
- 스토리는 도감이 "4→6/19"가 되기를 기대하지만, 시작 레시피가 3개라 실제로는 3→5/19다(탑승 +1, 합류 +1).
- 토스트 문구는 스펙의 "<직업>이" 그대로라서 받침 없는 직업명에서는 "원예사이"처럼 조사가 어색하다. 데이터(ko.json)만 고치면 되며 디자이너 결정이 필요하다.
- 승객이 이미 탄 상태에서 원하는 종류가 시작 레시피면 board 해금은 없다(의도대로).
- 자동 테스트는 없다(브리프 지시상 생략). 스크린샷은 코디네이터 캡처를 기다린다.
- `BuildMainCanvas.Generate` 뒤 `NpcDialog.prefab`도 수정 상태로 보인다. 대화 UI 에이전트의 작업이거나 전체 생성의 부산물이며, 030은 그 파일을 직접 고치지 않았다.
