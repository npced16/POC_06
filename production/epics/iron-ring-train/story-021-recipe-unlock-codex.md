# Story 021: 레시피 해금과 도감

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 20:10 KST 코디네이터 브리지 검증 — 컴파일 통과, Generate 멱등, 캡처 4장 육안 확인: 도감 3/19, 바퀴 해금 토스트, 발견 토스트·완성 배지·배너 서식지 1, 편집 모드 도감 예시)
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief Core loop 2단계 — 서식지 완성 조건은 처음에 몇 개만 알고 시작하고 선로를 돌며 하나씩 얻어 도감에 쌓인다. 사용자 지시(2026-11-01 19:15): "완성 조건도 이제 돌다 보면 얻게 되고 도감화."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 계약 `IHabitatCodex`(`IronRingContracts.cs`): `IsUnlocked(typeId)`, `UnlockedTypeIds`, `TryUnlock(typeId, source)`, `event RecipeUnlocked(typeId, source)`. 구현은 `Assets/Scripts/Habitats/HabitatCodex.cs`, Composing 훅에서 등록. 데이터 `Resources/Habitats/CodexUnlocks.json`: 시작 레시피(가스등 거리·석탄 야적장·보일러실), 해금 순서 목록, 출처별 개수(바퀴 1·경유지 이벤트 1·전투 승리 1).
- [x] 해금 출처: 런 시작 때 시작 레시피 3개. 바퀴를 한 바퀴 채울 때(`ITrackRuntime` 바퀴 증가) 순서 목록의 다음 레시피. 경유지 이벤트 해결·전투 승리(`IWaypointResolver`) 때 다음 레시피. 모르는 레시피를 우연히 완성하면(`IHabitatClusters.HabitatCompleted`) 그 레시피가 "발견"으로 해금되고 완성은 그대로 인정된다. 19종을 다 얻으면 더 해금하지 않는다.
- [x] 도감 패널(UI Toolkit, `Codex.uxml`·`CodexStyle.uss`, `MainUI` 프리팹의 자식 `Codex`로 `BuildMainUi.Generate`에 추가, 편집 모드 Game 뷰에 예시 문구로 보임): 상단 바의 "도감" 버튼 또는 `C` 키로 열고 닫는다. 19종을 격자로 보여 준다. 미해금은 "???"와 어두운 실루엣, 해금은 이름·종류(같은 타일/혼합/패턴)·칸 수·타일 배열 그림(묶음·혼합은 타일 아이콘×개수, 패턴은 템플릿 격자)·번영 보너스·찾아오는 NPC 직업. 현재 거점에서 완성된 종류에는 완성 표시.
- [x] 해금 순간 토스트 "새 레시피: <이름>"(출처가 발견이면 "발견: <이름>")가 뜬다(기존 토스트 자리).
- [x] "서식지 조건" 패널(`InventoryHud`의 inv-habitat)은 없애고, 단계 배너에 "서식지 N"(현재 거점 완성 수)을 붙인다. 완성 토스트·묶음 테두리는 그대로.
- [x] 스크린샷: (1) 도감 — 해금 3개와 미해금 "???"가 섞인 화면, (2) 한 바퀴 뒤 해금 토스트와 도감에 추가된 항목, (3) 편집 모드 Game 뷰에 도감 패널 예시가 보이는 화면.

---

## Implementation Notes

- 020의 카탈로그 구조(cluster·set·pattern)를 그대로 읽어 그린다. 타일 아이콘은 `ItemSprites.asset`(아이템 카탈로그 스프라이트)에서 가져온다.
- 데모 `--codex-demo --codex-stage start|lap|discover`: 시작 상태로 도감 열기, 한 바퀴 돌린 뒤 토스트, 모르는 레시피 완성으로 발견.
- HUD 규칙(018): 씬 프리팹의 UIDocument에 바인딩만 하고, UXML 기본값은 예시 문구, `preview-only` 요소는 Play 때 제거. `HudDocument.Bind` 사용.
- 검증은 열린 편집기 브리지. 캡처는 코디네이터.

---

## Out of Scope

- 레시피 힌트(부분 공개), 도감 보상, 세이브.

---

## Files

- `Assets/Scripts/Contracts/IronRingContracts.cs`(IHabitatCodex), `Assets/Scripts/Habitats/HabitatCodex.cs`·`HabitatsInstaller.cs`·`Resources/Habitats/CodexUnlocks.json`·`Demo/CodexDemoDriver.cs`
- `Assets/Scripts/Hud/Codex/CodexHud.cs`, `Assets/UI/Hud/Resources/Hud/Codex.uxml`·`CodexStyle.uss`, `Assets/Scripts/Hud/Editor/BuildMainUi.cs`(자식 추가), `Assets/Prefabs/UI/MainUI.prefab`, `Assets/Scripts/Hud/KoreanHud.cs`(도감 버튼)·`Items/InventoryHud.cs`(조건 패널 제거)·`HubPhase/PhaseHud.cs`(서식지 N)·`Hotbar/HotbarHud.cs`(토스트), `KoreanHud.uxml`·`PhaseHud.uxml`·`InventoryHud.uxml`
- `Assets/Scripts/Localization/Resources/ko.json`(`codex_*` 15개, `phase_habitats`)
- 실제 추가: `Assets/Scripts/Habitats/Resources/Habitats/CodexDemo.json`(데모 배치), `HotbarHud.ShowToast(string)`·`ToastSeconds`(토스트를 문서 루트로 옮겨 선로 화면에서도 보임), `KoreanHud.uxml` 상단 바 "도감 (C)" 버튼(`codex-open`, 인라인 크기만). 해금 순서(CodexUnlocks.json): 전신선 → 저수조 → 배관 회랑 → 공구상 → 톱니공방 → 벽돌 광장 → 철골 창고 → 온실 → 막사촌 → 급수탑 → 가로등길 → 증기 기관실 → 정비창 → 발전소 → 용광로 → 대공방. 패배는 해금 없음.
- 메모: 데모 lap 단계는 열차를 실제로 돌리지 않고 `HabitatCodex.OnLapCompleted`를 직접 호출(실제 경로는 `ITrackRuntime.LapCompleted` 구독). 편집 모드 캡처 메서드는 018 폴더에 저장하므로 파일을 옮겼다.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷: `production/qa/evidence/story-021-recipe-unlock-codex/` + `verification.md`

**Status**: [x] `021-codex-start.png`(--codex-demo --codex-stage start: "서식지 도감 (해금 3/19)", 가스등 거리·석탄 야적장·보일러실 카드에 타일 아이콘·칸 수·번영·NPC, 나머지 "???"), `021-codex-lap.png`(stage lap: 토스트 "새 레시피: 전신선", 해금 4/19), `021-codex-discover.png`(stage discover: 전신주 2 + 가스등 1로 전신선 완성 → "발견: 전신선", 배너 "서식지 1", 해금 4/19), `021-editmode-codex.png`(Play 아님, Game 뷰에 도감 예시 카드 3장·"완성" 배지·새 아트 선로 화면). 로그: `CODEX DEMO hub-1: 완성 서식지 telegraph_line`, `해금 4/19`.

---

## Dependencies

- Depends on: `story-020-habitat-catalog-redesign-grid.md`, `story-018-main-ui-prefab.md`
- Unlocks: None
