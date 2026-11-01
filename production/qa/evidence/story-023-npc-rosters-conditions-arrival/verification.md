# 스토리 023 검증: 열차 승객, 거점별 명단, 숨은 조건, 도착 연출

> 스토리: `production/epics/iron-ring-train/story-023-npc-rosters-conditions-arrival.md`
> 상태: 구현 적용, 컴파일 통과(브리지 `refresh` `failed:false`). 스크린샷은 코디네이터가 찍는 중이며, 이 폴더에 들어오면 아래 "증거" 표를 채운다.

## 1. 요약

- NPC는 열차 승객이다. 경유지에서 이벤트나 전투가 끝나면(승리 포함, 패배 제외) 각자 확률과 탑승 조건으로 탄다. 탈 때 제안과 힌트를 토스트로 알린다.
- 승객은 거점에 정차할 때 내린다. 그 거점이 자기 거점 목록에 있고, 원하는 서식지가 완성돼 있고, 숨은 조건을 모두 채워야 한다. 내린 승객은 그 거점에 합류한다. 합류는 이 경로로만 일어난다.
- 인내 바퀴가 지나도 집을 못 찾은 승객은 다음 경유지에서 떠나고 다시 후보가 된다. 한 NPC는 런에 한 번만 합류한다.
- 명단은 35명이고 거점마다 다르다. 서식지 19종마다 최소 한 명이 있다. 두 거점에 오는 사람은 4명(peddler, chimney_sweep, coal_merchant, dirigible_rigger)뿐이다.
- 정착 규칙: `_settleByHabitat`(기본 false). false이면 번영이 `_settledThreshold`(40) 이상일 때만 정착한다.

## 2. 규칙

| 항목 | 규칙 | 데이터 |
|---|---|---|
| 탑승 시점 | `IWaypointResolver.EncounterEnded`가 EventResolved나 Victory일 때(021 도감과 같은 신호). Defeat이면 타지 않는다. | — |
| 탑승 후보 | 아직 합류하지 않았고 타고 있지도 않으며, `_origin`이 이 경유지 id이거나 `"*"`이고, `_boardConditions`를 모두 채운 NPC. 조건은 그 NPC의 첫 거점 기준으로 판정한다. | `NpcPool.json` |
| 탑승 굴림 | 풀 순서대로 후보마다 `_chance`를 굴려, 성공한 사람을 빈 자리만큼 태운다. 시드 고정(`System.Random(_passengerSeed)`). | `_maxPassengers` 3, `_passengerSeed` 2311 |
| 제안과 힌트 | 탈 때 토스트: `{0}이 {1}에서 탔다: "{2}" {3}` (직업, 출신지, 제안, 힌트) | `_boardToastFormat` |
| 하차 | 거점에 정차하면(`INpcRecruiter.OnHubVisited` → `Passengers`) 탑승 순서대로 판정한다. `_hubIds`에 거점이 있고, `_habitatTypeId`가 그 거점에 완성돼 있고, `_conditions`를 모두 채우면 내려서 합류한다. 한 번에 여러 명 내릴 수 있다. | `_maxDisembarkPerVisit` 0(0이면 제한 없음) |
| 하차 토스트 | `열차에서 {0}이 내렸다 · {1} 출신`. 여러 명이면 `_toastSeconds`(4.0초) 간격으로 차례로 띄운다. | `PassengerAnnouncer` |
| 인내 | 조우가 끝날 때마다(패배 포함) `Lap - 탑승 바퀴 ≥ _patienceLaps`인 승객이 먼저 내려 떠난다. 토스트는 `{0}이 열차에서 내려 떠났다`. | `_patienceLaps` 3(0이면 무한) |
| 한 번만 합류 | `NpcRecruiter.TryJoin`이 이미 합류한 id를 거절한다. 떠난 승객은 다시 후보가 된다. | — |
| 숨은 조건 | `prosperityAtLeast`(그 거점 번영), `lapAtLeast`(`ITrackRuntime.Lap`), `visitAtLeast`(그 거점 방문 수, 이번 정차 포함), `habitatComplete`(그 거점에 다른 종류 완성), `npcPresent`(그 거점에 그 NPC 거주), `phaseAtLeast`(폐허·붕괴 0 < 복구 중 1 < 정착 2). 모르는 종류나 읽을 수 없는 값은 경고를 한 번 남기고 불충족으로 본다. 플레이어에게는 어디에도 표시하지 않는다. | `NpcConditions.cs` |
| 도착 연출 | `INpcRecruiter.NpcArrived`(NpcJoined 직후)가 오면, 열린 내부 화면이 그 거점일 때 NPC를 `TrainAnchor`에 세우고 가장 가까운 집 칸까지 `_arrivalSpeed`로 걷게 한다. 여러 명이면 `_arrivalStagger` 간격으로 출발하고, 도착하면 `_arrivalPause`만큼 쉰 뒤 평소처럼 배회한다. 내부 화면이 그 거점이 아니면 예전처럼 집에 바로 놓는다. | `hub_phase_view.json` 0.9 / 0.8 / 1.0 |
| 정착 | `settles = 번영 ≥ _settledThreshold || (_settleByHabitat && 서식지 완성)`. 4개 거점 모두 false. | `HubProfiles.json` |
| 이름과 대사 | 새 NPC의 이름, 대사, 제안, 힌트, 출신지는 `NpcPool.json`에 있다(`ko.json`은 수정하지 않음). | — |

