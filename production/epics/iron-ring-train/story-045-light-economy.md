# Story 045: 가벼운 마을 경영 — 교역·생산·창고·노동 제거, 건물 수입·주민·마을 등급만 남긴다

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief Core loop·MVP 1~3(가벼운 경영). 사용자 결정(2026-11-02 06:40, "게임이 이상하게 가는데?" → 선택 "가볍게 — 마을 키우기만"): 교역(시장·화물·거래 패널)·생산 체인·창고·노동·만족도를 전부 빼고, **타일 배치 → 건물 → 주민·NPC → 수입(마르크)·번영 → 마을 등급**의 단순 경영만 남긴다. 열차는 승객과 이벤트만 실어 나른다. 040·041의 교역·생산 코드는 `archive/economy-trade-2026-11-02/`로 보관하고, 042~044는 이 스토리로 대체한다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: LOW
**Engine Notes**: 보관은 Assets 밖으로 이동. 검증은 파이프라인(컴파일·생성기·씬 저장), 캡처 없음.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 제거: 시장·화물·거래 세션·창고·적재·생산·노동·만족도·건물 상태 아이콘·거래 패널·창고 패널과 그 계약(`ICargo`·`IMarkets`·`ITradeSession`·`IWarehouse`·`ITransfer`·`IProductionStatus`)·데이터(`Goods.json`·`Markets.json`)·데모 단계를 보관함으로. `HabitatTypes.json`의 생산 필드는 `_income`(마르크/바퀴) 하나로 교체(`_housing`·`_light` 유지).
- [ ] 돈: `IMoney`·장부 유지. 바퀴 정산 = +완성 건물 `_income` 합 +승객 운임(8/명) −열차 유지비(10) → 토스트 "바퀴 N 정산: +건물 a +운임 b −유지비 c = ±d". 파산은 데이터로 꺼 둠(`_bankruptLaps` 0). 타일 가격·환급(041)은 유지, 핫바에 가격 표시(codex).
- [ ] 주민: 시작 3. 정산 때 주거(건물 `_housing` 합) > 주민이면 +1(가스등 빛 `_light` 합 ≥ 주민×0.5면 +2), 주거 < 주민이면 −1(최소 1). 주민은 번영에 +1/명. 고유 NPC 합류는 그대로.
- [ ] 마을 등급(`TownTiers.json`): 폐허 → 정착촌(주민 5·번영 20) → 마을(12·50) → 도시(25·100). 등급 오를 때 레시피 해금·토스트·배너·내부 제목(codex), `HubPhase` 사상(폐허 연출 연동). 런: 20바퀴 안에 도시 = 승리, 기한 종료 = 등급별 부분 성공, 붕괴 = 패배.
- [ ] HUD(codex): 상단 "잔고 N마르크 · 주민 N", 거점 상태줄에 등급 이름과 다음 등급 조건, 등급 상승 배너, 결과 화면 "총수입 N · 총지출 N · 최종 등급". 거래·창고 화면 제거.
- [ ] 데모 `--town-demo --town-stage grow|tier`, `--economy-demo --economy-stage ledger`. 검증: 파이프라인 + `production/qa/evidence/story-045-light-economy/verification.md`(+ `verification-ui.md`).

---

## Implementation Notes

- 로직(Claude): `Assets/Scripts/Economy/`(MoneyStore·Ledger·PlacementCost·Settlement 단순화·Population·TownTiers·Installer), `HabitatCatalog`, `RunState`, `HubDevelopment`, `HabitatCodex`. UI(codex): `Hud/TopBar`·`HubPanel`/`PhaseBanner`·`InteriorTitle`·`RunResult`·핫바 가격, Trade/Warehouse 화면 제거.

---

## Out of Scope

- 교역·생산·노동·만족도(보관), 열차 업그레이드·선로 확장(보류), 장부 화면(보류 — 정산 토스트로 충분).

---

## Files

- 보관: `Assets/Scripts/Economy/Markets.cs`·`Warehouse.cs`(창고·적재 부분)·`Production.cs`·`Demo/ProductionDemoDriver.cs`·`Resources/Economy/Goods.json`·`Markets.json`, `Assets/Scripts/Hud/Trade/**`·`Editor/Screens/TradeScreenBuilder.cs`·`Prefabs/UI/Screens/Trade.prefab`, `Presentation/Interior/BuildingStatusIcon.cs`·`Prefabs/Interior/BuildingStatus.prefab`. 수정: `Contracts/IronRingContracts.cs`, `Economy/*.cs`·`Resources/Economy/Economy.json`·`TownTiers.json`(새), `Habitats/HabitatCatalog.cs`·`Resources/Habitats/HabitatTypes.json`, `Hub/RunState.cs`·`HubDevelopment*.cs`·`Data/HubProfiles.json`, `Habitats/HabitatCodex.cs`, `Combat/WaypointResolver.cs`·`Bootstrap/EncounterPresentationSource.cs`, `Presentation/Interior/BuildingView.cs`·`Editor/Story012Build.cs`·`InteriorCatalog.cs`, `Hud/**`(codex), `ko.json`, `Assets/Scenes/Main.unity`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 생성기 멱등 + 씬 저장: `production/qa/evidence/story-045-light-economy/verification.md`(캡처 없음)

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-040-economy-market-ledger.md`, `story-041-production-buildings.md`
- Unlocks: None
