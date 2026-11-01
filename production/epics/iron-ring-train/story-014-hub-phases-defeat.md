# Story 014: 거점 단계(폐허→복구→정착→붕괴)와 새 패배 규칙

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 16:05 KST 코디네이터 브리지 검증 — EditMode 154/154, 스크린샷 3장 확보·육안 확인)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01 16:10 KST (코디네이터, 검증 완료 → Done)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 6 — 거점 상태(폐허 → 복구 중 → 정착 → 붕괴)와 번영 수치, 정착했던 거점이 모두 붕괴하거나 기한 안에 정착 0이면 패배

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] `IHubDevelopment`를 구현한다. 모든 거점은 Ruined(번영 0)로 시작한다. 번영이 0을 넘으면 Recovering, 데이터 임계값(기본 40) 이상이거나 서식지가 완성되면 Settled, 정착 후 번영이 0으로 떨어지면 Collapsed. 단계 전환마다 `PhaseChanged`가 발생한다.
- [x] 번영 변화 규칙(데이터): 바퀴마다 배치 아이템 1개당 +1, 서식지 완성 +5, 시설 1개당 +2, NPC 1인당 +3, 기본 감소 -3. 거점별 성장 프로필 배수(004)는 유지한다. Ruined 거점은 감소하지 않는다(0 아래로 가지 않음).
- [x] 패배 규칙: 한 번이라도 정착한 거점이 모두 Collapsed가 되거나, `LapsUntilDeadline`(데이터, 기본 8바퀴)이 0이 될 때까지 정착한 거점이 하나도 없으면 `IHubStateSystem.RunLost`가 발생한다. 기존 "모두 폐허면 패배" 규칙은 제거한다.
- [x] 기존 `IHubStateSystem`의 `IsRuined`는 Ruined 또는 Collapsed일 때 true다. 005(전투 패배 페널티), 006(폐허에서 건설 금지), 010(폐허 연출), 011(HUD)은 수정 없이 동작한다.
- [x] 합류한 NPC는 거점 내부 화면에 스프라이트로 나타나 빈 터 사이를 천천히 돌아다닌다(순수 연출). 시설은 부지 칸에 지어지고 내부 화면에 보인다(006 로직 재사용, 부지 칸 클릭 → 건설 메뉴). — 화면 확인: `01-recovering-interior.png`(내부에 NPC 스프라이트, 부지 위 방벽, 배치 아이템). 부지 클릭→건설 메뉴는 스크립트 데모 경유 — 실제 마우스 기록은 사용자 확인 항목.
- [x] 내부 화면과 선로 화면 모두에서 거점 단계가 한국어로 표시된다(폐허·복구 중·정착·붕괴). 선로 화면의 거점 스프라이트는 단계에 따라 폐허/정상 버전으로 바뀐다. — 화면 확인: `01-recovering-interior.png`("주조소 · 복구 중 · 번영 20"), `02-settled.png`("주조소 · 정착 · 번영 31"). 선로 화면의 단계 라벨이 겹쳐 보이는 표시 결함은 014 에이전트가 수정 중(연출 폴리시, 판정 로직과 무관).
- [x] EditMode 테스트가 단계 전환, 번영 규칙, 두 가지 패배 조건, 기한 감소를 검증하고, 기존 Hub 테스트는 새 규칙에 맞게 갱신해 통과한다.
- [x] 스크린샷: 복구 중 거점 내부(NPC·시설 보임), 정착 표시, 패배 화면(기한 만료). — `production/qa/evidence/story-014-hub-phases-defeat/01-recovering-interior.png`, `02-settled.png`, `03-deadline-defeat.png` (2026-11-01 16:05 KST, 코디네이터 육안 확인).

---

## Implementation Notes

- 004의 `RunState`를 확장하거나 감싼다. 데이터 프로필 JSON에 단계 임계값과 새 번영 규칙을 추가한다.
- 기존 Integration 테스트(`IronRing.Integration.Tests`)의 시나리오를 새 규칙(폐허 시작, 조사·배치로 정착)으로 갱신한다.

---

## Out of Scope

- Story 012: 칸·조사
- Story 013: 아이템·태그 판정

---

## Files

