# Story 042: 주민·노동·만족도·마을 등급 — 일반 주민 증가, 노동자 자동 배치와 가동 조건, 만족도 4요소, 등급과 해금

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Ready (041 뒤)
> **Layer**: Core + Presentation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3. 사용자 지시(2026-11-02 04:20): "완전 경영 시뮬레이션 쪽으로 싹 다 가주라." 건물이 돌아가려면 사람이 필요하고, 사람이 늘려면 집·식량·빛·일자리가 필요하다. 등급이 오르면 레시피·업그레이드가 열린다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 순수 C# 로직 + UGUI 화면 프리팹(빌더 리플렉션) + 데이터 JSON. 검증은 파이프라인(컴파일·생성기·씬 저장), 캡처 없음.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 주민 `IPopulation`: 일반 주민 수(시작 3), 고유 NPC는 별도(기존). 바퀴마다 주거 여유(막사·침상 건물의 `_housing` 합) > 주민이고 식량(창고 소비 `_foodPerResident`)이 충분하고 만족도 ≥ 기준이면 +1~2(데이터), 식량 부족·만족도 낮음이면 −1(최소 1).
- [ ] 노동: 건물 `_workers` 합 ≤ 주민 + 고유 NPC. 자동 배치(완성 순, 데이터 우선순위), 부족한 건물은 가동 중단(041 상태 아이콘 "일손 부족"). 실업 = 주민 − 배치 노동자 → 만족도 감점.
- [ ] 만족도(0~100): 주거 충족·식량 충족·빛(가스등 거리 수)·일자리(실업률) 각 25점 비율, 데이터 가중치. 상단 상태줄 "주민 N · 만족도 N".
- [ ] 마을 등급(`TownTiers.json`): 폐허(0) → 정착촌(주민 5·번영 20) → 마을(주민 12·번영 50) → 도시(주민 25·번영 100). 등급 오를 때 토스트·내부 제목 갱신, 해금 표(레시피 id 목록·열차 업그레이드 id·개통 가능 휴식지). 기존 `HubPhase`(폐허·복구 중·정착·붕괴)는 등급으로 사상(데이터)하고 폐허 연출은 등급에 따름. 번영 공식은 기존 유지 + 생산 가동 건물 보너스.
- [ ] 결과: 20바퀴 기한, 도시 = 승리, 기한 종료 = 등급별 부분 성공, 파산 = 패배(040). 데모 `--town-demo --town-stage grow|starve|tier`.

---

## Implementation Notes

- `Assets/Scripts/Economy/Population.cs`·`Labor.cs`·`Satisfaction.cs`·`TownTiers.cs` + `Resources/Economy/TownTiers.json`, `RunState`(기한 20·승리 조건 등급), `HubDevelopment`(단계 ↔ 등급 사상), `HabitatCodex`(등급 해금), `TopBarScreen`, `InteriorTitle`, ko.json.

---

## Out of Scope

- 주민 개별 표현(걷는 주민은 선택: 기존 NPC 워커 재사용 가능하면 소수만), 교육·범죄 등 추가 지표.

---

## Files

- `Assets/Scripts/Economy/Population.cs`·`Labor.cs`·`Satisfaction.cs`·`TownTiers.cs`·`Resources/Economy/TownTiers.json`, `Assets/Scripts/Hub/RunState.cs`·`HubDevelopment*.cs`·`Data/HubProfiles.json`, `Assets/Scripts/Habitats/HabitatCodex.cs`·`Resources/Habitats/HabitatTypes.json`(`_housing`), `Assets/Scripts/Hud/TopBarScreen.cs`·`Interior/*Title*`·`RunResultScreen.cs`, `ko.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- 브리지 컴파일 확인 + 생성기 멱등 + 씬 저장(isDirty=false) + 데이터 키 표: `production/qa/evidence/story-042-population-labor-town-tier/verification.md`(캡처 없음 — 사용자 지시)

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-041-production-buildings.md`
- Unlocks: `story-043-train-upgrades-line-extension.md`
