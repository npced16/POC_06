# Story 036: 폐허 연출 — 웅덩이·잡초·모닥불·잔해로 "완전 폐허"처럼 보이게, 복구될수록 걷힌다

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-02 02:05 KST 코디네이터 — 장식 태그 수정 뒤 컴파일 통과, RuinBuild 멱등, 캡처 2장 + 콘택트 시트)
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 4(거점 단계 폐허→복구→정착). 사용자 지시(2026-11-02 01:20): "완전 폐허여서 물, 풀도 해주라. 모닥불 같은 것도 넣어주고." 지금 거점 내부(035 캡처)는 균일한 쇠판 격자라 "폐허"가 보이지 않는다. 폐허 단계의 거점에는 버려진 뒤 고인 물웅덩이, 바닥 틈을 뚫고 자란 잡초, 생존자의 모닥불·천막·잔해가 흩어져 있어야 하고, 복구·정착할수록 걷혀야 한다. 세계관 규칙(스팀펑크 마을 요소만)은 **서식지 타일**에 대한 규칙이다. 여기서 물·풀은 버려진 공업 폐허에 자연이 스며든 **환경 연출**이며 서식지 재료가 아니다(판정·번영에 관여하지 않음).

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: LOW
**Engine Notes**: 2D URP, SpriteRenderer, 프리팹 인스턴스(편집 규칙: 코드로 배치하지 말고 프리팹·씬).

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 폐허 소품 스프라이트(`Assets/Scripts/Art/Editor/SpriteGenRuin.cs` → `Assets/Art/Generated/ruin/`, PPU 64, 64×64 칸 기준): 물웅덩이 2종(2프레임 잔물결, 녹물 섞인 탁한 물에 기름막 반사), 잡초/풀 3종(바닥 틈·쇠판 이음새에서 자란 마른 풀·이끼, 올리브·황갈색), 잔해 2종(부서진 톱니·벽돌 더미·녹슨 파이프), 모닥불(2프레임 불꽃 깜빡임 + 기존 Smoke 프리팹 작은 연기, 둘레에 돌·드럼통), 천막/침낭 1종(생존자 흔적), 콘택트 시트. 팔레트는 `SteampunkPalette.json` + 물·풀 계열 소수.
- [ ] 거점 내부 연출(`Assets/Scripts/Presentation/Interior/RuinDressingView.cs`, 프리팹 `Assets/Prefabs/Interior/RuinProp.prefab`, 카탈로그 `RuinCatalog.asset`, 편집기 생성 `IronRing.Presentation.Interior.Editor.RuinBuild.Generate` 멱등): 거점 단계에 따라 **빈 바닥 칸** 위에 소품을 흩뿌린다. 밀도는 데이터 `RuinDressing.json`(폐허 0.35·붕괴 0.45·복구 중 0.15·정착 0, 종류별 가중치, 거점 id 시드로 결정적). 모닥불은 거점당 1개(열차 근처 빈 칸, 폐허·붕괴·복구 중에만). 타일·아이템이 놓인 칸의 소품은 즉시 사라지고(치워도 돌아오지 않음), 단계가 올라가면 밀도에 맞춰 일부가 1초에 걸쳐 사라진다. 소품은 바닥 위·타일 아래 정렬 순서. 고스트·클릭·판정에 간섭하지 않는다(콜라이더 없음).
- [ ] 선로 화면: 폐허·붕괴 단계의 거점 건물 둘레와 역 승강장에 같은 소품 몇 개(잡초·웅덩이·모닥불 1)가 보인다(`PropsBuild`와 같은 방식의 프리팹 인스턴스, 단계 따라 켜고 끔). 복구 중·정착에서는 모닥불만 남거나 사라진다.
- [ ] 장식 아이템(선택 배치): 팔레트에 "모닥불·웅덩이·잡초"를 장식 분류로 추가(핫바 1~9 기본 슬롯은 그대로, 팔레트에서 꺼냄). 장식은 어떤 서식지 레시피에도 속하지 않고 번영에 기여하지 않으며, 철거·되돌리기 규칙은 타일과 같다.
- [ ] 스크린샷: (1) 폐허 단계 거점 내부(웅덩이·잡초·잔해·모닥불), (2) 타일을 놓은 뒤 소품이 사라진 칸, (3) 선로 화면의 폐허 거점 둘레, (4) 콘택트 시트. `production/qa/evidence/story-036-ruin-dressing/` + verification.md. 브리프 Core loop 3줄에 "폐허 연출" 한 구절.

---

## Implementation Notes

- 거점 단계는 `IHubDevelopment.GetPhase/PhaseChanged`, 칸 상태는 `IHubInterior.Cells/CellChanged`, 내부 열림은 `IViewMode`. 소품 위치는 `HubInteriorView`의 칸 월드 좌표를 따른다(셀 뷰 참조).
- 사용자 편집 규칙: 고정 배치는 프리팹 인스턴스를 씬에, 동적 조각은 프리팹 인스턴스화. 소품은 동적(단계·칸 상태에 따라 바뀜)이므로 프리팹 인스턴스화 + 카탈로그 에셋.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터(`--habitat-demo --habitat-stage incomplete` = 폐허 상태 내부, `--map-depart` 120프레임 = 선로 화면).

---

## Out of Scope

- 날씨·낮밤, 소품 상호작용, NPC가 모닥불에 모이는 행동.

---

## Files

- `Assets/Scripts/Art/Editor/SpriteGenRuin.cs`, `Assets/Art/Generated/ruin/**`, `Assets/Scripts/Presentation/Interior/RuinDressingView.cs`·`Editor/RuinBuild.cs`·`Resources/Interior/RuinDressing.json`, `Assets/Prefabs/Interior/RuinProp.prefab`·`RuinCatalog.asset`, `Assets/Scripts/Presentation/Map/Editor/PropsBuild.cs`(또는 새 `RuinPropsBuild`)·`MapView`(단계 켜고 끔), `Assets/Scripts/Items/Resources/Items/ItemCatalog.json`(장식 3종)·`Assets/Scripts/Hud/Hotbar/Resources/Hotbar/Hotbar.json`(팔레트), `ko.json`, `design/game-brief.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 4장: `production/qa/evidence/story-036-ruin-dressing/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-027-combo-buildings-click-talk.md`, `story-014-hub-phases.md`
- Unlocks: None
