# 스토리 022 3단계 검증 — UI Toolkit 제거와 남은 화면 수정

날짜: 2026-11-01 · 편집기 6000.3.27f1(열린 상태, bridge만 사용)

## 1. 화면 수정

| 항목 | 원인 | 수정 |
|---|---|---|
| 도감 카드 이름·잠김 "???" 안 보임 | 카드 `Name` 라벨 높이 28px < Noto Sans KR 20pt 줄 높이(약 29px). `overflowMode = Ellipsis`라 줄이 세로로 넘치면 TMP가 줄 전체를 지운다. 잠김 "???"도 같은 라벨이라 함께 사라졌다 | `CodexScreenBuilder.Card`: Name 높이 34·위 2, Meta 위 37, Diagram 위 60. 카드 부품은 `SavePrefabIfMissing`이라 재생성되지 않았으므로 표식 `.bridge/rebuild/CodexCard`를 추가했다(`ConsumeMarker`, GUID 유지) |
| 도감 머리줄 제목과 힌트 겹침 | 제목(왼 18·오 420)과 오른쪽 정렬 힌트(왼 110·오 380)가 같은 줄의 같은 구간에 놓였다 | 제목은 첫 줄(위 8, 오른쪽 120 비움 = 닫기 버튼 자리), 힌트는 둘째 줄 왼쪽 정렬(위 44, 높이 24), 카드 격자는 위 76부터 |
| 토스트가 단계 띠와 겹침 | Box y = -84 | `ToastScreenBuilder`: y = -150(1280×720 기준, 띠·정착 기한 줄 아래) |
| 패배 화면 | `CombatFx` 화면이 하이어라키 마지막이라 `StateCombatEffects.RunLost → CombatFxScreen.ShowShade` 그늘이 Defeat 위를 덮을 수 있었다 | `CombatFxScreen.ShowShade`: MainCanvas에 `DefeatScreen`이 있으면 그늘을 띄우지 않고 true를 돌려준다. `DefeatScreen.LateUpdate`: `IsRunLost`와 화면 켜짐이 다르면 `Refresh`(이벤트를 놓쳐도 맞춤). Ready 시점에 이미 패배면 `Initialize → Refresh`가 켠다(기존) |

재생성: 표식 `Codex`·`CodexCard`·`Toast` → `Generate`. 로그 `MAINCANVAS REBUILT SCREEN: Toast`, `REBUILT PART: CodexCard`, `REBUILT SCREEN: Codex`. 이어서 한 번 더 실행한 `Generate`는 `canvasChanged=False sceneChanged=False`(멱등).

## 2. 지운 것(git rm)

- 에셋: `Assets/UI/`(UXML·USS·`Icons/` — Icons의 coal/grain/scrap/water는 UGUI 프리팹·씬·에셋 어디서도 GUID 참조가 없었다), `Assets/UI Toolkit/`(테마), `Assets/Fonts/Resources/`(KoreanPanelSettings·KoreanDefault.uss·KoreanTextSettings·UI Toolkit `NotoSansKR.asset`), `Assets/Prefabs/UI/MainUI.prefab`. 남긴 것: `NotoSansKR-Regular.otf`, `OFL.txt`, `NotoSansKR SDF.asset`
- 코드: `Hud/KoreanHud.cs`(HudDocument 포함), `Hud/HubPhase/PhaseHud.cs`, `Hud/Hotbar/HotbarHud.cs`, `Hud/Npc/NpcDialogHud.cs`, `Hud/Codex/CodexHud.cs`, `Hud/Items/InventoryHud.cs`, `Hud/Interior/InteriorHud.cs`, `Hud/Editor/BuildMainUi.cs`, `Localization/KoreanUi.cs` (각 .meta 포함). `HudCatalog` 클래스는 원래 `Hud/HudCatalog.cs`에 있어 그대로 남았다
- 씬: `Main.unity`의 `MainUI` 루트 — 새 `BuildMainCanvas.RemoveLegacyUi()`로 지웠다(로그 `MAINCANVAS LEGACY UI: removed`, 다시 부르면 `absent`)

## 3. 옮긴 것

- `Assets/UI/Hud/Resources/Hud/HudCatalog.json(.meta)` → `Assets/Scripts/Hud/Data/HudCatalog.json(.meta)`. GUID `4644950b98781e74186c1e0a0cc8878d` 그대로이고 TopBar·HubPanel·InteriorTitle 프리팹이 이 GUID를 참조한다. `InteriorTitleScreenBuilder.CATALOG` 경로를 새 위치로 바꿨다. (`Data.meta`는 Unity가 새로 만든 파일)
- `archive/legacy-uitoolkit/`(Assets 밖, 폴더 구조와 .meta 유지): `Assets/Scripts/Hud/Demo/`(HudDemoWorld), `Hud/Editor/Story011Build.cs`, `Assets/Scenes/Demo/Story011/`, `Assets/Scripts/Habitat/Demo/HabitatDemoView.cs`, `Habitat/Demo/Editor/`(Story002Builder), `Habitat/Resources/Story002/HabitatDemo.uxml·uss`, `Assets/Scenes/Demo/Story002/`, `Assets/Scripts/Art/Demo/`(ArtPreview), `Assets/Scenes/Demo/Story008/`(ArtPreview·UIDocument 씬), `Assets/Scripts/Bootstrap/UI/`(Main.uxml·Main.uss·MainPanel.asset — 참조하는 씬·프리팹이 없었다). `Story002/habitats.json`은 `BuildTopDownMain`이 읽어서 남겼다

