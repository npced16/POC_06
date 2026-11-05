# 대화창 목록

현재 프로젝트(`Assets/Scenes/Main.unity`, `MainCanvas` 아래 화면 프리팹)에 있는 대화창과 대화형 창을 정리한다.
현재 규칙은 `design/game-brief.md`가 우선한다. 이 문서는 2026-11-05 기준 코드와 데이터를 읽어 만든 참고 문서다.

| 창         | 화면 Id / 프리팹                                                | 스크립트                                        | 데이터                                                           | 현재 사용              |
| --------- | ---------------------------------------------------------- | ------------------------------------------- | ------------------------------------------------------------- | ------------------ |
| NPC 대화창   | `NpcDialog` / `Assets/Prefabs/UI/Screens/NpcDialog.prefab` | `Assets/Scripts/Hud/Npc/NpcDialogScreen.cs` | `Assets/Scripts/Npc/Data/NpcPool.json`, `NpcInteraction.json` | 사용 중 (마을 안 NPC 클릭) |
| 중간역 탑승 대화 | `NpcDialog` (같은 프리팹, `_boardingBackdrop`)                  | `NpcDialogScreen.OpenBoarding`              | 호출자가 넘긴 이름·출신·대사                                              | 호출하는 곳 없음 (아래 2절)  |
| 휴식지 사건 창  | `RestEvent` / `Assets/Prefabs/UI/Screens/RestEvent.prefab` | `Assets/Scripts/Hud/RestEventScreen.cs`     | `Assets/Scripts/Track/Resources/Track/RestEvents.json`        | 사용 중 (경유지 정차)      |
| 승객 알림     | `Toast` / `Assets/Prefabs/UI/Screens/Toast.prefab`         | `Assets/Scripts/Npc/PassengerAnnouncer.cs`  | `NpcPool.json`의 `_*ToastFormat`                               | 사용 중 (탑승·하차·떠남)    |

---

## 1. NPC 대화창 (`NpcDialog`)

### 1.1 여는 방법과 닫는 방법

- 열기: 마을 내부 화면에서 NPC를 클릭한다(`INpcClickSource.NpcClicked` → `Open(hubId, npcId)`).
- 닫기: `[닫기]` 버튼, `Esc`, 그늘(Dim) 클릭, 내부 화면이 닫히거나 다른 거점으로 바뀔 때.
- 열려 있는 동안 핫바를 숨기고(`HotbarScreen.SetSuppressed`), NPC를 강조하고 말하는 상태로 둔다.
- 대화창이 열려 있으면 루프 시간이 멈춘다(`LoopInstaller`에서 `dt = 0`).

### 1.2 화면 구성 (빌더 `Assets/Scripts/Hud/Editor/Screens/NpcDialogScreenBuilder.cs`)

| 요소 | 내용 |
| --- | --- |
| Dim | 화면 전체 반투명 그늘. 알파 `_dialogDimAlpha` = 0.35. 클릭하면 닫힌다. |
| Portrait | 왼쪽 아래 큰 상반신 초상. 높이는 기준 해상도(1280×720)의 55%. `Npc/NpcPortraits` 카탈로그에 없으면 몸체 스프라이트 × `_fallbackPortraitScale`(4). |
| Panel | 아래 가운데 이중 테두리 대화 상자. 폭 70%, 최소 높이 190, 아래 여백 24. |
| Title | NPC 이름(직업명). |
| Meta | `집  <서식지 이름>  ·  <출신> 출신`. 집이 없으면 `집 없음`을 강조색으로 표시. |
| Line | 대사. 한 쪽 최대 4줄, TMP Overflow = Page, 타자기 속도 `_typeSpeed` = 28자/초. |
| Reasons | 비활성 선택지 이유. 버튼 줄 왼쪽. |
| Options | 선택지 버튼(`Parts/DialogOption.prefab`). |
| Continue / Close | `[계속하기]` / `[닫기]`. 쪽이 남으면 `[계속하기]`가 `▼ 계속`으로 바뀐다. |

