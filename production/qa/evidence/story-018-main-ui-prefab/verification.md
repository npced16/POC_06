# 스토리 018 검증 기록 — MainUI 프리팹(씬에 놓인 UIDocument)

검증은 열린 편집기의 브리지(`tools/bridge/bridge.py`)로만 했다. Unity를 새로 띄우거나 batchmode를 쓰지 않았다.

## 바뀐 것

- **새 편집기 도구** `Assets/Scripts/Hud/Editor/BuildMainUi.cs` (`IronRing.Hud.Editor.BuildMainUi`)
  - `Generate()`: `Assets/Prefabs/UI/MainUI.prefab`이 없으면 만든다. 빠진 자식, `UIDocument`, HUD 컴포넌트만 보충하고, 바뀐 것이 있을 때만 프리팹을 저장한다. 그다음 `Assets/Scenes/Main.unity`를 연다. 루트에 `MainUI` 인스턴스가 없으면 놓고, 씬이 dirty일 때만 저장한다. 모달 대화상자는 띄우지 않는다. 메뉴: `IronRing/UI/Generate MainUI And Place In Main`.
  - `CaptureGameView()`: 편집 모드 Game 뷰 화면 픽셀을 PNG로 저장한다. 아래 "편집 모드 캡처"를 참고한다.
- **프리팹** `Assets/Prefabs/UI/MainUI.prefab`: 루트 `MainUI` 아래에 자식 6개가 있다. 자식마다 `UIDocument`가 붙어 있고, Panel Settings는 모두 `Assets/Fonts/Resources/KoreanPanelSettings.asset`이다.

  | 자식 | HUD 스크립트 | Source Asset | Sort Order |
  |---|---|---|---|
  | TopHud | KoreanHud | KoreanHud.uxml | 100 |
  | InteriorHud | InteriorHud | InteriorHud.uxml | 110 |
  | InventoryHud | InventoryHud | InventoryHud.uxml | 120 |
  | PhaseHud | PhaseHud | PhaseHud.uxml | 110 |
  | Hotbar | HotbarHud | Hotbar.uxml | 120 |
  | NpcDialog | NpcDialogHud | NpcDialog.uxml | 125 |

- **씬** `Assets/Scenes/Main.unity`: 루트에 `MainUI` 프리팹 인스턴스가 하나 있다.
- **HUD 스크립트 6개** (`KoreanHud`, `InteriorHud`, `InventoryHud`, `PhaseHud`, `HotbarHud`, `NpcDialogHud`)
  - `Create()`는 먼저 `FindAnyObjectByType<T>(FindObjectsInactive.Include)`로 씬에 놓인 컴포넌트를 찾아 `_instance`로 쓴다. 없을 때(MainUI 없는 데모 씬)만 예전처럼 `new GameObject(...).AddComponent<T>()`로 대비책을 만든다. 찾은 오브젝트가 꺼져 있으면(제작자가 하이어라키에서 끈 화면) 대비책도 만들지 않는다.
  - 서비스 확인 조건, `GameServices.Ready` 구독, `ResetStatics`, `OnDestroy` 해제, `PlacementGhost.UiHitTests`, `pickingMode`는 그대로 두었다.
  - `Initialize`는 `HudDocument.Bind`(`KoreanHud.cs`에 추가)로 자기 오브젝트의 `UIDocument`에 바인딩한다. 비어 있는 값만 채운다. PanelSettings가 없을 때만 이전 대비 값(`KoreanUi.DefaultPanel` 또는 그 복제)을 쓰고, Sort Order는 새 문서이거나 0일 때만 데이터·상수 값을 쓴다. Source Asset이 없을 때만 `Resources.Load`한다. 프리팹 값이 기준이다.
  - `Awake`는 씬에 놓인 문서를 서비스 준비 전까지 끈다(`UIDocument.enabled = false`). 그래서 Play 첫 프레임에 UXML 예시 문구가 비치지 않고, 서비스가 없으면 조용히 꺼진 채 남는다. `Bind`가 다시 켠 뒤 `HudDocument.ClearPreview`가 모든 TextElement 문구를 비우고 `preview-only` 클래스 요소를 제거한다. 그 결과 스토리 018 이전과 같은 빈 상태에서 데이터로 채운다.
  - `InventoryHud`의 `Instantiate(KoreanUi.DefaultPanel)`는 대비책 경로에서만 쓴다(그 복제본만 `OnDestroy`에서 파괴한다). `KoreanHud`의 PanelSettings 복제도 대비책에서만 만들며, `Destroy`는 null을 확인한다. `PhaseHud`의 USS 추가에 `Contains` 검사를 넣었다.
