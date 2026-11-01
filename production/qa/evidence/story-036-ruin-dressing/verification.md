# Story 036 검증 — 폐허 연출(웅덩이·잡초·잔해·모닥불)

작성: technical-artist 서브에이전트, 2026-11-02. 스크린샷 4장 중 (4) 콘택트 시트만 이 폴더에 있다. (1)~(3)은 코디네이터 캡처 대기.

## 스프라이트 (`Assets/Art/Generated/ruin/`, 64×64, PPU 64, Point, 무압축)

| 파일 | 내용 |
|---|---|
| `ruin_puddle_0_0/1` | 웅덩이 A, 2프레임 잔물결(녹물 섞인 탁한 물, 젖은 가장자리, 녹 얼룩, 기름막 띠) |
| `ruin_puddle_1_0/1` | 웅덩이 B(작은 웅덩이 둘이 이어짐), 2프레임 |
| `ruin_weeds_0` | 바닥 틈에서 자란 풀포기(올리브·황갈) |
| `ruin_weeds_1` | 쇠판 이음새(리벳)를 따라 핀 이끼 띠 |
| `ruin_weeds_2` | 키 큰 마른 풀과 씨앗 이삭 |
| `ruin_rubble_0` | 반쯤 부러진 구리 톱니와 벽돌 더미 |
| `ruin_rubble_1` | 끊어진 녹슨 파이프(황동 플랜지)와 휜 철판 |
| `ruin_campfire_0/1` | 모닥불 2프레임(불꽃·불티 변화, 호박색 광륜, 돌 고리, 엇갈린 장작, 녹슨 드럼통) |
| `ruin_tent_0` | 생존자 천막(덧댄 천, 줄과 말뚝)과 말아 둔 침낭, 깡통 잔 |
| `ruin_contact_sheet.png` | 콘택트 시트(이 폴더에 복사본 있음) |

장식 아이콘은 같은 그림을 쓴다. `Assets/Art/Generated/items/deco_campfire|deco_puddle|deco_weeds.png`이고 `ItemSprites.asset`에 넣었다. items/ 폴더에도 두었기 때문에 SpriteGenTiles를 다시 돌려도 이 항목은 남는다.
팔레트는 `SteampunkPalette.json`의 water·moss·shrub·sandstone·copper·ember·glow·fabric를 쓰고, 물은 water에 copper shade를 30% 섞어 녹물을 냈다. 팔레트 파일은 바꾸지 않았다.

## 배치 규칙과 수치 (`Assets/Scripts/Presentation/Interior/Resources/Interior/RuinDressing.json`)

- 대상: 상태가 `Empty`인 칸. 아이템이나 시설이 한 번이라도 놓인 칸은 소품을 즉시 지우고, 이번 실행 동안 다시 놓지 않는다(치워도 돌아오지 않음).
- 밀도: 폐허 0.35, 붕괴 0.45, 복구 중 0.15, 정착 0. 칸마다 `hash(FNV(hubId) ^ 3607, 칸, 1) < 밀도`로 정하므로 결정적이다.
- 종류 가중치: 웅덩이 3, 잡초 4, 잔해 2, 천막 1. 변형 선택, 지터 ±0.08칸, 좌우 반전도 모두 해시로 정한다.
- 모닥불: 열차 앵커에 가장 가까운 빈 칸 1개를 처음 바인딩할 때 고정한다. 폐허·붕괴·복구 중일 때만 켜지고 6fps로 깜빡이며, `Fx/Smoke.prefab`을 0.35배로 붙인다.
- 단계가 올라 밀도가 내려가면 빠지는 소품은 1초(`_fadeSeconds`)에 걸쳐 투명해진 뒤 파괴된다. 밀도가 올라가면(붕괴) 다시 나타난다.
- 정렬: `InteriorCatalog.CellOrder(2) + 1 = 3`이다. 바닥(0)·선로(1)·칸(2) 위, 타일 조각(10)·건물(12) 아래.
- RuinProp.prefab은 SpriteRenderer와 SpriteFrameLoop만 가지고 콜라이더가 없다. 그래서 칸 클릭·고스트·판정에 끼지 않는다.
- 선로 화면(`_map`): 씬 `RuinProps/RuinProps_hub-1` 아래에 웅덩이 2, 잡초 3, 잔해 1, 모닥불 1(연기 포함)을 프리팹 인스턴스로 놓았다. 정렬은 HubOrder−2(지면), 모닥불은 HubOrder−1이다. `MapRuinProps`가 폐허·붕괴에서는 전부 켜고, 복구 중에는 모닥불만, 정착에서는 모두 끈다.

## 장식 아이템

