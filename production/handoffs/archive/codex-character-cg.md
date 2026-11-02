# 코덱스 작업 의뢰: NPC 캐릭터 CG(상반신 초상) 35장 다시 그리기

작성: 코디네이터(Claude), 2026-11-02. 의뢰자: 사용자. 대상 에이전트: codex.

## 목표
스팀펑크 열차 경영 게임 "철환 장갑열차"의 NPC 35명 상반신 초상(CG)을 새로 만든다. 지금 초상은 코드로 생성한 96×128 픽셀 종이 인형(`Assets/Art/Generated/portraits/portrait_<id>.png`)이라 밋밋하다. 사용자가 원하는 느낌은 옛 온라인 게임(마비노기류) 대화 장면의 캐릭터 CG — 화면 왼쪽에 크게 뜨는 상반신 일러스트.

## 결과물 규격
- 파일: `Assets/Art/Portraits/portrait_<npcId>.png` (새 폴더; 기존 Generated 폴더는 건드리지 않음), 투명 배경 PNG, 512×640(가로×세로), 상반신(머리~가슴), 왼쪽 아래를 바라보는 3/4 각도, 화면 왼쪽에 배치될 것을 고려해 오른쪽 어깨가 살짝 앞.
- 스타일: 스팀펑크(황동·구리·가죽·고글·연기), 2D 일러스트(선화 + 셀 음영 2~3단), 팔레트는 `Assets/Art/Palette/SteampunkPalette.json`의 색 계열과 어울리게. 같은 직업이라도 사람이 다르면 얼굴·머리·장식이 다르게. 한 명당 1장. 대체 초상 `portrait_fallback.png` 1장 추가.
- 등록: Unity 임포트 설정은 Sprite(2D and UI), PPU 100, Bilinear, 압축 없음. 등록 방법은 `Assets/Scripts/Art/Editor/SpriteGenPortraits.cs`의 카탈로그 채우기(`NpcPortraits.asset`, `IronRing.Npc.NpcPortraitCatalog`: `_entries[]{_id,_sprite}`, `_fallback`)를 참고해 같은 에셋에 새 스프라이트를 배정하는 편집기 메서드 `IronRing.Art.Editor.PortraitCgImport.Assign`(인자 없는 정적 메서드)을 추가해 둔다. 대화 화면(`Assets/Scripts/Hud/Npc/NpcDialogScreen.cs`)은 `NpcPortraitCatalog.Get(id)`로 읽으므로 코드 수정은 필요 없다(초상 높이 396px에 비율 맞춤).
- 이미지 생성 수단은 자유(도구가 있으면 생성, 없으면 고해상도 절차 생성). 저작권이 불확실한 외부 이미지는 쓰지 않는다.

## 작업 규칙(이 저장소)
- Unity 편집기는 사용자가 열어 둔 상태다. Unity를 새로 띄우거나 batchmode를 쓰지 말 것. 임포트·메서드 실행은 `python -I tools/bridge/bridge.py refresh|execute-method --method <Ns.Type.Method>`(README: `tools/bridge/README.md`).
- 커밋은 작업 단위로 Conventional Commits(한국어 제목), 본문에 `Story: iron-ring-train 029(CG)`, 브랜치는 `feat/npc-portrait-cg`. PR 올려도 됨(main 직접 푸시 금지).
- 자동 테스트 없음. 증거는 `production/qa/evidence/story-029-dialog-scene-roofs-npc-posts/portraits_cg_contact_sheet.png`(콘택트 시트)와 `verification-cg.md`.

