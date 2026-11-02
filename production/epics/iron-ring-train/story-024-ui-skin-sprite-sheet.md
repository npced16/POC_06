# Story 024: UI 스킨 — 사용자 제공 스팀펑크 기어 UI 스프라이트 시트 적용

> **상태 메모(2026-11-02 정리)**: 부분 유효 — 시트 스킨은 026에서 선택 테마 모드로 보관, 기본은 Simple 스킨. 현재 규칙은 story-026 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 22:05 KST 코디네이터 브리지 검증 — 컴파일 통과, 슬라이스·테마·Reskin 멱등, Play 캡처 6장 검수 뒤 수정 6건 반영·재생성. 수정 뒤 재캡처는 편집기가 Play 중이라 대기)
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief Art & audio direction + MVP 7(한국어 HUD). 사용자 지시(2026-11-01 21:20): "UI 스타일이 왜 하나도 안 이쁘지. 이거(스팀펑크 기어 UI 스프라이트 시트) 활용해서 해줘." 시트는 사용자 소유 파일(`Assets/Art/UI/SteampunkUiSheet.png`, 1536×1024, 불투명한 갈색 배경 위에 구리·황동 파이프 프레임, 제목판, 모서리 조각, 진행 막대, 둥근 버튼(톱니·!·?·X), 직사각 버튼 4종(어두움·어두움2·구리·황금), 정사각 톱니 버튼 3종).

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: 2D Sprite 패키지 1.0.0(`ISpriteEditorDataProvider`로 시트 슬라이스), UGUI 2.0.0.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 배경 제거: 시트의 갈색 배경을 투명으로 바꾼 작업본 `Assets/Art/UI/SteampunkUiSheet_keyed.png`를 편집기 밖 스크립트(`tools/art/key_ui_sheet.py`, PIL만 사용)로 만든다. 조각의 짙은 외곽선·구리·황동·어두운 판은 남고 배경만 사라진다. 결과는 콘택트 시트로 눈으로 확인한다(조각 가장자리에 갈색 테가 남지 않음).
- [x] 슬라이스: 같은 스크립트가 조각의 경계 상자를 자동으로 찾아(투명 마스크의 연결 성분) `tools/art/ui_sheet_rects.json`에 이름·rect·9분할 border를 기록하고, 편집기 메서드 `IronRing.Art.Editor.UiSheetSlicer.Apply`가 그 JSON으로 텍스처를 Multiple 스프라이트로 나눈다(PPU 64, Point 아님 — 이 시트는 손그림이라 Bilinear, 압축 없음, FullRect, border = 프레임 두께). 조각 이름: `frame_plain`, `frame_titled`(제목판 포함), `corner_tl/tr/bl/br`(필요 시), `pipe_corner_gear`, `title_plate`, `progress_bar`, `btn_round_gear/alert/help/close`, `btn_dark`, `btn_dark2`, `btn_copper`, `btn_gold`, `slot_dark`, `slot_copper`, `slot_gold`.
- [x] 테마 교체: `UiTheme.asset`의 패널·버튼·슬롯·토스트 스프라이트를 시트 조각으로 바꾼다(패널 = `frame_plain`, 제목 있는 패널 = `frame_titled`, 버튼 = `btn_copper`(눌림 `btn_gold`), 보조 버튼 = `btn_dark`, 닫기 = `btn_round_gear`가 아닌 `btn_round_close`, 슬롯 = `slot_dark`/선택 `slot_gold`, 토스트 = `title_plate`, 진행 막대 = `progress_bar`). 9분할 border를 테마에 두고 `UiKit`이 쓴다. 글자색은 밝은 연기색, 강조는 황동.
- [x] 화면 재스킨: 모든 화면 프리팹(10개)과 부품(슬롯·카드·옵션·팔레트 항목)이 새 스프라이트를 쓴다. 기존 프리팹의 Image 참조를 옛 스프라이트→새 스프라이트로 바꾸는 편집기 메서드 `BuildMainCanvas.Reskin`(멱등)로 적용하고, 제목판·닫기 버튼처럼 새 조각이 필요한 곳은 빌더를 고쳐 재생성한다. 패널 여백(padding)은 프레임 두께만큼 늘린다.
- [x] 결과: 상단 바·배너·핫바·대화·도감·패배·거점 패널·토스트가 시트의 구리·황동 프레임으로 보이고, 글자가 프레임과 겹치지 않는다. 편집 모드 Game 뷰와 Play 캡처(핫바 고스트, 대화, 도감, 패배, 선로)로 확인한다.
- [x] 저작: 시트는 사용자 제공 자산이며 저장소에 둔다(`Assets/Art/UI/`). 생성 스프라이트(`ui_*.png`, SpriteGenUi)는 남겨 두되 테마에서는 쓰지 않는다.