### 1.3 진행 순서

1. 읽기 단계: 첫 대사(`_quote`) 한 줄만 보인다. `[계속하기]`를 누르면
   - 타자기 중이면 그 쪽을 즉시 완성한다(대화 상자 클릭도 같음).
   - 남은 쪽이 있으면 다음 쪽으로 넘긴다.
   - 아니면 선택 단계로 간다.
2. 선택 단계: `[계속하기]` 대신 선택지 버튼과 `[닫기]`가 나온다. 대사 칸에는 첫 대사가 남는다. 다음 대사(`_lines`)는 `[대화]`를 눌러야 나온다.
3. 선택지 결과 문장은 대사 칸에 타자기로 표시한다. 실패 이유는 강조색이다.

### 1.4 선택지 (`NpcInteraction.json`)

| Id | 라벨 | 효과 | 제한 |
| --- | --- | --- | --- |
| `talk` | 대화 | 다음 대사를 결과 문장으로 표시한다(`_quote` + `_lines` 순환). | 없음 |
| `encourage` | 격려 | NPC의 거점(집 거점, 없으면 합류 거점)에 번영 +3. 결과: `격려했다. 번영 +3` | NPC마다 정차당 1회. 열차가 다음 노드에 도착하면 초기화. |

비활성 이유 문구:

- `_usedReason`: 이번 정차에는 이미 격려했다
- `_noHubReason`: 거점에 머무는 주민이 아니다

### 1.5 UI 문구 (`Assets/Scripts/Localization/Resources/ko.json`)

| 키 | 값 |
| --- | --- |
| `npc_dialog_home` | 집 |
| `npc_dialog_home_none` | 집 없음 |
| `npc_dialog_origin` | {0} 출신 |
| `npc_dialog_continue` | 계속하기 |
| `npc_dialog_close` | 닫기 |
| `ui_npcdialog_more` | ▼ 계속 |

### 1.6 NPC별 대사 (`NpcPool.json`)

모든 NPC는 출신 `선로 너머`(`_origin: "*"`), 탑승 확률 0.35, 탑승 조건 `habitatComplete = <자기 서식지>`이다.
대사는 직업마다 성격을 하나 정하고 그 말투로 쓴다. 업무 보고가 아니라 플레이어에게 건네는 말로 쓴다.
대화창을 열면 첫 대사(`_quote`)만 나온다. [대화] 선택지를 누를 때마다 다음 대사로 넘어가며 `_quote` → `_lines` → `_quote` 순서로 돈다.

모든 NPC의 `_hint`는 같다: `일할 시설이 완성된 마을을 찾고 있습니다.`
`_proposal`은 `<서식지 이름>에 일할 자리가 있다면 합류하겠습니다.` 형식이다. 둘 다 승객 목록·알림용 안내 문구라 말투를 바꾸지 않았다.

