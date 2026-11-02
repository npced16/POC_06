# Story 001: 고리형 선로와 열차 이동

> **상태 메모(2026-11-02 정리)**: 부분 유효 — 고리 선로·열차 이동은 살아 있으나 '거점 4곳' 구성은 039의 원형 선로(황동골 + 휴식지 3)로 대체. 현재 규칙은 design/game-brief.md §Core loop 과 story-039 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 1 — 거점 4곳과 경유지로 이루어진 고리형 선로 위의 열차 이동

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Core loop, Build order step 1), scoped to this story:*

- [x] 선로 정의(노드 순서, 각 노드가 거점인지 경유지인지)를 외부 설정 에셋에서 읽어 고리를 구성한다. 코드에 하드코딩하지 않는다.
- [x] 열차는 한 번에 한 노드씩 다음 노드로 이동하고, 마지막 노드 다음에는 첫 노드로 돌아온다.
- [x] 거점 노드에 도착하면 정차 상태가 되고, 경유지 노드에 도착하면 경유지 이벤트 훅을 호출한다.
- [x] 거점 수는 설정에서 읽으며 4에 고정되지 않는다. 설정에 거점을 추가하면 코드 수정 없이 고리에 반영된다.
- [x] 선로 노드는 어떤 경우에도 제거되거나 끊기지 않는다.
- [x] 열차의 현재 노드를 질의할 수 있고, 위치가 바뀔 때 이벤트가 발생한다 (HUD와 다른 시스템이 구독).
- [x] 한 바퀴를 완주하면 바퀴 수가 1 증가한다.
- [x] 화면에서 열차가 노드 사이를 이동하는 모습이 보인다 (스크린샷으로 보존).

---

## Implementation Notes

- 선로 데이터는 ScriptableObject 같은 데이터 에셋으로 두고, 이동 규칙(순수 C#)과 화면 표현(MonoBehaviour)을 분리해 규칙을 단위 테스트할 수 있게 한다.
- Unity 6.3은 학습 범위 밖이므로, 쓰는 API는 `docs/engine-reference/unity/`에서 먼저 확인한다.
- 입력 시스템은 새 Input System이 기본이다 (`Assets/Settings/InputSystem_Actions.inputactions` 존재).

---

## Out of Scope

- Story 002: 서식지 조합 판정
- Story 004: 거점의 번성/쇠락 상태
- Story 005: 경유지 이벤트의 실제 내용과 전투

---

## Files

- `Assets/Scripts/Track/RingTrack.cs`
- `Assets/Scripts/Track/TrainController.cs`
- `Assets/Scripts/Track/TrackData.cs`
- `Assets/Scripts/Track/Demo/TrackDemoBootstrap.cs`
- `Assets/Scripts/Track/Resources/Track/ring-track.json`
- `Assets/Tests/EditMode/Track/Editor/RingTrackTests.cs`
- `production/qa/evidence/story-001-ring-track-train-movement/verify.ps1`
- `production/qa/evidence/story-001-ring-track-train-movement/verification.md`
- `production/qa/evidence/story-001-ring-track-train-movement/01-train-between-nodes.png`
- `production/qa/evidence/story-001-ring-track-train-movement/editmode-all.xml`
- `production/qa/evidence/story-001-ring-track-train-movement/editmode-all.log`
- `production/qa/evidence/story-001-ring-track-train-movement/build.log`
- `production/qa/evidence/story-001-ring-track-train-movement/stamp.txt`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*At `qa.level: minimal` tests are waived (advisory), but a retained screenshot of player-observable behaviour is not.*

**Story Type**: Logic
**Required evidence**:
- 단위 테스트: `Assets/Tests/EditMode/Track/` (권장, 이 단계에서는 advisory)
- 열차 이동 스크린샷: `production/qa/evidence/` (필수, `.claude/docs/run-and-observe.md` 절차)

**Status**: Verified — final EditMode tests, standalone build and rendered screenshot.

- `production/qa/evidence/story-001-ring-track-train-movement/editmode-all.xml`: coordinator's actual Unity EditMode run, 67 total passed, 0 failed, including all 10 `IronRing.Track.Tests` cases; XML inspected by the Track worker.
- `production/qa/evidence/story-001-ring-track-train-movement/editmode-all.log`: full Unity 6000.3.27f1 test log. This is test execution evidence, not a parse-only check.
- AC 1: `ShippedJson_LoadsAuthoredTopologyAndTiming`; AC 2/7: `Movement_AdvancesOneNodeAndCountsCompletedLaps`, `SingleNodeRing_RemainsConnected`; AC 3/6: `Arrival_StopsAtStationsAndPublishesWaypointAndPositionEvents`; AC 4/5: `Topology_IsImmutableAndStationCountComesFromData` with 1, 4 and 5 stations.
- Additional coverage: invalid topology rejection, stacked encounter holds, shared interface node identities/arrival/lap notifications.
- Approved provisional defaults: four stations and four waypoints, two seconds per edge, explicit departure from stations, optional demo auto-depart disabled by default. Values and node layout live in the JSON data asset.
- Workflow tier: minimal. Explicit task instructions require tests despite the tier's advisory waiver. `docs/README.md` was absent; coordinator approved this story and `/dev-story` as the spec authority.
- `RingTrack` implements `IronRing.Contracts.ITrackRuntime`; `TrackNode` implements `ITrackNode`. Station IDs also serve as HubIds. Holds stack by reason and pause both departure and in-transit interpolation.
- Per coordinator decision, runtime uses the default assembly and tests live in an `Editor` subfolder; no asmdefs remain.
- `build.log` records the final standalone build's successful exit code 0; output is `Builds/Demo/Story001/Game.exe`.
- Coordinator confirmed the final capture player exited 0 with `--track-demo true --track-screenshot ...` and retained the image below.
- `01-train-between-nodes.png` was opened and visually inspected: 1280×720, eight connected nodes, four gold stations, four teal waypoints, readable labels and an orange train between FOUNDRY and NORTH PASS. The status reads `LAP 0 / FOUNDRY / TRAVELLING`; the departure button is disabled during transit.
- Run result: OBSERVED — the train renders between adjacent nodes on the closed ring in `production/qa/evidence/story-001-ring-track-train-movement/01-train-between-nodes.png`.
- Evidence boundary: automated EditMode rules and a rendered in-transit demo frame are verified; manual pointer/keyboard input and subjective movement feel were not tested.
- `stamp.txt` uses PowerShell SHA-256 rather than the repository hash helper, because that helper invokes Git and this task forbids Git CLI use.

---

## Dependencies

- Depends on: None
- Unlocks: `production/epics/iron-ring-train/story-002-habitat-combo-flag.md`, `production/epics/iron-ring-train/story-005-waypoint-event-combat.md`
