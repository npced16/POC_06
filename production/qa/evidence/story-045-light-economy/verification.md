# 스토리 042 → 045 가벼운 경제: 검증 기록

작성 2026-11-02. 사용자 결정(06:40, 코디네이터 전달): "가볍게, 마을 키우기만". 042(주민·노동·만족도·등급)를 진행하던 중 범위가 바뀌었다. 교역·화물·시장·생산 체인·창고·노동·식량·만족도는 빼고, 타일 배치 → 건물 → 주민·NPC → 수입(마르크)·번영 → 마을 등급만 남겼다. 캡처는 하지 않았다(사용자 지시). 검증은 파이프라인으로 했다.

## 1. 보관으로 옮긴 것 (`archive/economy-trade-2026-11-02/`, Assets 밖, git mv)
| 원래 경로 | 보관 경로 |
|---|---|
| `Assets/Scripts/Economy/Markets.cs` | `Economy/Markets.cs` |
| `Assets/Scripts/Economy/Warehouse.cs`(Warehouse·Transfer) | `Economy/Warehouse.cs`. PlacementCost는 `Assets/Scripts/Economy/PlacementCost.cs`로 따로 떼어 남겼다 |
| `Assets/Scripts/Economy/Production.cs` | `Economy/Production.cs` |
| `Assets/Scripts/Economy/Demo/ProductionDemoDriver.cs` | `Economy/Demo/ProductionDemoDriver.cs` |
| `Assets/Scripts/Economy/Resources/Economy/Goods.json`·`Markets.json` | `Economy/Resources/` |
| `Assets/Scripts/Presentation/Interior/BuildingStatusIcon.cs` | `Presentation/BuildingStatusIcon.cs` |
| `Assets/Prefabs/Interior/BuildingStatus.prefab` | `Prefabs/BuildingStatus.prefab` |

모든 파일은 `.meta`와 함께 옮겼다. 나머지는 코드 안에서 지웠다.
- TradeSession(`Settlement.cs`), Cargo(`MoneyStore.cs`), GoodsCatalog·시장 모델(`EconomyData.cs`).
- 계약 `ICargo`·`IMarkets`·`ITradeSession`·`IWarehouse`·`ITransfer`·`IProductionStatus`·`BuildingStatus`(`IronRingContracts.cs`).
- 건물 상태 아이콘 관련 코드: `BuildingView`의 SyncStatus, `InteriorCatalog`의 `_buildingStatusPrefab`·`_statusLift`, `Story012Build`의 EnsureStatusPrefab.
- `HabitatGoodsAmount`·생산 필드(`HabitatCatalog.cs`).

`WaypointResolver`·`EncounterPresentationSource`에는 거래 정차 유지가 없었다. 정차 유지 `trade-market`은 TradeSession에만 있었고, TradeSession과 함께 빠졌다.

## 2. 남은 규칙과 공식
**바퀴 정산**(`Settlement.Settle`, 거점 도착 때):
1. 건물 수입 = Σ 완성 건물 `_income`. 대상은 마을 거점 hub-1의 `IHabitatClusters.CompletedAt`이다. 잔고에 더하고 장부에 `LedgerKind.Income`("건물 수입")으로 적는다.
2. 열차 유지비 `_trainUpkeep`(10)을 강제로 지출한다. 잔고가 0 아래로 내려가도 된다.
3. 장부를 마감하고 토스트 `econ_lap_settled`를 띄운다. 문구는 "바퀴 {0} 정산: +건물 {1} +운임 {2} −유지비 {3} = {4}"이다.
4. AfterSettle에서 마을 등급을 판정한다(지금 주민과 번영 기준). 그다음 주민 증감 → `RunState.RefreshPhase(hub-1)` 순서로 돈다.
5. 파산은 `_bankruptLaps` > 0일 때만 판정한다. 지금 값은 0이라 꺼져 있고, 코드 경로는 남겼다.

운임은 승객이 거점에서 내릴 때마다 `_fare`(8)이다(040과 같음).

**주민**(`Population`, IPopulation):
- 주거 = `_baseHousing`(3) + Σ `_housing`. 빛 = Σ `_light`.
- 주거 > 주민이면 주민이 늘어난다. 빛 ≥ 주민 × `_lightPerResident`(0.5)이면 +`_growPerLapLit`(2), 아니면 +`_growPerLap`(1)이다. 주거를 넘지 않는다.
- 주거 < 주민이면 −`_shrinkPerLap`(1)이고, `_minResidents`(1) 아래로는 줄지 않는다. 같으면 그대로다.
- 노동·식량·만족도·실업은 없다.

