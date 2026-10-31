# Story 005: 경유지 이벤트와 간단한 전투

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 2 — 경유지 이벤트 및 간단한 전투 1종

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Core loop step 1), scoped to this story:*

- [ ] 열차가 경유지에 도착하면 이벤트와 전투 중 하나가 발생한다. 발생 비율은 설정에서 읽는다.
- [ ] 이벤트는 자원 증감 같은 결과를 내며, 이벤트 정의는 외부 데이터이다.
- [ ] 전투는 한 가지 형태만 구현하고, 시작과 종료가 명확하며 승패 결과를 낸다.
- [ ] 전투에서 이기면 자원을 얻고, 지면 설정된 페널티를 받는다. 페널티 내용은 데이터이다.
- [ ] 전투 중에는 열차 이동이 멈추고, 종료 후 선로 순환이 이어진다.
- [ ] 이벤트와 전투의 난수 공급원은 주입할 수 있어 테스트가 결정적이다.
- [ ] 전투 중과 전투 결과가 화면에 보인다 (스크린샷으로 보존).
- [ ] 이 스토리의 전투는 Story 001의 경유지 훅, Story 004의 거점 수치 갱신 인터페이스와 실제로 연결되어 동작한다.

---

## Implementation Notes

- 열린 질문 (구현 전 사용자 확인 필요): 전투 방식(턴제, 실시간, 자동 해결), 적 종류, 승패 규칙은 브리프에 정해져 있지 않다. 구현 전에 `/quick-design`으로 한 장 사양을 받는 것을 권장한다.
- 전투는 MVP에서 한 종류뿐이다. 확장은 하지 않는다.

---

## Out of Scope

- Story 006: 방어시설이 전투에 주는 효과
- Story 007: 전투 UI의 다듬기

---

## Files

- `Assets/Scripts/Combat/WaypointResolver.cs`
- `Assets/Scripts/Combat/CombatEncounter.cs`
- `Assets/Tests/EditMode/Combat/WaypointCombatTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*At `qa.level: minimal` tests are waived (advisory), but a retained screenshot of player-observable behaviour is not.*

**Story Type**: Integration
**Required evidence**:
- 통합 테스트 `Assets/Tests/EditMode/Combat/` 또는 문서화된 플레이테스트 (권장, advisory)
- 전투 화면 스크린샷: `production/qa/evidence/` (필수)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-001-ring-track-train-movement.md`
- Unlocks: `production/epics/iron-ring-train/story-006-defense-facilities.md`
