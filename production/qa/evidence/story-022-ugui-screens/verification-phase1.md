# Story 022 — 1단계(틀) 검증 기록

날짜: 2026-11-01 · 담당: ui-programmer(1단계) · 편집기: Unity 6000.3.27f1(열린 편집기, 브리지로만 조작)

## 지금 있는 것

| 구분 | 경로 | 내용 |
|---|---|---|
| TMP 필수 리소스 | `Assets/TextMesh Pro/` | `FontSetup.GenerateTmp`가 `TMP_PackageResourceImporter.ImportResources(true,false,false)`로 가져옴(가져오기가 비동기라 첫 실행은 셰이더 대기로 멈추고, 두 번째 실행에서 폰트 생성) |
| 한국어 TMP 폰트 | `Assets/Fonts/NotoSansKR SDF.asset` | 동적, 36pt, 패딩 6, SDFAA, 1024², 다중 아틀라스. ko.json 글자와 ASCII를 미리 넣음. 아틀라스·머티리얼은 하위 에셋 |
| UI 스프라이트 | `Assets/Art/Generated/ui/ui_panel·ui_button·ui_button_pressed·ui_slot·ui_slot_selected·ui_toast.png` | `SpriteGenUi.Generate`. 64×64, PPU 64, Point, 무압축, FullRect, border 8. 색은 SteampunkPalette.json |
| 테마 | `Assets/Prefabs/UI/UiTheme.asset` (`UiTheme.cs`) | 색 6개(팔레트에서 처음 한 번), 폰트, 글자 크기 Small/Normal/Title, 9분할 스프라이트 6개, SliceMultiplier 0.5 |
| 캔버스 | `Assets/Prefabs/UI/MainCanvas.prefab` | Canvas Overlay, CanvasScaler 1280×720 match 0.5(referencePixelsPerUnit 64), GraphicRaycaster, `UiScreens`(테마 + 화면 목록) |
| 화면 프리팹 | `Assets/Prefabs/UI/Screens/InteriorTitle.prefab`, `Toast.prefab` | MainCanvas의 자식 인스턴스(순서 = SCREENS 표) |
| 씬 | `Assets/Scenes/Main.unity` | 루트에 MainCanvas 인스턴스 1개. EventSystem은 기존 루트 `EventSystem`(InputSystemUIInputModule) 재사용 — 캔버스 프리팹에는 넣지 않음(중복 경고 방지). 옛 MainUI(UI Toolkit)도 그대로 |

스크립트:
- 런타임 `Assets/Scripts/Hud/Ui/`: `UiTheme`, `UiScreen`, `UiScreens`, `KoreanFont`, `ToastScreen`
- `Assets/Scripts/Hud/Interior/InteriorTitleScreen.cs`(InteriorHud 포팅)
- 편집기: `Assets/Scripts/Hud/Editor/UiKit.cs`, `BuildMainCanvas.cs`, `Screens/InteriorTitleScreenBuilder.cs`, `Screens/ToastScreenBuilder.cs`, `Assets/Scripts/Art/Editor/SpriteGenUi.cs`, `FontSetup.GenerateTmp`
- 수정: `InteriorHud.Create`(UiScreens가 있으면 만들지 않음), `HotbarHud.ShowToast`·서식지 완성 토스트(ToastScreen으로 넘김, 없으면 옛 토스트), `PlacementGhost.IsPointerOverUi`(옛 UiHitTests 또는 `EventSystem.current.IsPointerOverGameObject()`)

## 2단계: 화면 하나를 더하는 방법

1. **화면 스크립트** (`Assets/Scripts/Hud/<영역>/<Id>Screen.cs` 또는 기존 HUD 클래스를 포팅):
   - 루트에 `[RequireComponent(typeof(UiScreen))]`, 참조는 `[SerializeField]`(TMP_Text, Button, Image, Transform)만. 이름 조회·런타임 UI 생성 금지.
   - 정적 부트: `SubsystemRegistration`에서 정적 초기화, `AfterSceneLoad`에서 `GameServices.Ready += Create`.
   - `Create()`: 필요한 서비스가 없으면 return → `UiScreens.Instance?.Find<T>()`로 자기 인스턴스를 얻고, 없거나 `enabled == false`면 return → 초기화.
   - 상황 화면은 `UiScreens.Instance.Show(id)/Hide(id)`로 켜고 끈다(루트 SetActive). 꺼진 화면은 Awake/Update가 돌지 않으므로 구독은 정적 Create에서 한다.
   - 기존 공개 API·정적 인스턴스 이름(`HotbarHud.Instance` 등)은 유지.
2. **생성 함수** `Assets/Scripts/Hud/Editor/Screens/<Id>ScreenBuilder.cs`: `public static GameObject Build(UiTheme theme)`:
   - `UiKit.ScreenRoot(id, alwaysOn)` → 스크립트 AddComponent → `UiKit.Panel/Label/Button/Slot/Image/ScrollGrid`로 자식 → `UiKit.Bind(script, "_field", obj)`로 참조 연결.
   - 예시 문구는 `Strings.Get(key)`(한국어 표)로 넣어 편집 모드에서 보이게 한다.
   - 부품 프리팹(슬롯·카드 등)은 `UiKit.SavePrefabIfMissing(BuildMainCanvas.PARTS_FOLDER + "/X.prefab", () => ...)`로 만든 뒤 `PrefabUtility.InstantiatePrefab`.
