# codex UI 작업 040: 경제 기반 — 거래 패널·상단 잔고·결과 재무 줄·품목 아이콘

먼저 `production/handoffs/codex-ui-charter.md`(역할·경로·규칙)를 읽는다. 스펙: `production/epics/iron-ring-train/story-040-economy-market-ledger.md`, 브리프 `design/game-brief.md`.

## UI 수용 기준
1. **품목 아이콘 6개** `Assets/Art/Generated/icons/goods_<id>.png`(64×64): coal 석탄(검은 덩어리·황동 테), iron 철(철괴), parts 부품(톱니+볼트), food 식량(통조림/빵), glass 유리(유리판·청록 반사), brass 황동(황동 주괴). 상태·버튼 아이콘: `ui_coin.png`(마르크 동전), `ui_cargo.png`(화물 상자), `ui_depart.png`(출발 화살표). `IconCatalog.asset`에 등록. 콘택트 시트 `production/qa/evidence/story-040-economy-market-ledger/icons_contact_sheet.png`.
2. **거래 패널** 화면 id `Trade`(`Assets/Scripts/Hud/Trade/TradeScreen.cs`, `TradeRowView.cs`, 빌더 `TradeScreenBuilder.cs`, 프리팹 `Screens/Trade.prefab`, `SCREEN_IDS`에 "Trade" 추가): 화면 중앙 패널(폭 ~760). 제목 "<휴식지 이름> 시장", 부제 "잔고 N마르크 · 화물 N/12". 품목 6줄: 아이콘·이름·가격(마르크)·"재고 N · 수요 N"·"내 화물 N"·[사기]·[팔기] 버튼(살 수 없으면/팔 것 없으면 비활성·흐리게). 아래 [출발] 큰 버튼. 거래 세션이 열리면 보이고 닫히면 숨는다. 이벤트 토스트는 기존 `ToastScreen`이 위에 뜸(겹치지 않게 패널 상단 여백).
3. **상단 상태줄**(`TopBarScreen` + 빌더, 표식 `.bridge/rebuild/TopBar`): 왼쪽 "현재 위치" 옆에 동전 아이콘 + "잔고 N마르크". 잔고 변동 시 갱신(`IMoney.Changed`).
4. **정산 토스트**: 로직이 `ToastScreen.Show`로 띄우므로 UI 작업 없음(문구 키는 로직 쪽).
5. **결과 화면**(`RunResultScreen` + 빌더, 표식 `.bridge/rebuild/RunResult`): 제목에 패배 사유("파산")를 쓰고, 한 줄 "총수입 N · 총지출 N"(장부 합계).
6. 모든 문구 한국어, `ko.json` 끝에 `ui_trade_*`, `ui_topbar_balance`, `ui_result_finance` 키 추가.

## 로직 API (Claude 에이전트가 채움)

작성 2026-11-02, 컴파일 통과(브리지 refresh failed:false). 계약은 `Assets/Scripts/Contracts/IronRingContracts.cs` 끝 "경제 기반(스토리 040)" 절, 구현은 `Assets/Scripts/Economy/`. 모든 서비스는 `GameServices.Get<T>()`(네임스페이스 `IronRing.Contracts.Presentation`)로 얻는다. `MainRoot`가 `GameServices.Composing`에서 `EconomyInstaller`를 설치하므로 **`GameServices.Ready` 이후**에 얻는다(기존 화면처럼 `Ready += Create`). 선로가 없는 독립 데모 씬에서는 등록되지 않으니 null이면 조용히 숨긴다.

