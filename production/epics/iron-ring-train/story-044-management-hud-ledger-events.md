# Story 044: 경영 HUD — 장부 화면, 건물 정보 패널, 목표 표시, 결과 재무 요약, 경제 이벤트

> **상태 메모(2026-11-02 정리)**: 대체 — 장부 화면·경제 이벤트는 보류, 정산 토스트·주민/등급 HUD는 045에서 구현. 현재 규칙은 story-045 참조.

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Superseded(045) — 원래: Ready (043 뒤)
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 5. 사용자 지시(2026-11-02 04:20): "완전 경영 시뮬레이션 쪽으로 싹 다 가주라." 경영의 정보를 한눈에: 장부, 건물 상태, 목표, 결과. 휴식지 이벤트는 경제 이벤트로 교체.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 순수 C# 로직 + UGUI 화면 프리팹(빌더 리플렉션) + 데이터 JSON. 검증은 파이프라인(컴파일·생성기·씬 저장), 캡처 없음.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 장부 화면(UGUI `Ledger`, L 키/상단 버튼): 최근 10바퀴 항목(운임·판매·구매·유지비·임금·업그레이드)과 바퀴별 순이익 막대(단색 사각형), 총수입·총지출·최고 잔고.
- [ ] 건물 정보: 완성 건물 클릭(빈 손) → 패널(이름·생산/소비·노동자 n/m·유지비·상태: 가동/일손 부족/입력 부족/증기 없음), NPC 대화는 그대로.
- [ ] 목표 표시: 상단에 "목표: 도시 (주민 N/25 · 번영 N/100) · 바퀴 N/20". 결과 화면 재무 요약(총수입·총지출·최고 잔고·최종 인구·등급).
- [ ] 경제 이벤트(`Events.json` 교체): 가격 급등/급락(해당 휴식지 품목 ×1.5/×0.6 한 바퀴), 상인(설계도 1개 판매 50마르크, 수락/거절 토스트 버튼), 파업(다음 바퀴 생산 절반), 폭풍(유지비 ×1.5 한 바퀴), 승객 몰림(운임 ×2 이번 정차). 휴식지별 가중치.
- [ ] 데모 `--hud-demo --hud-stage ledger|building|goal|event`.

---

## Implementation Notes

- `Hud/Ledger/LedgerScreen.cs` + 빌더, `Hud/Building/BuildingInfoScreen.cs` + 빌더, `TopBarScreen` 목표, `RunResultScreen`, `Economy/EconomyEvents.cs` + `Resources/Economy/Events.json`, `WaypointResolver` 이벤트 표 교체, ko.json.

---

## Out of Scope

- 그래프 라이브러리, 애니메이션 차트.

---

## Files

- `Assets/Scripts/Hud/Ledger/**`·`Building/**`·`TopBarScreen.cs`·`RunResultScreen.cs`·`Editor/Screens/LedgerScreenBuilder.cs`·`BuildingInfoScreenBuilder.cs`·`Editor/BuildMainCanvas.cs`, `Assets/Scripts/Economy/EconomyEvents.cs`·`Resources/Economy/Events.json`, `Assets/Scripts/Combat/WaypointResolver.cs`·`Resources/Story005Encounters.json`, `Assets/Scripts/Presentation/Interior/BuildingView.cs`(클릭), `Assets/Prefabs/UI/Screens/Ledger.prefab`·`BuildingInfo.prefab`, `ko.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- 브리지 컴파일 확인 + 생성기 멱등 + 씬 저장(isDirty=false) + 데이터 키 표: `production/qa/evidence/story-044-management-hud-ledger-events/verification.md`(캡처 없음 — 사용자 지시)

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-043-train-upgrades-line-extension.md`
- Unlocks: None
