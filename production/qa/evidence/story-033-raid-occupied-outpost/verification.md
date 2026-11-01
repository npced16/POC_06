# 스토리 033 검증: 습격 — 몬스터에게 점령된 종점을 뚫고 지나가기

- 스토리: `production/epics/iron-ring-train/story-033-raid-occupied-outpost.md`
- 유형: Integration (자동 테스트 없음, 사용자 지시). 증거 = 브리지 컴파일 + 스크린샷 4장(코디네이터 캡처).
- 범위: 로직·표현·HUD·데모. 스프라이트는 아트 에이전트(`SpriteGenRaid.cs`, `Assets/Art/Generated/raid/`)가 `RaidCatalog.asset`에 이름으로 넣는다. 스프라이트가 없으면 카탈로그 색의 사각형으로 그린다.

## 규칙과 수치 (`Assets/Scripts/Combat/Raid/Resources/Raid.json`)

| 항목 | 값 | 비고 |
|---|---|---|
| 점령 경유지 / 장비 기준 거점 | `TERMINAL` / `hub-1` | `_waypointId`, `_hubId` |
| 구간 통과 시간 | 12초 ÷ (1 + `IRaidLoadout.SpeedBonus`) | `_segmentSeconds` |
| 열차 기어가기 | 다음 선로 구간의 0.3까지, 출발은 그 지점부터 이어짐 | `_crawlEdgeFraction` |
| 최대 내구도 | 10 + 방벽판 × 5 | `_hullBase`, `_hullPerWall` |
| 포탑 | 개당 초당 0.8발, 피해 1(중포탑 3), 사거리 7, 열차에서 가장 가까운 몬스터 | `_turretShotsPerSecond`, `_turretDamage`, `_heavyTurretDamage`, `_turretRange` |
| 열차 대포(좌클릭) | 피해 2, 재장전 0.4초, 콜라이더를 비켜 가면 반경 0.6 안의 가장 가까운 몬스터 | `_playerDamage`, `_playerCooldown`, `_clickRadius` |
| 둥지 | 3개(오프셋 데이터), 체력 16, 런 동안 유지, 부서지면 스폰 중지 | `_nestOffsets`, `_nestHp` |
| 몬스터 | 증기 거미 hp 2·속도 2.2·피해 1 / 녹슨 자동인형 hp 5·속도 1.0·피해 3 | `_monsters` |
| 스폰 표(바퀴별) | 0바퀴: 2.4초·자동인형 15%·최대 6 / 2바퀴: 1.9초·30%·8 / 4바퀴: 1.5초·45%·10 | `_waves`(±30% 흔들림, 첫 스폰 0.6초 + 둥지마다 0.5초) |
| 접촉 | 거리 0.45 이하면 내구도 −피해 후 사라짐 | `_contactRadius` |
| 해방 | 둥지 0개 → 이후 습격 없음(그 경유지는 항상 이벤트), 승객 탑승 확률 +0.15 | `_liberatedBoardBonus` |
| 카메라 | 습격 중 종점으로 이동·크기 5, 끝나면 1.2초 동안 되돌림 | `_raidCameraSize`, `_cameraLerpPerSecond` |
| 연출 | 포탄 0.15초, 명중 번쩍임 0.18초, 사망 연기(Fx/Smoke ×0.5) 0.9초 | `_shellSeconds`, `_hitSeconds`, `_smokeSeconds`, `_smokeScale` |
| 난수 | 데이터 시드 33의 LCG, 같은 입력이면 같은 결과 | `_seed` |

결과: 내구도 ≤ 0이면 패배, 통과를 마치면 승리. `WaypointResolver.CompleteRaid`가 주사위 전투와 같은 보상·벌칙(승리: 다음 거점 번영 +5 / 패배: −10)을 적용하고 `EncounterEnded(Victory|Defeat)`를 낸다. 그래서 번영·승객·도감은 바뀐 것 없이 그대로 동작한다. 주사위 전투와 "다음 라운드"는 `Story005Encounters.json`의 `_raidMode:true`로 꺼진다(코드는 그대로 둠). 습격 중에는 `Combat == null`이라 TopBar의 "다음 라운드"가 보이지 않고, `Advance()`는 아무것도 하지 않는다.