### 서비스와 멤버
- `IMoney` — `int Balance`(0 아래 가능), `bool TryGain(int, string reason)`, `bool TrySpend(int, string reason)`, `void Charge(int, string reason)`(강제 지출), `event Action Changed`.
- `ICargo` — `int Capacity`(12), `int Used`, `int CountOf(string goodsId)`, `bool TryLoad(id, n)`, `bool TryUnload(id, n)`, `event Action Changed`. **UI는 TryLoad/TryUnload를 직접 부르지 말고 시장 TryBuy/TrySell만 쓴다**(돈·장부가 같이 움직인다).
- `IMarkets` — `IReadOnlyList<string> GoodsIds`(행 순서), `StopIds`, `bool HasMarket(stopId)`, `int Price(stop, id)`(1개 살 때), `int SellPrice(stop, id)`(1개 팔 때), `int Stock(stop, id)`(시장 재고), `int Demand(stop, id)`(시장 수요), `int BuyCost(stop, id, n)`/`int SellValue(stop, id, n)`(n개 합계, 불가면 -1), `bool TryBuy(stop, id, n)`, `bool TrySell(stop, id, n)`, `event Action Changed`(거래·바퀴 복귀).
- `ILedger` — `IReadOnlyList<LedgerEntry> Entries`(`Lap`, `Kind`(`LedgerKind.Fare/Sale/Purchase/Upkeep`), `Amount` 부호 있음, `Note`), `int CurrentLap`, `int Total(lap, kind)`, `int TotalIncome`, `int TotalExpense`(둘 다 양수), `event Action<int lap, int net> LapSettled`.
- `ITradeSession` — `bool IsOpen`, `string StopId`(REST-1/2/3), `float SecondsLeft`(자동 출발까지), `event Action Opened`, `event Action Closed`, `void Depart()`([출발] 버튼). 열려 있는 동안 열차는 정차 유지, 닫히면 자동으로 출발한다. 자동 출발 `Economy.json _autoDepartSeconds`(20초).
- 파산: `GameServices.Get<IRunLossInfo>().LastLossReason == RunLossReason.Bankrupt`(Outcome은 `Defeat`). 사유 문구는 `(GameServices.Get<IRunResult>() as IronRing.Hub.RunState)?.LossReasonText` = "파산"(ko.json `econ_bankrupt`).

### 버튼 활성 규칙(1개 단위)
- [사기]: `markets.Stock(stop,id) >= 1 && cargo.Used < cargo.Capacity && money.Balance >= markets.Price(stop,id)` (= `BuyCost(stop,id,1)`이 0 이상이고 잔고 이하).
- [팔기]: `cargo.CountOf(id) >= 1 && markets.Demand(stop,id) >= 1`.
- 갱신: `IMoney.Changed`, `ICargo.Changed`, `IMarkets.Changed`, `ITradeSession.Opened/Closed`.

### 호출 예시
```csharp
ITradeSession session = GameServices.Get<ITradeSession>();
IMarkets markets = GameServices.Get<IMarkets>();
session.Opened += () => { /* UiScreens.Show("Trade") */ };
session.Closed += () => { /* UiScreens.Hide("Trade") */ };
string stop = session.StopId;                         // "REST-1"
foreach (string id in markets.GoodsIds) { int p = markets.Price(stop, id); int sp = markets.SellPrice(stop, id); }
buyButton.onClick.AddListener(() => markets.TryBuy(session.StopId, "coal", 1));
sellButton.onClick.AddListener(() => markets.TrySell(session.StopId, "coal", 1));
departButton.onClick.AddListener(session.Depart);
```

### 품목(Goods.json, 데이터 순서)
| id | 이름 | 기준가 | 아이콘 키 | 자리표시 색 |
|---|---|---|---|---|
| coal | 석탄 | 12 | goods_coal | #3a3f4a |
| iron | 철 | 18 | goods_iron | #7d8590 |
| parts | 부품 | 30 | goods_parts | #995636 |
| food | 식량 | 10 | goods_food | #4f7a3a |
| glass | 유리 | 22 | goods_glass | #5b7983 |
| brass | 황동 | 26 | goods_brass | #d6a053 |

이름·아이콘 키·색은 `IronRing.Economy.EconomyInstaller.Goods`(`GoodsCatalog`: `NameOf(id)`, `Get(id).IconKey`, `ColorOf(id)`)에서 읽는다(계약 밖의 정적 접근, 설치 전 null). 휴식지 이름은 기존 ko.json `rest_1/rest_2/rest_3`(`stopId.ToLowerInvariant().Replace('-', '_')`).

### 토스트 문구 키(로직이 띄움, UI 작업 없음)
- `econ_lap_settled`: "바퀴 {0} 정산: +운임 {1} +판매 {2} −구매 {3} −유지비 {4} = {5}" — 거점 도착(정산) 때 `ToastScreen.Show`.
- `econ_bankrupt`: "파산" — 결과 화면 제목에 쓸 사유.

### 주의(토스트 겹침)
`ToastScreen.Show`는 **AlwaysOn이 아닌 화면이 하나라도 열려 있으면 토스트를 큐에 넣고 닫힌 뒤 띄운다**. 거래 패널을 보통 상황 화면(`ScreenRoot("Trade", false)`)으로 만들면 휴식지 이벤트 토스트가 패널이 닫힐 때까지 밀린다(AC "이벤트는 거래 패널 위 토스트"를 못 지킴). 권장: `UiKit.ScreenRoot("Trade", true)`로 만들고 `TradeScreen`이 초기화 때·`Closed`에서 직접 `Hide`, `Opened`에서 `Show`(AlwaysOn 화면은 Awake에서 꺼지지 않으므로 초기 Hide 필요).

## 요청(codex → 코디네이터)
(필요한 API나 막힌 것을 여기에)