## 3. 거점별 명단

조건 약어: 번영≥N, 바퀴≥N, 방문≥N, 완성:서식지, 거주:npc id, 단계≥단계. "떠돌이 선로"(`*`)는 어느 경유지에서나 탄다는 뜻이다.

### hub-1 — 10명 (이 거점 전용 8명)

| id | 직업 | 서식지 | 출신지(_origin) | 확률 | 탑승 조건 | 숨은 조건 | 다른 거점 | 제안 / 힌트 |
|---|---|---|---|---|---|---|---|---|
| `lamplighter` | 점등부 | 가스등 거리 | 떠돌이 선로 (`*`) | 0.6 | 없음 | 없음 | - | 가스등 거리를 만들어 주면 거기서 살겠소. / 불빛만 넉넉하면 어느 마을이든 좋아요. |
| `gardener` | 원예사 | 온실 | 떠돌이 선로 (`*`) | 0.55 | 없음 | 없음 | - | 온실을 지어 주면 거기 눌러앉겠어요. / 유리 지붕 아래 흙만 있으면 돼요. |
| `stoker` | 화부 | 보일러실 | 북쪽 고개 (`NORTH PASS`) | 0.5 | 없음 | 번영≥10 | - | 보일러실을 갖춰 주면 불을 맡겠소. / 굴뚝 연기가 좀 오르는, 번영이 조금은 있는 마을이면 좋겠소. |
| `water_keeper` | 급수원 | 저수조 | 서쪽 고개 (`WEST PASS`) | 0.45 | 없음 | 완성:온실 | - | 저수조를 마련해 주면 물을 지키겠소. / 온실이 있는 마을이면 좋겠어. |
| `tinker` | 수리공 | 공구점 | 떠돌이 선로 (`*`) | 0.5 | 없음 | 없음 | - | 공구점을 차려 주면 거기서 일하겠네. / 잡동사니 굴러다니는 곳이면 어디든 좋네. |
| `peddler` | 행상 | 벽돌 광장 | 떠돌이 선로 (`*`) | 0.4 | 없음 | 방문≥2 | hub-3 | 벽돌 광장을 깔아 주면 좌판을 펴겠소. / 몇 번 들러 본 단골 마을이면 좋겠소. |
| `glass_blower` | 유리공 | 온실 | 남쪽 고개 (`SOUTH PASS`) | 0.3 | 바퀴≥1 | 거주:gardener | - | 온실을 하나 내주면 유리를 불어 드리지. / 원예사가 사는 마을이면 좋겠군. |
| `lamp_oiler` | 등유 배달부 | 가로등 길 | 동쪽 고개 (`EAST PASS`) | 0.35 | 없음 | 완성:가스등 거리 | - | 가로등 길을 놓아 주면 기름을 대겠소. / 가스등 거리가 이미 있는 마을이라면 좋겠어. |
| `chimney_sweep` | 굴뚝 청소부 | 보일러실 | 떠돌이 선로 (`*`) | 0.35 | 없음 | 단계≥Recovering | hub-4 | 보일러실 곁에 살게 해 주면 굴뚝을 쓸겠소. / 폐허 말고, 다시 숨 쉬기 시작한 마을이면 좋겠어. |
| `clockmaker` | 시계공 | 톱니 공방 | 북쪽 고개 (`NORTH PASS`) | 0.3 | 바퀴≥2 | 번영≥25 | - | 톱니 공방을 내주면 시계탑을 고치겠소. / 번영이 제법 오른 마을이면 좋겠소. |