- `ItemCatalog.json`에 `deco_campfire` 모닥불, `deco_puddle` 웅덩이, `deco_weeds` 잡초를 추가했다. 분류는 `deco`, 태그는 없다.
- `Hotbar.json`의 `_paletteCategories`를 `["tile", "deco"]`로 바꿨다. 핫바 1~9 슬롯은 그대로다.
- 서식지·번영 무관 확인:
  - `HabitatTypes.json`의 모든 레시피는 `tile_*` id만 참조한다.
  - 번영의 `_perPlacedItem`은 모든 거점에서 0이다(`HubProfiles.json`).
  - 서식지 칸 수와 보너스는 완성 클러스터에서만 나온다.
  - 배치와 철거는 기존 `IItemPlacement`(Empty 칸 → ItemPlaced, 되돌리기 동일)를 그대로 탄다.
- `ko.json`에 `item_deco_*` 3개와 `item_category_deco`(장식)를 추가했다.

## 파일

- 새 파일
  - `Assets/Scripts/Art/Editor/SpriteGenRuin.cs`
  - `Assets/Art/Generated/ruin/**`
  - `Assets/Art/Generated/items/deco_*.png`
  - `Assets/Scripts/Presentation/Interior/RuinCatalog.cs`(+ `RuinDressingData`)
  - `RuinDressingView.cs`
  - `Editor/RuinBuild.cs`
  - `Resources/Interior/RuinDressing.json`
  - `Assets/Scripts/Presentation/Map/MapRuinProps.cs`
  - `Assets/Prefabs/Interior/RuinProp.prefab`
  - `Assets/Prefabs/Interior/RuinCatalog.asset`
- 수정한 파일
  - `ItemCatalog.json`, `Hotbar.json`, `ko.json`, `ItemSprites.asset`
  - `Assets/Scenes/Main.unity`: HubInterior hub-1에 RuinDressingView를 붙이고, `RuinProps` 루트를 추가했다.
  - `design/game-brief.md`: Core loop 셋째 줄에 한 구절을 넣었다.
- `PropsBuild.cs`와 `MapView.cs`는 건드리지 않았다. 선로 소품은 새 `RuinBuild.GenerateMap`과 `MapRuinProps`로 처리했다.

## 브리지 결과

- `refresh`: 036 파일은 컴파일 통과(16:32Z, errors 없음).
  - 이후 refresh는 034 쪽 `TrainEquipmentInstaller.cs`·`TrainEquipmentMode.cs` 오류로 실패한다. 036 파일에서 나는 오류는 없다.
- `SpriteGenRuin.Generate`: ok.
- `RuinBuild.Generate` 2회 모두 ok(멱등).
- `scene-status`: isDirty=false.
- 씬 확인: RuinDressingView 1개, MapRuinProps 1개, Ruin_* 인스턴스 7개.

## 캡처 플래그(코디네이터)

1. 폐허 내부: `--habitat-demo --habitat-stage incomplete`
2. 타일이 소품을 덮은 칸: 같은 데모에서 타일이 놓인 뒤 프레임. 데모가 놓는 칸(`_lCells` 하단 0행 1~3열, 1행 1열)에 소품이 없어야 한다. 아래 '남은 문제' 2번을 참고한다.
3. 선로 화면: `--map-depart` 120프레임. hub-1 둘레의 웅덩이·잡초·잔해·모닥불을 본다.
4. 콘택트 시트: 이 폴더의 `ruin_contact_sheet.png`

## 남은 문제

1. 천막 스프라이트는 64px에서 상자처럼 읽힌다. 다음 반복에서 A형 용마루와 입구 그늘을 보강할 것.
2. 캡처 (2)의 "치우기 전/후" 비교를 위해 소품이 있던 칸에 타일을 놓는 데모 스테이지는 따로 만들지 않았다. 지금은 타일이 놓인 칸에 소품이 없는 상태만 증명할 수 있다.
3. 플레이 모드에서 실제로 동작하는지는 보지 못했다(브리지는 Edit 모드만, play-capture 금지). 페이드, 단계 전환, 모닥불 연기 크기는 캡처로 확인이 필요하다.
4. Main.unity diff에는 다른 에이전트(033/034)의 변경이 섞여 있다.


## 코디네이터 검증 (2026-11-02 02:05 KST)
- 첫 캡처 3회가 240초 타임아웃: Play 시작 시 `ArgumentException: Item needs at least one tag: deco_campfire` → `Unknown tag deco`. 원인: 장식 아이템에 태그가 없고, 카탈로그는 알려진 태그만 받음. 수정: `ItemCatalog.json` `_tags`에 `{deco, 장식}` 추가, 장식 3종 `_tags:["deco"]`(서식지·번영은 태그를 쓰지 않음). 이후 refresh failed=false.
- `036-interior-ruined-v2.png`(--habitat-demo --habitat-stage incomplete 150프레임): 폐허 단계 내부에 웅덩이·잡초·잔해·모닥불(연기)·천막이 빈 칸에 흩어짐, 타일이 놓인 칸에는 소품 없음, 핫바에 "열차" 버튼. `036-map-ruined.png`(--map-depart 120프레임): 주조소 건물 둘레에 소품(잡초·웅덩이·등) 표시. 메모: 선로 화면에서 종점 너머 둥지가 화면 오른쪽 끝에 잘림(카메라 크기 조정 후속).