## 4. 바꾼 코드

- `StateCombatEffects`: UIDocument 오버레이 경로와 `FloatingMessage` 목록을 지우고 문구·그늘은 `CombatFxScreen`만 쓴다
- `HotbarScreen.SLOT_COUNT = 9`(옛 HotbarHud 상수 대체), `PlacementDemoDriver`는 `HotbarScreen.Instance`만 쓴다
- `NpcDialogScreen`: `NpcDialogHud.OpenForDemo/InteractForDemo` 대입을 지웠다(읽는 쪽이 없었다). 데모 단계는 이미 `NpcHomesDemoDriver.StageReady → NpcDialogScreen.HandleDemoStage`. `NpcHomesDemoDriver`(023 소유)는 수정하지 않았다
- `TopBarScreen` 도감 버튼 → `CodexScreen.Toggle`. `CodexDemoDriver`·`CodexScreen`·`ToastScreen`은 주석만 바꿨다
- `ItemsDemoDriver`: `InventoryHud` 대기·제작 메뉴·회수 메뉴·배너 호출을 지웠다. 시스템 호출과 로그는 그대로 남겼다(옛 인벤토리 HUD에 해당하는 UGUI 화면은 없다)
- `ItemsViewSettings.HudSortingOrder`와 `ItemsView.json`의 `_hudSortingOrder`를 지웠다
- `FontSetup.Generate`(UI Toolkit FontAsset·PanelSettings)를 지우고 `GenerateTmp`만 남겼다. `Story008Build`: 미리보기 씬과 `Build`를 지웠다. `Generate`는 스프라이트만 만들고(메뉴 "Generate Story008 Sprites") `Verify`는 남겼다
- `BuildMainCanvas.RemoveLegacyUi()`를 추가했다(인자 없음, 멱등)

## 5. bridge 결과

- `refresh`: `failed=false, errors=""` / `compile-status`: 오류 없음
- `execute-method RemoveLegacyUi` → `Generate`(표식 3개) → `Generate`(멱등 확인) 모두 ok
- `scene-status`: `Assets/Scenes/Main.unity`, isDirty=false, playing=false
- `grep -rn "UnityEngine.UIElements\|UIDocument\|VisualElement" Assets/Scripts` → 0건
- Editor.log의 `HotbarCellView ... PlacementEntry` CS0246은 이번 작업 전 로그다(그 뒤 ReloadAssembly가 두 번 성공했고 compile-status도 깨끗하다). refresh 뒤 Missing/GUID 경고는 없었다

## 6. 편집 모드 캡처 — 못 찍음

`CaptureGameView`를 두 번 실행했지만 둘 다 `MAINCANVAS CAPTURE FAILED: Unity 편집기가 앞 창이 아닙니다.`로 끝났다. 기다리는 동안 `AppActivate(38248)`를 반복했고 True가 돌아왔지만, Windows가 백그라운드 셸의 포커스 전환을 막는 것으로 보인다. 코디네이터가 편집기를 앞으로 띄운 상태에서 다시 실행해야 한다(결과는 `022-editmode-gameview-v5.png`가 된다).

## 7. 코디네이터가 Play로 찍을 것

- `--placement-demo --placement-stage ghost`, `--placement-demo --placement-stage drag`
- `--npc-demo --npc-stage panel`
- `--codex-demo --codex-stage start`(카드 이름·"???"·머리줄 두 줄 확인), `--codex-demo --codex-stage discover`(토스트가 y -150, 단계 띠 아래)
- `--map-depart`
- `--deadline-demo` 긴 실행(프레임을 많이): 기한이 지나 Defeat 화면이 사유 제목("phase_loss_deadline")과 함께 맨 위에 보이는지, CombatFx 그늘이 덮지 않는지
- 전투 문구: Main에서 따로 켜는 전투 플래그는 확인하지 못했다. 맵 주행 중 전투가 나면 CombatFx 문구가 나오는지 본다

## 8. 남은 것

- `PlacementGhost.UiHitTests`: 비어 있는 채로 남겼다. 스토리 023이 수정 중인 `InteriorNpcWalker`가 아직 이 목록을 읽는다. 023이 끝나면 InteriorNpcWalker를 EventSystem 레이캐스트만 쓰도록 바꾸고 목록을 지운다
- `HubPhaseViewSettings.HudSortingOrder`·`hub_phase_view.json`의 `_hudSortingOrder`(옛 PhaseHud용)는 023 소유 파일이라 그대로 두었다. 같은 정리 때 지운다
- `archive/legacy-uitoolkit/`의 Story002·008·011 데모는 빌드되지 않는다(참고용 보관)
- `Assets/Fonts/NotoSansKR SDF.asset` 변경은 TMP 동적 아틀라스 때문이다(1단계 메모와 같음)

## 코디네이터 검수 (2026-11-01 21:10 KST)

- 3단계 뒤 Play 캡처 7장(`022-final-*.png`): 핫바 9칸·초록 고스트·드래그 칩, NPC 대화 패널, 도감(카드 이름·"???"·머리줄 두 줄·3/19→4/19), 토스트가 배너 아래, 선로 화면 월드 라벨, 패배 화면 최상단. 모두 UGUI만으로 그려짐. 게임 코드 예외 없음.
- 편집 모드 캡처는 Unity 창이 다른 창 뒤에 있어 대기 상태로 끝남(사용자가 편집기를 앞에 두고 `IronRing/UI` 메뉴 또는 브리지로 `CaptureGameView` 실행 가능).