### hub-2 — 10명 (이 거점 전용 8명)

| id | 직업 | 서식지 | 출신지(_origin) | 확률 | 탑승 조건 | 숨은 조건 | 다른 거점 | 제안 / 힌트 |
|---|---|---|---|---|---|---|---|---|
| `mechanic` | 기계공 | 톱니 공방 | 북쪽 고개 (`NORTH PASS`) | 0.5 | 없음 | 없음 | - | 톱니 공방을 지어 주면 거기 자리 잡을게요. / 기름 냄새 나는 곳이면 어디든 좋아요. |
| `pipefitter` | 배관공 | 증기관 회랑 | 떠돌이 선로 (`*`) | 0.5 | 없음 | 완성:보일러실 | - | 증기관 회랑을 놓아 주면 이음매를 맡겠소. / 보일러실이 있는 마을이면 좋겠어. |
| `coal_merchant` | 석탄 상인 | 석탄 야적장 | 동쪽 고개 (`EAST PASS`) | 0.45 | 없음 | 번영≥15 | hub-3 | 석탄 야적장을 만들어 주면 거기서 장사하겠소. / 돈이 좀 도는, 번영이 오른 마을이면 좋겠소. |
| `telegrapher` | 전신 기사 | 전신선 | 떠돌이 선로 (`*`) | 0.4 | 바퀴≥1 | 방문≥2 | - | 전신선을 이어 주면 소식을 전하겠어요. / 자주 오가는 마을이면 좋겠어요. |
| `gas_fitter` | 가스관 기술자 | 가스등 거리 | 북쪽 고개 (`NORTH PASS`) | 0.5 | 없음 | 없음 | - | 가스등 거리를 깔아 주면 가스관을 이어 드리지. / 가스관만 이어지면 어느 마을이든 좋소. |
| `engine_driver` | 기관사 | 증기 기관실 | 떠돌이 선로 (`*`) | 0.35 | 없음 | 완성:증기관 회랑 | - | 증기 기관실을 마련해 주면 기관을 몰겠소. / 증기관 회랑이 있는 마을이면 좋겠어. |
| `brass_polisher` | 황동 연마공 | 공구점 | 동쪽 고개 (`EAST PASS`) | 0.4 | 없음 | 거주:mechanic | - | 공구점을 내주면 황동을 반짝이게 닦겠소. / 기계공이 사는 마을이면 손이 잘 맞겠지. |
| `signal_keeper` | 신호수 | 가로등 길 | 떠돌이 선로 (`*`) | 0.35 | 없음 | 단계≥Recovering | - | 가로등 길을 놓아 주면 신호등을 지키겠소. / 다시 불이 들어오기 시작한 마을이면 좋겠어. |
| `dirigible_rigger` | 비행선 삭구공 | 수리고 | 북쪽 고개 (`NORTH PASS`) | 0.25 | 바퀴≥2 | 번영≥30 | hub-4 | 수리고를 지어 주면 비행선 밧줄을 손보겠소. / 번영이 꽤 오른 큰 마을이라면 좋겠소. |
| `steam_cook` | 증기 요리사 | 숙소 | 떠돌이 선로 (`*`) | 0.45 | 없음 | 방문≥3 | - | 숙소를 지어 주면 따뜻한 밥을 짓겠어요. / 자주 들르는 정든 마을이면 좋겠어요. |

