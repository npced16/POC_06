# Story 038: 2차 — 탑뷰 여정·전투 화면(프리미티브): 같은 여정을 위에서 본 화면으로, 1차와 전환해 비교

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready (037 뒤)
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 1·4. 사용자 지시(2026-11-02 02:35): "2차로 탑뷰로 만들어줘." 037의 횡스크롤 여정·전투를 **위에서 본 화면**으로도 만들어, 데이터 플래그로 둘을 바꿔 가며 어느 쪽이 재미있는지 비교한다. 역시 프리미티브.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: LOW
**Engine Notes**: 037의 구조(카탈로그·프리팹·생성기·IRaid 사상)를 재사용.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] `MainSettings.json` `_travelView: "top"`이면 출발 시 **탑뷰 여정 화면**(`TravelTopView`, 루트 `TravelTop`, y=−400 부근): 선로가 화면 가운데를 가로지르고 열차(3량 사각형, 장비 칸 점)가 화면 왼쪽 1/3에 고정된 채 선로 위를 달리며, 선로 양옆 풍경(역 승강장·건물 블록·폐역·급수탑·다리·전초)이 왼쪽으로 흐른다(시차 없음, 위·아래 띠 2개). 같은 `travel-side.json` 지형지물 목록을 쓴다(위치·라벨 공유, 모양만 탑뷰용).
- [ ] 습격: 종점에서 둥지 3개가 선로 끝 너머 위·아래·정면에, 몬스터는 선로 양옆과 정면에서 열차 머리로 달려오고(IRaid의 X·Y를 탑뷰 좌표로 사상: X = 선로 방향 거리, Y = 선로에서 옆으로의 거리), 포탑은 자기 칸에서 가장 가까운 적을 향해 쏜다(포탄 원, 명중 번쩍임). 클릭 사격·HUD·결과 흐름은 037과 같다.
- [ ] 되돌아오기·도착: 037과 같은 규칙(풍경 반대로, 열차 반전).
- [ ] 데모: `--travel-demo --travel-stage …`가 `_travelView`에 따라 두 화면 모두 동작. 스크린샷 (1) 출발 직후, (2) 종점 습격 중, (3) 복귀 도착. `production/qa/evidence/story-038-top-down-travel-combat/` + verification.md. 브리프에 "탑뷰 여정(2차)과 데이터로 전환" 한 구절.

---

## Implementation Notes

- `Assets/Scripts/Presentation/Travel/TravelTopView.cs`·`TravelRaidTopView.cs`·`Editor/TravelTopBuild.cs`; 공통 코드는 037의 `TravelPrimitives`·카탈로그·지형지물 로더를 쓴다. 두 뷰는 동시에 켜지지 않는다(플래그로 하나만).
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 픽셀 스프라이트, 두 뷰 실시간 전환 키(데이터 플래그만).

---

## Files

- `Assets/Scripts/Presentation/Travel/TravelTopView.cs`·`TravelRaidTopView.cs`·`Editor/TravelTopBuild.cs`, `Assets/Prefabs/Travel/**`, `Assets/Scripts/Bootstrap/Data/MainSettings.json`, `Assets/Scenes/Main.unity`, `design/game-brief.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 3장: `production/qa/evidence/story-038-top-down-travel-combat/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-037-side-scroll-travel-combat.md`
- Unlocks: None
