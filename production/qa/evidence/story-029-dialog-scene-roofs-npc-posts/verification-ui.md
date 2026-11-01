# 스토리 029 — 대화 장면·배경 그늘·NPC 고정 배치 (UI 쪽 검증, AC 2·3·4)

> 작성: ui-programmer, 2026-11-01. 캡처는 코디네이터 몫(아래 플래그). 이 문서는 구현과 브리지 결과만 담는다.

## 1. 대화 장면 배치 (캔버스 기준 1280×720, `NpcDialogScreenBuilder`)

| 요소 | 값 | 근거 |
|---|---|---|
| 그늘 `Dim` | 전체 화면, 검정 알파 0.35(`NpcDialogScreen._dialogDimAlpha`, 런타임에 적용), 클릭을 받음(Button, 전환·내비게이션 없음) | AC 3 |
| 대화 상자 `Dialog` | 아래 가운데 기준, 폭 896(=1280×0.7), 아래 여백 24, 높이 = max(190, 패널 여백 + Heading 줄 + 6 + Normal 줄×4 + 6 + 버튼 높이 + 패널 여백) → 현재 테마에서 **213** | AC 2 |
| 이중 테두리 | Simple 패널(`ui_panel_flat`, 자체 황동 테두리) + 안쪽 4px 들인 1px 줄 4개(`InnerLine/Top·Bottom·Left·Right`, 색 `theme.Accent`) | 테두리 전용 스프라이트가 테마에 없어 줄 이미지 4개로 만듦 |
| 초상 `Portrait` | 왼쪽 아래 기준, x=16, 높이 396(=720×0.55), 폭 297(96:128 비율). 상자 **앞**에 그려 상자 왼쪽 약 121px를 덮음 | 참고 이미지 |
| 이름 `Name` | Heading, `theme.Accent`(황동), 굵게, 맨 윗줄 | AC 2 |
| 집·출신 `Meta` | Small, 흐린 색, 이름 줄 오른쪽 정렬("집  가스등 거리  ·  북쪽 고개 출신") | 025·026 정보 유지 |
| 대사 `Line` | Normal, 왼쪽 위 정렬, 4줄 칸(넘치면 자름), 타자기 `_typeSpeed` 28자/초 | AC 2 |
| 글자 왼쪽 시작 | max(패널 여백, 초상 오른쪽 − 상자 왼쪽 + 12) = **133** | 초상과 겹치지 않게 |
| 버튼 줄 `Actions` | 오른쪽 아래, 높이 `theme.ButtonHeight`, 왼쪽으로 자람: [선택지들][닫기] 또는 [계속하기]. 버튼 폭 120 | AC 2 |
| 이유 `Reasons` | Small, 흐린 색, 버튼 줄 왼쪽(비활성 선택지 이유) | 기존 동작 유지 |

모든 글자 크기·색·버튼 높이·여백은 `UiTheme`에서 온다. 새 문자열 `npc_dialog_continue`("계속하기")를 `ko.json`에 더했다.

## 2. 동작 (`NpcDialogScreen`)

- 상태: **닫힘 → 읽기 → 선택**.
- 열기(NPC·건물 클릭, 데모): 대사 순서 = 풀 정의의 `_quote`(있으면) + `_lines`(`NpcInstaller.Pool.Find`). 정의가 없으면 `CurrentLine` 한 줄.
- [계속하기]: 타자기 중이면 즉시 완성, 끝났으면 다음 줄, 마지막 줄 뒤에는 선택 단계로 간다. 선택 단계에서는 [계속하기]를 숨기고 `INpcInteraction.Options` 버튼 + [닫기]를 보인다.
- 선택지를 누르면 `TryInteract` 명령만 보낸다. 결과 문장(격려 결과, 대화면 다음 대사, 실패 이유는 강조색)은 대사 칸에 타자기로 한 줄 뜬다. 정차당 1회 제한은 버튼 비활성 + 이유 줄로 보인다.
- 닫기: Esc, 그늘 클릭, [닫기], 내부 화면이 닫히거나 다른 거점으로 갈 때. 대사 칸 위를 클릭하면 타자기가 즉시 완성된다(027).
- 입력 차단: 열린 동안 전체 그늘이 레이캐스트를 받는다. `InteriorNpcWalker.IsPointerOverUi`가 참이 되어 NPC·건물 클릭과 배치가 막힌다. 상황 화면(`_alwaysOn=false`)이라 `UiScreens` 규칙을 따른다.
- 초상: `Resources.Load<NpcPortraitCatalog>("Npc/NpcPortraits").Get(id)`. 스프라이트가 있으면 높이 396 고정에 폭은 그 스프라이트 비율로 맞춘다. 카탈로그가 없거나 null을 주면 몸체 스프라이트 픽셀 크기 × `_fallbackPortraitScale`(4)로 그린다. 카탈로그에 대체 초상(`_fallback`)이 있으면 그 초상이 쓰인다.
- 몸체: 열 때 `InteriorNpcWalker.SetTalking(id, true)`, 닫을 때 false. 말하는 NPC는 자기 자리에서 숨쉬기 흔들림을 멈추고 정면(flipX 끔)을 본다. 강조 색(017)은 그대로다.
- 데모: `HandleDemoStage`에 `panel-end`를 더했다(열고 [계속하기]를 끝까지 → 선택지 줄). `encouraged`도 이제 선택 단계까지 간 뒤 격려를 누른다. `OpenForDemo`·`InteractForDemo`라는 이름은 이 파일에 원래 없었고, 데모가 실제로 쓰는 진입점은 `HandleDemoStage`(+`Instance`·`Open`)다. 둘 다 유지했다.

