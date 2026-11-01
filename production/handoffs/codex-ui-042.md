# codex UI 작업 042: 주민·만족도·마을 등급 표시

먼저 `production/handoffs/codex-ui-charter.md`. 스펙: `production/epics/iron-ring-train/story-042-population-labor-town-tier.md`.

## UI 수용 기준
1. **상단 상태줄**(`TopBarScreen` + 빌더, 표식 `.bridge/rebuild/TopBar`): 잔고 옆에 "주민 N · 만족도 N%"(아이콘 `ui_people`, `ui_smile` 새로 만들어 IconCatalog 등록). 만족도 50 미만이면 숫자 붉게.
2. **거점 상태줄**(HubPanel/HubPhase의 "황동골 · 폐허 · 번영 0/40 …" 줄): 단계 이름을 마을 등급 이름(폐허·정착촌·마을·도시)으로, 그 옆에 다음 등급 조건 "다음: 정착촌 (주민 3/5 · 번영 12/20)". 어느 화면이 그 줄을 그리는지 `Assets/Scripts/Hud/HubPhase/PhaseBannerScreen.cs`·`HubPanelScreen.cs`에서 확인.
3. **등급 상승 연출**: 등급이 오르면 중앙 큰 배너 "황동골이 정착촌이 되었습니다"(`PhaseBanner` 재사용 또는 확장) 2초.
4. **내부 제목**(`InteriorTitle`): "황동골 · 정착촌".
5. 문구 `ko.json` 끝에 `ui_topbar_people`, `ui_tier_*`.

## 로직 API (Claude 에이전트가 채움)

## 요청(codex → 코디네이터)