### hub-3 — 10명 (이 거점 전용 8명)

| id | 직업 | 서식지 | 출신지(_origin) | 확률 | 탑승 조건 | 숨은 조건 | 다른 거점 | 제안 / 힌트 |
|---|---|---|---|---|---|---|---|---|
| `peddler` | 행상 | 벽돌 광장 | 떠돌이 선로 (`*`) | 0.4 | 없음 | 방문≥2 | hub-1 | 벽돌 광장을 깔아 주면 좌판을 펴겠소. / 몇 번 들러 본 단골 마을이면 좋겠소. |
| `coal_merchant` | 석탄 상인 | 석탄 야적장 | 동쪽 고개 (`EAST PASS`) | 0.45 | 없음 | 번영≥15 | hub-2 | 석탄 야적장을 만들어 주면 거기서 장사하겠소. / 돈이 좀 도는, 번영이 오른 마을이면 좋겠소. |
| `ironworker` | 철공 | 철골 창고 | 동쪽 고개 (`EAST PASS`) | 0.5 | 없음 | 없음 | - | 철골 창고를 세워 주면 거기서 일하겠소. / 쇠 두드리는 소리가 나는 곳이면 좋소. |
| `smelter` | 제련공 | 용광로 | 남쪽 고개 (`SOUTH PASS`) | 0.35 | 바퀴≥1 | 완성:석탄 야적장 | - | 용광로를 지어 주면 쇳물을 다루겠소. / 석탄 야적장이 있는 마을이면 좋겠소. |
| `night_watch` | 야간 경비 | 숙소 | 떠돌이 선로 (`*`) | 0.45 | 없음 | 번영≥10 | - | 숙소를 지어 주면 밤을 지키겠소. / 지킬 만한 것이 좀 있는, 번영이 오른 마을이면 좋겠소. |
| `riveter` | 리벳공 | 철골 창고 | 떠돌이 선로 (`*`) | 0.4 | 없음 | 거주:ironworker | - | 철골 창고를 내주면 리벳을 박겠소. / 철공이 사는 마을이면 좋겠어. |
| `foundry_hand` | 주조공 | 용광로 | 동쪽 고개 (`EAST PASS`) | 0.3 | 없음 | 단계≥Recovering | - | 용광로 곁에 살게 해 주면 거푸집을 짜겠소. / 주조소처럼 다시 불이 붙는 마을이라면 좋겠어. |
| `chief_engineer` | 수석 기술자 | 대톱니 공장 | 떠돌이 선로 (`*`) | 0.25 | 바퀴≥2 | 번영≥30, 완성:톱니 공방 | - | 대톱니 공장을 세워 주면 공장장을 맡겠소. / 톱니 공방이 있고 번영이 꽤 오른 마을이면 좋겠소. |
| `boilermaker` | 보일러 제작공 | 보일러실 | 남쪽 고개 (`SOUTH PASS`) | 0.4 | 없음 | 없음 | - | 보일러실을 내주면 새 보일러를 짜 드리지. / 증기만 돌면 어디든 좋소. |
| `scrap_dealer` | 고철상 | 석탄 야적장 | 떠돌이 선로 (`*`) | 0.45 | 없음 | 방문≥2 | - | 석탄 야적장 한쪽을 내주면 고철을 모으겠소. / 몇 번 들러 본 마을이면 좋겠소. |

### hub-4 — 9명 (이 거점 전용 7명)