- **UXML 6개** (`Assets/UI/Hud/Resources/Hud/`): 모든 UXML에 `<ui:Style src="../../../../Fonts/Resources/KoreanDefault.uss" />`(한글 폰트)를 넣었고, PhaseHud.uxml에는 `<ui:Style src="PhaseHudStyle.uss" />`도 넣었다. 라벨·버튼에는 예시 한국어 문구를 넣었다(주조소, 단계: 복구 중 · 번영 12, 점등부 마르타, 집: 가스등 거리, "증기압이 안정됐군. 오늘은 등불이 밝겠어." 등). 미리보기 전용 핫바 슬롯 5칸, 서식지 태그 2줄, NPC 선택지 버튼 2개에는 `preview-only` 클래스를 붙였다. 요소 이름과 id는 바꾸지 않았다.
  - `InventoryHud.uxml`의 옛 인벤토리 바(`inv-bar`)는 015부터 Play에서 숨긴다. 미리보기도 같도록 인라인 `display: none`을 주었고, 스크립트는 `LegacyMenus ? Flex : None`으로 설정한다.
- **USS 3개**: `#interior-panel`, `#npc-dialog`, `#hotbar-toast`의 `display: none`을 지워 편집 모드에서 보이게 했다. 숨김은 스크립트 `Initialize`가 바로 한다(InteriorHud `Refresh`, NpcDialogHud `Hide`, HotbarHud `_toast.style.display = None`). 배치와 색은 바꾸지 않았다.
  - USS 숨김을 **유지한** 것: KoreanHud `#panel`(거점 패널은 내부 패널과 같은 자리라 겹침), `#defeat`(화면 전체를 덮음), `#resources`(015에서 숨김), Hotbar `#hotbar-palette`·`#hotbar-notice`·`#hotbar-drag`, Inventory `.inv-menu`·`#inv-banner`, Phase `#phase-build-menu`·`#phase-loss-reason`, NPC `#npc-dialog-result`. 모두 잠깐 뜨는 요소이거나 옛(Legacy) 요소이고, UI Builder에서 해당 요소를 골라 display를 바꾸면 볼 수 있다.

## 브리지 결과

| 단계 | id | 결과 |
|---|---|---|
| refresh(구현 후) | aa022a05fa8c4aab997c9bbe1c1e692d | `failed:false`, errors 없음 |
| execute-method Generate (1회) | 685451d15ffd4b208fc9e462091025de | ok. 콘솔 `MAINUI GENERATE: prefab=Assets/Prefabs/UI/MainUI.prefab sceneInstanceAdded=True` |
| scene-status | 8253128c9f5e4d84aa856414b0ff53c0 | `Assets/Scenes/Main.unity`, `isDirty:false` |
| execute-method Generate (2회, 멱등) | 0afd89adc3a44250ab8477ea142ace5f | ok. `sceneInstanceAdded=False`. Main.unity, MainUI.prefab md5가 1회 뒤와 같음 |
| scene-status | 93e42790432f4199b25be98944906bf5 | `isDirty:false` |
| refresh(캡처 수정 후) | b7945a2c820646e2826841f0fb9d180f | `failed:false` |
| compile-status | e350c6d8c1274444b26d451a40d361dd | `failed:false` |

씬 인스턴스 확인: `Main.unity`의 PrefabInstance가 MainUI.prefab(guid `8b43c2dd9955d9048a9584364e519fab`)을 가리키고, `m_Name` 값은 `MainUI`이다(이름이 프리팹 루트와 같아 수정 목록에 들어 있음).

## 편집 모드 캡처 (`CaptureGameView`)

- 1차 시도에서는 `ReadScreenPixel`로 Game 뷰 창 좌표를 읽었다. 그런데 Unity 편집기가 다른 창(터미널) 뒤에 있어서 그 창의 글자가 찍혔다. 증거가 아니므로 **삭제했다**.
- 2차 시도에서는 Game 뷰 렌더 텍스처(`PlayModeView.m_TargetTexture`, 리플렉션)를 읽었다. 이 텍스처에는 카메라 화면만 있고 UI Toolkit HUD 오버레이가 들어가지 않으며, 위아래도 뒤집혀 있었다. 이것도 HUD 증거가 아니므로 **삭제했다**.
- 현재 구현: 화면 픽셀 읽기를 쓰되, Unity 편집기가 OS 앞 창(`InternalEditorUtility.isApplicationActive`)일 때만 30회 갱신 뒤 찍는다. 앞 창이 아니면 최대 120초 기다린 뒤 `MAINUI CAPTURE FAILED: ...`를 남긴다. 성공하면 콘솔에 `MAINUI CAPTURE: <절대 경로> (<너비>x<높이>, pixelsPerPoint=<값>)`가 남는다. 저장 경로는 `production/qa/evidence/story-018-main-ui-prefab/018-editmode-gameview.png`이고, 기존 파일이 있으면 `-v2`, `-v3`…으로 저장한다.
- **찍는 법**: `execute-method --method IronRing.Hud.Editor.BuildMainUi.CaptureGameView`를 보낸 뒤 120초 안에 Unity 편집기 창을 클릭해 앞으로 가져온다(Game 뷰 탭이 보이게).

