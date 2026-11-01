# Story 016 검증 기록: 서식지 종류 카탈로그와 연결 판정

- 날짜: 2026-11-01
- 컴파일: `python -I tools/bridge/bridge.py refresh --timeout 400` → `{"compiling":false,"errors":"","failed":false}` (오류 없음), `compile-status` 동일
- 타일 스프라이트: `execute-method --method IronRing.Art.Editor.SpriteGenTiles.Generate` 성공 → `Assets/Art/Generated/tiles/*.png` 13장, `tiles_contact_sheet.png`(이 폴더에도 복사), `ItemSprites.asset` 항목 25개(타일 13 + 장식 12, 스팀펑크 타일로 재생성; 옛 자연 타일 PNG 삭제)
- 자동 테스트: 브리프 규칙에 따라 두지 않음

## 서식지 카탈로그 (`Assets/Scripts/Habitats/Resources/Habitats/HabitatTypes.json`, 스팀펑크 마을 요소 19종)

타일 13종(아이템 카탈로그 category "tile"): 보일러(tile_boiler), 증기관(tile_pipe), 톱니장치(tile_gear), 가스등(tile_gaslamp), 석탄더미(tile_coal), 철골(tile_girder), 벽돌길(tile_cobble), 물탱크(tile_tank), 온실유리(tile_glass), 침상(tile_bunk), 작업대(tile_bench), 전신주(tile_wire), 굴뚝(tile_chimney)

pattern 조건에서 줄은 " / "로 구분한다(급수탑 = 물탱크 위, 철골 아래 세로 2칸).

| id | 이름 | 방식 | 조건 | 번영 | NPC |
|---|---|---|---|---|---|
| boiler_room | 보일러실 | cluster | 보일러 4 | 5 | 화부 (stoker) |
| pipe_gallery | 배관 회랑 | cluster | 증기관 4 | 5 | 배관공 (pipefitter) |
| cogworks | 톱니공방 | cluster | 톱니장치 4 | 5 | 기계공 (mechanic) |
| lamp_street | 가스등 거리 | cluster | 가스등 4 | 5 | 점등부 (lamplighter) |
| coal_yard | 석탄 야적장 | cluster | 석탄더미 4 | 5 | 석탄 상인 (coal_merchant) |
| iron_depot | 철골 창고 | cluster | 철골 4 | 5 | 철공 (ironworker) |
| cobble_plaza | 벽돌 광장 | cluster | 벽돌길 4 | 5 | 행상 (peddler) |
| cistern | 저수조 | cluster | 물탱크 4 | 5 | 급수원 (water_keeper) |
| greenhouse | 온실 | cluster | 온실유리 4 | 5 | 원예사 (gardener) |
| barracks | 막사촌 | cluster | 침상 4 | 5 | 야간 경비 (night_watch) |
| toolshop | 공구상 | cluster | 작업대 4 | 5 | 수리공 (tinker) |
| telegraph_line | 전신선 | cluster | 전신주 3 | 4 | 전신 기사 (telegrapher) |
| grand_cogworks | 대공방 | cluster | 톱니장치 9 | 10 | 수석 기술자 (chief_engineer) |
| engine_room | 증기 기관실 | pattern | 보일러-증기관-톱니장치 | 7 | 기관사 (engine_driver) |
| blast_furnace | 용광로 | pattern | 철골-철골-철골 / 철골-굴뚝-철골 / 철골-철골-철골 | 12 | 제련공 (smelter) |
| lamp_road | 가로등길 | pattern | 가스등-벽돌길-가스등 | 6 | 순찰대원 (patrol) |
| water_tower | 급수탑 | pattern | 물탱크 / 철골 | 6 | 급수탑지기 (tower_keeper) |
| repair_bay | 정비창 | pattern | 철골-작업대-철골 | 7 | 정비사 (repairer) |
| power_house | 발전소 | pattern | 보일러-톱니장치-전신주 | 9 | 전기 기사 (electrician) |

## 판정 규칙(구현)

