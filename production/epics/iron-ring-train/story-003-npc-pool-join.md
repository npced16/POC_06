# Story 003: 고유 NPC 풀과 확률 합류

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 4 — 고유 NPC 풀과 확률 합류 (한 NPC는 한 번만 등장)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Core loop step 3), scoped to this story:*

- [x] NPC 풀(각 NPC의 식별자, 이름, 한 줄 대사, 합류 가능한 서식지 조건)은 외부 데이터에서 읽는다.
- [x] 이벤트 플래그가 켜진 거점에 열차가 다음에 도착하면, 설정된 확률로 풀에서 NPC 한 명이 선택된다.
- [x] 확률 판정에 쓰는 난수 공급원은 주입할 수 있어, 테스트에서 같은 입력이 같은 결과를 낸다.
- [x] 한 번 합류한 NPC는 어떤 거점에서도 다시 나오지 않는다 (고유).
- [x] 풀이 소진되었거나 조건에 맞는 NPC가 없으면 합류가 일어나지 않고 오류도 없다.
- [x] 합류한 NPC는 그 거점 소속으로 기록되고, 질의할 수 있으며, 합류 시 이벤트가 발생한다.
- [x] 합류 판정이 한 방문에 한 번만 실행된다 (같은 방문에서 중복 합류 없음).
- [x] 합류한 NPC의 이름과 한 줄 대사가 화면에 표시된다 (스크린샷으로 보존).

---

## Implementation Notes

- NPC 대화 트리는 만들지 않는다. 한 줄 대사만 쓴다.
- 확률 값과 NPC 목록은 모두 데이터이며, 코드에 넣지 않는다.
- 열린 질문: 합류 NPC가 거점 번성도에 주는 효과의 크기는 Story 004가 정한다. 이 스토리는 소속 기록까지만 한다.

---

## Out of Scope

- Story 002: 플래그를 켜는 일
- Story 004: NPC가 거점 상태에 미치는 영향

---

## Files

- `Assets/Scripts/Npc/Data/NpcDemoVisit.json`
- `Assets/Scripts/Npc/Data/NpcPool.json`
- `Assets/Scripts/Npc/Demo/NpcDemoView.cs`
- `Assets/Scripts/Npc/Editor/Story003Build.cs`
- `Assets/Scripts/Npc/NpcDefinition.cs`
- `Assets/Scripts/Npc/NpcPoolData.cs`
- `Assets/Scripts/Npc/NpcRecruiter.cs`
- `Assets/Tests/EditMode/Npc/Editor/NpcRecruiterTests.cs`
- `Assets/Scenes/Demo/Story003/NpcJoinDemo.unity`
- `production/qa/evidence/story-003-npc-pool-join/`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*At `qa.level: minimal` tests are waived (advisory), but a retained screenshot of player-observable behaviour is not.*

**Story Type**: Logic
**Required evidence**:
- 단위 테스트: `Assets/Tests/EditMode/Npc/` (권장, advisory)
- NPC 합류 화면 스크린샷: `production/qa/evidence/` (필수)

**Status**: Complete. NPC EditMode tests 11/11 passed; final demo build and capture exited 0; screenshot visually inspected at 1280x720.

- XML: `production/qa/evidence/story-003-npc-pool-join/editmode-all.xml`
- Log: `production/qa/evidence/story-003-npc-pool-join/editmode-all.log`
- Command: project.yaml test command with `-testFilter IronRing`, executed by coordinator under external Unity mutex.
- Cases: probability boundaries, flag/habitat gating, deterministic selection, global uniqueness, exhaustion, affiliation/event ordering, reentrant visit protection, JSON fields, duplicate ids.
- Contract: each `OnHubVisited` call is a new visit; integration must call it once per arrival as documented in INpcRecruiter.
- Workflow: minimal tier; no ADR or manifest; docs/README.md absent. Explicit dispatch requires tests and authorizes interface fakes while Story 002 proceeds in parallel.

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-002-habitat-combo-flag.md`
- Unlocks: `production/epics/iron-ring-train/story-004-hub-growth-ruin-defeat.md`

## Completion Notes

- Provisional data defaults: join chance 0.5, eight unique NPCs, two per hub-1 through hub-4, complete habitat required, one quote each. No prosperity effects or dialogue tree.
- Final build: `production/qa/evidence/story-003-npc-pool-join/build-2.log` (exit 0).
- Final player: `production/qa/evidence/story-003-npc-pool-join/player-2.log` (exit 0, coordinator-run windowed player).
- Screenshot: `production/qa/evidence/story-003-npc-pool-join/01-npc-joined.png` (1280x720; Ada Brass, full quote with corrected curly delimiters, JOINED HUB-1; no clipping).
- Detailed evidence: `production/qa/evidence/story-003-npc-pool-join/evidence.md`.
- Content receipt: `production/qa/evidence/story-003-npc-pool-join/stamp.txt` (Python-computed Git blob SHA-1 format, without invoking Git CLI).
- Tests establish isolated logic with interface fakes; the observed standalone demo establishes display behavior. Wiring into the later production composition root remains the coordinator integration pass, outside this story.

Run result: OBSERVED - Ada Brass, the complete arrival quote and JOINED HUB-1 render clearly with correct punctuation in `production/qa/evidence/story-003-npc-pool-join/01-npc-joined.png`.
