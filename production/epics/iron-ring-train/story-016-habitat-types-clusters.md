# Story 016: 서식지 종류 카탈로그와 연결 판정

> **상태 메모(2026-11-02 정리)**: 부분 유효 — 연결 판정 규칙은 살아 있고 카탈로그 내용은 020이 대체. 현재 규칙은 story-020·story-035 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 17:25 KST 스팀펑크 카탈로그로 교체 후 코디네이터 브리지 재검증 — 컴파일 통과, habitat-demo-v4-steampunk.png: 증기 기관실(거울상 인정)·가스등 거리·온실 완성, 증기관-보일러-톱니장치 순서는 미완성, NPC 합류)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3 — 같은 타일이 필요한 칸 수만큼 이어지면 서식지 완성, 서식지 종류 카탈로그(데이터, 12종 이상)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 서식지 카탈로그는 데이터 파일이며 종류마다 id, 한국어 이름, 정의 방식(`cluster` 또는 `pattern`), 번영 보너스, 찾아오는 NPC 종류가 있다. 크기는 종류마다 다르다(3, 4, 9칸 등).
  - **세계관 규칙(사용자, 2026-11-01 17:10)**: 타일과 서식지는 스팀펑크 마을을 이루는 요소로만 만든다. 자연 타일(풀·물·돌·불·꽃·숯·모래·이끼·나무·밀)은 쓰지 않는다. 타일 13종: 보일러(tile_boiler)·증기관(tile_pipe)·톱니장치(tile_gear)·가스등(tile_gaslamp)·석탄더미(tile_coal)·철골(tile_girder)·벽돌길(tile_cobble)·물탱크(tile_tank)·온실유리(tile_glass)·침상(tile_bunk)·작업대(tile_bench)·전신주(tile_wire)·굴뚝(tile_chimney).
  - `cluster`: 타일 id + 필요한 칸 수. 같은 타일이 필요한 칸 수 이상 상하좌우로 이어지면 완성, 모양(ㅁ·일자·L·T 등) 무관. 보일러 4 → 보일러실, 증기관 4 → 배관 회랑, 톱니장치 4 → 톱니공방, 가스등 4 → 가스등 거리, 석탄더미 4 → 석탄 야적장, 철골 4 → 철골 창고, 벽돌길 4 → 벽돌 광장, 물탱크 4 → 저수조, 온실유리 4 → 온실, 침상 4 → 막사촌, 작업대 4 → 공구상, 전신주 3 → 전신선, 톱니장치 9 → 대공방.
  - `pattern`: 타일 id의 격자 템플릿(줄 단위, `.`은 무관 칸). 회전 4가지·거울상 포함 8가지 변형 중 하나로 들어맞으면 완성, 임의로 순서를 바꾼 배치는 불인정. 보일러-증기관-톱니장치 → 증기 기관실(톱니장치-증기관-보일러와 세로 배치는 인정, 증기관-보일러-톱니장치는 불인정), 3×3 철골 둘레 8 + 가운데 굴뚝 → 용광로, 가스등-벽돌길-가스등 → 가로등길, 물탱크 위 철골(세로 2칸) → 급수탑, 철골-작업대-철골 → 정비창, 보일러-톱니장치-전신주 → 발전소.
  - 카탈로그 cluster 13종 + pattern 6종 = 19종. 타일 13종을 아이템 카탈로그에 두고 32픽셀 황동·증기 톤 스프라이트를 생성한다. NPC는 종류마다 한 명(19명, 스팀펑크 직업명).
- [x] `IHabitatClusters`를 구현한다. 배치가 바뀔 때마다 거점의 두 구역(위·아래 3×6, 선로 건너편 미연결)에서 cluster 종류는 같은 타일 묶음 크기로, pattern 종류는 템플릿 8변형 슬라이딩 매칭으로 완성 여부를 찾는다. 한 칸은 같은 종류의 두 서식지에 중복 계산되지 않으며(겹치는 매칭은 하나만), 서로 다른 종류가 다른 칸을 쓰면 동시에 완성될 수 있다.
- [x] `IHabitatSystem`을 구현해 등록한다(`IsHabitatComplete` = 완성 서식지 1개 이상, `EventFlagRaised`는 거점마다 처음 완성될 때 1회·sticky). 기존 태그 합산 시스템(013)은 코드로 보존하되 등록하지 않는다.
- [x] 타일이 빠져 묶음이 깨지면 `HabitatBroken`이 발생하고 완성 목록에서 빠진다(플래그는 sticky 유지).
- [x] NPC: NPC 정의에 서식지 종류 id를 두고, 그 종류의 서식지가 완성된 거점에만 다음 방문 때 합류 판정이 일어난다(003 로직은 조건 함수만 바꾼다). 한 거점에 여러 종류가 완성되면 각 종류의 NPC가 후보가 된다.
- [x] 번영(014): 바퀴마다 배치 칸 1개당 +1, 완성 서식지 1개당 +5(카탈로그 보너스가 있으면 그 값), 나머지 규칙은 유지. 조사 관련 데이터는 비활성(조사 0회, 잔해 0, 모든 칸 Empty)으로 바꾸고 `IHubInterior`는 그대로 둔다.
- [x] 자원·제작 비활성: 타일·아이템·시설은 무제한이다(`IItemInventory`는 항상 충분한 것으로 동작하거나 무제한 모드 데이터 플래그). 조사 발견·자원 제작·자원 보상 경로를 데이터로 끈다. 시설 비용은 0. 경유지 이벤트와 전투 결과는 번영 변화(다음 거점 +5 / -10)만 준다(005 데이터 수정, 코드 변경 최소).
- [x] 스크린샷: 한 거점에 서로 다른 서식지 두 종류가 완성된 화면(묶음 표시는 015), 완성 문구, NPC 합류.