3. **등록**: `BuildMainCanvas.SCREENS`에 `("<Id>", <Id>ScreenBuilder.Build)`를 그리는 순서 자리에 한 줄 넣는다. 최종 순서: TopBar, HubPanel, Defeat, PhaseBanner, InteriorTitle, Hotbar, Toast, NpcDialog, Codex, CombatFx.
4. **실행**: `refresh` → `execute-method --method IronRing.Hud.Editor.BuildMainCanvas.Generate`. 없는 프리팹만 만들고, 캔버스에 빠진 인스턴스만 순서 자리에 끼우고 `UiScreens._screens`에 등록한다. 이미 있는 프리팹은 다시 만들지 않으므로 생성 함수를 고친 뒤 다시 만들려면 프리팹을 지우거나 프리팹 모드에서 직접 고친다(같은 결과를 캔버스 인스턴스가 따라감).
5. 옛 UI Toolkit HUD의 `Create()`에 `if (UiScreens.Instance != null) return;`을 넣어 공존 중 두 번 그려지지 않게 한다.

UiScreen id: 프리팹 이름과 같다. AlwaysOn: TopBar·PhaseBanner·Hotbar·Toast·CombatFx = true, HubPanel·InteriorTitle·NpcDialog·Codex·Defeat = false.

## 브리지 결과

- `refresh`: 매번 `failed:false`, `errors:""` (마지막 2026-11-01T11:28:43Z)
- `FontSetup.GenerateTmp`: 1회차 "TMP 셰이더가 아직 없습니다"(가져오기 비동기) → refresh 뒤 2회차 "TMP FONT: created … atlases=1"
- `SpriteGenUi.Generate`: "UI SPRITES" 로그, `ui_panel.png.meta`에 `spriteBorder: {x: 8, y: 8, z: 8, w: 8}`, `spriteMeshType: 0`(FullRect)
- `BuildMainCanvas.Generate` 1회차 `canvasChanged=True sceneChanged=True`, 2회차 `canvasChanged=False sceneChanged=False`; `git status --short Assets/Prefabs/UI Assets/Scenes` 두 번 동일(SAME). 이후 3회차도 False/False
- `scene-status`: `Assets/Scenes/Main.unity`, `isDirty:false`, `playing:false`

## 캡처 (편집 모드 Game 뷰, Play 아님)

- `022-editmode-gameview.png`: 첫 캡처. UGUI 제목 상자가 오른쪽 위에서 옛 TopHud 도감 버튼과 겹쳐 있었다.
- `022-editmode-gameview-v2.png`: 제목 상자를 옛 InteriorHud 자리(오른쪽 위, 위에서 104)로 옮긴 뒤. 확인한 것:
  - **InteriorTitle**: 오른쪽, 황동 테두리·모서리 리벳의 자두색 9분할 패널에 "주조소"(NotoSansKR SDF, 네모 없음). 옛 UI Toolkit "주조소 내부" 상자와 같은 자리에 겹쳐 보인다(공존 단계라 정상).
  - **Toast**: 위 가운데 "주조소 완성!"이 보이나 옛 PhaseHud 띠 아래에 깔려 흐리다(UI Toolkit 패널이 Overlay 캔버스 위에 그려짐).
  - 한글 글리프 정상.

## 열린 문제 / 다음 단계 참고

- **3단계 필수**: `InteriorTitleScreen._catalogJson`이 `Assets/UI/Hud/Resources/Hud/HudCatalog.json`을 인스펙터 참조한다(`InteriorTitleScreenBuilder.CATALOG`). `Assets/UI/`를 지울 때 이 파일을 `.meta`와 함께 옮겨야 참조(GUID)가 유지된다. 옛 HUD들의 `Resources.Load("Hud/HudCatalog")`도 같은 파일을 쓴다.
- 공존 중에는 UI Toolkit 패널이 UGUI 위에 그려져 UGUI 화면이 가려진다. 모든 화면이 켜진 편집 모드 캡처(AC)는 3단계 뒤에 다시 찍는다.
- `Assets/Prefabs/UI/Parts/`는 빈 폴더(+ .meta)다. 2단계가 부품 프리팹을 넣기 전까지 git에는 .meta만 남는다.
- TMP 동적 아틀라스: 새 글자가 처음 그려질 때 `NotoSansKR SDF.asset`이 바뀐다(정상 TMP 동작). 고정이 필요하면 2·3단계에서 문자열 표 갱신 후 글자를 미리 넣는 단계를 추가한다.
- 버튼은 게임패드 선택 표시로 눌린 스프라이트를 같이 쓴다(전용 선택 스프라이트 없음).
- `InteriorNpcWalker`는 여전히 `PlacementGhost.UiHitTests` + `RaycastAll`을 쓴다(UGUI 캔버스도 GraphicRaycaster로 걸리므로 동작). 3단계에서 UiHitTests를 지울 때 함께 정리.
- 스크린 리더·글자 배율·색약 모드는 이 단계 범위 밖이다(테마 데이터로 확장 가능).
