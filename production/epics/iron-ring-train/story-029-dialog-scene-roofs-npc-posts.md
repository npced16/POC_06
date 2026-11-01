# Story 029: 옛 온라인 게임식 대화 장면(대형 초상 + 하단 대화 상자), 건물 지붕 디테일, NPC 고정 배치

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Core loop 3단계(건물 클릭 대화). 사용자 지시(2026-11-01 23:05)와 참고 이미지(마비노기풍 대화 장면: 화면 왼쪽에 큰 상반신 초상, 아래 가운데에 금테 대화 상자 — 이름, 여러 줄 대사, "계속하기" 버튼): "대화창은 이런 느낌. 1. 건물 지붕 디테일 올리고, 2. NPC는 각 건물에 배치하는 방식, 돌아다니지 말도록."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: UGUI + TMP, 2D URP.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 초상: NPC 35명마다 상반신 초상 스프라이트(`Assets/Art/Generated/portraits/portrait_<npcId>.png`, 96×128, PPU 64, Point)를 생성기 `SpriteGenPortraits`가 만든다. 종이 인형식 조합(얼굴형·피부·머리·수염·모자/고글/모자 없음·옷깃·장식)을 NPC id 시드와 직업(데이터 `_portrait` 힌트: `hat|goggles|cap|bare`, `beard`, `hair` 색)으로 정하고, 팔레트는 스팀펑크(황동·구리·가죽·연기색). 같은 직업이라도 사람이 다르면 얼굴이 다르다. `Assets/Scripts/Npc/Resources/Npc/NpcPortraits.asset`(`NpcPortraitCatalog`, id → Sprite, 없으면 대체 초상)에 등록한다. 콘택트 시트 1장.
- [ ] 대화 장면(UGUI 화면 `NpcDialog` 재구성): 열리면 화면 왼쪽에 초상을 크게(높이 화면의 55%, 아래쪽 기준) 띄우고, 아래 가운데에 폭 70%의 대화 상자(Simple 스킨 패널 + 안쪽 1px 황동 이중 테두리)를 둔다. 상자 안: 이름(Heading, 황동색), 대사 영역(Normal, 최대 4줄, 타자기), 오른쪽 아래 "계속하기" 버튼. 계속하기는 타자기 중이면 즉시 완성, 끝났으면 다음 줄(NPC의 `_quote` + `_lines`)로 넘어가고, 마지막 줄 뒤에는 선택지 줄(기존 `INpcInteraction.Options`: 격려하기 등 + "닫기")이 버튼으로 나타난다. 격려 결과 문장도 같은 상자에 한 줄로 뜬다. Esc·상자 밖 클릭은 닫기. 열려 있는 동안 핫바·배치 입력은 막는다(상황 화면 규칙).
- [ ] 대화 중 배경: 내부 화면은 그대로 보이되 살짝 어둡게(반투명 그늘, 데이터), 해당 NPC 몸체는 문 앞에서 플레이어 쪽을 본다.
- [ ] NPC 고정 배치: 합류한 NPC는 항상 자기 건물의 문 앞 자리에 선다(배회 코드 경로 삭제가 아니라 데이터 `_npcWanders:false` 고정 + 건물이 없는 NPC는 집 칸 가운데에 선다). 한 건물에 여럿이면 문 양옆으로 0.45칸씩 벌려 선다(최대 3명, 그 이상은 뒤 줄). 도착 걷기(023)는 그 자리에서 끝난다.
- [ ] 지붕 디테일: 지붕 4계열에 기와 줄무늬·골함석 이랑·슬레이트 비늘·유리 격자 질감, 처마 그림자 1px, 모서리 황동 덮개, 벽(아래 가장자리)에 창문 1~2개(호박색 불빛, 밤낮 없음), 지붕 위 작은 장식(환풍기·안테나·물탱크 중 시드 선택)을 더한다. 문 2종에 문틀·계단. 상징물은 그림자와 받침. 콘택트 시트로 전후 비교.
- [ ] 스크린샷: (1) 대화 장면(초상 + 상자 + 계속하기), (2) 마지막 줄 뒤 선택지, (3) 지붕 디테일 건물 3채(일자·L자·3×3), (4) 한 건물 문 앞 NPC 2명. `production/qa/evidence/story-029-dialog-scene-roofs-npc-posts/` + verification.md.

---

## Implementation Notes

- 초상·지붕은 `Assets/Scripts/Art/Editor/`(ArtKit·팔레트). `NpcPortraitCatalog`(런타임 ScriptableObject, 코디네이터가 미리 둠)는 생성기가 SerializedObject로 채운다.
- 대화 화면은 `Assets/Scripts/Hud/Npc/NpcDialogScreen.cs` + `Editor/Screens/NpcDialogScreenBuilder.cs`를 다시 짠다(화면 id `NpcDialog` 유지, 데모 진입점 `OpenForDemo/InteractForDemo/HandleDemoStage` 유지). 초상은 `Resources.Load<NpcPortraitCatalog>("Npc/NpcPortraits")`. 그늘은 `CombatFxScreen.ShowShade`와 별개로 대화 화면 자체의 전체 Image.
- NPC 배치는 `InteriorNpcWalker.cs` + `hub_phase_view.json`(문 앞 오프셋, 옆 간격).
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- NPC 감정 표정 변화, 음성, 대화 트리.

---

## Files

- `Assets/Scripts/Art/Editor/SpriteGenPortraits.cs`·`SpriteGenBuildings.cs`, `Assets/Art/Generated/portraits/**`·`buildings/**`, `Assets/Scripts/Npc/NpcPortraitCatalog.cs`·`Resources/Npc/NpcPortraits.asset`, `Assets/Scripts/Npc/Data/NpcPool.json`(`_portrait` 힌트)
- `Assets/Scripts/Hud/Npc/NpcDialogScreen.cs`·`Editor/Screens/NpcDialogScreenBuilder.cs`·`Prefabs/UI/Screens/NpcDialog.prefab`, `ko.json`
- `Assets/Scripts/Presentation/HubPhase/InteriorNpcWalker.cs`·`HubPhaseViewSettings.cs`·`hub_phase_view.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 스크린샷 4장 + 콘택트 시트: `production/qa/evidence/story-029-dialog-scene-roofs-npc-posts/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-027-combo-buildings-click-talk.md`
- Unlocks: None