| Id | 이름 | 성격 | 서식지 | 첫 대사 (`_quote`) | 다음 대사 (`_lines`) | 직무 |
| --- | --- | --- | --- | --- | --- | --- |
| `land_clearer` | 개간공 | 우직함 | 개간지 (`cleared_lot`) | 이 돌무더기 밑에 뭐가 묻혀 있을지 누가 알아요? | 땅만 고르면 사람은 알아서 모여요. 두고 봐요. | income +2 |
| `woodcutter` | 벌목공 | 무뚝뚝함 | 벌목터 (`logging_site`) | 나무 냄새 좋지? 난 이게 제일 좋더라. | 많이 베면 그만큼 심어야지. 그게 순리야. | income +2 |
| `well_keeper` | 우물지기 | 조심스러움 | 돌우물 (`stone_well`) | 물 한 모금 드실래요? 오늘 물이 유난히 맑네요. | 우물이 마르면… 아, 그런 생각은 안 하기로 했어요. | housing +1 |
| `shelter_keeper` | 거처 관리인 | 다정함 | 임시 거처 (`temporary_shelter`) | 피곤해 보이네요. 빈 침상 하나쯤은 늘 있어요. | 지붕이 있으면 사람이 떠나질 않아요. 신기하죠. | housing +2 |
| `field_keeper` | 밭지기 | 느긋함 | 계단식 밭 (`terrace_field`) | 싹이 났어요! 아주 조그맣지만요. | 밥은 서두른다고 빨리 익지 않잖아요. 곡식도 그래요. | income +2 |
| `coal_miner` | 갱도 광부 | 투박함 | 석탄 갱도 (`coal_shaft`) | 얼굴에 검댕 묻었다고? 하하, 이게 훈장이지. | 갱도 안은 조용해. 너무 조용해서 탈이지. | income +2 |
| `cook` | 공동 주방장 | 수다스러움 | 공동 취사장 (`communal_kitchen`) | 배고프죠? 표정에 다 써 있어요. 앉아요, 앉아. | 같이 밥 먹은 사람끼리는 쉽게 안 싸워요. 진짜예요. | housing +2 |
| `blacksmith` | 대장장이 | 고집스러움 | 철 대장간 (`iron_forge`) | 쇠는 거짓말을 안 해. 두드린 만큼 단단해지지. | 녹슨 선로 못이라도 가져와. 내가 살려 줄 테니. | income +3 |
| `copper_miner` | 구리 광부 | 들뜸 | 구리 갱도 (`copper_pit`) | 이 초록 돌 좀 봐요! 구리 냄새가 나요, 구리 냄새! | 언젠가 이 구리로 마을 끝까지 불이 켜질 거예요. | income +2 |
| `stoker` | 보일러 화부 | 예민함 | 보일러실 (`steam_boiler`) | 쉿, 들려요? 보일러가 기분이 좋을 때 내는 소리예요. | 불은 너무 세도, 너무 약해도 토라져요. 사람 같죠. | income +3 |
| `pump_keeper` | 급수 기술자 | 걱정 많음 | 급수장 (`pump_station`) | 어디서 물 새는 소리 안 났어요? 아, 아니면 됐어요. | 물이 끊기면 열차도 못 떠나요. 그래서 잠이 안 와요. | housing +2 |
| `machinist` | 기계공 | 신남 | 기계 공방 (`gear_workshop`) | 톱니 소리가 딱딱 맞을 때 그 기분, 알아요? | 남은 부품으로 뭘 만들고 있는데… 아직 비밀이에요. | income +3 |
| `station_master` | 역무원 | 깐깐함 | 증기역 (`steam_station`) | 열차는 제시간에 와야 합니다. 그게 약속이니까요. | 승강장에 사람이 늘었어요. 예전엔 저 혼자였는데. | income +3 |
| `wire_drawer` | 전선공 | 차분함 | 전선 공방 (`wire_workshop`) | 전선은 천천히 뽑아야 해요. 급하면 끊어지거든요. | 이 선 끝에 불이 켜지는 걸 보면, 그날은 잠이 잘 와요. | light +1 |
| `electrician` | 발전 기술자 | 자부심 | 발전소 (`electric_works`) | 바늘 떨리는 거 보여요? 지금 이 마을의 심장 소리예요. | 밤이 무섭지 않은 마을. 제가 만들어 볼게요. | light +3 |
| `night_vendor` | 야시장 상인 | 능청스러움 | 야시장 (`night_market`) | 구경만 해도 돈 안 받아요. 오늘만요. | 열차 손님들 짐이 제일 재밌어요. 별걸 다 가지고 다니거든요. | income +2 |
| `clock_keeper` | 시계탑지기 | 엄격함 | 시계탑 (`clock_tower`) | 일 분 늦는 건 괜찮아. 매일 늦는 게 문제지. | 종소리가 울리면 다들 고개를 들어. 그 순간이 좋아. | light +2 |

참고: `ko.json`의 `npc_electrician`은 `전기 기사`지만 대화창 이름은 `NpcPool.json`의 `_name`(`발전 기술자`)을 쓴다.

