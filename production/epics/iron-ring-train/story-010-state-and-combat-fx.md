# Story 010: 거점 상태와 전투 연출

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP features 2, 4, 5, 6 + Art & audio direction — 폐허·시설·NPC·전투가 탑뷰 씬에서 보인다

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] `GameServices.Ready` 이후 `IMapAnchors`로 거점·노드 위치를 얻어 연출 객체를 만든다. 씬 파일은 수정하지 않는다.
- [x] 거점 위에 번영 게이지가 보이고 `IHubStateSystem.ProsperityChanged`에 따라 갱신된다. `HubRuined` 시 거점 스프라이트가 폐허 버전으로 바뀌고 잔해 느낌의 간단한 연출이 나온다.
- [x] `FacilityBuilt` 시 거점 주변에 시설 스프라이트가 추가되고, `NpcJoined` 시 NPC 스프라이트와 한국어 한 줄 말풍선이 거점에 나타난다.
- [x] `EncounterStarted`(전투) 시 경유지 옆에 습격자 스프라이트가 등장하고, 라운드 결과가 떠오르는 한국어 텍스트로 표시되며, `EncounterEnded` 시 습격자가 사라진다. 이벤트 종료는 아이콘과 한 줄 문구로 보인다.
- [x] `RunLost` 시 화면이 어두워지고 한국어 패배 문구가 씬 위에 표시된다.
- [x] 폐허·시설+NPC·전투·패배 스크린샷을 남긴다.

---

## Implementation Notes

- 모든 문구는 `IronRing.Localization.Strings`(Story 008)에서 가져온다. 하드코딩 금지.
- 자기 데모 씬(`Assets/Scenes/Demo/Story010/`)에서는 `IMapAnchors`와 시스템 인터페이스의 페이크를 써서 검증한다. 실제 씬 연결은 코디네이터의 통합 실행에서 확인한다.

---

## Out of Scope

- Story 009: 맵과 열차 이동
- Story 011: HUD 패널

---

## Files

- `Assets/Scripts/Presentation/Effects/StateCombatEffects.cs`: `GameServices.Ready` 후 `IMapAnchors`와 시스템 이벤트 구독, 번영 게이지·폐허·시설·NPC 말풍선·전투·패배 연출.
- `Assets/Scripts/Presentation/Effects/EffectsSettings.cs`: Inspector에서 편집 가능한 연출 설정(위치·크기·시간·색상).
- `Assets/Scripts/Presentation/Effects/Demo/EffectsDemo.cs`: 지도 및 일곱 시스템 계약 페이크와 자동 시나리오(`--fx-demo`, `--fx-stage=`).
- `Assets/Scripts/Presentation/Effects/Editor/Story010Build.cs`: 기존 편집을 보존하는 애셋·씬 최초 생성기.
- `Assets/Scripts/Presentation/Effects/Resources/Story010Effects.asset`, `EffectsSprite.mat`: 아트 참조와 연출 데이터, URP 비조명 재질.
- `Assets/Scenes/Demo/Story010/Story010Effects.unity`: 전용 탑뷰 데모 씬.
- `production/qa/evidence/story-010-state-and-combat-fx/`: 스크린샷 4장, `verification.md`, `source-stamp.txt`.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 폐허·시설+NPC·전투·패배 스크린샷: `production/qa/evidence/story-010-state-and-combat-fx/` (필수)

**Status**: [x] 검증 완료 (2026-11-01) — 인수 조건 6개 모두 확인.

**스크린샷 (Visual/Feel 필수 증거, 보존됨)**
- `production/qa/evidence/story-010-state-and-combat-fx/01-ruin.png`: 폐허 — 폐허 거점 스프라이트, 「폐허」 이름표, 번영 게이지.
- `production/qa/evidence/story-010-state-and-combat-fx/02-facilities-npc.png`: 시설+NPC — 방벽·포탑·공방 스프라이트, NPC 스프라이트와 말풍선 「새 동료가 합류했습니다」.
- `production/qa/evidence/story-010-state-and-combat-fx/03-combat.png`: 전투 — 경유지 옆 습격자 스프라이트, 떠오르는 문구 「1/3 습격을 막아냈습니다」.
- `production/qa/evidence/story-010-state-and-combat-fx/04-defeat.png`: 패배 — 어두워진 화면 위 「모든 거점이 무너졌습니다 / 철환의 여정이 끝났습니다」.
- 캡처 시각: 2026-11-01 13:39~13:40 KST, 편집기 내 브리지(`tools/bridge`)로 `Assets/Scenes/Demo/Story010/Story010Effects.unity` Play 캡처(`--fx-demo --fx-stage=ruin|facilities|combat|defeat`, 45프레임 대기).
- 코디네이터 육안 확인: 위 네 항목의 스프라이트·한글 문구·게이지가 모두 보이고 겹치지 않음.

**보조 증거**
- `verification.md`: 구현, 브리지 컴파일·씬 생성 결과(refresh/compile-status failed=false), 검증 완료 기록.
- `source-stamp.txt`: 소스 해시.

**한계**: 데모 씬은 일곱 시스템과 `IMapAnchors`의 페이크 위에서 동작한다. 실제 규칙 시스템과의 결합은 Main 씬 통합 실행(Story 009 증거)에서 확인한다. 정지 이미지는 배치·존재·한글 가독성을 증명하며 타이밍과 감각은 수동 플레이 대상이다.

---

## Dependencies

- Depends on: `story-008-art-set-korean-base.md` (009와는 계약으로만 연결, 병렬 가능)
- Unlocks: None
