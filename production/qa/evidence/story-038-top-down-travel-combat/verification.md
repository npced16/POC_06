# Story 038 검증 — 중간역 정차: 탑뷰 디펜스·고철 파밍·고철로 열차 장비

- 날짜: 2026-11-02 · 브랜치 `feat/story-037-travel-view`(커밋 안 함)
- 반영한 지시: 스토리 AC + 사용자 03:30 지시. 적을 죽여도 보상이 없고, 버텨야 고철이 들어온다. 정차에 끝이 없고 파도가 점점 세지며, "출발 (고철 N)"으로 언제든 떠난다. 검증은 파이프라인(컴파일·생성기·scene-status)으로만 하고 캡처는 하지 않는다.

## 규칙
| 규칙 | 구현 |
|---|---|
| 정차 진입 | `_stopView: "top"`이면 열차가 `Raid.json _waypointId`(TERMINAL)에 서는 순간 `RaidSystem.IsOccupied`가 늘 true를 돌려준다(해방된 뒤에도 같다). 그러면 `Begin`이 정차 디펜스(`ActiveStop`)를 연다. 카메라는 `StopTop`(y=−400)으로 가고, `StopViewMode.Active` 동안 `TravelSideView`는 물러난다 |
| 끝 | 시간으로는 끝나지 않는다. 출발 버튼을 누르면 `RaidSystem.Leave()` → 다음 Tick에 승리로 끝나고, 기존 `CompleteRaid(true)` 번영 보상과 hold 해제가 일어나 열차가 떠난다. 내구도가 0이 되면 패배로 끝난다(기존 흐름) |
| 파도 | 파도 번호 w = ⌊버틴 초 ÷ `_stopWaveEverySeconds`⌋ + 바퀴 × `_stopLapWaveBonus`. 입구당 스폰 간격 = max(`Min`, `Start` − w×`Step`), 동시 상한 = min(`Cap`, `Start` + w×`Step`), 자동인형 확률 = min(`Max`, `Start` + w×`Step`) |
| 입구 | `_stopEntrances` 4개(열차 머리 기준). 체력은 `_stopEntranceHp`이고 정차마다 되살아난다(`_stopEntranceRespawn`). 다 부수면 그 정차 동안만 스폰이 멈춘다. 종점 해방이나 길가(037) 습격과는 관계없다 |
| 적 이동·사격 | 적은 열차 몸통(머리–꼬리 선분)에서 가장 가까운 점으로 달려온다. 포탑·중포의 대상도 선분까지의 거리로 고른다. 포탄은 장착 칸 위치에서 나간다 |
| 고철 수입 | 적 처치로는 0이다. 인부가 캐서 열차까지 돌아와야 들어온다(`_scrapPerSecond`×시간, 한 번에 `_carryMax`). 출발하면 버틴 초 × `_holdBonusPerSecond`(내림)를 보너스로 받는다 |
| 출발 시 운반분 | `_fleeKeepCarried` false면 인부가 들고 있던 고철은 버리고 창고 고철만 가져간다. 패배하면 보너스도 운반분도 없다 |
| 인부 | 상태 Idle→ToPile→Mining→Returning→(남은 양이 있으면 다시 ToPile)이고, 위협을 받으면 Fleeing으로 간다. 전이표(`ScrapFarming.Transitions`)에 없는 전이는 예외를 던진다. 적이 `_fleeDistance` 안에 오면 도망쳐서 배정이 풀린다(피해 없음, 다시 클릭해야 한다). 더미를 클릭하면 가장 가까운 쉬는 인부가 간다 |
| 더미 | `_piles` 5개(양 4·4·5·5·3). `_respawnPiles`가 켜져 있으면 정차마다 다시 채운다. 양이 정해져 있어서 한 번 정차로 얻는 고철에는 자연 상한이 있다 |
| 장비 비용 | `TryPlace`가 비용을 낸다. 모자라면 거절하고 `PlacementRefused` → 핫바 알림이 뜬다. `TryRemove`는 비용 × `_refundRatio`(내림)를 환급한다. 데모·`ApplyDemo`는 `PlaceFree`를 쓴다. 서식지 타일은 비용이 없다 |
| HUD | 상단 바퀴 줄에 "· 고철 N"이 붙는다. 정차 중 출발 버튼은 "출발 (고철 N)"이며, N = 창고 + 지금 받을 보너스이고 언제든 누를 수 있다. 습격 패널 둘째 줄은 "버틴 시간 N초 · 파도 W · 고철 N(+인부 운반 중 n) · 인부 a/b"이다. 핫바 장비 칸은 이름 아래에 "고철 N"을 보이고 살 수 없으면 붉고 흐리게 칠한다. 결과 화면에 "· 고철 N"이 붙는다 |
| map 경로 | `_travelView: "map"`이어도 `_stopView: "top"`이면 정차 화면이 맡고 RaidView는 쉰다. `_stopView: "none"`이거나 `--raid-demo`이면 예전 033 도착 습격이 그대로 돈다 |

## 좌표 사상
정차 좌표 = `StopTop` 루트 로컬이다(X = 선로 방향, 오른쪽이 열차 진행 방향 / Y = 옆). 열차 중심은 화면 왼쪽 1/3(`-반폭 + 2·반폭·TrainScreenFraction`)에 두고, 머리 X = 중심 + 길이/2로 잡는다. 입구·더미·인부 집 오프셋은 모두 열차 머리를 원점으로 한다. `RaidUnit.X/Y`와 `ScrapWorker/ScrapPile.X/Y`가 이 좌표를 그대로 쓴다. 월드 변환은 `StopTopView.ToWorld/ToLocal`이 맡는다.