### 1.7 데모 단계

`NpcHomesDemoDriver` 단계로 대화창을 연다. 대상은 `hub-1`의 NPC(지정이 없으면 첫 합류 NPC)다.

| 단계           | 동작                                           |
| ------------ | -------------------------------------------- |
| `panel`      | 대화창을 연다.                                     |
| `panel-end`  | 열고 `[계속하기]`를 끝까지 눌러 선택지 줄을 보인다.              |
| `encouraged` | `panel-end` 뒤 격려를 누른다. 이미 했으면 이유를 대사 칸에 보인다. |
| 그 밖          | 대화창을 닫는다.                                    |

---

## 2. 중간역 탑승 대화 (`NpcDialogScreen.OpenBoarding`)

- 같은 `NpcDialog` 프리팹을 쓰고 `_boardingBackdrop`(승강장 배경)을 켠다.
- 이름, 출신, 대사 한 줄을 표시한다. 선택지는 없고 마지막 쪽에서 `[계속하기]`를 누르면 닫힌다.
- 열린 휴식지 사건 화면을 잠시 가리고, 닫으면 사건 화면이 다시 보인다.
- 내부 화면 상태와 무관하게 유지된다(마을 밖에서도 닫히지 않는다).
- 현재 코드에서 `OpenBoarding`을 호출하는 곳은 없다. `design/game-brief.md`는 "승객이 탑승하면 별도의 초상 대화 창 대신 짧은 알림을 보여준다"로 정했고, 탑승은 4절의 승객 알림으로 표시된다.

---

## 3. 휴식지 사건 창 (`RestEvent`)

### 3.1 구성과 진행

- 사건 내용: 제목(`_title`), 상황(`_body`), 선택지 버튼 최대 4개(데이터는 1~3개, 만석 교체로 늘어날 수 있다).
- 사용할 수 없는 선택지는 비활성화하고 라벨 아래 줄에 이유를 붙인다(예: `마르크 부족`).
- 선택 뒤에는 선택지를 숨기고 결과 상자(결과 문장, 상세, `[계속]`)를 보인다.
- `[계속]`을 누르면 사건이 닫히고 열차 화면으로 돌아온다. 승객 알림은 사건 창이 닫힌 뒤 표시된다.

### 3.2 사건 (`RestEvents.json`)

| Id | 가중치 | 제목 | 상황 | 선택지 | 결과 | 효과 |
| --- | --- | --- | --- | --- | --- | --- |
| `waypoint_boarding` | 1 | 중간역의 낯선 승객 | 열차 문 앞에 서 있는 사람: {이름}<br>{건물} 완성 → {이름} 합류 · {직무} +{값}<br>탄 승객 {탄 수}/{최대}명 · 태울지 결정하자. | 태운다 | 새 승객이 객차에 올랐다. | `passenger` (`_candidate`) |
| | | | | 돌려보낸다 | 승객은 다음 열차를 기다리기로 했다. | `nothing` |
| `quiet_stop` | 0 (후보가 없을 때만) | 조용한 정차 | 열차가 잠시 쉬어 갑니다. | 계속 가기 | 열차가 다시 출발합니다. | `nothing` |

`waypoint_boarding`(`_passengerCandidate: true`) 진행:

- 창이 열릴 때 `Passengers.PickCandidate`로 후보 한 명을 정한다(출신지·탑승 조건을 채우는 NPC 우선). 후보가 없으면 이 사건은 뽑히지 않고, 가중치 0인 `quiet_stop`이 대신 나온다.
- 좌석이 가득 찼으면(3/3) [태운다] 대신 지금 승객마다 `{이름} 내리고 태운다`(`rest_boarding_swap`)가 나온다. 고른 승객은 떠남 알림과 함께 내리고, 나중에 다시 탈 수 있다.
- 거절에는 보상이 없다. 좌석 수와 3바퀴 뒤 떠나는 규칙이 선택의 대가다.

