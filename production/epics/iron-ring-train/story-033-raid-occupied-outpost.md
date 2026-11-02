# Story 033: 습격 — 몬스터에게 점령된 종점을 뚫고 지나가기

> **상태 메모(2026-11-02 정리)**: 제거/보관 — 습격은 039 전투 제거로 빠짐(코드 archive/combat-2026-11-02/). 현재 규칙은 design/game-brief.md §Out of scope 와 story-039 참조.

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Removed(039) — 원래: Done (2026-11-02 01:17 KST 코디네이터 — 컴파일 통과, RaidBuild/BuildMainCanvas/SpriteGenRaid 실행, 캡처 4장; 구도(전선·돌진) 수정은 034에서)
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 2(경유지 이벤트·전투), MVP 5(방어시설). 사용자 지시(2026-11-02 00:40): "습격 막아내는 게 좀 재밌었으면 좋겠는데, 몬스터로 점령된 거점 지나가는 느낌으로." 지금 전투는 "다음 라운드" 버튼과 주사위(열차 10 대 적 8~14, 3판 2선승)뿐이라 보이는 것도 할 것도 없다. 종점을 몬스터 둥지가 있는 점령된 거점으로 만들고, 열차가 그 안을 통과하는 동안 실시간으로 막아내게 한다.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: HIGH
**Engine Notes**: 2D URP, 새 Input System, UGUI.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 점령된 종점: 종점 노드 자리에 폐허 거점 그림(기존 `hub_*_ruined` 계열)과 몬스터 둥지 3개(데이터, 시드)가 보인다. 둥지는 파괴되면 그 런 동안 사라지며(진척), 둥지가 다 사라지면 종점이 "해방"되어 그 뒤로는 습격이 없고 승객 탑승 확률이 오른다(데이터). 선로 화면에 둥지 수 라벨("둥지 3").
- [ ] 습격 장면: 열차가 종점에 들어서면 선로 화면 카메라가 종점을 확대(크기 데이터)하고, 열차가 종점 구간을 천천히 통과(구간 길이·속도 데이터, 약 12초)하는 동안 둥지에서 몬스터(2종: 증기 거미(빠름·약함), 녹슨 자동인형(느림·단단함), 바퀴 수에 따라 수·종류 증가, 데이터 테이블)가 나와 열차로 달려온다. 열차에 닿으면 열차 내구도(방벽 수에 비례, 데이터)를 깎고 사라진다.
- [ ] 플레이어 조작: 몬스터를 좌클릭하면 열차 대포가 쏜다(재장전 0.4초, 피해 데이터). 역에 지은 포탑 수만큼 자동 사격(초당 발수 데이터)이 가장 가까운 몬스터를 노린다. 방벽은 내구도를 늘린다. 둥지도 클릭해 부술 수 있다(체력 높음).
- [ ] 결과: 통과 동안 내구도가 0이 되면 패배(기존 패배 결과: 다음 거점 번영 −10, 승객 1명 이탈(데이터)); 통과를 마치면 승리(기존 승리: 번영 +5). 결과는 기존 `IWaypointResolver`의 Victory/Defeat 신호로 이어져 해금·승객·번영이 그대로 동작한다. 주사위 전투와 "다음 라운드" 버튼은 데이터 플래그로 끈다(코드 보존).
- [ ] 연출·HUD: 열차 내구도 바와 "둥지 N · 몬스터 N" 표시(전투 중에만), 사격 궤적·명중 번쩍임·몬스터 사망 연기, 시작 토스트 "점령된 종점을 통과합니다 — 클릭으로 사격", 끝 토스트. 전투 중에는 배치·대화·도감 입력을 막는다.
- [ ] 데모 `--raid-demo --raid-stage start|mid|win|lose`(자동 사격만으로 결과를 내는 시뮬레이션 포함). 스크린샷 4장: 점령된 종점 접근, 통과 중 사격, 승리 토스트, 패배. `production/qa/evidence/story-033-raid-occupied-outpost/` + verification.md.

---

## Implementation Notes

- 로직: `Assets/Scripts/Combat/Raid/`(RaidSystem: 둥지·몬스터 스폰·이동·피해·내구도, 결정적 시드, `IRaid` 계약(상태·이벤트) 추가), `WaypointResolver`와 연결(전투 조우가 시작되면 RaidSystem이 결과를 결정하고 `EncounterEnded(Victory|Defeat)`를 낸다), `Story005Encounters.json`에 `_raidMode:true`, 몬스터·둥지 데이터 `Raid.json`.
- 표현: `Assets/Scripts/Presentation/Raid/`(RaidView: 둥지·몬스터 프리팹 인스턴스, 클릭 콜라이더, 사격선·히트 이펙트, 카메라 줌), 스프라이트 생성기 `SpriteGenRaid`(몬스터 2종 2프레임, 둥지, 포탄, 폐허 종점 장식), 프리팹 `Assets/Prefabs/Raid/*`.
- HUD: `RaidScreen`(내구도 바·카운트, Simple 스킨). 포탑·방벽은 기존 `IFacilityBuilder` 수량을 읽는다.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 몬스터 종류 3종 이상, 보스, 열차 업그레이드, 사운드.

---

## Files

- `Assets/Scripts/Contracts/IronRingContracts.cs`(IRaid 추가), `Assets/Scripts/Combat/Raid/*.cs`·`Resources/Raid.json`, `Assets/Scripts/Combat/WaypointResolver.cs`·`Resources/Story005Encounters.json`, `Assets/Scripts/Presentation/Raid/*.cs`, `Assets/Scripts/Art/Editor/SpriteGenRaid.cs`, `Assets/Art/Generated/raid/**`, `Assets/Prefabs/Raid/**`, `Assets/Scripts/Hud/RaidScreen.cs` + 빌더, `Assets/Scripts/Presentation/Map/MapView.cs`(카메라 줌·종점 장식), `ko.json`, `design/game-brief.md`(MVP 2 문장)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 4장: `production/qa/evidence/story-033-raid-occupied-outpost/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-032-poc-one-station-one-terminal.md`, `story-005-waypoint-event-combat.md`, `story-006-defense-facilities.md`
- Unlocks: None
