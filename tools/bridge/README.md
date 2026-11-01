# Coordinator Bridge

사용자가 이미 열어 둔 Unity Editor를 파일 큐로 제어합니다. Unity를 새로 시작하거나 종료하지 않으며 batchmode를 사용하지 않습니다. Python 3 표준 라이브러리만 사용하므로 `python -I`로 실행할 수 있습니다. Windows에서 `python` 명령이 없으면 `py -3 -I`를 사용합니다.

```powershell
python -I tools/bridge/bridge.py ping --timeout 600
python -I tools/bridge/bridge.py compile-status
python -I tools/bridge/bridge.py scene-status
python -I tools/bridge/bridge.py refresh --timeout 600
python -I tools/bridge/bridge.py execute-method --method IronRing.Art.Editor.Story008Build.Build
python -I tools/bridge/bridge.py run-tests --filter IronRing.Track.Tests --results C:/abs/editmode.xml
python -I tools/bridge/bridge.py open-scene --path Assets/Scenes/Main.unity --no-restore-scene
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 90 --screenshot C:/abs/shot.png
python -I tools/bridge/bridge.py play-capture --seconds 3 --screenshot C:/abs/shot2.png
python -I tools/bridge/bridge.py stop
```

`--project`의 기본값은 이 스크립트가 위치한 저장소입니다. `--timeout`은 기본 300초이며 하위 명령 앞뒤 모두 지정할 수 있습니다. 종료 코드 0은 성공, 1은 작업 오류, 2는 타임아웃 또는 잘못된 CLI 인자입니다. 표준 출력에는 결과 JSON이 표시됩니다. `output`은 문자열이며 compile-status/refresh의 경우 내부 JSON 문자열에 `compiling`, `failed`, `errors`가 들어 있습니다. compile-status의 `ok`는 조회 성공 여부이므로 컴파일 성공 여부는 `failed`로 판단합니다.

CLI는 `.bridge/commands/<id>.json`을 원자적으로 게시합니다. Editor는 약 1초마다 하나씩 실행하고 `.bridge/results/<id>.json`에 `id`, `ok`, `output`, `error`, `finishedAt`을 기록합니다. 완료 명령은 `.bridge/done/`, 동작 로그는 `.bridge/bridge.log`에 저장됩니다. 최초 Editor 세션 시작 시 10분 이전 큐 파일은 실행하지 않고 done으로 옮깁니다. 잘못된 JSON이나 파일명과 다른 id도 로그를 남기고 보관합니다.

진행 중 명령과 컴파일 오류는 SessionState에 저장해 도메인 재로드를 통과합니다. Editor 프로세스 재시작에 대한 재개는 지원하지 않습니다. refresh는 ForceUpdate 후 컴파일이 끝날 때까지 기다리고 실패 여부 및 수집한 오류를 반환합니다. 브리지 설치 전 발생한 오류의 상세 메시지는 수집할 수 없지만 Editor의 컴파일 실패 상태는 반환합니다. execute-method는 인자 없는 정적 void/string 메서드만 지원하며 예외를 결과에 담습니다. 실행 중 임의 도메인 재로드로 메서드 완료를 확인할 수 없으면 중복 실행하지 않고 실패로 보고합니다.

run-tests는 EditMode 테스트만 실행합니다. filter는 네임스페이스/클래스/메서드 이름 접두사이며 정규식 문법을 해석하지 않습니다. Unity TestRunnerApi가 NUnit XML을 저장하며 개별 test-case의 fullname/result와 전체 passed/failed/total 요약을 확인할 수 있습니다. 매칭 테스트가 0개이면 실패입니다. 사용자가 별도 Test Runner 실행을 동시에 시작하지 않아야 합니다.

scene-status는 활성 씬 경로, isDirty, playing을 반환합니다. execute-method, open-scene, play-capture의 saveScene/restoreScene 기본값은 모두 true입니다. 실행 전 경로가 있는 dirty 씬은 SaveOpenScenes로 저장하고 완료 후 원래 활성 씬 경로를 복원합니다. 저장 경로가 없는 dirty 씬은 모달 없이 실패합니다. CLI의 --no-save-scene 및 --no-restore-scene으로 각각 끌 수 있습니다. 저장을 끈 상태에서 dirty 씬을 바꾸려 하면 실패합니다. open-scene으로 이동한 씬을 계속 열어 두려면 --no-restore-scene을 지정합니다. 명령 내부의 임의 execute-method가 표시하는 모달은 해당 메서드 작성자가 방지해야 합니다. 캡처는 Play 모드에서 실행되어 Edit 모드로 복귀합니다. 기본 지연은 90프레임이며 seconds는 timeScale과 무관한 실제 경과 시간입니다. 기존 캡처 파일은 덮어쓰지 않습니다. Game View가 렌더링 중이어야 하며 Editor 일시 정지, 모달 대화상자, Scene View 선택 등으로 캡처가 지연될 수 있습니다. 캡처용 오브젝트는 Play 모드에서만 생성되고 빌드에 포함되지 않습니다. `extraArgs` 문자열은 `SessionState`의 `IronRing.Bridge.Capture.ExtraArgs`로 전달되며 브리지가 자체 해석하지 않습니다.

명령은 순차 실행합니다. stop은 자신의 차례에 Play 모드만 종료하며 실행 중 테스트/캡처를 선점하지 않습니다. CLI 타임아웃은 실행 취소가 아니므로 같은 작업을 즉시 재전송하지 말고 출력된 id의 결과/로그를 확인합니다. 새 스크립트가 아직 import되지 않았다면 사용자가 열린 Editor에서 refresh할 때까지 기다려야 합니다. 로컬 명령 큐는 신뢰하는 사용자/에이전트만 쓰도록 관리해야 하며 샌드박스나 원격 인증 기능은 없습니다.

TestRunner 콜백은 설치된 1.6.0 패키지 소스와 [Unity 공식 설명](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/test-framework-introduction/running-tests/from-code/extension-get-test-results)을 기준으로 구현했습니다.

이름 없는 활성 씬이 clean이면 복원할 경로가 없으므로 대상 씬을 열고 완료 후 그 씬을 유지합니다. 이름 없는 dirty 씬은 항상 실패합니다. 이름 있는 씬은 기본적으로 저장 후 복원합니다. 이 동작은 조정자가 확인한 사용자 씬 보호 규칙입니다.
