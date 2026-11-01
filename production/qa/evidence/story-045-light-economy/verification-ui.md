# 스토리 045 — UI 검증 (HUD 인수·감사)

작성 2026-11-02, UI 담당 Claude(codex 작업 인수). 범위: `Assets/Scripts/Hud/**`, `Assets/Prefabs/UI/**`, `ko.json`(추가만).
테스트·플레이 캡처 없음(데모 캡처는 코디네이터).

## 1. codex가 남긴 것 감사

| 항목 | 결과 |
|---|---|
| 제거된 서비스 참조(`IMarkets`·`ICargo`·`ITradeSession`·`IWarehouse`·`IProductionStatus`·`ITransfer`·`BuildingStatus`) | `Assets/Scripts/Hud/**`에 없음 |
| "만족도" 문구 | HUD 코드·문자열에 없음 |
| `BuildMainCanvas.SCREEN_IDS` | Trade·Warehouse·Raid·CombatFx 없음. `REMOVED_SCREEN_IDS = {Raid, CombatFx, Trade, Warehouse}`가 옛 인스턴스를 지움 |
| `Prefabs/UI/Screens` | Trade·Warehouse 프리팹 없음 |
| 핫바·팔레트 가격 | `HotbarScreen.RefreshPrice` → `HotbarCellView.SetPrice`: "N마르크"(`ui_hotbar_price`), `IPlacementCost.CanAfford` 거짓이면 아이콘·이름·가격·틀을 흐린 색으로 |
| 상단 바 | "잔고 N마르크"(음수면 강조색) + 주민 아이콘 "주민 N"(`IPopulation`이 늦게 등록돼도 `ScreenTicker`로 다시 찾음) |
| 거점 상태줄·패널 | 등급 이름 + `ui_tier_next` "다음: <등급> (주민 a/b · 번영 c/d)", 최고 등급이면 "최고 등급 달성" |
| 내부 제목 | `ui_tier_title` "황동골 · <등급>" |
| 결과 화면 | "총수입 N · 총지출 N · 최종 등급 <등급>"(`ILedger`) |
| 등급 배너 | `PhaseBannerScreen`에 2초 큐 배너가 이미 있었음(`_tierBannerSeconds` 2) |

### 발견한 결함(고침)
1. **등급 표시가 모든 거점에 붙음.** `PhaseBannerScreen`·`HubPanelScreen`·`InteriorTitleScreen`이 `ITownTier`가 있으면 hub-2~4에도 마을 등급 이름을 썼고, 다음 등급 조건의 번영을 **현재 거점**의 번영으로 계산했다. API 문서는 등급이 hub-1에만 해당하고 다른 거점은 기존 단계를 쓴다고 한다.
2. **등급 상승 배너의 거점 이름이 현재 거점.** 다른 거점에서 정산되면 "칠철항이 정착촌이 되었습니다" 식으로 틀릴 수 있었다.
3. **배너와 로직 토스트 `town_tier_up`이 같은 소식을 동시에 띄움.**
4. **결과 화면 제목·본문이 옛 규칙**("부분 성공 (정착 n/4)", "네 거점이 모두 정착했다").
5. 목표 줄(도시·바퀴 기한) 없음.

## 2. 고친 것

- `Hud/Ui/TownUiText.cs`: `HubId(IPopulation)`(설정의 등급 거점, 기본 hub-1), `Goal(...)`(승리 등급 = `TownTiers.VictoryTier` 데이터에서 이름·조건), `TierToastMatcher(tierName)`(`town_tier_up` 문구 끝부분으로 판별).
- `Hud/Ui/ToastScreen.cs`: `Swallow(match, seconds)` — 이미 떠 있거나 쌓인 같은 토스트를 지우고, 창 안에 들어오는 같은 토스트는 삼킨다. 로직 코드(`Economy/**`)는 건드리지 않았다. 이벤트 구독 순서와 무관하게 동작한다(로직이 먼저면 지우고, 나중이면 막음).
- `Hud/HubPhase/PhaseBannerScreen.cs`: 등급 상승 때 배너(2초, "황동골이 <등급>이 되었습니다", 단계마다 큐) + 토스트 삼킴(`_tierToastSwallowSeconds` 1초). 등급 이름·다음 조건은 hub-1에서만, 번영은 항상 hub-1 기준. 붕괴면 기존 붕괴 문구. **목표 줄** `_goal`(Small 14, 흐린 색, 다음 등급 줄 바로 아래): "목표: 도시 (주민 N/25 · 번영 N/100) · 바퀴 N/20". 패배 후에는 숨김.
- `Hud/Editor/Screens/PhaseBannerScreenBuilder.cs`: `Goal` 라벨(위에서 142px, 820×24) 추가·바인딩.
- `Hud/HubPanelScreen.cs`, `Hud/Interior/InteriorTitleScreen.cs`: 등급 표시를 hub-1에만. 다른 거점은 기존 문구(정착 기준 포함).
- `Hud/RunResultScreen.cs`: 등급이 있으면 제목 "승리 · 도시" / "부분 성공 · <등급>" / "패배", 본문은 황동골 기준 문구. 거점 줄의 hub-1 단계는 등급 이름(붕괴 제외).
- `ko.json` 추가 8개: `ui_goal_line`, `ui_goal_laps`, `ui_result_victory_tier`, `ui_result_partial_tier`, `ui_result_body_victory_town`, `ui_result_body_partial_town`, `ui_result_body_ruin_town`, `ui_result_body_collapse_town`.

