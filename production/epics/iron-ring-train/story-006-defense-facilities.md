# Story 006: 방어시설 건설

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

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

- [x] 방어시설은 2~3종이며, 종류별 비용과 효과는 외부 데이터에서 읽는다.
- [x] 정차한 거점에서 자원을 소모해 시설을 지을 수 있다. 자원이 부족하면 건설이 거부되고 상태가 변하지 않는다.
- [x] 거점마다 지을 수 있는 시설 수의 한도가 설정에 있고, 한도를 넘으면 거부된다.
- [x] 지어진 시설은 해당 거점의 번성/쇠락 수치(Story 004)와 전투 결과(Story 005)에 설정된 만큼 영향을 준다.
- [x] 폐허 거점에서 건설할 수 있는지 여부는 설정으로 정하고, 정해진 대로 동작한다.
- [x] 지어진 시설이 거점 화면에 보인다 (스크린샷으로 보존).
- [x] 시설 건설 시 이벤트가 발생한다.

---

## Implementation Notes

- Dispatch-approved provisional data: wall costs 3 scrap and grants +3 combat; turret costs 4 scrap + 1 coal and grants +5 combat; workshop costs 3 scrap + 2 grain, grants +0 combat and is flagged as a prosperity facility. Workshop grants a provisional +2 prosperity once on construction. Limit is 3 per hub; ruined hubs cannot build. All values live in `Assets/Scripts/Defense/Data/Facilities.json`.
- `FacilityBuilder` implements `IFacilityBuilder` exactly and receives definitions, capacity, ruin policy, `IResourceInventory`, `IHubStateSystem` and `Func<string, bool> isDocked` through its constructor. Integration should supply a predicate that is true only for the currently docked hub. Story 005 consumes the hub's summed `CombatBonus`; Story 004 receives `ApplyDelta` on construction.
- Dependencies are being implemented in parallel under coordinator authorization; local interface fakes isolate this story. `docs/README.md` is absent; the supplied story and `/dev-story` workflow were used. No shared composition root, contracts, build settings or Git state were changed.

---

## Out of Scope

- Story 004: 번성/쇠락 수치 자체의 규칙
- Story 007: 건설 메뉴의 다듬기

---

## Files

- `Assets/Scripts/Defense/FacilityDefinition.cs`
- `Assets/Scripts/Defense/FacilityBuilder.cs`
- `Assets/Scripts/Defense/FacilityCatalog.cs`
- `Assets/Scripts/Defense/Data/Facilities.json`
- `Assets/Scripts/Defense/Demo/DefenseDemoFakes.cs`
- `Assets/Scripts/Defense/Demo/FacilityDemoView.cs`
- `Assets/Scripts/Defense/Editor/FacilityDemoBuild.cs`
- `Assets/Tests/EditMode/Defense/Editor/FacilityBuilderTests.cs`
- `Assets/Scenes/Demo/Story006/FacilityDemo.unity` (generated through Unity Editor APIs, plus metadata)
- `production/qa/evidence/story-006-defense-facilities/verification.md`
- Evidence in the same directory: `stamp.txt`, `editmode.xml`, `tests.log`, `editmode-all.xml`, `editmode-all.log`, `build.log`, `coordinator-validation.txt`, `player.log`, `player-213117.log`, `01-built-facilities.png`.

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

**Status**: [x] Verified with retained execution and visual evidence

- Nine EditMode cases passed (editmode.xml: 9 passed, 0 failed, 0 skipped; 2026-10-31 21:31 UTC): catalog values; successful spending/effects/events through `IFacilityBuilder`; secondary-resource shortage with zero notifications; per-hub capacity; both ruin policies; undocked/unknown input refusal; query/snapshot/reentrancy behavior; custom data effects and capacity.
- Tests are in an `Editor` subfolder so they use the default Editor assembly without introducing assembly definitions.
- The original worker launch was blocked by execution policy. The coordinator subsequently ran the project.yaml EditMode command with `-testFilter IronRing.Defense.Tests` and story-specific evidence paths under the corrected Editor mutex, then built and captured the player; test, build and player exit codes are 0 (`coordinator-validation.txt`). The mutex is `C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock`, held until Editor process exit.
- Defense tests verify exact prosperity changes and per-hub combat sums. `editmode-all.xml` also records 67/67 passing tests, including Story 005 `FacilityBonus_UsesNextHubAndChangesRound`; reviewed source confirms `WaypointResolver` consumes `IFacilityBuilder.CombatBonus(nextHub)` for combat strength. The shared composition root remains the coordinator integration pass as required by ownership rules.
- Current Defense source, data and test SHA256 values match `stamp.txt`; no implementation changes were needed after the successful runs.
- Demo builder entry point: `IronRing.Defense.Editor.FacilityDemoBuild.Build`. Creates `Assets/Scenes/Demo/Story006/FacilityDemo.unity` through Unity APIs once, preserves later edits, and builds `Builds/Demo/Story006/Game.exe` with explicit scenes without changing shared EditorBuildSettings.
- Run result: OBSERVED — `production/qa/evidence/story-006-defense-facilities/01-built-facilities.png` shows all three installed facilities at Foundry Hub, combat +8, prosperity +2, capacity 3/3, three build events and ruin building disabled, without label clipping at 1280x720. This verifies the isolated demo display, not manual build-menu input or full-game traversal.

---

## Dependencies

- Depends on: `production/epics/iron-ring-train/story-004-hub-growth-ruin-defeat.md`, `production/epics/iron-ring-train/story-005-waypoint-event-combat.md`
- Unlocks: `production/epics/iron-ring-train/story-007-minimal-hud.md`
