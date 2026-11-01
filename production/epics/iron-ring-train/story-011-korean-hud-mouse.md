# Story 011: 한국어 HUD와 마우스 조작

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 7 + Art & audio direction — 한국어 HUD, 씬 위의 마우스 조작

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 모든 UI 문자열은 `IronRing.Localization.Strings`에서 오고 Story 008 한글 폰트로 렌더링된다. 영어 문자열이 화면에 남지 않는다.
- [x] 상단 오버레이에 자원 4종(아이콘+수량), 현재 위치, 바퀴 수가 보인다. 오버레이는 얇아서 씬을 가리지 않는다.
- [x] `IHubClickSource.HubClicked`로 정차 중인 거점을 클릭하면 측면 패널이 열려 슬롯 배치·회수, 서식지 상태, 시설 건설을 할 수 있다. 정차 중이 아닌 거점은 정보만 보여 준다.
- [x] 출발 버튼과 전투 다음 라운드 버튼이 한국어로 있고 실제 시스템을 호출한다. HUD는 이벤트만 구독하고 상태를 직접 바꾸지 않는다.
- [x] 패배 시 한국어 패배 화면이 나온다. 1280x720에서 요소가 겹치지 않는다.
- [x] 기존 영어 패널형 HUD(Story 007)는 제거하거나 비활성화한다.
- [ ] 평상시·패널 열림·패배 스크린샷과 실제 마우스 조작 기록을 남긴다. — **부분 확인**: 스크린샷 4장 확보, 실제 마우스 조작은 사용자 수동 확인 대기.

---

## Implementation Notes

- UI Toolkit 유지. `GameServices.Ready` 이후 자기 UIDocument를 생성한다. 씬 파일과 Bootstrap은 수정하지 않는다.
- 자기 데모 씬에서는 페이크로 검증하고, 실제 연결은 코디네이터 통합 실행에서 확인한다.

---

## Out of Scope

- Story 009: 맵
- Story 010: 씬 연출

---

## Files

- `Assets/Scripts/Hud/KoreanHud.cs`: `GameServices.Ready` 후 UIDocument 생성, 일곱 시스템·`IHubClickSource`·`ICombatRoundCommand`·`IEncounterPresentationSource` 구독, 상단 오버레이·측면 패널·출발/다음 라운드·패배 오버레이.
- `Assets/Scripts/Hud/HudCatalog.cs`: 자원 4종·시설 3종·거점별 슬롯 4개 표시 카탈로그.
- `Assets/Scripts/Hud/Demo/HudDemoWorld.cs`: 일곱 시스템과 클릭/전투 계약 페이크(`--hud-demo-panel`, `--hud-demo-defeat`, `--hud-demo-combat`).
- `Assets/Scripts/Hud/Editor/Story011Build.cs`: 데모 씬 최초 생성기.
- `Assets/UI/Hud/Resources/Hud/KoreanHud.uxml`: HUD 레이아웃. `<ui:Style src="KoreanHudStyle.uss"/>`로 스타일시트 참조.
- `Assets/UI/Hud/Resources/Hud/KoreanHudStyle.uss`: HUD 스타일(1280×720 기준). 구 `KoreanHud.uss`에서 이름 변경(GUID 유지).
- `Assets/UI/Hud/Resources/Hud/HudCatalog.json`, `Icons/`: 표시 카탈로그 데이터와 자원 아이콘.
- `Assets/Scripts/Localization/Resources/ko.json`: 코디네이터 승인에 따라 HUD 키 5개 추가(`hud_remove`, `hud_next_round`, `hud_empty`, `hud_complete`, `hud_incomplete`).
- `Assets/Scenes/Demo/Story011/Story011Hud.unity`: 전용 데모 씬.
- `production/qa/evidence/story-011-korean-hud-mouse/`: 스크린샷 4장, `static-check.txt`, `verification.md`.

