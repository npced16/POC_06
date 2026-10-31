# Story 004: 거점 성장·쇠락·폐허 전환과 패배 판정

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 6 — 거점별 고유 성장 방식과 번성/쇠락 수치, 쇠락 시 폐허 전환 (방문 가능, 폐허 비주얼), 네 곳 모두 폐허면 패배

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state), scoped to this story:*

- [ ] 각 거점은 번성/쇠락 수치를 가지며, 초기값과 한계값은 거점별 설정에서 읽는다.
- [ ] 네 거점은 서로 다른 성장 방식을 가진다. 성장 방식은 거점 설정 데이터로 정의되며 코드 분기로 하드코딩하지 않는다.
- [ ] 번성/쇠락 수치는 소속 NPC, 지어진 시설, 열차가 한 바퀴 도는 시간 경과에 따라 변한다.
- [ ] 수치가 쇠락 한계에 도달하면 거점은 폐허 상태로 전환된다.
- [ ] 폐허 거점도 선로에 그대로 남고, 열차는 그 거점에 계속 정차할 수 있다. 선로는 끊기지 않는다.
- [ ] 폐허 거점은 폐허 형태를 눈으로 알 수 있게 표시된다 (스크린샷으로 보존).
- [ ] 네 거점이 모두 폐허가 되면 런이 끝나고 패배 상태가 된다. 하나라도 남아 있으면 런은 계속된다.
- [ ] 거점이 추가되어도 패배 판정은 "모든 거점이 폐허"라는 규칙 그대로 동작한다 (4에 고정하지 않는다).
- [ ] 거점 상태 변화와 패배 시점에 이벤트가 발생한다 (HUD 구독용).

---

## Implementation Notes

- 열린 질문 (구현 전 사용자 확인 필요): 폐허가 된 거점을 다시 되살릴 수 있는지는 브리프에 정해져 있지 않다. 되살림 규칙은 이 스토리의 범위에 넣지 않는다.
- 열린 질문: 거점 네 곳의 구체적인 성장 방식 내용(자원 종류, 수치 변화 규칙)은 정해지지 않았다. 구현 시작 전에 `/quick-design`으로 한 장 사양을 받는 것을 권장한다.
- 수치와 한계값은 모두 데이터이며 임의로 넣지 않는다.

---

## Out of Scope

- Story 003: NPC 합류 판정
- Story 005: 전투로 인한 거점 피해 (이 스토리는 수치 갱신 인터페이스만 제공)
- Story 007: 수치의 HUD 표시

---

## Files

- `Assets/Scripts/Hub/HubState.cs`
- `Assets/Scripts/Hub/HubDefinition.cs`
- `Assets/Scripts/Hub/RunState.cs`
- `Assets/Tests/EditMode/Hub/HubStateTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*At `qa.level: minimal` tests are waived (advisory), but a retained screenshot of player-observable behaviour is not.*

**Story Type**: Logic
**Required evidence**:
- 단위 테스트: `Assets/Tests/EditMode/Hub/` (권장, advisory)
- 폐허 거점과 패배 화면 스크린샷: `production/qa/evidence/` (필수)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-001-ring-track-train-movement.md`, `production/epics/iron-ring-train/story-002-habitat-combo-flag.md`, `production/epics/iron-ring-train/story-003-npc-pool-join.md`
- Unlocks: `production/epics/iron-ring-train/story-006-defense-facilities.md`, `production/epics/iron-ring-train/story-007-minimal-hud.md`
