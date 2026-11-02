# 스토리 048 검증 — 로직·데이터·지도/내부 (gameplay)

스펙: `production/epics/iron-ring-train/story-048-line-opens-second-village.md`. 캡처 없음(파이프라인 검증). UI 쪽은 `verification-ui.md`.

## 1. 유지비 없음
- `Economy.json` `_trainUpkeep` 6 → **0**.
- `Settlement.Settle`: `TrainUpkeep > 0`일 때만 돈을 빼고 장부에 Upkeep을 적는다(0이면 장부 항목 없음).
- 토스트: `Settlement.PartFormats`(ko.json `econ_lap_head`/`econ_part_buildings`/`econ_part_fare`/`econ_part_upkeep`/`econ_lap_total`)로 0인 항목을 뺀다.
  예: 건물 12, 운임 0 → "바퀴 3 정산: +건물 12 = +12". 옛 `econ_lap_settled`는 남겨 둠(키는 append-only), 쓰지 않음.

## 2. 선로 (ring-track.json, 5노드)
| 순서 | id | 이름 | 역 | 잠김 | 지도 좌표 | 굽힘 |
|---|---|---|---|---|---|---|
| 0 | hub-1 | 황동골 | 예 | 아니오 | (−7,1) | (−7,8) |
| 1 | REST-1 | 잿더미 쉼터 | 아니오 | — | (0,8) | (7,8) |
| 2 | REST-2 | 녹슨 다리 | 아니오 | — | (7,1) | 없음(직선) |
| 3 | **hub-2** | **잿골** | 예 | **예** | (7,−4) | (7,−6) |
| 4 | REST-3 | 급수탑 쉼터 | 아니오 | — | (0,−6) | (−7,−6) |

- 잿골은 기존 오른쪽 세로 선로 위에 놓여 고리 모양·카메라(`_cameraSize` 9.5)가 그대로다.
- `RingTrack.Arrive`: 잠긴 노드에 닿으면 위치만 넘기고(`PositionChanged`, `PassedLocked`) 계속 달린다. `Arrived`·`WaypointReached` 없음 → 정차·내부·승객·이벤트 없음. 첫 노드는 잠길 수 없다(생성자 검사).
- `RingTrack.Unlock(nodeId)` + `NodeUnlocked`. `ITrackNode.IsLocked`(기본 구현 false, append-only).
- `WaypointResolver.FindNextHub`는 잠긴 역을 건너뛴다. `Passengers`의 거점 후보(PriorityHub·ConditionHub)는 `ILineOpener.IsOpen`으로 거른다.

## 3. 개통 규칙
- `TownTiers.json` 정착촌(camp) `_unlockHubs: ["hub-2"]`(`_unlockStops` 옆).
- `TownTiers.EvaluateAt`: 첫 마을(`_hubId` hub-1)이 그 등급에 오르면 `HubUnlockRequested("hub-2")` → `EconomyInstaller` → `ILineOpener.Open("hub-2")`
  → 로그 `LINE: opened hub-2`, `HubOpened` → RunState `SetHubOpen(true)`, 주민·등급에 잿골 추가, 토스트 `line_opened` "선로가 열렸습니다 — 잿골".
- 순서: 정산 토스트 → 등급 토스트 → 개통 토스트. 잿골은 다음 정산부터 센다. 다음 도착부터 정차·내부 열림(MainRoot의 기존 `Arrived` 경로).

## 4. 마을별 주민·등급
- `Population`: 마을마다 주민·주거·빛·수입. `Income` = 열린 모든 마을 합(정산 건물 수입). `SettleAt(hub, lap)`.
- `TownTiers`: 마을마다 등급·붕괴. 레시피·업그레이드·휴식지 해금은 전역 누적, `_unlockHubs`는 황동골 등급만.
- 정산(`AfterSettle`): 열린 마을마다 등급 판정 → 주민 증감 → `RunState.RefreshPhase`. 로그 `TOWN: lap=N hub=… tier=…`.
- "현재 마을"(`CurrentHubId`): 내부 화면 거점 → 열차가 선 열린 역 → hub-1. 옛 멤버(`Residents`, `Tier` …)는 현재 마을 값.
- `RunState`: 잠긴 역은 바퀴 번영 규칙을 받지 않고 `HubCount`/`SettledCount`/결과 요약에서 빠진다(정착 1/1 → 열리면 2/2). 마을마다 TownLink를 붙이되 첫 링크(hub-1)만 승패를 정한다(도시 = 승리, 붕괴 = 패배). 잿골 붕괴 = 토스트 `town_collapsed_bonus`.
- `GameDefinition.LockedHubIds`/`OpenHubIds`(잠긴 역도 프로필·레시피·내부는 처음부터 갖는다).

