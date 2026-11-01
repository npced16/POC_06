# codex 상시 역할: UI와 아이콘 전담

작성: 코디네이터(Claude), 2026-11-02 05:15. 의뢰자: 사용자("아이콘이랑 UI 쪽은 싹 다 codex한테"). 대상: codex.

## 역할
이 프로젝트(Unity 6000.3.27f1, 2D URP, **UGUI + TextMeshPro**, 한국어)의 **모든 UI 화면과 아이콘**을 맡는다. 로직·데이터는 Claude 에이전트가 만들고, 화면이 붙을 API를 `production/handoffs/codex-ui-<스토리번호>.md`의 "로직 API" 절에 적어 준다. codex는 그 절과 스토리 파일의 UI 수용 기준을 보고 화면을 만든다.

## 소유 경로(여기만 수정)
- `Assets/Scripts/Hud/**` (화면 스크립트: `<Id>Screen.cs`, 행 뷰 등) — 단, `Assets/Scripts/Hud/Editor/BuildMainCanvas.cs`의 `SCREEN_IDS` 표에 화면 id를 추가하는 것 포함
- `Assets/Scripts/Hud/Editor/Screens/<Id>ScreenBuilder.cs` (프리팹 생성기, 리플렉션으로 발견됨: `public static GameObject Build(UiTheme theme)` 패턴 — 기존 빌더를 복사해서 시작)
- `Assets/Prefabs/UI/**` (MainCanvas 프리팹, 화면 프리팹 `Screens/<Id>.prefab`, 부품 `Parts/`), `Assets/Prefabs/UI/UiTheme.asset`
- `Assets/Art/Generated/icons/**` + `Assets/Scripts/Art/Editor/SpriteGenIcons.cs`(아이콘 생성기) 또는 이미지 생성 도구로 만든 PNG(투명 배경, 64×64, Sprite/Point/압축 없음, PPU 64)
- `Assets/Scripts/Localization/Resources/ko.json` — **끝에 덧붙이기만**(키 중복 금지, `Strings`가 거부함). UI 문구 키는 `ui_<screen>_<name>`.
- 금지: `Assets/Scripts/Economy/**`, `Contracts/**`, `Combat/**`, `Hub/**`, `Npc/**`, `Habitats/**`, `Items/**`, `Track/**`, `Presentation/**`(지도·내부 월드는 Claude), `design/**`, `production/epics/**`. 필요한 API가 없으면 `production/handoffs/codex-ui-<번호>.md` 끝에 "요청" 절로 적어 두고(코디네이터가 읽음) 가능한 부분부터 진행.

## UI 규칙(사용자 결정)
- UGUI 캔버스 프리팹 `MainCanvas` 하나(씬 인스턴스), 화면마다 프리팹 하나를 자식 인스턴스로 두고 `UiScreens.Show/Hide`로 켜고 끈다(`Assets/Scripts/Hud/Ui/`: `UiScreens`, `UiScreen`, `ScreenTicker`, `ToastScreen`, `KoreanFont`). 스크립트는 `[SerializeField]` 참조로 바인딩만, **런타임에 UI를 코드로 만들지 않는다**(생성은 편집기 빌더에서만).
- 스킨 Simple(단색 패널), 글자 4단계 Title 28 · Heading 22 · Normal 18 · Small 14, 줄 높이 ×1.5, 버튼 36/28. 색·크기는 `UiTheme`에서. 글꼴 `Assets/Fonts/NotoSansKR SDF.asset`(TMP). 참고 화면: `Assets/Scripts/Hud/Editor/Screens/HubPanelScreenBuilder.cs`, `CodexScreenBuilder.cs`, `PassengersScreenBuilder.cs`.
- 아이콘: 64×64 스팀펑크 톤(황동·구리·녹·어두운 외곽선, 2~3단 명암), 팔레트 `Assets/Art/Palette/SteampunkPalette.json`. 품목·상태·버튼 아이콘은 `IconCatalog.asset`(ScriptableObject, codex가 만듦: id → Sprite)에 등록해 화면 스크립트가 id로 찾는다.

## 작업 방법
- Unity 편집기는 사용자가 열어 둔 상태. **Unity를 새로 띄우거나 batchmode 금지.** 임포트·컴파일·생성기는 브리지로만: `py -3 -I tools/bridge/bridge.py refresh --timeout 600`(결과 JSON `failed:false` 확인), `py -3 -I tools/bridge/bridge.py execute-method --method IronRing.Hud.Editor.BuildMainCanvas.Generate --timeout 600`(화면 하나를 다시 만들려면 먼저 표식 파일 `.bridge/rebuild/<Id>`를 만든다), `scene-status`(isDirty=false 확인). 편집기가 Play 모드면 "Command requires Edit mode" — 30~60초 뒤 재시도. `play-capture`는 쓰지 않는다(사용자가 편집기에서 직접 본다).
- 다른 에이전트(Claude)가 같은 폴더에서 로직을 고치는 중이라 컴파일이 그쪽 파일로 잠시 깨질 수 있다: 1분 기다렸다 재시도. **git 명령 금지**(checkout/commit/branch/push). 커밋은 코디네이터가 한다.
- 끝나면 `production/qa/evidence/story-<번호>-*/verification-ui.md`에 만든 화면·아이콘 목록, 바인딩한 API, 브리지 결과를 적고 마지막 메시지로 요약.

## 새 작업 받는 법
코디네이터가 이 터미널에 "codex-ui-<번호>.md 읽고 진행" 한 줄을 보낸다. 그 파일의 "UI 수용 기준"과 "로직 API" 절을 읽고 시작한다. 로직 API 절이 비어 있으면 아이콘·레이아웃(빈 데이터로 보이게)부터 만들고, API가 채워지면 바인딩한다.