## NPC 목록(35명)
| id | 직업 | 서식지 | 거점 | 외형 힌트 |
|---|---|---|---|---|
| lamplighter | 점등부 | lamp_street | hub-1 | {"_hat": "cap", "_acc": "glow"} |
| gardener | 원예사 | greenhouse | hub-1 | {"_hat": "cap", "_collar": "apron"} |
| stoker | 화부 | boiler_room | hub-1 | {"_hat": "bandana", "_acc": "soot", "_collar": "apron"} |
| water_keeper | 급수원 | cistern | hub-1 | {} |
| tinker | 수리공 | toolshop | hub-1 | {} |
| peddler | 행상 | cobble_plaza | hub-1, hub-3 | {} |
| glass_blower | 유리공 | greenhouse | hub-1 | {"_hat": "goggles", "_collar": "apron"} |
| lamp_oiler | 등유 배달부 | lamp_road | hub-1 | {"_hat": "cap", "_collar": "apron"} |
| chimney_sweep | 굴뚝 청소부 | boiler_room | hub-1, hub-4 | {"_hat": "hat", "_acc": "soot"} |
| clockmaker | 시계공 | cogworks | hub-1 | {"_acc": "monocle", "_collar": "coat"} |
| mechanic | 기계공 | cogworks | hub-2 | {"_hat": "goggles"} |
| pipefitter | 배관공 | pipe_gallery | hub-2 | {"_hat": "cap"} |
| coal_merchant | 석탄 상인 | coal_yard | hub-2, hub-3 | {"_hat": "cap", "_acc": "soot", "_collar": "scarf"} |
| telegrapher | 전신 기사 | telegraph_line | hub-2 | {"_acc": "monocle", "_collar": "uniform"} |
| gas_fitter | 가스관 기술자 | lamp_street | hub-2 | {"_hat": "cap"} |
| engine_driver | 기관사 | engine_room | hub-2 | {"_hat": "goggles", "_collar": "scarf"} |
| brass_polisher | 황동 연마공 | toolshop | hub-2 | {} |
| signal_keeper | 신호수 | lamp_road | hub-2 | {"_hat": "cap", "_collar": "uniform"} |
| dirigible_rigger | 비행선 삭구공 | repair_bay | hub-2, hub-4 | {"_hat": "goggles", "_collar": "scarf"} |
| steam_cook | 증기 요리사 | barracks | hub-2 | {"_hat": "bandana", "_collar": "apron"} |
| ironworker | 철공 | iron_depot | hub-3 | {} |
| smelter | 제련공 | blast_furnace | hub-3 | {"_hat": "mask", "_collar": "apron"} |
| night_watch | 야간 경비 | barracks | hub-3 | {"_hat": "cap", "_collar": "uniform", "_acc": "glow"} |
| riveter | 리벳공 | iron_depot | hub-3 | {} |
| foundry_hand | 주조공 | blast_furnace | hub-3 | {"_hat": "mask", "_acc": "soot"} |
| chief_engineer | 수석 기술자 | grand_cogworks | hub-3 | {"_hat": "hat", "_acc": "monocle", "_collar": "coat", "_beard": "beard", "_hair": "grey"} |
| boilermaker | 보일러 제작공 | boiler_room | hub-3 | {"_hat": "goggles", "_collar": "apron", "_beard": "mustache"} |
| scrap_dealer | 고철상 | coal_yard | hub-3 | {"_hat": "bandana"} |
| tower_keeper | 급수탑지기 | water_tower | hub-4 | {} |
| patrol | 순찰대원 | lamp_road | hub-4 | {"_hat": "cap", "_collar": "uniform"} |
| repairer | 정비사 | repair_bay | hub-4 | {} |
| electrician | 전기 기사 | power_house | hub-4 | {"_hat": "goggles"} |
| lineman | 전신 가설공 | telegraph_line | hub-4 | {"_hat": "cap"} |
| pump_operator | 양수기 기사 | cistern | hub-4 | {} |
| valve_keeper | 밸브 관리인 | pipe_gallery | hub-4 | {} |

## 완료 기준
- 36장(35 + 대체) PNG, 콘택트 시트, `PortraitCgImport.Assign` 실행 뒤 `NpcPortraits.asset`이 새 스프라이트를 가리킴, 브리지 `refresh` `failed:false`.
