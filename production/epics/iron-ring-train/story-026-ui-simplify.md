# Story 026: UI 단순화 — 장식 스킨은 보관, 깔끔한 기본 스킨으로

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 — 커밋 e8986d1·806592d, 검증 production/qa/evidence/story-026-ui-simplify/verification.md; 문서 상태만 늦게 갱신)
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 7(한국어 HUD). 사용자 지시(2026-11-01 22:15): "UI가 전반적으로 쩅하고 구려. 싹 다 날리자. 저런 스타일(시트)로 나중에 쓸 수만 있게 하고 간단하게만 하자. UI는 나중에 디테일 잡는 느낌으로."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: LOW
**Engine Notes**: UGUI 2.0.0 + TMP.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 테마에 스킨 모드(`_skin`: `Simple` | `Sheet`)를 두고 기본값은 `Simple`. `Simple`은 장식 없는 깔끔한 패널(어두운 반투명 바탕, 1~2px 황동 테두리, 모서리 둥글기 없음 또는 4px), 작은 사각 버튼(같은 테두리, 눌림은 밝기만), 단순 슬롯(어두운 사각 + 선택 시 황동 테두리), 토스트는 글자만 있는 얇은 띠. 제목은 제목판 없이 굵은 라벨, 닫기는 작은 "닫기" 텍스트 버튼.
- [ ] `Sheet` 모드는 024의 결과(시트 조각·제목판·둥근 닫기)를 그대로 보존한다. `UiSheetTheme.ApplySheetTheme`/`ApplySimpleTheme` 편집기 메서드로 테마를 바꾸고 `BuildMainCanvas.Generate`(전체 표식)로 재생성하면 두 스킨을 오갈 수 있다. 시트·슬라이서·키잉 스크립트·조각 JSON은 삭제하지 않는다.
- [ ] 여백·글자는 024의 4단계 글자 체계와 줄 높이·버튼 높이 규칙을 유지하되, Simple에서는 패널 pad 12~16, 버튼 높이 36으로 줄여 화면이 덜 답답하게 한다. 겹침·잘림 없음.
- [ ] 화면 11개와 부품을 Simple로 재생성하고 Play 캡처(핫바·대화·승객·도감·패배·선로)로 확인한다. `production/qa/evidence/story-026-ui-simplify/` + verification.md.

---

## Implementation Notes

- `UiKit`의 `Panel/TitlePlate/CloseButton/Bar/Slot/Button`이 테마의 스킨 모드를 보고 분기한다(Simple: 평면 스프라이트 `ui_panel_flat`·`ui_button_flat`·`ui_slot_flat` — `SpriteGenUi`에 1~2px 테두리 버전 추가, 또는 스프라이트 없이 Image 색만). 빌더는 손대지 않거나 최소로.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 새 화면, 시트 스타일 다듬기(나중에).

---

## Files

- `Assets/Scripts/Hud/Ui/UiTheme.cs`, `Assets/Prefabs/UI/UiTheme.asset`, `Assets/Scripts/Hud/Editor/UiKit.cs`·`UiSheetTheme.cs`(ApplySimpleTheme), `Assets/Scripts/Art/Editor/SpriteGenUi.cs`(flat 변형), `Assets/Art/Generated/ui/*_flat.png`, `Assets/Prefabs/UI/Screens/*.prefab`·`Parts/*.prefab`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- Play 캡처 6장: `production/qa/evidence/story-026-ui-simplify/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-024-ui-skin-sprite-sheet.md`
- Unlocks: None
