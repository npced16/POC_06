# 스토리 029: 아트 검수 (AC 1 초상, AC 5 지붕 디테일)

검수일 2026-11-01. technical-artist 서브에이전트가 작성. 열린 편집기에 브리지만 사용했고, play-capture(스크린샷)는 코디네이터 몫이다.

## 1. 초상 (AC 1)

- 생성기: `Assets/Scripts/Art/Editor/SpriteGenPortraits.cs` (`IronRing.Art.Editor.SpriteGenPortraits.Generate`, 메뉴 `IronRing/Art/Generate NPC Portraits`)
- 출력: `Assets/Art/Generated/portraits/portrait_<npcId>.png` 35장, `portrait_fallback.png`, `portraits_contact_sheet.png`. 모두 96×128, PPU 64, Point, 무압축이며 `ArtKit.ConfigureImporter`로 임포트한다.
- 카탈로그: `Assets/Scripts/Npc/Resources/Npc/NpcPortraits.asset` (`NpcPortraitCatalog`). 에셋이 없어서 새로 만들었다. `_entries` 35개와 `_fallback`을 SerializedObject로 채운다. 매번 배열을 다시 쓰므로 여러 번 실행해도 결과가 같다(멱등).
- 그리는 방식: 128×128 PixelCanvas에 그린 뒤 가운데 96열을 잘라 쓴다. 정면에서 살짝 왼쪽을 본다(귀는 오른쪽, 코 그늘도 오른쪽). 빛은 왼쪽 위에서 오며 명암은 2~3단 셀 셰이딩이고, 부위마다 외곽선 레이어(`BeginLayer`/`EndLayer`)를 둘러 타일·건물과 같은 외곽선 룩을 낸다.

### 조합 규칙
시드는 NPC id의 FNV-1a 해시다. `_portrait` 힌트에 값이 있고 허용된 값이면 힌트를, 아니면 시드로 고른 값을 쓴다.

| 부위 | 값 | 시드로 고름 |
|---|---|---|
| 얼굴형 | 갸름(19×26), 둥근(22×24), 각진(초타원 p=3.2) | 3 |
| 피부 | `skin_fair`, `skin_tan`, `skin_olive`, `skin_deep` | 4 |
| 눈 | 점, 큰 눈(흰자), 가는 눈 | 3 |
| 머리 모양 | 짧게, 옆가르마, 긴 머리(뒷머리), 대머리 옆머리, 곱슬, 올림머리 | 6 |
| 머리색 `_hair` | black(iron), brown(wood), copper, grey(smoke·ground), blond(brass), auburn(fabric) | 6 |
| 수염 `_beard` | none, mustache, beard, goatee | 시드 표에서는 none 비중이 높음 |
| 머리 장비 `_hat` | hat(실크해트), cap(납작모자), goggles(이마 고글), mask(올린 용접면), bandana, bare | mask는 힌트로만 나옴 |
| 옷깃 `_collar` | coat(깃·크라바트), apron(셔츠 + 가죽 앞치마), scarf(목도리), uniform(높은 깃·황동 단추·견장) | 4 |
| 장식 `_acc` | none, monocle, pipe, earring, scar, soot(검댕), glow(등불 빛 얼굴) | soot과 glow는 힌트로만 나옴 |

옷 색은 fabric, wood, wall, wall_alt, copper, moss 중 시드로 고르고, 제복이면 iron 또는 wall_alt를 쓴다. 대체 초상은 그늘에 묻힌 실루엣에 황동 테두리 빛을 두르고 눈만 호박색으로 남긴다.

### 힌트 표 (NpcPool.json `_portrait`, 25명)
| id | 힌트 |
|---|---|
| lamplighter | cap, glow |
| gardener | cap, apron |
| stoker | bandana, soot, apron |
| glass_blower | goggles, apron |
| lamp_oiler | cap, apron |
| chimney_sweep | hat, soot |
| clockmaker | monocle, coat |
| mechanic | goggles |
| pipefitter | cap |
| coal_merchant | cap, soot, scarf |
| telegrapher | monocle, uniform |
| gas_fitter | cap |
| engine_driver | goggles, scarf |
| signal_keeper | cap, uniform |
| dirigible_rigger | goggles, scarf |
| steam_cook | bandana, apron |
| smelter | mask, apron |
| night_watch | cap, uniform, glow |
| foundry_hand | mask, soot |
| chief_engineer | hat, monocle, coat, beard, grey |
| boilermaker | goggles, apron, mustache |
| scrap_dealer | bandana |
| patrol | cap, uniform |
| electrician | goggles |
| lineman | cap |

나머지 10명(water_keeper, tinker, peddler, brass_polisher, ironworker, riveter, tower_keeper, repairer, pump_operator, valve_keeper)은 모두 시드로 정해진다. 생성기는 `NpcPoolData`/`NpcDefinition`을 쓰지 않고 JSON을 따로 파싱한다(`_id`, `_portrait`만 읽음). `_portrait`는 JsonUtility가 무시하는 추가 키라서 런타임 데이터 클래스에 영향이 없다.