**기존 영어 HUD(Story 007) 처리**: `Assets/Scripts/UI`, `Assets/UI/Story007`, `Assets/Scenes/Demo/Story007`은 코디네이터 통합 작업에서 `Assets/` 바깥의 `archive/legacy-story007-hud/`로 이동한다(이 스토리 범위 밖, 코디네이터 수행). 이동 뒤 Unity가 컴파일하는 범위에서 제외되어 AC 6을 만족한다.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- 화면별 스크린샷: `production/qa/evidence/story-011-korean-hud-mouse/` (필수)

**Status**: [ ] 부분 검증 (2026-11-01) — AC 1~6 확인, AC 7은 스크린샷만 확보. 실제 마우스 조작 기록은 사용자 수동 확인 대기.

**스크린샷 (UI 필수 증거, 보존됨)**
- `production/qa/evidence/story-011-korean-hud-mouse/01-normal.png`: 평상시 — 상단 바에 석탄/고철/물/곡물(아이콘+수량), 현재 위치, 순환(바퀴 수). 얇은 오버레이로 씬이 보인다.
- `production/qa/evidence/story-011-korean-hud-mouse/02-panel.png`: 패널 열림 — 우측 패널 「주조소」에 번영/동료, 서식지 슬롯 4개와 자원 배치, 방어시설 방벽/포탑/공방과 건설 버튼, 「정차 중」 표시.
- `production/qa/evidence/story-011-korean-hud-mouse/03-defeat.png`: 패배 — 한국어 패배 오버레이, 요소 겹침 없음.
- `production/qa/evidence/story-011-korean-hud-mouse/04-combat.png`: 전투 — 「다음 라운드」 버튼 활성, 「출발」 버튼 비활성.
- 캡처 시각: 2026-11-01 13:44 KST, USS 수정 뒤 편집기 내 브리지(`tools/bridge`)로 `Assets/Scenes/Demo/Story011/Story011Hud.unity` Play 캡처(기본, `--hud-demo-panel`, `--hud-demo-defeat`, `--hud-demo-combat`).
- 코디네이터 육안 확인: 위 네 화면의 한글 문구·아이콘·레이아웃이 모두 보이고 영어 문자열이 남지 않음.

**미확보 증거 (AC 7)**
- 실제 마우스 조작 기록(거점 클릭 → 패널 열림, 슬롯 배치·회수, 건설, 출발, 다음 라운드)은 **존재하지 않는다**. 스크린샷은 데모 인자로 상태를 재현한 것이며 사람이 마우스로 조작한 기록이 아니다. 사용자가 편집기에서 직접 조작하고 결과를 남기기 전까지 Status는 Ready를 유지한다.

**버그 수정 기록**
- 증상: Play에서 HUD가 USS 없이 렌더링됨(자원 라벨이 세로로 쌓이고 「출발」 버튼이 전체 너비).
- 원인: `KoreanHud.uxml`과 `KoreanHud.uss`가 같은 Resources 이름(`Hud/KoreanHud`)을 공유해 `Resources.Load<StyleSheet>`가 시트를 받지 못했고(null이 `styleSheets.Add`로 조용히 통과), UXML에 스타일 참조도 없었다.
- 수정: USS를 `KoreanHudStyle.uss`로 이름 변경(GUID 유지), UXML에 `<ui:Style src="KoreanHudStyle.uss"/>` 추가, 코드는 `Hud/KoreanHudStyle`을 로드하고 null이면 LogError. 브리지 refresh failed=false. 재캡처한 4장이 위 스크린샷이다.

**보조 증거**
- `static-check.txt`: UXML·JSON 파싱, 문자열 키 중복·참조 키 존재 검사 통과(Unity 컴파일 증거와 구분).
- `verification.md`: 구현 범위, 승인된 ko.json 키 추가, 스타일 미적용 수정 메모.

**한계**: 데모 씬은 페이크 시스템 위에서 동작한다. 실제 시스템과의 결합은 Main 씬 통합 실행에서 확인한다.

---

## Dependencies

- Depends on: `story-008-art-set-korean-base.md` (009와는 계약으로만 연결, 병렬 가능)
- Unlocks: None
