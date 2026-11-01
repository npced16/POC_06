# 스토리 009 검증 기록

## 구현

- JSON의 노드 `_mapPosition.x/y`를 읽어 노드→굽힘점→다음 노드 경로에 선로 타일, 거점 4개, 경유지 4개와 한국어 이름표 8개를 생성한다.
- NotoSansKR 폰트를 사용하는 월드 TextMesh가 `Strings.Get`의 한국어 이름을 표시한다.
- MapView는 ITrackRuntime과 TrainController.TravelProgress 읽기 함수를 주입받는다. 직선에서는 선형 보간, 굽은 간선에서는 반지름 0.5의 원호를 따라 실제 진행도를 표시한다.
- Collider2D와 새 Input System의 마우스 입력, 카메라 레이캐스트가 거점 클릭 이벤트를 발생시킨다.
- Bootstrap에서 영어 HUD 연결을 제거하고 일곱 규칙 서비스와 맵·클릭 서비스를 등록한 뒤 MarkReady를 호출한다.
- 추가 요청에 따라 실제 WaypointResolver의 결과를 전달하는 IEncounterPresentationSource와 HUD 명령을 전달하는 ICombatRoundCommand를 같은 어댑터로 연결한다.
- 코디네이터가 추가로 소유 범위를 승인하여 ko.json에 이벤트 이름 키 4개를 추가했다. MainSettings의 영문 제목→키 매핑을 사용하며, 알 수 없는 제목은 기존 event_supply로 대체한다.
- 일반 Build는 완성된 Main 씬의 수동 편집을 보존한다. 명시적인 Rebuild만 JSON 초기 배치를 재적용한다.

## 기준 및 임시 제작값

- Tier: workflow minimal, qa.level minimal. 새 테스트는 작성하지 않았다. 기존 Integration 테스트의 HUD 경유 검증을 실제 시스템 상태 검증으로 변경했다.
- `docs/README.md`가 없음을 확인하고 코디네이터에게 질의했다. 코디네이터가 스토리, 공통 규칙, 게임 브리프와 계약을 기준으로 진행하도록 승인했다.
- 엔진 검토 후 카메라 직교 크기 11.25, 위치 (0, 0, -10), 타일 1유닛/32PPU, 고리 범위 x=-15..15/y=-7..7, 거점 크기 2, 열차 크기 1.25를 JSON 제작 기본값으로 사용한다. 1280×720에서 1유닛이 32픽셀이 된다.
- 라벨은 팔레트의 #F4E5BB를 쓰며 폰트 크기, 문자 크기, 정렬 순서, 열차 전방각을 JSON에서 읽어 씬 Inspector에 저장한다.
- 씬 Transform과 카메라 설정을 Awake/Start에서 덮어쓰지 않는다. 열차의 런타임 위치만 저장된 선로 앵커를 따른다.

## 현재 확보한 증거

- `refresh.json`: 열린 편집기 refresh 성공, compiling=false, failed=false, errors 빈 문자열.
- `scene-build.json`: BuildTopDownMain.Rebuild 실행 성공.
- `scene-inspection.json`: 검토 전 저장된 씬에서 노드 좌표 8개, 이름표 8개, 선로 56개, 거점 클릭 대상 4개 확인. 이는 이전 배치의 정적 검사이며 플레이 증거나 검토 후 배치 증거가 아니다. 재생성 후 갱신할 예정이다.
- 초기 TMP 생성은 프로젝트 TMP_Settings 부재로 실패했다. 설정을 외부 범위에 추가하지 않고 기존 Font를 직접 사용하는 TextMesh로 변경했다.
- 초기 JsonUtility 좌표 별칭 읽기가 적용되지 않는 것을 씬 정적 검사에서 발견했다. 임시 Newtonsoft 읽기로 복구했으나 엔진 검토 후 `_mapPosition` Vector2를 단일 JsonUtility 파싱으로 읽도록 정리했다. 중복 좌표와 격자·직각 검증은 유지한다.

## 엔진 검토 반영