장비: 사용자 추가 지시(2026-11-02 01:00)에 따라 `RaidSystem`은 `IRaidLoadout`(포탑·중포탑·방벽판·속도)을 습격을 시작할 때마다 읽는다. 지금은 `FacilityLoadoutAdapter`가 hub-1의 방벽·포탑 시설 수를 감싸서 등록되어 있고, 스토리 034가 열차 장비 제공자로 덮어쓴다.

## 파일

- 계약: `Assets/Scripts/Contracts/IronRingContracts.cs`(`RaidUnit`, `IRaidLoadout`, `IRaid` 추가)
- 로직: `Assets/Scripts/Combat/Raid/RaidData.cs`, `RaidSystem.cs`, `RaidInstaller.cs`(+`RaidTicker`), `FacilityLoadoutAdapter.cs`, `RaidCatalog.cs`, `Resources/Raid.json`, `Demo/RaidDemoDriver.cs`
- 연결: `Assets/Scripts/Combat/WaypointResolver.cs`(`IRaidRunner`, `AttachRaid`, `CompleteRaid`, `IsRaid`), `EncounterData.cs`(`_raidMode`), `Resources/Story005Encounters.json`
- 열차 이동: `Assets/Scripts/Track/TrainController.cs`(`HeldProgressSource`, `SetDepartureHeadStart`)
- 표현: `Assets/Scripts/Presentation/Raid/RaidView.cs`, `RaidPiece.cs`, `Editor/RaidBuild.cs`, `Assets/Scripts/Presentation/Map/MapView.cs`(습격 중 거점 클릭 무시, 기어가는 동안 방향 맞춤)
- HUD: `Assets/Scripts/Hud/RaidScreen.cs`, `Assets/Scripts/Hud/Editor/Screens/RaidScreenBuilder.cs`, `BuildMainCanvas.SCREEN_IDS`에 `"Raid"`(PhaseBanner 다음)
- 문자열: `Assets/Scripts/Localization/Resources/ko.json`(raid_* 9개)
- 승객: `Assets/Scripts/Npc/Passengers.cs`(탑승 확률 + `IRaid.BoardChanceBonus`, 한 줄. Files 범위 밖이지만 해방 보너스에 필요)
- 브리프: `design/game-brief.md` MVP 4(경유지 전투와 방어시설) 줄
- 생성물(RaidBuild): `Assets/Prefabs/Raid/RaidCatalog.asset`, `Monster.prefab`, `Nest.prefab`, `Shell.prefab`, `Hit.prefab`, Main.unity의 MapView 오브젝트에 `RaidView`

## 브리지 결과

- 2026-11-02 00:44~01:10 `refresh` 두 번 모두 `Command requires Edit mode`(편집기가 Play 모드). `scene-status`: Main.unity, isDirty=false, playing=true. `compile-status`: failed=false, errors="". 다만 Play 모드라 새 스크립트가 아직 import되지 않았을 수 있어, 이 결과로는 컴파일 통과를 확인하지 못했다.
- 2026-11-02 01:12 (편집 모드 복귀 뒤) `refresh`: ok=true, compiling=false, failed=false, errors="" — 스토리 033 스크립트 컴파일 통과.
- 남은 브리지 단계(편집 모드에서): `refresh` → `execute-method --method IronRing.Presentation.Raid.Editor.RaidBuild.Generate` 2회(두 번째 로그 `view=unchanged`) → `.bridge/rebuild/Raid` 표식 + `execute-method --method IronRing.Hud.Editor.BuildMainCanvas.Generate` → `scene-status` isDirty=false.

## 캡처 플래그 (코디네이터)

