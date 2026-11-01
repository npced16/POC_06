# 스토리 024 검증 — UI 스킨(사용자 스팀펑크 시트) + 타이포 통일

날짜: 2026-11-01 · 엔진 6000.3.27f1 · 열린 편집기 브리지만 사용(배치 모드·Play 캡처 없음)

## 1. 배경 키잉 (`tools/art/key_ui_sheet.py`, PIL만)

실행: `python tools/art/key_ui_sheet.py` (PIL이 사용자 site-packages에 있어 `-I` 불가).

지시된 "큰 블러 배경 추정 + 국소 분산" 대신 **외곽선 장벽 + 테두리 플러드**를 썼다. 시트의 모든 조각은 거의 검은 외곽선(max 채널 0~5)으로 둘러싸여 있고, 배경에서 가장 어두운 곳도 max ≈ 31이라 한 문턱으로 갈린다. 첫 시도에 깨끗해서 블러 방식은 쓰지 않았다.

| 단계 | 값 |
|---|---|
| 외곽선 장벽 | max(R,G,B) < 20 (`OUTLINE_MAX`) |
| 틈 막기 | 장벽 MaxFilter 3 (1px 팽창) |
| 배경 | 이미지 네 변에서 장벽이 아닌 픽셀로 플러드. 닿지 않은 곳 = 전경(조각 안 구멍 자동 채움) |
| 테 제거 | 전경 MinFilter 3 (1px 침식) |
| 티끌 | 연결 성분 400px 미만 버림 |

결과: `Assets/Art/UI/SteampunkUiSheet_keyed.png` (원본 그대로). 콘택트 시트 `keyed_contact.png`(마젠타 바탕): 판 구멍·갈색 테 없음, 확대 크롭으로 확인.

## 2. 조각 (`tools/art/ui_sheet_rects.json`, Unity 좌표 y는 아래에서)

연결 성분 20개 = 이름 20개(위치·크기 규칙). border = 9분할(모서리 장식이 늘어나지 않게 파이프보다 넓을 수 있음), pad = 글자 여백(판이 시작하는 곳까지, 행·열 스캔 측정). 단위 시트 px, l/b/r/t.

| 이름 | x | y | w | h | border | pad |
|---|---|---|---|---|---|---|
| frame_plain | 30 | 529 | 713 | 471 | 176/56/136/84 | 64/48/48/76 |
| frame_titled | 786 | 519 | 728 | 505 | 180/64/180/118 | 44/60/44/116 |
| title_plate | 895 | 385 | 491 | 96 | 44/16/44/20 | 40/12/40/18 |
| progress_bar | 895 | 316 | 491 | 56 | 112/24/112/24 | 112/16/112/20 |
| btn_round_close_top | 1422 | 390 | 87 | 88 | 0 | 0 |
| btn_round_gear / alert / help / close | 936·1080·1237·1395 | 184~186 | 107~113 | 108~113 | 0 | 0 |
| corner_a / corner_b | 31 / 308 | 325 / 255 | 204 / 207 | 171 / 239 | 0 | 0 |
| pipe_corner_gear / pipe_corner_small | 572 / 30 | 220 / 179 | 278 / 215 | 283 / 124 | 0 | 0 |
| btn_dark_arrows | 21 | 61 | 333 | 86 | 44/16/44/16 | 42/14/42/12 |
| btn_dark | 367 | 56 | 314 | 90 | 32/18/32/18 | 28/13/26/13 |
| btn_copper / btn_gold | 694 / 910 | 46 | 203 / 204 | 111 | 32/30/32/30 | 28/24/28/24 |
| slot_dark / copper / gold | 1134·1268·1395 | 43 | 114~120 | 114~115 | 24 | 13/12/12/12 |

이름은 코디네이터 지시(`btn_dark_arrows`·`btn_dark`)를 따랐다. 스토리 AC의 `btn_dark`·`btn_dark2`와 다르다.

## 3. 슬라이스·테마

