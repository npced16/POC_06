# Story 015: 배치 개편 — 조사 제거, 드래그 앤 드롭과 핫바 고스트 배치

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 17:25 KST 스팀펑크 타일로 재캡처 — 01-hotbar-ghost-green-v2-steampunk, 02-ghost-blocked-red-v3-steampunk, 03-drag-v2-steampunk, 04-placed-L-v3-steampunk 육안 확인)
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3 — 드래그 앤 드롭·핫바(1~9, 고스트)로 타일·아이템을 칸에 배치. 잔해 조사는 없앤다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Core loop 2단계, Reference game), scoped to this story:*

- [x] 거점 내부의 36칸은 처음부터 모두 빈 터(배치 가능)로 보인다. 잔해 타일, "조사" 메뉴, 남은 조사 횟수 표시, 조사 결과 문구는 화면에서 사라진다(로직 데이터로 조사 횟수를 0, 잔해 없음으로 둔다).
- [x] 핫바: 화면 하단에 슬롯 9개가 있고 키 1~9로 슬롯을 고른다. 슬롯에는 카탈로그의 타일·아이템·시설이 아이콘과 함께 무제한으로 보인다(수량 표시 없음, 배정 규칙은 데이터). 핫바 옆의 팔레트에서 슬롯에 넣을 종류를 바꿀 수 있다(카탈로그 전체 목록).
- [x] 고스트: 슬롯을 고르면 그 아이템의 반투명 스프라이트가 마우스를 따라다니며 칸에 스냅된다. 놓을 수 있는 칸은 초록, 없는 칸(이미 찼거나 시설 한도 초과)은 붉게 보인다. 좌클릭으로 배치, 우클릭·Esc로 취소.
- [x] 드래그 앤 드롭: 핫바·팔레트의 아이템을 끌어 칸에 놓으면 배치된다. 드래그 중에도 같은 고스트와 초록·붉은 표시가 나온다.
- [x] 자원 바(상단 석탄·고철·물·곡물)와 제작 메뉴, 인벤토리 수량 표시는 화면에서 없앤다(코드는 비활성 보존).
- [x] 놓인 아이템을 우클릭하면 회수되어 인벤토리로 돌아온다(선택 중이 아닐 때).
- [x] 시설(방벽·포탑·공방)도 핫바·드래그로 칸에 놓는다. 비용은 없고(데이터에서 0), 거점당 한도와 폐허 금지는 기존 `IFacilityBuilder` 규칙으로 판정하며, 놓이면 칸이 FacilityBuilt가 된다. (구현·컴파일 확인. 시작 거점은 폐허라 규칙상 붉은 고스트로 거부되며, 복구 중 이후의 실제 배치 확인은 사용자 플레이 몫)
- [x] 완성된 서식지의 칸들은 테두리나 바닥 색으로 묶여 보이고, 완성 순간 한국어 문구가 짧게 뜬다(종류 이름 포함).
- [x] 선로 화면·내부 화면 전환, 번영·단계 HUD는 그대로 동작한다(제작 메뉴는 AC5에 따라 숨김 — 원문의 충돌을 브리프대로 정리).
- [x] 스크린샷: 핫바와 고스트(초록), 놓을 수 없는 칸의 붉은 고스트, 드래그 중 화면, 배치 완료 화면.

---

## Implementation Notes

- 입력은 새 Input System. 숫자 키 1~9, 마우스 위치, 좌·우클릭, Esc. UI Toolkit의 드래그는 PointerDown/Move/Up 이벤트로 구현한다.
- 고스트는 `InteriorCatalog`에 추가한 `GhostPiece.prefab`(SpriteRenderer, 반투명 머티리얼)을 인스턴스화해 칸 앵커로 스냅한다. 코드로 스프라이트를 만들지 않는다.
- 배치·회수는 `IItemPlacement`를, 시설은 `IFacilityBuilder` + `IInteriorFacilityCells`를 호출한다. 서식지 완성 묶음 표시는 `IHabitatClusters`(016) 이벤트를 구독한다. 016이 아직 없으면 표시만 생략한다.
- 조사 관련 UI(InteriorHud의 조사 메뉴·결과·횟수)는 제거하거나 숨긴다. 012의 로직은 데이터로 비활성화한다(조사 횟수 0, 잔해 0, 모든 칸 Empty).

---

## Out of Scope

- Story 016: 서식지 종류 카탈로그와 연결 판정, NPC 매칭, 번영 반영

---

## Implementation Status (2026-11-01)

브리지 `refresh` 컴파일 통과, `Story012Build.Generate` 재실행으로 GhostPiece·ClusterFrame 프리팹과 카탈로그 필드 생성 확인. 화면 확인은 코디네이터 캡처(아래 verification.md)로 한다. AC 체크는 캡처 뒤에 한다.

