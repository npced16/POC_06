# 스토리 010 검증 기록

## 구현

- `GameServices.Ready` 이후 지도 및 일곱 시스템 계약을 조회하고 상태 연출을 런타임 생성한다.
- 번영 게이지, 폐허 스프라이트 교체와 잔해, 방벽·포탑·공방, NPC 합류 말풍선, 조우 연출, 패배 화면을 담당 폴더에 구현했다.
- `Story010Effects.asset`에 스프라이트 참조, 연출 위치·크기·시간·색상을 저장한다. 기존 씬/카메라 배치를 런타임에 덮어쓰지 않는다.
- 한국어는 `Strings.Get`으로 가져오며 `KoreanUi`의 NotoSansKR 폰트 설정을 사용한다.
- 실제 규칙 시스템은 수정하지 않는다. 데모는 일곱 인터페이스와 `IMapAnchors`의 페이크를 사용한다.

## 절차와 제한

- workflow minimal, Visual/Feel 스토리. 사용자 지시에 따라 테스트 코드는 작성하지 않았다.
- `docs/README.md`가 없으므로 지정된 스토리와 공유 orchestration 규칙, 게임 브리프를 기준으로 구현했다.
- 추가 Unity 전문 검토 워커 요청은 Orca의 `nested_worker_depth_exceeded`로 실행되지 않았다. 자체 API 검토와 열린 에디터의 실제 컴파일 결과를 기록한다.
- 씬 생성은 열린 에디터의 브리지 `execute-method`로 수행했다. Unity 실행/종료, batchmode, git, asmdef, Main 씬, Bootstrap 변경은 하지 않았다.

## 컴파일 및 씬 생성

- `py -3 -I tools/bridge/bridge.py refresh --timeout 600`: 종료 0, compiling=false, failed=false, errors 빈 문자열. 요청 ID `b3609782e081484ea5273fb0eba2f8a6`.
- `execute-method --method IronRing.Presentation.Effects.Editor.Story010Build.Generate`: 종료 0. 요청 ID `fc02901a972d4efdb44f94402ba95c1d`.
- `compile-status`: 종료 0, compiling=false, failed=false, errors 빈 문자열. 요청 ID `5edce55def6841f3a08ce13619191804`.

## 플레이 관찰

현재 코디네이터 캡처를 기다리는 중이며 아래 파일은 아직 관찰 완료로 간주하지 않는다. 첫 캡처 요청 bfdaca14895d4a159c300c5d2340dc2b는 Play 진입 후 지연되었다. 코디네이터는 09:08 KST에 사용자가 직접 닫아야 하는 Windows 보안 대화상자가 전체 큐를 막고 있음을 확인했다. 사용자 처리를 기다리며 새 브리지 명령과 실패 정산을 중단하라는 지시에 따라 대기한다.

공통 인자: `--scene Assets/Scenes/Demo/Story010/Story010Effects.unity --after-frames 45`.

| 파일 | extraArgs | 확인 대상 |
|---|---|---|
| 01-ruin.png | --fx-demo --fx-stage=ruin | 폐허, 잔해, 번영 게이지 |
| 02-facilities-npc.png | --fx-demo --fx-stage=facilities | 시설 3종, NPC, 한글 말풍선 |
| 03-combat.png | --fx-demo --fx-stage=combat | 습격자, 전투 문구 |
| 04-defeat.png | --fx-demo --fx-stage=defeat | 전체 어둡게, 한글 패배 문구 |

`--fx-demo`만 지정하면 100프레임마다 폐허→시설과 NPC→전투→종료→패배로 진행한다. stage 옵션은 동일 이벤트 순서를 빠르게 진행한 뒤 해당 상태에서 정지하여 캡처를 재현한다.

## 임시 기본값 및 남은 확인

- 번영 최대 100은 현재 HubProfiles 값과 일치하며 Inspector에서 조정 가능하다.
- 말풍선 3초, 잔해 2초, 데모 단계 100프레임은 시각 검증용 기본값이다.
- 코디네이터가 추가한 IEncounterPresentationSource를 소비한다. IsCombat에 따른 분기, RoundResolved의 라운드 번호·한글 승패, EventResolved의 현지화 키·아이콘을 연결했다. 선택 계약이 없으면 EncounterEnded 결과만 표시한다. 실제 규칙 시스템 어댑터는 스토리 009 소유이며 통합 확인은 코디네이터가 담당한다.
- 정지 이미지는 배치·존재·한글 가독성을 확인하며 타이밍과 게임 감각은 별도 플레이 확인 대상이다.

- 계약 반영 후 refresh 요청 9e18a313b4eb47d981a922e3cbab48e0 및 compile-status 요청 7e49ee7233dd4572a4c88cd767c840a5: 모두 종료 0, failed=false, errors 빈 문자열.

Run result: NOT VERIFIED — 코디네이터 플레이 캡처 대기.

## 검증 완료 (2026-11-01)

위 "플레이 관찰" 표의 네 캡처가 모두 확보되어 관찰 완료로 전환한다. Windows 보안 대화상자 해제 뒤 코디네이터가 편집기 내 브리지(`tools/bridge`)로 `Assets/Scenes/Demo/Story010/Story010Effects.unity`를 Play 캡처했다(13:39~13:40 KST, 45프레임 대기).

| 파일 | 시각 | 코디네이터 육안 확인 |
|---|---|---|
| `01-ruin.png` | 13:39 | 폐허 거점 스프라이트, 「폐허」 이름표, 번영 게이지 |
| `02-facilities-npc.png` | 13:40 | 방벽·포탑·공방 스프라이트, NPC 스프라이트와 말풍선 「새 동료가 합류했습니다」 |
| `03-combat.png` | 13:40 | 경유지 옆 습격자 스프라이트, 떠오르는 문구 「1/3 습격을 막아냈습니다」 |
| `04-defeat.png` | 13:40 | 어두워진 화면 위 「모든 거점이 무너졌습니다 / 철환의 여정이 끝났습니다」 |

인수 조건 6개가 모두 확인되어 스토리 상태를 Done으로 바꾼다. 데모 씬은 페이크 계약 위에서 동작하므로 실제 규칙 시스템과의 결합은 Main 씬 통합 실행(스토리 009 증거)에서 확인한다. 정지 이미지는 배치·존재·한글 가독성을 증명하며 타이밍과 게임 감각은 수동 플레이 대상이다.

Run result: VERIFIED — 폐허·시설+NPC·전투·패배 스크린샷 4장 보존.
