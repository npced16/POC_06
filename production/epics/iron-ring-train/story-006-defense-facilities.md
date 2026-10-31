# Story 006: 방어시설 건설

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 5 — 방어시설 건설 2~3종

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Core loop step 2 and 4), scoped to this story:*

- [ ] 방어시설은 2~3종이며, 종류별 비용과 효과는 외부 데이터에서 읽는다.
- [ ] 정차한 거점에서 자원을 소모해 시설을 지을 수 있다. 자원이 부족하면 건설이 거부되고 상태가 변하지 않는다.
- [ ] 거점마다 지을 수 있는 시설 수의 한도가 설정에 있고, 한도를 넘으면 거부된다.
- [ ] 지어진 시설은 해당 거점의 번성/쇠락 수치(Story 004)와 전투 결과(Story 005)에 설정된 만큼 영향을 준다.
- [ ] 폐허 거점에서 건설할 수 있는지 여부는 설정으로 정하고, 정해진 대로 동작한다.
- [ ] 지어진 시설이 거점 화면에 보인다 (스크린샷으로 보존).
- [ ] 시설 건설 시 이벤트가 발생한다.

---

## Implementation Notes

- 열린 질문 (구현 전 사용자 확인 필요): 시설의 구체적인 종류와 효과(예: 방벽, 포탑, 수리소)는 정해지지 않았다. 폐허 거점에서 건설 가능 여부도 정해지지 않았다. `/quick-design`으로 확인한다.

---

## Out of Scope

- Story 004: 번성/쇠락 수치 자체의 규칙
- Story 007: 건설 메뉴의 다듬기

---

## Files

- `Assets/Scripts/Defense/FacilityDefinition.cs`
- `Assets/Scripts/Defense/FacilityBuilder.cs`
- `Assets/Tests/EditMode/Defense/FacilityBuilderTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*At `qa.level: minimal` tests are waived (advisory), but a retained screenshot of player-observable behaviour is not.*

**Story Type**: Integration
**Required evidence**:
- 통합 테스트 `Assets/Tests/EditMode/Defense/` 또는 문서화된 플레이테스트 (권장, advisory)
- 시설이 지어진 거점 스크린샷: `production/qa/evidence/` (필수)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-004-hub-growth-ruin-defeat.md`, `production/epics/iron-ring-train/story-005-waypoint-event-combat.md`
- Unlocks: `production/epics/iron-ring-train/story-007-minimal-hud.md`