설정: `_seed` 5101, `_autoChooseSeconds` 0, `_autoContinueSeconds` 0, `_disableAutoBoarding` true(확률 탑승을 끄고 사건 효과로만 탑승).

### 3.3 결과 상세 문구 (`ko.json`)

| 키 | 값 |
| --- | --- |
| `rest_choice_no_money` | 마르크 부족 |
| `rest_detail_sep` | ` · ` |
| `rest_detail_tile` | {0} ×{1} |
| `rest_detail_tile_lost` | {0} 1개 잃음 |
| `rest_detail_money` | {0}마르크 |
| `rest_detail_on_return` | 거점 도착 시 {0}마르크 |
| `rest_detail_prosperity` | 번영 {0} |
| `rest_detail_passenger` | 승객 {0}명 합류 |
| `rest_detail_delay` | {0}초 지연 |
| `loop_continue` | 계속 |

---

## 4. 승객 알림 (`Toast`)

대화창은 아니지만 탑승 대화를 대신하는 알림이다. 휴식지 사건이 열려 있는 동안은 대기하고, 한 번에 하나씩 `_toastSeconds`(4초) 동안 보인다.

| 상황 | 형식 (`NpcPool.json`) | 예시 |
| --- | --- | --- |
| 탑승 | `{0} {1}에서 탔다 · {2}` | 벌목공이 선로 너머에서 탔다 · 벌목터 완성 → 벌목공 합류 |
| 하차 | `열차에서 {0} 내렸다 · {1} 출신` + ` · <직무> +<값>` | 열차에서 벌목공이 내렸다 · 선로 너머 출신 · 매 바퀴 마르크 +2 |
| 떠남 | `{0} 열차에서 내려 떠났다` | 벌목공이 열차에서 내려 떠났다 |

- `{0}` 이름에는 받침에 따라 `이`/`가`를 붙인다.
- 탑승의 `{2}`는 `passengers_wants`(`{0} 완성 → {1} 합류`)이며, 서식지가 없으면 `_proposal`을 쓴다.
- 직무 이름: `income` = 매 바퀴 마르크, `housing` = 주거, `light` = 빛.

---

## 5. 변경 기록

대화창과 대화 문구를 바꿀 때마다 아래에 추가한다. 최근 변경이 위에 온다.

| 날짜 | 범위 | 변경 | 파일 |
| --- | --- | --- | --- |
| 2026-11-05 | NPC 대화창 | 대화창을 열면 첫 대사 한 줄만 읽고 바로 선택지로 간다. 다음 대사는 [대화]를 눌러야 나온다. 전에는 선택지 전에 첫 대사와 `_lines`를 모두 읽었고 첫 대사가 두 번 나왔다. | `Assets/Scripts/Hud/Npc/NpcDialogScreen.cs` |
| 2026-11-05 | NPC 대화창 | 17명의 대사를 직업별 성격 말투로 교체했다. 첫 대사(`_quote`)는 성격이 드러나는 한 줄, 다음 대사(`_lines`)는 한 줄이다. 공통 대사 "오늘도 제 작업장을 살피겠습니다." / "이 마을이 커지면 제 일도 더 바빠지겠군요."와 옛 "~하겠습니다" 첫 대사는 없앴다. | `Assets/Scripts/Npc/Data/NpcPool.json` |
| 2026-11-05 | 휴식지 사건 창 | `waypoint_boarding`에 탑승 결정을 추가했다. 후보의 이름·요청·직무와 `탄 승객 N/최대명`을 보여 주고 [태운다]/[돌려보낸다]를 고른다. 만석이면 `{이름} 내리고 태운다`로 교체한다. 선택지 버튼을 4개로 늘렸다. | `RestEvents.json`, `ko.json`, `PhaseLoop.cs`, `Passengers.cs`, `RestEvent.prefab` |
| 2026-11-05 | 문서 | 대화창 목록 문서를 처음 만들었다. | `docs/dialogues.md` |
