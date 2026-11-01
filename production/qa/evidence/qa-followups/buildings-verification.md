# QA 후속: 건물마다 다른 장식 — 검증 (2026-11-02)

QA 메모(2026-11-01) "건물마다 다른 장식"에 대한 작업. 완성 건물의 꾸밈을 서식지 종류별 데이터 `_decor`로 고른다(난수 없음, 종류마다 늘 같은 결과).

## 데이터 키 (`Assets/Scripts/Habitats/Resources/Habitats/HabitatTypes.json`)
종류마다 선택 블록 `_decor`를 둔다:
- `_roofTint`: `base` | `copper` | `wine` | `moss`. 지붕 색조이며, 팔레트의 copper·wall·moss 쪽으로 명도를 보존해 옮긴다.
- `_windows`: 0 = 지붕 조각의 기본 창, 1 = 나무 덧창 + 꽃상자, 2 = 황동 테 둥근 현창. 문 칸을 뺀 아래 가장자리 칸에 놓는다.
- `_props`: 덧소품 id 1~2개(`SpriteGenBuildings.DECOR`). 첫째는 왼쪽 위 칸부터, 둘째는 오른쪽 아래 칸부터 빈 칸을 쓰고 굴뚝·상징물 칸은 피한다. 빈 칸이 없으면 생략한다.
- 블록이 없으면 `_buildingStyle`별 기본값을 쓴다(`HabitatDecor.DefaultFor`): machine = base·2·steam_pipes, housing = base·1·laundry, storage = base·0·coal_pile, glass = base·1·glass_cap.

| 종류 | 계열 | 색조 | 창 | 덧소품 | 상징물 |
|---|---|---|---|---|---|
| lamp_street | housing | wine | 1 | laundry, water_tank | lamp_post |
| coal_yard | storage | base | 0 | coal_pile, gas_lamp | coal_heap |
| cobble_plaza | housing | base | 2 | cog_sign, water_tank | cobble_arch |
| grand_cogworks | machine | base | 2 | cog_sign, iron_frame | big_gear |
| boiler_room | machine | copper | 0 | twin_chimney, coal_pile | boiler_dome |
| pipe_gallery | machine | moss | 2 | steam_pipes, water_tank | pipe_valve |
| cogworks | machine | wine | 1 | cog_sign, steam_pipes | small_gear |
| iron_depot | storage | copper | 2 | iron_frame, cog_sign | girder |
| cistern | storage | moss | 1 | water_tank, steam_pipes | tank |
| greenhouse | glass | base | 1 | glass_cap, water_tank | glass_roof |
| barracks | housing | moss | 1 | laundry, twin_chimney | bunk |
| toolshop | housing | copper | 0 | cog_sign, steam_pipes | bench_tools |
| telegraph_line | housing | base | 0 | iron_frame, laundry | wire_pole |
| water_tower | storage | wine | 2 | steam_pipes, gas_lamp | water_tower |
| lamp_road | housing | moss | 2 | tele_pole, coal_pile | twin_lamps |
| engine_room | machine | copper | 2 | steam_pipes, coal_pile | engine |
| power_house | machine | wine | 0 | tele_pole, gas_lamp | dynamo |
| repair_bay | storage | base | 1 | iron_frame, laundry | crane |
| blast_furnace | machine | base | 1 | coal_pile, iron_frame | chimney_stack |

카탈로그 표현 값(`InteriorCatalog.asset`): `_buildingWindows`, `_buildingDecor`, `_buildingDecorScale` 0.6, `_decorLift` 0.08. 색조 계열은 `_buildingStyles`에 `style@tint` id로 추가된다.

## 스프라이트 (`Assets/Art/Generated/buildings/`, 64 px, PPU 64)
- 기존 PNG 이름은 모두 그대로 두었다. 기존 .meta 변경 0건이므로 GUID 참조가 유지된다. 내용이 바뀐 PNG는 기존 `buildings_contact_sheet.png` 1건뿐이다.
- 새 PNG는 252개다.
  - `bld_{machine|housing|storage|glass}_{copper|wine|moss}_{00..15}` 지붕 192개
  - `bld_{style}_{tint}_inner_{ne|se|sw|nw}` 오목 모서리 48개
  - `bld_window_1`, `bld_window_2`
  - `bld_decor_{cog_sign, steam_pipes, twin_chimney, water_tank, glass_cap, gas_lamp, tele_pole, coal_pile, iron_frame, laundry}`
- 19종 견본 시트는 `production/qa/evidence/qa-followups/buildings_contact_sheet.png`다. 데이터 순서로 4열이며, 3칸 가로 견본에 문·상징물·굴뚝·창 무늬·첫 덧소품을 그렸다. 견본 아래 띠 색은 계열(쇠 = machine, 벽돌 = housing, 나무 = storage, 물 = glass)이다. Read로 열어 19종이 서로 구별되는지 확인했다.

## 바뀐 파일
- `Assets/Scripts/Habitats/HabitatDecor.cs` (신규)
- `Assets/Scripts/Habitats/HabitatCatalog.cs`: `HabitatType.Decor`, `SetDecor`, 파서 `_decor`. 덧붙이기만 했다.
- `Assets/Scripts/Habitats/Resources/Habitats/HabitatTypes.json`: 19종 `_decor`
- `Assets/Scripts/Presentation/Interior/InteriorCatalog.cs`: 색조 지붕·창·덧소품 조회, `SetBuildingDecor`, `TintedStyleId`
- `Assets/Scripts/Presentation/Interior/BuildingView.cs`: 색조 지붕·모서리, `Decorate`(창 무늬·덧소품, 프리팹 `BuildingProp` 인스턴스만 사용), 로그에 색조·창·소품 표시
- `Assets/Scripts/Presentation/Interior/Editor/Story012Build.cs`: 색조 계열·창·덧소품을 카탈로그에 이름으로 연결
- `Assets/Scripts/Art/Editor/SpriteGenBuildings.cs`: `TINTS`, `WINDOW_PATTERNS`, `DECOR`, `Tint`, `DrawWindowPattern`, `DrawDecor`, `WriteTypeSheet`
- `Assets/Prefabs/Interior/InteriorCatalog.asset`: 생성기가 갱신했으며 기존 줄 삭제 없이 추가만 있다.
- `BuildingPiece.cs`와 프리팹은 바꿀 필요가 없었다.

## 브리지 결과
| 단계 | 결과 |
|---|---|
| `refresh` | ok, `{"compiling":false,"errors":"","failed":false}` |
| `execute-method SpriteGenBuildings.Generate` | ok |
| `execute-method Story012Build.Generate` 1회차 | ok |
| `execute-method Story012Build.Generate` 2회차 | ok, `InteriorCatalog.asset` md5 동일(7a6438be…), 변경 없음 |
| `scene-status` | `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}` |

## 남은 일
- 실제 장면 확인(`--habitat-demo --habitat-stage complete`)은 하지 않았다. 캡처는 코디네이터 몫이다.
- 3칸짜리 일자 건물에서는 둘째 덧소품을 놓을 빈 칸이 없어 생략된다. 4칸 이상 건물에서 둘 다 보인다.
