# Story 019: 탑뷰 스팀펑크 환경 아트 적용 (참고 이미지 기준)

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 20:05 KST 코디네이터 브리지 검증 — 컴파일 통과, 생성기 5종·소품 배치 멱등, 콘택트 시트 5장·선로 화면·내부 캡처 육안 확인)
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief Art & audio direction(환경 아트 기준) — 사용자 지시(2026-11-01 19:40): "Top-down Steampunk 2D Game Environment Art 스타일로 참고 이미지를 반영해 달라." 참고 이미지는 저작권상 저장소에 넣지 않고 코디네이터 스크래치패드에 두며, 브리프의 팔레트·모티프 문장이 저장소의 기준이다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 팔레트 데이터 `Assets/Art/Palette/SteampunkPalette.json`: 브리프의 색(돌바닥 #65575c·줄눈 #3a2e44·벽 #54303a/#513647·지붕 #3a3f4a·구리 #995636·황동 #d6a053·연기 #f2decd·창 불빛 #ffcf6a·물 #5b7983·관목 #4f7a3a·외곽선 #322438)과 명암 단계(밝음·기본·그늘)를 이름 붙여 둔다. 모든 생성기는 이 파일을 읽고, 색을 코드에 박지 않는다.
- [x] 해상도: 모든 생성 스프라이트를 64×64(PPU 64, Point 필터, 압축 없음)로 다시 만든다. 파일 이름과 .meta(GUID)는 그대로 두어 프리팹·카탈로그 참조가 끊기지 않는다(새 파일은 추가만). 월드 크기(1칸 = 1유닛)는 변하지 않는다.
- [x] 거점 내부: `interior_floor`는 돌바닥(줄눈·이끼 점·먼지), `interior_empty`는 리벳 박힌 슬레이트 판(가운데 작은 표식), 선로(`rail_*`)는 나무 침목 위 어두운 강철 레일과 황동 볼트, `train`은 장갑열차(어두운 철판·구리 보일러·굴뚝). 타일 13종은 참고 스타일로 다시 그린다: 보일러(리벳 구리 원통·압력계), 증기관(플랜지 달린 황동 관), 톱니장치(큰 톱니 두 개), 가스등(기둥 + 호박색 광륜), 석탄더미(검은 더미 + 주황 불씨), 철골(리벳 강철 보), 벽돌길(돌 포장), 물탱크(리벳 탱크 + 청록 물), 온실유리(유리틀 + 초록 식물), 침상(철제 침대 + 담요), 작업대(나무 작업대 + 공구), 전신주(기둥 + 전선), 굴뚝(벽돌 굴뚝 + 연기 한 줌).
- [x] 선로 화면: `hub_1~4`는 스팀펑크 건물(슬레이트 지붕·적갈색 벽·호박색 창·굴뚝·톱니 장식, 거점마다 모양이 다름), `hub_*_ruined`는 불 꺼진 무너진 지붕, `waypoint`는 신호 기둥 또는 톱니 표지, `npc`·`raider`·시설(`wall`·`turret`·`workshop`)·`items/*`도 같은 팔레트로 다시 그린다. 카메라 배경색은 어두운 보라 회색(팔레트 값)으로 바꾼다.
- [x] 연기: 굴뚝 연기 프리팹 `Assets/Prefabs/Fx/Smoke.prefab`(스프라이트 시트 4~6프레임 루프 또는 파티클, 팔레트 연기색)을 만들고, 복구 중·정착 거점의 건물과 열차 굴뚝에 자식으로 붙인다(폐허·붕괴는 연기 없음). 프리팹 안에서 켜고 끄며, 코드는 상태에 따라 활성만 바꾼다.
- [x] 장식 소품: 나무 상자·배럴·관목·가스등 기둥 소품 프리팹(`Assets/Prefabs/Map/Props/*.prefab`)을 만들고, 선로 화면의 거점 주변에 소품 인스턴스 8~16개를 씬에 배치한다(편집기 메서드로 한 번 놓고, 저장된 씬이 기준). 런타임 생성 금지.
- [x] UI 색: `Assets/UI/Hud/Resources/Hud/*Style.uss`의 색만 팔레트에 맞춘다(짙은 자두색 패널, 황동 테두리, 호박색 강조). 배치·크기·요소 이름은 바꾸지 않는다.
- [x] 스크린샷: (1) 선로 화면(거점 4곳·연기·소품), (2) 내부 화면에 서식지 세 종류가 완성된 모습(`--habitat-demo`), (3) 타일·건물 콘택트 시트. 이전 캡처와 나란히 보고 참고 이미지의 분위기(어두운 바탕·따뜻한 불빛·구리/황동)가 났는지 검증 기록에 적는다.

---

## Implementation Notes

- 생성기는 `Assets/Scripts/Art/Editor/`의 `SpriteGen`(기본·거점·선로·열차), `SpriteGenTiles`(타일 13종), `SpriteGenItems`(옛 아이템)이며 `IronRing.Art.Editor.*.Generate`를 브리지 `execute-method`로 실행한다. 64px로 올릴 때 `SIZE` 상수와 임포터 PPU를 함께 바꾼다. 새 생성기(`SpriteGenFx`, 소품)는 같은 패턴.
- 참고 이미지: 코디네이터가 준 경로(스크래치패드)를 Read로 보고 작업한다. 이미지를 저장소에 복사하지 않는다.
- 거점 내부 그리드는 020에서 4×12로 넓어진다. 생성기는 칸 크기만 다루므로 영향 없다. `InteriorCatalog.asset`·`HubInterior.prefab`은 020이 손대므로 이 스토리는 스프라이트 파일만 덮어쓴다(참조는 GUID로 유지).
- 검증은 열린 편집기 브리지(`refresh`, `execute-method`, 코디네이터 `play-capture`). 스프라이트 생성 뒤 `refresh`로 임포트가 끝나야 캡처에 반영된다.

---

## Out of Scope

- 020: 서식지 카탈로그·그리드 확장. 021: 도감. 애니메이션 캐릭터(걷기 프레임), 조명 시스템(2D Light), 사운드.

---

## Files

- `Assets/Art/Palette/SteampunkPalette.json`, `Assets/Scripts/Art/Editor/SpriteGen.cs`·`SpriteGenTiles.cs`·`SpriteGenItems.cs`·`SpriteGenFx.cs`(새)·`SpriteGenProps.cs`(새), `Assets/Art/Generated/**/*.png`(64px 재생성)
- `Assets/Prefabs/Fx/Smoke.prefab`, `Assets/Prefabs/Map/Props/*.prefab`, `Assets/Prefabs/Map/HubBuilding.prefab`·`Train.prefab`(연기 자식), `Assets/Scripts/Bootstrap/Editor/BuildTopDownMain.cs` 또는 `Assets/Scripts/Presentation/Map/Editor/*`(소품 배치 메서드), `Assets/Scripts/Presentation/Map/MapNode.cs`(연기 활성 토글), 카메라 배경색(`MapCatalog`/씬)
- `Assets/UI/Hud/Resources/Hud/*Style.uss` 6개(색만: 자두색 패널·황동 테두리·호박색 강조·구리 버튼)
- 실제 추가: `Assets/Scripts/Art/Editor/ArtKit.cs`(팔레트 로더·픽셀 캔버스·모티프·임포터·콘택트 시트), `Assets/Scripts/Presentation/Effects/SpriteFrameLoop.cs`(연기 프레임 루프), `Assets/Scripts/Presentation/Map/Editor/PropsBuild.cs`(`PropsBuild.Generate`, 거점마다 소품 4개), `Assets/Scripts/Presentation/HubPhase/RingHubPhaseView.cs`(Smoke 자식 토글), `Assets/Art/Generated/fx/`·`props/`, `Assets/Scripts/Track/Resources/Track/ring-track.json`(`_map._background`만, Rebuild 색 일치용)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷: `production/qa/evidence/story-019-steampunk-environment-art/` + `verification.md`

**Status**: [x] `019-map-depart.png`(--map-depart: 거점 4곳 건물이 각각 다른 모양, 소품 16개, 톱니 경유지, 연기 나는 열차, 보라 회색 배경, 황동·자두색 HUD), `019-interior-habitats.png`(--habitat-demo: 돌바닥·리벳 판 96칸, 새 타일로 완성된 서식지 3종, 021 작업분인 도감 버튼·발견 토스트도 보임), `019-map.png`(기본 시작 = 내부 화면), 콘택트 시트 5장(`contact_sheet`·`tiles`·`items`·`fx`·`props`). 메모: 시작 시 거점이 모두 폐허라 건물 연기는 복구 중 이후에만 보인다(코드로 토글 확인). 선로 화면의 건물이 라벨에 비해 작게 보이므로 거점 스케일(MapCatalog)을 키울지는 사용자 판단.

---

## Dependencies

- Depends on: `story-009-topdown-main-scene.md`, `story-016-habitat-types-clusters.md`
- Unlocks: None (020과 병렬)
