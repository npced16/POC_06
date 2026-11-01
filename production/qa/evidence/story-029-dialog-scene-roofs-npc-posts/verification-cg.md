# Story 029(CG) — NPC 초상 제작 검증

- 기준: `production/handoffs/codex-character-cg.md` 및 사용자의 경로/브리지 제한.
- `docs/README.md`는 작업 시작 시 존재하지 않아 인계 문서와 Story 029를 스펙으로 사용.
- 생성 방식: 내장 imagegen, 인물별 독립 생성. 외부 이미지 미사용.
- 산출물: `Assets/Art/Portraits/portrait_<npcId>.png`, NPC 35장 + fallback 1장, 총 36장 저장 완료.
- 최종 규격: 투명 RGBA PNG, 512×640. 원본은 생성 도구 보관 위치에 유지하고 출력 사본을 규격화.
- Unity: 사용자가 연 Editor에 브리지로만 접근. Play 모드 중 refresh 거부 확인 후 기다렸다 재시도했으며, Edit 모드에서 refresh → Assign → refresh 모두 성공.
- 자동 테스트 작성/실행 없음. Git CLI 사용 없음.

## 검증 결과

- 인계 문서의 NPC 35개 ID + fallback과 파일명을 대조: 누락 0, PNG 36개.
- PNG 헤더 확인: 36개 모두 512×640, 8비트 RGBA(color type 6).
- SHA-256 비교: 서로 다른 해시 36개, 동일 파일 중복 없음.
- 각 파일에서 8픽셀 간격으로 5,120개 알파 표본 확인: 완전 투명 표본 1,087~1,834개, 완전 불투명 표본 9~57개. 모든 파일에 투명 배경과 비투명 인물 픽셀이 존재하며 안티앨리어싱 알파를 유지.
- [최종 콘택트 시트](portraits_cg_contact_sheet.png): 1536×2052, 6열×6행, ID 라벨 포함. 개별 PNG와 시트를 육안 확인하여 얼굴·머리·직업 장비 차이 및 팔레트 일관성 확인.
- 굴뚝 청소부와 수석 기술자는 모자 윗부분 잘림을 발견해 이미지 생성 도구로 구도를 보정한 뒤 교체. 얼굴·의상·색을 보존하면서 모자 전체가 보이도록 수정.
- 카탈로그 YAML과 PNG `.meta` GUID 대조: NPC 35개 + fallback 모두 새 `Assets/Art/Portraits/` 스프라이트와 일치. 기존 ID 및 순서를 유지하고 스프라이트 참조만 재배정.
- 36개 `.meta` 확인: Sprite, PPU 100, Bilinear, textureCompression 0. `Assign`에서도 로드된 Sprite 크기를 검증.
- 새 편집기 코드: `Assets/Scripts/Art/Editor/PortraitCgImport.cs`. 기존 카탈로그의 ID를 읽고 모든 스프라이트 로드 성공 후 해당 카탈로그만 저장.
- Game View 대화 장면의 실제 표시 검수는 이번 실행 범위에 포함하지 않음. 검증 범위는 파일, 콘택트 시트, 임포트, 카탈로그 배정, 컴파일 상태.

## 브리지 실행 결과

로컬 `python` 별칭은 실행 경로 오류가 있어 `tools/bridge/README.md`에 명시된 `py -3 -I` 대안을 사용했다. 새 Unity 프로세스 및 batchmode는 사용하지 않았다. `Assign`에는 `--no-save-scene --no-restore-scene`을 지정하여 병행 작업 중인 씬을 저장하거나 전환하지 않았다.

1. 임포트 전 refresh: `py -3 -I tools/bridge/bridge.py refresh --timeout 600`
   - ID: `39e0d4d633994b8fb84fd711067b9e5b`
   - 완료 UTC: `2026-11-01T16:16:44.2538056Z`
   - `ok:true`, `compiling:false`, `failed:false`, `errors:""`.
2. 배정: `py -3 -I tools/bridge/bridge.py execute-method --method IronRing.Art.Editor.PortraitCgImport.Assign --no-save-scene --no-restore-scene --timeout 600`
   - ID: `0e525f1b20b4480d97c4e97aa8d11084`
   - 완료 UTC: `2026-11-01T16:17:24.5074965Z`
   - `ok:true`, 오류 없음.
   - 반환: `PortraitCgImport.Assign: verified 35 NPC sprites + 1 fallback; 512x640; Sprite, PPU 100, Bilinear, Uncompressed; existing ids/order preserved.`
3. 배정 후 refresh: `py -3 -I tools/bridge/bridge.py refresh --timeout 600`
   - ID: `5c162d1eb01a46aca314161fc45d0f43`
   - 완료 UTC: `2026-11-01T16:18:08.2695335Z`
   - `ok:true`, `compiling:false`, `failed:false`, `errors:""`.

Play 모드 중 앞선 refresh 시도는 `Command requires Edit mode`로 거부되었고, Editor를 강제로 종료하거나 Play 모드를 제어하지 않고 재시도했다.

## 공통 생성 프롬프트

Use case stylized-concept. ONE production game dialogue portrait, target 512x640 transparent PNG. Head-to-chest bust, three-quarter angle looking slightly DOWN toward IMAGE LEFT, right shoulder slightly forward. Entire head and shoulders with margin, torso ends at bottom. Old online RPG anime CG, graceful hand-drawn dark plum line art, expressive anatomically natural face, restrained detail, 2-3 clearly separated CEL SHADING tones, NOT photorealism, NOT pixel art. Consistent muted steampunk palette: brass #d6a053 copper #995636 leather #7a4f32 iron #4a4d5a cream #f2decd dark plum #322438 moss #56603a. Transparent background with real alpha, no scenery, no lettering, no border, no watermark. Character: 