| id | 직업 | 서식지 | 출신지(_origin) | 확률 | 탑승 조건 | 숨은 조건 | 다른 거점 | 제안 / 힌트 |
|---|---|---|---|---|---|---|---|---|
| `chimney_sweep` | 굴뚝 청소부 | 보일러실 | 떠돌이 선로 (`*`) | 0.35 | 없음 | 단계≥Recovering | hub-1 | 보일러실 곁에 살게 해 주면 굴뚝을 쓸겠소. / 폐허 말고, 다시 숨 쉬기 시작한 마을이면 좋겠어. |
| `dirigible_rigger` | 비행선 삭구공 | 수리고 | 북쪽 고개 (`NORTH PASS`) | 0.25 | 바퀴≥2 | 번영≥30 | hub-2 | 수리고를 지어 주면 비행선 밧줄을 손보겠소. / 번영이 꽤 오른 큰 마을이라면 좋겠소. |
| `tower_keeper` | 급수탑지기 | 급수탑 | 서쪽 고개 (`WEST PASS`) | 0.45 | 없음 | 완성:저수조 | - | 급수탑을 세워 주면 물을 대겠소. / 저수조가 있는 마을이면 좋겠어. |
| `patrol` | 순찰대원 | 가로등 길 | 떠돌이 선로 (`*`) | 0.45 | 없음 | 없음 | - | 가로등 길을 놓아 주면 순찰을 돌겠소. / 밤길이 환하면 어디든 좋소. |
| `repairer` | 정비사 | 수리고 | 남쪽 고개 (`SOUTH PASS`) | 0.45 | 없음 | 없음 | - | 수리고를 지어 주면 고장 난 건 다 고치겠소. / 고칠 게 많은 마을이면 더 좋소. |
| `electrician` | 전기 기사 | 발전소 | 서쪽 고개 (`WEST PASS`) | 0.3 | 바퀴≥1 | 완성:전신선 | - | 발전소를 지어 주면 전기를 돌리겠소. / 전신선이 이어진 마을이면 좋겠어. |
| `lineman` | 전신 가설공 | 전신선 | 떠돌이 선로 (`*`) | 0.4 | 없음 | 번영≥15 | - | 전신선을 이어 주면 전봇대를 세우겠소. / 번영이 좀 오른 마을이면 좋겠소. |
| `pump_operator` | 양수기 기사 | 저수조 | 서쪽 고개 (`WEST PASS`) | 0.4 | 없음 | 없음 | - | 저수조를 지어 주면 양수기를 돌리겠소. / 물만 고이면 어디든 좋소. |
| `valve_keeper` | 밸브 관리인 | 증기관 회랑 | 떠돌이 선로 (`*`) | 0.35 | 없음 | 거주:tower_keeper | - | 증기관 회랑을 놓아 주면 밸브를 지키겠소. / 급수탑지기가 사는 마을이면 좋겠어. |

## 4. 계약

- 새 계약 `IPassengers`: `PassengerIds`, `Proposal`, `Hint`, `OriginName`, `LapsAboard`, `Boarded(npcId, waypointId)`, `Disembarked(npcId, hubId)`, `Left(npcId)`, `TryBoard(npcId, waypointId, force)`.
- `INpcRecruiter`에 추가: `CandidatesOf(habitatTypeId)`, `JoinedOf(habitatTypeId)`, `NpcArrived(npcId, hubId)`.
- `INpcInteraction`에 추가: `Origin(npcId)`. 코디네이터 결정 1에 따라 속성이 아닌 메서드로 했다.
- 위 추가 멤버는 모두 C# 기본 인터페이스 구현이다. 다른 데모 구현체를 고치지 않아도 컴파일된다.
- `NpcInstaller.Passengers`(구체 타입)에 데모용 `BoardForHub(hubId, preferredId)`가 있다.

## 5. 바뀐 파일

