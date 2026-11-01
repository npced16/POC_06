# Story 027: 조합이 완성되면 그 모양대로 건물이 서고, 건물을 클릭해 NPC와 대화

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 22:55 KST 코디네이터 브리지 검증 — 컴파일 통과, 캡처: 건물 3채(일자·2×2·L자)·3×3 용광로·초상과 타자기 대화창. 검수 수정: 격자 반올림(홀수 열 반정수) 버그, 닫기 위치)
> **Layer**: Presentation + Core
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Core loop 2·3단계. 사용자 지시(2026-11-01 22:35): "내가 조합하면 이제 그 모양대로 건물이 생기고, 해당 건물 클릭하면 NPC 창 나와서 옛날 게임처럼 말 걸었으면 좋겠는데." 점검 보고서(`production/qa/playtest-2026-11-01.md`)도 "완성해도 색 테두리뿐이라 보상 순간이 없다"를 재미 없음의 원인으로 꼽았다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 건물 생성: 서식지가 완성되면(`IHabitatClusters.HabitatCompleted`) 그 묶음의 칸들 위에 건물이 선다. 건물은 칸 모양 그대로다 — 칸마다 지붕 조각을 이웃 여부(상하좌우 + 대각)에 따라 고르는 자동 타일링(모서리·가장자리·안쪽·오목 모서리)으로 덮고, 아래쪽 가장자리 칸 가운데에 문, 한 모서리에 굴뚝(연기 켬), 지붕 위에 종류 상징물(보일러실 = 보일러 돔, 톱니공방 = 큰 톱니, 가스등 거리 = 등불 기둥, 온실 = 유리 지붕 …)을 얹는다. 지붕 팔레트는 종류 계열별 4가지(기계·주거·창고·유리)이며 데이터(`HabitatTypes.json`에 `_buildingStyle`, `_signProp`)로 정한다. 타일 스프라이트는 건물 아래에 숨긴다(회수하면 다시 보임).
- [x] 건물 해체: 묶음이 깨지면(`HabitatBroken`) 건물이 사라지고 타일이 다시 보인다. 완성된 묶음에 칸이 더 붙으면(테두리 갱신 버그 S3-1과 같은 경로) 건물 모양도 갱신된다.
- [x] 클릭 대화: 건물 어느 칸이든 빈 손으로 좌클릭하면 그 집에 사는 NPC의 대화 패널이 열린다(기존 `NpcDialogScreen`, `INpcClickSource` 경로 재사용). 사는 NPC가 없으면 "<종류 이름> — 아직 아무도 살지 않는다. 승객이 찾아오길 기다린다."가 짧게 뜬다(토스트 또는 패널). NPC 몸체는 배회 대신 문 앞에 서 있고(가벼운 숨쉬기/흔들림), 대화 중에는 문 앞에서 플레이어 쪽을 본다. 배회 코드는 데이터 플래그로 끈다(`_npcWanders:false`).
- [x] 대화 느낌: 패널에 NPC 초상(몸체 스프라이트를 2~3배로 키운 상자)과 이름표, 대사는 한 글자씩 찍히는 타자기 효과(속도 데이터, 클릭하면 즉시 완성), 선택지는 기존(대화·격려·닫기). 패널은 Simple 스킨 규칙을 따른다.
- [x] 완성 순간: 건물이 세워질 때 짧은 연출(지붕 조각이 0.3초 동안 아래에서 올라오며 등장, 연기 시작, "<종류> 완성!" 토스트 유지). 데이터로 켜고 끈다.
- [x] 선로 화면 반영은 범위 밖(내부 화면만). 핫바·고스트는 건물 위에 타일을 또 놓을 수 없게 붉게 보인다(이미 찬 칸 규칙 그대로).
- [x] 스크린샷: (1) L자 가스등 거리 건물, (2) 3×3 용광로 건물(패턴), (3) 건물 클릭으로 열린 대화 패널(초상·타자기 중간), (4) 빈 건물 안내. `production/qa/evidence/story-027-combo-buildings-click-talk/` + verification.md.