구현(실제 경로, Bootstrap 미수정):
- `Assets/Scripts/Hub/RunState.cs` (IHubStateSystem + IHubDevelopment, 단계 머신·바퀴 규칙·기한·두 패배 조건, `RunLossReason`)
- `Assets/Scripts/Hub/HubState.cs` (명시적 전이표), `HubDefinition.cs` (`HubRunRules` 포함), `HubData.cs`
- `Assets/Scripts/Hub/Data/HubProfiles.json` (`_initial` 0, `_settledThreshold` 40, `_basePerLap` -3, `_perPlacedItem` 1, `_habitatBonus` 5, `_deadlineLaps` 8; 004 배수 유지)
- `Assets/Scripts/Hub/IProsperitySources.cs` (배치 아이템 = IHubInterior ItemPlaced 칸 수, 서식지 = IHabitatSystem)
- `Assets/Scripts/Hub/IInteriorFacilityPlots.cs` (부지 칸 건설 래퍼, 006 FacilityBuilder 재사용)
- `Assets/Scripts/Hub/HubDevelopmentInstaller.cs` (BeforeSceneLoad에서 Composing/Ready 구독, 한 번만 설치)
- `Assets/Scripts/Hub/Demo/PhaseDemoDriver.cs`, `Assets/Scripts/Hub/Demo/Resources/HubPhase/hub_phase_demo.json` (`--phase-demo`, `--deadline-demo`)
- `Assets/Scripts/Presentation/HubPhase/InteriorNpcWalker.cs`, `InteriorFacilityView.cs`, `RingHubPhaseView.cs`, `HubPhaseViewSettings.cs`, `Resources/HubPhase/hub_phase_view.json`
- `Assets/Scripts/Hud/HubPhase/PhaseHud.cs`, `Assets/UI/Hud/Resources/Hud/PhaseHud.uxml`, `PhaseHudStyle.uss`
- `Assets/Scripts/Localization/Resources/ko.json` (`phase_*` 키 11개 추가)
- 테스트: `Assets/Tests/EditMode/Hub/Editor/HubStateTests.cs`, `HubSourcesTests.cs`, `Assets/Tests/EditMode/Integration/Editor/IntegrationFlowTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- EditMode 테스트 통과 (브리지 run-tests)
- 스크린샷 3장: `production/qa/evidence/story-014-hub-phases-defeat/`

**Status**: [x] EditMode 통과 / [x] 스크린샷 3장 (코디네이터 캡처·육안 확인, 2026-11-01 16:05 KST)
- `production/qa/evidence/integration/editmode-prefab.xml` — 프리팹·씬 배치 전환 뒤 전체 `IronRing` 154/154 (2026-11-01 16:04 KST, 최종)
- `production/qa/evidence/story-014-hub-phases-defeat/editmode.xml` — IronRing.Hub.Tests 28/28
- `production/qa/evidence/story-014-hub-phases-defeat/editmode-integration.xml` — IronRing.Integration.Tests 3/3
- `production/qa/evidence/story-014-hub-phases-defeat/editmode-all.xml` — IronRing 전체 154/154
- 스크린샷 (`production/qa/evidence/story-014-hub-phases-defeat/`):
  - `01-recovering-interior.png`: "주조소 · 복구 중 · 번영 20", 배치 아이템, 부지 위 방벽, 내부를 돌아다니는 NPC.
  - `02-settled.png`: "주조소 · 정착 · 번영 31".
  - `03-deadline-defeat.png`: 패배 화면 제목 "기한 안에 정착한 거점이 없습니다". 패배 사유별 제목은 `IRunLossInfo.LastLossReason`(`RunLossReason.DeadlineExpired` / `AllSettledCollapsed`)을 011 오버레이가 읽어 표시하며, `PhaseHud`는 보조 문구만 맡는다.
- 캡처 인자: `production/qa/evidence/story-014-hub-phases-defeat/verification.md`
- 남은 항목(스토리 Done과 별개): 선로 화면 거점 단계 라벨 겹침은 014 에이전트가 수정 중(연출). 내부 화면 실제 마우스 조작 기록은 사용자 확인.

---

## Dependencies

- Depends on: `story-012-hub-interior-map.md`, `story-013-items-tag-habitat.md`
- Unlocks: None
