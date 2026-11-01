# codex UI 작업 042: 주민·만족도·마을 등급 표시

먼저 `production/handoffs/codex-ui-charter.md`. 스펙: `production/epics/iron-ring-train/story-042-population-labor-town-tier.md`.

## UI 수용 기준
1. **상단 상태줄**(`TopBarScreen` + 빌더, 표식 `.bridge/rebuild/TopBar`): 잔고 옆에 "주민 N · 만족도 N%"(아이콘 `ui_people`, `ui_smile` 새로 만들어 IconCatalog 등록). 만족도 50 미만이면 숫자 붉게.
2. **거점 상태줄**(HubPanel/HubPhase의 "황동골 · 폐허 · 번영 0/40 …" 줄): 단계 이름을 마을 등급 이름(폐허·정착촌·마을·도시)으로, 그 옆에 다음 등급 조건 "다음: 정착촌 (주민 3/5 · 번영 12/20)". 어느 화면이 그 줄을 그리는지 `Assets/Scripts/Hud/HubPhase/PhaseBannerScreen.cs`·`HubPanelScreen.cs`에서 확인.
3. **등급 상승 연출**: 등급이 오르면 중앙 큰 배너 "황동골이 정착촌이 되었습니다"(`PhaseBanner` 재사용 또는 확장) 2초.
4. **내부 제목**(`InteriorTitle`): "황동골 · 정착촌".
5. 문구 `ko.json` 끝에 `ui_topbar_people`, `ui_tier_*`.

## 로직 API (Claude 에이전트가 채움)
작성 2026-11-02, 컴파일 통과(브리지 refresh failed:false). **범위가 바뀌었다(사용자 06:40 "가볍게, 마을 키우기만", 스토리 045)**: 만족도·노동·식량은 없다. 수용 기준 1의 "만족도 N%"·`ui_smile`은 빼고 "주민 N"만 표시한다(코디네이터와 확인할 것). **창고·거래 패널과 그 계약(ICargo·IMarkets·ITradeSession·IWarehouse·ITransfer·IProductionStatus·BuildingStatus)은 제거됐다.** 건물 상태 아이콘(`status_*`)도 더 쓰지 않는다.

계약은 `Assets/Scripts/Contracts/IronRingContracts.cs` 끝 "주민·마을 등급(스토리 042)" 절, 구현은 `Assets/Scripts/Economy/Population.cs`·`TownTiers.cs`. 둘 다 **`GameServices.Ready`에서** 등록된다(서식지가 같은 Composing에 생겨서). 화면은 처음 쓸 때 `GameServices.Get<T>()`로 다시 얻고, null이면(선로 없는 독립 데모 씬) 조용히 숨긴다.

### IPopulation
- `int Residents`: 일반 주민(시작 3, 최소 1). 고유 NPC는 따로 센다(`INpcRecruiter.NpcsAt`).
- `int Housing`: 주거 = 기본 3 + 완성 건물 `_housing` 합.
- `int Light`: 빛 = 완성 건물 `_light` 합.
- `event Action Changed`: 정산 직후, 건물이 완성되거나 부서질 때 난다.
- 규칙(툴팁용): 바퀴 정산 때 주거 > 주민이면 +1(빛 ≥ 주민×0.5면 +2), 주거 < 주민이면 −1.

### ITownTier
- `int Tier`(0~3), `string TierId`(`ruin`|`camp`|`village`|`city`), `string TierName`(폐허|정착촌|마을|도시, `TownTiers.json _name`).
- `int NextTier`, `bool IsMax`, `int NextResidentsNeed`, `int NextProsperityNeed`. 최고 등급이면 Need가 0이다.
  - "다음: 정착촌 (주민 3/5 · 번영 12/20)"에서 현재 주민은 `IPopulation.Residents`다.
  - 현재 번영은 `IHubStateSystem.GetProsperity("hub-1")`다(`IHubDevelopment`와 같은 RunState).
- 다음 등급 이름은 `ITownTier`에 없으니 `TierName`을 다음 등급 기준으로 매핑하거나, `(ITownTier as IronRing.Economy.TownTiers).NameOf(NextTier)`를 쓴다.
- `event Action<int> TierChanged`(새 등급 번호): 정산 중 등급이 오를 때 단계마다 난다. 등급은 내려가지 않는다. 큰 배너는 여기에 건다.
  - 로직이 이미 토스트 `town_tier_up`("{0}이(가) {1} 등급이 되었습니다")을 띄운다. 배너와 겹치면 알려 달라. 토스트를 끄겠다.
- `IReadOnlyList<string> UnlockedRecipeIds`, `UnlockedUpgradeIds`: 누적 해금 목록. 레시피는 도감에 출처 `"tier"`(`HabitatCodex.SOURCE_TIER`)로 열린다.
- 등급 표: 폐허 0/0 → 정착촌 주민 5·번영 20 → 마을 12·50 → 도시 25·100.

### 단계 이름과 결과
- 거점 단계(`IHubDevelopment.GetPhase("hub-1")`, `PhaseChanged`)는 이제 등급을 따른다: 폐허→Ruined, 정착촌→Recovering, 마을·도시→Settled, 붕괴→Collapsed.
  - 그래서 기존 단계 이름(폐허·복구 중·정착·붕괴) 대신 hub-1에서는 `ITownTier.TierName`을 표시하면 된다(붕괴일 때만 기존 붕괴 문구).
  - 다른 거점(hub-2~4)은 기존 단계 그대로다.
- 결과(`IRunResult`): 기한 20바퀴. 도시 도달 = 즉시 Victory, 기한 종료 때 정착촌 이상 = Partial, 폐허 = Defeat(DeadlineExpired), hub-1 붕괴 = Defeat(AllSettledCollapsed).
  - 결과 화면의 "정착 N/4"는 등급 이름으로 바꾸는 편이 맞다.
  - 파산은 데이터로 꺼져 있다(`Economy.json _bankruptLaps` 0).

### 정산 토스트
- `econ_lap_settled` = "바퀴 {0} 정산: +건물 {1} +운임 {2} −유지비 {3} = {4}". 로직이 ToastScreen으로 띄우므로 UI 작업은 없다.
- 장부(`ILedger`)의 종류: `Fare`(운임), `Upkeep`(열차 유지비), 새 `Income`(건물 수입, 메모 "건물 수입"). `Sale`·`Purchase`는 더 생기지 않는다.
- 상단 잔고(`IMoney`)와 핫바 가격(`IPlacementCost`)은 그대로다.

## 요청(codex → 코디네이터)
