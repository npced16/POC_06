# 스토리 040 검증 — 경제 기반(로직·데이터·연결)

작성 2026-11-02, gameplay-programmer(Claude). 범위 변경(사용자 05:10 "아이콘이랑 UI 쪽은 싹 다 codex한테"): 이 문서는 **로직 쪽**만 다룬다. 거래 패널·상단 잔고·결과 화면 문구·아이콘은 codex(`production/handoffs/codex-ui-040.md`)가 맡는다. 캡처 없음(사용자 지시), 자동 테스트 없음(브리프 개발 규칙).

## 규칙·수치

| 규칙 | 수치(데이터) | 구현 |
|---|---|---|
| 시작 잔고 | 200마르크 (`Economy.json _startBalance`) | `MoneyStore` |
| 열차 유지비 | 20/바퀴 (`_trainUpkeep`), 강제 지출(0 아래 허용) | `Settlement.Settle` |
| 승객 운임 | 8/명, 거점 하차(`IPassengers.Disembarked`) 때 (`_fare`) | `Settlement.OnDisembarked` |
| 파산 | 정산 직후 잔고 < 0이 3바퀴 연속 (`_bankruptLaps`) → `RunState.DeclareBankrupt("파산")` → Defeat, `RunLossReason.Bankrupt` | `Settlement.JudgeBankruptcy`, `RunState` |
| 화물칸 | 12칸 (`_cargoCapacity`), 칸 = 수량 합 | `Cargo` |
| 살 때 가격 | max(1, round(기준가 × 휴식지 배율 × factor)), factor 시작 1 | `Markets` |
| 팔 때 가격 | max(1, round(살 때 가격 × 0.8)) (`_sellRatio`, 같은 곳 되팔기 차익 방지) | `Markets` |
| 거래 변동 | 1개 살 때 factor +0.05 (`_buyPriceStep`), 팔 때 −0.05 (`_sellPriceStep`), 0.4~3.0으로 자름 | `Markets` |
| 바퀴 복귀 | factor = 1 + (factor−1)×(1−1.0) (`_recoverRatio` 1.0 = 기준가로 완전 복귀), 재고·수요 데이터 값으로 | `Markets.RecoverLap` |
| 거래 세션 | 시장 있는 휴식지 도착 → 정차 유지 "trade-market", [출발](`Depart`) 또는 20초(`_autoDepartSeconds`) 뒤 해제 | `TradeSession` |
| 정산 토스트 | "바퀴 N 정산: +운임 a +판매 b −구매 c −유지비 d = ±e" 4초 (`_settleToastSeconds`, ko `econ_lap_settled`) | `Settlement` + `EconomyInstaller` |

정산 시점: `RingTrack.Arrive`는 `LapCompleted`를 `Arrived`보다 먼저 내고 승객 하차는 `Arrived`에서 일어나므로, 바퀴 번호를 기억했다가 같은 도착의 `Arrived`(거점)에서 정산한다 — 그 도착의 운임이 그 바퀴 장부에 들어간다.

예: 잿더미 쉼터에서 석탄 4개 = 8.4→8, 8.82→9, 9.24→9, 9.66→10, 합 36마르크(factor 1.00→1.20). 녹슨 다리에서 4개 팔면 개당 판매가 11, 11, 10, 10, 합 42마르크(+6, factor 1.00→0.80).

## 휴식지 성격(Markets.json, 배율 / 재고 / 수요)

| 품목 | REST-1 잿더미 쉼터 | REST-2 녹슨 다리 | REST-3 급수탑 쉼터 |
|---|---|---|---|
| coal 석탄 12 | 0.70 / 12 / 3 | 1.20 / 3 / 8 | 1.00 / 5 / 5 |
| iron 철 18 | 0.75 / 10 / 3 | 1.15 / 3 / 8 | 1.00 / 4 / 5 |
| parts 부품 30 | 1.10 / 3 / 5 | 1.45 / 1 / 6 | 1.20 / 2 / 5 |
| food 식량 10 | 1.10 / 4 / 6 | 1.00 / 5 / 5 | 0.70 / 12 / 3 |
| glass 유리 22 | 1.00 / 4 / 4 | 1.10 / 3 / 5 | 0.75 / 10 / 3 |
| brass 황동 26 | 1.00 / 3 / 4 | 1.40 / 1 / 6 | 1.10 / 3 / 5 |

## 데이터 키