- AC1 조사 UI 제거: InteriorHud는 거점 이름만 표시(조사 메뉴·남은 횟수·결과 문구 삭제). 칸을 Empty로 만드는 데이터는 016 담당.
- AC2 핫바: `HotbarHud` 9칸, 키 1~9, 아이콘·이름(수량 없음), 기본 배정 `Hotbar.json`, 팔레트(카탈로그 전체 + 시설)에서 항목→슬롯 클릭으로 재배정.
- AC3 고스트: `PlacementGhost`가 `GhostPiece.prefab`을 가장 가까운 칸 앵커에 스냅, 초록/붉음(카탈로그 색), 좌클릭 배치, 우클릭·Esc 취소, UI 위 클릭 무시(EventSystem + 핫바 패널 Pick).
- AC4 드래그: 슬롯·팔레트 항목의 PointerDown/Move/Up(포인터 캡처, 8px 임계값), 같은 고스트 + 아이콘 칩.
- AC5 자원 바(`#resources` USS 숨김), 제작 메뉴·인벤토리 바·칸 메뉴·완성 배너 숨김(`InventoryHud.LegacyMenus = false`, 코드 보존).
- AC6 선택이 없을 때 우클릭 = `IItemPlacement.TryPickUp`.
- AC7 시설: 핫바·팔레트 항목(방벽·포탑·공방). 판정 = 칸 Empty/FacilityPlot + `IFacilityBuilder.CanBuild`; 배치 = `IInteriorFacilityCells.TryBuildFacility` 후 `IFacilityBuilder.TryBuild`. **블로커**: 012의 `HubInterior.TryBuildFacility`는 FacilityPlot 칸만 받으므로 016에서 모든 칸이 Empty가 되면 시설 칸 기록이 거부된다(아무것도 소모하지 않고 경고 로그). Interior 로직(015 소유 아님)에서 Empty→FacilityBuilt 허용이 필요하다.
- AC8 `ClusterFrameView`가 `IHabitatClusters` 완성 칸마다 `ClusterFrame.prefab`(종류별 색)을 놓고, 핫바가 "<이름> 완성!" 토스트를 띄운다(016 미등록이면 생략). 이름은 ko.json `habitat_type_<id>`/`habitat_<id>` 키 → 016 구현의 이름 메서드 → id 순.
- AC9 단계 배너·출발 버튼·선로 HUD는 손대지 않음. 제작 메뉴는 브리프(제작 없음)에 따라 숨김 — AC5와 AC9 문구가 충돌해 AC5를 따랐다.

## Files

- `Assets/Scripts/Hud/Interior/*.cs`, `Assets/Scripts/Hud/Items/*.cs`, `Assets/UI/Hud/Resources/Hud/Interior*.uxml/uss`, `InventoryHud*.uxml/uss`
- `Assets/Scripts/Presentation/Interior/*.cs` (고스트·선택·드롭 처리), `Assets/Prefabs/Interior/GhostPiece.prefab`, `InteriorCatalog.asset`
- `Assets/Scripts/Interior/Resources/Interior/HubInterior.json` (조사 비활성 데이터)
- `Assets/Scripts/Localization/Resources/ko.json` (키 추가)

실제 변경:
- 신규: `Assets/Scripts/Hud/Hotbar/HotbarHud.cs`, `HotbarData.cs`, `PlacementDemoDriver.cs`, `Resources/Hotbar/Hotbar.json`; `Assets/UI/Hud/Resources/Hud/Hotbar.uxml`, `HotbarStyle.uss`; `Assets/Scripts/Presentation/Interior/PlacementGhost.cs`, `PlacementEntry.cs`, `ClusterFrameView.cs`; `Assets/Prefabs/Interior/GhostPiece.prefab`, `GhostPiece.mat`, `ClusterFrame.prefab`, `ClusterFrame.png`
- 수정: `Assets/Scripts/Hud/Interior/InteriorHud.cs`, `InteriorHud.uxml`, `InteriorHudStyle.uss`; `Assets/Scripts/Hud/Items/InventoryHud.cs`; `KoreanHudStyle.uss`; `Assets/Scripts/Presentation/Interior/InteriorCatalog.cs`, `Editor/Story012Build.cs`; `InteriorCatalog.asset`; `ko.json`
- `HubInterior.json`(조사 비활성 데이터)은 016 소유라 손대지 않음

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- 스크린샷 4장: `production/qa/evidence/story-015-placement-hotbar-dragdrop/` (브리지 컴파일 확인 + 코디네이터 캡처)

**Status**: [x] 캡처·육안 확인 완료 — `01-hotbar-ghost-green.png`, `02-ghost-blocked-red-v2.png`, `03-drag.png`, `04-placed-L-v2.png` (절차와 검수 메모: `verification.md`)

---

## Dependencies

- Depends on: `story-012-hub-interior-map.md`, `story-013-items-tag-habitat.md`
- Unlocks: None (016과 병렬)
