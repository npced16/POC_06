# Story 025: 승객 화면 반영과 UI 잔재 정리

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Core loop 3단계(열차 승객·제안·힌트·도감 "???"). 사용자 지시(2026-11-01 21:20): 후속 1·2 "둘 다 작업해 달라" — 023이 남긴 화면 반영과 022가 남긴 잔재 정리.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: UGUI 2.0.0 + TMP, 새 Input System.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 상단 바: "승객 N/3"(`IPassengers.PassengerIds.Count`, 정원은 데이터) 라벨과 버튼. 누르면 승객 화면이 열린다(단축키 P).
- [ ] 승객 화면(새 화면 프리팹 `Passengers`, 상황 화면): 탄 사람마다 한 줄 — 직업명, 출신지, 제안("가스등 거리를 만들어 주면 거기서 살겠소."), 힌트, 탄 지 몇 바퀴. 비어 있으면 "승객이 없다". 탑승·하차·이탈 때 갱신. 닫기·Esc.
- [ ] 대화 패널: 집 줄 옆에 "<출신지> 출신"(`INpcInteraction.Origin(npcId)`).
- [ ] 도감 카드 "찾아오는 이": 그 서식지 종류 후보(`INpcRecruiter.CandidatesOf`) 가운데 실제로 합류한 사람(`JoinedOf`)만 이름으로, 나머지는 "???"로(예: "점등부, ???"). 숨은 조건은 어디에도 보이지 않는다.
- [ ] 탑승 토스트는 그대로 두되, 승객 화면을 열면 같은 제안·힌트를 다시 볼 수 있다.
- [ ] 잔재 정리: `PlacementGhost.UiHitTests`와 `InteriorNpcWalker`의 사용 제거(EventSystem 판정만), `HubPhaseViewSettings.HudSortingOrder`와 `hub_phase_view.json`의 같은 필드 삭제, `ItemsDemoDriver` 등 남은 죽은 참조 확인. `grep UiHitTests` 0건.
- [ ] 데모 `--npc-demo --npc-stage passengers`: 승객 2명을 태운 뒤 승객 화면을 연다. 스크린샷: (1) 승객 화면, (2) 상단 바 승객 수, (3) 대화 패널 출신지 줄, (4) 도감 카드 "점등부, ???". `production/qa/evidence/story-025-passenger-hud-cleanup/` + verification.md.

---

## Implementation Notes

- 화면 추가 규칙(022): `Assets/Scripts/Hud/Editor/Screens/PassengersScreenBuilder.cs`(`Build(UiTheme)`), `BuildMainCanvas.SCREEN_IDS`에 `Passengers` 추가(TopBar 다음, NpcDialog 앞), 런타임 `Assets/Scripts/Hud/Npc/PassengersScreen.cs`(+ 줄 부품 `PassengerRow.prefab`). 빌더 수정 뒤 재생성은 `.bridge/rebuild/<Id>` 표식.
- 024(UI 스킨)가 병렬로 테마·스프라이트를 바꾸고 있으므로, 이 스토리는 레이아웃·바인딩만 다루고 색·스프라이트는 테마 값을 그대로 쓴다. 024가 끝난 뒤 코디네이터가 Reskin을 돌린다.
- 검증은 열린 편집기 브리지. 캡처는 코디네이터.

---

## Out of Scope

- 승객과의 대화, 승객 내보내기 버튼, 세이브.

---

## Files

- `Assets/Scripts/Hud/TopBarScreen.cs`, `Editor/Screens/TopBarScreenBuilder.cs`, `Hud/Npc/PassengersScreen.cs`·`PassengerRowView.cs`, `Editor/Screens/PassengersScreenBuilder.cs`, `Hud/Npc/NpcDialogScreen.cs`·`Editor/Screens/NpcDialogScreenBuilder.cs`, `Hud/Codex/CodexScreen.cs`·`CodexCardView.cs`, `Hud/Editor/BuildMainCanvas.cs`(SCREEN_IDS), `Prefabs/UI/Screens/*.prefab`·`Parts/PassengerRow.prefab`, `Hud/Ui/ScreenTicker.cs`(P 키)
- `Presentation/Interior/PlacementGhost.cs`, `Presentation/HubPhase/InteriorNpcWalker.cs`·`HubPhaseViewSettings.cs`·`Resources/HubPhase/hub_phase_view.json`, `Npc/Demo/NpcHomesDemoDriver.cs`·`Resources/Npc/NpcHomesDemo.json`, `ko.json`(키 추가)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 4장: `production/qa/evidence/story-025-passenger-hud-cleanup/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-022-ugui-screens.md`, `story-023-npc-rosters-conditions-arrival.md`
- Unlocks: None
