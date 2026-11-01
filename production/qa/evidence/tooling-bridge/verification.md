# Coordinator Bridge 검증 결과

검증 일시: 2026-11-01 08:36~08:39 KST. 실행 중이던 Unity 6000.3.27f1 Editor에서 수행했습니다. Editor 시작/종료, batchmode, Git CLI, 테스트 코드 추가는 하지 않았습니다.

## 확인한 결과

| 항목 | 실제 결과 |
| --- | --- |
| ping | ok=true, pong; Unity 6000.3.27f1 |
| compile-status | ok=true, compiling=false, failed=false, errors 비어 있음 |
| refresh | ForceUpdate 이후 도메인 재로드를 거쳐 같은 명령 id로 ok=true 완료 |
| Track EditMode | passed=10, failed=0, total=10, skipped=0 |
| Main play-capture | 90프레임 지연 후 main.png 생성, 이미지 직접 확인 |
| 캡처 종료 상태 | Assets/Scenes/Main.unity, isDirty=false, playing=false |

실제 CLI는 `py -3 -I tools/bridge/bridge.py`를 사용했습니다. 요청된 `python -I`는 이 PC의 python.exe 경로 오류로 실행되지 않았으며, 설치된 Python 3 런처의 동일 격리 옵션으로 대체했습니다.

## 증거 파일

- `track-editmode.xml`: Unity TestRunnerApi에서 저장한 NUnit 결과. 개별 fullname/result 포함.
- `main.png`: 실제 Main 씬 Game View 캡처. 현재 씬의 기존 영어 패널 UI가 보이며 게임 아트 개선의 증거로 해석하지 않습니다.
- `command-results.json`: 해당 검증 명령들의 원본 결과 JSON 모음.
- `bridge-log.txt`: 큐 처리와 도메인 재로드 상태 복원 기록.

## 씬 보호 규칙과 검증 범위

조정자 추가 요청에 따라 scene-status와 saveScene/restoreScene을 구현했습니다. 두 옵션 기본값은 true이며, 이름 있는 씬은 실행 전 저장하고 명령 완료 후 원래 활성 씬 경로로 복원합니다. 이름 없는 dirty 씬은 모달 없이 실패하고 이름 없는 clean 씬은 대상 씬을 유지합니다. 이번 시작 상태는 path가 비어 있는 clean Untitled 씬이었으며, 조정자의 명시적 승인 후 `--no-restore-scene`으로 Main을 캡처했습니다.

이번 실제 검증은 기본 설치, 정상 컴파일, refresh 도메인 재로드 재개, EditMode 테스트, Play 캡처 및 종료입니다. dirty 씬 저장, 다른 이름 있는 씬으로부터의 복원, 컴파일 실패 발생 후 오류 수집, Play 전환 시 도메인 재로드를 강제하는 설정은 별도로 실행하지 않았습니다. 기존 Editor 설정을 변경하지 않았습니다. 임의 execute-method 내부에서 발생하는 사용자 작성 모달까지 브리지가 차단하지는 않습니다.

## 변경 파일

- Assets/Editor/CoordinatorBridge/CoordinatorBridge.cs 및 Unity 생성 meta
- Assets/Scripts/Tools/CoordinatorCapture.cs 및 Unity 생성 meta
- tools/bridge/bridge.py
- tools/bridge/README.md
- .gitignore의 .bridge/ 한 줄
- production/qa/evidence/tooling-bridge/ 내부 명세 및 본 검증 자료

제약: CLI 타임아웃은 작업 취소가 아니며 stop은 큐에서 선점하지 않습니다. SessionState 복구는 같은 Editor 세션의 도메인 재로드까지만 지원합니다. 테스트 0개는 성공으로 처리하지 않습니다. 추가로 수행해야 하는 필수 인수 조건은 없습니다.