---

## Implementation Notes

- 아트: `Assets/Scripts/Art/Editor/SpriteGenBuildings.cs`(팔레트 기반 64px) — 지붕 세트 4계열 × 조각 16종(이웃 비트마스크 4방향 = 16, 대각은 모서리 보정 조각 4종 추가), 문 2종, 굴뚝, 상징물 19종(서식지 종류마다 1개, 기존 타일 그림 재활용 가능). `Assets/Art/Generated/buildings/`.
- 표현: `Assets/Scripts/Presentation/Interior/BuildingView.cs`(서식지 묶음 → 건물 조각 인스턴스, 프리팹 `Assets/Prefabs/Interior/BuildingPiece.prefab`·`BuildingProp.prefab` 카탈로그 참조), `ClusterFrameView`는 건물이 있는 묶음에서는 테두리를 숨김. 클릭은 건물 칸의 `BoxCollider2D` + `IInteriorCellClickSource`/`INpcClickSource` 경로. 집 배정은 `INpcHomes.HomesAt(hubId)`로 묶음 ↔ NPC 매핑.
- NPC: `InteriorNpcWalker`에 문 앞 대기 모드(`hub_phase_view.json` `_npcWanders:false`, 문 앞 위치 = 묶음의 아래 가장자리 가운데 칸의 아래쪽 오프셋).
- 대화 패널: `NpcDialogScreen`에 초상 Image·타자기(코루틴, `_typeSpeed` 데이터).
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 선로 화면의 거점 외관 변화, 건물 업그레이드, 건물 내부, 사운드.

---

## Files

- `Assets/Scripts/Art/Editor/SpriteGenBuildings.cs`, `Assets/Art/Generated/buildings/**`
- `Assets/Scripts/Presentation/Interior/BuildingView.cs`·`ClusterFrameView.cs`·`InteriorCatalog.cs/.asset`·`Editor/Story012Build.cs`(프리팹 보장), `Assets/Prefabs/Interior/BuildingPiece.prefab`·`BuildingProp.prefab`
- `Assets/Scripts/Habitats/HabitatCatalog.cs`·`Resources/Habitats/HabitatTypes.json`(`_buildingStyle`, `_signProp`)
- `Assets/Scripts/Presentation/HubPhase/InteriorNpcWalker.cs`·`HubPhaseViewSettings.cs`·`hub_phase_view.json`
- `Assets/Scripts/Hud/Npc/NpcDialogScreen.cs`·`Editor/Screens/NpcDialogScreenBuilder.cs`, `ko.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 스크린샷 4장: `production/qa/evidence/story-027-combo-buildings-click-talk/` + `verification.md`

**Status**: [x] `027-v2-buildings.png`(--habitat-demo: 증기 기관실 일자 3칸·보일러실 2×2·가스등 거리 L자 4칸이 지붕·문·굴뚝 연기·상징물로 덮임, 로그 `BUILDING: hub-1 … 지붕 machine/housing 상징물 … 문 칸 …`), `027-v2-furnace.png`(--habitat-stage furnace: 3×3 용광로 9칸 지붕 + 굴뚝 연기), `027-v2-dialog.png`(--npc-demo --npc-stage panel: 초상 상자·타자기 대사·닫기 오른쪽 위; 문 앞에 선 점등부). 빈 건물 클릭 토스트는 코드 경로(`BUILDING CLICK: … 사는 NPC 없음`)로 확인, 실제 마우스 클릭은 사용자 확인. `buildings_contact_sheet.png`.

---

## Dependencies

- Depends on: `story-020-habitat-catalog-redesign-grid.md`, `story-023-npc-rosters-conditions-arrival.md`, `story-026-ui-simplify.md`
- Unlocks: None
