# codex UI 작업 041: 생산 건물 — 창고 패널·핫바 타일 가격·건물 상태 아이콘

먼저 `production/handoffs/codex-ui-charter.md`. 스펙: `production/epics/iron-ring-train/story-041-production-buildings.md`.

## UI 수용 기준
1. **창고 패널** 화면 id `Warehouse`(`Assets/Scripts/Hud/Warehouse/WarehouseScreen.cs`, `WarehouseRowView.cs`, 빌더, 프리팹, `SCREEN_IDS`): 거점 내부 화면에서 핫바 오른쪽 "창고" 버튼(또는 W 키)으로 열고 닫는 패널. 제목 "황동골 창고 · N/60", 품목 6줄: 아이콘·이름·창고 수량·열차 화물 수량·[싣기]·[내리기](1개 단위, 불가하면 비활성). 아래 "화물 N/12". 출발하면 자동으로 닫힘.
2. **핫바 가격**: 핫바 칸(`HotbarCellView`)의 이름 아래 작은 글씨로 "N마르크"(타일 가격, `ItemCatalog` `_price`), 잔고가 모자라면 칸을 붉게·흐리게. 팔레트 패널도 같은 표시.
3. **건물 상태 아이콘** `Assets/Art/Generated/icons/status_<id>.png`(32×32 권장, 64 캔버스 가능): `status_running`(톱니 회전 느낌·초록), `status_no_workers`(사람 실루엣에 빗금·노랑), `status_no_input`(빈 상자·주황), `status_no_steam`(김 빠진 보일러·파랑). `IconCatalog.asset`에 등록. 월드 위 표시는 Claude(`BuildingView`)가 `IconCatalog`에서 id로 꺼내 쓴다 — codex는 아이콘만.
4. 문구는 `ko.json` 끝에 `ui_warehouse_*`, `ui_hotbar_price` 등.

## 로직 API (Claude 에이전트가 채움)
작성 2026-11-02, 컴파일 통과(브리지 refresh failed:false). 계약은 `Assets/Scripts/Contracts/IronRingContracts.cs` 끝 "생산 건물·거점 창고·타일 비용(스토리 041)" 절, 구현은 `Assets/Scripts/Economy/Warehouse.cs`(Warehouse·Transfer·PlacementCost)·`Production.cs`. 모두 `GameServices.Get<T>()`로 얻는다.
- `IWarehouse`·`ITransfer`·`IPlacementCost`는 040과 같이 `GameServices.Composing`에서 등록된다.
- `IProductionStatus`는 **`GameServices.Ready`에서** 등록된다(서식지·배치 서비스가 같은 Composing에 생겨서). 화면은 `Ready += Create` 안에서 얻되, 얻는 순서 때문에 null이 나올 수 있으니 처음 쓸 때 다시 `Get`한다. 선로가 없는 독립 데모 씬에서는 등록되지 않으므로 null이면 조용히 숨긴다.

### 서비스와 멤버
- `IWarehouse`: `int Capacity`(60, `Economy.json _warehouseCapacity`), `int CountOf(string goodsId)`, `int Total`, `bool TryAdd(id, n)`, `bool TryTake(id, n)`, `event Action Changed`. **UI는 TryAdd/TryTake를 직접 부르지 않는다**(생산 로직이 쓴다). 표시만 한다.
- `ITransfer`: `bool CanTransfer`, `bool TryLoad(id, n)`(창고에서 화물칸으로), `bool TryUnload(id, n)`(화물칸에서 창고로). 원자적이며 돈과 장부는 움직이지 않는다.
- `IPlacementCost`: `int PriceOf(itemId)`(배치 가격), `int RefundOf(itemId)`(회수 환급), `bool CanAfford(itemId)`(지금 잔고로 놓을 수 있는지).
- `IProductionStatus`: `string HubId`("hub-1"), `BuildingStatus Status(int clusterIndex)`(clusterIndex = `GameServices.Get<IHabitatClusters>().CompletedAt(HubId)`의 순서. 지금 창고 상태로 다음 정산을 미리 판정한 값), `string IconId(BuildingStatus)`, `event Action Changed`(창고·건물·정산이 바뀔 때), `event Action<int> Produced`(정산 생산 직후, 바퀴 번호).
- `BuildingStatus` enum: `Running`, `NoWorkers`(042 전에는 나오지 않음), `NoInput`, `NoSteam`, `NoWater`.

### 창고 패널을 열 수 있는 때
- **거점 내부 화면이 창고 거점에서 열려 있을 때만** 옮길 수 있다: `IViewMode.InteriorOpen && IViewMode.InteriorHubId == "hub-1"`. 이 조건이 `ITransfer.CanTransfer`와 같다. 패널은 `IViewMode.InteriorChanged(true/false)`에 맞춰 보이거나 숨긴다.
- [싣기] 활성 조건: `transfer.CanTransfer && warehouse.CountOf(id) >= 1 && cargo.Used < cargo.Capacity`.
- [내리기] 활성 조건: `transfer.CanTransfer && cargo.CountOf(id) >= 1 && warehouse.Total < warehouse.Capacity`.
- 갱신 시점: `IWarehouse.Changed`, `ICargo.Changed`, `IViewMode.InteriorChanged`.
- 품목 행 순서·이름·아이콘은 040과 같다(`IMarkets.GoodsIds`, `EconomyInstaller.Goods`).

### 핫바 칸 가격
- `GameServices.Get<IPlacementCost>().PriceOf(itemId)`: 타일은 4~12마르크, 장식은 1마르크. 잔고가 모자란 칸은 `CanAfford(itemId) == false`이므로 흐리게 표시한다. 갱신 시점은 `IMoney.Changed`.
- 결제와 환급은 로직이 한다. 배치(`IItemPlacement.TryPlace`)하면 잔고에서 빠지고, 회수·철거·되돌리기(`TryPickUp`)하면 `RefundOf`만큼 돌아온다. 잔고가 모자라면 고스트가 붉어지고, 클릭하면 토스트 `econ_no_funds`("잔고 부족")가 뜬다(PlacementGhost가 띄우므로 UI 작업은 없다).

### 상태 아이콘 id(IconCatalog)
- `status_running`, `status_no_workers`, `status_no_input`, `status_no_steam`, `status_no_water`.
- `status_no_water` 스프라이트가 없으면 월드 아이콘은 `status_no_input`을 대신 쓴다.
- 월드 건물 위 아이콘은 `BuildingStatus.prefab`(Assets/Prefabs/Interior, Claude 소유)이 `Assets/Prefabs/UI/IconCatalog.asset`을 참조해 가동이 아닐 때만 보인다. 카탈로그의 id를 바꾸면 알려 달라.

### 정산 토스트
`econ_lap_settled`의 {4} 유지비에 건물 유지비(장부 `LedgerKind.Upkeep`, 메모 "건물 유지비")가 함께 들어간다. 건물 유지비는 그 바퀴에 가동한 건물에만 붙는다.

## 요청(codex → 코디네이터)