## UI Builder 편집 절차

1. 하이어라키에서 `MainUI` 아래 자식(예: `NpcDialog`)을 고른다. 인스펙터 `UIDocument`의 Source Asset(UXML)을 더블클릭하면 UI Builder가 열린다. 또는 Project 창에서 `Assets/UI/Hud/Resources/Hud/*.uxml`을 더블클릭한다.
2. UI Builder에서 요소를 고쳐 저장(Ctrl+S)한다. 같은 폴더의 `*Style.uss`도 UI Builder의 StyleSheets 패널에서 고칠 수 있다.
3. 저장하면 편집 모드 Game 뷰가 UI Toolkit 기본 동작으로 다시 그려진다(Play 불필요).
4. 화면을 하나씩 보려면 `MainUI`의 HUD 자식 체크박스(active)를 끈다. 그 화면만 Game 뷰에서 사라진다(별도 코드 없음). 그 상태로 Play하면 그 HUD는 만들어지지 않는다(대비책도 만들지 않음).
5. 레이어 순서는 각 자식 `UIDocument`의 Sort Order가 기준이다. 스크립트는 0이 아니면 덮어쓰지 않는다.
6. 예시 문구는 Play 시작 때 코드가 비우고 데이터로 채운다. 미리보기에만 필요한 요소에는 `preview-only` 클래스를 붙이면 Play 때 제거된다.

## 한계·주의

- **패널 공유 변화**: 예전에는 KoreanHud, PhaseHud, InventoryHud가 PanelSettings를 복제해 각자 다른 패널을 썼다. 이제는 6개 문서가 모두 같은 `KoreanPanelSettings` 에셋, 즉 한 패널을 쓴다. 그래서 순서는 문서 Sort Order로만 정해진다(100 → 110 → 120 → 125). 예전에는 패널 사이 순서가 PanelSettings sortingOrder(모두 0)여서 사실상 정해져 있지 않았다. 따라서 KoreanHud의 패배 화면(`#defeat`)은 이제 다른 HUD 아래에 깔린다. Play 캡처로 확인이 필요하다.
- 예시 문구는 편집 모드 미리보기 전용이라 로컬라이즈 문자열(`Strings`)을 거치지 않는다. Play에서는 `ClearPreview`가 모두 지운다.
- `Assets/Fonts/Resources/NotoSansKR.asset`(동적 폰트 아틀라스)이 편집 모드에서 새 한글 글자를 그리며 수정되었다. 스토리 파일 목록 밖의 변경이므로 커밋에 넣을지는 코디네이터가 판단한다.
- Play 캡처(`--placement-demo --placement-stage ghost`, `--npc-demo --npc-stage panel`)는 코디네이터가 한다.

## 코디네이터 검수 (2026-11-01 19:30 KST)

- 편집 모드 캡처: Unity 창을 앞으로 가져온 뒤 `CaptureGameView` 실행 → `018-editmode-gameview.png`(1205×748). Game 뷰 탭(Play 아님)에 상단 바(현재 위치: 주조소 / 3바퀴째), 서식지 패널 예시, 단계 배너(주조소 · 복구 중 · 번영 12), 정착 기한, "증기 공방 완성!" 토스트, 주조소 내부 제목, NPC 대화(점등부 마르타, 집: 가스등 거리, 대사, 버튼 3개), 핫바 예시 5칸과 팔레트 버튼, 다음 라운드·출발 버튼이 모두 그려짐. 한글 폰트 정상. 동일한 `-v2` 중복 캡처는 삭제.
- Play 캡처(브리지 `play-capture`, 120프레임): `018-play-ghost.png`(--placement-demo --placement-stage ghost) — 초록 고스트, 핫바 9칸, 조건 패널 "서식지 미완성", 015의 01-hotbar-ghost-green과 같은 구성. `018-play-npc-panel.png`(--npc-demo --npc-stage panel) — 원예사 패널(집 온실), 가스등 거리·온실 완성 테두리, "온실 완성!" 토스트, 017의 017-panel과 같은 구성. Editor.log에 게임 코드 예외 없음.
- `NotoSansKR.asset`은 커밋에서 제외(되돌림).
- 예시 문구 가운데 핫바 3~5칸(황동 벤치·톱니 선반·석탄 상자)과 "증기 공방"은 실제 카탈로그 이름이 아니다. UI는 019에서 UGUI로 전환되므로 수정하지 않았다.
