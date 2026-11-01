# Story 014 검증 기록 — 거점 단계와 새 패배 규칙

날짜: 2026-11-01 · 엔진: Unity 6000.3.27f1 (열린 에디터, 브리지로만 검증)

## 컴파일

- `python -I tools/bridge/bridge.py refresh --timeout 400` → `failed=false`, 오류 없음

## EditMode 테스트

| 필터 | 결과 | 파일 |
|---|---|---|
| `IronRing.Hub.Tests` | 28/28 통과 | `editmode.xml` |
| `IronRing.Integration.Tests` | 3/3 통과 | `editmode-integration.xml` |
| `IronRing` (전체) | 154/154 통과 | `editmode-all.xml` |

검증 범위: 시작 단계(전 거점 Ruined·번영 0), 단계 전이표(Ruined→Recovering→Settled→Collapsed, Recovering→Ruined), 정착 후 임계값 아래 유지, 서식지 완성 정착, Ruined는 감소하지 않음, 바퀴 규칙(아이템 +1·서식지 +5·시설 +2·NPC +3·기본 -3, 004 배수 유지), 기한 감소와 기한 패배, 같은 바퀴에서 정착을 기한보다 먼저 판정, 정착했던 거점 전부 붕괴 시 패배, 이벤트 순서, 부지 칸 건설 래퍼, 실제 시스템 통합 흐름(폐허 시작 → 서식지 → 정착 → NPC → 건설 → 붕괴 패배, 그리고 8바퀴 기한 패배).

## 스크린샷 3장 (코디네이터가 캡처)

데모 시간 값은 `Assets/Scripts/Hub/Demo/Resources/HubPhase/hub_phase_demo.json`에서 오며, 모두 실제 시간(timeScale 무관)이다. `--extra-args` 값은 `--`로 시작하므로 반드시 `=` 형식으로 넘긴다.

> 2026-11-01 재수정: 이전 01/02 캡처는 무효다. 당시 데모는 013 태그 서식지를 완성하지 못해 NPC가 합류하지 않았고, 부지를 찾지 못해 시설을 거점 단위로 지었으며, 그 결과 8바퀴 기한 패배로 끝났다. 아래 인자로 다시 캡처한다.

1. 복구 중 거점 내부(NPC·시설 보임) — 시작 1초 뒤 아래 순서로 진행한 뒤 6초 동안 유지한다.
   - hub-1에 번영 20을 줘 Recovering으로 만든다(데모 시드).
   - 잔해를 조사한다. 정차당 횟수가 다 떨어지면 `OnDocked`로 다시 채운다. 보장 부지 1칸과 빈 터 6칸이 드러날 때까지 계속한다.
   - 013 `IItemPlacement`로 화로·화로·톱니 장식을 놓아 열 2·산업 1을 채운다. 태그 서식지가 완성되고, 없는 아이템은 데모용으로 지급하며 로그를 남긴다.
   - 실제 `OnHubVisited`로 NPC 1명을 합류시킨다(최대 20회).
   - 부지에 `wall`을 짓는다. 012 칸이 FacilityBuilt로 바뀐다.
   - `IInteriorNavigation.Open`으로 내부 화면을 연다.

```
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --seconds 4 --extra-args=--phase-demo --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-014-hub-phases-defeat/01-recovering-interior.png --timeout 300
```

2. 정착 표시 — 유지가 끝나는 약 7초에 첫 바퀴를 진행한다. 서식지 완성(+5)으로 곧바로 Settled가 되어 배너에 "주조소 · 정착"이 보이고, 정착했으므로 기한 표시는 사라진다.

```
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --seconds 14 --extra-args=--phase-demo --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-014-hub-phases-defeat/02-settled.png --timeout 300
```

3. 패배 화면(기한 만료) — 시작 1초 뒤부터 0.5초마다 바퀴를 진행하면 약 5초에 기한 패배가 난다. 011 패배 화면 제목이 "기한 안에 정착한 거점이 없습니다"(`IRunLossInfo` = DeadlineExpired)로 바뀌고, 010의 겹치는 패배 오버레이는 나오지 않는다.

```
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --seconds 8 --extra-args=--deadline-demo --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-014-hub-phases-defeat/03-deadline-defeat.png --timeout 300
```

콘솔 확인 줄:
- `PHASE-DEMO recovering: phase=Recovering, prosperity=20, habitat=True, npcs=1, facilities=1`
- `PHASE-DEMO settled: phase=Settled ... lost=False`
- `DEADLINE-DEMO: lost=True, reason=DeadlineExpired`

"시설 부지를 찾지 못해"나 "아이템 배치 실패" 경고가 보이면 데모 전제가 깨진 것이다.

## 프리팹(런타임 레이아웃 금지 규칙)

- NPC와 시설 조각은 `Assets/Prefabs/HubPhase/HubPhaseNpc.prefab`, `HubPhaseFacility_{wall,turret,workshop}.prefab`이다. `Resources/HubPhase/HubPhasePrefabCatalog.asset`이 이들을 참조한다.
- 생성은 `execute-method --method IronRing.Presentation.HubPhase.Editor.HubPhasePrefabBuild.Generate`로 하며 다시 실행할 수 있다(실행 완료).
- HUD의 거점 라벨과 건설 버튼은 `PhaseMapLabel.uxml`, `PhaseBuildOption.uxml` 템플릿을 인스턴스화한다.

## 확인할 점과 한계

- 내부 화면 표시(NPC·시설)는 012의 `IViewMode`·`IInteriorAnchors`·`IHubInterior`가 등록되어 있어야 보인다. 없으면 각 뷰가 로그 한 줄을 남기고 건너뛴다. 데모는 `IInteriorNavigation.Open(hubId)`로 내부 화면을 열며, 미등록이면 경고 한 줄을 남기고 건너뛴다.
- 부지 칸 건설: `IFacilityBuilder.TryBuild` 성공 후 `IInteriorFacilityCells.TryBuildFacility`를 불러 012의 칸을 FacilityBuilt로 바꾼다. 서비스가 없거나 거절하면 `IInteriorFacilityPlots`의 자체 기록으로 대신 표시한다(테스트 2건으로 검증).
- 부지가 드러나지 않으면 데모는 거점 단위로 건설하고 경고를 남긴다(내부에는 시설이 보이지 않음).
- 설치자는 `MainRoot.RaiseComposing()`(012가 추가) 시점에 설치하며, Composing이 없으면 Ready에서 설치한다(중복 없음). `IHubDevelopment`, `IRunLossInfo`, `IInteriorFacilityPlots`를 등록한다.
- 허가받은 최소 수정: `KoreanHud.cs`(패배 제목을 사유별로 표시), `StateCombatEffects.cs`(`IRunLossInfo`가 있으면 010 패배 오버레이 생략).
