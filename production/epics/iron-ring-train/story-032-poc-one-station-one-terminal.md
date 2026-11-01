# Story 032: POC 축소 — 역 1곳과 종점 1곳을 오가는 선로

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 1(선로 위 열차 이동)·Player goal. 사용자 지시(2026-11-02 00:20): "일단 POC 느낌으로 1개 역 1개 종점 도는 방식으로 해주라." 거점 4곳 고리 대신, 역(거점) 1곳과 종점(경유지) 1곳을 왕복하는 선로로 줄인다. 데이터만 바꾸고 4곳 구조는 데이터로 되돌릴 수 있게 둔다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: none.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 선로 데이터 `ring-track.json`: 노드 2개 — `hub-1`(역, 주조소, 왼쪽)과 `TERMINAL`(종점, 오른쪽, `_isStation:false`, 라벨 "종점"). 열차는 역 → 종점 → 역으로 왕복하고 역에 도착할 때마다 1바퀴. 종점에서는 지금처럼 경유지 이벤트·전투가 일어나고 승객이 탄다. 원래 8노드 데이터는 `ring-track-4hubs.json`으로 보관.
- [ ] 선로 화면: 역과 종점을 잇는 직선(또는 완만한 왕복 선로) 위를 열차가 오가고, 역에는 건물(주조소)·라벨·소품, 종점에는 종점 표지(톱니 신호기)·라벨 "종점". `BuildTopDownMain.Rebuild` 뒤 `BuildMainCanvas.Generate`·`Story012Build.Generate`·`MapLabelsBuild.Generate`·`PropsBuild.Generate`를 차례로 돌려 씬이 완전히 복원된다(MainCanvas·HubInteriors(hub-1 하나)·라벨·소품). 멱등 확인.
- [ ] 런 규칙: 거점 수는 선로의 역 수(1)로 계산 — 결과 화면 "정착 1/1", 조기 승리 = 역 1곳 정착. 기한 8바퀴 유지. 결과 화면 거점 줄 1개.
- [ ] 승객: 역이 하나뿐이므로 `_hubIds`에 hub-1이 없는 NPC도 hub-1에 내릴 수 있게 데이터 플래그 `_ignoreHubRoster:true`(승객 설정). 탑승 순환 우선은 hub-1 고정. 가르침·원하는 설계도 해금은 그대로.
- [ ] 데모: `--npc-demo --npc-stage hub2`는 역이 하나라 건너뛰고 경고 로그, 나머지 데모(`--habitat-demo`, `--npc-demo` 나머지, `--codex-demo`, `--result-demo`, `--placement-demo`, `--deadline-demo`, `--map-depart`)는 동작. `--map-depart`로 열차가 종점까지 갔다가 돌아오는 캡처 2장(출발 직후, 종점 정차).
- [ ] 스크린샷: (1) 선로 화면(역·종점·열차), (2) 종점 정차(이벤트/전투 문구), (3) 결과 화면 "정착 1/1 승리". `production/qa/evidence/story-032-poc-one-station-one-terminal/` + verification.md. 브리프 MVP 1·목표 문장에 "POC는 역 1곳·종점 1곳" 한 줄.

---

## Implementation Notes

- `Assets/Scripts/Track/Resources/Track/ring-track.json`(노드·위치·카메라), `RingTrack.cs`(2노드 순환·바퀴 판정 확인), `Assets/Scripts/Bootstrap/Editor/BuildTopDownMain.cs`(2노드 선로 그리기: 같은 선을 왕복하므로 레일 중복 없이 한 번만), `MapView`/`MapTrain`(왕복 방향 전환 시 스프라이트 각도), `Hub/HubData.cs`·`RunState.cs`(거점 수 = 역 노드 수; HubProfiles.json은 4곳 유지하되 선로에 없는 거점은 무시), `Npc/Passengers.cs`(`_ignoreHubRoster`), 데모 드라이버들.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 거점 추가/복귀 UI, 선로 분기.

---

## Files

- `Assets/Scripts/Track/Resources/Track/ring-track.json`·`ring-track-4hubs.json`(보관), `Assets/Scripts/Track/*.cs`(필요 시), `Assets/Scripts/Bootstrap/Editor/BuildTopDownMain.cs`, `Assets/Scripts/Presentation/Map/*.cs`, `Assets/Scenes/Main.unity`(재생성), `Assets/Scripts/Hub/HubData.cs`·`RunState.cs`, `Assets/Scripts/Npc/Passengers.cs`·`NpcPoolData.cs`·`Data/NpcPool.json`(승객 설정), 데모 드라이버, `ko.json`(종점 라벨), `design/game-brief.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 3장: `production/qa/evidence/story-032-poc-one-station-one-terminal/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-028-run-ending-and-prosperity.md`, `story-023-npc-rosters-conditions-arrival.md`
- Unlocks: None
