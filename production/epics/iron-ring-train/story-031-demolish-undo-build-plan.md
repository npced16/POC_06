# Story 031: 부수기·되돌리기, 그리고 건물을 먼저 고르고 타일을 하나씩 놓는 건설 계획

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready (A는 착수, B는 사용자 확인 뒤)
> **Layer**: Core + Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Core loop 2단계(배치). 사용자 지시(2026-11-02 00:10): "좀 하나씩 나오고, 취소 및 부수는 거 가능하게 해주라. 그리고 좀 더 직관적으로 연관성 있도록 — '~~건물은 이런 거 있으면 되겠다'처럼." 코디네이터 해석: A = 부수기·되돌리기(확실), B = 건물을 먼저 고르면 필요한 타일이 하나씩 올라오는 건설 계획 모드(확인 대기).

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: UGUI + TMP, 새 Input System.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

### A. 부수기·되돌리기
- [ ] 부수기: 완성된 건물의 아무 칸에서 빈 손 우클릭 → 그 건물의 타일이 모두 회수되고 건물이 허물어진다(짧은 무너짐 연출: 조각이 0.25초 동안 내려앉으며 사라짐, 토스트 "<종류> 철거"). 사는 NPC는 집을 잃고 열차 옆 자리로 간다(기존 집 없음 규칙). 미완성 타일의 우클릭 회수는 그대로.
- [ ] 되돌리기: Ctrl+Z(새 Input System)로 최근 배치·회수·철거를 순서대로 되돌린다(최대 5단계, 데이터). 되돌린 결과는 일반 배치와 같은 경로(`IItemPlacement`)를 거쳐 서식지 판정·건물·번영이 다시 계산된다. 거점을 떠나면 기록이 비워진다.
- [ ] 핫바 안내줄에 "우클릭: 회수/철거 · Ctrl+Z: 되돌리기"를 넣는다.

### B. 건설 계획(사용자 확인 뒤)
- [ ] 핫바 오른쪽 "건물" 버튼(B 키)으로 건물 목록 패널: 해금된 종류마다 건물 그림(지붕 계열 + 상징물 썸네일)과 "<이름>은 <타일>×n + <타일>×m (+ 아무거나 k)이면 된다 · N칸" 한 줄. 미해금은 "???"로 끝에.
- [ ] 항목을 고르면 계획 모드: 패턴형은 템플릿 모양 고스트가 마우스를 따라오고 R 키로 회전·거울상 전환, 좌클릭으로 자리를 확정하면 각 칸에 필요한 타일이 순서대로 자동 선택되어(핫바에 "다음: 보일러 (1/3)") 좌클릭마다 하나씩 놓인다. 묶음·혼합형은 칸 수만큼 자유 배치: 첫 칸을 놓으면 인접 칸만 초록으로 안내되고, 필요한 타일이 하나씩 올라온다(최소 수를 먼저, 그다음 "아무거나"). 다 놓으면 서식지 판정으로 건물이 선다.
- [ ] 계획 취소: Esc/우클릭/"취소" 버튼 → 계획 고스트·안내만 사라지고 놓은 타일은 남는다(되돌리기로 치울 수 있음).
- [ ] 핫바 직접 배치는 그대로 가능(계획은 선택 사항).
- [ ] 스크린샷: (1) 건물 목록 패널, (2) 패턴 계획 고스트(회전), (3) "다음: 석탄더미 (2/4)" 안내와 하나씩 놓는 중, (4) 철거 직후. `production/qa/evidence/story-031-demolish-undo-build-plan/` + verification.md.

---

## Implementation Notes

- A: `PlacementGhost`(우클릭 분기: 건물 칸이면 철거), `BuildingView`(무너짐 연출·건물 칸 조회), `ItemPlacement`/`IItemPlacement`(되돌리기 기록은 표현 쪽 `PlacementHistory`로 — 배치·회수 호출을 감싸서 기록), 핫바 안내줄.
- B: `BuildPlanScreen`(UGUI 화면, Simple 스킨) + `PlacementPlan`(현재 계획: 종류·템플릿·확정 위치·진행), `PlacementGhost` 확장(다중 칸 고스트, 회전), 핫바와 연결(다음 타일 자동 선택·진행 표시). 데이터: 계획 고스트 색, 안내 문구.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 건물 이동, 업그레이드, 비용.

---

## Files

- A: `Assets/Scripts/Presentation/Interior/PlacementGhost.cs`·`BuildingView.cs`·`PlacementHistory.cs`(새), `Assets/Scripts/Hud/Hotbar/HotbarScreen.cs`, `ko.json`, `hub_phase_view.json`/`InteriorCatalog`(연출 값)
- B: `Assets/Scripts/Hud/BuildPlan/BuildPlanScreen.cs`·`BuildPlanRowView.cs` + 빌더, `Assets/Scripts/Presentation/Interior/PlacementPlan.cs`, `PlacementGhost.cs`, `HotbarScreen.cs`, `BuildMainCanvas.cs`(SCREEN_IDS), `ko.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 4장: `production/qa/evidence/story-031-demolish-undo-build-plan/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-027-combo-buildings-click-talk.md`, `story-030-early-win-npc-unlocks.md`
- Unlocks: None
