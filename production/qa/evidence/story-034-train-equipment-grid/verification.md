# 스토리 034 검증 — 열차 장비 그리드(칸마다 3×2)와 습격 연동

## 규칙과 수치 (데이터: `Assets/Scripts/Train/Equipment/Resources/Train/TrainEquipment.json`)

- 열차 3량 × 칸마다 3(가로)×2(세로) = 장비 칸 상한 18. 열 0 = 진행 방향 앞, 행 0 = 위. 칸 순번 0 = 맨 앞 칸.
- 장비 5종:

| id | 이름 | 폭 | 효과 |
|---|---|---|---|
| turret | 포탑 | 1 | 자동 사격 초당 0.8발, 피해 1 |
| heavy_turret | 중포 | 2(가로) | 자동 사격 초당 0.3발, 피해 3 |
| armor_plate | 방벽판 | 1 | 최대 내구도 +4 |
| steam_booster | 증기 부스터 | 1 | 통과 속도 +15% (구간 12초 ÷ (1 + 합)) |
| repair_rig | 수리 장치 | 1 | 습격 중 초당 내구도 +0.2 (1이 모일 때마다 +1, 최대치까지) |

- 습격 최대 내구도 = `_hullBase`(10) + 방벽판 합(개당 4). 사거리와 대포는 Raid.json 그대로.
- `TrainLoadout`이 `ITrainLoadout`, `IRaidLoadout`, `IRaidLoadoutStats`(새 계약, append-only)를 구현한다. `TrainEquipmentInstaller`가 Composing에서 둘 다 등록해 033의 임시 `FacilityLoadoutAdapter`를 덮어쓴다. 어댑터 코드는 보존했다. RaidSystem이 습격 시작마다 늦게 읽기 때문에 설치 순서와 무관하다.
- 거점 시설(방벽·포탑·공방)은 핫바 슬롯 8·9(→ tile_tank·tile_glass)와 팔레트(`_facilities: []`)에서 뺐다. 시설 코드는 보존했다.

## 바퀴별 곡선 (Raid.json `_waves`, lap 6 단계를 새로 추가)

| 바퀴부터 | 스폰 간격(둥지당) | 기계병 비율 | 동시 상한 | 유입 HP/초(3둥지) | 평균 접촉 피해 |
|---|---|---|---|---|---|
| 0 | 2.4 | 0.15 | 6 | 3.1 | 1.3 |
| 2 | 1.9 | 0.30 | 8 | 4.6 | 1.6 |
| 4 | 1.5 | 0.45 | 10 | 6.7 | 1.9 |
| 6 (신규) | 1.25 | 0.55 | 12 | 8.8 | 2.1 |

추정 (해석적 계산이며 플레이 실측은 아님. 유효 교전 약 10초, 대포 실효 약 3 DPS로 가정):

| 장비 | 칸 | 포탑 DPS | 내구도 | lap 0 | lap 4 | lap 6 |
|---|---|---|---|---|---|---|
| 포탑 2 + 방벽판 1 | 3 | 1.6 | 14 | 사격 안 해도 생존(피해 약 8) | 클릭해야 간신히(피해 약 12) | 패배 |
| 포탑 4 + 방벽판 2 | 6 | 3.2 | 18 | 여유 | 클릭하면 여유(피해 약 3), 클릭 없으면 패배(약 20) | 클릭해야 간신히(약 15) |
| 포탑 3 + 중포 1 + 방벽판 2 + 수리 1 | 8 | 3.3 | 18 + 회복 2 | 여유 | 여유 | 클릭하면 생존 |

결론: lap 0은 포탑 2 + 방벽판 1로 버티고, lap 4 이후로는 6칸 안팎이 필요하며, lap 6 이후로는 8칸 이상을 권장한다. 둥지(체력 16 × 3)는 대포로만 부술 수 있다. 실측은 `--raid-demo` 로그(`RAID: start … turrets=… repair=…`, `RAID: hit`, `RAID: end`)로 확인해야 한다.

