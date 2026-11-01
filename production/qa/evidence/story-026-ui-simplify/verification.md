# 스토리 026 검증 — UI 단순화(Simple 스킨 기본, 시트 스킨은 보존)

날짜: 2026-11-01 · 사용자 지시(22:15) "싹 다 날리자… 시트 스타일은 나중에 쓸 수만 있게, 간단하게" · 열린 편집기 브리지만 사용, 캡처는 코디네이터

## 1. 바뀐 것

| 영역 | 내용 |
|---|---|
| `UiTheme` | `UiSkin { Simple, Sheet }` + `_skin`(기본 Simple), `Skin` 프로퍼티 |
| `SpriteGenUi.GenerateFlat` (메뉴 IronRing/Art/Generate Flat UI Sprites) | 32×32·border 6·Bilinear 평면 9분할 6종을 `Assets/Art/Generated/ui/`에 만든다. 모서리 둥글기 4(부호 거리 + 0.5px 안티앨리어싱). 테두리색은 팔레트 황동 `Bs.B` |
| `UiSheetTheme.ApplySimpleTheme` | 평면 스프라이트(없으면 먼저 생성), sliceMultiplier 1, pad 12(슬롯 3), 버튼 높이 36(좁은 줄 28), `_skin = Simple`. 글자 4단계(28/22/18/14)와 줄 높이 ×1.3은 그대로 |
| `UiSheetTheme.ApplySheetTheme` | 024 그대로에 `_skin = Sheet`만 추가 |
| `UiKit` 분기(빌더는 그대로) | `TitlePlate`: Simple이면 제목판 없이 패널 안 왼쪽 위의 굵은 Heading 한 줄(강조색, 말줄임, 닫기 자리 비움). `CloseButton`: Simple이면 오른쪽 위 64×28 "닫기" 텍스트 버튼(Small 글자). `ContentTop`: Simple이면 pad.top + Heading 줄 높이 + 6. `Slot`: Simple이면 평면(가운데 채움, SlotBack 없음). `Bar`·`Panel`·`Button`은 테마 스프라이트만 바뀌므로 분기 없음 |
| 부품 재생성 | 남은 `SavePrefabIfMissing` 부품(FloatingMessage·HotbarSlot·PaletteItem·TileIcon)도 `SavePrefab(…, ConsumeMarker)`로 바꿨다(GUID 유지). 공용 `UiKit.ConsumeMarker` 추가. 이제 화면 11개 + 부품 8개 전부 표식으로 다시 만들 수 있다 |

평면 스프라이트 6종:

| 이름 | 바탕 RGBA | 테두리 |
|---|---|---|
| ui_panel_flat | 28,20,30,224 (0.88) | 황동 1.5px |
| ui_button_flat | 52,40,54,240 | 황동 1.5px |
| ui_button_flat_pressed | 84,64,76,245(밝기만 올림) | 황동 1.5px |
| ui_slot_flat | 20,16,22,230 | 황동 45% 1px |
| ui_slot_flat_selected | 20,16,22,230 | 황동 2px |
| ui_toast_flat | 20,14,22,215 | 없음 |

보존(삭제 안 함): `Assets/Art/UI/SteampunkUiSheet*.png`, `UiSheetSlicer.cs`, `tools/art/key_ui_sheet.py`, `tools/art/ui_sheet_rects.json`, 테마의 시트용 칸, `UiSheetTheme.Reskin`·`NormalizeFonts`.

## 2. 스킨 전환 절차

1. `execute-method IronRing.Hud.Editor.UiSheetTheme.ApplySimpleTheme` 또는 `ApplySheetTheme`
2. `.bridge/rebuild/` 표식 19개: TopBar Passengers HubPanel Defeat PhaseBanner InteriorTitle Hotbar Toast NpcDialog Codex CombatFx CodexCard PassengerRow FacilityRow DialogOption FloatingMessage HotbarSlot PaletteItem TileIcon
3. `BuildMainCanvas.Generate` → 한 번 더 `Generate`(변경 없음 확인)

## 3. 브리지 결과

