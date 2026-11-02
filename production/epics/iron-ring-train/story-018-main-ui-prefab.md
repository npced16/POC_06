# Story 018: MainUI 프리팹 — 씬에 놓인 UI 문서와 편집 모드 미리보기

> **상태 메모(2026-11-02 정리)**: 대체 — UI Toolkit MainUI 프리팹은 022 UGUI MainCanvas로 대체. 현재 규칙은 design/game-brief.md §편집 규칙 과 story-022 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Superseded(022) — 원래: Done (2026-11-01 19:30 KST 코디네이터 브리지 검증 — 컴파일 통과, Generate 멱등, 편집 모드 Game 뷰 캡처 1장 + Play 캡처 2장 육안 확인. 참고: 사용자 결정(19:20)으로 UI는 UGUI 화면별 프리팹으로 전환 예정(019) — 이 스토리의 UI Toolkit MainUI는 그때 대체됨)
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 7(한국어 HUD) + 편집 규칙 — 고정 배치는 씬의 프리팹 인스턴스. 사용자 결정(2026-11-01 18:30): "UI도 씬에 보이게, 프리팹으로(옵션 1: UIDocument를 씬 프리팹 MainUI로)". 사용자 우려(18:40): "Game 뷰에서 UI 툴킷 미리보기가 안 되지 않나? 화면이 여러 개면?" → 씬에 UIDocument가 있으면 편집 모드 Game 뷰에 그려진다. 지금은 HUD 6개가 Play 때 코드로 문서를 만들어 씬에 UIDocument가 0개라 보이지 않는다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 프리팹 `Assets/Prefabs/UI/MainUI.prefab`: 루트 `MainUI` 아래 HUD별 자식 6개 — `TopHud`(KoreanHud), `InteriorHud`, `InventoryHud`, `PhaseHud`, `Hotbar`(HotbarHud), `NpcDialog`(NpcDialogHud). 자식마다 `UIDocument`(Panel Settings = `Assets/Fonts/Resources/KoreanPanelSettings.asset`, Source Asset = 해당 UXML, Sort Order = 지금 코드가 쓰는 값)와 그 HUD 스크립트 컴포넌트가 붙어 있다. 레이어 순서의 기준은 프리팹의 Sort Order다.
- [x] `Assets/Scenes/Main.unity`에 `MainUI` 인스턴스가 루트에 하나 있다(저장됨, isDirty=false). 편집기 메서드 `IronRing.Hud.Editor.BuildMainUi.Generate`가 프리팹이 없으면 만들고, 빠진 자식·컴포넌트만 보충하고, 씬에 인스턴스가 없으면 추가한 뒤 씬을 저장한다. 여러 번 실행해도 결과가 같고(멱등), 제작자가 손으로 바꾼 값(Sort Order, 위치, UXML 교체)은 덮어쓰지 않는다.
- [x] HUD 스크립트 6개는 씬에 자기 컴포넌트가 있으면 그 오브젝트의 `UIDocument`에 바인딩만 한다(`GetComponent<UIDocument>()`; 비어 있는 값만 채움 — PanelSettings 없으면 `KoreanUi.DefaultPanel`, Source Asset 없으면 `Resources.Load`). 런타임에 새 GameObject·UIDocument를 만드는 경로는 씬에 인스턴스가 없을 때(MainUI 없는 데모 씬)만 대비책으로 남는다. 서비스 준비(`GameServices.Ready`) 뒤 초기화하는 흐름, 서비스가 없을 때 조용히 비활성으로 남는 규칙은 유지한다.
- [x] 편집 모드 미리보기: Play를 누르지 않은 Game 뷰에 상단 HUD·핫바·내부 패널·NPC 대화 패널이 그려진다. 코드가 Play 때 숨기는 패널(내부 패널, 인벤토리 패널, NPC 대화, 토스트)은 UXML 기본 상태에서는 보이고, 라벨에는 실제와 비슷한 예시 한국어 문구(거점 이름·단계·번영·대사 등)가 들어 있다. Play 시작 때는 지금처럼 코드가 숨기고 데이터로 채운다.
- [x] 화면별로 보기: 하이어라키에서 HUD 자식 오브젝트를 끄면 그 화면만 Game 뷰에서 사라진다(별도 코드 없음, 구조로 보장). UI Builder에서 UXML을 열어 저장하면 Game 뷰가 갱신된다(UI Toolkit 기본 동작, 절차를 검증 기록에 적는다).
- [x] Play 결과는 전과 같다: `--placement-demo --placement-stage ghost`와 `--npc-demo --npc-stage panel` 캡처가 015·017 증거와 같은 구성(핫바·고스트·내부 패널·대화 패널)으로 보이고, 콘솔에 오류·예외가 없다. 숨김 패널이 Play 첫 프레임에 깜빡이지 않도록 Initialize에서 바로 숨긴다.
- [x] 브리프 편집 규칙에 UI 줄을 더한다(코디네이터). 스크린샷: (1) 편집 모드(Play 아님) Game 뷰 전체 HUD, (2) Play `--placement-demo` ghost, (3) Play `--npc-demo` panel. `production/qa/evidence/story-018-main-ui-prefab/` + verification.md.