---

## Implementation Notes

- 핵심 로직은 순수 C#(`Assets/Scripts/Habitats/`, namespace IronRing.Habitats). 연결 판정은 36칸 격자에서 같은 타일 id로 flood fill. 구역(위/아래) 경계는 넘지 않는다.
- 데모 플래그 `--habitat-demo`: hub-1에 풀 4칸(L자)과 물 4칸(일자)을 놓아 두 서식지를 완성하고 NPC를 합류시킨다.

---

## Out of Scope

- Story 015: 핫바·드래그·고스트·묶음 표시 UI

---

## Files

- 새로 만든 파일: `Assets/Scripts/Habitats/HabitatCatalog.cs`, `HabitatMatcher.cs`, `ClusterHabitatSystem.cs`, `HabitatsInstaller.cs`, `Demo/HabitatDemoDriver.cs`, `Resources/Habitats/HabitatTypes.json`, `Resources/Habitats/HabitatDemo.json`; `Assets/Scripts/Art/Editor/SpriteGenTiles.cs` → `Assets/Art/Generated/tiles/*.png`
- 아이템: `Assets/Scripts/Items/Resources/Items/ItemCatalog.json`(타일 13종 category "tile", `_unlimited`/`_unlimitedCount`/`_craftingDisabled`, 비용 0), `ItemCatalog.cs`(Category·무제한·제작 비활성 플래그), `ItemInventory.cs`(무제한 모드), `ItemsInstaller.cs`(TagHabitatSystem 대신 ClusterHabitatSystem 등록, 반사 재배선 제거), `ItemSprites.asset`
- NPC: `Assets/Scripts/Npc/NpcDefinition.cs`, `NpcPoolData.cs`(`_habitatTypeId`), `NpcRecruiter.cs`(조건 함수 IsEligible), `Data/NpcPool.json`(19명, 서식지 종류별 스팀펑크 직업 1명, 모든 거점)
- 번영: `Assets/Scripts/Hub/IProsperitySources.cs`(HabitatBonus: 완성 서식지마다 카탈로그 보너스 또는 기본 5), `RunState.cs`, `HubDevelopmentInstaller.cs`, `IInteriorFacilityPlots.cs`(빈 칸도 시설 자리)
- 내부: `Assets/Scripts/Interior/InteriorData.cs`(`_startEmpty`), `HubInterior.cs`(Empty 시작, TryBuildFacility가 Empty 허용), `Resources/Interior/HubInterior.json`(조사 0, 부지 0, 잔해 확률 0)
- 조우·시설: `Assets/Scripts/Combat/Resources/Story005Encounters.json`(자원 0, 승리·이벤트 +5, 패배 -10), `EncounterData.cs`·`WaypointResolver.cs`(`_victoryProsperity`/`_eventProsperity` 최소 추가), `Assets/Scripts/Defense/Data/Facilities.json`(비용 0)
- `Assets/Scripts/Hud/Hotbar/Resources/Hotbar/Hotbar.json`(슬롯 1~6 보일러·증기관·톱니장치·가스등·철골·벽돌길, `_paletteCategories`), `HotbarHud.cs`(팔레트 분류 필터만, 코디네이터 허용)
- `Assets/Scripts/Localization/Resources/ko.json`(옛 자연 타일·서식지 키 삭제, habitat_type_* 19, npc_*·npc_quote_* 19, item_tile_* 13, 완성·흩어짐 문구)
- 증거: `production/qa/evidence/story-016-habitat-types-clusters/verification.md`, `tiles_contact_sheet.png`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- 브리지 컴파일 확인과 코디네이터 스크린샷: `production/qa/evidence/story-016-habitat-types-clusters/` (자동 테스트는 두지 않음)

**Status**: [x] 컴파일 확인·검증 기록 작성, [x] 코디네이터 스크린샷 `habitat-demo-v2.png`(초원·연못·NPC 합류), `habitat-demo-v3-pattern.png`(온천 = 풀-불-물 거울상 인정, 불-풀-물 미완성)

---

## Dependencies

- Depends on: `story-013-items-tag-habitat.md`, `story-014-hub-phases-defeat.md`
- Unlocks: None (015와 병렬)
