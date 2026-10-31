# Story 002: 서식지 조합 판정과 이벤트 플래그

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3 — 자원 배치로 서식지 조합을 완성하고 이벤트 플래그를 켜는 시스템

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Core loop step 2 and 3; reference game 포코피아), scoped to this story:*

- [ ] 거점에는 자원을 놓을 수 있는 배치 슬롯이 있고, 슬롯 수와 배치 가능한 자원 종류는 설정에서 읽는다.
- [ ] 정차한 거점에서 플레이어가 자원을 슬롯에 배치하거나 회수할 수 있고, 보유 자원이 그만큼 증감한다.
- [ ] 서식지 조합 정의(필요한 자원과 배치 패턴)는 외부 데이터이며, 거점마다 서로 다른 조합을 가질 수 있다.
- [ ] 배치가 바뀔 때마다 조합 완성 여부를 판정하고, 완성되면 해당 거점의 이벤트 플래그를 켠다.
- [ ] 같은 배치에서 판정을 여러 번 해도 결과가 같다 (결정적).
- [ ] 자원이 부족하면 배치가 거부되고 상태가 변하지 않는다.
- [ ] 조합 완성 여부와 플래그 상태를 질의할 수 있고, 플래그가 켜질 때 이벤트가 발생한다.
- [ ] 거점 화면에서 배치한 자원과 서식지 완성 상태가 눈으로 구분된다 (스크린샷으로 보존).

---

## Implementation Notes

- 조합 판정은 순수 C#으로 두고 데이터 에셋만 읽게 해 단위 테스트가 가능하게 한다.
- 열린 질문: 조합이 풀렸을 때(자원 회수 후) 플래그를 즉시 끌지, 이미 켜진 플래그를 유지할지는 브리프에 정해져 있지 않다. 구현 전에 사용자에게 확인한다.
- 이 스토리는 플래그를 켜는 데까지만 한다. NPC 선택은 Story 003이 다룬다.

---

## Out of Scope

- Story 003: 플래그를 읽고 NPC를 확률로 합류시키는 일
- Story 004: 거점의 번성/쇠락 수치

---

## Files

- `Assets/Scripts/Habitat/HabitatDefinition.cs`
- `Assets/Scripts/Habitat/HabitatEvaluator.cs`
- `Assets/Tests/EditMode/Habitat/HabitatEvaluatorTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*At `qa.level: minimal` tests are waived (advisory), but a retained screenshot of player-observable behaviour is not.*

**Story Type**: Logic
**Required evidence**:
- 단위 테스트: `Assets/Tests/EditMode/Habitat/` (권장, advisory)
- 서식지 배치 화면 스크린샷: `production/qa/evidence/` (필수)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-001-ring-track-train-movement.md`
- Unlocks: `production/epics/iron-ring-train/story-003-npc-pool-join.md`, `production/epics/iron-ring-train/story-004-hub-growth-ruin-defeat.md`
