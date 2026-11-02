# Story 051: 건설 → 순환(중간역 사건·선택지) → 건설·조합 반복 루프

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Done (사건 창 화면 육안 확인만 남음)
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: 사용자 지시(2026-11-02 12:40): "1. 건설페이즈 2. 순환페이즈(중간역에서 합류하는 사람 or 물자 고르기 등등 - 이벤트) 3. 다음으로 건설 & 조합페이즈 반복되도록". 질문 뒤 선택: 물자 = **타일 묶음**(재고에 쌓이고 배치 때 재고 먼저, 없으면 돈), 중간역 = **랜덤 사건이 나오고 그 사건의 선택지를 고른다**(카드 고르기 아님).
지금 휴식지 이벤트(`Combat/Resources/Story005Encounters.json`)는 고르는 것 없이 자동(번영 ±, 탑승 확률, 지연)이고 화면에 페이즈 구분이 없다.

## 루프

1. **건설 페이즈**(거점 정차 중): 배너 "건설 페이즈 — 바퀴 N". 타일 배치·건물 완성·조합 해금(050). 끝은 플레이어가 **출발**을 누를 때(시간 제한 없음).
2. **순환 페이즈**(출발 ~ 거점 복귀): 배너 "순환 페이즈". 열차가 고리를 돌고 **휴식지마다 정차해 사건 하나**가 뜬다(사건 창: 제목·짧은 상황 글·선택지 2~3개). 선택하면 결과 한 줄을 보여 주고 출발. 잠긴 역(잿골 잠김)은 그냥 지나감, 열린 다른 마을은 정차만(사건 없음, 승객 하차·건설 가능 여부는 기존 규칙).
3. 거점 복귀 → 정산 토스트 → 다시 **건설 페이즈**(이번엔 재고 물자로 건설·조합).

## 사건 (데이터: 새 `Assets/Scripts/Track/Resources/Track/RestEvents.json` 또는 기존 encounter 파일 교체 — 구현자가 정하되 한 파일)

- 사건 = `_id`, `_title`, `_text`, `_weight`, `_minLap`(선택), `_choices[]`. 선택지 = `_label`, `_result`(결과 한 줄), `_effects[]`.
- 효과 종류(`_effects`): `tiles`(타일 묶음 → 재고, `_items:[{id,count}]` 또는 `_randomUnlocked:n` = 열린 타일 중 무작위 n개), `passenger`(지정 NPC 또는 무작위 NPC가 합류 → 거점에서 하차, 기존 승객 규칙), `money`(±), `prosperity`(±, 황동골), `delay`(다음 휴식지까지 지연), `nothing`.
- 타일 묶음은 **열린 타일만**(050 `IHabitatCodex.IsTileUnlocked`). 잠긴 타일이 지정되면 열린 타일로 대체.
- 처음 묶음 (스팀펑크 톤, 10종 안팎), 예:
  - **떠돌이 기술자**: "길가에 공구 가방을 멘 기술자가 손을 흔든다." → [태운다: 승객 합류] [공구만 산다: −8마르크, 작업대×2] [지나친다]
  - **버려진 화물칸**: "선로 옆에 뒤집힌 화물칸." → [뒤진다: 열린 타일 무작위 3] [고철만 챙긴다: 철골×1(열렸으면) 아니면 +5마르크]
  - **석탄 상인**: [석탄 3 + 보일러 1 (−10마르크)] [흥정한다: 50% 석탄×2 무료 / 50% 상인이 떠남]
  - **피난민 가족**: [태운다: 승객 2 합류, 번영 −2] [물과 빵을 나눈다: 번영 +3]
  - **모래 폭풍**: [기다린다: 지연] [뚫고 간다: 무작위 재고 타일 1개 잃음]
  - **급수탑 관리인**: [물탱크 수리 돕기: 물탱크×1] [물만 받는다: 번영 +2]
  - **선로 보수반**: [일을 돕는다: 지연, +6마르크] [지나친다]
  - **우편 행낭**: [배달해 준다: 다음 거점 도착 시 +12마르크] [열어 본다: 무작위 타일 2, 번영 −3]
  - (그 밖 구현자가 2~3종, 모두 증기·황동·폐허 세계관)