- `--raid-demo --raid-stage start`: 출발 → 종점 습격 0.8초 뒤 `Time.timeScale=0`으로 멈춤(카메라 확대, 둥지, 첫 몬스터; 포탑 2·방벽 1). 로그 `RAID: capture-ready stage=start`
- `--raid-demo --raid-stage mid`: 습격 5초 뒤 멈춤(포탑 2 자동 사격 중)
- `--raid-demo --raid-stage win`: 포탑 6·방벽 3으로 끝까지 통과, 끝나고 0.5초 뒤 멈춤(승리 토스트 "습격을 막아냈다 (+5)")
- `--raid-demo --raid-stage lose`: 포탑 0, 최대 내구도 4, 패배 0.5초 뒤 멈춤(토스트 "열차가 뚫렸다 (−10)")
- `--map-depart`: 기존 출발 데모. 이제 종점에서 습격(약 12초)을 거친 뒤 hub-1로 돌아간다. 500프레임 근처면 습격 중일 가능성이 높다.
- 로그: `RAID: start hull=…`, `RAID: kill …`, `RAID: hit …`, `RAID: nest … destroyed`, `RAID: end victory=…`, `RAID: capture-ready …`
- 멈춤 시점은 습격 경과 시간 기준이다. 캡처 프레임은 `capture-ready` 로그 뒤로 잡는다.

## 미결 사항

- 스크린샷 4장은 아직 없다(코디네이터 캡처).
- 습격 중 배치·대화·도감 입력 차단은 부분적으로만 된다. 지도 거점 클릭은 막혀 있고, 내부 화면은 출발할 때 닫혀서 배치·NPC 대화가 열리지 않는다. 도감 C 키는 막지 않았다(`RaidScreen.IsRaidActive`는 있지만 CodexScreen에 연결하지 않음).
- 끝 토스트의 +5/−10은 ko.json 문구에 고정되어 있다. 실제 값은 `Story005Encounters.json`의 `_victoryProsperity`/`_prosperityPenalty`이다.
- 열차는 습격 동안 다음 구간(TERMINAL→hub-1)의 앞 30%를 기어간다. 시각적으로는 "종점에 닿은 뒤 돌아 나가며 통과"하는 모양이다.
- CombatFx/StateCombatEffects는 습격(Combat == null)을 따로 다루지 않는다. 시작 때 이벤트용 연출이 함께 뜨는지 캡처에서 확인해야 한다.
- 배경은 종점 마커 SpriteRenderer의 최대 sortingOrder − 1이다. 선로 타일 뒤로 가려지는지 캡처에서 확인해야 한다.


## 코디네이터 검증 (2026-11-02 01:17 KST)
- 브리지 `refresh` failed=false(편집기 편집 모드 전환 직후). `RaidBuild.Generate` 2회, `.bridge/rebuild/Raid` + `BuildMainCanvas.Generate`, `SpriteGenRaid.Generate`(카탈로그 배정 포함) 실행, `scene-status` isDirty=false.
- 캡처: `033-raid-start.png`(1000프레임: 종점 폐허 배경·둥지 3·거미 1, 내구도 15/15, 둥지 3·몬스터 1, "클릭으로 사격"), `033-raid-mid.png`(1300프레임: 내구도 13/15, 열차가 종점 구간을 기어가는 중, 포탑 격추 로그 `RAID: kill spider#0 by turret`), `033-raid-win.png`(1900프레임: `RAID: end victory=True hull=25/25`, 승객 3/3, 복구 중), `033-raid-lose.png`(1500프레임: `RAID: end victory=False hull=0/4 … t=3.1`, 폐허 유지).
- 후속(사용자 01:25): 구도를 "열차 쪽 포탑 전선 ← 둥지에서 달려오는 적"으로 바꿀 것(둥지를 종점 너머 한쪽에, 열차는 종점에 멈춰 정면을 보고, 몬스터는 한 방향으로 돌진). 내구도 글자 대비(노랑 위 노랑) 수정.

