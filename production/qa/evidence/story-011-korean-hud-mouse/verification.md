# 스토리 011 검증 기록

## 구현 범위

- `IronRing.Hud.KoreanHud`가 `GameServices.Ready` 후 일곱 시스템, `IHubClickSource`, 선택적 `ICombatRoundCommand`와 `IEncounterPresentationSource`를 연결한다.
- 생성한 UIDocument와 복제한 한국어 PanelSettings에 Story008 폰트를 적용한다.
- 얇은 상단 자원 오버레이, 클릭한 거점의 측면 관리 패널, 출발/전투 라운드 명령, 패배 오버레이를 구현했다.
- 관리 명령은 현재 거점 정차 여부, 조우 진행 여부, 폐허와 런 종료 여부를 검사한다. HUD는 배치/회수/건설/출발/라운드 인터페이스 명령만 호출하고 재고·번영을 직접 변경하지 않는다.
- 정적 구독과 인스턴스는 SubsystemRegistration에서 초기화하며 OnDestroy에서 구독을 해제한다.
- 별도 데모는 일곱 시스템과 클릭/전투 계약을 구현한 페이크를 사용한다. 실제 게임 밸런스나 전투 판정 증거가 아니다.

## 승인된 범위 변경

코디네이터 답변 `msg_2a36c07c1144`에 따라 `Assets/Scripts/Localization/Resources/ko.json`에 기존 키를 수정하지 않고 다음 키를 추가했다.

- `hud_remove`: 회수
- `hud_next_round`: 다음 라운드
- `hud_empty`: 비어 있음
- `hud_complete`: 완성
- `hud_incomplete`: 미완성

기존 영어 HUD, Main 씬, Bootstrap, 계약 파일은 수정하지 않았다. 기존 HUD 제거는 코디네이터 통합 작업 범위다.

## 제작 기본값

- HUD 표시 카탈로그는 현재 게임 데이터의 자원 4종, 시설 3종, 거점별 슬롯 4개를 따른다.
- UI는 1280×720 공유 한국어 패널 기준이며 레이아웃은 USS에서 조절한다.
- 데모 초기 재고 24, 번영 70, 순환 3, 시설 비용 4, 전투 3라운드는 데모 컴포넌트 Inspector 값이다.
- 데모 초기 배치는 생성 도구에서 한 번 저장하며 기존 씬은 재생성하지 않는다.

## 검증 상태

- `static-check.txt`: UXML과 JSON 파싱, 문자열 키 중복 및 참조 키 존재 검사 통과. Unity 컴파일 증거와 구분한다.
- 브리지 refresh 요청 `546909a8531e47cbb0188a4f829c639e`: 앞선 Story010 캡처와 Windows 보안 모달로 대기 후 120초 타임아웃. 타임아웃은 취소가 아니므로 재제출하지 않았다.
- 컴파일, 데모 씬 생성, 세 화면 캡처, 실제 마우스 조작: 아직 검증 전.
- Windows 보안 모달은 관찰만 했고 조작하지 않았다. 코디네이터에게 전달했다.
- 사용자 지시에 따라 테스트 코드를 작성하지 않았다.

## 재개 명령

기존 refresh 결과 확인 후 다음 명령을 순차 실행한다.

```powershell
py -3 -I tools/bridge/bridge.py compile-status
py -3 -I tools/bridge/bridge.py execute-method --method IronRing.Hud.Editor.Story011Build.Create
```

플레이 캡처는 코디네이터만 실행한다. 씬은 `Assets/Scenes/Demo/Story011/Story011Hud.unity`, 대기는 90프레임이며 시나리오는 기본, `--hud-demo-panel`, `--hud-demo-defeat`다. 추가 전투 시나리오는 `--hud-demo-combat`다.

## 스타일 미적용 수정 메모 (2026-11-01)

- 증상: Play에서 HUD가 USS 없이 렌더링됨(자원 라벨이 세로로 쌓이고 "출발" 버튼이 전체 너비).
- 추정 원인: `KoreanHud.uxml`과 `KoreanHud.uss`가 같은 Resources 경로명(`Hud/KoreanHud`)을 공유하여 `Resources.Load<StyleSheet>`가 시트를 받지 못했고(null이 `styleSheets.Add`로 조용히 통과), UXML 자체는 스타일 참조가 없었음. 에디터 로그 접근이 불가하여 코드와 에셋 구조로 추정했으며, 실제 화면 재캡처로 확인이 필요함.
- 수정: USS를 `KoreanHudStyle.uss`로 이름 변경(GUID 유지), UXML에 `<ui:Style src="KoreanHudStyle.uss"/>` 추가, 코드는 `Hud/KoreanHudStyle`을 로드하고 null이면 LogError, 한글 폰트는 `KoreanUi.Apply(_root)`로 적용.
- 컴파일: bridge refresh failed=false, 오류 없음.
- 재캡처: 데모 extra-args `--hud-demo-panel`(우측 거점 패널), `--hud-demo-defeat`(패배 오버레이), `--hud-demo-combat`(다음 라운드 버튼).