## 5. 데이터 키
- `ring-track.json` 노드 `_locked`; `TownTiers.json` `_unlockHubs`; `Economy.json` `_trainUpkeep` 0; `HubProfiles.json` hub-2 `_name` "잿골".
- ko.json: `hub_2` "정원" → "잿골"(그 자리에서 바꿈), 끝에 추가: `econ_lap_head`, `econ_part_buildings`, `econ_part_fare`, `econ_part_upkeep`, `econ_lap_total`, `line_opened`, `map_line_closed`, `map_hub_ruin`, `town_collapsed_bonus`.

## 6. 지도 표지
- `BuildTopDownMain`: 잠긴 역의 MapNode에 `MapLockedHub`(역 건물 렌더러·라벨·클릭 콜라이더·문구 "폐쇄된 선로"/"잿골 · 폐허"). 역 그림이 없는 거점은 첫 역 그림을 쓴다.
- `MapLockedHub`: 잠긴 동안 건물을 어두운 색(0.22,0.2,0.24)으로, MapLabel 이름 "폐쇄된 선로", 단계 줄 숨김, 클릭 끔, `RuinProps_hub-2` 렌더러 끔. `HubOpened`이면 원래 색·"잿골 · 폐허"·클릭·폐허 소품(단계 규칙은 MapRuinProps).

## 7. 생성기 체인 (브리지, 2026-11-02)
- `refresh`: `{"compiling":false,"errors":"","failed":false}`.
- 1회차: `BuildTopDownMain.Rebuild` → `Story012Build.Generate` → `BuildMainCanvas.Generate` → `MapLabelsBuild.Generate` → `PropsBuild.Generate` → `RuinBuild.Generate` → `RuinBuild.GenerateMap` 모두 ok.
- 2회차(Rebuild 뒤 생성기 6개 재실행, 멱등 확인): 모두 ok.
- `scene-status`: `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}`.
- 씬에 hub-2: 지도 노드 `hub-2`(MapLockedHub `_lockedText` "폐쇄된 선로"), `RuinProps_hub-2`, hub-2 내부 인스턴스 덮어쓰기 값 확인(씬 YAML은 읽기만 함).

## 8. 데모
- `--line-demo --line-stage locked`: 역에서 출발 → `LINE: pass hub-2 locked` → 황동골 복귀 `LINE: lap done`.
- `--line-demo --line-stage opened`: 시드 → `LINE: opened hub-2` → `LINE: seeded tier=정착촌 open=True hubs=2` → `LINE: arrived hub-2 interior=true`.
- 실행은 코디네이터 몫(이 에이전트는 play 캡처를 하지 않음). 미실행.

## 코디네이터 데모 실행 (2026-11-02 10:55 KST, 브리지 play-capture)

| 단계 | 인자 | Editor.log | 캡처 |
|---|---|---|---|
| locked | `--line-demo --line-stage locked` | `LINE: demo stage=locked locked=[hub-2] hubs=1` → `LINE: pass hub-2 locked` → `LINE: lap done lap=1 hubs=1` | `048-locked.png` (황동골·폐허, 주민 3, 정착 대상 5) |
| opened | `--line-demo --line-stage opened` | `LINE: opened hub-2` → `LINE: seeded tier=정착촌 open=True hubs=2` → `LINE: arrived hub-2 interior=True residents=3 tier=폐허 current=hub-2 hubs=2` | `048-opened.png` (잿골 내부 폐허, 상단 "현재 위치 잿골 · 잿골 주민 3", 목표 줄은 황동골 기준) |

- 예외·컴파일 오류 없음. 실행 뒤 `scene-status` isDirty=false, playing=false.
- 휴식지 이벤트가 열차를 붙잡아 locked 데모가 멈출 위험은 실제로 나타나지 않았다.
