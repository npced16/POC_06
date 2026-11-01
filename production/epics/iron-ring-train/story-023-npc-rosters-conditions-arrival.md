# Story 023: 거점별 NPC 후보·숨은 조건·확률과 열차 도착 연출

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 4(고유 NPC 풀과 확률 합류). 사용자 지시(2026-11-01 20:45): "서식지별로 찾아오는 유형이 다른데 여러 명이 찾아올 수 있어. 확률은 각자 다르게, 숨겨진 조건으로. 그리고 어디에서 들어오는지도." + (20:50) "마을마다 찾아오는 사람이 다르도록." 정착 규칙은 사용자 답이 없어 코디네이터 제안(서식지 예외 제거, 번영 40으로만 정착)을 데이터 플래그로 넣고 기본값을 그렇게 둔다(되돌리기 쉬움).

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] NPC 데이터(`Assets/Scripts/Npc/Data/NpcPool.json`)를 약 35명으로 늘린다. NPC마다 `_id`, `_name`(한국어 직업명), `_habitatTypeId`(불러오는 서식지 종류), `_hubIds`(찾아오는 거점, 1~2곳 — 숨김), `_chance`(방문당 합류 확률 0~1, 각자 다름), `_conditions`(숨은 조건 목록, 아래 종류), `_origin`(출신지 한국어, 예: 북쪽 고개·동쪽 고개·다른 거점 이름), `_quote`·`_lines`(대사 3줄). 모두 스팀펑크 직업·말투. 거점 4곳의 명단이 서로 다르다: 거점마다 8~10명이고, 한 거점에서만 오는 사람이 과반이며, 두 거점에서 오는 사람은 소수다. 19종마다 어느 거점에서든 최소 한 명은 온다.
- [ ] 숨은 조건 종류(데이터): `prosperityAtLeast`(그 거점 번영), `lapAtLeast`(바퀴), `visitAtLeast`(그 거점 방문 횟수), `habitatComplete`(그 거점에 다른 종류 완성), `npcPresent`(그 거점에 특정 NPC 거주), `phaseAtLeast`(Recovering/Settled). 조건은 플레이어에게 어디에도 표시하지 않는다. 알 수 없는 조건 종류는 경고 로그 뒤 불충족으로 본다.
- [ ] 합류 판정(`NpcRecruiter`): 거점 H에 정차할 때 후보 = 아직 합류하지 않은 NPC 중 H가 `_hubIds`에 있고, `_habitatTypeId` 서식지가 H에 완성돼 있고, 조건을 모두 채운 사람. 후보마다 각자 `_chance`로 굴리고, 성공한 사람 중 데이터 `_maxJoinsPerVisit`(기본 1)만큼 합류시킨다(여럿이면 확률 여유가 큰 순). 한 NPC는 런에 한 번만 온다(기존 규칙). 결정적 시드 유지.
- [ ] 도착 연출: 합류한 NPC는 내부 화면의 열차 칸 위치에서 나타나 집(자기 서식지 칸)까지 걸어간 뒤(`InteriorNpcWalker` 도착 상태, 속도·대기는 데이터) 평소처럼 집 곁을 배회한다. 도착 순간 토스트 "열차에서 <직업>이 내렸다 · <출신지> 출신"(`ToastScreen.Show`, 022 틀). 같은 방문에 여러 명이면 차례로.
- [ ] 출신지 표시: 대화 패널의 집 줄 옆에 "<출신지> 출신"이 보인다(022의 NpcDialog 화면이 끝난 뒤 그 화면에 반영; 그전까지는 `INpcInteraction`에 `Origin` 문자열만 제공). 도감 카드의 "찾아오는 이"는 실제로 합류한 사람만 이름을 보이고 나머지 후보 수만큼 "???"를 보인다(마찬가지로 022 Codex 화면 뒤 반영; 계약 `INpcRecruiter.CandidatesOf(habitatTypeId)` 개수 + `JoinedOf` 제공).
- [ ] 정착 규칙(데이터): `HubProfiles.json`에 `_settleByHabitat`(기본 false)를 두고, false면 번영이 `_settledThreshold` 이상일 때만 정착한다. true면 지금처럼 완성 서식지 하나로도 정착한다. 브리프의 거점 단계 설명을 맞춘다.
- [ ] 데모 `--npc-demo --npc-stage arrival|hub2`: `arrival` = hub-1에서 합류 직후 열차에서 집으로 걸어가는 중간 프레임(캡처용으로 걸음 느리게), `hub2` = hub-2에 같은 서식지를 완성해 hub-1과 다른 사람이 오는 것을 보임. 기존 `homes|panel|encouraged` 단계는 그대로 동작.
- [ ] 스크린샷: (1) 열차에서 내려 걸어가는 NPC, (2) hub-1과 hub-2에 서로 다른 NPC(두 장 또는 한 장에 로그), (3) 대화 패널의 출신지 줄과 도감의 "???"(022 뒤). `production/qa/evidence/story-023-npc-rosters-conditions-arrival/` + verification.md.

