# Story 022: UGUI 전환 — 캔버스 하나와 상황별 화면 프리팹

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 7(한국어 HUD) + 편집 규칙. 사용자 결정(2026-11-01 20:20): "UGUI로 싹 다 대체해 달라. UI 툴킷 말고. 프리팹화해서 상황별 패널을 보기 쉽게." 이전 결정(UI Toolkit 유지, 018·021)을 뒤집는 최종 결정이다. 목적은 디자이너가 Scene 뷰·프리팹 모드에서 화면을 직접 고치고, 화면(상황)마다 프리팹 하나를 켜고 끄는 구조.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: `com.unity.ugui` 2.0.0(TextMeshPro 포함), 새 Input System 전용(`activeInputHandler: 1`) → EventSystem은 `InputSystemUIInputModule`.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 글꼴: `Assets/Fonts/NotoSansKR-Regular.otf`로 TextMeshPro 폰트 에셋(`Assets/Fonts/NotoSansKR SDF.asset`, 동적 아틀라스)을 편집기 메서드로 만든다(`IronRing.Art.Editor.FontSetup.GenerateTmp`). TMP 필수 리소스는 스크립트로 가져온다(`TMP_PackageResourceImporter.ImportResources`). 모든 UI 글자는 TMP이며 한글이 네모 없이 나온다.
- [ ] 테마: `Assets/Prefabs/UI/UiTheme.asset`(ScriptableObject)에 팔레트 색(자두색 패널·황동 테두리·호박색 강조·구리 버튼·연기색 글자), 폰트 에셋, 글자 크기, 9분할 패널·버튼 스프라이트(`Assets/Art/Generated/ui/ui_panel.png`·`ui_button.png`·`ui_slot.png`, 64px, border 8, 생성기 `SpriteGenUi`)를 둔다. 색·크기는 코드에 박지 않는다.
- [ ] 캔버스: `Assets/Prefabs/UI/MainCanvas.prefab`(Canvas Screen Space Overlay, CanvasScaler 1280×720 Scale With Screen Size, GraphicRaycaster, 루트에 `UiScreens`) + 같은 프리팹 안의 `EventSystem`(`InputSystemUIInputModule`). `Assets/Scenes/Main.unity` 루트에 인스턴스 하나. 편집기 메서드 `IronRing.Hud.Editor.BuildMainCanvas.Generate`가 프리팹·화면 프리팹·씬 인스턴스를 없을 때만 만들고 빠진 것만 보충한다(멱등, 손으로 바꾼 값 보존, 모달 없음).
- [ ] 화면 프리팹(`Assets/Prefabs/UI/Screens/*.prefab`, 각각 루트에 `UiScreen`(id) 컴포넌트, MainCanvas의 자식 인스턴스, 하이어라키 순서 = 그리는 순서): `TopBar`(현재 위치·바퀴·출발·다음 라운드·도감 버튼), `HubPanel`(거점 이름·단계·서식지·시설·메시지·닫기), `Defeat`(패배 제목·본문), `PhaseBanner`(거점 · 단계 · 번영 · 서식지 N, 정착 기한, 패배 사유), `InteriorTitle`(거점 내부 제목), `Hotbar`(슬롯 9개 = `HotbarSlot.prefab`, 팔레트 = `PaletteItem.prefab` 목록, 힌트, 놓을 수 없음 안내, 드래그 칩), `Toast`(한 줄 토스트, 선로·내부 공통), `NpcDialog`(직업·집·대사·선택지 버튼 = `DialogOption.prefab`·닫기), `Codex`(제목·해금 수·ScrollRect 격자 = `CodexCard.prefab`·타일 아이콘 = `TileIcon.prefab`·닫기), `CombatFx`(전투·상태 변화 떠오르는 문구 = `FloatingMessage.prefab`, 월드 좌표→화면 좌표). 선로 화면의 거점 이름·단계 라벨과 경유지 이름은 HUD가 아니라 `HubBuilding.prefab`·`WaypointMarker.prefab`의 월드 공간 TMP 자식으로 옮긴다(디자이너가 프리팹에서 위치·크기 조정).
- [ ] 전환: `UiScreens.Show(id)/Hide(id)/Toggle(id)`로 화면 루트를 SetActive 한다. 항상 켜진 화면(TopBar·PhaseBanner·Hotbar·Toast·CombatFx)과 상황 화면(HubPanel·InteriorTitle·NpcDialog·Codex·Defeat)을 데이터(UiTheme 또는 UiScreens 목록)로 구분한다. 디자이너는 하이어라키에서 화면을 켜고 끄며 보고, 프리팹 모드에서 고친다.
- [ ] 바인딩: HUD 스크립트는 이름 조회 대신 인스펙터에 꽂힌 참조(`[SerializeField]` TMP_Text·Button·Image·Transform)만 쓴다. 지금의 공개 API와 정적 인스턴스(`HotbarHud.Instance`, `NpcDialogHud.Instance`, `CodexHud.Toggle`, 데모 드라이버가 쓰는 멤버)는 이름을 유지해 데모가 그대로 돈다. 서비스 준비(`GameServices.Ready`) 뒤 초기화하고 서비스가 없으면 조용히 꺼진 채 남는 규칙은 유지한다. 런타임에 UI를 코드로 만들지 않는다(MainCanvas 없는 씬은 HUD 없음).
- [ ] 입력: `PlacementGhost.UiHitTests` 대신 `EventSystem.current.IsPointerOverGameObject()`로 UI 위 클릭을 거른다. 핫바 드래그는 `IBeginDragHandler/IDragHandler/IEndDragHandler`, 키(1~9, C, Esc)는 새 Input System.
- [ ] 제거: UI Toolkit 자산·코드를 모두 없앤다 — `Assets/UI/`, `Assets/UI Toolkit/`, `Assets/Fonts/Resources/`(PanelSettings·KoreanDefault.uss·UI Toolkit FontAsset·TextSettings), `Assets/Prefabs/UI/MainUI.prefab`, `KoreanUi.cs`·`HudDocument`·`BuildMainUi.cs`, `StateCombatEffects`의 UIDocument 오버레이. UIElements를 쓰는 옛 데모(`Assets/Scripts/Habitat/Demo/HabitatDemoView.cs`·`Editor/Story002Builder.cs`, `Assets/Scripts/Art/Demo/ArtPreview.cs`, `Story008Build`의 미리보기)는 `archive/legacy-uitoolkit/`로 옮긴다(저장소 밖 Assets). `Assets/Scripts`에 `UnityEngine.UIElements` 참조가 0이어야 한다(에디터 스크립트 포함).
- [ ] 결과 동일: `--placement-demo --placement-stage ghost|drag|placed`, `--npc-demo --npc-stage panel|encouraged`, `--codex-demo --codex-stage start|discover`, `--map-depart`, `--hud-demo-defeat`(또는 패배 화면을 여는 기존 플래그), 전투 문구(`--combat-result`류)가 UI Toolkit 때와 같은 정보를 보여 준다. 콘솔 예외 없음.
- [ ] 스크린샷: 위 데모 캡처 + 편집 모드(Play 아님) Game 뷰에 모든 화면이 켜진 상태 1장(`BuildMainCanvas.CaptureGameView`) + 프리팹 모드에서 화면 하나를 연 Scene 뷰 캡처는 사용자 확인으로 대체. `production/qa/evidence/story-022-ugui-screens/` + verification.md.

