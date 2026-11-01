# Story 030: 조기 승리와 NPC를 따라가는 레시피 해금

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Player goal & fail state, Core loop 2~3단계. 사용자 지시(2026-11-01 23:05): "3. 조기 승리해도 돼. 4. 이제 NPC 따라서 해금되도록."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: LOW
**Engine Notes**: none.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 조기 승리: 네 거점이 모두 정착하는 순간 런이 끝나고 결과 화면 "승리"가 뜬다(기한을 기다리지 않음). 데이터 `_earlyWin:true`(`RunState` 설정). 결과 화면의 총 바퀴는 그때까지 바퀴.
- [ ] NPC 기반 해금: 레시피는 NPC를 통해서만 열린다. (a) 승객이 열차에 타면 그 사람이 원하는 서식지(`_habitatTypeId`)가 아직 잠겨 있으면 즉시 해금("탑승: <직업>이 <서식지> 설계도를 건넸다"), (b) 그 NPC가 거점에 내려 합류하면 데이터 `_teachesTypeId`(NPC마다 하나, 자기 서식지와 다른 종류, 거점 명단에 맞게 분배)를 해금("<직업>이 <서식지> 설계도를 가르쳐 줬다"). 바퀴·경유지 이벤트·전투 승리 해금은 끈다(`CodexUnlocks.json`의 `_perLap/_perEvent/_perVictory` = 0). 우연한 완성 "발견"은 유지. 시작 레시피 3개는 유지.
- [ ] 도감 카드에 "가르쳐 주는 이: <직업>"(해금된 경우만) 한 줄, 승객 화면 줄에 "원하는 것: <서식지>"가 보인다(화면 쪽은 029 대화 UI 작업과 겹치지 않는 Codex·Passengers 화면만).
- [ ] 19종이 모두 적어도 한 명의 NPC로 열릴 수 있는지 데이터로 검증(스크립트 로그).
- [ ] 데모 `--codex-demo --codex-stage npc`: 승객 한 명을 태워 설계도 해금 토스트, 내려서 가르침 해금 토스트, 도감 4→6/19. 스크린샷 2장. `production/qa/evidence/story-030-early-win-npc-unlocks/` + verification.md.

---

## Implementation Notes

- `Assets/Scripts/Hub/RunState.cs`(조기 승리), `Assets/Scripts/Habitats/HabitatCodex.cs`(해금 출처: `IPassengers.Boarded`/`INpcRecruiter.NpcJoined` 구독, 바퀴·이벤트 구독 제거 또는 데이터 0), `Resources/Habitats/CodexUnlocks.json`, `Assets/Scripts/Npc/Data/NpcPool.json`(`_teachesTypeId` 35명), `NpcDefinition/NpcPoolData`(필드), `Assets/Scripts/Hud/Codex/CodexScreen.cs`·`CodexCardView.cs`, `Assets/Scripts/Hud/Npc/PassengersScreen.cs`·`PassengerRowView.cs`(029가 손대지 않는 파일).
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 대화 UI(029), 레시피 힌트 공개.

---

## Files

- `Assets/Scripts/Hub/RunState.cs`·`Data/HubProfiles.json` 또는 런 설정, `Assets/Scripts/Habitats/HabitatCodex.cs`·`Resources/Habitats/CodexUnlocks.json`·`Demo/CodexDemoDriver.cs`, `Assets/Scripts/Npc/NpcDefinition.cs`·`NpcPoolData.cs`·`Data/NpcPool.json`, `Assets/Scripts/Hud/Codex/*.cs`·`Hud/Npc/PassengersScreen.cs`·`PassengerRowView.cs`, `ko.json`, `design/game-brief.md`(해금 문장)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 2장: `production/qa/evidence/story-030-early-win-npc-unlocks/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-028-run-ending-and-prosperity.md`, `story-023-npc-rosters-conditions-arrival.md`
- Unlocks: None