- `Assets/Scripts/Economy/Resources/Economy/Economy.json`: `_startBalance` 200, `_trainUpkeep` 20, `_fare` 8, `_bankruptLaps` 3, `_cargoCapacity` 12, `_autoDepartSeconds` 20, `_buyPriceStep` 0.05, `_sellPriceStep` 0.05, `_minPriceFactor` 0.4, `_maxPriceFactor` 3.0, `_sellRatio` 0.8, `_recoverRatio` 1.0, `_settleToastSeconds` 4, 데모 `_demoBankruptStart` 10, `_demoBankruptUpkeep` 50, `_demoTradeStop` REST-1, `_demoTradeGoods` coal, `_demoTradeCount` 4, `_demoSellStop` REST-2, `_demoHubDwellSeconds` 1.
- `Goods.json`: `_goods[]` = `_id`, `_name`, `_basePrice`, `_iconKey`, `_color`.
- `Markets.json`: `_markets[]` = `_stopId`, `_note`, `_goods[]`(`_id`, `_multiplier`, `_stock`, `_demand`).
- `ko.json`(끝에 추가): `econ_lap_settled`, `econ_bankrupt`.

## 데모 `--economy-demo --economy-stage trade|ledger|bankrupt` (로그 `ECON: …`)

- trade: 거점 1초 뒤 출발 → REST-1 세션 열림 → 석탄 4개 구매(`ECON: demo trade REST-1 buy coal x4 ok=True cost=36 …`) → 자동 출발 멈춤, 세션 열린 채 유지.
- ledger: REST-1에서 사고 출발, REST-2에서 전부 판매, 거점 복귀 정산 로그(`ECON: settle lap=1 …`, `ECON: demo ledger checkpoint`).
- bankrupt: 시작 10, 유지비 50 → 정산 −40(1) −90(2) −140(3) → `ECON: bankrupt lap=3 …`, `RUN END: outcome=Defeat … reason=Bankrupt`.
- 기존 데모(`--integration-demo` 등)는 손대지 않았고 경제는 `--economy-demo`가 없으면 데모 동작을 하지 않는다. 플레이 실행은 하지 않았다(캡처 없음 지시) — 로그 확인은 미실행.

## 파일

- 수정: `Assets/Scripts/Contracts/IronRingContracts.cs`(append: `IMoney`, `ICargo`, `IMarkets`, `LedgerKind`, `LedgerEntry`, `ILedger`, `ITradeSession`; `RunLossReason.Bankrupt` 끝에 추가), `Assets/Scripts/Hub/RunState.cs`(`DeclareBankrupt`, `LossReasonText`), `Assets/Scripts/Localization/Resources/ko.json`.
- 새: `Assets/Scripts/Economy/EconomyData.cs`(설정·품목·시장 모델 + `GoodsCatalog`), `MoneyStore.cs`(+ `Cargo`, `Ledger`), `Markets.cs`, `Settlement.cs`(+ `TradeSession`), `EconomyInstaller.cs`, `Demo/EconomyDemoDriver.cs`, `Resources/Economy/{Economy,Goods,Markets}.json`.
- 손대지 않음: `WaypointResolver`·`EncounterPresentationSource`(거래 세션은 별도 정차 유지 사유로 겹쳐 걸어 연결 — 이벤트가 끝나도 세션이 열려 있으면 정차), `Passengers.cs`(운임은 `IPassengers.Disembarked` 구독으로 처리), UI 전부.

## 브리지 결과

- `refresh` → `{"compiling":false,"errors":"","failed":false}`
- `compile-status` → `{"compiling":false,"errors":"","failed":false}`
- `scene-status` → `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}`
- MainCanvas 생성기(`.bridge/rebuild/Trade`/`TopBar`): UI가 codex 범위로 넘어가 이번엔 실행하지 않음.

## 남은 일·주의

- UI(거래 패널·상단 잔고·결과 "파산"·"총수입 N · 총지출 N")는 codex. 핸드오프에 API·버튼 활성 규칙·토스트 겹침 주의(거래 화면은 AlwaysOn 루트로 만들어야 이벤트 토스트가 큐에 밀리지 않음)를 적었다.
- 거래 화면이 붙기 전까지는 휴식지에서 20초 정차 후 자동 출발(UI 없이도 막히지 않음).
- 데모·플레이 로그는 미실행(캡처·플레이 없음 지시). 결과 화면은 현재 파산 시 기존 "패배" 제목 + 기한 본문을 보인다(codex가 교체).
- 휴식지 이벤트의 경제 이벤트 전환(가격 급등 등)은 044 범위.