NpcPool.json 쓰기: 2026-11-01 23:04:56에 썼다. 파일을 다시 읽은 직후 `_portrait` 키만 추가했고, 들여쓰기 2칸과 LF는 왕복해도 그대로였다. 이후 030 에이전트가 23:07:57에 `_teachesTypeId`를 추가했고, 두 키 모두 커밋 0c52d76에 들어 있다(`_portrait` 25개, `_teachesTypeId` 35개).

### 콘택트 시트
- `portraits_contact_sheet.png`(1배), `portraits_contact_sheet_x2.png`(2배). 순서는 NpcPool 순서이고, 마지막 칸이 대체 초상이다.
- 수정 반복 2회: 처음에는 목이 길고 머리가 작아 위쪽이 비었다. 머리를 약 15% 키우고 CY를 70에서 64로 내렸고, 앞치마 셔츠의 흰색이 너무 튀어서 sandstone 계열로 바꿨다.
- 결과: 모자·고글·용접면·두건·실크해트 실루엣이 서로 달라 96×128에서 바로 구분된다. 같은 장비를 쓴 사람끼리도 피부·머리색·수염·옷깃 조합으로 달라 보인다.

## 2. 지붕 디테일 (AC 5)

`Assets/Scripts/Art/Editor/SpriteGenBuildings.cs`를 확장했다. 파일 이름, 조각 구성, 64×64 크기, 마스크 체계는 그대로다. BuildingView는 바꾸지 않았다.

- 질감:
  - machine: 16px 리벳 철판. 세로 이음 돌기, 가로 이음에 황동 리벳, 판마다 밝기 차이를 준다.
  - housing: 6줄 기와 단에 8px 기와를 엇갈려 놓고 아래 끝을 둥글게, 이끼 점을 뿌린다.
  - storage: 3px 이랑 골함석, 21줄마다 판 겹침과 녹 얼룩. 지붕색을 wood에서 ground로 바꿨다(철판 느낌, machine의 iron과 구분).
  - glass: 12×16 쇠 창살, 대각 반사 띠, 일부 칸은 안쪽 호박빛.
- 처마 그림자: 남쪽이 닫힌 조각의 처마 아래 벽 윗줄에 반투명 outline 그림자 1px를 넣었다.
- 바깥 모서리 황동 덮개: 두 닫힌 변이 만나는 모서리에 6×6 황동 판과 리벳을 둔다.
- 벽 띠 창문: 남쪽 이웃이 빈 조각에만 벽이 있으므로 창도 그 조각에만 나온다. 창은 style·mask 시드로 왼쪽, 오른쪽, 양쪽 중 하나에 1~2개 둔다. 가운데(문 자리)는 비우고, 벽 아래에 바닥 그림자 줄을 넣었다.
- 지붕 장식: 북서 바깥 모서리 조각(mask 2, 4, 6)의 왼쪽 위에 환풍기, 안테나, 작은 구리 물탱크 중 하나를 style·mask 시드로 골라 굽는다. 북동 칸의 굴뚝, 가운데의 상징물과 겹치지 않는 자리다. 별도 `ornament_*` 조각과 BuildingView 변경은 필요 없었다.
- 문 2종: 돌계단 2단, 문틀(0번은 sandstone 아치, 1번은 황동), 쐐기돌을 넣었다. 문 몸체는 4px 올라갔다.
- 상징물 19종: 외곽선 레이어 밖에 반투명 그림자 타원을 두고, 쇠 받침판에 황동 리벳을 박았다.

전후 비교: `roofs_before_contact_sheet.png` → `roofs_after_contact_sheet.png`.

## 3. 브리지 결과
| 명령 | 결과 |
|---|---|
| `refresh` (SpriteGenPortraits 추가 후, 수정 후, 지붕 수정 후, 마지막) | 모두 `failed:false`, `errors:""` |
| `execute-method SpriteGenPortraits.Generate` | ok (3회) |
| `execute-method SpriteGenBuildings.Generate` | ok (2회) |
| 카탈로그 확인 | `_id` 35개, `_fallback` 지정됨 |

## 4. 열린 문제
- 팔레트에 `skin_fair`, `skin_tan`, `skin_olive`, `skin_deep` 4색을 추가했다(`Assets/Art/Palette/SteampunkPalette.json`). 기존 색만으로는 읽히는 피부색이 나오지 않았다. 스토리 Files 범위 밖이므로 art-director 확인이 필요하다.
- 지붕 장식과 창 배치는 건물마다가 아니라 style·mask마다 고정이다. 같은 모양의 건물은 같은 장식을 갖는다. 건물마다 다르게 하려면 BuildingView에 오버레이 훅이 필요하다.
- 문 몸체가 4px 올라가서, 문 앞 NPC 자리(hub_phase_view.json의 오프셋)와 맞는지 실제 화면에서 확인해야 한다(코디네이터 캡처).
- 각진 얼굴의 오른쪽 그늘이 다소 넓게 덩어리져 보인다. 기능상 문제는 없다.
- 표정 변화는 범위 밖이다.
