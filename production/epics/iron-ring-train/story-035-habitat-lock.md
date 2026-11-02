# Story 035: 서식지 고정 — 한 번 완성된 서식지는 더 커지지 않고, 자기 칸이 빠질 때만 깨진다

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-02 — 커밋 b89a9d4, 검증 production/qa/evidence/story-035-habitat-lock/verification.md; 문서 상태만 늦게 갱신)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 3(서식지 판정). 사용자 지시(2026-11-02 01:20): "서식지 결정되는 것도 조금 이상해, 판정 다듬어봐. 이미 만들어지면 더 확장 안 되게."

지금은 칸이 바뀔 때마다 거점 전체를 처음부터 다시 판정한다(`HabitatMatcher.Evaluate`). 묶음형·혼합 묶음형은 이어진 칸을 전부 flood fill로 모으므로, 완성된 서식지 옆에 같은 계열 타일을 놓으면 그 서식지가 그 칸까지 삼켜 커지고(건물이 갑자기 늘어남), 때로는 더 큰 종류로 바뀌거나 다른 칸 조합으로 다시 잡힌다. 플레이어가 "다 지었다"고 느낀 건물이 옆 타일 하나에 모양을 바꾸는 것이 "이상한 판정"의 원인이다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: LOW
**Engine Notes**: 순수 C#.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 고정: 서식지가 완성되는 순간의 칸 묶음(종류 + 칸 번호 + 칸마다 놓인 타일)을 그 거점의 "고정 목록"에 넣는다. 이후 판정에서 고정된 칸은 처음부터 차지된 것으로 보고 새 판정에 쓰지 않는다. 따라서 완성된 서식지 옆에 같은 계열 타일을 놓아도 그 서식지는 커지지 않고, 새 타일은 새 서식지의 시작 칸이 된다(고정 칸과는 이어지지 않음).
- [ ] 깨짐: 고정된 서식지는 자기 칸 중 하나라도 타일이 빠지거나 다른 타일로 바뀔 때만 고정이 풀린다(`HabitatBroken`). 풀린 칸들은 그 판정에서 바로 일반 칸으로 돌아가 다시 판정된다(남은 칸이 조건을 채우면 새로 완성될 수 있다).
- [ ] 이벤트·소비자: `CompletedAt`은 고정 목록(완성 순서)을 돌려준다. `HabitatCompleted`/`HabitatBroken`/`HabitatChanged`의 의미(종류별 증감, 칸 구성 변화)는 그대로라 건물·NPC 집·번영·도감은 코드 수정 없이 동작한다. 되돌리기(031-A)와 철거는 배치 경로를 그대로 타므로 함께 동작한다.
- [ ] 데이터: `HabitatTypes.json` 루트 `_lockCompleted`(기본 true). false면 예전처럼 매번 전체 재판정.
- [ ] 브리프 MVP 3 문장에 고정 규칙 한 줄. 검증: 브리지 컴파일 + `--habitat-demo --habitat-stage complete` 뒤 옆 칸에 같은 타일을 하나 더 놓는 데모 단계 `--habitat-stage locked`(완성 4칸 그대로, 옆 타일은 혼자) 캡처 1장. `production/qa/evidence/story-035-habitat-lock/` + verification.md.

---

## Implementation Notes

- `HabitatMatcher.Evaluate(cells, catalog, reserved)`: reserved 칸을 claimed에 미리 넣는다. 고정 서식지 자체는 돌려주지 않는다(호출자가 합침).
- `ClusterHabitatSystem`: 거점별 `List<LockedCluster>`(HabitatCluster + 칸→타일 스냅샷). `Compute(hubId)` = ① 고정 목록에서 스냅샷과 지금 타일이 다른 것을 제거 ② 남은 고정 칸을 reserved로 넘겨 매처 실행 ③ 새 결과를 고정 목록에 추가 ④ 고정 목록의 클러스터를 반환. 이벤트 계산(`Evaluate`)은 그대로.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 완성 칸 수를 RequiredCount로 잘라내기(완성 순간의 묶음을 그대로 고정), 건물 이동.

---

## Files

- `Assets/Scripts/Habitats/HabitatMatcher.cs`, `Assets/Scripts/Habitats/ClusterHabitatSystem.cs`, `Assets/Scripts/Habitats/HabitatCatalog.cs`(`LockCompleted`), `Assets/Scripts/Habitats/Resources/Habitats/HabitatTypes.json`(`_lockCompleted`), `Assets/Scripts/Habitats/Demo/HabitatDemoDriver.cs`(`locked` 단계), `design/game-brief.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- 브리지 컴파일 확인 + 캡처 1장: `production/qa/evidence/story-035-habitat-lock/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-020-habitat-variety.md`, `story-031-demolish-undo-build-plan.md`
- Unlocks: None
