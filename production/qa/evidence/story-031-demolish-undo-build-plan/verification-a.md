# 스토리 031 A — 부수기·되돌리기 검증 메모

## 동작
- **철거**: 핫바 선택·드래그가 없을 때 우클릭한 칸이 완성 서식지 묶음(`IHabitatClusters.CompletedAt`)에 속하면 `PlacementGhost.TryDemolish`가 묶음의 모든 ItemPlaced 칸을 `IItemPlacement.TryPickUp`으로 회수한다. 서식지가 깨지는 것은 기존 경로(`ClusterHabitatSystem` → `HabitatBroken`/`HabitatChanged`)라 `INpcHomes`가 집을 거두고 NPC는 029의 열차 옆 자리로 간다. 로그 `DEMOLISH: <type> <hub> cells <칸=아이템,…> (묶음 n칸)`. 토스트 "<종류 이름> 철거"(`placement_demolished`). 건물이 아닌 칸의 우클릭은 기존 한 칸 회수 그대로.
- **무너짐 연출**: `BuildingView.Sync`에서 사라진 건물의 칸을 새 묶음이 이어받지 않으면(철거·깨짐) 바로 지우지 않고 `_buildingCollapseSeconds`(0.25) 동안 `_buildingCollapseDistance`(0.3)만큼 ease-in으로 내려앉으며 모든 SpriteRenderer 알파를 0으로 줄인 뒤 지운다. 시작 즉시 Collider2D를 꺼 클릭이 닿지 않는다. 묶음이 커지거나 줄어 다른 건물로 바뀌는 경우는 이전처럼 바로 교체한다. 로그 `BUILDING COLLAPSE: …`.
- **되돌리기**: `PlacementHistory`(일반 C# 클래스, PlacementGhost가 소유)가 고스트 경로의 아이템 배치·회수·철거를 (칸, 아이템) 목록으로 기록한다(최대 `_undoDepth` 5). Ctrl+Z(`Keyboard.current.ctrlKey.isPressed && zKey.wasPressedThisFrame`)는 최근 기록의 역동작을 `IItemPlacement`로 보낸다(배치→회수, 회수→다시 놓기, 철거→모든 타일 다시 놓기). 서식지·건물·번영·NPC 집은 일반 배치와 같이 재계산된다. 칸 상태가 기록과 달라진 칸은 건너뛴다. 역동작은 기록하지 않는다(다시 하기 없음). 내부 화면이 닫히면(`IViewMode.InteriorChanged(false)`, 출발 포함) 기록을 비운다. 토스트 "되돌림: 배치 가스등" / "되돌림: 회수 …" / "되돌림: 가스등 거리 철거"(`_undoToast`로 끌 수 있음). 로그 `UNDO: <Kind> <hub> <type> 칸 … 적용 a/n 남은 기록 k`.
- **안내줄**: `hotbar_hint` = "1~9 선택 · 좌클릭 배치 · 끌어서 놓기 · 우클릭 회수/철거 · Ctrl+Z 되돌리기 · Esc 취소".

## 파일
- 새: `Assets/Scripts/Presentation/Interior/PlacementHistory.cs` (`PlacementActionKind`, `PlacementAction`, `PlacementHistory`)
- `Assets/Scripts/Presentation/Interior/PlacementGhost.cs` — `BuildingAt`, `TryDemolish`, `TryUndo`, `Demolished`/`Undone` 이벤트, `UndoToastEnabled`, `UndoCount`, 우클릭 분기, Ctrl+Z, 닫힘 때 기록 비움
- `Assets/Scripts/Presentation/Interior/BuildingView.cs` — 무너짐 연출(`Collapse`, `StartCollapse`, `AnimateCollapses`)
- `Assets/Scripts/Presentation/Interior/InteriorCatalog.cs` + `Assets/Prefabs/Interior/InteriorCatalog.asset` — `_buildingCollapseSeconds` 0.25, `_buildingCollapseDistance` 0.3, `_undoDepth` 5, `_undoToast` true
- `Assets/Scripts/Hud/Hotbar/HotbarScreen.cs` — 철거·되돌림 토스트
- `Assets/Scripts/Hud/Hotbar/PlacementDemoDriver.cs`, `HotbarData.cs`, `Resources/Hotbar/Hotbar.json` — demolish/undo 데모 단계, `_demolishDelayFrames` 40
- `Assets/Scripts/Localization/Resources/ko.json` — `hotbar_hint` 변경, `placement_demolished`, `placement_undone`

## 브리지 결과
- `refresh`: 편집기가 플레이 모드라 15:11~15:22 UTC 사이 다섯 번 모두 "Command requires Edit mode"로 거부. `scene-status`: Main.unity isDirty=false, playing=true. 플레이 모드를 강제로 끄지 않았다.
- `compile-status`(15:15, 15:17 UTC): compiling=false, failed=false, errors 없음. `PlacementHistory.cs.meta`는 편집기가 이미 만들었다.
- 대신 편집기가 만든 `Assembly-CSharp.csproj`의 소스 127개(+ PlacementHistory.cs)·참조 364개·정의 기호로 Roslyn csc 오프라인 컴파일: **오류 0**. 편집기 모드로 돌아온 뒤 `refresh`/`compile-status`를 다시 돌려야 한다(PlacementHistory.cs.meta도 그때 생성).

## 캡처 플래그(코디네이터)
- 철거: `--placement-demo --placement-stage demolish` — hub-1 내부가 열리면 L자 4칸에 가스등(가스등 거리 완성) → 40프레임 뒤 `TryDemolish`. 로그 `PLACEMENT DEMO demolish: 건물 lamp_street …`, `DEMOLISH: lamp_street …`, `BUILDING COLLAPSE: …`. 무너지는 중을 찍으려면 철거 로그 직후 0.25초 안, 아니면 직후(빈 칸 + "가스등 거리 철거" 토스트).
- 되돌리기: `--placement-demo --placement-stage undo` — L자 앞 두 칸 배치 후 `TryUndo` 1회 → 두 번째 칸이 회수, 로그 `UNDO: Place …`, `PLACEMENT DEMO undo: 되돌림 True, 남은 기록 1`, 토스트 "되돌림: 배치 가스등".

## 남은 문제
- 편집기 브리지 컴파일 확인 미완(플레이 모드).
- 시설(벽·포탑) 건설은 IItemPlacement 경로가 아니라 기록하지 않는다(되돌릴 역 API가 없음).
- 다시 하기(Ctrl+Y)는 범위 밖.
- 철거 중 묶음이 줄어드는 동안 `HabitatChanged`가 한두 번 날 수 있다(개수 기반 판정이라 "완성" 토스트는 나지 않음). 같은 프레임 안이라 화면에는 무너짐 하나만 보인다.
- 스크린샷 미촬영(코디네이터 몫).