## 3. NPC 고정 배치 (`InteriorNpcWalker`, `hub_phase_view.json`)

데이터 `_npcWanders:false`(이미 false)일 때 합류한 모든 NPC는 자리에 선다. 배회 코드는 지우지 않고 데이터로 끈다(스토리 AC 문구를 따름).

| 경우 | 자리 |
|---|---|
| 집에 건물이 섬 | `BuildingView.TryGetDoor` + `_doorOffsetY`(−0.42) |
| 집 칸은 있으나 건물 없음 | 집 칸 앵커들의 가운데 |
| 집 없음 | 내부 열차 앵커 |

- 한 자리에 여럿이면 합류 순으로 옆으로 `_npcPostSpacing` 0.45칸씩 벌려 가운데 맞춤으로 선다. 한 줄 최대 `_npcPostsPerRow` 3명이고, 넘치면 `_npcRowOffset` 0.3칸 위(뒤) 줄에 서며 그 줄은 정렬 순서가 한 단계 낮다.
- 처음 만들 때 자리에서 생기고, 멀면 `_npcSpeed`로 걸어간 뒤 숨쉬기 흔들림을 한다.
- 도착 걷기(023)는 열차 칸에서 출발해 `ArrivalSpeed`로 걷고, **배정된 자리에서** 끝난다(`Arriving=false`).
- 자리는 매 프레임 다시 계산한다(NPC는 몇 명이고 `TryGetDoor`는 건물 목록 조회). 그래서 건물이 늦게 서거나 집이 바뀌어도 따라간다.
- 새 데이터 키: `_npcPostSpacing` 0.45, `_npcPostsPerRow` 3, `_npcRowOffset` 0.3.

## 4. crowd 데모 (`NpcHomesDemoDriver`, `NpcHomesDemo.json`)

`--npc-stage crowd`는 homes와 같이 hub-1에 가스등 서식지를 완성하고 점등부를 합류시킨다. 그 뒤 `_crowdNpcIds`(`gas_fitter`, 같은 `lamp_street` 종류이고 원래 hub-2 NPC)를 `NpcRecruiter.TryJoin`으로 데모 강제 합류시킨다. 같은 종류 묶음이 하나뿐이라 NpcHomes 규칙("모두 차 있으면 함께 산다")대로 한 건물에 둘이 살고, 문 앞 양옆(±0.225칸)에 선다. 대화 화면은 닫혀 있다. 로그는 `NPC DEMO: crowd gas_fitter → hub-1 합류 = True`.

## 5. 브리지 결과

| 명령 | id | 결과 |
|---|---|---|
| refresh | f0064dae | failed=false, errors 없음 |
| 표식 `NpcDialog`·`DialogOption` → Generate | 46a78d3d | Executed. `NpcDialog.prefab`·`DialogOption.prefab` 14:10:29 갱신, 표식 소비 |
| Generate 2회차 | 3488db44 | Executed. 프리팹 시각 그대로(변경 없음) |
| scene-status | 6d223469 | Main.unity, isDirty=false, playing=false |

프리팹 확인: `Portrait` 297×396, `Dialog` 896×213, `_dialogDimAlpha` 0.35, `_continue` 바인딩 있음.

## 6. 캡처 플래그 (코디네이터)

1. 대화 장면: `--npc-demo --npc-stage panel` (타자기 진행 중일 수 있으니 2초 이상 뒤 캡처)
2. 선택지 줄: `--npc-demo --npc-stage panel-end`
3. 한 건물 문 앞 NPC 2명: `--npc-demo --npc-stage crowd`

## 7. 남은 일·주의

- 플레이 캡처를 하지 않았다. 배치·겹침(초상과 상자, 긴 이름과 Meta 겹침)은 화면으로 확인해야 한다.
- 초상 카탈로그 에셋은 아트 쪽 작업이다. 에셋이 없으면 몸체 ×4 대체 초상(작음)이 보인다.
- 안쪽 황동 줄 색은 `theme.Accent`다. 아트 감독이 `theme.Border`를 원하면 한 줄만 바꾸면 된다.
- 게임패드: 버튼은 UGUI 선택 가능 요소이고 Esc는 키보드만 받는다. 게임패드 취소 버튼 연결은 기존 화면들과 같이 아직 없다.
- 4줄을 넘는 대사는 잘린다(현재 데이터 대사는 한 문장 길이).