---

## Implementation Notes

- 배경 키잉: 배경은 흐린 갈색 그라데이션이고 조각은 짙은 외곽선이 있다. 큰 반지름 블러(배경 추정)와의 색 거리 + 국소 분산으로 배경 마스크를 만들고, 조각 내부의 구멍은 채운다. 가장자리는 1px 안쪽으로 깎아 갈색 테를 없앤다. PIL만 쓴다(numpy 없음).
- 조각 자동 검출 뒤 이름은 위치·크기 규칙으로 붙인다(1행 큰 프레임 2개, 2행 모서리·제목판·막대·둥근 버튼, 3행 직사각 버튼·정사각 버튼). 애매하면 JSON을 손으로 고쳐도 되게 둔다.
- 슬라이스 적용은 `UnityEditor.U2D.Sprites.ISpriteEditorDataProvider`(2D Sprite 패키지) 사용. `TextureImporter.spritesheet`는 Unity 6에서 obsolete.
- 9분할 border는 프레임 조각의 파이프 두께(약 48~64px)로 두고, 중간 구간이 늘어나도 어색하지 않은지 캡처로 본다. 버튼은 border 24 정도.
- 검증은 열린 편집기 브리지(`refresh`, `execute-method`, `scene-status`), 캡처는 코디네이터.

---

## Out of Scope

- 새 화면·기능, 애니메이션(톱니 회전 등), 사운드.

---

## Files

- `Assets/Art/UI/SteampunkUiSheet.png`(+keyed), `tools/art/key_ui_sheet.py`, `tools/art/ui_sheet_rects.json`, `Assets/Scripts/Art/Editor/UiSheetSlicer.cs`
- `Assets/Scripts/Hud/Ui/UiTheme.cs`(border 필드), `Assets/Prefabs/UI/UiTheme.asset`, `Assets/Scripts/Hud/Editor/UiKit.cs`·`BuildMainCanvas.cs`(Reskin)·`Screens/*ScreenBuilder.cs`(제목판·닫기), `Assets/Prefabs/UI/Screens/*.prefab`·`Parts/*.prefab`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- 키잉 콘택트 시트 + 편집 모드 캡처 + Play 캡처 5장: `production/qa/evidence/story-024-ui-skin-sprite-sheet/` + `verification.md`

**Status**: [x] `keyed_contact.png`(배경 키잉 결과), `024-part1-editmode.png`(편집 모드, Part 1 직후), Play `024-ghost.png`(상단 바·배너·핫바 슬롯·고스트), `024-npc-panel.png`(대화 패널 프레임·둥근 닫기·출신지), `024-passengers.png`(승객 화면 프레임·제목판), `024-codex.png`(도감 프레임·카드·???), `024-map.png`, `024-defeat.png`. 검수에서 나온 겹침·잘림 6건(토스트/제목판, 제목판 폭, 내용 여백, 배너 여백, 완성 배지, 버튼 높이)은 수정·재생성 완료, 재캡처 `024-v2-*.png`는 편집기 Play 종료 뒤 추가.

---

## Dependencies

- Depends on: `story-022-ugui-screens.md`
- Unlocks: None (025와 병렬, 빌더 수정은 025 뒤)
