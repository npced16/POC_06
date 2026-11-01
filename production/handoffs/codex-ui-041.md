# codex UI 작업 041: 생산 건물 — 창고 패널·핫바 타일 가격·건물 상태 아이콘

먼저 `production/handoffs/codex-ui-charter.md`. 스펙: `production/epics/iron-ring-train/story-041-production-buildings.md`.

## UI 수용 기준
1. **창고 패널** 화면 id `Warehouse`(`Assets/Scripts/Hud/Warehouse/WarehouseScreen.cs`, `WarehouseRowView.cs`, 빌더, 프리팹, `SCREEN_IDS`): 거점 내부 화면에서 핫바 오른쪽 "창고" 버튼(또는 W 키)으로 열고 닫는 패널. 제목 "황동골 창고 · N/60", 품목 6줄: 아이콘·이름·창고 수량·열차 화물 수량·[싣기]·[내리기](1개 단위, 불가하면 비활성). 아래 "화물 N/12". 출발하면 자동으로 닫힘.
2. **핫바 가격**: 핫바 칸(`HotbarCellView`)의 이름 아래 작은 글씨로 "N마르크"(타일 가격, `ItemCatalog` `_price`), 잔고가 모자라면 칸을 붉게·흐리게. 팔레트 패널도 같은 표시.
3. **건물 상태 아이콘** `Assets/Art/Generated/icons/status_<id>.png`(32×32 권장, 64 캔버스 가능): `status_running`(톱니 회전 느낌·초록), `status_no_workers`(사람 실루엣에 빗금·노랑), `status_no_input`(빈 상자·주황), `status_no_steam`(김 빠진 보일러·파랑). `IconCatalog.asset`에 등록. 월드 위 표시는 Claude(`BuildingView`)가 `IconCatalog`에서 id로 꺼내 쓴다 — codex는 아이콘만.
4. 문구는 `ko.json` 끝에 `ui_warehouse_*`, `ui_hotbar_price` 등.

## 로직 API (Claude 에이전트가 채움)
(비어 있으면 아이콘·빈 레이아웃부터)

## 요청(codex → 코디네이터)
