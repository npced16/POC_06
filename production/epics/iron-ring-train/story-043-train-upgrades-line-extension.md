# Story 043: 열차 업그레이드와 선로 확장 — 화물칸·승객칸·기관 구매, 새 휴식지/거점 개통

> **상태 메모(2026-11-02 정리)**: 대체 — 열차 업그레이드는 보류, 선로 확장은 048의 '정착촌 달성 시 선로 개통'으로 부분 채택. 현재 규칙은 design/game-brief.md §선로가 열린다 와 story-048 참조.

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Superseded(045) — 원래: Ready (042 뒤)
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 4. 사용자 지시(2026-11-02 04:20): "완전 경영 시뮬레이션 쪽으로 싹 다 가주라." 돈을 열차와 노선에 투자한다. 등급 해금 표에 따라 살 수 있는 것이 늘어난다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 순수 C# 로직 + UGUI 화면 프리팹(빌더 리플렉션) + 데이터 JSON. 검증은 파이프라인(컴파일·생성기·씬 저장), 캡처 없음.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 열차 업그레이드(`TrainUpgrades.json`): 화물칸 +6칸(가격 120, 최대 3), 승객칸 +2명(가격 90, 최대 2), 기관 속도 +20%(가격 150, 최대 2). 거점 내부 "열차" 패널(UGUI `TrainUpgrade`: 항목·현재 단계·가격·[구매])에서 구매, 등급 해금 반영, 내부 열차 칸 수가 늘어난 만큼 선로 위 열차 칸도 데이터로 늘어남(프리팹 인스턴스).
- [ ] 선로 확장(`LineExtensions.json`): 등급 "마을"부터 새 휴식지 1곳(예: "황동 다리 장터", 고가 시장) 개통(가격 300) — `ring-track.json`의 잠긴 노드(`_locked:true`)를 열면 원이 5점이 되고 선로 그림·라벨이 갱신된다(잠긴 노드를 처음부터 그려 두고 "폐쇄" 표시 → 개통 시 표지 교체).
- [ ] 데모 `--upgrade-demo --upgrade-stage cars|engine|extend`.

---

## Implementation Notes

- `Assets/Scripts/Economy/TrainUpgrades.cs`·`LineExtensions.cs` + JSON, `TrainController`(속도 배율), `ICargo` 용량, `Passengers` 좌석, `RingTrack`/`MapView`(잠긴 노드 표시·개통), `Hud/TrainUpgrade/**` + 빌더, 내부 열차 칸 프리팹 수(Story012Build 데이터), ko.json.

---

## Out of Scope

- 노선 2개 이상, 열차 2대.

---

## Files

- `Assets/Scripts/Economy/TrainUpgrades.cs`·`LineExtensions.cs`·`Resources/Economy/*.json`, `Assets/Scripts/Track/TrainController.cs`·`RingTrack.cs`·`Resources/Track/ring-track.json`, `Assets/Scripts/Presentation/Map/MapView.cs`·`MapNode.cs`, `Assets/Scripts/Presentation/Interior/Editor/Story012Build.cs`, `Assets/Scripts/Hud/TrainUpgrade/**`·`Editor/Screens/TrainUpgradeScreenBuilder.cs`·`Editor/BuildMainCanvas.cs`, `Assets/Prefabs/UI/Screens/TrainUpgrade.prefab`, `ko.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 생성기 멱등 + 씬 저장(isDirty=false) + 데이터 키 표: `production/qa/evidence/story-043-train-upgrades-line-extension/verification.md`(캡처 없음 — 사용자 지시)

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-042-population-labor-town-tier.md`
- Unlocks: `story-044-management-hud-ledger-events.md`
