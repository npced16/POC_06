# Story 013: 아이템 인벤토리·배치와 태그 조합 서식지

> **상태 메모(2026-11-02 정리)**: 대체 — 아이템 인벤토리·조사 발견·태그 조합은 015 핫바 배치와 016/020 서식지 카탈로그로 대체. 현재 규칙은 story-015·story-020 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Superseded(015·016) — 원래: Done (2026-11-01 16:05 KST 코디네이터 브리지 검증 — EditMode 154/154, 스크린샷 3장 확보·육안 확인)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01 16:10 KST (코디네이터, 검증 완료 → Done)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3 — 아이템 배치와 태그 조합으로 서식지 완성, 아이템 획득(조사 발견·이벤트 보상·자원 제작)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 아이템 카탈로그는 데이터 파일이다. 12종(가로등·화로·물통·우물·화분·텃밭·벤치·천막·창고 상자·톱니 장식·깃발·증기관)에 한국어 이름, 태그(빛·열·물·식물·쉼터·산업), 제작 비용(자원 4종)이 있다.
- [x] `IItemInventory`를 구현한다. 수량 증감, 제작(`TryCraft`: `IResourceInventory`에서 비용을 빼고 아이템 추가, 부족하면 false, 상태 불변), 스냅샷, 변경 이벤트.
- [x] `IItemPlacement`를 구현한다. 조사된 빈 터에만 배치, 배치 시 인벤토리에서 1 감소, 회수 시 1 증가, `IHubInterior` 칸 상태를 ItemPlaced/Empty로 갱신, 이벤트 발생.
- [x] 태그 조합 서식지: 거점마다 필요한 태그 조건(예: 주조소 = 열 2 + 산업 1)이 데이터에 있고, 배치된 아이템 태그의 합이 조건을 채우면 `IHabitatSystem.IsHabitatComplete`가 true가 되고 `EventFlagRaised`가 발생한다. 기존 NPC 로직(003)은 수정 없이 그대로 동작한다.
- [x] 새 서식지 구현은 `IHabitatSystem`을 구현하되 슬롯 메서드(`TryPlace`/`TryRemove`/`SlotContent`)는 비활성(false/null)으로 두고, Bootstrap에서 기존 `HabitatEvaluator` 대신 등록한다. 기존 002 코드는 삭제하지 않는다.
  - 구현 메모: Bootstrap을 고치지 않고 `ItemsInstaller`가 `GameServices.Composing`에서 `IHabitatSystem`을 다시 등록해 덮어쓴다. NpcRecruiter는 GameSession이 이전 평가기로 만들었으므로 설치자가 리플렉션으로 `_habitats`를 태그 서식지로 바꿔 끼운다(TODO: Bootstrap에서 직접 주입하면 제거).
- [x] 아이템 획득 경로 3개가 동작한다. 조사 발견(012의 FoundItem → 인벤토리 추가), 경유지 이벤트·전투 승리 보상(005의 이벤트 데이터에 아이템 보상 필드 추가, 코드 변경 최소), 자원 제작.
- [x] 아이템 12종의 32픽셀 스프라이트를 Story 008 생성기에 추가하고, 내부 화면에서 배치된 아이템이 칸 위에 보인다. 내부 HUD에 인벤토리 바(아이템 아이콘+수량)와 제작 메뉴(한국어)가 있고, 빈 터를 클릭하면 배치 메뉴, 배치된 칸을 클릭하면 회수 메뉴가 뜬다. — 화면 확인: `01-inventory-craft.png`(제작 메뉴 12종·태그·비용), `02-items-placed.png`(톱니 장식·화로·증기관이 `ItemPiece` 프리팹 인스턴스로 칸 위에 보임, 칸 메뉴 "회수"). 메뉴 호출은 스크립트 데모 경유 — 실제 마우스 기록은 사용자 확인 항목.
- [x] EditMode 테스트가 제작 비용·거부, 배치·회수의 수량 보존, 태그 합산과 완성 판정, 플래그 발생 1회를 검증한다.
- [x] 스크린샷: 인벤토리 바와 제작 메뉴, 아이템 배치 후 내부 화면, 서식지 완성 문구. — `production/qa/evidence/story-013-items-tag-habitat/01-inventory-craft.png`, `02-items-placed.png`, `03-habitat-complete.png` (2026-11-01 16:04–16:05 KST, 코디네이터 육안 확인).

---

## Implementation Notes