- `IronRing.Art.Editor.UiSheetSlicer.Apply` (메뉴 IronRing/Art/Slice UI Sheet): Sprite·Multiple·PPU 64·Bilinear·무압축·FullRect·밉맵 없음. `SpriteDataProviderFactories` → `SetSpriteRects` + `ISpriteNameFileIdDataProvider` → `Apply` → `SaveAndReimport`. API는 설치 패키지 `com.unity.2d.sprite` 1.0.0 소스와 대조했다(engine-reference에는 없음). 두 번 실행해 .meta md5 동일(멱등).
- `IronRing.Hud.Editor.UiSheetTheme.ApplySheetTheme`: `UiTheme.asset`
  - 스프라이트: 패널 `frame_plain`, 버튼 `btn_copper`(눌림 `btn_gold`), 슬롯 `slot_dark`(선택 `slot_gold`), 토스트·제목판·얇은 띠 `title_plate`, 새 칸 `frame_titled`·`progress_bar`·`btn_round_close`·`btn_dark`·`slot_copper`
  - pad(Vector4)는 JSON에서, `_sliceMultiplier` 2(시트 2px = 화면 1px, 캔버스 refPPU 64 기준). 화면 프레임 여백 = 32/24/24/38(l/b/r/t, 화면 px)
  - 글자: Title 28 · Heading 22 · Normal 18 · Small 14, 줄 높이 계수 1.3, 버튼 높이 44
  - 두 번째 실행 "변경 없음"

## 4. 재스킨·빌더 (Part 1 → Part 2)

- `UiSheetTheme.Reskin`: 옛 `Assets/Art/Generated/ui/ui_*` 참조만 교체. 1회차 프리팹 19개·참조 86개, 2회차 0개, Generate 뒤 다시 0개.
  - 결정 1: `ui_panel` 중 높이 120 미만(TopBar Bar 54, PhaseBanner 상자 38, InteriorTitle 60, Hotbar Bar 90)은 `title_plate`
  - 결정 2: 슬롯은 Sliced + fillCenter 끔 + 테마 Panel 색 바탕 `SlotBack`(첫 자식, slot pad 안쪽)
- 빌더(Part 2, 025 레이아웃 유지):
  - `UiKit` 새 도우미: `FramePad`, `TitlePlate`(패널 위에 22px 걸친 54px 제목판), `CloseButton`(48px 둥근 X, 모서리에서 24px), `Bar`(얇은 띠 = title_plate), `Slot`(fillCenter 끔 + SlotBack)
  - HubPanel·Codex·Passengers·Defeat: frame_plain + 제목판 + 둥근 닫기(Defeat는 닫기 없음, 제목판 가운데). 내용은 FramePad 안쪽, 목록 위치는 pad.top+34
  - NpcDialog: 둥근 닫기(ignoreLayout, 오른쪽 위), 폭 390→418(내용 폭 유지), 세로 레이아웃 padding = FramePad(+닫기 반지름). 제목은 025 머리줄(이름·집·출신지)에 그대로 둠 → 제목판은 쓰지 않음
  - TopBar·PhaseBanner·InteriorTitle·Hotbar Bar: `UiKit.Bar`(title_plate)
  - `frame_titled`는 쓰지 않음(결정 3)
- 재생성: 표식 11개 화면 + CodexCard·PassengerRow → `Generate` 로그 REBUILT 13건. 2·3회차 `canvasChanged=False sceneChanged=False`.
- `UiSheetTheme.NormalizeFonts`: SavePrefabIfMissing 부품(HotbarSlot·FacilityRow·DialogOption·PaletteItem·TileIcon·FloatingMessage)과 그 중첩 인스턴스의 옛 글자 크기(16/20/23.8/35)를 테마 값으로 바꿈. 1회차 프리팹 11개·라벨 47개, 2회차 0개.
- scene-status: Main.unity, isDirty=false, playing=false.

## 5. 화면별 글자 크기 (테마 단계)

