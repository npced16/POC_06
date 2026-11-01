# 스토리 035 검증 — 서식지 고정

## 규칙
- 서식지가 완성되는 순간의 칸 묶음(종류·칸 번호·칸마다 놓인 타일)을 거점별 고정 목록에 넣는다(`ClusterHabitatSystem.LockedCluster`).
- 이후 판정은 고정 칸을 처음부터 차지된 것으로 본다(`HabitatMatcher.Evaluate(cells, catalog, reserved)`). 옆에 같은 계열 타일을 놓아도 고정 서식지는 커지지 않고, 새 타일은 새 서식지의 시작이 된다.
- 고정은 자기 칸의 타일이 빠지거나 바뀔 때만 풀린다. 풀린 칸은 같은 판정에서 일반 칸으로 다시 본다.
- 이벤트(`HabitatCompleted`/`HabitatBroken`/`HabitatChanged`) 의미는 그대로. 데이터 `HabitatTypes.json` 루트 `_lockCompleted`(true). false면 예전처럼 전체 재판정.

## 파일
- `Assets/Scripts/Habitats/HabitatMatcher.cs`(reserved), `ClusterHabitatSystem.cs`(고정 목록), `HabitatCatalog.cs`(`LockCompleted`), `Resources/Habitats/HabitatTypes.json`, `Resources/Habitats/HabitatDemo.json`(`_lockedPlacements`), `Demo/HabitatDemoDriver.cs`(`locked` 단계), `design/game-brief.md`.

## 데모
- `--habitat-demo --habitat-stage locked`: complete 배치(가스등 3·증기 기관실 패턴·보일러실 4칸) + 옆 칸에 가스등 2개(위 3,10 / 2,10)·보일러 1·석탄 1(아래 1,10 / 2,10). 기대: 가스등 거리는 3칸 그대로, 보일러실은 4칸 그대로, 추가 타일은 어느 서식지에도 속하지 않음(로그 `HABITAT DEMO hub-1: 완성 서식지 …[칸 목록]`).

## 결과
- 2026-11-02 01:15 KST 코디네이터: 브리지 `refresh` failed=false. `035-locked.png`(--habitat-demo --habitat-stage locked 150프레임): 가스등 거리 건물 3칸·보일러실 건물 4칸 그대로, 옆에 놓은 가스등 2개·보일러·석탄은 건물 밖에 홀로 서 있음. 로그 `HABITAT DEMO hub-1: 완성 서식지 lamp_street[33,44,45], engine_room[14,15,16], boiler_room[68,69,80,81]` — 추가 타일 4칸(위 3,10·2,10 / 아래 1,10·2,10)이 어느 서식지에도 흡수되지 않음.