## 표현

- 거점 내부: T 키 또는 핫바의 "열차" 버튼(팔레트 버튼 위)으로 장비 모드에 들어가고, T·Esc(선택이 없을 때)·내부 화면 닫힘으로 나간다. 모드에서는 각 TrainCar 아래 씬 인스턴스 `TrainCarGrid`의 칸 틀 6개가 켜지고, 핫바 9칸이 장비 5종으로 바뀐다(나가면 타일 슬롯이 복원된다). PlacementGhost가 `IPlacementSurface`(TrainEquipmentView)로 스냅·배치·우클릭 떼기·드래그·Ctrl+Z를 처리한다. 중포 고스트는 2칸 폭으로 스냅한다. 장비 조각은 `EquipmentPiece` 프리팹으로 칸 아래 `Pieces`에 동적으로 만들며, 모드 밖에서도 보인다.
- 되돌리기: 장비 모드의 Ctrl+Z는 장비 놓기·떼기를 되돌린다(`TrainEquipmentMode` 기록, 카탈로그 `_undoDepth` 8). 모드를 나가면 기록을 지운다. 타일 되돌리기 기록과는 별개다.
- 습격: 습격 시작 시 RaidView가 열차 칸 위치(`CarPosition`)에 열·행 오프셋을 더한 자리에 장비 스프라이트를 올리고, 포 순번(포탑 → 중포, Slots 순서)마다 자기 칸에서 포탄을 쏜다(`RaidSystem.GunFired`). 플레이어 대포는 맨 앞 칸에서 쏜다.
- 결과 화면: 바퀴 줄 끝에 "· 열차 장비 N칸"을 붙인다.
- 스프라이트 `Assets/Art/Generated/equipment/`(PPU 64): 포탑, 중포, 방벽판, 증기 부스터, 수리 장치, 칸 틀. 콘택트 시트는 `034-equipment-contact-sheet.png`.

## 파일

- 계약: `Assets/Scripts/Contracts/IronRingContracts.cs` (EquipmentSlot, ITrainLoadout, IRaidLoadoutStats 추가)
- 로직: `Assets/Scripts/Train/Equipment/` TrainEquipmentData.cs, TrainLoadout.cs, TrainEquipmentInstaller.cs, TrainEquipmentCatalog.cs, Demo/EquipmentDemoDriver.cs, Resources/Train/TrainEquipment.json, Resources/Train/TrainEquipmentCatalog.asset
- 표현: `Assets/Scripts/Presentation/Interior/` TrainEquipmentView.cs, TrainEquipmentMode.cs, TrainCarGridView.cs, IPlacementSurface.cs, PlacementGhost.cs(배치 면), PlacementEntry.cs(`PlacementKind.Equipment`), Editor/TrainEquipmentBuild.cs
- 프리팹: `Assets/Prefabs/Interior/TrainCarGrid.prefab`, `EquipmentPiece.prefab`, `Assets/Prefabs/UI/Screens/Hotbar.prefab`(TrainToggle)
- 핫바: `Assets/Scripts/Hud/Hotbar/HotbarScreen.cs`, `Hud/Editor/Screens/HotbarScreenBuilder.cs`, `Resources/Hotbar/Hotbar.json`
- 습격: `Assets/Scripts/Combat/Raid/RaidSystem.cs`(장비 수치·수리·GunFired), `Demo/RaidDemoDriver.cs`(단계별 열차 배치), `Resources/Raid.json`(lap 6 웨이브), `Assets/Scripts/Presentation/Raid/RaidView.cs`
- 결과: `Assets/Scripts/Hud/RunResultScreen.cs`. 문자열: `ko.json`(equip_*, hotbar_train/tiles/hint_equipment, result_equipment 등 추가)
- 아트: `Assets/Scripts/Art/Editor/SpriteGenEquipment.cs`, `Assets/Art/Generated/equipment/*`
- 씬: `Assets/Scenes/Main.unity`(hub-1 HubInterior에 TrainEquipmentView, TrainCar 3개 아래 TrainCarGrid 인스턴스)
- 브리프: `design/game-brief.md` MVP 방어시설 줄 → 열차 장비 그리드

