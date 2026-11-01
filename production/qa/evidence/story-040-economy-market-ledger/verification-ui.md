# Story 040 UI 검증

구현 및 편집기 검증 완료. 실제 Play 거래 조작과 화면 육안 검증은 미실행(사용자 지시: play-capture 금지, 화면은 사용자가 편집기에서 확인).

## 구현

- 품목 아이콘: `goods_coal`, `goods_iron`, `goods_parts`, `goods_food`, `goods_glass`, `goods_brass`.
- UI 아이콘: `ui_coin`, `ui_cargo`, `ui_depart`.
- `Assets/Art/Generated/icons/`: 투명 64×64 PNG, 기존 SteampunkPalette의 외곽선·명암 사용. Sprite / Point / 무압축 / PPU 64 / mipmap 비활성 확인.
- `Assets/Prefabs/UI/IconCatalog.asset`: 아홉 id → Sprite 등록. `Assets/Scripts/Hud/Ui/IconCatalog.cs` 조회 API. 생성기는 `IronRing.Art.Editor.SpriteGenIcons.Generate`이며 다른 카탈로그 항목은 보존한다.
- [콘택트 시트](icons_contact_sheet.png): 직접 이미지 확인. 위에서 아래, 왼쪽에서 오른쪽 순서: 석탄·철·부품 / 식량·유리·황동 / 동전·화물·출발.
- `TradeScreen.cs`, `TradeRowView.cs`, `TradeScreenBuilder.cs`, `Screens/Trade.prefab`, `SCREEN_IDS` 등록. 여섯 행과 모든 화면 참조가 프리팹에 저장됨. 런타임 UI 생성 없음.
- 760×476 거래 패널. 기준 1280×720에서 패널 상단 y=204, 토스트 y=150~194로 여백 확보. 구매/판매 가격, 재고/수요, 보유 화물, 사기/팔기, 출발, 자동 출발 초 표시.
- Trade는 토스트 대기열에 막히지 않도록 AlwaysOn 표식을 사용하되 Awake에서 숨기고 세션 이벤트로 표시/숨김.
- TopBar: 현재 위치 옆 동전과 잔고. 기존 위치·바퀴·승객·도감 표시 유지.
- RunResult: 파산 시 제목 아래 사유 `econ_bankrupt`, 총수입·총지출 한 줄.
- ko.json 끝에 UI 키 18개 추가. 전체 키 중복 없음. 테마 기존 값 Title 28 / Heading 22 / Normal 18 / Small 14 및 버튼 36 유지.

## 바인딩

- 서비스는 `GameServices.Ready` 후 조회. 경제 서비스가 없는 독립 데모에서는 거래 화면과 잔고 표시를 숨김.
- `ITradeSession.Opened/Closed`, `IsOpen`, `StopId`, `SecondsLeft`, `Depart()`.
- `IMoney.Balance/Changed`, `ICargo.Used/Capacity/CountOf/Changed`, `IMarkets.Price/SellPrice/Stock/Demand/BuyCost/Changed`.
- 사기/팔기는 `TryBuy(stop, goods, 1)` / `TrySell(stop, goods, 1)`만 호출. 화물 직접 변경 없음.
- 사기: 열린 세션, 재고 1 이상, 화물 여유, BuyCost 유효 및 잔고 이내. 팔기: 열린 세션, 보유 1 이상, 수요 1 이상. 비활성 버튼 글자는 theme.Muted.
- 품목 이름·아이콘 키는 `EconomyInstaller.Goods.NameOf/Get(...).IconKey`. 편집기 미리보기는 한국어 UI 키 사용.
- 결과는 `IRunLossInfo.LastLossReason == Bankrupt`, `ILedger.TotalIncome/TotalExpense` 사용.
- 정산 및 이벤트 토스트 로직은 변경하지 않음.

## 브리지 결과

| 작업 | 요청 id | 결과 |
|---|---|---|
| refresh | 830b5da02aa540cd8603a4204e45dbb0 | ok=true, compiling=false, failed=false, errors 빈 문자열 |
| compile-status | 3965783d586d429188c1cc07b5565ca6 | 최초 CLI 대기는 30초 초과, 이후 같은 요청 결과 파일에서 ok=true, failed=false 확인 |
| SpriteGenIcons.Generate | 100e6177265b46d7a46b2751cda07985 | ok=true, 9 sprites/catalog/contact sheet saved |
| BuildMainCanvas.Generate | 902879a9a009410fbae69d6f1079c8ee | ok=true, TopBar/RunResult/Trade 재생성, canvasChanged=True, sceneChanged=False |
| BuildMainCanvas.Generate 재실행 | da5ca394fe1f48ada8cddbfe18341e0b | ok=true, canvasChanged=False, sceneChanged=False |
| scene-status | 96894a16395a4d9eb767cc88b219e385 | Main.unity, isDirty=false, playing=false |

생성된 Trade 프리팹에서 6개 품목 행과 비어 있지 않은 직렬화 참조 확인. TopBar 잔고·동전·카탈로그, RunResult 재무 라벨 참조 확인. Main 씬 EventSystem 존재, 기존 MainCanvas GraphicRaycaster 유지.

## 범위 및 제한

- `docs/README.md`는 현재 경로에 없음. 제공된 charter, 040 handoff, story-040 및 game-brief를 구현 스펙으로 사용.
- 경제·계약·거점 등 다른 에이전트 소유 코드는 수정하지 않음. 씬 저장은 지정된 기존 빌더/브리지를 사용.
- git 명령, 자동 테스트 코드, Play 진입, play-capture, Unity 새 실행/종료 없음.
- 거래 버튼 실제 클릭, 세션 개폐와 자동 출발의 실행 화면, 잔고 변경 및 파산 화면은 사용자 편집기 검증이 남음. 컴파일·에셋 검증을 실제 플레이 검증으로 간주하지 않음.
