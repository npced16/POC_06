# Story 007: 최소 HUD

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 7 — 최소한의 HUD (자원, 거점 상태, 현재 위치)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP item 7, Art & audio direction), scoped to this story:*

- [ ] 보유 자원의 종류별 수량이 화면에 항상 보인다.
- [ ] 네 거점 각각의 상태(번성/쇠락 수치, 폐허 여부)가 보인다. 거점이 추가되면 코드 수정 없이 항목이 늘어난다.
- [ ] 열차의 현재 위치(노드)와 현재 바퀴 수가 보인다.
- [ ] HUD 값은 각 시스템의 이벤트를 구독해 갱신되며, 시스템 상태를 직접 바꾸지 않는다.
- [ ] 패배(모든 거점 폐허) 시 패배 화면 또는 문구가 표시된다.
- [ ] 1280x720에서 모든 HUD 요소가 겹치지 않고 읽힌다 (스크린샷으로 보존).
- [ ] 마우스로 거점 선택과 배치·건설 조작이 가능하다 (주 입력 키보드/마우스).

---

## Implementation Notes

- UI 구현 방식(UI Toolkit 또는 UGUI)은 Unity 6.3 참조 문서(`docs/engine-reference/unity/`)에서 확인하고, 선택 이유를 스토리 완료 시 기록한다.
- 아트 방향은 황동과 증기 톤이다. 이 스토리에서는 읽기 쉬움이 우선이고 장식은 나중으로 미룬다.

---

## Out of Scope

- 설정 메뉴, 일시정지 메뉴, 타이틀 화면
- 게임패드 내비게이션 (후속 작업)

---

## Files

- `Assets/Scripts/UI/HudController.cs`
- `Assets/Scripts/UI/HubStatusView.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*At `qa.level: minimal` tests are waived (advisory), but a UI story's retained screenshot is not.*

**Story Type**: UI
**Required evidence**:
- 화면별 스크린샷(평상시, 폐허 거점 포함, 패배 화면): `production/qa/evidence/` (필수)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-004-hub-growth-ruin-defeat.md`, `production/epics/iron-ring-train/story-006-defense-facilities.md`
- Unlocks: None