## 데이터 키(신규·변경)
| 파일 | 키 | 값 |
|---|---|---|
| `Train/Scrap/Resources/Train/Scrap.json`(신규) | `_startScrap` | 6 |
| | `_refundRatio` | 1.0 |
| | `_holdBonusPerSecond` | 0.25 |
| | `_fleeKeepCarried` | false |
| | `_demoHoldSeconds` | 12 |
| | `_scrapPerSecond` / `_carryMax` | 1.0 / 3 |
| | `_workers` / `_workerSpeed` / `_fleeSpeedFactor` | 2 / 1.8 / 1.4 |
| | `_fleeDistance` / `_keepCarryOnFlee` / `_arriveDistance` | 1.6 / true / 0.12 |
| | `_respawnPiles` / `_homeX` / `_homeY` / `_workerSpacing` | true / −3.3 / −1.0 / 0.4 |
| | `_piles` | (−1.6,2.8,4) (3.6,2.3,4) (4.6,−2.9,5) (−7.4,3.0,5) (−8.2,−3.2,3) |
| `TrainEquipment.json` | `_cost` | 포탑 3 · 중포 6 · 방벽판 2 ·부스터 4 · 수리 3 |
| `Raid.json` | `_stopEntrances` | (9.6,1.0) (3.0,5.3) (−6.0,−5.3) (−9.8,2.2) |
| | `_stopEntranceHp` / `_stopEntranceRespawn` | 16 / true |
| | `_stopWaveEverySeconds` | 6 |
| | `_stopSpawnIntervalStart/Step/Min` | 3.2 / 0.35 / 0.9 |
| | `_stopAutomatonStart/Step/Max` | 0.1 / 0.08 / 0.6 |
| | `_stopMaxAliveStart/Step/Cap` | 4 / 2 / 16 |
| | `_stopLapWaveBonus` | 1 |
| `MainSettings.json` | `_stopView` | "top" ("none" = 예전 동작) |
| `TravelCatalog`(코드 기본값) | `StopCameraSize` 6 · `StopCarDepth` 1.1 · `StopEntranceSize` 1.4 · `ScrapPieceSize` 0.32 · `WorkerSize` 0.34 + 색 10개 | |
| `ko.json`(뒤에 추가) | `hud_scrap` `result_scrap` `equip_cost` `equip_no_scrap` `stop_station_label` `stop_toast_arrive` `stop_toast_depart` `stop_toast_lost` `stop_no_worker` `stop_worker_flee` `stop_pile_label` `stop_depart_button` `stop_status_line` | |

`_stopSeconds`는 지시에 따라 넣지 않았다(정차에 끝 시간이 없다).

## 파이프라인 결과
- `bridge refresh`: 첫 실행은 실패했다(CS0103 `GameServices`: 새 파일 4개에 `using IronRing.Contracts.Presentation` 누락). 수정 후 다시 돌려 `ok=true, failed=false`.
- `execute-method IronRing.Presentation.Travel.Editor.StopTopBuild.Generate` 2회: 둘 다 `ok=true`. 1회차에 `Assets/Prefabs/Travel/StopTop.prefab`을 만들고 Main.unity에 StopTop(y=−400)을 놓아 저장했다. 2회차는 프리팹·씬 인스턴스가 이미 있어 아무것도 바꾸지 않았다. 브리지 출력에는 "unchanged" 로그 문자열이 나오지 않으므로, 멱등은 씬이 dirty가 아닌 것으로만 확인했다.
- `scene-status`: `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}`.
- HUD 프리팹 재생성: 하지 않았다. 직렬화 필드를 추가하지 않고 기존 `_lap`, `_countLabel`, 출발 버튼 라벨, 핫바 이름 텍스트만 코드에서 바꾼다.
- 플레이 실행·캡처: 하지 않았다(지시).

## 데모(최소)
`--stop-demo --stop-stage arrive|farm|wave|depart [--travel-speed 4]`. 포탑 1·방벽판 1은 고철로 산다(`SCRAP: spend turret 3`, `SCRAP: spend armor_plate 2`). 포탑 1은 무료로 놓는다. 그다음 출발해 정차 디펜스 안에서 멈추고 `STOP: capture-ready stage=… held wave hull monsters scrap carried busy` 로그를 남긴다. 단계별로 멈추는 시점은 arrive 0.4초, farm 인부 1명 + 3초, wave 8초 이후 포탑 발사 직후, depart `_demoHoldSeconds`(12)초이다. `--travel-demo`의 장비 배치는 `PlaceFree`로 바꿨다(고철 때문에 거절되지 않게).

## 열린 문제
1. 플레이 검증을 하지 않았다. 정차 진입, 출발 버튼으로 떠나기, 횡스크롤 복귀를 실제로 돌려 보지 않았다.
2. 출발 버튼에서 "N"은 창고 + 보너스이다. 버려지는 인부 운반분은 HUD 패널 줄에 "(+인부 운반 중 n)"으로 따로 보인다.
3. 정차가 끝나면 열차가 바로 떠난다. 승리 시 보상과 승객 탑승은 기존 `EncounterEnded(Victory)` 흐름을 그대로 탄다. 정차에서 "패배"한 경우의 벌칙(자원 감소·번영)도 기존과 같다.
4. 입구를 모두 부수면 그 정차 동안은 안전하게 파밍할 수 있다. 다만 더미 양이 유한해서 상한이 있다. 디자인 확인이 필요하다.
5. `Assets/Fonts/NotoSansKR SDF.asset`이 바뀌어 있다(새 한글 글자로 TMP 동적 아틀라스가 갱신된 것으로 보인다). 커밋에 포함할지 코디네이터가 판단해야 한다.
6. 브리프 문장은 지시대로 손대지 않았다.
