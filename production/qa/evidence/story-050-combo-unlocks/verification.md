# Story 050 검증: 조합 해금 (로직·데이터)

스펙: `production/epics/iron-ring-train/story-050-combo-unlocks.md` (b1228e6). UI: `production/handoffs/ui-050.md`.

## 데이터 키

`Assets/Scripts/Habitats/Resources/Habitats/CodexUnlocks.json`
- `_starterTiles`: tile_boiler, tile_coal, tile_cobble, tile_bunk, tile_bench
- `_tileUnlocks` (`{_recipe, _tiles[]}`): boiler_room→tile_pipe,tile_chimney · coal_yard→tile_girder · cobble_plaza→tile_gaslamp · barracks→tile_tank · iron_depot→tile_gear · water_tower→tile_glass · lamp_street→tile_wire
- `_recipeRequires` (`{_recipe, _requires}`): power_house←engine_room · blast_furnace←iron_depot
- `_toastSeconds`: 4.0 · `_perEvent`: 0 (떠돌이 상인 설계도는 그대로) · 옛 키 `_starters/_unlockOrder/_perLap` 삭제(남아 있어도 코드가 무시)

`Assets/Scripts/Economy/Resources/Economy/TownTiers.json`: 모든 `_unlockRecipes: []`(TownTiers.cs도 무시), `_demoGrowBuildings`의 lamp_road → lamp_street.

`HabitatTypes.json`: 15종(lamp_road·grand_cogworks·cistern·toolshop 삭제 → `archive/habitats-2026-11-02/removed-habitats.json`), iron_depot = 철골3+작업대1, telegraph_line = 전신주2+철골1.

`Assets/Scripts/Npc/Data/NpcPool.json`: 집이 삭제 건물인 NPC 8명 제거(water_keeper, tinker, chief_engineer, patrol, lamp_oiler, brass_polisher, signal_keeper, pump_operator; 35 → 27명). 삭제 건물을 가르치던 NPC 8명은 남은 건물로 바꿈(lamplighter→telegraph_line, clockmaker→repair_bay, pipefitter→water_tower, ironworker→blast_furnace, foundry_hand→cogworks, tower_keeper→greenhouse, repairer→cogworks, valve_keeper→engine_room). tower_keeper의 `habitatComplete cistern` 조건 제거.

`ko.json`(끝에 추가): `unlock_toast_tiles` "새 타일: {0}", `unlock_toast_recipes` "새 설계도: {0}", `unlock_name_sep` " · ".

## 로그 기대값

Play 시작(항상): `UNLOCK: start tiles tile_boiler,tile_coal,tile_cobble,tile_bunk,tile_bench (5/13) recipes coal_yard,cobble_plaza,boiler_room,barracks (4/15)`

`--unlock-demo` (= `--unlock-stage all`, `Resources/Habitats/UnlockDemo.json` `_chain`, 2.5초 간격, hub-1 내부 자동 열림). 건물 완성은 `HabitatCodex.MarkBuilt`(HabitatCompleted 핸들러와 같은 경로)로 흉내 냄:

```
UNLOCK: demo stage=all
UNLOCK: start tiles tile_boiler,tile_coal,tile_cobble,tile_bunk,tile_bench (5/13) recipes coal_yard,cobble_plaza,boiler_room,barracks
UNLOCK: tile tile_pipe,tile_chimney by boiler_room
UNLOCK: build boiler_room first=True tiles 7/13 recipes 4/15
UNLOCK: recipe none by boiler_room
UNLOCK: tile tile_girder by coal_yard
UNLOCK: recipe iron_depot,repair_bay by coal_yard
UNLOCK: tile tile_gaslamp by cobble_plaza
UNLOCK: recipe lamp_street by cobble_plaza
UNLOCK: tile tile_tank by barracks
UNLOCK: recipe water_tower by barracks
UNLOCK: tile tile_gear by iron_depot
UNLOCK: recipe pipe_gallery,cogworks,engine_room,blast_furnace by iron_depot
UNLOCK: tile tile_glass by water_tower
UNLOCK: recipe greenhouse by water_tower
UNLOCK: tile tile_wire by lamp_street
UNLOCK: recipe telegraph_line by lamp_street
UNLOCK: recipe power_house by engine_room
UNLOCK: rebuild boiler_room changed=False
UNLOCK: recipes known 15/15 unknown=[]
UNLOCK: all tiles 13/13
```
(각 건물 뒤에 `UNLOCK: build <id> first=True tiles n/13 recipes k/15` 줄이 붙는다.)

참고: 새 해금표에서 보일러실(증기관·굴뚝)만으로 재료가 다 갖춰지는 레시피는 없다(배관 회랑은 톱니가 필요) → 스펙의 "배관 회랑·굴뚝 관련 레시피가 알려지는" 문장과 달리 `recipe none by boiler_room`. 배관 회랑은 철골 창고 완성(톱니) 때 알려진다.

토스트: "새 타일: 증기관 · 굴뚝", "새 설계도: 철골 창고 · 정비창" 등(아이템/서식지 카탈로그 이름).

캡처 권장: `--unlock-demo --unlock-stage start`(핫바 5종 시작), `--unlock-demo --unlock-stage first`(보일러실 직후, 약 3초 뒤).

## 브리지

- refresh: **미확인** — 작업 중 POC_06 편집기가 열려 있지 않았음(Temp/UnityLockfile 없음, 실행 중 Unity는 다른 프로젝트 TimeGunner). 큐에 refresh·ping 명령이 남아 편집기가 열리면 실행된다.
- scene-status: 미확인(같은 이유). 씬 YAML은 건드리지 않음.

## 코디네이터 실행 (2026-11-02 22:40 KST)
- refresh failed=false(050 로직+UI, 051 로직 포함), `.bridge/rebuild/Hotbar` + BuildMainCanvas.Generate ok, scene-status isDirty=false.
- `--unlock-demo --unlock-stage all`: 7단계 사슬 → `tiles 13/13 recipes 15/15`. 캡처 050-start.png(핫바 5종), 050-first.png(증기관·굴뚝 자동 배정), 050-all.png.