---

## Implementation Notes

- 로직은 `Assets/Scripts/Npc/`(NpcDefinition·NpcPoolData·NpcRecruiter·NpcHomes·NpcInteraction·NpcInstaller). 조건 판정은 작은 정적 평가기(`NpcConditions.cs`)로 두고 서비스(`IHubStateSystem`, `ITrackRuntime`, `IHabitatClusters`, `INpcRecruiter`)만 읽는다.
- 계약(`IronRingContracts.cs`): `INpcRecruiter`에 `IReadOnlyList<string> CandidatesOf(string habitatTypeId)`(후보 id, 거점 무관)·`IReadOnlyList<string> JoinedOf(string habitatTypeId)`·`event Action<string, string> NpcArrived`(npcId, hubId — 합류와 같은 순간이지만 표현이 도착 연출에 쓰는 신호) 추가. `INpcInteraction`에 `string Origin` 추가.
- 표현: `Assets/Scripts/Presentation/HubPhase/InteriorNpcWalker.cs`·`NpcBody.cs`. 열차 칸 앵커는 `IInteriorAnchors`/`HubInteriorRoot`의 TrainAnchor를 쓴다. 도착 속도·대기는 `hub_phase_view.json`에 추가.
- 022(UGUI 전환)와 병렬: `Assets/Scripts/Hud/**`, `PlacementGhost.cs`, UXML/USS, `Assets/Prefabs/UI/**`는 손대지 않는다. 토스트는 `IronRing.Hud.Ui.ToastScreen.Show(text, seconds)`(없으면 false)만 호출. `ko.json`은 022 에이전트들도 끝에 키를 붙이므로, 새 NPC의 이름·대사·출신지는 `NpcPool.json` 안에 두고 코드가 ko 키가 없으면 데이터 값을 쓰게 한다(ko.json 수정 금지).
- 검증은 열린 편집기 브리지(`refresh`, `compile-status`). 캡처는 코디네이터.

---

## Out of Scope

- 경유지에서 승객을 태우는 방식(사용자가 고르지 않음), NPC 간 관계, 떠나는 NPC, 세이브.

---

## Files

- `Assets/Scripts/Contracts/IronRingContracts.cs`(INpcRecruiter·INpcInteraction 확장)
- `Assets/Scripts/Npc/NpcDefinition.cs`·`NpcPoolData.cs`·`NpcRecruiter.cs`·`NpcConditions.cs`(새)·`NpcInteraction.cs`·`NpcInstaller.cs`, `Data/NpcPool.json`(약 35명), `Data/NpcInteraction.json`, `Demo/NpcHomesDemoDriver.cs`, `Resources/Npc/NpcHomesDemo.json`(hub2·arrival 단계)
- `Assets/Scripts/Hub/HubState.cs`·`Data/HubProfiles.json`(`_settleByHabitat`)
- `Assets/Scripts/Presentation/HubPhase/InteriorNpcWalker.cs`·`NpcBody.cs`·`HubPhaseViewSettings.cs`·`Resources/HubPhase/hub_phase_view.json`(도착 속도·대기)
- 뒤에(022 완료 후): `NpcDialogScreen`(출신지 줄), `Codex` 카드(합류한 이름·"???")

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 3장: `production/qa/evidence/story-023-npc-rosters-conditions-arrival/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-017-npc-homes-dialogue.md`, `story-021-recipe-unlock-codex.md`
- Unlocks: None (022와 병렬, HUD 반영은 022 뒤)
