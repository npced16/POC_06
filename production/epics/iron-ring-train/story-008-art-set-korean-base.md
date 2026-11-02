# Story 008: 플레이스홀더 아트 세트와 한국어 기반

> **상태 메모(2026-11-02 정리)**: 부분 유효 — 한국어 기반(ko.json·글꼴)은 살아 있고, 플레이스홀더 아트는 019 스팀펑크 환경 아트로 대체. 현재 규칙은 design/game-brief.md §Art & audio 와 story-019 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief Art & audio direction — 탑뷰 2D 도트, 황동·증기 톤, 게임 내 텍스트는 한국어 (표현 계층 전환의 기반)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Art & audio direction), scoped to this story:*

- [x] 에디터 배치 메서드 `IronRing.Art.Editor.SpriteGen.Generate`가 `Assets/Art/Generated/` 아래에 PNG 스프라이트를 코드로 생성한다. 재실행하면 같은 결과가 나온다.
- [x] 생성 목록: 선로 타일(직선 가로/세로, 곡선 4종), 거점 4종(주조소·정원·항구·성채) 정상/폐허 각 1장, 열차(위 방향 기준 1장, 회전해서 씀), 경유지 표식, 습격자, 시설 3종(방벽·포탑·공방), NPC 1종, 번영 게이지 배경/채움.
- [x] 32픽셀 기준 도트이며, 팔레트(황동·증기 톤)는 `Assets/Art/palette.json`에서 읽는다. 임포트 설정은 Pixels Per Unit 32, Filter Point, Compression None.
- [x] 한글 폰트 `Assets/Fonts/NotoSansKR-Regular.otf`(OFL 라이선스 파일 동봉)로 UI Toolkit용 FontAsset을 에디터 스크립트로 생성하고, 런타임 UI의 기본 폰트로 지정한다.
- [x] 한국어 문자열 테이블 `Assets/Scripts/Localization/Resources/ko.json`과 접근 API `IronRing.Localization.Strings.Get(key)`가 있다. 거점·경유지 표시명, 자원(석탄·고철·물·곡물), 시설, 이벤트 문구, HUD 라벨, 전투 문구가 들어 있다.
- [x] 한글이 깨지지 않고 렌더링되는 확인 씬 스크린샷을 남긴다 (스프라이트 시트 미리보기 포함).

---

## Implementation Notes

- 스프라이트는 `Texture2D`에 픽셀을 찍어 `EncodeToPNG`로 저장한다. 외부 아트 툴 없이 반복 생성할 수 있어야 한다.
- 폰트 파일은 코디네이터가 `C:/Users/JUNGLE/AppData/Local/Temp/claude/.../scratchpad/fonts-dl/`에 내려받아 둔다. 라이선스(OFL)도 함께 복사한다.
- 문자열 키는 영어 snake_case, 값은 한국어. 코드 식별자는 영어를 유지한다.

---

## Out of Scope

- Story 009: 씬 배치와 열차 이동
- Story 010: 연출
- Story 011: HUD 레이아웃

---

## Files

- `Assets/Scripts/Art/Editor/SpriteGen.cs`: 결정적 23종 스프라이트와 시트 생성.
- `Assets/Scripts/Art/Editor/FontSetup.cs`: FontAsset 및 기본 한글 패널/USS 생성.
- `Assets/Scripts/Art/Editor/Story008Build.cs`: 전용 씬 생성·빌드·재생성 검증.
- `Assets/Scripts/Art/Demo/ArtPreview.cs`: 한국어와 시트 표시.
- `Assets/Art/palette.json`, `Assets/Art/Generated/*.png`, `Assets/Art/Generated/manifest.json`.
- `Assets/Fonts/NotoSansKR-Regular.otf`, `Assets/Fonts/OFL.txt`, `Assets/Fonts/Resources/*`.
- `Assets/Scripts/Localization/Strings.cs`, `Assets/Scripts/Localization/KoreanUi.cs`, `Assets/Scripts/Localization/Resources/ko.json`.
- `Assets/Tests/EditMode/Localization/Editor/StringsTests.cs`: 12개 테스트 케이스.
- `Assets/Scenes/Demo/Story008/Story008Art.unity`: 최초 생성 후 수동 배치 보존.
- `production/qa/evidence/story-008-art-set-korean-base/verification.md`: 검증 기록 및 명령.


---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- 스프라이트 시트 미리보기 PNG와 한글 렌더링 스크린샷: `production/qa/evidence/story-008-art-set-korean-base/` (필수)
- 문자열 테이블 단위 테스트 (권장, advisory)

**Status**: [x] 검증 완료 (2026-11-01)

- 열린 Unity 편집기 브리지 컴파일 결과: 오류 없음 (코디네이터 실행).
- `production/qa/evidence/story-008-art-set-korean-base/editmode.xml`: Localization 12/12 통과, 실패 0.
- `production/qa/evidence/story-008-art-set-korean-base/generation-check.txt`: PNG 24개 재생성 SHA256 일치, 스프라이트 23개 크기 및 임포터 설정 통과.
- `production/qa/evidence/story-008-art-set-korean-base/contact_sheet.png`: 전체 미리보기.
- `production/qa/evidence/story-008-art-set-korean-base/01-korean-art.png`: 편집기 Play 캡처.
- `production/qa/evidence/story-008-art-set-korean-base/02-korean-art-player.png`: 1280×720 플레이어 캡처. 직접 열어 한국어와 전체 시트 표시 확인.
- `production/qa/evidence/story-008-art-set-korean-base/verification.md`: 실행 절차와 결과, 임시 시각 기본값, 종료 로그 경고 기록.
- `production/qa/evidence/story-008-art-set-korean-base/source-stamp.txt`: 최종 소스와 애셋 SHA256.

Run result: OBSERVED — 빌드 플레이어에서 한글 깨짐 없이 23개 아트와 한국어 문구 표시 확인.

---

## Dependencies

- Depends on: None (표현 계층의 기반)
- Unlocks: `story-009-topdown-main-scene.md`, `story-010-state-and-combat-fx.md`, `story-011-korean-hud-mouse.md`