배치 판단: 목표 줄은 상단 바에 넣지 않았다. 1280 기준 상단 바는 위치(18~318)·잔고(328~564)·주민(574~738)·바퀴(744~884)·승객/도감 버튼(오른쪽 ~350px)로 남는 폭이 50px 미만이다. 거점 상태줄 아래(다음 등급 줄 밑)가 같은 정보 묶음이라 읽기도 낫다.

## 3. 프리팹 재생성
- Play 시작 예외 2건(코디네이터 데모에서 발견) 고침:
  - `UnassignedReferenceException: _tierBanner of PhaseBannerScreen` — 프리팹이 새 필드 이전 것이었다. 빌더는 이미 `TierBanner`를 만들고 바인딩한다 → 재생성으로 채움. `ClearSample`·`Refresh`·`LateUpdate`에 `_tierBanner`·`_tierBannerText`·`_nextTier` null 가드 추가.
  - `NullReferenceException at TopBarScreen.Initialize (75행)` — `_people`·`_peopleIcon`이 TopBar 프리팹에 없었다(빌더는 만들지만 TopBar가 재생성되지 않음). 재생성 + `_people`·`_peopleIcon`·`_icons`·`_balance`·`_coin`·`_theme` null 가드. `HubPanelScreen._nextTier`, `RunResultScreen._finance`도 가드.
- 표식: `.bridge/rebuild/PhaseBanner`, `RunResult`, `HubPanel`, `InteriorTitle`(1차), `TopBar`, `PhaseBanner`(2차). 표식은 생성기가 소비해 비었다.
- `refresh` → `failed:false`, `errors:""` (수정 전후 두 번).
- `BuildMainCanvas.Generate` 2회 실행 → 둘 다 `Executed`. 1회차 뒤와 2회차 뒤 `git diff --stat Assets/Prefabs`가 같다(5 files, +987/−367) → **2회차 무변경**.
- 바뀐 프리팹: `MainCanvas.prefab`, `Screens/TopBar.prefab`(주민 칸 생김), `Screens/PhaseBanner.prefab`(Goal·TierBanner), `Screens/HubPanel.prefab`, `Screens/InteriorTitle.prefab`. RunResult·Hotbar는 빌더 변경이 없어 프리팹 무변경.
- `scene-status` → `isDirty:false`, `playing:false`, `Assets/Scenes/Main.unity`.
- 부수 변경: `Assets/Fonts/NotoSansKR SDF.asset`(동적 아틀라스에 새 한글 글리프 추가). 커밋에 포함해야 다른 PC에서 글자가 빠지지 않는다.

## 4. 남은 것·확인 요청
- **바퀴 N/20의 N.** `ITrackRuntime.Lap`을 쓰고 총량은 `Lap + IHubDevelopment.LapsUntilDeadline`으로 계산한다(별도 "총 기한" API가 없음). 기한 정의가 "남은 바퀴 = 20 − 완료 바퀴"면 정확히 20이 된다. 데모에서 첫 바퀴에 "바퀴 0/20" 또는 "1/20" 중 무엇으로 보이는지 코디네이터 확인 필요.
- 토스트 삼킴은 HUD 쪽 우회다. 로직이 `town_tier_up` 토스트를 끄면(handoff "요청"에 적음) `Swallow` 호출은 그대로 둬도 무해하다.
- 플레이 캡처(1280×720 상단 줄 겹침, 배너 2초)는 코디네이터 데모에서 확인.