---

## Implementation Notes

- **단계와 담당** (코디네이터가 순서대로 에이전트에 맡김):
  1. 틀(1명): TMP 리소스·폰트 에셋, `SpriteGenUi`, `UiTheme`, `UiScreens`/`UiScreen`, 편집기 조립 도구 `UiKit`(테마로 패널·라벨·버튼·슬롯을 만드는 정적 함수), `BuildMainCanvas.Generate`(캔버스·EventSystem·빈 화면 루트), `InteriorTitle`·`Toast` 두 화면을 본보기로 포팅, `PlacementGhost` 판정 교체. 이 단계에서는 UI Toolkit HUD와 공존한다(둘 다 컴파일).
  2. 화면(2명 병렬): A = TopBar·HubPanel·Defeat(`KoreanHud`), PhaseBanner(`PhaseHud`), 월드 공간 라벨(HubBuilding·WaypointMarker 프리팹), CombatFx(`StateCombatEffects`). B = Hotbar(`HotbarHud`+드래그), NpcDialog, Codex, `InventoryHud` 제거(남은 기능 없음). 각자 자기 화면 프리팹을 `UiKit`으로 만들어 `BuildMainCanvas`의 화면 목록에 등록한다.
  3. 정리(1명): UI Toolkit 자산·코드 제거, 옛 데모 보관, `UnityEngine.UIElements` 참조 0 확인, 전체 데모 재검증. 캡처는 코디네이터.
