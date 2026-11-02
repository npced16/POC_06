# 혼합 묶음 번영 상한 — 검증 기록

- 출처: QA 메모 2026-11-01 "혼합 묶음 번영 상한"
- 날짜: 2026-11-02
- 범위: 로직·데이터 변경(플레이 화면 변화 없음, play-capture 생략 — 지시에 따름). 테스트·커밋 없음.

## 문제
스토리 035로 완성 시점의 칸이 고정되지만, 완성 전에 Set/Cluster 건물을 레시피보다 훨씬 많은 칸으로 키운 뒤 완성하면
칸마다 랩당 +1(`_perHabitatCell`) 번영이 붙어 종류 보너스보다 커진다.

## 공식
건물(완성 서식지 묶음) 하나가 번영에 세는 칸 수:

- Cluster / Set: `counted = min(cells, ceil(RequiredCount × _cellCapRatio))`
- Pattern: `counted = min(cells, RequiredCount)` (템플릿 칸 수 그대로)
- 카탈로그에 없는 종류: 상한 없음

랩당 Δ의 서식지 칸 항 = `_perHabitatCell × Σ counted`. 종류 보너스(카탈로그 `_prosperityBonus`, 없으면 `_habitatBonus`)는 그대로.

예: RequiredCount 4, 비율 1.5 → 상한 6칸. 10칸으로 완성된 건물은 6칸만 센다. 6칸(보일러 5 + 석탄 1) 건물은 상한과 같아 그대로 6.

## 데이터 키
- `Assets/Scripts/Hub/Data/HubProfiles.json` 최상위 `"_cellCapRatio": 1.5`
- 키가 없거나 0 이하이면 1.5로 처리(`HubData` 필드 기본값 + `HubRunRules` 가드).

## 로그
상한이 적용된 건물마다 1회(거점·종류·첫 칸·칸 수 기준 중복 제거):
`PROSPERITY: cap <typeId> cells=<n> counted=<m>`

## 변경 파일
- `Assets/Scripts/Hub/Data/HubProfiles.json` — `_cellCapRatio` 추가
- `Assets/Scripts/Hub/HubData.cs` — `_cellCapRatio` 파싱(기본 1.5) → `HubRunRules`
- `Assets/Scripts/Hub/HubDefinition.cs` — `HubRunRules.CellCapRatio`(선택 인자, 기본 1.5)
- `Assets/Scripts/Hub/RunState.cs` — `CellCapRatio` 읽기 전용 속성
- `Assets/Scripts/Hub/IProsperitySources.cs` — `ServiceProsperitySources`에 선택 인자 `cellCap`; `HabitatCellCount`가 묶음별 상한 적용·1회 로그
- `Assets/Scripts/Hub/HubDevelopmentInstaller.cs` — 카탈로그 `Kind`/`RequiredCount`와 비율로 `CellCapOf` 구성

기존 호출부(데모 등)는 새 인자가 모두 선택 인자라 수정 없이 컴파일된다.

## 브리지 결과
- `python -I tools/bridge/bridge.py --timeout 600 refresh` → `{"compiling":false,"errors":"","failed":false}`
- `scene-status` → `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}`
