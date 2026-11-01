# Story 009: 탑뷰 메인 씬과 열차 이동

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 1 + Art & audio direction — 고리형 선로 위의 열차 이동이 탑뷰 2D 씬에서 보인다

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] `Assets/Scenes/Main.unity`가 탑뷰 맵으로 재구성된다. 직교 카메라가 고리 전체를 1280x720 안에 담는다.
- [x] 선로 노드의 맵 좌표는 선로 데이터(`ring-track` JSON 확장)에서 읽는다. 고리형 선로가 선로 타일(Story 008)로 그려진다.
- [x] 거점 4곳은 거점 스프라이트와 한국어 이름표(월드 공간 텍스트, Story 008 폰트)로, 경유지 4곳은 표식 스프라이트로 보인다.
- [x] 열차 스프라이트가 `ITrackRuntime`의 실제 진행도를 따라 노드 사이를 부드럽게 이동하고 진행 방향으로 회전한다. 정차·홀드 중에는 멈춘다.
- [x] `IMapAnchors`(노드·거점·열차 앵커, 카메라)와 `IHubClickSource`(거점 스프라이트 클릭)를 구현해 `GameServices`에 등록하고, 컴포지션 루트가 모든 서비스를 등록한 뒤 `GameServices.MarkReady()`를 부른다.
- [x] 기존 로직(선로·거점·서식지·NPC·전투·시설)은 그대로 재사용되며, 이전 패널형 HUD 대신 씬이 화면의 주인공이다.
- [x] 열차가 선로 위를 이동하는 스크린샷과 정차 스크린샷을 남긴다.

---

## Implementation Notes

- `Assets/Scripts/Bootstrap/`의 컴포지션 루트와 `MainBuild` 씬 빌더는 이 스토리가 소유한다. 다른 워커는 Bootstrap을 수정하지 않는다.
- 거점 클릭은 `Collider2D` + 카메라 레이캐스트로 감지한다. 새 Input System을 쓴다.
- 열차 회전은 현재 노드와 다음 노드의 방향 벡터로 계산한다.

---

## Out of Scope

- Story 010: 폐허·시설·NPC·전투 연출
- Story 011: HUD와 패널

---

## Files

- `Assets/Scripts/Presentation/Map/MapView.cs`: `IMapAnchors`·`IHubClickSource` 구현, 실제 진행도 표시, 직선 보간과 원호 이동, 거점 클릭.
- `Assets/Scripts/Presentation/Map/MapNode.cs`, `Assets/Scripts/Presentation/Map/HubTarget.cs`: 편집 가능한 노드·열차 앵커와 거점 식별자.
- `Assets/Scripts/Bootstrap/MainRoot.cs`: 컴포지션 루트 — 일곱 규칙 서비스와 맵·클릭 서비스 등록 뒤 `GameServices.MarkReady()` 호출. 기존 영어 HUD 의존 제거.
- `Assets/Scripts/Bootstrap/GameSession.cs`: 세션 조립과 데모 유지.
- `Assets/Scripts/Bootstrap/GameDefinition.cs`, `Assets/Scripts/Bootstrap/Data/MainSettings.json`: 이벤트 제목과 한국어 키 매핑.
- `Assets/Scripts/Bootstrap/EncounterPresentationSource.cs`: `IEncounterPresentationSource`·`ICombatRoundCommand` 어댑터(전투 표현과 라운드 명령).
- `Assets/Scripts/Bootstrap/Editor/BuildTopDownMain.cs`, `Assets/Scripts/Bootstrap/Editor/MainBuild.cs`: 열린 편집기용 씬 생성과 명시적 Rebuild.
- `Assets/Scenes/Main.unity`: 탑뷰 맵, 직교 카메라(크기 11.25), 선로 타일 56개, 거점 4·경유지 4, 한글 이름표 8개, EventSystem·AudioListener.
- `Assets/Scripts/Track/Resources/Track/ring-track.json`: 노드별 `_mapPosition` x/y(8개)와 곡선·제작 기본값 추가.
- `Assets/Scripts/Localization/Resources/ko.json`: 코디네이터 추가 승인에 따른 이벤트 이름 키 4개 추가.
- `Assets/Tests/EditMode/Integration/Editor/IntegrationFlowTests.cs`: 기존 두 테스트의 HUD 경유 검증을 실제 시스템 검증으로 변경.
- `production/qa/evidence/story-009-topdown-main-scene/`: 스크린샷 2장, 브리지 결과, 정적 씬 검사 및 검증 문서.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 이동·정차 스크린샷: `production/qa/evidence/story-009-topdown-main-scene/` (필수)

**Status**: [x] 검증 완료 (2026-11-01) — 인수 조건 7개 모두 확인.

**자동 테스트**
- `production/qa/evidence/integration/editmode-presentation.xml`: EditMode 81/81 통과(실패 0). `IronRing.Track.Tests.RingTrackTests` 10/10, `IronRing.Integration.Tests.IntegrationFlowTests` 2/2 포함. 열린 편집기의 브리지(`tools/bridge`) `run-tests`로 실행.

**스크린샷 (Visual/Feel 필수 증거, 보존됨)**
- `production/qa/evidence/story-009-topdown-main-scene/01-stopped.png`: 정차 — 열차가 주조소에 도킹한 상태.
- `production/qa/evidence/story-009-topdown-main-scene/02-moving.png`: 이동 — `--map-depart` 인자로 출발한 뒤 열차가 오른쪽으로 이동 중.
- 캡처 시각: 2026-11-01 13:44~13:45 KST, 편집기 내 브리지(`tools/bridge`)를 통해 `Assets/Scenes/Main.unity` Play 캡처.
- 코디네이터 육안 확인: 고리형 선로 타일, 거점 4곳과 한국어 이름표(주조소/정원/항구/성채), 경유지 4곳(북쪽·동쪽·남쪽·서쪽 고개), 주조소에 정차한 열차와 `--map-depart` 뒤 오른쪽으로 이동하는 열차.

**보조 증거**
- `verification.md`: 구현, 제작 기본값, 엔진 검토 반영 내역과 검증 완료 기록.
- `refresh.json`, `scene-build.json`: 열린 편집기의 컴파일과 Rebuild 결과.
- `scene-inspection.json`: 노드 8개 좌표, 선로 56개, 한글 이름표 8개와 거점 클릭 대상 4개 정적 확인.
- `source-stamp.txt`: 변경 소스 및 씬 SHA256.

**한계**: 정지 이미지 2장은 배치·이동·한글 가독성을 증명한다. 입력 반응과 전체 이동 감각은 수동 플레이 확인 대상이다.

---

## Dependencies

- Depends on: `story-008-art-set-korean-base.md`
- Unlocks: `story-010-state-and-combat-fx.md`, `story-011-korean-hud-mouse.md` (병렬 가능, 최종 통합 실행은 코디네이터)