---

## Implementation Notes

- 프리팹 생성은 편집기 전용 스크립트 `Assets/Scripts/Hud/Editor/BuildMainUi.cs`(namespace `IronRing.Hud.Editor`, 인자 없는 `public static void Generate()` — 브리지 execute-method 규격). 패턴은 `Assets/Scripts/Presentation/Interior/Editor/Story012Build.cs`(프리팹 보장 + 빠진 인스턴스만 추가 + `EditorSceneManager.SaveScene`)를 따른다. 모달 대화상자를 띄우지 않는다.
- Sort Order 현재값: KoreanHud 100, InteriorHud 110, InventoryHud `InteriorViewSettings`/`HubPhaseViewSettings`의 HudSortingOrder, HotbarHud `Hotbar.json`의 `_sortingOrder`, NpcDialogHud `SORTING_ORDER` 상수. 프리팹에 같은 값을 넣고, 문서가 미리 있으면 스크립트는 Sort Order를 덮어쓰지 않는다(데이터 파일의 값은 대비책 경로에서만 쓴다).
- UXML/USS는 `Assets/UI/Hud/Resources/Hud/`. USS는 UXML과 다른 이름(`*Style.uss`)이라 스크립트가 직접 붙이는 구조다. 편집 모드에서도 스타일이 보여야 하므로 UXML에 `<Style src="…Style.uss"/>`를 넣어 UXML만으로 완성되게 하고, 코드의 중복 추가는 `Contains` 검사로 무해하게 둔다.
- 한글 폰트는 PanelSettings의 테마·`KoreanDefault.uss`(`KoreanUi.Apply`)로 들어간다. 편집 모드에서 한글이 네모로 나오면 UXML에 `KoreanDefault.uss`도 `<Style>`로 넣는다.
- `PlacementGhost.UiHitTests` 등록, `pickingMode` 설정, 이벤트 구독·해제(`OnDestroy`)는 그대로 둔다. `_instance` 정적 참조는 씬에서 찾은 컴포넌트로 채운다(`FindAnyObjectByType<T>(FindObjectsInactive.Include)`).
- 검증은 열린 편집기의 브리지로만 한다(batchmode·편집기 종료 금지): `python -I tools/bridge/bridge.py refresh` → `compile-status`(failed=false) → `execute-method --method IronRing.Hud.Editor.BuildMainUi.Generate` → `scene-status`(isDirty=false). 편집 모드 Game 뷰 캡처는 편집기 메서드 `IronRing.Hud.Editor.BuildMainUi.CaptureGameView`(GameView 창을 앞으로 가져와 `InternalEditorUtility.ReadScreenPixel`로 창 영역을 읽어 `production/qa/evidence/story-018-main-ui-prefab/018-editmode-gameview.png`로 저장, 기존 파일은 덮어쓰지 않음)로 시도하고, 실패하면 사유를 기록한다. Play 캡처는 코디네이터가 `play-capture`로 한다.

---

## Out of Scope

- UGUI 캔버스 전환, 별도 UI 씬, 월드 공간 UI, UXML 디자인 변경(배치·색은 그대로), 데이터 바인딩(Unity 6 runtime binding) 도입

---

## Files

- 새로: `Assets/Scripts/Hud/Editor/BuildMainUi.cs`(Generate·CaptureGameView), `Assets/Prefabs/UI/MainUI.prefab`(+meta, `Assets/Prefabs/UI.meta`)
- 수정: `Assets/Scripts/Hud/KoreanHud.cs`(+ `HudDocument.Bind/ClearPreview`), `Interior/InteriorHud.cs`, `Items/InventoryHud.cs`, `HubPhase/PhaseHud.cs`, `Hotbar/HotbarHud.cs`, `Npc/NpcDialogHud.cs`(씬 컴포넌트 우선 바인딩, Awake에서 문서 끔, 대비책 분리), `Assets/UI/Hud/Resources/Hud/*.uxml` 6개(한글 USS·Style 참조, 예시 문구, preview-only 요소), `HotbarStyle.uss`·`InteriorHudStyle.uss`·`NpcDialogStyle.uss`(display:none 제거 → 코드가 숨김), `Assets/Scenes/Main.unity`(MainUI 인스턴스)
- 손대지 않음: `Assets/Fonts/Resources/NotoSansKR.asset`(편집 모드 글리프 아틀라스 갱신으로 바뀌었으나 되돌림 — 동적 폰트라 필요 시 다시 생성됨)
- 문서: `design/game-brief.md`(편집 규칙 UI 줄), `production/epics/iron-ring-train/EPIC.md`(018 행·요약)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 3장: `production/qa/evidence/story-018-main-ui-prefab/` (편집 모드 Game 뷰, Play ghost, Play NPC 패널) + `verification.md`(절차·UI Builder 편집 순서)

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-011-korean-hud-mouse.md`, `story-015-placement-hotbar-dragdrop.md`, `story-017-npc-homes-dialogue.md`
- Unlocks: None
