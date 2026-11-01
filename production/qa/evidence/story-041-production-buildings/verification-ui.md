# Story 041 UI 검증

UI 구현 및 브리지 편집기 검증 완료. Play 모드 실제 조작·화면 검증은 미실행(사용자 지시: 캡처 없음, 사용자가 편집기에서 확인).

## 구현 목록

- `Assets/Scripts/Hud/Warehouse/WarehouseScreen.cs`, `WarehouseRowView.cs`.
- `Assets/Scripts/Hud/Editor/Screens/WarehouseScreenBuilder.cs`, `Assets/Prefabs/UI/Screens/Warehouse.prefab`.
- `BuildMainCanvas.SCREEN_IDS`에 Warehouse 등록, MainCanvas 자식 프리팹 및 화면 목록 반영.
- 창고 패널 760×420, 품목 여섯 행(석탄·철·부품·식량·유리·황동), 거점 이름과 창고 총량/상한, 품목별 창고/열차 수량, 싣기/내리기, 화물 총량/상한, 닫기 버튼.
- 핫바 오른쪽 `창고 (W)` 버튼. W로 열기/닫기, Esc 또는 닫기 버튼으로 닫기. 내부 이탈·이송 불가·정차에서 출발로 전환 시 자동 닫기.
- 창고를 열면 기존 팔레트와 배치 선택 해제. 창고가 열려 있을 때 핫바 숫자키 입력 차단. 팔레트/핫바 슬롯 선택 시 창고 닫기.
- 핫바/팔레트 부품에 이름 아래 `N마르크` 추가. 슬롯 66×98, 팔레트 항목 78×98, 핫바 862×114로 가격 줄과 창고 버튼 공간 확보. 이름/가격 줄 각각 21 높이, Small 14 사용.
- 잔고 부족 시 아이콘·이름·가격·테두리를 붉고 흐리게 표시. 부족 표시 색은 HotbarCellView Inspector에서 편집 가능.
- 상태 아이콘: `status_running`(초록 톱니와 회전 화살표), `status_no_workers`(노랑 사람과 빗금), `status_no_input`(주황 빈 상자), `status_no_steam`(파랑 보일러와 꺼진 증기).
- `SpriteGenIcons.GenerateStatus`로 네 아이콘과 [콘택트 시트](icons_contact_sheet.png) 생성. 콘택트 시트를 직접 열어 확인했으며 순서는 위 목록과 같다.
- `Assets/Art/Generated/icons/status_*.png`: 64×64 RGBA 투명 PNG, Sprite / PPU64 / Point / 무압축 / mipmap 없음 확인.
- IconCatalog에 기존 9개를 보존하고 4개를 추가해 총 13개 고유 id 등록. GUID 유지.
- ko.json 끝에 `ui_warehouse_*` 8개와 `ui_hotbar_price` 추가. 최종 파일에서 중복 키 없음 확인.

## 바인딩 API

초기에는 handoff API 절이 비어 있어 아이콘과 빈 데이터 레이아웃부터 작성. 실제 계약 코드로 연결 후, Claude가 채운 최종 API 절과 대조했다.

- `GameServices.Ready` 후 `IWarehouse`, `ICargo`, `ITransfer`, `IViewMode`, `ITrackRuntime` 연결. 서비스가 없는 독립 데모에서는 창고를 열지 않는다.
- 창고 열기: `IViewMode.InteriorOpen && ITransfer.CanTransfer`. 거점 이름은 `InteriorHubId`의 지역화 키로 조회.
- 수량/상한: `IWarehouse.CountOf/Total/Capacity`, `ICargo.CountOf/Used/Capacity`.
- 갱신: `IWarehouse.Changed`, `ICargo.Changed`, `IViewMode.InteriorChanged`.
- 싣기: `ITransfer.TryLoad(goodsId, 1)`만 호출. CanTransfer, 창고 수량 1 이상, 화물 여유 조건으로 활성화.
- 내리기: `ITransfer.TryUnload(goodsId, 1)`만 호출. CanTransfer, 화물 수량 1 이상, 창고 여유 조건으로 활성화.
- `TryAdd/TryTake` 또는 화물 직접 변경 호출 없음.
- 품목 이름/아이콘: `EconomyInstaller.Goods.NameOf/Get(...).IconKey` 및 IconCatalog.Get.
- 핫바 가격: `IPlacementCost.PriceOf/CanAfford`, `IMoney.Changed`; 독립 데모의 가격 조회는 `ItemDefinition.Price` 사용. 편집기 미리보기도 ItemCatalog 데이터를 읽는다.
- ScreenTicker로 화면이 꺼져 있어도 W/출발 상태 확인. 파괴 시 상태 이벤트·버튼·ticker 등록 해제.
- `status_no_water`는 요청 범위 밖이며 API 문서대로 Claude 소유 월드 표시에서 `status_no_input` 대체 사용. 월드 BuildingView·BuildingStatus.prefab은 수정하지 않음.

## 브리지 검증

| 작업 | 요청 id | 결과 |
|---|---|---|
| 초기 refresh | 58f07556e4024481a97d36163b5b2d19 | ok=true, compiling=false, failed=false, errors 빈 문자열 |
| 최종 refresh | 3cf737bcc29b45cdac349ebcfcdb1d8f | ok=true, compiling=false, failed=false, errors 빈 문자열 |
| SpriteGenIcons.GenerateStatus | 05bf57c2af0c42f89af023484ffd0c58 | ok=true, 4 sprites/catalog/contact sheet saved |
| BuildMainCanvas.Generate | e558d0b967044cdda2696fb6f2061548 | ok=true, HotbarSlot/PaletteItem/Hotbar/Warehouse 재생성; canvasChanged=True, sceneChanged=False |
| BuildMainCanvas.Generate 재실행 | 38a01c62cb6a45348cc163f72ac29316 | ok=true, canvasChanged=False, sceneChanged=False |
| scene-status | 8534895ae7864e94a0324cd465eedea7 | Main.unity, isDirty=false, playing=false |

생성된 에셋을 읽어 Warehouse의 6개 품목 행과 비어 있지 않은 직렬화 참조, Hotbar의 창고 버튼 참조, HotbarSlot/PaletteItem의 가격 라벨 참조 확인. Hotbar에 데이터 가격(예: 5/6/8마르크)이 저장된 것을 확인.

## 검증 범위

- 실제 버튼 클릭, W/Esc 조작, 적재/하역 후 표시 갱신, 잔고 부족 색상, 출발 자동 닫힘의 Play 동작은 미검증. 컴파일·에셋 참조 검증으로 한정한다.
- Play 진입·캡처·git 명령·테스트 코드 작성 없음. Unity를 새로 실행하거나 닫지 않음.
- docs/README.md는 앞선 작업에서 부재 확인. charter와 story-041 및 채워진 codex-ui-041 API를 기준으로 구현.
