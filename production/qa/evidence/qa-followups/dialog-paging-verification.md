# NPC 대화 상자 긴 대사 쪽 나눔 — 확인 기록

- 작성: ui-programmer(Claude), 2026-11-02
- 근거: QA 메모 2026-11-01 "4줄 초과 대사"(대사 칸이 4줄을 넘는 문장을 잘라 버림)
- 규칙: `production/handoffs/codex-ui-charter.md` (빌더로만 생성, 런타임 UI 생성 없음, Simple 스킨, 글자 4단계·줄 높이 ×1.5)

## 고른 방법: TMP Overflow = Page (쪽 나눔). 상자 자동 늘림은 쓰지 않음
- 이유: 상자 높이(213)는 초상(297×396)·버튼 줄·핫바 가림과 맞물려 있어 늘리면 레이아웃이 흔들리고, 늘린 뒤에도 결국 쪽 나눔이 필요하다. TMP의 Page 모드는 실제 글자 폭·줄바꿈으로 쪽을 계산하므로(화면 비율·초상 폭이 바뀌어도 그대로 맞음) 한 가지 방식만 두는 편이 단순하고 튼튼하다.
- 동작
  1. 대사를 넣으면 `ForceMeshUpdate`로 `textInfo.pageCount`·`pageInfo`를 채우고 1쪽(`pageToDisplay = 1`)부터 보인다.
  2. 타자기는 그 쪽의 첫 글자(`pageInfo[p].firstCharacterIndex`)부터 마지막 글자까지만 친다(`maxVisibleCharacters`는 절대 번호).
  3. 남은 쪽이 있으면 [계속하기] 버튼 글자가 **"▼ 계속"**(`ui_npcdialog_more`)으로 바뀐다. 누르면: 타자 중이면 지금 쪽 즉시 완성 → 다음 쪽.
  4. 마지막 쪽에서만 버튼이 "계속하기"로 돌아오고, 누르면 다음 대사 → 마지막 대사 뒤 선택지 단계.
  5. 선택 단계에서 결과 문장이 넘치면 같은 버튼이 "▼ 계속"으로 잠시 다시 나타나 쪽을 넘기고, 마지막 쪽이면 숨는다.
  6. 대사 칸에 말줄임·잘림 없음(빌더 `Truncate` → `Page`, 화면 스크립트도 시작할 때 `Page`로 강제). 이름·집/출신 줄은 NoWrap + Overflow(잘리지 않음) 그대로, 선택지 줄 배치도 그대로.

## 한 쪽 최대 줄 수
- 대사 칸 높이 = `theme.LineHeight(Normal) × BODY_LINES` = ceil(18 × 1.5) × 4 = **108px → 한 쪽 4줄**.
- 글자 폭(추정): 상자 896 − 왼쪽 시작 137(초상 오른쪽 313 − 상자 왼쪽 192 + 간격 16) − 오른쪽 여백 ≈ 730px. 한글 18px 기준 한 줄 약 40자, **한 쪽 약 160자**(추정치, 실제는 TMP 줄바꿈이 결정).
- `Assets/Scripts/Npc/Data/NpcPool.json`의 `_quote`/`_lines` 105개 중 가장 긴 것은 32자(예: 장갑 관련 대사) → 지금 데이터는 모두 1줄·1쪽. 쪽 나눔은 앞으로의 긴 대사·결과 문장·외부 데이터 대비.
- 데모 데이터 `Assets/Scripts/Npc/Resources/Npc/NpcHomesDemo.json`에는 대사 필드가 없어(대사는 NpcPool에서 읽음) 긴 시험 줄은 넣지 않았다(지시대로 생략). 데모 단계 `panel-end`는 [계속하기]를 끝까지 누르는 루프라 쪽이 있어도 그대로 끝까지 간다.

## 바꾼 파일
- `Assets/Scripts/Hud/Npc/NpcDialogScreen.cs` — `_page`, `ShowPage`, `PageCount/PageFirst/PageEnd/HasMorePages`, `RefreshContinue`(버튼 표시·글자), `Continue` 순서(타자 완성 → 다음 쪽 → 다음 줄 → 선택지), 타자기 끝 = 쪽 끝, `Open`에서 `LayoutText`를 대사보다 먼저.
- `Assets/Scripts/Hud/Editor/Screens/NpcDialogScreenBuilder.cs` — 대사 칸 `overflowMode = Page`.
- `Assets/Prefabs/UI/Screens/NpcDialog.prefab` — 빌더로 재생성(Line의 `m_overflowMode: 5` = Page).
- `Assets/Scripts/Localization/Resources/ko.json` — 끝에 `ui_npcdialog_more` = "▼ 계속" 추가.

## 브리지 결과
- `refresh` (수정 직후): `failed:false`, errors 없음.
- 표식 `.bridge/rebuild/NpcDialog` + `execute-method BuildMainCanvas.Generate` 1회: ok. 2회: ok, `NpcDialog.prefab`·`MainCanvas.prefab` md5 변화 없음(92c15fd3…, ddfe9c68…).
- `scene-status`: `isDirty:false`, Main.unity, playing:false.
- 두 번째 `refresh`: 다른 에이전트가 고치는 중인 `Assets/Scripts/Hud/HubPanelScreen.cs`(FacilityRow 형식 없음, CS0246)로 실패 — 이 작업 파일과 무관. 재시도 결과는 아래.

## 남은 확인(사용자 눈)
- Play에서 긴 결과 문장(160자 이상)을 띄워 "▼ 계속" → 다음 쪽 → 마지막 쪽 "계속하기"를 직접 확인. play-capture는 쓰지 않았다.
- "▼"(U+25BC) 글리프가 NotoSansKR SDF에 들어가는지(동적 아틀라스면 자동 추가) 화면에서 확인.
- 1분 뒤 재시도한 `refresh`: 여전히 `failed:true`. 오류 위치는 모두 다른 에이전트 작업 중인 파일(`Bootstrap/GameDefinition.cs`(8,33), `Bootstrap/GameSession.cs`(7,32), `Hud/HubPanelScreen.cs`(31) — `FacilityCatalog`/`FacilityRow` 형식 제거 중). 이 작업의 파일(NpcDialogScreen.cs·NpcDialogScreenBuilder.cs)에는 오류 없음. 이 작업 코드는 앞선 `failed:false` refresh에서 이미 컴파일됐고, 프리팹 재생성도 그 상태에서 했다.
