# AGENTS.md

## 0. Git

- 커밋 메세지 제안만 가능
- git cli 수행 금지

## 1. Comment

- Unity `MonoBehaviour`의 Lifecycle 및 Callback 메서드를 제외한 모든 메서드의 상단에 XML `summary` 주석을 작성한다.
- `summary`에는 다음 내용을 포함한다.
  - 메서드가 수행하는 동작
  - 메서드가 사용하는 주요 입력값
  - 메서드가 반환하는 값 또는 변경하는 주요 상태
- 내부 주석은 짧은 평문 문장으로 작성하며, 2~3문장을 초과하지 않는다.
- 내부 주석에는 이모지와 Markdown 문법을 사용하지 않는다.

```csharp
/// <summary>
/// 이동 입력을 받아 현재 속도를 목표 속도로 변경한다.
/// moveInput을 사용하며, 변경된 속도를 _velocity에 저장한다.
/// </summary>
private void ProcessMovement(Vector2 moveInput)
{
    Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);

    // 지면 이동에서는 입력 방향을 즉시 속도에 반영하지 않고 가속도로 처리한다.
    _velocity = Vector3.MoveTowards(
        _velocity,
        direction * _moveSpeed,
        _moveAcceleration * Time.fixedDeltaTime);
}
```

---

## 2. Field

### Inspector 설정값

- Inspector에 노출해야 하는 값만 `[SerializeField]`를 사용한다.
- `[SerializeField] public`은 사용하지 않는다.
- `[SerializeField]`가 필요하지 않은 필드는 `private`으로 선언한다.

### 외부 공개 상태

- 클래스 외부에서 값을 참조해야할 경우에만 `public` 프로퍼티를 사용한다.

---

## 3. Field 순서

### 역할 별 1차 분류

필드는 역할에 따라 [Header("")] attribute를 사용하여 그룹화한다.

그룹은 다음 순서로 배치한다.

1. `const`
2. `static`
3. `[Header("")]`
4. ...

### 접근 제한자 별 2차 분류

각 [Header("")] 그룹 내부의 필드는 다음 순서로 배치한다.

1. `[SerializeField] private`
2. `private/protected`
3. `public`

---

## 4. Access Modifier

### Field

- 모든 필드는 접근 제한자를 명시한다.

### Method

- 모든 메서드는 접근 제한자를 명시한다.
- 단, Unity Lifecycle, Callback 메서드는 접근 제한자를 생략한다.
- 단, Unity Lifecycle, Callback 메서드에 virtual, override, abstract 등 Method Modifier을 사용하는 경우에는 접근 제한자를 명시한다.

---

## 5. Using

`using`은 다음 순서로 작성한다.

1. `System`
2. `UnityEngine`
3. Unity 패키지
4. 외부 패키지
5. 프로젝트 namespace

각 그룹 사이에는 한 줄을 넣는다.

```csharp
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using Cinemachine;

using Game.Player;
using Game.System;
```

- 사용하지 않는 `using`은 제거한다.
- 같은 그룹 안에서는 알파벳 순서로 정렬한다.

---

## 6. Naming

- private 필드: `_camelCase`
- public 프로퍼티: `PascalCase`
- 메서드: `PascalCase`
- 지역 변수: `camelCase`
- 상수: `SCREAMING_SNAKE_CASE`
- enum 타입: `PascalCase`
- enum 값: `PascalCase`

---

## 7. Scope

- 요청받은 기능과 직접 관련된 코드만 수정한다.
- 요청받지 않은 클래스, 메서드, 변수의 이름을 변경하지 않는다.
- 요청받지 않은 리팩터링을 하지 않는다.
- 기존 동작을 변경하는 코드는 요청된 경우에만 수정한다.
- 테스트 코드 작성 금지

## 8. Code Structure

- Unity Lifecycle 순서와 Inspector 설정을 고려하여 불필요한 `null` check를 추가하지 않는다.

## 9. 애셋 생성 및 Inspector 설정

- 위치, 회전, 크기, 카메라 구도, 연출 시간 등 제작자가 조절할 값은 Inspector에 노출한다.
- 씬과 프리팹에 저장된 설정값을 기준으로 동작한다.
- Awake, OnEnable, Start 등에서 제작자가 설정한 Transform이나 카메라 값을 하드코딩된 값으로 덮어쓰지 않는다.
- OnValidate는 해당 컴포넌트의 입력값 검증에 사용하며, 다른 오브젝트의 Transform이나 카메라 설정을 자동 변경하지 않는다.
- 초기 배치는 애셋 생성 시 한 번 설정하고, 이후 수동 편집한 값을 보존한다.
- 자동 배치나 자동 크기 조절이 필요하면 명시적인 옵션으로 제공하고 기본값은 비활성화한다.
- 자동 배치는 별도 실행 기능으로 제공하여 제작자가 요청할 때만 적용한다.
- 기능상 반드시 필요한 이동과 초기화는 Inspector에 지정된 기준점과 설정값을 사용한다.
- 서로 독립적으로 편집해야 하는 기계, 종이, 카메라의 설정을 분리한다.

## 10. Spec-Driven Development (SDD)