- 핵심 로직은 순수 C#(`Assets/Scripts/Items/`), 뷰·HUD는 012가 만든 내부 뷰·HUD를 확장한다(같은 폴더에 파일 추가, 기존 파일은 최소 수정).
- 거점별 태그 조건 기본값: 주조소 열2+산업1, 정원 식물2+물1, 항구 물2+산업1, 성채 쉼터2+빛1. 모두 데이터.
- 순수 장식 효과(번영 보너스)는 014가 아이템 수로 계산한다. 이 스토리는 태그 판정까지.

---

## Out of Scope

- Story 012: 칸·조사·내부 뷰 기본
- Story 014: 거점 단계·번영 반영·패배

---

## Files

- 핵심(순수 C#): `Assets/Scripts/Items/ItemCatalog.cs`, `ItemInventory.cs`, `ItemPlacement.cs`, `TagHabitatSystem.cs`, `EncounterItemRewards.cs`, `ItemsViewSettings.cs`, `ItemsInstaller.cs`
- 데이터: `Assets/Scripts/Items/Resources/Items/ItemCatalog.json`(12종·태그·비용), `HabitatTags.json`(거점별 태그 조건), `ItemsView.json`(표현·데모 값), `ItemSprites.asset`(생성물)
- 뷰: `Assets/Scripts/Presentation/Items/ItemSpriteSet.cs`, `ItemCellView.cs`
- HUD: `Assets/Scripts/Hud/Items/InventoryHud.cs`, `ItemsDemoDriver.cs`, `Assets/UI/Hud/Resources/Hud/InventoryHud.uxml`, `InventoryHudStyle.uss`
- 아트: `Assets/Scripts/Art/Editor/SpriteGenItems.cs`(SpriteGen.cs는 수정하지 않음) → `Assets/Art/Generated/items/*.png`
- 기존 파일 수정: `Assets/Scripts/Localization/Resources/ko.json`(inv_* 21키 추가), `Assets/Scripts/Combat/Resources/Story005Encounters.json`(선택 필드 `_rewardItem`, `_rewardItemAmount`, `_events[]._item/_itemAmount`; 005 C# 코드 변경 없음)
- Bootstrap 수정 없음(`GameServices.Composing` 설치자)
- 테스트: `Assets/Tests/EditMode/Items/Editor/ItemsTestFakes.cs`, `ItemInventoryTests.cs`, `ItemPlacementTests.cs`, `TagHabitatSystemTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- EditMode 테스트 통과 (브리지 run-tests)
- 스크린샷 3장: `production/qa/evidence/story-013-items-tag-habitat/`

**Status**: [x] 확보 (2026-11-01 16:05 KST, 코디네이터 브리지 검증) — EditMode 통과, 스크린샷 3장 확보·육안 확인
- `production/qa/evidence/integration/editmode-prefab.xml` — 프리팹·씬 배치 전환 뒤 전체 `IronRing` 154/154 (2026-11-01 16:04 KST, 최종).
- `production/qa/evidence/story-013-items-tag-habitat/editmode.xml` — EditMode 27/27 통과; `editmode-all.xml` — 전체 `IronRing` 154/154 (결함 수정 후: 배치 칸이 HUD에 가려지던 데모 칸 선택, 아이템 렌더러 머티리얼/정렬 레이어)
- 아이템 스프라이트 시트: `items_contact_sheet.png`
- [x] 스크린샷 3장 (코디네이터 캡처·육안 확인, 2026-11-01 16:04–16:05 KST, 인자는 `verification.md`):
  - `01-inventory-craft.png`: 제작 메뉴 — 아이템 12종, 태그, 자원 비용.
  - `02-items-placed.png`: 톱니 장식·화로·증기관이 `Assets/Prefabs/Interior/ItemPiece.prefab` 인스턴스로 칸 위에 배치됨, 칸 메뉴 "회수".
  - `03-habitat-complete.png`: 열 2/2, 산업 2/1, "서식지가 완성되었습니다! 이벤트 플래그가 켜졌습니다".
  - 첫 캡처 버전은 데모가 아이템을 맨 윗줄에 놓아 상단 배너에 가려 보이지 않았다. 데모의 배치 칸 선택을 고친 뒤 재캡처한 것이 위 3장이다.

---

## Dependencies

- Depends on: `story-012-hub-interior-map.md`
- Unlocks: `story-014-hub-phases-defeat.md`
