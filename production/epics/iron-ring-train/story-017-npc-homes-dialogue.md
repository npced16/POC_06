# Story 017: NPC 집 배정, 서식지 곁 배회, 클릭 대화·상호작용

> **상태 메모(2026-11-02 정리)**: 부분 유효 — NPC 집 배정은 살아 있고, 대화 진입·배치는 027(건물 클릭)·029(고정 배치·대화 장면)가 대체. 현재 규칙은 story-027·story-029 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-01 18:05 KST 코디네이터 브리지 검증 — 컴파일 통과, 캡처 3장 육안 확인: 집 곁 NPC 2명, 대화 패널, 격려 결과·비활성 버튼)
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 4 — 합류한 NPC는 거점 내부에 거주. 사용자 지시(2026-11-01 17:40): "캐릭터는 각자 서식지 배정되면 각자 서식지 근처에 있도록, 클릭 시 UI 나와서 대화되게끔 혹은 상호작용 되도록."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 집 배정: NPC가 합류하면 그 NPC의 서식지 종류 가운데 그 거점에서 완성된 묶음 하나가 집이 된다(같은 종류가 여럿이면 아직 아무도 살지 않는 묶음 우선). `INpcHomes.HomeOf`가 거점·종류·칸 목록을 돌려준다.
- [x] 집이 깨지면(타일 회수로 서식지 해제) 칸 목록이 비고 `HomeChanged`가 난다. 같은 종류가 다시 완성되면 그 묶음을 집으로 되찾는다. NPC는 거점을 떠나지 않는다.
- [x] 배회: 내부 화면에서 NPC는 집 칸들과 그에 상하좌우로 붙은 빈 칸 사이만 오간다. 집이 없으면(깨짐·미배정) 지금처럼 거점 전체를 배회한다. 움직임 값(속도·멈춤)은 기존 데이터.
- [x] 클릭: 빈 손(핫바 선택·드래그 없음)으로 NPC를 좌클릭하면 대화 패널이 열린다. 핫바 선택 중에는 NPC 클릭이 배치로만 처리된다. 패널·HUD 위 클릭은 NPC에 닿지 않는다.
- [x] 대화 패널(UI Toolkit, 한국어): 직업명, 집 서식지 이름(없으면 "집 없음"), 현재 대사, 버튼 [대화](다음 대사, 데이터의 줄 목록 순환) [격려](정차 1회, 그 거점 번영 +N — N은 데이터) [닫기]. 격려를 이미 했으면 버튼이 비활성이고 이유가 보인다. 선택지 목록과 라벨은 데이터(`INpcInteraction.Options`).
- [x] 격려 결과는 패널에 한 줄로 보이고(`번영 +N`), `IHubStateSystem.ProsperityChanged`로 HUD 번영 숫자가 바뀐다. 출발하면 격려 횟수가 초기화된다.
- [x] 대사: NPC마다 2~3줄(`NpcPool.json`의 `_lines`, 없으면 `_quote` 한 줄). 모두 스팀펑크 직업에 맞는 한국어.
  - 로직 메모: `_quote`가 첫 줄, `_lines` 2줄이 뒤따라 NPC마다 3줄. 격려 로직(데이터 N, 정차당 1회, `ApplyDelta`)은 `NpcInteraction`에 구현됨 — 계약에 출발 이벤트가 없어 `ITrackRuntime.Arrived`(출발 뒤 다음 노드 도착)에서 초기화. 패널·HUD 표시는 표현 쪽 AC.
- [x] 스크린샷: (1) 집 서식지 곁에 모여 있는 NPC 2명 이상, (2) 대화 패널이 열린 화면, (3) 격려 뒤 결과 문장과 비활성 버튼.

---

## Implementation Notes

- 계약: `INpcHomes`, `NpcHome`, `INpcInteraction`, `NpcInteractionOption`(`IronRingContracts.cs`), `INpcClickSource`(`IronRingPresentationContracts.cs`). 로직은 `Assets/Scripts/Npc/`(NpcHomes, NpcInteraction, 데이터 JSON), 등록은 Composing 훅의 설치자.
- 표현: `InteriorNpcWalker`가 집 칸 기준 목표를 고르고 NPC 몸체에 2D 콜라이더를 붙여 클릭을 `INpcClickSource`로 올린다. 패널은 `Assets/Scripts/Hud/Npc/NpcDialogHud.cs` + `Assets/UI/Hud/Resources/Hud/NpcDialog.uxml/NpcDialogStyle.uss`. 패널은 PlacementGhost의 UI 판정에 등록한다.
- 데모 `--npc-demo [--npc-stage homes|panel|encouraged]`: hub-1에 서식지 두 종류를 완성하고 NPC 두 명을 합류시킨 뒤, 단계에 따라 첫 NPC의 패널을 열거나 격려까지 실행한다.

---

## Out of Scope

- NPC 간 대화, 선물·거래, NPC 이동 명령, 대화 트리(한 줄 대사 순환만)

---

## Files

- `Assets/Scripts/Npc/NpcHomes.cs`, `NpcInteraction.cs`, `NpcInstaller.cs`, `Data/NpcPool.json`(줄 추가), `Data/NpcInteraction.json`, `Demo/NpcHomesDemoDriver.cs`
- `Assets/Scripts/Npc/NpcDefinition.cs`·`NpcPoolData.cs`(`_lines`, `Find`), `NpcDataRefs.cs` + `Resources/Npc/NpcData.asset`(Resources 밖 JSON 두 개 참조), `Resources/Npc/NpcHomesDemo.json`(데모 배치·방문 횟수)
- `Assets/Scripts/Presentation/HubPhase/InteriorNpcWalker.cs`, `Assets/Prefabs/HubPhase/`(NPC 프리팹 콜라이더)
- `Assets/Scripts/Hud/Npc/NpcDialogHud.cs`, `Assets/UI/Hud/Resources/Hud/NpcDialog.uxml`, `NpcDialogStyle.uss`
- `Assets/Scripts/Localization/Resources/ko.json`(키 추가)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 3장: `production/qa/evidence/story-017-npc-homes-dialogue/`

**Status**: [x] `017-homes.png`(점등부는 가스등 거리 위, 원예사는 온실 옆 칸), `017-panel.png`(점등부 패널: 집 가스등 거리, 대사, 대화·격려·닫기), `017-encouraged.png`(원예사 패널: "격려했다. 번영 +3", 격려 버튼 비활성 + 이유, 상단 번영 0→3). 로그: `NPC DEMO: homes → gardener:greenhouse[19,20,21,22]`, `lamplighter:lamp_street[7,13,14,15]`.

---

## Dependencies

- Depends on: `story-016-habitat-types-clusters.md`, `story-015-placement-hotbar-dragdrop.md`
- Unlocks: None