| 단계 | 결과 |
|---|---|
| refresh 6ac90477 | failed=false |
| ApplySimpleTheme 396cc1a6 | `UI FLAT SPRITES` 생성 → "ApplySimpleTheme 변경" |
| Generate 24506126 (표식 19개) | REBUILT 화면 11 + 부품 8, 표식 모두 소비 |
| Generate b3cf5624 (2회차) | `canvasChanged=False sceneChanged=False` |
| NormalizeFonts c75dd0b3 · Reskin 084e4bf1 | 둘 다 0건(평면 스프라이트는 Reskin 매핑 대상이 아님) |
| scene-status 51253fc5 | Main.unity isDirty=false, playing=false |
| 되돌리기 확인: ApplySheetTheme f6aede82 + 전체 표식 Generate ×2 | 2회차 변경 없음. 화면·부품 프리팹의 시트 텍스처 참조 0 → 56, `_skin: 1` |
| 다시 Simple: ApplySimpleTheme 779a913c + 전체 표식 Generate ×2 | 2회차 변경 없음. 시트 참조 56 → 0, 평면 패널 참조 6(Codex·Defeat·HubPanel·NpcDialog·Passengers·Hotbar 팔레트), `_skin: 0` |
| scene-status 03c2423f | isDirty=false |

최종 상태: **Simple**.

주의: 첫 Simple 재생성 명령은 코디네이터의 play-capture(6c0ba715) 뒤에 큐에서 기다렸다가 13:23:33에 실행됐다. 그 캡처는 Simple 적용 전 상태다.

## 4. 확인 필요 (Play 캡처: 핫바·대화·승객·도감·패배·선로)

- [ ] 제목(Heading 굵게)과 "닫기" 버튼이 겹치지 않음 — 제목 줄은 오른쪽을 닫기 폭 64 + 8만큼 비움
- [ ] 힌트·목록이 제목 줄 아래부터(ContentTop = 12 + 29 + 6 = 47)
- [ ] 평면 패널 1.5px 테두리가 1280×720에서 흐리지 않음(Bilinear, sliceMultiplier 1)
- [ ] 슬롯 선택(2px 황동)이 구분됨
- [ ] 배너·기한 상자: 좌우 여백 18(`PlateSide` = pad 12 + 6), 높이 40
- [ ] 버튼 높이 36 / 시설 줄 짓기 28

## 5. 못 한 것

- Play 캡처와 겹침·잘림 확인(코디네이터 몫)
- Defeat 카드(760×220)·배너 상자 높이(40)처럼 024에서 빌더에 넣은 시트 기준 크기는 그대로다. Simple에서 너무 크면 빌더 쪽 조정이 필요하다
- 대화 패널(빌더 한 줄 수정): 오른쪽 여백을 스킨에 따라 둔다. Sheet는 둥근 X 반지름 24, Simple은 "닫기" 폭 64 + 8. 머리줄(이름·집·출신지)이 "닫기"와 겹치지 않게 하려는 것이다. refresh 122aa714 → 표식 NpcDialog Generate ×2(2회차 변경 없음) → scene-status 956bac05 isDirty=false. 대신 Simple에서는 대화 본문 폭이 72px 줄어든다

## 6. Simple 캡처 검수 뒤 수정 2건

- **제목이 안 보임 (도감·승객·거점·패배)**
  - 원인: 줄 높이 계수 1.3으로 계산한 Heading 라벨 높이가 29px였다. Noto Sans KR 22pt 줄 높이(약 32px)보다 낮아서, Ellipsis가 줄 전체를 지웠다(022 phase3 도감 카드와 같은 원인).
  - 수정: 테마 `_lineHeightFactor`를 1.5로 올렸다(기본값과 Apply 두 메서드 모두). 이제 제목 높이는 33px이고, Codex 프리팹의 Title RectTransform이 높이 33, 왼쪽 12, 오른쪽 84(닫기 64 + 8 + pad 12)임을 확인했다. `_title` 바인딩은 끊기지 않았다.
- **대화 패널 "닫기"가 집·출신지 줄과 겹침**
  - 머리줄에는 이름만 두고, 오른쪽에 닫기 자리를 비웠다(header padding.right = 닫기 폭 + 8).
  - 집·출신지는 그 아래 별도 줄 `Meta`로 옮겼다(Small, 오른쪽 정렬). 본문은 그 아래이고, 세로 레이아웃 오른쪽 여백은 pad만 남겼다.
- 브리지: refresh 085ebd02(failed=false) · ApplySimpleTheme 801e220e · 표식 Codex·Passengers·HubPanel·Defeat·NpcDialog → Generate bba9d284(REBUILT 5) · Generate d7d20a33(변경 없음) · scene-status 794c36c3(isDirty=false).
