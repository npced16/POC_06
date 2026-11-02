# Story 048: 유지비 없음, 한 마을 느낌 — 일정 이상 진행되면 선로가 열려 두 번째 마을(잿골)이 생긴다

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: 사용자 지시(2026-11-02 09:30): "나중에 여러 마을로 확장해서 여러 곳을 돌게 만들 거야. 유지비 같은 건 두지 말고 각 마을별 멸망 시나리오 같은 거 해서 할 예정. 지금은 한 마을만 하는 느낌으로. 일정 이상 진행되면 선로 열려서 마을이 하나 더 생기게 해주라." 따라서 (1) 열차 유지비 제거(돈은 건물 수입·운임으로만 오름), (2) 주민·등급을 **마을(거점)마다** 따로 세는 구조로 바꾸되 지금은 황동골 한 곳만 보이게, (3) 황동골이 **정착촌**이 되면 "선로가 열렸습니다" 연출과 함께 두 번째 마을 **잿골**(hub-2)이 고리에 나타나 열차가 거기도 정차하고 내부 배치·주민·등급이 따로 돌아간다. 마을별 멸망 시나리오는 다음 스토리.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 선로 그림은 처음부터 5점 고리로 그려 두고(잠긴 노드 포함) 잠긴 노드는 통과만 한다. 생성기(Rebuild + 체인) 재실행.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 유지비 제거: `Economy.json` `_trainUpkeep` 0, 정산 토스트는 0인 항목을 생략("바퀴 N 정산: +건물 a +운임 b = +c"). 장부의 Upkeep 항목은 0이면 적지 않음.
- [ ] 선로: `ring-track.json` 5노드 고리 — 황동골(hub-1) → 잿더미 쉼터 → 녹슨 다리 → **잿골(hub-2, `_locked:true`)** → 급수탑 쉼터 → 황동골. 잠긴 노드는 지도에 어두운 폐허 표지와 라벨 "폐쇄된 선로"로 보이고 열차는 멈추지 않고 지나간다(정차·이벤트·승객 없음). `GameDefinition`의 거점 수는 **열린 역**만 센다(결과 "정착 1/1" → 열리면 2).
- [ ] 개통: `TownTiers.json` 정착촌 단계의 `_unlockHubs: ["hub-2"]`. 황동골이 정착촌이 되는 정산 때 hub-2가 열린다: 토스트+배너 "선로가 열렸습니다 — 잿골", 지도 표지가 역 건물(폐허)로 바뀌고 라벨 "잿골 · 폐허", 다음 도착부터 정차·내부 열림·승객 하차(기존 다거점 규칙: NPC `_hubIds`에 hub-2가 있는 사람은 잿골에 내림; `_ignoreHubRoster`는 유지하되 잿골도 후보). 잿골 내부는 폐허 연출로 시작.
- [ ] 마을별 주민·등급: `IPopulation`/`ITownTier`를 hubId 키로(`ResidentsAt(hubId)`, `TierAt(hubId)` 등 append-only 확장; 기존 멤버는 "현재 마을"= 내부가 열린 거점 또는 열차가 선 거점, 없으면 hub-1). 정산은 열린 마을마다 각각(건물 수입은 모든 열린 마을 합산, 주민·등급은 마을별). HUD: 상단 "주민"은 현재 마을, 거점 상태줄·내부 제목·배너는 그 마을 이름(황동골/잿골). 목표 줄은 황동골 도시 그대로(잿골은 보너스).
- [ ] 결과: 승리 조건은 황동골 도시 그대로. 결과 화면 거점 줄에 잿골(열렸으면)도 등급으로 표시.
- [ ] 씬: `BuildTopDownMain.Rebuild` → 생성기 체인(Story012Build가 hub-2 내부도 생성, RuinBuild hub-2 포함) 2회, isDirty=false. 데모 `--line-demo --line-stage locked|opened`(로그 `LINE: …`).
- [ ] 브리프: 유지비 삭제, "일정 이상 진행되면 선로가 열려 마을이 하나 더", "나중에 여러 마을·마을별 멸망 시나리오" 한 줄. 검증은 파이프라인 + `production/qa/evidence/story-048-line-opens-second-village/verification.md`(+ `verification-ui.md`).

---

## Implementation Notes

- 로직(Claude gameplay): `RingTrack`/`TrainController`(잠긴 노드 통과: `ITrackNode.IsLocked`, `Unlock(nodeId)` 이벤트), `GameDefinition`(열린 역), `TownTiers`(`_unlockHubs` → `ILineOpener`), `Population`/`TownTiers` per-hub, `Settlement`(마을별 정산), `Passengers`(hub-2 하차), `WaypointResolver`(잠긴 노드 무시), 데이터(`ring-track.json`, `HubProfiles.json` hub-2 이름 "잿골", `TownTiers.json`, `Economy.json`), 생성기(Story012Build hub-2 내부, MapLabels, Props, RuinBuild).
- UI(Claude ui-programmer): 지도 표지 잠김/개통 상태(`MapNode`/`WaypointMarker` 프리팹 변형은 Presentation(Claude gameplay) — HUD는 토스트·배너·상단 주민(현재 마을)·결과 줄).
- 검증은 파이프라인, 캡처 없음.

---

## Out of Scope

- 마을별 멸망 시나리오(다음 스토리), 세 번째 마을, 선로 분기.

---

## Files

- `Assets/Scripts/Track/*.cs`·`Resources/Track/ring-track.json`, `Assets/Scripts/Bootstrap/GameDefinition.cs`·`Data/MainSettings.json`, `Assets/Scripts/Economy/*.cs`·`Resources/Economy/*.json`, `Assets/Scripts/Contracts/IronRingContracts.cs`, `Assets/Scripts/Npc/Passengers.cs`, `Assets/Scripts/Combat/WaypointResolver.cs`, `Assets/Scripts/Hub/Data/HubProfiles.json`·`RunState.cs`, `Assets/Scripts/Presentation/Map/*.cs`·`Editor/*.cs`, `Assets/Scripts/Presentation/Interior/Editor/Story012Build.cs`·`Editor/RuinBuild.cs`, `Assets/Scripts/Hud/**`(상단·배너·결과), `Assets/Scenes/Main.unity`, `ko.json`, `design/game-brief.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 생성기 멱등 + 씬 저장: `production/qa/evidence/story-048-line-opens-second-village/verification.md`(캡처 없음)

**Status**: [x] Done (2026-11-02 — 데모 locked·opened 로그 확인, 캡처 048-locked.png·048-opened.png)

---

## Dependencies

- Depends on: `story-045-light-economy.md`
- Unlocks: 마을별 멸망 시나리오(미작성)