## 브리지 결과

- `refresh`: failed=false, errors="" (최종 포함 매회)
- `execute-method IronRing.Presentation.Interior.Editor.TrainEquipmentBuild.Generate` 2회: 둘 다 ok. 두 번째 실행 뒤 씬의 TrainCarGrid는 3개 그대로(멱등)
- `.bridge/rebuild/Hotbar` + `BuildMainCanvas.Generate`: ok, Hotbar.prefab에 TrainToggle 생김
- `scene-status`: isDirty=false, Main.unity

## 캡처 플래그 (코디네이터)

- `--equipment-demo --equipment-stage grid`: hub-1 내부, 장비 모드, 빈 3×2 그리드 3량
- `--equipment-demo --equipment-stage placed`: 포탑 2, 중포 1, 방벽판 2, 부스터 1 배치, 핫바는 장비 목록. 로그 `EQUIP: capture-ready stage=…`
- `--raid-demo --raid-stage mid`: 포탑 2 + 방벽판 1(열차 배치), 습격 중 포탑 사격
- `--raid-demo --raid-stage win`: 포탑 4, 중포 2, 방벽판 2, 수리 1, 부스터 1
- start·lose도 유지된다(lose = 장비 없음, 내구도 4 강제)

## 남은 문제

- 플레이 모드 실행·캡처는 아직 안 했다(코디네이터가 캡처). 그리드 방향은 "거점 내부 열차가 +x로 향한다"는 가정으로 칸 순번을 정했다(TrainCar 회전 −90°). 캡처에서 앞뒤가 반대로 보이면 TrainEquipmentBuild의 index 식만 바꾸면 된다.
- 열차 칸을 클릭해 장비 모드로 들어가는 기능은 넣지 않았다(T 키와 "열차" 버튼만 된다).
- 스토리 메모의 "ItemCatalog에 category equipment 추가" 대신 별도 `TrainEquipmentCatalog`(Resources)를 썼다. 장비가 타일 배치(IItemPlacement)나 핫바 자동 채움에 섞이지 않게 하려는 의도적 편차다.
- 습격 HUD에 "장비 N칸"을 추가하거나 패널을 옮기지는 않았다(선택 사항).
- 곡선은 해석적 추정이다. 실측 플레이테스트가 필요하다.
- Main 씬에 거점 내부가 hub-1 하나뿐이라 그리드도 3개다. 다른 거점이 생기면 Generate를 다시 실행하면 된다.
- `Assets/Fonts/NotoSansKR SDF.asset` 변경은 캔버스 재빌드 때 동적 글리프가 추가된 것으로 보인다.


## 코디네이터 검증 (2026-11-02 02:05 KST)
- refresh failed=false. `034-grid.png`(--equipment-demo --equipment-stage grid 200프레임): 열차 3량 위에 3×2 그리드 3개, 핫바가 장비 목록(포탑·중포·방벽판·증기 부스터·수리 장치), 안내줄 "열차 장비 · 1~9 선택 · 좌클릭 장착 · 우클릭 떼기 · Ctrl+Z 되돌리기 · T/Esc 나가기", "타일" 버튼으로 복귀. `034-placed.png`(stage placed 260프레임): 포탑 2·중포 1·방벽판 2·부스터 1이 칸에 보임(로그 `EQUIP: capture-ready stage=placed … used=7/18`). `034-raid-mid.png`(--raid-demo --raid-stage mid 1300프레임): 열차 장비에서 포탑 사격(`RAID: start … turrets=2+0 repair=0.00/s`).
- 메모: 열차 칸이 내부 격자 1칸 크기라 3×2 장비 칸이 작게 보임 → 장비 모드에서 카메라를 열차로 당기는 후속.
