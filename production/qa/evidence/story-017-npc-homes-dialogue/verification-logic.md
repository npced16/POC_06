# Story 017 로직 검증 기록

- 날짜: 2026-11-01
- 범위: NPC 집 배정, 대사·상호작용 로직, 설치자, `--npc-demo` (표현·HUD는 별도)
- 컴파일: `python -I tools/bridge/bridge.py refresh --timeout 400` 결과 `failed:false`, 오류 없음
- 캡처: 코디네이터가 맡음 (이 기록에는 없음)

## 코드

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Npc/NpcHomes.cs` | `INpcHomes`. 합류할 때 같은 종류의 완성 묶음을 집으로 잡는다(빈 묶음 우선). 깨지면 빈 칸 목록과 `HomeChanged`를 낸다. 다시 완성되면 마지막 집과 가장 많이 겹치는 묶음을 되찾는다. 시스템보다 먼저 합류한 NPC도 생성할 때 기록한다. `IHabitatClusters`는 첫 사용 때 늦게 얻는다. |
| `Assets/Scripts/Npc/NpcInteraction.cs` | `INpcInteraction`. 직업명, 대사 순환, 데이터 선택지를 다룬다. 격려는 `IHubStateSystem.ApplyDelta(거점, +N, "npc-encourage:<id>")`를 부르고 NPC마다 정차당 한 번이다. 출발 이벤트가 계약에 없어 `ITrackRuntime.Arrived`에서 초기화한다. |
| `Assets/Scripts/Npc/NpcInstaller.cs` | `GameServices.Composing`에서 등록하고 Ready에서 한 번 더 Bind한다. 정적 상태는 SubsystemRegistration에서 초기화한다. |
| `Assets/Scripts/Npc/NpcDataRefs.cs`, `Resources/Npc/NpcData.asset` | Resources 밖에 있는 JSON 두 개를 참조한다. 에디터에서 에셋이 없으면 경로로 대신 읽는다. |
| `Assets/Scripts/Npc/Demo/NpcHomesDemoDriver.cs` | `--npc-demo [--npc-stage homes\|panel\|encouraged]` 데모 |

## 데이터

- `Assets/Scripts/Npc/Data/NpcPool.json`: 19명 모두에 `_lines` 2줄을 더했다. `_quote`가 첫 줄이라 NPC마다 대사가 3줄이다.
- `Assets/Scripts/Npc/Data/NpcInteraction.json`: `talk`(대화)와 `encourage`(격려, `_prosperityDelta` 3, `_oncePerDocking` true) 선택지, 결과 문장 `격려했다. 번영 +{0}`, 비활성 이유 `이번 정차에는 이미 격려했다`
- `Assets/Scripts/Npc/Resources/Npc/NpcHomesDemo.json`: hub-1, 대기 20프레임, 최대 방문 24회, 대상 lamplighter·gardener, 배치는 가스등 L자 (1,1)(2,1)(2,2)(2,3) 위 구역과 유리 일자 0행 1~4열 아래 구역

## 데모 로그 (예상 형식)

```
NPC DEMO: 단계 <stage>
NPC DEMO: 배치 tile_gaslamp → 위(1,1) 칸 <i> = True   (8줄)
NPC DEMO hub-1: 방문 <n>회, 합류 <ids>
NPC DEMO: homes → lamplighter:lamp_street[<cells>]
NPC DEMO: homes → gardener:greenhouse[<cells>]
NPC DEMO: 첫 NPC <id> '<직업명>' 대사 "<첫 줄>"
NPC DEMO: 격려 <id> = True "격려했다. 번영 +3" 번영 a → a+3, 버튼 활성=False 이유="이번 정차에는 이미 격려했다"   (encouraged 단계만)
```

## HUD 연결점

- `NpcHomesDemoDriver.Stage`: 데모 단계 문자열. 플래그가 없으면 null이다.
- `NpcHomesDemoDriver.StageReady`: `event Action<string>`로 첫 NPC id를 넘긴다. 이미 끝났으면 구독 즉시 호출된다.
- `NpcHomesDemoDriver.FirstNpcId`, `NpcHomesDemoDriver.IsStageReady`
