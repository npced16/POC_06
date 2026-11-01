# Story 002: 서식지 조합 판정과 이벤트 플래그

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

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

- [x] 거점에는 자원을 놓을 수 있는 배치 슬롯이 있고, 슬롯 수와 배치 가능한 자원 종류는 설정에서 읽는다.
- [x] 정차한 거점에서 플레이어가 자원을 슬롯에 배치하거나 회수할 수 있고, 보유 자원이 그만큼 증감한다.
- [x] 서식지 조합 정의(필요한 자원과 배치 패턴)는 외부 데이터이며, 거점마다 서로 다른 조합을 가질 수 있다.
- [x] 배치가 바뀔 때마다 조합 완성 여부를 판정하고, 완성되면 해당 거점의 이벤트 플래그를 켠다.
- [x] 같은 배치에서 판정을 여러 번 해도 결과가 같다 (결정적).
- [x] 자원이 부족하면 배치가 거부되고 상태가 변하지 않는다.
- [x] 조합 완성 여부와 플래그 상태를 질의할 수 있고, 플래그가 켜질 때 이벤트가 발생한다.
- [x] 거점 화면에서 배치한 자원과 서식지 완성 상태가 눈으로 구분된다 (스크린샷으로 보존).

---

## Implementation Notes

- 조합 판정은 순수 C#으로 두고 데이터 에셋만 읽게 해 단위 테스트가 가능하게 한다.
- Dispatch에서 확정한 임시 기본값: 데이터의 `_flagSticky=true`에 따라 이미 켜진 플래그를 유지한다. 현재 조합 완성 여부는 회수 즉시 갱신하며, 플래그 상승 이벤트는 최초 완성 때 한 번만 발생한다.
- 이 스토리는 플래그를 켜는 데까지만 한다. NPC 선택은 Story 003이 다룬다.

---

## Out of Scope

- Story 003: 플래그를 읽고 NPC를 확률로 합류시키는 일
- Story 004: 거점의 번성/쇠락 수치

---

## Files

- `Assets/Scripts/Inventory/ResourceInventory.cs`
- `Assets/Scripts/Habitat/HabitatDefinition.cs`
- `Assets/Scripts/Habitat/HabitatEvaluator.cs`
- `Assets/Scripts/Habitat/HabitatConfiguration.cs`
- `Assets/Scripts/Habitat/Demo/HabitatDemoTrack.cs`
- `Assets/Scripts/Habitat/Demo/HabitatDemoView.cs`
- `Assets/Scripts/Habitat/Demo/Editor/Story002Builder.cs`
- `Assets/Scripts/Habitat/Resources/Story002/habitats.json`
- `Assets/Scripts/Habitat/Resources/Story002/HabitatDemo.uxml`
- `Assets/Scripts/Habitat/Resources/Story002/HabitatDemo.uss`
- `Assets/Tests/EditMode/Habitat/Editor/HabitatEvaluatorTests.cs`
- `Assets/Tests/EditMode/Inventory/Editor/ResourceInventoryTests.cs`
- `Assets/Scripts/ScreenshotOnArg.cs`
- `production/qa/evidence/story-002-habitat-combo-flag/verification.md`
- `Assets/Scenes/Demo/Story002/HabitatDemo.unity`
- `Assets/Scenes/Demo/Story002/HabitatPanel.asset`
- `production/qa/evidence/story-002-habitat-combo-flag/01-complete.png`
- `production/qa/evidence/story-002-habitat-combo-flag/editmode-all.xml`
- `production/qa/evidence/story-002-habitat-combo-flag/stamp.txt`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*At `qa.level: minimal` tests are waived (advisory), but a retained screenshot of player-observable behaviour is not.*

**Story Type**: Logic

**Verified evidence**: [verification report](../../qa/evidence/story-002-habitat-combo-flag/verification.md), [test results](../../qa/evidence/story-002-habitat-combo-flag/editmode-all.xml), [final screenshot](../../qa/evidence/story-002-habitat-combo-flag/01-complete.png), [build log](../../qa/evidence/story-002-habitat-combo-flag/build-r.log), [player log](../../qa/evidence/story-002-habitat-combo-flag/player-r-01-complete.log).

Run result: OBSERVED — hub-1의 coal / scrap / water / 빈 슬롯, 재고 5 / 5 / 5 / 6, HABITAT COMPLETE와 raised sticky 플래그를 1280x720 플레이어 화면에서 확인했다. 패널 넘침 수정 후 재캡처했으며 실제 마우스 입력 검증과는 구분한다.

**Required evidence**:
- 단위 테스트: `Assets/Tests/EditMode/Habitat/` (권장, advisory)
- 서식지 배치 화면 스크린샷: `production/qa/evidence/` (필수)

**Status**: [x] Verified — coordinator-run EditMode 12/12 (Habitat 8, Inventory 4; combined suite 67/67), successful player rebuild, retained and inspected 1280x720 screenshot.

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-001-ring-track-train-movement.md`
- Unlocks: `production/epics/iron-ring-train/story-003-npc-pool-join.md`, `production/epics/iron-ring-train/story-004-hub-growth-ruin-defeat.md`