- 코디네이터가 전달한 unity-specialist 검토의 필수 1~5 및 권고 6~8을 모두 코드에 반영했다. 방화벽 대화상자가 해제된 뒤 컴파일 및 Rebuild 검증이 필요하다.
- EventSystem과 InputSystemUIInputModule을 씬에 생성해 HUD가 소비한 클릭을 거점으로 전달하지 않도록 한다.
- AudioListener를 추가하고 모든 맵 SpriteRenderer에 URP Sprite-Unlit-Default 머티리얼을 명시한다.
- 노드 배열 순서 대신 Id로 열차 앵커를 찾는다. Bind에서 실제 선로 노드마다 앵커가 있는지 검증한다.
- 바운딩 박스 둘레 대신 실제 경로를 따라 정수 반복으로 타일을 생성하고, 굽힘점의 직각과 격자 정렬을 확인한다.
- MainRoot는 순서에 의존하는 데이터 배열 대신 이름이 있는 TextAsset 필드 7개를 저장한다.
- 브리지는 종료 후 ExtraArgs 문자열을 남기므로 MainRoot는 브리지 단계가 enter-play 또는 capture인 동안만 편집기 캡처 인자를 읽는다. Armed는 캡처 도우미가 Start 이전에 해제하므로 조건으로 사용하지 않는다. 이전 `--map-depart` 또는 `--integration-demo`가 이후 수동 Play를 변경하지 않도록 했다.

## 진행 중인 검증

- 첫 `run-tests --filter IronRing.Track.Tests`는 CoordinatorBridge.cs:158의 `_testApi.Execute`에서 NullReferenceException으로 실행하지 못했다. 코디네이터에게 브리지 복구를 요청했다.
- Track 및 Integration XML 결과를 확보해야 한다.
- 후속 refresh/compile-status는 진행 중인 코디네이터 Play 캡처 뒤 큐에서 대기 중이다. 완료되지 않은 명령을 통과로 기록하지 않는다.
- 대기 중인 원래 명령 ID: refresh `279d18246d5d4885833717f6894e2123`, compile-status `f847d2af18154d1db85330877f6d54c7`. 클라이언트 시간 초과는 취소가 아니므로 `.bridge/results/<id>.json`의 최종 결과를 먼저 확인한다.
- 재개 순서: 기존 큐 결과 확인 → `IronRing.Bootstrap.Editor.BuildTopDownMain.Rebuild` 실행 → Track 및 Integration 테스트 → 코디네이터 이동·정차 캡처. 현재 저장된 씬은 엔진 검토 이전 배치이므로 Rebuild 없이 Play하지 않는다.
- 코디네이터가 편집기 앞 Windows 보안(방화벽) 대화상자가 실행을 막고 있음을 확인했다. 사용자 직접 해제 후 기존 큐를 이어가기로 했으며, 실패 인계하지 말고 대기하라는 지시를 받았다.
- 코디네이터가 실행하는 이동·정차 Play 캡처를 직접 열어 한글과 구도를 확인해야 한다.
- 이미지 두 장은 위치와 배치를 증명한다. 입력 반응과 전체 이동 감각을 수동 플레이 통과로 간주하지 않는다.

## 검증 완료 (2026-11-01)

위 "진행 중인 검증" 항목은 모두 해소되었다. 코디네이터가 전달한 unity-specialist 검토 항목 1~8을 코드에 반영한 뒤 `IronRing.Bootstrap.Editor.BuildTopDownMain.Rebuild`를 마지막으로 한 번 실행했고, 그 결과 씬에서 아래 증거를 확보했다.

- **자동 테스트**: `production/qa/evidence/integration/editmode-presentation.xml` — EditMode 81/81 통과, 실패 0. `IronRing.Track.Tests.RingTrackTests` 10/10, `IronRing.Integration.Tests.IntegrationFlowTests` 2/2 포함. 브리지 `run-tests`로 실행(13:39 KST).
- **정차 스크린샷**: `01-stopped.png` (13:44 KST) — 열차가 주조소에 도킹한 상태.
- **이동 스크린샷**: `02-moving.png` (13:45 KST) — `--map-depart` 인자로 출발한 뒤 열차가 오른쪽으로 이동 중.
- **캡처 경로**: 편집기 내 브리지(`tools/bridge`)로 `Assets/Scenes/Main.unity`를 Play 캡처. 수동 Play에는 캡처 인자가 적용되지 않음을 MainRoot의 브리지 단계 검사로 보장한다.
- **코디네이터 육안 확인**: 고리형 선로 타일, 거점 4곳과 한국어 이름표(주조소/정원/항구/성채), 경유지 4곳(북쪽·동쪽·남쪽·서쪽 고개), 주조소에 정차한 열차, `--map-depart` 뒤 오른쪽으로 이동하는 열차.

인수 조건 7개가 모두 확인되어 스토리 상태를 Done으로 바꾼다. 정지 이미지 2장은 배치·이동·한글 가독성을 증명하며, 입력 반응과 전체 이동 감각은 여전히 수동 플레이 확인 대상이다.

Run result: VERIFIED — 자동 테스트 81/81, 정차·이동 스크린샷 2장 보존.
