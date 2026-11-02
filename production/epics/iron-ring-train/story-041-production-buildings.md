# Story 041: 생산 건물 — 서식지 레시피를 생산 데이터로(입력/출력/노동자/유지비), 거점 창고, 타일 비용, 창고 → 열차 적재

> **상태 메모(2026-11-02 정리)**: 부분 유효 — 타일 가격(4~12마르크, 전액 환급)만 남고 생산 건물·창고·적재는 045에서 보관. 현재 규칙은 design/game-brief.md §Core loop 과 story-045 참조.

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Partial(045) — 타일 가격·환급만 구현·유지, 생산·창고·적재 보관 — 원래: Ready(040 뒤)
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 2. 사용자 지시(2026-11-02 04:20): "완전 경영 시뮬레이션 쪽으로 싹 다 가주라." 완성된 서식지(건물)가 바퀴마다 품목을 생산·소비하고, 타일에 값이 생기며, 창고의 물건을 열차에 싣는다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 순수 C# 로직 + UGUI 화면 프리팹(빌더 리플렉션) + 데이터 JSON. 검증은 파이프라인(컴파일·생성기·씬 저장), 캡처 없음.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] `HabitatTypes.json`에 생산 필드: `_produces[{goodsId,amount}]`, `_consumes[{goodsId,amount}]`, `_workers`(필요 노동자), `_upkeep`(마르크/바퀴), `_needsSteam`(보일러실 증기 필요 여부). 19종 모두 값 지정(예: 석탄 야적장 석탄 +3; 보일러실 석탄 −1 → 증기 공급; 공방 철 −1 + 증기 → 부품 +1; 온실 → 식량 +2; 가스등 거리 → 만족도(042); 막사·침상 → 주거(042)).
- [ ] 거점 창고 `IWarehouse`(품목별 수량, 상한 데이터 60): 바퀴 정산 때 건물이 입력을 소비하고 출력을 창고에 넣는다(노동자 부족·입력 부족·증기 없음이면 가동 중단, 건물 위에 상태 아이콘). 유지비는 장부에.
- [ ] 타일 비용(`ItemCatalog.json` `_price`): 배치 시 잔고에서 차감, 회수·철거·되돌리기 환급(`_refundRatio`). 잔고 부족이면 고스트 붉게·배치 거절 토스트. 장식은 소액.
- [ ] 적재: 거점 내부 화면 "창고" 패널(UGUI `Warehouse`: 품목 줄 — 창고 수량·열차 수량·[싣기][내리기])로 창고 ↔ 화물칸 이동. 마을 소비(식량 등)는 042.
- [ ] 데모: `--production-demo --production-stage build|tick|load`(로그 `PROD: …`).

---

## Implementation Notes

- `HabitatCatalog`/`HabitatType` 생산 필드, `Assets/Scripts/Economy/Production.cs`(바퀴 정산 훅: `RunState` 바퀴 증가 뒤), `Warehouse.cs`, `BuildingView` 상태 아이콘(프리팹 조각), `PlacementGhost`/`ItemPlacement` 비용, `Hud/Warehouse/WarehouseScreen.cs` + 빌더, 핫바 칸에 가격 표시, ko.json.

---

## Out of Scope

- 노동자 배치·만족도·주거(042), 품목 아이콘 아트(기존 아이템 아이콘 재사용·단색 대체).

---

## Files

- `Assets/Scripts/Habitats/HabitatCatalog.cs`·`Resources/Habitats/HabitatTypes.json`, `Assets/Scripts/Economy/Production.cs`·`Warehouse.cs`, `Assets/Scripts/Items/Resources/Items/ItemCatalog.json`·`ItemPlacement.cs`, `Assets/Scripts/Presentation/Interior/PlacementGhost.cs`·`BuildingView.cs`, `Assets/Scripts/Hud/Warehouse/**`·`Hotbar/**`·`Editor/Screens/WarehouseScreenBuilder.cs`·`Editor/BuildMainCanvas.cs`, `Assets/Prefabs/UI/Screens/Warehouse.prefab`, `ko.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 생성기 멱등 + 씬 저장(isDirty=false) + 데이터 키 표: `production/qa/evidence/story-041-production-buildings/verification.md`(캡처 없음 — 사용자 지시)

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-040-economy-market-ledger.md`
- Unlocks: `story-042-population-labor-town-tier.md`