- 기능 구현 전에 게임 컨셉 문서 `design/game-brief.md`를 먼저 읽고, 바뀌는 규칙은 그 문서부터 고친다.
- 스펙 없이 기능 구현을 시작하지 않는다. 기능 스펙은 `docs/specs/NNN-*.md`이며 작성 규칙·절차·목록은 `docs/README.md`, 양식은 `docs/spec-template.md`를 따른다. 게임 규칙의 원본은 `design/game-brief.md`다.
- 구현은 승인된 스펙의 `FR-xxx` 단위로 하고, 끝나면 FR별 충족 코드를 보고한다. `production/epics`의 옛 스토리 번호는 새로 만들지 않는다.
- 커밋 메세지 제안 시 스펙 작업은 스펙 번호를 범위로 쓰고(예: `feat(001): 잔해 치우기 비용표 적용`), 스펙 없는 정리 작업은 범위 없이 쓴다(예: `chore: 옛 데모 인자 정리`).

## 11. 이 프로젝트 (황동골 철도 회사)

- **게임 기준 문서**: `design/game-brief.md` (루프, 해금표, 사건, 수치 위치, 다음 할 일).
- **엔진**: Unity 6000.3.27f1 (6.3 LTS). Unity 6 API는 `docs/engine-reference/unity/`를 먼저 확인한다.
- **코드 위치**: `Assets/Scripts/<시스템>/` (네임스페이스 `IronRing.*`). 공유 계약은 `Assets/Scripts/Contracts/IronRingContracts.cs`(멤버는 끝에만 추가, 새 멤버는 기본 구현). 조립은 `Assets/Scripts/Bootstrap/`, 서비스는 각 `*Installer`가 `GameServices.Composing`에서 등록하고 `GameServices.Ready` 뒤에 `GameServices.Get<T>()`로 얻는다.
- **데이터**: 게임 수치는 각 시스템 `Resources/**.json`. 한국어 문구는 `Assets/Scripts/Localization/Resources/ko.json`(끝에 추가, 편집 직전에 다시 읽기). 콘텐츠는 스팀펑크 마을 요소만.
- **씬·프리팹**: 플레이 씬 `Assets/Scenes/Main.unity`. 편집기가 열려 있는 동안 `.unity`·`.prefab` YAML을 디스크에서 직접 고치지 않는다(편집기 Reload 창이 떠서 브리지가 멈춘다). 프리팹·씬 변경은 편집기 생성기로만 한다.
  - UI 프리팹: `.bridge/rebuild/<화면Id>` 빈 파일을 만들고 `IronRing.Hud.Editor.BuildMainCanvas.Generate` 실행(그 화면만 GUID 유지하고 재생성).
  - 그 밖 생성기: `IronRing.Bootstrap.Editor.BuildTopDownMain.Rebuild`(파괴적), `IronRing.Presentation.Interior.Editor.Story012Build.Generate`, `IronRing.Presentation.Interior.Editor.RuinBuild.Generate`/`GenerateMap`(멱등).
- **UI**: UGUI. `MainCanvas` 프리팹 하나 아래에 화면마다 프리팹(`Assets/Prefabs/UI/Screens/`), 빌더는 `Assets/Scripts/Hud/Editor/Screens/<Id>ScreenBuilder.cs`, 색·글자는 `UiTheme.asset`. 런타임에 UI를 코드로 만들지 않는다.
- **편집기 제어와 검증은 Unity Pipeline CLI(`unity`)만 사용한다. Python 스크립트와 옛 파일 큐 브리지는 사용하거나 다시 만들지 않는다.** 편집기는 사용자가 열어 둔다. 닫거나 batchmode로 띄우지 않는다. 여러 편집기가 열려 있으므로 `--project-path <저장소 루트>`를 지정한다. `<저장소 루트>`는 작업 중인 이 저장소의 절대 경로이며 `git rev-parse --show-toplevel`로 확인하고, `unity status` 목록에서 같은 경로의 편집기만 대상으로 삼는다.
  - 연결 확인: `unity status --format json`
  - 컴파일: `unity command script recompile --project-path <저장소 루트> --format json` 후 `unity command script recompile_status --project-path <저장소 루트> --format json`에서 완료·오류 여부 확인.
  - 씬 저장 상태: `unity command scene list_open --project-path <저장소 루트> --format json`에서 `Main.unity`의 `isDirty:false` 확인.
  - 생성기: `unity command script eval`에서 편집기 정적 메서드를 호출한다. 실행 전에 Play Mode 여부와 대상 메서드의 파괴적 동작을 확인한다.
  - Play Mode·캡처: `unity command editor.playmode start|stop`, `unity command capture game_view`를 사용한다. 데모 상태와 실제 입력 검증은 별도로 기록한다. 로그는 `%LOCALAPPDATA%/Unity/Editor/Editor.log`에서 `LOOP:`·`UNLOCK:`·`LINE:` 등으로 찾는다.
  - 지금 쓸 수 있는 데모 인자: `--loop-demo`, `--unlock-demo [--unlock-stage start|first|all]`, `--line-demo --line-stage locked|opened`, `--town-demo`, `--economy-demo ledger`.
- **자동 테스트는 만들지 않는다.** 증거는 Pipeline 결과와 캡처. 캡처는 `production/qa/evidence/`에 둔다.
- **보관**: 제거한 기능은 `archive/`(Assets 밖)로 옮긴다. 기록(옛 스토리·의뢰서·증거)은 `production/`에 있다 — 참고용이며 현재 규칙은 `design/game-brief.md`가 우선한다.