| 구분 | 파일 |
|---|---|
| 새 파일 | `Assets/Scripts/Npc/NpcConditions.cs`, `Npc/Passengers.cs`, `Npc/PassengerAnnouncer.cs` |
| 교체 | `Npc/NpcDefinition.cs`, `Npc/NpcPoolData.cs`, `Npc/NpcRecruiter.cs`, `Npc/NpcInstaller.cs`, `Npc/Demo/NpcHomesDemoDriver.cs` |
| 수정 | `Contracts/IronRingContracts.cs`, `Npc/NpcInteraction.cs`, `Hub/HubDefinition.cs`, `Hub/HubData.cs`, `Hub/HubState.cs`, `Presentation/HubPhase/InteriorNpcWalker.cs`, `Presentation/HubPhase/HubPhaseViewSettings.cs`, `Habitats/Demo/HabitatDemoDriver.cs`, `Hub/Demo/PhaseDemoDriver.cs` |
| 데이터 | `Npc/Data/NpcPool.json`(35명과 승객 설정), `Hub/Data/HubProfiles.json`(`_settleByHabitat`), `Presentation/HubPhase/Resources/HubPhase/hub_phase_view.json`(도착 3값), `Npc/Resources/Npc/NpcHomesDemo.json`(arrival·hub2·board 설정) |

`ko.json`과 022 소유 파일(`Hud/**`, `Prefabs/UI/**`, `PlacementGhost.cs`, `StateCombatEffects.cs`, `RingHubPhaseView.cs`)은 수정하지 않았다.

## 6. 적용 방식과 컴파일

- 게임플레이 에이전트가 초안과 `apply.py`를 세션 스크래치패드에 만들었다. 기준 문자열을 먼저 모두 확인하고, 하나라도 어긋나면 아무것도 쓰지 않는 스크립트다. 실제 파일 사본에 미리 돌려 9개 수정, 8개 복사, JSON 3개가 적용되는 것을 확인했다.
- 코디네이터가 실제 리포에 `apply.py`를 실행했다. `NpcPool.json`은 같은 폴더의 `pool.py`가 생성한다. 기존 19명은 id, 이름, 서식지, 대사를 그대로 두었다.
- 브리지: `python -I tools/bridge/bridge.py --timeout 600 refresh` → `failed:false`(코디네이터 실행).
- 자동 테스트 없음(사용자 결정). 증거는 컴파일 확인과 스크린샷이다.

## 7. 데모 플래그와 기대 로그

| 플래그 | 하는 일 | 확인할 로그 |
|---|---|---|
| `--npc-demo --npc-stage board` | hub-1에서 출발한다. 경유지 조우를 `WaypointResolver.Advance()`로 끝내면 굴림으로 탑승한다. 아무도 안 타면 강제 탑승하고 그 사실을 로그에 남긴다. | `PASSENGER: board <id> at NORTH PASS …`, `PASSENGER: toast "…탔다: …"`, `NPC DEMO: board <id> at <wp> "<제안>" <힌트>` (강제였다면 `NPC DEMO: board 굴림에서 아무도 타지 않아 … 강제 탑승`) |
| `--npc-demo --npc-stage arrival` | hub-1 내부에 가스등 L자와 온실을 놓고, lamplighter를 태워 정차시킨다. 걸음 속도는 0.25로 느리게 해 걸어가는 중간을 캡처한다. | `PASSENGER: disembark lamplighter at hub-1`, `NPC ARRIVAL: lamplighter 열차 칸 → …`, `NPC DEMO: arrival hub-1 하차 lamplighter`, 토스트 "열차에서 점등부이 내렸다 · 떠돌이 선로 출신" |
| `--npc-demo --npc-stage hub2` | hub-2 내부를 열고 같은 가스등 거리(4칸)를 완성한 뒤, hub-2 명단의 gas_fitter를 태워 내리게 한다. | `NPC DEMO: hub2 hub-2 완성 서식지 lamp_street`, `PASSENGER: disembark gas_fitter at hub-2`, `NPC DEMO: hub2 hub-2 하차 gas_fitter — lamp_street 후보 전체 [lamplighter, gas_fitter], hub-1 쪽은 lamplighter` |
| `--npc-demo --npc-stage homes\|panel\|encouraged` | 예전과 같은 결과다. 대상(lamplighter, gardener)을 강제로 태운 뒤 정차시켜 하차와 합류로 이어진다. | `NPC DEMO: 승객 <id> 탑승 = True`, `NPC DEMO hub-1: 방문 1회, 합류 lamplighter, gardener`, `NPC DEMO: homes → …` |
| `--habitat-demo` | 서식지를 완성한 뒤 `BoardForHub`로 내릴 수 있는 승객을 태우고 방문시킨다. | `HABITAT DEMO hub-1: 승객 탑승 <id>`, `… NPC 합류 → <id>` |