- 위·아래 구역(3×6)을 따로 본다. 선로 건너편은 잇지 않는다.
- pattern 먼저(칸 수 많은 것부터): 템플릿 8변형(회전 4 × 거울)을 줄 우선으로 밀어 보고, 아직 쓰이지 않은 칸만으로 맞으면 그 칸을 차지한다. `톱니장치-증기관-보일러`(거울)와 세로 배치는 인정, `증기관-보일러-톱니장치`는 어떤 변형에도 맞지 않아 불인정.
- 그다음 남은 칸에서 같은 타일 상하좌우 묶음을 찾고, 그 타일의 cluster 종류 중 필요 칸 수 ≤ 묶음 크기인 가장 큰 종류 하나로 센다(톱니장치 9칸 묶음 = 대공방 1개, 톱니공방으로 중복 계산하지 않음).
- 한 칸은 서식지 하나에만 속한다.

## 캡처 인자 (코디네이터용)

```
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 240 --screenshot production/qa/evidence/story-016-habitat-types-clusters/habitat-demo.png --extra-args "--habitat-demo"
```

- 프레임 대신 시간으로: `--seconds 6`
- 데모 동작(데이터 `Resources/Habitats/HabitatDemo.json`): hub-1 내부가 열리면(안 열려 있으면 `IInteriorNavigation.Open`) 20프레임 뒤 `IItemPlacement`로 놓는다.
  - 위 구역 가스등 4칸 L자 (1,1),(2,1),(2,2),(2,3) → 가스등 거리 완성
  - 아래 구역 0줄 1~4칸 온실유리 4칸 일자 → 온실 완성
  - 아래 구역 2줄 0~2칸 톱니장치-증기관-보일러(증기 기관실의 거울상) → 증기 기관실 완성
  - 위 구역 0줄 3~5칸 증기관-보일러-톱니장치(순서 틀림) → 완성되지 않아야 함
  - 이어서 `INpcRecruiter.OnHubVisited`를 합류할 때까지 최대 12회 → 점등부·원예사·기관사 중 한 명 합류
- 콘솔 확인 줄: `HABITAT DEMO hub-1: 완성 서식지 engine_room[...], lamp_street[...], greenhouse[...]`(engine_room만 있고 다른 pattern은 없어야 함), `HABITAT DEMO hub-1: 방문 N회째 NPC 합류 → ...`
- 묶음 테두리·완성 문구 표시는 Story 015 화면이 맡는다.

## 스크린샷

- [x] `habitat-demo.png` / `habitat-demo-v2.png`(2026-11-01 17:00 KST, 테두리 수정 뒤) — 초원(위 L자)·연못(아래 일자) 완성, "연못 완성!" 문구, NPC 합류(로그: 방문 N회째 NPC 합류 → tess/mira)
- [x] `habitat-demo-v3-pattern.png` — 데모 데이터에 아래 (2,0~2) 풀-불-물과 위 (0,3~5) 불-풀-물을 추가. 결과: 풀-불-물은 온천(물-불-풀 거울상)으로 완성되어 테두리·"온천 완성!"·패널 "완성 서식지: 온천 · 초원 · 연못"; 불-풀-물은 테두리 없음(미완성). 로그: `완성 서식지 hot_spring[30,31,32], meadow[7,13,14,15], pond[19,20,21,22]`, `방문 4회째 NPC 합류 → mira`.

## 코디네이터 재검증 (2026-11-01 17:25 KST, 스팀펑크 카탈로그)

- `habitat-demo-v4-steampunk.png`: 위 가스등 L자(가스등 거리, 주황 테두리), 아래 온실유리 일자(온실, 초록 테두리), 아래 (2,0~2) 톱니장치-증기관-보일러(증기 기관실 템플릿 보일러-증기관-톱니장치의 거울상, 분홍 테두리), 위 (0,3~5) 증기관-보일러-톱니장치는 테두리 없음(순서가 달라 미완성). 패널 "완성 서식지: 증기 기관실 · 가스등 거리 · 온실", 문구 "증기 기관실 완성!", NPC(원예사) 합류.
- 로그: `완성 서식지 engine_room[30,31,32], lamp_street[7,13,14,15], greenhouse[19,20,21,22] complete=True flag=True`, `방문 2회째 NPC 합류 → gardener`.
- 타일 13종 스프라이트 `Assets/Art/Generated/tiles/`(contact sheet 포함) 육안 확인: 황동·철 톤.
- 자연 타일 시절 캡처는 `archive/legacy-captures/nature-tiles/`로 옮겼다.