| 화면 | Title 28 | Heading 22 | Normal 18 | Small 14 |
|---|---|---|---|---|
| TopBar | — | — | 위치·바퀴·버튼(승객·도감) | — |
| PhaseBanner | 패배 원인(Loss) | 거점 상자·기한 상자 | — | — |
| InteriorTitle | 거점 이름 | — | — | — |
| HubPanel | 제목판 | 시설 소제목 | 시설 줄·짓기 버튼 | 상태·서식지·안내 |
| NpcDialog | — | NPC 이름 | 대사·선택지·버튼 | 집·출신지·결과·이유 |
| Codex | 제목판 | — | 카드 이름 | 힌트·종류·칸 수·보너스·NPC(자동 축소)·완성 배지 |
| Passengers | 제목판 | — | 이름·제안(자동 축소)·빈 상태 | 힌트·출신지·탄 바퀴·힌트 줄(자동 축소) |
| Defeat / CombatFx | 제목 | — | 본문 | 전투 문구 |
| Hotbar | — | — | 팔레트 제목·알림 | 안내줄·슬롯 이름(자동 축소)·키 숫자 |
| Toast | — | — | 문구 | — |

인라인 크기 제거: TopBar 버튼 Title×0.85, Defeat 제목 Title×1.25. 자동 축소는 기존 좁은 칸(도감 NPC 줄·타일 개수·핫바 슬롯 이름·승객 제안/힌트)에만 남김.

## 6. 잘림·겹침 체크리스트

| 항목 | 상태 |
|---|---|
| 제목이 프레임 파이프와 겹침(도감 Part 1 캡처에서 보임) | 고침: 제목판으로 옮김 — Play 캡처로 확인 필요 |
| 닫기 버튼 위치·크기 통일 | 고침: 둥근 X 48px, 4개 화면 같은 위치 |
| 목록·본문이 프레임 안쪽 | FramePad 적용(HubPanel·Codex·Passengers·NpcDialog·Defeat) — 확인 필요 |
| 대화 패널 출신지 줄 폭 | 내용 폭 362 유지(패널 418) — 확인 필요 |
| 도감 카드 머리줄(이름·완성 배지) | 카드 부품 재생성, 배지는 slot_gold 9분할(가운데 톱니가 늘어날 수 있음) — 확인 필요 |
| 승객 줄 | 부품 재생성 — 확인 필요 |
| 핫바 슬롯 이름 | 자동 축소 최대 Small 14 — 확인 필요 |
| 상단 바 한 줄 기준선 | 버튼 글자 Normal로 통일, 기준선 정렬은 손대지 않음 — 확인 필요 |
| 배너·기한 상자 같은 높이 | 같은 Box 도우미(같은 높이) + 같은 Heading — 확인 필요 |
| 버튼 높이 44 통일 | **안 함**: 테마 값만 넣고 빌더의 버튼 크기(30~34)는 그대로(시설 줄 높이 36 안에 들어가야 함) |
| 라벨 높이 ≥ 글자 × 1.3 | 새로 고친 줄(도감·승객 힌트, 시설 소제목)만 `theme.LineHeight`. 나머지 고정 높이는 전수 점검 안 함 |

## 7. 캡처

- `024-part1-editmode.png`: Reskin 직후 편집 모드 Game 뷰(모든 화면이 겹쳐 보임). 도감 제목이 프레임 왼쪽 위 파이프와 겹치고 닫기가 작은 구리 버튼인 것이 보임 → Part 2에서 고침.
- Part 2 뒤 편집 모드 캡처(cd60d47f): 찍히지 않음. `AppActivate(38248)`를 두 번 시도했고 모두 True였지만 로그는 "CAPTURE WAITING"에서 멈췄다(편집기가 앞 창이 아님). 편집기를 앞에 두고 `CaptureGameView`를 다시 실행해야 한다. 성공하면 결과는 `story-022-ugui-screens/022-editmode-gameview-v6.png`. Play 캡처 5장은 코디네이터 몫.

## 9. Play 검수 뒤 수정 6건 (코디네이터 캡처 `024-*.png` 기준)

