# 스토리 041 검증: 생산 건물·거점 창고·타일 비용 (로직·데이터·월드 표현)

- 일자: 2026-11-02
- 범위: 로직, 데이터, 월드 표현(Claude 담당). HUD와 UI(창고 패널, 핫바 가격)는 codex가 맡으며 `production/handoffs/codex-ui-041.md`의 "로직 API" 절에 정리했다.
- 검증 방식: 파이프라인만 돌렸다(사용자 지시로 캡처와 플레이 실행은 하지 않음).

## 브리지 결과
| 명령 | 결과 |
|---|---|
| `refresh` 1차 | failed:true. CS0136(`BuildingView.cs`에서 지역 변수 `top` 이름이 겹침). 이름을 `statusTop`으로 바꿔 고침 |
| `refresh` 2차 | ok, `failed:false`, errors 없음 |
| `execute-method Story012Build.Generate` 1회 | ok |
| `execute-method Story012Build.Generate` 2회 | ok(멱등: 두 번째 실행에서 바뀐 에셋 없음) |
| `scene-status` | `isDirty:false`, `Assets/Scenes/Main.unity`, playing:false |

Generate 결과는 다음과 같다.
- `Assets/Prefabs/Interior/BuildingStatus.prefab`을 새로 만들었다(SpriteRenderer와 BuildingStatusIcon. `_icons`가 `Assets/Prefabs/UI/IconCatalog.asset`을 참조).
- `InteriorCatalog.asset`에 `_buildingStatusPrefab`과 `_statusLift`가 추가됐다.

## 생산 표 (`Habitats/Resources/Habitats/HabitatTypes.json`)
| 종류 | 이름 | 생산 | 소비 | 노동자 | 유지비 | 증기 필요 | 증기 공급 | 물 필요 | 물 공급 | 주거 | 빛 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| lamp_street | 가스등 거리 | - | - | 1 | 2 | | | | | 0 | 3 |
| coal_yard | 석탄 야적장 | 석탄 3 | - | 2 | 4 | | | | | 0 | 0 |
| cobble_plaza | 벽돌 광장 | - | - | 0 | 2 | | | | | 2 | 1 |
| grand_cogworks | 대공방 | 부품 3 | 철 2 | 5 | 12 | O | | | | 0 | 0 |
| boiler_room | 보일러실 | - | 석탄 1 | 2 | 5 | | O | | | 0 | 0 |
| pipe_gallery | 배관 회랑 | - | 석탄 1 | 1 | 4 | | O | | | 0 | 0 |
| cogworks | 톱니공방 | 부품 1 | 철 1 | 3 | 6 | O | | | | 0 | 0 |
| iron_depot | 철골 창고 | 철 2 | - | 2 | 5 | | | | | 0 | 0 |
| cistern | 저수조 | - | - | 1 | 3 | | | | O | 0 | 0 |
| greenhouse | 온실 | 식량 2 | - | 2 | 4 | | | O | | 0 | 0 |
| barracks | 막사촌 | - | - | 0 | 3 | | | | | 6 | 0 |
| toolshop | 공구상 | 부품 1 | 철 1 | 2 | 5 | | | | | 0 | 0 |
| telegraph_line | 전신선 | - | - | 1 | 3 | | | | | 0 | 2 |
| water_tower | 급수탑 | - | - | 1 | 3 | | | | O | 0 | 0 |
| lamp_road | 가로등길 | - | - | 1 | 2 | | | | | 0 | 4 |
| engine_room | 증기 기관실 | 부품 1 | 석탄 1 | 3 | 7 | | O | | | 0 | 0 |
| power_house | 발전소 | 황동 1 | 석탄 2 | 3 | 8 | | O | | | 0 | 1 |
| repair_bay | 정비창 | 황동 1 | 부품 1, 철 1 | 3 | 7 | O | | | | 0 | 0 |
| blast_furnace | 용광로 | 유리 2, 황동 1 | 석탄 2, 철 1 | 4 | 10 | O | | | | 0 | 0 |

규칙(`Production.cs`)은 다음과 같다.
- 판정 순서: 노동자, 증기, 물, 입력 순으로 본다. 노동 공급이 붙기 전(042 이전)에는 노동자가 늘 충분하다.
- 건물 처리 순서: 입력 없는 생산 건물, 증기·물 공급 건물, 나머지 순이며, 같은 묶음 안에서는 `CompletedAt` 순서를 따른다.
- 가동 중인 건물은 입력을 빼고 출력을 넣는다. 창고 상한을 넘는 출력은 버리고 로그를 남긴다.
- 가동 중인 건물의 유지비 합은 장부에 `Upkeep` "건물 유지비" 한 줄로 적고, 열차 유지비보다 먼저 정산한다.

