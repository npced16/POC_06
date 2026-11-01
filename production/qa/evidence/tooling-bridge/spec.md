# Coordinator Bridge 작업 명세

- 기준: task_702058895d46에 전달된 구현 요구사항. 저장소에 docs/README.md가 없어 해당 절차는 읽을 수 없음.
- 열린 Unity 6000.3.27f1 Editor에서 파일 큐를 약 1초마다 처리하며 Editor 시작/종료 및 batchmode 실행 금지.
- 명령: ping, refresh, compile-status, execute-method, run-tests(EditMode), open-scene, play-capture, stop.
- 결과는 id/ok/output/error/finishedAt JSON이며 처리 파일은 done으로 이동. 시작 시 10분 이전 명령은 실행하지 않음.
- SessionState로 실행 중 명령과 컴파일 오류를 보존. Play 캡처는 임시 런타임 오브젝트에서 수행 후 Edit 모드로 복귀.
- Python 표준 라이브러리 CLI, 원자적 요청 게시, 타임아웃/실패 종료 코드, 한국어 README 제공.
- 검증: 열린 Editor의 ping/compile-status 응답, IronRing.Track.Tests 통과 요약과 XML, Main 씬 PNG.
- 테스트 코드/asmdef/Git CLI/소유 범위 밖 변경 금지. 조정자 추가 요청에 따라 dirty 씬은 실행 전 저장하고 원래 활성 씬을 복원함.
- scene-status 및 saveScene/restoreScene 옵션 추가, 기본값 true. 이름 없는 dirty 씬은 실패하고 clean 씬은 대상 씬을 유지함.