- 같은 바퀴에 같은 사건은 한 번만.

## 타일 재고

- 품목별 재고 수(`ITileStock`: `CountOf(itemId)`, `Add`, `TryTake`, `event Changed`). 배치 시 재고가 있으면 재고 1을 쓰고 돈은 안 냄, 없으면 기존처럼 돈. 회수/철거 시 재고로 되돌림(돈 환급 대신 — 단, 돈으로 산 타일은 기존처럼 환급; 구현자가 단순한 쪽으로 정하고 기록).
- 핫바 슬롯에 재고 수 배지(×3), 재고가 있으면 가격 대신 "재고 3".

## Acceptance Criteria

- [ ] 페이즈 상태 `IPhaseLoop`(Build/Circulate, `event PhaseChanged`, `Lap`) — 배너 "건설 페이즈 — 바퀴 N" / "순환 페이즈". 출발 버튼은 건설 페이즈에서만.
- [ ] 휴식지 도착 → 열차 정지 → 사건 창(선택지 2~3) → 선택 → 결과 한 줄 → 출발(자동, 또는 [계속] 버튼). 기존 자동 이벤트는 이 사건으로 대체.
- [ ] 효과 6종 동작, 타일 묶음은 열린 타일만, 재고에 쌓이고 배치 때 먼저 쓰임.
- [ ] 합류한 사람은 승객 목록에 오르고 거점에서 내린다(기존 Passengers 규칙 재사용).
- [ ] 데모 `--loop-demo`: 건설(보일러실 완성) → 출발 → 휴식지 3곳에서 첫 번째 선택지 자동 선택 → 복귀 → 재고로 배치. 로그 `LOOP: phase Build lap=1`, `LOOP: event <id> choice <n> -> <effects>`, `LOOP: stock tile_coal=3`, `LOOP: phase Build lap=2`.
- [ ] 검증: 브리지 refresh failed=false, scene-status isDirty=false, 데모 로그, 캡처(건설 배너·사건 창·재고 배지) `production/qa/evidence/story-051-phase-loop-events/`.
- [ ] 브리프: 한 바퀴 = 건설 → 순환(사건 선택) → 건설·조합, 물자 = 타일 묶음 재고.

## Implementation Notes

- 로직(Claude gameplay): 페이즈 상태, 휴식지 정차·사건 선택 흐름(RingTrack/TrainController 정차 유지 → 선택 후 Depart), 사건 데이터·효과 적용, `ITileStock` + ItemPlacement 결제 경로, 승객 합류(Passengers), 옛 encounter 자동 효과 끄기, 데모. `production/handoffs/ui-051.md` "로직 API" 절.
- UI(codex): 페이즈 배너, 사건 창(제목·상황 글·선택지 버튼·결과 줄 — NpcDialog/DialogOption 프리팹 재사용 검토), 핫바 재고 배지. `production/handoffs/ui-051.md`.
- 씬 YAML 직접 편집 금지.

## Out of Scope

- 사건 연쇄(앞 선택이 뒤 사건을 부름), 상점 화면, 재고 상한, 세이브.

## Files

- `Assets/Scripts/Track/**`, `Assets/Scripts/Combat/EncounterData.cs`·`Resources/*.json`, `Assets/Scripts/Items/**`, `Assets/Scripts/Npc/Passengers.cs`, `Assets/Scripts/Economy/**`(결제), `Assets/Scripts/Contracts/*.cs`, `Assets/Scripts/Hud/**`·`Assets/Prefabs/UI/**`(codex), `ko.json`, `design/game-brief.md`

## Test Evidence

**Story Type**: Integration — `production/qa/evidence/story-051-phase-loop-events/verification.md` + 캡처
