# Story 040: 경제 기반 — 돈(마르크)·품목 6종·휴식지 시장·열차 화물칸·거래 패널·운임·유지비·정산 장부·파산

> **상태 메모(2026-11-02 정리)**: 부분 유효 — 돈(마르크)·장부·운임만 남고 품목·시장·화물칸·거래 패널은 045에서 archive/economy-trade-2026-11-02/ 로 보관, 유지비는 048에서 0. 현재 규칙은 design/game-brief.md §돈 과 story-045·story-048 참조.

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Partial(045·048) — 돈·장부·운임 구현, 교역·파산 보관(045), 유지비 0(048) — 원래: Ready
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 1. 사용자 지시(2026-11-02 04:20): "완전 경영 시뮬레이션 쪽으로 싹 다 가주라." 전투를 뺀 039 위에 경영의 뼈대를 올린다. 돈과 품목, 휴식지 시장, 열차 화물칸, 거래, 바퀴 정산, 파산 패배.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 순수 C# 로직 + UGUI 화면 프리팹(빌더 리플렉션) + 데이터 JSON. 검증은 파이프라인(컴파일·생성기·씬 저장), 캡처 없음.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 돈: `IMoney`(append-only 계약) `Balance`, `TryGain/TrySpend(amount, reason)`, `Changed`. 데이터 `Economy.json`: 시작 잔고 200 마르크, 열차 유지비/바퀴 20, 승객 운임 8/명(휴식지 → 거점 하차 시), 파산 = 잔고 < 0이 `_bankruptLaps` 3바퀴 연속(기존 Defeat 신호로 결과 화면 "파산").
- [ ] 품목 6종(`Goods.json`): 석탄·철·부품·식량·유리·황동(id·이름·기준가·아이콘 키). 열차 화물칸: `ICargo` 용량(데이터 12칸, 품목별 수량), 적재/하역.
- [ ] 휴식지 시장(`Markets.json`): 휴식지 3곳마다 품목별 가격 배율·수요(사는 쪽 상한)·공급(파는 쪽 재고) — 거래하면 가격이 데이터 비율로 움직이고 바퀴마다 기준가로 복귀. 정차하면 거래 패널(UGUI 화면 `Trade`: 품목 줄 — 가격·내 수량·시장 재고·[사기][팔기] 버튼, 잔고·화물 N/12)이 열리고, 닫으면(또는 `_autoDepartSeconds`) 출발. 휴식지 이벤트(039)는 거래 패널 위 토스트로.
- [ ] 정산: 거점 복귀마다 장부 항목(`ILedger`: 바퀴·항목·금액; 운임·판매·구매·유지비)과 토스트 "바퀴 N 정산: +운임 a +판매 b −구매 c −유지비 d = ±e". 상단 상태줄에 "잔고 N마르크".
- [ ] 데모: `--economy-demo --economy-stage trade|ledger|bankrupt`(로그 `ECON: …`). 기존 데모 유지.

---

## Implementation Notes

- 로직 `Assets/Scripts/Economy/`(MoneyStore, Goods, Cargo, Markets, Ledger, EconomyInstaller, Resources/Economy/*.json), 화면 `Assets/Scripts/Hud/Trade/TradeScreen.cs` + `Editor/Screens/TradeScreenBuilder.cs`(SCREEN_IDS 추가, `.bridge/rebuild/Trade`), `TopBarScreen` 잔고, `RunResultScreen` 파산/재무 한 줄, `WaypointResolver`(휴식지 도착 → 거래 패널 열기, 승객 운임), `RunState`(파산 패배), ko.json.
- 검증은 열린 편집기 브리지, 캡처 없음.

---

## Out of Scope

- 생산·창고(041), 주민·만족도(042), 업그레이드(043), 장부 화면·그래프(044).

---

## Files

- `Assets/Scripts/Contracts/IronRingContracts.cs`(IMoney·ICargo·ILedger·IMarkets), `Assets/Scripts/Economy/**`(새), `Assets/Scripts/Hud/Trade/**`·`Editor/Screens/TradeScreenBuilder.cs`·`Editor/BuildMainCanvas.cs`·`TopBarScreen.cs`·`RunResultScreen.cs`, `Assets/Scripts/Combat/WaypointResolver.cs`, `Assets/Scripts/Hub/RunState.cs`, `Assets/Scripts/Npc/Passengers.cs`(운임), `Assets/Prefabs/UI/Screens/Trade.prefab`, `Assets/Scenes/Main.unity`, `ko.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 생성기 멱등 + 씬 저장(isDirty=false) + 데이터 키 표: `production/qa/evidence/story-040-economy-market-ledger/verification.md`(캡처 없음 — 사용자 지시)

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-039-no-combat-ring-rest-stops.md`
- Unlocks: `story-041-production-buildings.md`