**번영**(`RunState.LapDelta`): 기존 공식에 `_perResident`(1) × hub-1 주민을 더한다. 기존의 NPC × 3(`_perNpc`)은 그대로다.

**마을 등급**(`TownTiers`, ITownTier):
- 다음 등급의 주민과 번영을 둘 다 채우면 오른다. 한 정산에 여러 단계도 오를 수 있다.
- 등급은 내려가지 않는다. 오를 때 토스트 `town_tier_up`이 뜨고, 로그 `TOWN: tier up …`에 해금 목록이 남는다.
- 레시피 해금은 `HabitatCodex.BindTiers`가 출처 "tier"로 한다. 등급 0의 해금은 연결하는 즉시 연다.

**붕괴**: 등급 ≥ `_collapseMinTier`(1)인 상태에서 주민 ≤ `_collapseResidents`(1)이고 번영 ≤ `_collapseProsperity`(0)인 정산이 `_collapseLaps`(2)번 이어지면 Collapsed가 된다. 토스트는 `town_collapsed`다.

**결과**(`RunState`, `_useTownTiers` true):
| 상황 | 결과 |
|---|---|
| 도시(`_victoryTier` 3) 도달 | 즉시 승리(`_earlyWin`) |
| 기한 20바퀴 도달, 등급 ≥ 정착촌(`_partialTier` 1) | 부분 성공 |
| 기한 20바퀴 도달, 폐허 | 패배(DeadlineExpired) |
| hub-1 붕괴 | 패배(AllSettledCollapsed) |

hub-2~4는 기존 문턱 규칙으로 단계만 바뀌고 결과에는 영향이 없다.

## 3. 등급 표 (`TownTiers.json _tiers`)
| 번호 | id | 이름 | 주민 | 번영 | HubPhase | 레시피 해금 | 업그레이드(043) | 휴식지(043) |
|---|---|---|---|---|---|---|---|---|
| 0 | ruin | 폐허 | 0 | 0 | Ruined | barracks, greenhouse | – | – |
| 1 | camp | 정착촌 | 5 | 20 | Recovering | cobble_plaza, water_tower, iron_depot, toolshop | cargo_plus | REST-2 |
| 2 | village | 마을 | 12 | 50 | Settled | cogworks, power_house, telegraph_line, lamp_road, engine_room | boiler_plus, car_plus | REST-3 |
| 3 | city | 도시 | 25 | 100 | Settled | grand_cogworks, blast_furnace, repair_bay | express | – |

## 4. 데이터 키
- `Economy.json`: `_startBalance` 200, `_trainUpkeep` 10, `_fare` 8, `_bankruptLaps` 0(끔), `_settleToastSeconds` 4, `_refundRatio` 1.
- `TownTiers.json`:
  - 거점: `_hubId` hub-1, `_hubName` 황동골.
  - 주민: `_startResidents` 3, `_minResidents` 1, `_baseHousing` 3, `_growPerLap` 1, `_growPerLapLit` 2, `_shrinkPerLap` 1, `_lightPerResident` 0.5.
  - 붕괴·결과: `_collapseResidents` 1, `_collapseProsperity` 0, `_collapseLaps` 2, `_collapseMinTier` 1, `_victoryTier` 3, `_partialTier` 1.
  - 등급 표: `_tiers[]`(`_id`, `_name`, `_residents`, `_prosperity`, `_phase`, `_unlockRecipes`, `_unlockUpgrades`, `_unlockStops`).
  - 데모: `_demoSettles` 3, `_demoGrowBuildings`, `_demoTierResidents` 5, `_demoTierProsperity` 20.
- `HubProfiles.json`: `_deadlineLaps` 20, `_earlyWin` true, `_useTownTiers` true(false면 기존 문턱 규칙), `_perResident` 1.
- `HabitatTypes.json`: 건물마다 `_income`, `_housing`, `_light`. 생산 키 8개는 지웠다.
  - `_income`: lamp_street 1, coal_yard 4, cobble_plaza 0, grand_cogworks 12, boiler_room 3, pipe_gallery 2, cogworks 6, iron_depot 4, cistern 2, greenhouse 3, barracks 0, toolshop 5, telegraph_line 3, water_tower 2, lamp_road 1, engine_room 6, power_house 7, repair_bay 8, blast_furnace 12.
