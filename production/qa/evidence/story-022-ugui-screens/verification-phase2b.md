# Story 022 — 2-B단계(Hotbar·NpcDialog·Codex) 검증 기록

날짜: 2026-11-01 · 담당: ui-programmer(2-B) · 편집기: Unity 6000.3.27f1(열린 편집기, 브리지로만 조작)

## 화면

| 화면 id | AlwaysOn | 스크립트 | 부품 프리팹 |
|---|---|---|---|
| `Hotbar` | true (거점 내부가 아니면 `_content`를 끈다) | `Assets/Scripts/Hud/Hotbar/HotbarScreen.cs`, `HotbarCellView.cs` | `Parts/HotbarSlot.prefab`(9칸은 생성기가 인스턴스로 넣음), `Parts/PaletteItem.prefab`(런타임 생성) |
| `NpcDialog` | false | `Assets/Scripts/Hud/Npc/NpcDialogScreen.cs`, `DialogOptionView.cs` | `Parts/DialogOption.prefab`(런타임 생성) |
| `Codex` | false | `Assets/Scripts/Hud/Codex/CodexScreen.cs`, `CodexCardView.cs`, `CodexTileView.cs` | `Parts/CodexCard.prefab`, `Parts/TileIcon.prefab`(런타임 생성) |
| (InventoryHud) | — | 옮기지 않음. `InventoryHud.Create`는 `UiScreens.Instance != null`이면 바로 return | — |

생성기: `Assets/Scripts/Hud/Editor/Screens/HotbarScreenBuilder.cs`, `NpcDialogScreenBuilder.cs`, `CodexScreenBuilder.cs`.
공용 새 파일: `Assets/Scripts/Hud/Ui/ScreenTicker.cs`. 꺼진 상황 화면은 Update가 돌지 않는다. 그래서 도감의 C 키와 토스트 큐, NPC 대화의 늦은 서비스 연결·Esc를 숨은 오브젝트 하나에서 매 프레임 돌린다(UI는 만들지 않는다).

## 동작 요약

- **Hotbar**: 키 1~9(새 Input System), 슬롯 클릭은 선택·해제, 팔레트 항목 클릭 후 슬롯 클릭은 배정. 끌기는 `IPointerDown/IInitializePotentialDrag/IBeginDrag/IDrag/IEndDrag`로 받는다. 임계값은 Hotbar.json `_dragThresholdPixels`로 직접 재고(EventSystem 기본 임계값은 끔), 시작하면 `PlacementGhost.BeginDrag`, 놓으면 `PlacementGhost.EndDrag(true)`를 부른다. UI 위에서 놓으면 고스트의 `IsPointerOverUi`(EventSystem)가 거른다. 드래그 칩은 클릭 통과. 알림은 `_noticeBox`, 서식지 완성·`ShowToast`는 `ToastScreen.Show`.
- 선택 틀은 `UiTheme.SlotSelectedSprite`를 쓴다. 배정 대기(옛 초록 테두리)는 `UiTheme.Accent` 틴트로 바꿨다. 테마에 초록이 없어서다.
- **NpcDialog**: `INpcClickSource.NpcClicked`로 연다. 열기 조건은 옛 코드와 같다. 직업명·집(없으면 강조색)·대사·결과 한 줄(실패면 강조색)·선택지 버튼(비활성이면 `interactable=false`, 이유는 아래 줄 `_reasons`)·닫기를 보인다. Esc, 거점 이탈, 내부 화면 닫힘이면 닫는다. `NpcDialogHud.OpenForDemo/InteractForDemo`를 새 화면이 채우고, `NpcDialogHud.HandleDemoStage`는 옛 인스턴스가 없으면 `NpcDialogScreen.HandleDemoStage`로 넘긴다.
- **Codex**: 카탈로그 종류마다 카드 하나를 만든다. 잠긴 카드는 "???"와 어두운 실루엣, 해금된 카드는 이름·종류·칸 수·배열(TileIcon, 패턴은 행 길이만큼 열, 빈 칸은 흐리게, 혼합은 "+k")·보너스·NPC·"완성"을 보인다. C 키로 열고 닫고, Esc와 닫기 버튼으로 닫는다. 해금 토스트는 한 프레임 늦게, 겹치면 차례로 `ToastScreen.Show`(시간은 Hotbar.json `_toastSeconds`). `CodexHud.Toggle`·`CodexHud.Open`은 옛 인스턴스가 없으면 `CodexScreen.Toggle/Open`으로 넘긴다(2-A TopBar 버튼 경로 유지).
- **데모 드라이버**: `PlacementDemoDriver`는 새 인터페이스 `IHotbarDemoTarget`(Entry·SelectSlot·BeginDemoDrag)로 `HotbarScreen.Instance`를 먼저 쓰고, 없으면 `HotbarHud.Instance`를 쓴다(옛 클래스도 이 인터페이스를 구현). `CodexDemoDriver.StageReady`와 `NpcHomesDemoDriver.StageReady`는 새 화면이 구독한다.
- **옛 클래스**: `HotbarHud/NpcDialogHud/CodexHud/InventoryHud.Create`에 `if (UiScreens.Instance != null) return;`을 넣었다. UI Toolkit 파일은 지우지 않았다(3단계).
- **문자열**: `ko.json`에 편집 모드 예시용 키 8개를 덧붙였다(`ui_sample_npc_title/home/line/result/option`, `ui_sample_notice`, `ui_sample_codex_name`, `ui_sample_codex_title`). 런타임 문구는 기존 키를 쓴다.