| # | 문제 | 수정 |
|---|---|---|
| 1 | 토스트가 도감·승객 제목판과 겹침 | `ToastScreen`: 상황 화면(`UiScreen.AlwaysOn == false`)이 하나라도 `UiScreens.IsOpen`이면 `Show`는 큐에 넣는다(최대 `_maxQueued` 4, 넘치면 가장 오래된 것 버림). 떠 있던 토스트는 상황 화면이 열리면 숨긴다. `Update`에서 모두 닫히면 차례로 띄운다. 항상 켜진 화면만 있을 때는 그대로 |
| 2 | 제목판이 왼쪽 톱니와 겹침, Defeat 제목 말줄임 | `UiKit.TitlePlate`: 프레임 위 가장자리 **가운데**(세로도 가운데로 걸침). 폭은 글자 폭 + 좌우 `PlateSide`(26 = title_plate 끝 장식 20 + 6). HorizontalLayoutGroup + ContentSizeFitter라 런타임 문구에도 따라 늘고, 말줄임 없음(Overflow). Defeat 카드는 640×200 → 760×220. 가장 긴 사유 "기한 안에 정착한 거점이 없습니다"(Title 28)가 들어가는 폭 |
| 3 | 힌트 줄이 위 파이프에 닿음 | `UiKit.ContentTop = pad.top + 제목판 높이/2 + 8` = 73px. Codex·Passengers: 힌트(Small) 한 줄을 여기서 시작하고, 목록은 그 아래 LineHeight(Small)+6. HubPanel: 상태·서식지·소제목·목록을 ContentTop 기준으로 내림. Defeat 본문도 ContentTop |
| 4 | 배너·기한 상자 글자가 끝 장식에 닿음 | PhaseBanner 두 상자: padding 좌우 `PlateSide`(26), 높이 40(`BOX_HEIGHT`), 둘 다 Heading 22. 글자 폭에 따라 상자가 늘어나는 구조(행 레이아웃 + preferred)는 그대로 |
| 5 | 도감 "완성" 배지의 톱니가 늘어남 | 배지 = `btn_gold` Sliced, 높이 24, Small 글자. JSON에서 `btn_copper`/`btn_gold` border를 24로 바꾸고 다시 슬라이스 |
| 6 | 버튼 높이가 제각각 | 테마 `ButtonHeight` 40(상단 바 54px 띠에 맞춤) 하나만 씀: 상단 바 승객·도감, 다음 라운드·출발, 대화 선택지, 핫바 팔레트 열기·닫기. 좁은 줄 예외는 테마 `ButtonHeightSmall` 32(시설 줄 짓기, 줄 높이 36). 둥근 닫기는 48px 원형이라 제외 |

부품 재생성: `FacilityRow`·`DialogOption`을 `SavePrefabIfMissing` → `SavePrefab(…, ConsumeMarker)`로 바꿔 표식으로 다시 만들 수 있게 했다(GUID 유지).

재생성: 표식 Codex·CodexCard·Passengers·HubPanel·FacilityRow·Defeat·PhaseBanner·TopBar·NpcDialog·DialogOption·Hotbar → REBUILT 11건. 2회차 `canvasChanged=False sceneChanged=False`. 뒤이은 NormalizeFonts·Reskin 모두 0건. scene-status isDirty=false.

글자 크기 표(5절) 갱신: PhaseBanner 기한 상자 Small → **Heading**(이제 표와 같음). 나머지 칸은 그대로.

못 한 것: 라벨 고정 높이 ≥ 글자×1.3 전수 점검(새로 고친 줄만 `LineHeight`), 상단 바 라벨·버튼 기준선 정렬(둘 다 띠 세로 가운데 앵커라 대체로 맞음). Play 캡처로 확인 필요.

브리지(수정 6건): refresh f7254ce3 · Slicer.Apply 9bbcc47d · Generate 2회 · scene-status 57ffb52f.

## 8. 브리지 id

refresh 81c57502·88faf0f7·364cedce·211537dc·4c91eed7·5b449467(모두 failed=false) · Slicer.Apply dc7403de · ApplySheetTheme e625cabe·1b87c127·30cbb866·09552d93 · Reskin e752269c·01641fc9 · Generate 18b50998·8a088df9 · scene-status 5c772617·f3a211ad · Capture 357ccb7b(성공)·cd60d47f