- 프리팹 생성은 편집기 코드로 한 번 하고(규칙: 고정 배치는 프리팹 인스턴스), 이후 수정은 디자이너가 프리팹 모드에서 한다. 생성 메서드는 이미 있는 프리팹·자식을 덮어쓰지 않는다.
- 월드 공간 라벨은 `TextMeshPro`(3D) 컴포넌트, 정렬 레이어·순서는 건물 위. `RingHubPhaseView`가 단계 문구만 바꾼다.
- 핫바 아이콘은 `ItemSprites.asset`의 스프라이트를 `Image`에 넣는다. 도감 카드의 패턴 격자는 `GridLayoutGroup` + `TileIcon.prefab`.
- 검증은 열린 편집기 브리지(`refresh`, `execute-method`, `scene-status`), 캡처는 코디네이터 `play-capture`와 `CaptureGameView`.

---

## Out of Scope

- 새 화면·기능 추가, 애니메이션, 사운드, 월드 공간 UI(라벨 제외), 세이브.

---

## Files

- 새로: `Assets/Scripts/Hud/Ui/UiTheme.cs`·`UiScreens.cs`·`UiScreen.cs`·`KoreanFont.cs`, `Assets/Scripts/Hud/Editor/UiKit.cs`·`BuildMainCanvas.cs`, `Assets/Scripts/Art/Editor/SpriteGenUi.cs`·`FontSetup.cs`(TMP), `Assets/Prefabs/UI/UiTheme.asset`·`MainCanvas.prefab`·`Screens/*.prefab`·`Parts/*.prefab`, `Assets/Fonts/NotoSansKR SDF.asset`(+아틀라스·머티리얼), `Assets/TextMesh Pro/`(필수 리소스), `Assets/Art/Generated/ui/*.png`
- 수정: `Assets/Scripts/Hud/**`(HUD 7개 포팅), `Assets/Scripts/Presentation/Effects/StateCombatEffects.cs`, `Assets/Scripts/Presentation/Interior/PlacementGhost.cs`, `Assets/Scripts/Presentation/HubPhase/RingHubPhaseView.cs`, `Assets/Prefabs/Map/HubBuilding.prefab`·`WaypointMarker.prefab`(라벨 자식), `Assets/Scenes/Main.unity`(MainCanvas 인스턴스, MainUI 제거), `Assets/Scripts/Items/ItemsViewSettings.cs`(HudSortingOrder 제거)
- 삭제·보관: `Assets/UI/`, `Assets/UI Toolkit/`, `Assets/Fonts/Resources/`, `Assets/Prefabs/UI/MainUI.prefab`, `Assets/Scripts/Localization/KoreanUi.cs`, `Assets/Scripts/Hud/Editor/BuildMainUi.cs`, `archive/legacy-uitoolkit/`(옛 데모)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- 브리지 컴파일 확인 + 데모 캡처 + 편집 모드 캡처: `production/qa/evidence/story-022-ugui-screens/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-018-main-ui-prefab.md`, `story-021-recipe-unlock-codex.md`
- Unlocks: None