## 브리지 결과

- `refresh` 1회차: `HotbarCellView.cs`의 `PlacementEntry` using 누락으로 실패 → `IronRing.Presentation.Interior`로 고친 뒤 `failed:false`, `errors:""`(2026-11-01T11:46:48Z).
- `BuildMainCanvas.Generate` 1회차: Screens에 `Hotbar/NpcDialog/Codex.prefab`, Parts에 `HotbarSlot/PaletteItem/DialogOption/CodexCard/TileIcon.prefab`을 만들었다. 세 화면 GUID가 `MainCanvas.prefab`에 들어갔다.
- 바인딩 점검: 세 화면 프리팹에 빈(`fileID: 0`) `_` 필드가 없다. `_slotViews`는 9개 모두 연결.
- `scene-status`: `Assets/Scenes/Main.unity`, `isDirty:false`, `playing:false`.
- `Generate` 2회차: `Assets/Prefabs/UI`·`Assets/Scenes`의 git 상태와 해시가 1회차 뒤와 같다(SAME). 멱등.

## 캡처 (편집 모드 Game 뷰, Play 아님)

- `022-editmode-gameview-v3.png`: 공존 단계라 옛 UI Toolkit 도감·핫바·NPC 패널이 UGUI 위에 그려져 대부분 가려진다. 보이는 UGUI 부분:
  - Codex 패널 머리줄 "레시피 도감  3 / 19"·힌트·닫기(옛 도감 뒤)
  - 왼쪽 NPC 대화 "점등부 마르타"·대사·"격려하기"
  - 하단 핫바의 "2 증기관" 아이콘 칸과 오른쪽 "팔레트" 버튼

  한글은 네모 없이 나온다. 화면별 확인은 3단계(UI Toolkit 제거) 뒤 다시 찍는다.

## 코디네이터가 Play 캡처할 것

- `--placement-demo --placement-stage ghost`: 1번 칸 선택 틀(호박색), 고스트가 칸에 고정.
- `--placement-demo --placement-stage drag`: 드래그 칩이 칸 위(오프셋 34,34)에 보이고 고스트가 끄는 항목을 표시.
- `--placement-demo --placement-stage placed`: L자 배치 뒤 선택 해제 상태의 핫바.
- `--npc-demo --npc-stage panel`: 왼쪽 아래 대화 상자(직업·집·대사·선택지·닫기).
- `--npc-demo --npc-stage encouraged`: 결과 줄과 비활성 격려 버튼, 이유 줄.
- `--codex-demo --codex-stage start`: 도감 열림, 카드 19장(잠김 "???" 실루엣 포함).
- `--codex-demo --codex-stage discover`: "발견: …" 토스트와 해금 카드.
- 공통: 콘솔 예외가 없는지 확인.

## 열린 문제

- 손 조작(Play)은 확인하지 못했다. 마우스 드래그·놓기·팔레트 배정·키 1~9·C·Esc는 코디네이터 Play 캡처나 사용자 확인이 필요하다.
- UGUI는 드래그 뒤에도 같은 오브젝트에 클릭을 보낼 수 있다. 그래서 부품이 자체 플래그(`_dragStarted`)로 드래그 뒤 클릭을 막는다. 팔레트 항목이 드래그를 받으므로 팔레트 스크롤은 마우스 휠과 스크롤바로만 된다(항목 위 끌기 스크롤 없음).
- 도감 카드는 열 때마다 19장을 새로 만든다(풀링 없음). 지금 규모에서는 문제없다.
- 도감 패널 위치(위 150, 아래 90)는 2-A TopBar·PhaseBanner와의 겹침을 3단계 캡처에서 다시 볼 것.
- `Assets/Fonts/Resources/NotoSansKR.asset`(UI Toolkit 폰트, 동적 아틀라스)이 편집기 동작으로 바뀌어 있다. 이 단계에서 의도한 변경이 아니다.
- `ScreenTicker`는 `Hud/Ui`(1단계 폴더)에 새 파일로 추가했다. 기존 파일은 고치지 않았다.
