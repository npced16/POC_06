# 스토리 020 검증 — 서식지 카탈로그 재설계(혼합 묶음)와 설치칸 4×12

날짜: 2026-11-01 · 편집기: 열린 Unity 6000.3.27f1(브리지) · 테스트: 없음(사용자 지시)

## 변경

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Habitats/HabitatCatalog.cs` | `HabitatKind.Set` 추가, `HabitatTileRequirement`(타일 id·최소 수), `HabitatType.CreateSet`, `SetTiles`, `AcceptsTile`. JSON `_kind: "set"` + `_tiles[{_tileItemId,_minCount}]` + `_requiredCount` 파싱. requiredCount ≥ 1이고 최소 수 합 이상이어야 하며, 집합 안 타일 중복은 거부 |
| `Assets/Scripts/Habitats/HabitatMatcher.cs` | 1) 패턴을 먼저 판정(그대로). 2) 묶음형(cluster·set)은 RequiredCount 내림차순·카탈로그 순서로, 종류마다 받는 타일 칸의 상하좌우 flood fill을 돌려 총 칸 수와 타일별 최소 수를 채우면 묶음 전체를 차지. 위·아래 구역은 따로 판정(선로를 건너 잇지 않음). 한 칸은 서식지 하나에만 속함. cluster만 놓고 보면 016과 결과가 같음 |
| `Assets/Scripts/Habitats/Resources/Habitats/HabitatTypes.json` | 19종(아래 표) |
| `Assets/Scripts/Habitats/Demo/HabitatDemoDriver.cs` | `--habitat-stage complete\|incomplete`(기본 complete). incomplete는 `_incompletePlacements`를 놓음 |
| `Assets/Scripts/Habitats/Resources/Habitats/HabitatDemo.json` | 4×12 격자 좌표로 다시 짬(아래 표) |
| `Assets/Scripts/Npc/Resources/Npc/NpcHomesDemo.json` | 아래(0,5)에 물탱크 추가 → 온실(유리 4 + 물탱크 1 = 5). 가스등 4칸 L자는 가스등 거리(≥3)로 그대로 |
| `Assets/Scripts/Interior/Resources/Interior/HubInterior.json` | `_rows 4`, `_columns 12` (96칸) |
| `Assets/Prefabs/Interior/InteriorCatalog.asset` | `_floorMarginColumns 0`, `_floorMarginRows 0`. 스프라이트 참조는 손대지 않음 |
| `Assets/Scripts/Presentation/Interior/Editor/Story012Build.cs` | 프리팹 칸 수가 행×열×2와 다르면 `HubInterior.prefab`을 같은 경로에 다시 만듦(GUID 유지). 다시 만들었거나 칸 수가 다른 씬 인스턴스는 지운 뒤 같은 부모·이름·위치·형제 순서·거점 id로 새로 놓음(`ReplaceMismatchedInteriors`). 바뀐 것이 없으면 씬을 저장하지 않음 |
| `Assets/Prefabs/Interior/HubInterior.prefab`, `Assets/Scenes/Main.unity` | Generate가 다시 만들고 교체한 결과 |

바꾸지 않은 것:
- `ko.json`: 19종 모두 id와 한국어 이름이 그대로라 `habitat_type_<id>` 키를 그대로 씀.
- `Hotbar.json` 데모 칸: (0..1, 1..4)는 새 격자 안이라 그대로.
- 카메라 크기 `_defaultCameraSize 5.5`: 그대로 둠. 96칸 영역은 가로 12 × 세로 9(위 4 + 선로 1 + 아래 4)칸이고, 예전 바닥(6열 + 좌우 여백 3 = 12, 3행 + 여백 1 ×2 + 선로 = 9)과 크기가 같음. 5.5는 16:9에서 세로 11칸, 가로 약 19.6칸이 보여 상·하단 HUD 여유도 남음.
- 번영·NPC 합류·토스트·묶음 테두리·대화: `IHabitatClusters.CompletedAt`의 TypeId/Cells와 카탈로그 id만 쓰고 종류(kind)로 분기하는 곳이 없어(grep으로 확인) 코드 변경 없음.

## 카탈로그 19종

| id | 이름 | 종류 | 조건 | 번영 | NPC |
|---|---|---|---|---|---|
| lamp_street | 가스등 거리 | cluster | 가스등×3 | 4 | lamplighter |
| coal_yard | 석탄 야적장 | cluster | 석탄더미×4 | 5 | coal_merchant |
| cobble_plaza | 벽돌 광장 | cluster | 벽돌길×6 | 7 | peddler |
| grand_cogworks | 대공방 | cluster | 톱니장치×9 | 10 | chief_engineer |
| boiler_room | 보일러실 | set | 보일러≥2·석탄더미≥1, 총4 | 5 | stoker |
| pipe_gallery | 배관 회랑 | set | 증기관≥3·톱니장치≥1, 총4 | 5 | pipefitter |
| cogworks | 톱니공방 | set | 톱니장치≥2·작업대≥2, 총5 | 6 | mechanic |
| iron_depot | 철골 창고 | set | 철골≥3·굴뚝≥1, 총5 | 6 | ironworker |
| cistern | 저수조 | set | 물탱크≥2·증기관≥1, 총4 | 5 | water_keeper |
| greenhouse | 온실 | set | 온실유리≥3·물탱크≥1, 총5 | 6 | gardener |
| barracks | 막사촌 | set | 침상≥3·작업대≥1, 총5 | 6 | night_watch |
| toolshop | 공구상 | set | 작업대≥2·톱니장치≥1·철골≥1, 총4 | 5 | tinker |
| telegraph_line | 전신선 | set | 전신주≥2·가스등≥1, 총3 | 4 | telegrapher |
| water_tower | 급수탑 | pattern | 물탱크 / 철골(세로 2) | 6 | tower_keeper |
| lamp_road | 가로등길 | pattern | 가스등 벽돌길 가스등 | 6 | patrol |
| engine_room | 증기 기관실 | pattern | 보일러 증기관 톱니장치 | 7 | engine_driver |
| power_house | 발전소 | pattern | 보일러 톱니장치 전신주 | 9 | electrician |
| repair_bay | 정비창 | pattern | 철골 철골 / 작업대 작업대(2×2) | 7 | repairer |
| blast_furnace | 용광로 | pattern | 3×3 철골 둘레 + 가운데 굴뚝 | 12 | smelter |

## 데모 배치(hub-1, 줄·칸은 0부터, 위 구역 3줄이 선로에 붙음)

- 공통: 위(3,8)(3,9)(3,10) 가스등 → **가스등 거리**. 위(1,2) 보일러·(1,3) 증기관·(1,4) 톱니장치 → **증기 기관실**.
- complete: 아래(1,8)(1,9) 보일러, (2,8)(2,9) 석탄더미 → **보일러실**(보일러 2·석탄 2, 총 4). 세 종류 완성 후 NPC 합류.
- incomplete: 아래(1,8) 보일러, (1,9)(2,8)(2,9) 석탄더미 → 보일러 1 < 2로 **보일러실 미완성**, 석탄 3 < 4로 석탄 야적장도 아님. 가스등 거리·증기 기관실은 완성(테두리 2개, 아래 묶음은 테두리 없음).

## 브리지 결과

- `refresh`: `{"compiling":false,"errors":"","failed":false}`(마지막 코드 변경 뒤 포함 3회 모두 failed:false)
- `execute-method IronRing.Presentation.Interior.Editor.Story012Build.Generate`: ok → `scene-status` `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}`
- `HubInterior.prefab`: `Cell 0..95` 96개(고유 이름 96). `Main.unity`: `HubInterior hub-1..hub-4` 인스턴스 4개, `HubInteriors` 루트와 `MainUI` 유지(`BuildTopDownMain.Rebuild` 미사용)
- 멱등: Generate를 두 번째로 실행하기 전후 `Assets/Prefabs/Interior/*`와 `Main.unity`의 md5가 모두 같음(OK). scene-status isDirty=false

## 캡처할 것(코디네이터)

1. `--habitat-demo` — 4×12 격자에 가스등 거리·보일러실·증기 기관실 완성, 테두리 3개, NPC 합류 토스트
2. `--habitat-demo --habitat-stage incomplete` — 보일러실 미완성(아래 2×2 묶음에 테두리 없음)
3. `--npc-demo --npc-stage homes` — lamplighter(가스등 거리)·gardener(온실) 집 배정(017 구성)
4. (선택) `--placement-demo` — 새 격자에서 좌표 확인

## 남은 문제

- 스크린샷이 아직 없음(코디네이터 몫).
- 작업 중 `Assets/Fonts/Resources/KoreanPanelSettings.asset`, `NotoSansKR.asset`이 수정된 것으로 표시됨. 이 스토리에서 손댄 파일이 아님(편집기가 동적 폰트 아틀라스를 갱신했거나 019 쪽 작업으로 보임). 커밋할 때 빼거나 따로 판단할 것.
- 묶음형 판정 순서(큰 RequiredCount 먼저)는 사양에 없던 결정임. 예: 톱니장치 9칸 + 작업대 2칸이 붙어 있으면 대공방(9)이 톱니를 먼저 차지해 톱니공방은 생기지 않음. 바꾸려면 기획 판단이 필요함.
- 판정 결과를 Play에서 직접 보지는 않았음(로그 `HABITAT DEMO {hub}: 완성 서식지 ...`로 확인 가능).

## 코디네이터 검수 (2026-11-01 19:55 KST)

- 브리지 `compile-status` failed:false, `scene-status` Main.unity isDirty:false 확인 뒤 `play-capture` 3장.
- 첫 캡처(5.5)에서 위 4행 중 0행이 단계 배너·조건 패널에, 아래 3행이 핫바에 가려져 `InteriorCatalog.asset` `_defaultCameraSize`와 `HubInterior.prefab` `_cameraSize`를 7.0으로 올리고 재캡처(`020-habitat-complete-v2.png`). 첫 캡처는 삭제. 왼쪽 "서식지 조건" 패널의 겹침은 021에서 패널이 없어지며 해소.
- `020-habitat-complete-v2.png`: 증기 기관실(분홍 테두리, 보일러-증기관-톱니장치)·가스등 거리(주황, 가스등 3)·보일러실(노랑, 보일러 2 + 석탄더미 2) 완성, "보일러실 완성!" 토스트, 조건 패널에 셋 나열, NPC 1명 합류.
- `020-habitat-incomplete.png`: 보일러 1 + 석탄더미 3은 테두리 없음(혼합 묶음 최소 수 미달), 나머지 둘은 완성.
- `020-npc-homes.png`: 점등부가 가스등 거리(L자 4칸) 위, 원예사가 온실(유리 4 + 물탱크 1) 옆 칸.
- Editor.log 게임 코드 예외 없음. `KoreanPanelSettings.asset`·`NotoSansKR.asset`의 편집기 재직렬화 변경은 커밋에서 제외(되돌림).
- 판정 순서(큰 묶음 우선)는 에이전트 결정 — 스토리 파일 메모 참고.
