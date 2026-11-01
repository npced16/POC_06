# Story 028: 런의 끝과 번영 규칙 — 점검 보고서 S1·S2 반영

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 22:50 KST 코디네이터 브리지 검증 — 컴파일 통과, 결과 화면 부분/승리/패배, 다시 시작 재로드 뒤 HUD 재연결 로그 확인, 배너 "번영 N/40 · 다음 바퀴 +M")
> **Layer**: Core + Presentation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Player goal & fail state. 점검 보고서 `production/qa/playtest-2026-11-01.md`의 S1-1(이기는 길도 끝도 없음), S1-2(타일 칸 수가 번영을 지배), S2-1(패배 화면에 다시 시작 없음), S2-2(승객 탑승이 풀 앞쪽에 쏠림), S2-3(초반 전투 패배 무영향), S2-4(시작 레시피 재료가 핫바에 없음), S3-1(완성 묶음에 칸이 붙어도 테두리 미갱신). 사용자 질문(22:35) "왜 게임이 더 재미가 없을까"에 대한 구조적 답.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: none.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 런의 끝: 기한(`_deadlineLaps`, 8바퀴)이 지나면 무조건 결과 화면이 뜬다 — 정착 4곳이면 "승리", 1~3곳이면 "부분 성공(정착 N/4)", 0곳이면 "패배". 정착한 거점이 있어도 기한은 꺼지지 않는다(`RunState` 수정). 결과 화면(새 UGUI 화면 `RunResult`, Simple 스킨)에 거점별 단계·번영·합류 NPC 수, 총 바퀴, "다시 시작" 버튼(씬 재로드) — 패배 화면(`Defeat`)은 결과 화면으로 통합.
- [x] 번영 규칙: 놓인 칸당 +1을 없애고(`_perPlacedItem:0`), 완성 서식지의 칸만 센다(`_perHabitatCell`, 기본 1) + 서식지 보너스 + NPC×3 + 시설×2 − 기본 감소. 폐허도 전투 패배·방치 감소를 받는다(바닥 0). 데이터로 다시 맞춘 목표: 서식지 2개 + NPC 1명이면 3~4바퀴에 정착, 서식지 1개뿐이면 기한 안에 못 함. 브리프의 거점 단계 문장과 `HubProfiles.json` 주석 갱신.
- [x] 승객 탑승 공정성: 후보를 풀 순서가 아니라 섞어서(시드 결정적) 굴리고, 거점별로 돌아가며 우선권을 준다(이번 경유지는 hub-k 명단 우선). 성채(hub-4) 명단도 탄다는 것을 데모 로그로 확인.
- [x] 핫바 기본 칸: 시작 레시피 재료(가스등·석탄더미·보일러)가 1~3번에 오도록 `Hotbar.json` 교체.
- [x] 테두리/건물 갱신(S3-1): 완성된 묶음에 칸이 붙거나 빠져도 묶음 구성이 바뀌면 `HabitatChanged`(새 이벤트) 또는 Broken→Completed 재발생으로 표시가 갱신된다.
- [x] 정보 표시: 배너에 "번영 N/40"과 "다음 바퀴 +M"(현재 공식으로 계산), 승객 화면 각 줄에 "내릴 조건: <힌트>"는 그대로, 거점 패널에 정착까지 남은 번영.
- [x] 스크린샷: (1) 결과 화면(부분 성공), (2) 결과 화면(승리, 데모 플래그로 4곳 정착), (3) 배너 "번영 N/40 · 다음 바퀴 +M", (4) hub-4 승객 탑승 로그. `production/qa/evidence/story-028-run-ending-and-prosperity/` + verification.md.

---

## Implementation Notes

- 로직: `Assets/Scripts/Hub/RunState.cs`(기한·결과), `HubState.cs`(폐허 감소), `IProsperitySources.cs`/`HubProfiles.json`(공식), `Assets/Scripts/Npc/Passengers.cs`(셔플·거점 순환), `Hotbar.json`, `ClusterHabitatSystem.cs`(묶음 구성 변화 이벤트).
- 계약: `IRunLossInfo` → `IRunResult`(승리/부분/패배, 거점별 요약) 확장(추가만).
- 화면: `Assets/Scripts/Hud/RunResultScreen.cs` + 빌더(`SCREEN_IDS`에 `RunResult`, Defeat 제거), 다시 시작은 `SceneManager.LoadScene` + `GameServices.Reset`.
- 데모 `--result-demo --result-stage partial|win|lose`: 정착 수를 강제로 맞추고 기한을 끝낸다.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 자원·제작 부활, 죽은 기능 삭제(별도 정리 스토리), 사운드.

---

## Files

- `Assets/Scripts/Hub/RunState.cs`·`HubState.cs`·`IProsperitySources.cs`·`Data/HubProfiles.json`·`Demo/PhaseDemoDriver.cs`
- `Assets/Scripts/Contracts/IronRingContracts.cs`(IRunResult 추가)
- `Assets/Scripts/Npc/Passengers.cs`, `Assets/Scripts/Hud/Hotbar/Resources/Hotbar/Hotbar.json`
- `Assets/Scripts/Habitats/ClusterHabitatSystem.cs`(+ 계약 이벤트), `Assets/Scripts/Presentation/Interior/ClusterFrameView.cs`
- `Assets/Scripts/Hud/RunResultScreen.cs`·`Editor/Screens/RunResultScreenBuilder.cs`·`HubPhase/PhaseBannerScreen.cs`·`HubPanelScreen.cs`·`Editor/BuildMainCanvas.cs`, `ko.json`
- `design/game-brief.md`(목표·실패·거점 단계 문장)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 4장: `production/qa/evidence/story-028-run-ending-and-prosperity/` + `verification.md`

**Status**: [x] `028-v2-result-partial.png`("부분 성공 (정착 2/4)", 거점별 줄, 총 8바퀴, 다시 시작), `028-v2-result-win.png`("승리 — 네 거점이 모두 정착했다"), `028-v2-restart.png`(재시작 뒤 새 런: 번영 0/40, 결산까지 8바퀴; 로그 `RUN RESTART: re-ran 10 BeforeSceneLoad + 18 AfterSceneLoad hooks`, `after restart ready=True, over=False, lap=0 … TopBar=True`), `028-map-banner.png`(선로 화면 배너). 탑승 공정성은 `BoardingFairnessProbe` 로그(거점마다 3명, hub-4 포함). 에이전트 결정: 기본 감소 −3→−6, hub-3/4 배율 0.9/1.2.

---

## Dependencies

- Depends on: `story-014-hub-phases-defeat.md`, `story-023-npc-rosters-conditions-arrival.md`, `story-026-ui-simplify.md`
- Unlocks: None