## 구도 수정 (사용자 01:25 "포탑이랑 전선으로, 적들이 달려오게")

- 이전 캡처의 문제: 둥지 3개가 종점을 삼각형으로 둘러싸고 열차가 한가운데 있었다. 습격 중에는 열차가 hub-1 쪽으로 기어가면서 적에게 등을 보였다.
- 바꾼 구도: 열차 쪽 전선에 맞서 적이 한 방향에서 달려온다.
  - 둥지: 종점 오른쪽(선로 끝 너머)에 세로로 지그재그 배치.
  - 몬스터: 왼쪽으로 열차 머리(진행 방향 맨 앞 끝)를 향해 달려온다. 머리에서 `_contactRadius` 0.45 안에 들어오면 내구도를 깎는다.
  - 열차: 습격 중에는 종점에 멈춰 오른쪽을 본 채 버티고, 끝나면 이전처럼 왼쪽으로 출발한다.
- 바뀐 데이터 (`Raid.json`):
  - `_nestOffsets`: (−1.8, 1.3) / (1.9, 1.1) / (0.3, −1.9) → (4.2, 1.7) / (4.7, 0) / (4.2, −1.7)
  - `_crawlEdgeFraction`: 0.3 → 0 (0이면 정지)
  - 새 값 `_trainCars` 3: 열차 칸 수. 칸 위치 계산에 쓴다.
  - 새 값 `_cameraOffsetX` 1.8: 습격 카메라 중심을 둥지 쪽으로 민다.
  - 새 값 `_nestLabelGap` 1.0: "둥지 N" 라벨을 가장 아래 둥지 아래, 둥지들 가로 평균 위치에 둔다. 배경·"종점" 라벨과 겹치지 않는다.
- 사격: 포탑 자동 사격과 클릭 포탄 모두 맨 앞 칸(`RaidView.CarPosition(0)`)에서 나간다.
  - 공개 조회 함수 `RaidView.CarPosition(int index)`: 0 = 맨 앞 칸. 열차 스프라이트의 진행 방향 길이를 칸 수로 나눈 중심 위치이며, 034의 칸 위 장비가 쓴다.
  - 공개 조회 함수 `RaidView.TrainFront()`: 몬스터가 노리는 지점.
- HUD: 내구도 글자를 어두운 잉크색 (34, 24, 18)과 굵은 글씨로 바꿨다. 노란 채움 위에서 읽힌다. `Raid.prefab`의 m_fontColor와 m_fontStyle 두 줄이 바뀌었다.
- 데모 4단계(start/mid/win/lose)의 로직은 바꾸지 않았다.
- 브리지 (2026-11-02 01:23):
  - `refresh`: ok, failed=false, 오류 없음.
  - `RaidBuild.Generate` 2회: 둘 다 ok. 씬 변경 없음(멱등).
  - `.bridge/rebuild/Raid` 표식과 `BuildMainCanvas.Generate`: ok. 표식은 소비되었다.
  - `scene-status`: Main.unity, isDirty=false, playing=false.
- 남은 확인(캡처): 둥지와 몬스터가 화면 안에 들어오는지(카메라 크기 5, 중심 +1.8), 그리고 열차가 오른쪽을 보는지. 열차 방향은 hub-1에서 들어올 때의 마지막 방향을 그대로 쓴다.

- 코디네이터 캡처(01:45): `033-raid-mid-v2.png`(--raid-demo --raid-stage mid 1300프레임) — 열차가 종점에 멈춰 오른쪽을 보고, 둥지 3개가 오른쪽에 세로 지그재그, 자동인형이 왼쪽으로 열차 머리를 향해 돌진, "내구도 13/15" 어두운 글자로 읽힘, "둥지 3" 라벨이 둥지 아래. 구도 확인. 남은 메모: 습격 HUD 패널이 맨 위 둥지와 가깝다(034에서 HUD 위치 조정 가능).