## 8. 증거

| # | 내용 | 파일 | 상태 |
|---|---|---|---|
| 1 | 경유지 탑승 제안·힌트 토스트 | (코디네이터 캡처) | 대기 |
| 2 | 열차에서 내려 집으로 걸어가는 NPC | (코디네이터 캡처) | 대기 |
| 3 | hub-1(lamplighter)과 hub-2(gas_fitter)에 서로 다른 NPC | (코디네이터 캡처) | 대기 |
| 4 | 대화 패널 출신지, 도감 "???", 상단 바 승객 수 | — | 022 뒤 |

## 9. 남은 것과 위험

- **HUD 반영은 022 뒤:** 승객 패널, 상단 바 "승객 N", 대화 패널의 "<출신지> 출신" 줄(`INpcInteraction.Origin`), 도감 카드의 합류자 이름과 "???"(`CandidatesOf`/`JoinedOf`)는 아직 화면에 없다. 지금은 계약으로만 읽을 수 있다.
- **토스트 조사:** 문구가 데이터 그대로라 "점등부이"처럼 받침 없는 직업명에서 조사가 어색하다. 조사 처리(이/가)는 열린 과제다.
- **떠돌이 승객의 출신지:** `"*"` 출신은 출신지 이름이 "떠돌이 선로"로 고정이다. 실제로 탄 경유지 이름을 쓰려면 경유지 한국어 이름을 읽는 경로가 필요하다.
- **`--habitat-demo`:** 데모가 완성하는 서식지의 hub-1 후보가 모두 숨은 조건에 막히면 `BoardForHub`가 경고를 남기고 합류가 0명이 된다. 캡처 로그로 확인할 것.
- **`hub2`:** 정차하지 않은 hub-2 내부에 배치한다. 배치가 거절되면 `NPC DEMO: 배치 hub-2 … = False`와 경고 로그로 사유가 남는다.
- **방문 수:** 방문 수는 `Passengers`가 직접 센다. 런 시작 때 이미 정차해 있던 hub-1은 첫 `OnHubVisited` 호출부터 센다.

## 코디네이터 검수 (2026-11-01 21:05 KST)

- apply.py 적용 뒤 `refresh` failed:false. 조사 수정(`PassengerAnnouncer.Subject`: 받침 유무로 이/가) 뒤 다시 failed:false.
- 캡처: `023-board.png`(북쪽 고개 정차 화면, 토스트 큐 — 로그로 탑승 확인), `023-arrival.png`(열차 칸 옆에서 집으로 걷는 점등부), `023-hub2.png`(정원에서 가스관 기술자 탑승·하차 토스트 "가스관 기술자가 북쪽 고개에서 탔다: …"), `023-habitat-demo.png`(서식지 3종 + 내린 점등부). 로그: `PASSENGER: board gas_fitter`, `PASSENGER: disembark gas_fitter at hub-2`, `HABITAT DEMO hub-1: 승객 탑승 lamplighter`, `방문 1회째 NPC 합류 → lamplighter`.
- 관찰: hub2 캡처에서 가스등 4칸 중 3칸만 테두리가 그려짐(네 번째 칸이 마지막에 놓여 테두리 갱신 전 캡처된 것으로 보임, 판정은 완성). 화면 반영(승객 수·승객 패널·대화 출신지·도감 ???)은 022 뒤 후속.