## 인물별 지정

| ID | 개별 프롬프트 |
|---|---|
| lamplighter | slim adult man, angular olive face, messy copper hair, chin beard, cap, olive scarf, leather coat, amber lantern |
| gardener | young woman, round freckled fair face, thick auburn braid, moss-green cap, linen apron, seed packets and small brass pruning shears |
| stoker | powerfully built middle-aged dark-skinned man, shaved head under burgundy bandana, broad nose, soot on cheeks, heavy leather apron, furnace poker at shoulder |
| water_keeper | older olive-skinned woman with silver hair in bun, long face, blue-grey high collar coat, copper water gauge brooch, ring of cistern keys |
| tinker | young tan man, narrow face, wild black curls, tiny round spectacles, patchwork leather vest, screwdriver breast pocket |
| peddler | cheerful middle-aged woman, deep skin, full cheeks, braided black hair, patterned burgundy shawl, brass coin necklaces and shoulder satchel |
| glass_blower | slender fair man with swept blond hair, brass goggles on head, cream leather apron, delicate blue glass vial clipped at chest |
| lamp_oiler | young olive woman, short black bob under brown cap, ochre apron, copper oil-can strap, oil stains |
| chimney_sweep | lanky older pale man, long nose, untidy grey hair, battered tall hat, soot streaks, black coat and small chimney brush over shoulder |
| clockmaker | elderly fair woman, sharp face, silver bob, brass monocle and chain, plum tailored coat, tiny watchmaker tools |
| mechanic | athletic young deep-skinned woman, short coiled hair, brass goggles, blue-grey work jacket, wrench in breast pocket |
| pipefitter | stocky tan middle-aged man, square jaw, short brown beard, flat cap, copper pipe fitting at leather vest pocket |
| coal_merchant | portly middle-aged olive man, black mustache, burgundy cap, striped scarf, soot marks, brass scale token |
| telegrapher | slender fair woman, neat dark hair bun, brass monocle, iron-blue uniform, brass wired earpiece and telegraph insignia |
| gas_fitter | lean elderly dark-skinned man, grey sideburns, brown cap, leather collar, small copper pressure regulator and gas pipe wrench |
| engine_driver | confident tan woman, long copper ponytail, forehead goggles, burgundy scarf, dark locomotive jacket |
| brass_polisher | young pale man, soft oval face, flaxen wavy hair, rolled cream shirt and brown waistcoat, polished brass component and cloth tucked at chest |
| signal_keeper | middle-aged deep-skinned woman, broad cheekbones, short grey curls under uniform cap, burgundy uniform with brass signal badge |
| dirigible_rigger | lean tan young man, windswept auburn hair, brass goggles, long blue-grey scarf, leather flight harness with rope knots |
| steam_cook | plump older fair woman, rosy cheeks, grey curls under red bandana, ivory apron, copper ladle tucked into apron |
| ironworker | muscular olive woman, square face, short black undercut, heavy dark leather work vest, iron hammer at shoulder |
| smelter | large dark-skinned middle-aged man, short black beard, raised iron furnace protective face shield, thick leather apron, amber heat reflections |
| night_watch | older tan man, weary long face, grey mustache, dark uniform cap, high-collar navy uniform, warm amber chest lantern |
| riveter | young fair woman, sharp green eyes, copper pixie haircut, leather shoulder guard, brass rivets and rivet hammer |
| foundry_hand | stocky olive middle-aged woman, dark curly hair, raised metal protective mask, soot-streaked cheeks, iron-grey work clothing |
| chief_engineer | elderly dignified man, fair skin, long grey beard and swept grey hair, tall steampunk hat, brass monocle, elaborate plum coat |
| boilermaker | broad tan middle-aged man, prominent curled brown mustache, short hair, brass goggles on forehead, thick leather apron and copper boiler fittings |
| scrap_dealer | wiry older deep-skinned woman, hooked nose, silver dreadlocks tied with teal bandana, mismatched brass earrings, patchwork leather coat |
| tower_keeper | tall fair middle-aged man, long black hair tied back, narrow face, weathered blue-grey cloak, brass water-level gauge pendant |
| patrol | young olive woman, stern angular face, short brown hair under peaked uniform cap, dark plum uniform with brass rank insignia |
| repairer | middle-aged fair man, receding copper hair, bushy eyebrows, short goatee, tan leather work jacket, precision tools at breast |
| electrician | young deep-skinned man, high black curls, brass goggles on forehead, ivory high-collar shirt and blue-grey insulated vest, copper coil insignia |
| lineman | lean pale older woman, grey braid under brown cap, safety harness, burgundy work jacket, copper wire spool at shoulder |
| pump_operator | sturdy tan young woman, round face, dark brown side braid, blue-grey work shirt, leather braces, brass pressure dial at chest |
| valve_keeper | older olive man, bald crown and white side hair, full white mustache, moss vest and copper-red coat, brass valve-wheel brooch |
| fallback | anonymous adult traveler, face mostly shadowed beneath plain dark hood, visible neutral lower face, simple brown cloak with subtle brass clasp, understated readable silhouette |


## 코디네이터 확인 (2026-11-02 01:40 KST)
- 콘택트 시트 육안 확인: 36장 모두 스팀펑크 상반신 일러스트, 직업 장비·얼굴·머리 다름, 팔레트 일관. `029-dialog-cg.png`(--npc-demo --npc-stage panel 260프레임): 대화 장면 왼쪽에 점등부 CG가 투명 배경으로 크게 뜨고 하단 상자(이름·집·출신·대사)와 겹침 없음.
