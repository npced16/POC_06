# Story 038: 2차 — 중간역 정차: 탑뷰 디펜스와 자원 파밍(프리미티브), 고철로 열차 장비를 산다

> **Epic**: 철환 장갑열차 MVP (iron-ring-train)
> **Status**: Done (2026-11-02 03:50 KST 코디네이터 — 파이프라인 검증: refresh 통과, StopTopBuild 멱등, 씬 저장; 캡처 없음, 사용자 편집기 확인)
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: MVP 1·4·5. 사용자 지시(2026-11-02 02:35·02:45): "2차로 탑뷰로 만들어줘. 지나가면서 전투로 만들거나, 중간역에 정차해서 전투하고 자원 파밍하고 가게끔. 정차 시엔 탑뷰로 해서 디펜스하게." 037이 **달리면서 치르는 전투(횡스크롤)**를 맡고, 038은 **중간역 정차 = 위에서 본 디펜스 + 자원 파밍**을 맡는다. POC 선로는 역 1곳·종점 1곳이므로 종점이 그 중간역 역할(정차·파밍·회차)을 한다. 거점을 더 넣는 것은 `ring-track.json` 데이터로. 코디네이터 가정: 파밍한 자원(고철)은 열차 장비(034)의 비용으로 쓴다 — 정차 디펜스 → 고철 → 열차 강화 → 다음 정차가 더 세짐. 서식지 타일은 지금처럼 무제한.

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3.27f1 | **Risk**: MEDIUM
**Engine Notes**: 037의 프리미티브 헬퍼·카탈로그·생성기 패턴 재사용.

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 정차 화면: 열차가 중간역(POC: 종점 `TERMINAL`)에 닿으면 **탑뷰 정차 화면**(`StopTopView`, 루트 `StopTop`, y=−400 부근, 프리미티브)으로 카메라가 간다. 화면 가운데 가로 선로 위 열차 3량(장비 칸 점·장착 장비 사각형), 승강장과 역 건물 블록, 둘레에 고철 더미(사각형 더미, 데이터 개수·양), 화면 네 변 바깥에서 적이 들어오는 입구 3~4개(둥지/굴 표시). 라벨 "중간역 — 잿더미 전초".
- [ ] 디펜스(기존 `RaidSystem` arrival 모드 재사용·확장): 정차에는 정해진 끝이 없다. 시간이 갈수록 세지는 파도(`_waveEverySeconds`, 상한 데이터)가 입구에서 열차를 향해 달려온다(탑뷰 좌표: X 선로 방향, Y 옆). 포탑·중포는 자기 칸에서 가장 가까운 적을 쏘고(포탄 원, 명중 번쩍임, 잔해), 클릭 사격 가능. 적이 열차에 닿으면 내구도 감소, 0이면 패배(기존 흐름). **언제든 도망갈 수 있다**: HUD "출발 (고철 N)" 버튼으로 즉시 떠나며 창고 고철만 가져간다(인부가 운반 중인 양은 데이터 `_fleeKeepCarried`). 입구(둥지)를 다 부수면 해방. 사용자 지시(03:30): "적을 죽인다고 자원을 얻는 게 아니라 버티면서 얻고, 언제든 도망갈 수 있는 열차".
- [ ] 자원 파밍(처치 보상 없음 — 고철은 버티는 동안만 쌓인다): 고철 더미를 클릭하면 열차에서 **수집 인부**(작은 원)가 걸어가 더미를 캐서(초당 `_scrapPerSecond`) 열차로 돌아오며 고철이 오른다. 인부는 데이터 수(기본 2), 적에게 닿으면 열차로 도망(파밍 중단, 피해 없음). HUD에 "고철 N"(상단 상태줄)·"버틴 시간 N초"와 파밍 진행 표시. 버틴 시간에 비례한 보너스(`_holdBonusPerSecond`)는 떠날 때 더해진다. 떠나면 인부는 즉시 복귀하고 남은 더미는 다음 정차 때 다시 생성(데이터).
- [ ] 고철 경제: `TrainEquipment.json`에 장비별 `_cost`(포탑 3·중포 6·방벽판 2·부스터 4·수리 3, 데이터). 장비 모드에서 고철이 모자라면 배치 불가(핫바 칸에 비용 표시·붉게), 떼면 전액 환급(데이터 `_refundRatio` 1.0). 시작 고철 `_startScrap` 6. 결과 화면에 "고철 N". `IScrap` 계약(append-only): `Amount`, `TryGain/TrySpend`, `Changed`. 서식지 타일·장식은 비용 없음.
- [ ] 되돌아가기: 정차가 끝나면 037 횡스크롤로 복귀 구간을 달리고 역에 도착해 내부가 열린다. `_travelView: "map"`이면 예전 지도·습격 표현 그대로(정차 화면·파밍은 side/top 공통 — 플래그 `_stopView: "top"|"none"`).
- [ ] 검증(사용자 지시 03:30 "캡처 말고 파이프라인으로"): 브리지 컴파일 통과 + 생성기 멱등 + 씬 저장(isDirty=false) + 데이터 키 표를 `production/qa/evidence/story-038-top-down-travel-combat/verification.md`에 기록. 캡처 없음 — 사용자가 편집기에서 직접 본다. `--stop-demo` 단계는 최소로. 브리프 Core loop에 "중간역 정차: 탑뷰 디펜스·고철 파밍 → 고철로 열차 장비" 한 구절.

---

## Implementation Notes

- `Assets/Scripts/Presentation/Travel/StopTopView.cs`·`StopRaidTopView.cs`·`ScrapWorkerView.cs`·`Editor/StopTopBuild.cs`(멱등), 로직 `Assets/Scripts/Train/Scrap/`(`ScrapStore : IScrap`, `ScrapFarming`(더미·인부 상태, 순수 C#, 데이터 `Resources/Train/Scrap.json`)), `RaidSystem` 웨이브 입구 좌표·정차 시간(`Raid.json` arrival 모드 확장), `TrainLoadout.TryPlace`에 비용 검사, 핫바 장비 칸 비용 표시(`HotbarScreen`), `TopBarScreen` 고철 표시, `RunResultScreen`.
- 검증은 열린 편집기 브리지, 캡처는 코디네이터.

---

## Out of Scope

- 픽셀 스프라이트, 자원 종류 추가(고철 1종만), 서식지 타일 비용.

---

## Files

- `Assets/Scripts/Contracts/IronRingContracts.cs`(IScrap), `Assets/Scripts/Train/Scrap/**`·`Resources/Train/Scrap.json`, `Assets/Scripts/Train/Equipment/*`(비용)·`TrainEquipment.json`, `Assets/Scripts/Combat/Raid/RaidSystem.cs`·`Raid.json`(정차 웨이브), `Assets/Scripts/Presentation/Travel/StopTopView.cs`·`StopRaidTopView.cs`·`ScrapWorkerView.cs`·`Editor/StopTopBuild.cs`, `Assets/Prefabs/Travel/**`, `Assets/Scripts/Hud/Hotbar/HotbarScreen.cs`·`TopBar*`·`RunResultScreen.cs`, `MainSettings.json`(`_stopView`), `Assets/Scenes/Main.unity`, `ko.json`, `design/game-brief.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 브리지 컴파일 확인 + 스크린샷 4장: `production/qa/evidence/story-038-top-down-travel-combat/` + `verification.md`

**Status**: [ ] 미완

---

## Dependencies

- Depends on: `story-037-side-scroll-travel-combat.md`, `story-034-train-equipment-grid.md`
- Unlocks: None
