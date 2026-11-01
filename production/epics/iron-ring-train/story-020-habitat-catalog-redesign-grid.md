# Story 020: 서식지 카탈로그 재설계(혼합 묶음·크기 다양화)와 설치칸 확장

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3 — 같은 타일 묶음 4종·혼합 묶음 9종·패턴형 6종, 크기 2~9칸; 거점 내부 4×12칸. 사용자 지시(2026-11-01 19:10): "너무 같은 거 4개만 있잖아", "바닥에 보이는 칸은 다 놓을 수 있게".

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 설치칸 = 바닥: `HubInterior.json`을 위·아래 각 4행 12열(총 96칸)로 바꾸고 `InteriorCatalog.asset`의 바닥 여백(`_floorMarginColumns/_floorMarginRows`)을 0으로 둔다. `Story012Build.Generate`가 그리드 크기가 다른 기존 `HubInterior` 인스턴스를 새 프리팹으로 교체하고(같으면 그대로), 씬을 저장한다. 내부 카메라 크기는 96칸이 한 화면에 들어오게 데이터로 맞춘다.
- [ ] 새 종류 `set`(혼합 묶음): `_tiles: [{ "_tileItemId", "_minCount" }]`와 `_requiredCount`(총 칸 수). 집합에 속한 타일이 놓인 칸들이 상하좌우로 이어진 묶음에서 총 칸 수가 `_requiredCount` 이상이고 타일별 최소 수를 모두 채우면 완성. 모양 무관, 선로 건너편 미연결, 한 칸은 같은 종류의 두 서식지에 중복 계산되지 않음, 타일이 빠져 조건이 깨지면 `HabitatBroken`. cluster·pattern 규칙은 그대로.
- [ ] 카탈로그 19종(`HabitatTypes.json`, NPC id·한국어 이름 유지):
  - 같은 타일 묶음 4: 가스등 거리(가스등×3, +4) · 석탄 야적장(석탄더미×4, +5) · 벽돌 광장(벽돌길×6, +7) · 대공방(톱니장치×9, +10)
  - 혼합 묶음 9: 보일러실(보일러≥2·석탄더미≥1, 총4, +5) · 배관 회랑(증기관≥3·톱니장치≥1, 총4, +5) · 톱니공방(톱니장치≥2·작업대≥2, 총5, +6) · 철골 창고(철골≥3·굴뚝≥1, 총5, +6) · 저수조(물탱크≥2·증기관≥1, 총4, +5) · 온실(온실유리≥3·물탱크≥1, 총5, +6) · 막사촌(침상≥3·작업대≥1, 총5, +6) · 공구상(작업대≥2·톱니장치≥1·철골≥1, 총4, +5) · 전신선(전신주≥2·가스등≥1, 총3, +4)
  - 패턴 6: 급수탑(물탱크 위 철골, 세로 2칸, +6) · 가로등길(가스등-벽돌길-가스등, +6) · 증기 기관실(보일러-증기관-톱니장치, +7) · 발전소(보일러-톱니장치-전신주, +9) · 정비창(2×2: 철골 철골 / 작업대 작업대, +7) · 용광로(3×3 철골 둘레 + 가운데 굴뚝, +12)
- [ ] 데모 데이터(`HabitatDemo.json`, `NpcHomesDemo.json`, 핫바 배치 데모)를 새 레시피로 고친다. `--habitat-demo`는 한 거점에서 같은 타일 묶음·혼합 묶음·패턴 한 종류씩 세 개를 완성하고 NPC를 합류시킨다. `--npc-demo`는 두 NPC가 각자 집을 받는다(017 캡처와 같은 구성).
- [ ] 번영(014)·NPC 합류(003/016)·완성 토스트·묶음 테두리(015)·대화(017)는 새 종류에서도 그대로 동작한다. `ko.json`의 이름·완성 문구를 맞춘다.
- [ ] 스크린샷: 4×12 그리드에 세 종류가 완성된 내부 화면(`--habitat-demo`), 혼합 묶음이 최소 수를 못 채워 미완성인 화면(한 장, 데모 단계 플래그로).

---

## Implementation Notes

- 로직은 `Assets/Scripts/Habitats/`(HabitatCatalog·HabitatMatcher·ClusterHabitatSystem). set 판정은 cluster의 flood fill을 "타일 id가 집합에 속함"으로 일반화하고 묶음 안의 타일별 개수를 센다. 겹침 규칙은 cluster와 같다.
- 데모 placement 좌표(`_upper/_row/_column`)는 행·열이라 그리드가 커져도 유효하다. 3행 6열을 넘는 새 배치도 넣어 넓어진 칸을 쓴다.
- 프리팹 교체: `Story012Build.Generate`에서 인스턴스의 칸 수(`InteriorCell` 자식 수)와 데이터 행×열×2를 비교해 다르면 그 인스턴스를 지우고 새로 놓는다(위치·이름 유지). `BuildTopDownMain.Rebuild`는 쓰지 않는다(MainUI 등 다른 루트를 지움).
- 이 스토리는 스프라이트 파일을 손대지 않는다(019 담당). `InteriorCatalog.asset`은 여백·카메라 값만 바꾼다.
- 검증은 열린 편집기 브리지(`refresh` → `execute-method IronRing.Presentation.Interior.Editor.Story012Build.Generate` → `scene-status` isDirty=false). 캡처는 코디네이터.

---

## Out of Scope

- 019: 아트. 021: 레시피 해금·도감. 시설 종류 추가, 타일 추가.

---

## Files

- `Assets/Scripts/Habitats/HabitatCatalog.cs`·`HabitatMatcher.cs`·`ClusterHabitatSystem.cs`, `Resources/Habitats/HabitatTypes.json`·`HabitatDemo.json`, `Assets/Scripts/Npc/Resources/Npc/NpcHomesDemo.json`, `Assets/Scripts/Hud/Hotbar/PlacementDemoDriver.cs`(배치 좌표만)
- `Assets/Scripts/Interior/Resources/Interior/HubInterior.json`, `Assets/Prefabs/Interior/InteriorCatalog.asset`(여백·카메라 크기), `Assets/Scripts/Presentation/Interior/Editor/Story012Build.cs`, `Assets/Prefabs/Interior/HubInterior.prefab`, `Assets/Scenes/Main.unity`(인스턴스 교체)
- `Assets/Scripts/Localization/Resources/ko.json`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷: `production/qa/evidence/story-020-habitat-catalog-redesign-grid/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-016-habitat-types-clusters.md`, `story-017-npc-homes-dialogue.md`
- Unlocks: `story-021-recipe-unlock-codex.md`
