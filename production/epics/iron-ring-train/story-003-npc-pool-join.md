# Story 003: 고유 NPC 풀과 확률 합류

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

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

- [ ] NPC 풀(각 NPC의 식별자, 이름, 한 줄 대사, 합류 가능한 서식지 조건)은 외부 데이터에서 읽는다.
- [ ] 이벤트 플래그가 켜진 거점에 열차가 다음에 도착하면, 설정된 확률로 풀에서 NPC 한 명이 선택된다.
- [ ] 확률 판정에 쓰는 난수 공급원은 주입할 수 있어, 테스트에서 같은 입력이 같은 결과를 낸다.
- [ ] 한 번 합류한 NPC는 어떤 거점에서도 다시 나오지 않는다 (고유).
- [ ] 풀이 소진되었거나 조건에 맞는 NPC가 없으면 합류가 일어나지 않고 오류도 없다.
- [ ] 합류한 NPC는 그 거점 소속으로 기록되고, 질의할 수 있으며, 합류 시 이벤트가 발생한다.
- [ ] 합류 판정이 한 방문에 한 번만 실행된다 (같은 방문에서 중복 합류 없음).
- [ ] 합류한 NPC의 이름과 한 줄 대사가 화면에 표시된다 (스크린샷으로 보존).

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

- `Assets/Scripts/Npc/NpcDefinition.cs`
- `Assets/Scripts/Npc/NpcRecruiter.cs`
- `Assets/Tests/EditMode/Npc/NpcRecruiterTests.cs`

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

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-002-habitat-combo-flag.md`
- Unlocks: `production/epics/iron-ring-train/story-004-hub-growth-ruin-defeat.md`