## 타일 비용 표 (`Items/Resources/Items/ItemCatalog.json` `_price`)
| 아이템 | 가격 |
|---|---|
| tile_coal, tile_cobble | 4 |
| tile_gaslamp, tile_bunk, tile_bench | 5 |
| tile_pipe, tile_girder, tile_wire | 6 |
| tile_tank | 7 |
| tile_gear, tile_glass | 8 |
| tile_boiler | 10 |
| tile_chimney | 12 |
| 장식 15종(lantern … deco_weeds) | 1 |

환급액은 round(가격 × `_refundRatio`)이고 `_refundRatio`는 1.0이다. 배치 결제와 환급은 `IMoney.TrySpend`와 `TryGain`으로 처리하며 장부에는 적지 않는다.

## 데이터 키
- `HabitatTypes.json`의 각 종류: `_produces[{_goodsId,_amount}]`, `_consumes[...]`, `_workers`, `_upkeep`, `_needsSteam`, `_providesSteam`, `_needsWater`, `_providesWater`, `_housing`, `_light`.
- `ItemCatalog.json`의 각 아이템: `_price`.
- `Economy.json`:
  - 일반 키: `_warehouseCapacity` 60, `_refundRatio` 1.0, `_warehouseHubId` "hub-1".
  - 데모 키: `_demoSeedGoods` "iron", `_demoSeedCount` 2, `_demoTicks` 2, `_demoLoadGoods` "coal", `_demoLoadCount` 3.
- `ko.json`(뒤에 덧붙임): `econ_no_funds` "잔고 부족".
- `InteriorCatalog.asset`: `_buildingStatusPrefab`, `_statusLift` 0.75.

## 파일
- 계약: `Assets/Scripts/Contracts/IronRingContracts.cs`에 `IWarehouse`, `ITransfer`, `IPlacementCost`, `BuildingStatus`, `IProductionStatus`를 덧붙였다.
- 경제:
  - `Assets/Scripts/Economy/Warehouse.cs`(신규: Warehouse, Transfer, PlacementCost)
  - `Production.cs`(신규)
  - `Demo/ProductionDemoDriver.cs`(신규)
  - `EconomyData.cs`, `EconomyInstaller.cs`
  - `Settlement.cs`(`Producing` 훅)
  - `Resources/Economy/Economy.json`
- 서식지: `Assets/Scripts/Habitats/HabitatCatalog.cs`(`HabitatGoodsAmount`, 생산 속성), `Resources/Habitats/HabitatTypes.json`.
- 아이템: `Assets/Scripts/Items/ItemPlacement.cs`(`TryCharge`, `CanAfford`, `Refund` 훅), `ItemCatalog.cs`(`Price`), `Resources/Items/ItemCatalog.json`.
- 표현:
  - `Assets/Scripts/Presentation/Interior/PlacementGhost.cs`(잔고 검사, 잔고 부족 토스트)
  - `BuildingView.cs`(상태 아이콘)
  - `BuildingStatusIcon.cs`(신규)
  - `InteriorCatalog.cs`
  - `Editor/Story012Build.cs`(`EnsureStatusPrefab`)
- 생성 에셋: `Assets/Prefabs/Interior/BuildingStatus.prefab`, `InteriorCatalog.asset`.
- 문서: `production/handoffs/codex-ui-041.md`의 "로직 API" 절.

## 데모
`--production-demo --production-stage build|tick|load`
- build: 내부 화면을 열고 석탄 야적장, 보일러실, 톱니공방을 놓는다.
- tick: 창고에 철 2를 넣고 정산을 2회 돌린다.
- load: 석탄 3을 화물칸에 싣는다.

로그 접두어는 `PROD:`다. 이번에는 실행하지 않았다(파이프라인 검증만 함).

1회차 정산의 기대 흐름은 다음과 같다.
- 석탄 야적장이 석탄 3을 만든다.
- 보일러실이 석탄 1을 쓰고 증기를 공급한다.
- 톱니공방이 철 1과 증기로 부품 1을 만든다.
- 창고에는 석탄 2, 철 1, 부품 1이 남는다.

## 남은 문제
- 데모를 플레이 모드에서 실제로 돌려 보지 않았다(지시 범위가 파이프라인 검증까지).
- 철거한 건물을 되돌릴 때 잔고가 모자라면 타일 일부만 다시 놓인다(돈이 되는 만큼만).
- 건물 유지비는 가동한 건물에만 붙는다(스토리 지시대로). 멈춘 건물에도 물릴지는 디자인 결정이 필요하다.
- 배치 결제와 환급은 장부에 적지 않는다. 바퀴 손익에 넣을지는 디자인 결정이 필요하다.
- 창고 UI 패널과 핫바 가격 표시는 codex가 맡는다.
- `status_no_water` 아이콘 스프라이트가 없으면 `status_no_input`으로 대신 표시한다.
