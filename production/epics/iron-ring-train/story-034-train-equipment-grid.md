# Story 034: 열차 강화 — 칸마다 3×2 장비 그리드(포탑·방벽판)와 습격 연동

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-02 02:05 KST 코디네이터 — 컴파일 통과, TrainEquipmentBuild 멱등, 캡처 3장; 장비 모드 카메라 확대 후속)
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 5(방어시설) 재정의. 사용자 지시(2026-11-02 01:00): "첫 거점에서는 내 열차 포탑 디펜스를 그리드로 할 수 있도록, 각 칸은 3×2. 열차가 다음 역에서 몰려오는 몹들을 잡는 게임으로, 열차 강화와 거점 강화를 반복." 거점 시설(방벽·포탑·공방)은 열차 장비로 옮긴다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: none.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

**Control Manifest Rules (this layer)**: N/A

---

## Acceptance Criteria

- [ ] 열차 장비 그리드: 거점 내부 화면에서 선로 위 열차 칸 3량마다 3×2(가로 3·세로 2) 장비 칸이 있다. 핫바의 "열차" 탭(T 키) 또는 열차 칸 클릭으로 장비 모드가 되면 각 열차 칸 위에 3×2 그리드가 펼쳐지고(프리팹 `TrainCarGrid`), 장비를 서식지 타일처럼 드래그/핫바로 놓고 우클릭으로 뗀다. 장비 종류(데이터 `TrainEquipment.json`): 포탑(자동 사격, 초당 0.8발, 피해 1), 중포(초당 0.3발, 피해 3, 2칸 차지 가로), 방벽판(내구도 +4), 증기 부스터(통과 속도 +15%), 수리 장치(통과 중 초당 내구도 +0.2). 칸 수가 상한(18)이라 무제한 타일과 달리 선택이 중요하다. 기존 거점 시설(방벽·포탑·공방)은 핫바·팔레트에서 빼고 코드는 보존.
- [ ] 습격 연동: `IRaidLoadout`(033)의 제공자를 열차 장비로 교체 — 포탑·중포 수, 방벽판 수(내구도), 부스터(속도), 수리 장치. 습격 화면에서 열차 칸 위에 실제 장비가 보이고 포탑이 쏜다(표현은 033의 RaidView에 장비 스프라이트 얹기).
- [ ] 루프: 역에서 서식지 건물 + 열차 장비를 고치고 → 출발 → 종점 습격(몬스터 수·종류는 바퀴마다 증가) → 귀환 → 반복. 바퀴마다 몬스터가 세지니 장비를 늘려야 한다(데이터 곡선, 8바퀴 안에 둥지 3개를 다 부수면 해방).
- [ ] 결과 화면에 "열차 장비 N칸", 승객 화면은 그대로. 열차 장비 칸은 되돌리기(031-A)에도 포함.
- [ ] 스크린샷: (1) 열차 장비 모드(3×2 그리드 3량), (2) 포탑·방벽판이 놓인 열차, (3) 습격 중 열차 포탑 사격, (4) 데이터 곡선 표(verification). `production/qa/evidence/story-034-train-equipment-grid/` + verification.md.

---

## Implementation Notes

- 로직 `Assets/Scripts/Train/Equipment/`(TrainLoadout: 칸·장비 상태, `ITrainLoadout` 계약 + `IRaidLoadout` 구현, 데이터), 표현 `Assets/Scripts/Presentation/Interior/TrainEquipmentView.cs`(열차 칸 위 그리드·장비 스프라이트, 핫바 연동), 아이템 카탈로그에 장비 category "equipment"(스프라이트는 기존 wall/turret 재활용 + 중포·방벽판·부스터·수리 새로 생성 `SpriteGenEquipment`), 핫바 탭(타일/열차).
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 열차 칸 추가/구매, 장비 업그레이드 단계, 비용 자원.

---

## Files

- `Assets/Scripts/Contracts/IronRingContracts.cs`(ITrainLoadout), `Assets/Scripts/Train/Equipment/*.cs`·`Resources/Train/TrainEquipment.json`, `Assets/Scripts/Presentation/Interior/TrainEquipmentView.cs`·`Editor/Story012Build.cs`(TrainCarGrid 프리팹), `Assets/Prefabs/Interior/TrainCarGrid.prefab`·`EquipmentPiece.prefab`, `Assets/Scripts/Art/Editor/SpriteGenEquipment.cs`, `Assets/Scripts/Items/ItemCatalog.json`(장비 항목), `Assets/Scripts/Hud/Hotbar/HotbarScreen.cs`·`Hotbar.json`(탭), `Assets/Scripts/Presentation/Raid/RaidView.cs`(장비 표시), `Assets/Scripts/Combat/Raid/*`(IRaidLoadout 제공자 교체), `ko.json`, `design/game-brief.md`(MVP 5)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 3장: `production/qa/evidence/story-034-train-equipment-grid/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-033-raid-occupied-outpost.md`
- Unlocks: None
