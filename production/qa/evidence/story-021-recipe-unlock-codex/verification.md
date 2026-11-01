# 스토리 021 검증 — 레시피 해금과 도감

- 날짜: 2026-11-01
- 검증 방법: 열린 Unity 편집기(6000.3.27f1) 브리지. Unity 실행·batchmode 없음. 캡처는 코디네이터가 한다.

## 바뀐 것

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Contracts/IronRingContracts.cs` | `IHabitatCodex` 계약 추가: `IsUnlocked`, `UnlockedTypeIds`, `TryUnlock(typeId, source)`, `event RecipeUnlocked(typeId, source)` |
| `Assets/Scripts/Habitats/HabitatCodex.cs` (새) | `CodexUnlockSettings`(JSON 파서) + `HabitatCodex`. 시작 레시피는 생성 때 조용히 해금(토스트 없음). `ITrackRuntime.LapCompleted` → 바퀴당 1개, `IWaypointResolver.EncounterEnded` → `EventResolved` 1개 / `Victory` 1개 / `Defeat` 없음, `IHabitatClusters.HabitatCompleted`가 모르는 종류면 `discover`로 해금(완성은 막지 않음). 순서 목록 다음 → 남은 카탈로그 순, 19종을 다 알면 멈춤 |
| `Assets/Scripts/Habitats/HabitatsInstaller.cs` | `Install` 끝에 `InstallCodex()`: `Resources/Habitats/CodexUnlocks.json`을 읽어 `IHabitatCodex`로 등록, `HabitatsInstaller.Codex` 노출, SubsystemRegistration에서 정리 |
| `Assets/Scripts/Habitats/Resources/Habitats/CodexUnlocks.json` (새) | 시작 3종, 해금 순서 16종, `_perLap`/`_perEvent`/`_perVictory` = 1 |
| `Assets/Scripts/Habitats/Resources/Habitats/CodexDemo.json` (새) | 데모 거점·대기 프레임·바퀴 수·발견 배치(전신선: 전신주 2 + 가스등 1, 위층 3행 8~10열) |
| `Assets/Scripts/Habitats/Demo/CodexDemoDriver.cs` (새) | `--codex-demo --codex-stage start\|lap\|discover`, `Stage`/`IsStageReady`/`StageReady` |
| `Assets/Scripts/Hud/Codex/CodexHud.cs` (새) | 도감 패널. 018 패턴(정적 `_instance`, `ResetStatics`, `Boot`→`Ready`, 씬 컴포넌트 우선 `Create`, `Awake`에서 문서 끄기, `HudDocument.Bind(..., 130, "Hud/Codex")`, `OnDestroy` 구독 해제, `PlacementGhost.UiHitTests` 등록—닫혀 있으면 false). C 키 토글, Esc·닫기 버튼으로 닫기. 19칸 격자, 미해금 "???"+실루엣, 해금은 이름·종류·칸 수·타일 그림·번영 +N·찾아오는 NPC 직업, 현재 거점 완성 종류에 "완성" 배지. 해금 토스트는 큐에 넣어 다음 프레임 이후 핫바 토스트 자리에 차례로 |
| `Assets/UI/Hud/Resources/Hud/Codex.uxml`, `CodexStyle.uss` (새) | 예시 문구(가스등 거리·보일러실·급수탑 + "???" 5칸, `preview-only`)로 편집 모드 미리보기 |
| `Assets/Scripts/Hud/Hotbar/HotbarHud.cs` | `public void ShowToast(string)`, `public float ToastSeconds`. 토스트 요소를 문서 루트로 옮겨 지도 화면에서도 같은 자리에 보임(도감 토스트만; 기존 "완성!" 토스트는 내부 화면에서만) |
| `Assets/Scripts/Hud/KoreanHud.cs`, `KoreanHud.uxml` | 상단 바 `codex-open` 버튼("도감 (C)") → `CodexHud.Toggle` |
| `Assets/Scripts/Hud/Items/InventoryHud.cs`, `InventoryHud.uxml` | "서식지 조건" 패널(`inv-habitat`, 태그 진행도·완성 목록)과 채우는 코드 제거. 나머지 그대로(옛 USS 규칙은 색 파일이라 손대지 않음, 쓰이지 않을 뿐) |
| `Assets/Scripts/Hud/HubPhase/PhaseHud.cs`, `PhaseHud.uxml` | 배너 끝에 "  ·  서식지 N"(현재 거점 `IHabitatClusters.CompletedAt` 수), 완성·깨짐 때 갱신 |
| `Assets/Scripts/Hud/Editor/BuildMainUi.cs` | `CHILDREN`에 `("Codex", CodexHud, "Codex.uxml", 130)` |
| `Assets/Prefabs/UI/MainUI.prefab` | `Codex` 자식(UIDocument sortingOrder 130 + CodexHud) — Generate가 추가 |
| `Assets/Scripts/Localization/Resources/ko.json` | `codex_*` 15개, `phase_habitats` |

## 해금 순서

- 시작: 가스등 거리(lamp_street), 석탄 야적장(coal_yard), 보일러실(boiler_room)
- 순서(바퀴·이벤트·승리 공용): telegraph_line → cistern → pipe_gallery → toolshop → cogworks → cobble_plaza → iron_depot → greenhouse → barracks → water_tower → lamp_road → engine_room → repair_bay → power_house → blast_furnace → grand_cogworks
- 작은 혼합 묶음(3~4칸) → 5~6칸 묶음 → 패턴 → 용광로(9칸 패턴) → 대톱니 공방(9칸 같은 타일)

## 브리지 결과

- `refresh`: `{"compiling":false,"errors":"","failed":false}`
- `execute-method IronRing.Hud.Editor.BuildMainUi.Generate`: ok
- `scene-status`: `isDirty:false`, `Assets/Scenes/Main.unity`
- `MainUI.prefab`: `m_Name: Codex` 있음, `m_SortingOrder: 130`
- Generate 재실행: `MainUI.prefab`·`Main.unity` md5 동일(멱등), `isDirty:false`
- `compile-status`: `failed:false`
- 참고: `Main.unity`의 작업 트리 변경은 병렬 019(아트) 작업 몫이 섞여 있다. 씬의 MainUI는 프리팹 인스턴스라 Codex 자식은 프리팹 쪽 변경으로만 들어간다.

## 캡처할 것(코디네이터)

1. `--codex-demo --codex-stage start` — 런 시작 상태로 도감이 열림: 해금 3/19, "???" 16칸
2. `--codex-demo --codex-stage lap` — 바퀴 완료 신호 1회 후 토스트 "새 레시피: 전신선"(패널 위쪽 토스트 자리) + 도감 4/19에 전신선 추가. 토스트는 데이터 `ToastSeconds` 동안만 보이므로 대기 프레임을 짧게(기본 90프레임 근처) 잡는다
3. `--codex-demo --codex-stage discover` — hub-1 내부에서 전신선 타일을 놓아 완성 → 토스트 "발견: 전신선", 도감에 전신선 + "완성" 배지, 배너 "서식지 1"
4. 편집 모드: `execute-method IronRing.Hud.Editor.BuildMainUi.CaptureGameView` — Game 뷰에 도감 예시 패널

## 남은 문제 / 메모

- lap 단계는 실제 열차를 한 바퀴 돌리지 않고 `HabitatCodex.OnLapCompleted`(=`ITrackRuntime.LapCompleted` 구독 경로)를 직접 부른다. PhaseDemoDriver가 `RunState.OnLapCompleted`를 직접 부르는 것과 같은 방식.
- 시작 레시피는 이벤트를 내지 않으므로 토스트가 없다(의도).
- 자동 테스트 없음(지시). Play 실행 관찰은 코디네이터 캡처로 확인 필요.
- 상단 바 도감 버튼 스타일은 기존 `KoreanHudStyle.uss`(색 파일, 019 소관)를 건드리지 않으려고 UXML 인라인 크기만 지정했다.

## 코디네이터 검수 (2026-11-01 20:10 KST)

- 브리지 `play-capture` 3장: `021-codex-start.png`(150프레임) 도감 3/19·"???" 16칸·타일 아이콘 카드, `021-codex-lap.png`(100프레임) "새 레시피: 전신선" 토스트와 4/19, `021-codex-discover.png`(100프레임) "발견: 전신선"·배너 "서식지 1"·4/19. 로그 `CODEX DEMO: 배치 tile_wire → 위(3,8) 칸 44 = True` … `완성 서식지 telegraph_line`, `해금 4/19`.
- 편집 모드: Unity 창을 앞으로 가져온 뒤 `BuildMainUi.CaptureGameView` → `021-editmode-codex.png`(018 폴더에 저장된 파일을 옮김). Play 없이 Game 뷰에 도감 예시 카드(가스등 거리 "완성" 배지·보일러실·급수탑)와 019 아트가 함께 보인다.
- Editor.log 게임 코드 예외 없음. `NotoSansKR.asset` 재직렬화 변경은 커밋에서 제외.
- 관찰: 도감 카드의 패턴 그림이 아이콘 대신 글자 약자("보·석·물·철")로 나오는 것은 편집 모드 예시(preview-only)뿐이고 Play에서는 타일 아이콘이 나온다.