- `ko.json`: `econ_lap_settled` 문구를 바꿨고, `town_tier_up`·`town_collapsed`를 끝에 추가했다.

## 5. 데모
- `--economy-demo --economy-stage ledger`: 1초 뒤 한 바퀴를 정산하고 `ECON: settle …`, `ECON: demo ledger done …` 로그를 남긴다.
- `--town-demo --town-stage grow`: 가상 건물(막사·온실·가스등 길, 실제 타일 배치 없이 Population.ExtraBuildings에만 들어감)을 더하고 3번 정산한다.
  - 계산상 기대값: 주거 9, 빛 4 → 주민 3→5→7→9.
  - 로그: `TOWN: lap=… residents a->b`, `TOWN: demo grow …`.
- `--town-demo --town-stage tier`: 주민 5, 번영 20으로 시드하고 한 번 정산한다.
  - 기대 로그: `TOWN: tier up 정착촌 (camp) recipes=[…]`, `TOWN: demo tier tier=1 정착촌 … phase=Recovering`.
- 데모는 이번에 실행하지 않았다. 기대값은 공식으로 계산한 값이다.

## 6. 브리지 결과
- `refresh` 1회차: failed:true. `EconomyInstaller.cs`에 `using IronRing.Contracts.Presentation`이 빠져 있었다(GameServices). 고친 뒤 2회차: `{"compiling":false,"errors":"","failed":false}`.
- `execute-method IronRing.Presentation.Interior.Editor.Story012Build.Generate` 2회: 둘 다 성공했고, 1·2회차 뒤 git 상태가 같았다(멱등).
  - `InteriorCatalog.asset`은 제거된 필드 때문에 다시 직렬화됐다.
- `scene-status`: `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}`.

## 7. 수정·추가한 파일
- 계약: `Assets/Scripts/Contracts/IronRingContracts.cs`
  - 거래·창고·생산 계약을 지우고 `LedgerKind.Income`을 추가했다.
  - `IPopulation`(Residents, Housing, Light, Changed)과 `ITownTier`를 추가했다.
- Economy:
  - 새 파일: `TownTiers.cs`, `Population.cs`, `PlacementCost.cs`, `Demo/TownDemoDriver.cs`, `Resources/Economy/TownTiers.json`.
  - 수정: `Settlement.cs`, `EconomyData.cs`, `MoneyStore.cs`, `EconomyInstaller.cs`, `Demo/EconomyDemoDriver.cs`, `Resources/Economy/Economy.json`.
- Hub:
  - `RunState.cs`: TownLink, AttachTown, 등급 결과, `_perResident`.
  - `HubState.cs`: SetPhase.
  - `HubDefinition.cs`, `HubData.cs`: UseTownTiers, PerResident.
  - `Data/HubProfiles.json`.
- Habitats: `HabitatCatalog.cs`(`_income`, SetEconomy), `HabitatCodex.cs`(BindTiers, SOURCE_TIER), `Resources/Habitats/HabitatTypes.json`.
- Presentation: `Interior/BuildingView.cs`, `Interior/InteriorCatalog.cs`, `Interior/Editor/Story012Build.cs`, `Assets/Prefabs/Interior/InteriorCatalog.asset`(생성기가 다시 저장).
- Localization: `ko.json`.

## 8. 남은 문제
- 기한은 LapCompleted에서 센다. 그래서 20번째 바퀴는 정산(도착) 전에 런이 끝나고, 그 바퀴의 수입과 등급 판정은 결과에 들어가지 않는다(기존 RunState 순서 그대로).
- 번영 `_minimum`이 0이다. 그래서 붕괴 조건은 "번영 ≤ 0"(`_collapseProsperity` 0)으로 읽었다.
- 시작 주거 3(`_baseHousing`)이 없으면 첫 정산에서 주민이 바로 1로 준다. 그래서 데이터로 넣었다.
- 막사·온실은 시작 레시피가 아니다. 정착촌(주민 5)이 되려면 주거가 먼저 필요해서, 등급 0의 해금으로 연다.
- `LedgerKind.Sale`·`Purchase`는 계약 열거형(append-only)이라 남겼다. 쓰지는 않는다.
- 결과 화면의 "정착 N/4"는 등급 기준으로 바꿔야 한다(codex UI). `_unlockUpgrades`·`_unlockStops`는 043이 소비한다.
- HUD(`Hud/**`)의 창고·거래 화면 정리는 codex가 맡는다. 이번 refresh 시점에는 컴파일 오류가 없었다.
