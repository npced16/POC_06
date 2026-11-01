# Story 022 — 2-A단계(TopBar·HubPanel·Defeat·PhaseBanner·월드 라벨·CombatFx) 검증 기록

날짜: 2026-11-01 · 담당: ui-programmer(2-A) · 편집기: Unity 6000.3.27f1(열린 편집기, 브리지로만 조작) · 커밋 없음

## 화면

| Id | AlwaysOn | 런타임 스크립트 | 옛 코드 | 내용 |
|---|---|---|---|---|
| TopBar | 예 | `Assets/Scripts/Hud/TopBarScreen.cs` | KoreanHud 상단 바·#actions | 위 띠(현재 위치, 바퀴, 도감 (C) → `CodexHud.Toggle`), 왼쪽 아래 다음 라운드(전투 중에만)·출발. 꺼진 버튼 글자는 테마 Muted |
| HubPanel | 아니오 | `Assets/Scripts/Hud/HubPanelScreen.cs` + 부품 `FacilityRow.cs` | KoreanHud #panel | 거점 이름·닫기, 번영·주민, 서식지, "시설" + 시설 줄(HudCatalog 시설 수만큼 `FacilityRow.prefab`을 빌더가 미리 둠), 안내 문구. `IHubClickSource.HubClicked`로 열리고 닫기·거점 내부·패배 때 닫힘 |
| Defeat | 아니오 | `Assets/Scripts/Hud/DefeatScreen.cs` | KoreanHud #defeat | 클릭을 막는 그늘(테마 Panel, α 0.86) + 카드(사유별 제목 `IRunLossInfo` → phase_loss_deadline / phase_loss_collapse / defeat_title, 본문) |
| PhaseBanner | 예 | `Assets/Scripts/Hud/HubPhase/PhaseBannerScreen.cs` | PhaseHud 띠·기한·패배 사유 | "거점 · 단계 · 번영 N · 서식지 N"(단계별 글자색은 컴포넌트의 `_phaseColors`), 정착 기한 상자, `IRunLossInfo`가 없을 때만 보이는 패배 사유 줄. 칸 클릭 건설 메뉴는 옮기지 않음(`_buildMenuOnCellClick`=false, true면 경고 로그) |
| CombatFx | 예 | `Assets/Scripts/Hud/CombatFxScreen.cs` + 부품 `FloatingMessageView.cs` | StateCombatEffects의 UIDocument | `FloatingMessage.prefab` 6개 풀(같은 group은 교체, 모자라면 가장 오래된 것 재사용), `Camera.WorldToScreenPoint` → `RectTransformUtility.ScreenPointToLocalPointInRectangle`(Overlay라 카메라 null)로 매 프레임 배치. 그늘 + 패배 제목·본문(`IRunLossInfo`가 없을 때만) |

정적 API: `CombatFxScreen.Available`, `CombatFxScreen.Message(world, text, group, seconds, riseSpeed, camera)`, `CombatFxScreen.ShowShade(title, body, color)`.

## 월드 공간 지도 라벨

- `Assets/Scripts/Presentation/Map/MapLabel.cs`: `SetName(text)`, `SetPhase(HubPhase, text)`(단계별 색 `_phaseColors`). Awake에서 아직 이름을 받지 않았다면 인스턴스의 옛 TextMesh 이름(지도 생성기가 넣은 한국어 이름)을 옮기고, 단계 예시 문구는 지운다.
- `Assets/Scripts/Presentation/Map/Editor/MapLabelsBuild.cs`(`IronRing.Presentation.Map.Editor.MapLabelsBuild.Generate`): HubBuilding.prefab에 `NameLabel`(y -1.75)·`PhaseLabel`(y -2.25), WaypointMarker.prefab에 `NameLabel`(y -0.95). TextMeshPro 3D, 테마 글꼴, 크기 5/4, sortingOrder 30(건물 10·연기 11 위), 외곽선 머티리얼 `Assets/Prefabs/Map/MapLabelOutline.mat`(글꼴 머티리얼 복제, OUTLINE_ON 0.22, 테마 Panel 색). 옛 `Name` TextMesh는 지우지 않고 MeshRenderer만 끔(`BuildTopDownMain`이 `GetComponentInChildren<TextMesh>()`로 이름을 넣기 때문). 빠진 것만 더하고, 바뀐 프리팹만 저장.
- `RingHubPhaseView.Apply`: 거점 앵커의 MapLabel에 이름(`hub-1` → `hub_1`)과 단계 문구·색을 쓴다(단계가 바뀔 때마다).

## 옛 UI Toolkit 경로 차단(UiScreens.Instance != null이면 만들지 않음)

- `KoreanHud.Create`, `PhaseHud.Create`: 첫 줄 뒤에 `if (IronRing.Hud.Ui.UiScreens.Instance != null) return;`. 씬의 MainUI에 놓인 문서는 Awake에서 꺼진 채 남는다.
- `StateCombatEffects.Initialize`: `UiScreens.Instance == null`일 때만 UIDocument 오버레이를 만든다. 오버레이가 없으면 `Message` → `CombatFxScreen.Message`, `RunLost` 그늘 → `CombatFxScreen.ShowShade`. 스프라이트·잔해·게이지 연출은 그대로.

## 파일

새로 만든 파일:
- 런타임: `Assets/Scripts/Hud/TopBarScreen.cs`(+ `KoreanHudServices` 내부 도우미), `HubPanelScreen.cs`, `FacilityRow.cs`, `DefeatScreen.cs`, `CombatFxScreen.cs`, `FloatingMessageView.cs`, `Assets/Scripts/Hud/HubPhase/PhaseBannerScreen.cs`, `Assets/Scripts/Presentation/Map/MapLabel.cs`
- 편집기: `Assets/Scripts/Hud/Editor/Screens/TopBarScreenBuilder.cs`, `HubPanelScreenBuilder.cs`, `DefeatScreenBuilder.cs`, `PhaseBannerScreenBuilder.cs`, `CombatFxScreenBuilder.cs`, `ScreenArrayBinding.cs`(배열 필드 바인딩), `Assets/Scripts/Presentation/Map/Editor/MapLabelsBuild.cs`
- 에셋: `Assets/Prefabs/UI/Screens/TopBar·HubPanel·Defeat·PhaseBanner·CombatFx.prefab`, `Assets/Prefabs/UI/Parts/FacilityRow·FloatingMessage.prefab`, `Assets/Prefabs/Map/MapLabelOutline.mat`

수정한 파일: `KoreanHud.cs`, `PhaseHud.cs`, `StateCombatEffects.cs`, `RingHubPhaseView.cs`, `HubBuilding.prefab`, `WaypointMarker.prefab`, `MainCanvas.prefab`(Generate가 인스턴스 추가). ko.json에는 키를 더하지 않았다(필요한 키가 모두 있음).

## 브리지 결과

- `refresh`: 처음 두 번은 2-B 소유 `HotbarCellView.cs`(`PlacementEntry` 없음) 오류로 `failed:true`. 기다린 뒤 `failed:false`(2026-11-01T11:47Z 무렵). 2-A 파일 오류는 없었다.
- `BuildMainCanvas.Generate` 1회차 ok → MainCanvas.prefab에 다섯 화면 인스턴스가 하나씩(소스 프리팹 GUID로 확인). 2회차 ok.
- `MapLabelsBuild.Generate` 1회차 ok(두 프리팹에 라벨 추가, `_name`·`_phase` 바인딩 확인). 2회차 ok.
- 멱등: 2회차 전후로 MainCanvas, 화면 5개, HubBuilding, WaypointMarker, Main.unity의 md5가 같다(SAME).
- `scene-status`: `Assets/Scenes/Main.unity`, `isDirty:false`, `playing:false`.

## 캡처(편집 모드 Game 뷰, Play 아님)

`production/qa/evidence/story-022-ugui-screens/022-editmode-gameview-v4.png`. 옛 UI Toolkit 패널(도감·NPC 대화·InteriorHud·핫바 등)이 위에 겹쳐 그려지는 공존 상태다. 보이는 2-A 화면:
- TopBar: 위 띠의 "현재 위치: 주조소", 오른쪽 "도감 (C)", 왼쪽 아래 구리 버튼 "다음 라운드"·"출발" — 한글 정상
- PhaseBanner: "주조소 · 복구 중 · 번영 12 · 서식지 2"와 "정착 기한: 3바퀴 남음" 상자 — 한글 정상
- 월드 라벨: 아래쪽 거점 건물 밑의 "주조소 / 복구 중"(TMP 3D, 외곽선) — 핫바와 겹치는 위치라 디자이너 조정 필요
- HubPanel·Defeat는 상황 화면이라 꺼져 있고, CombatFx는 예시 문구 하나만 보인다(대부분 다른 패널에 가려짐)

## 코디네이터가 Play로 찍을 것

- 기본 실행 + 거점 클릭 → HubPanel(이름·번영·서식지·시설 3줄·닫기)
- `--map-depart` → TopBar 위치·바퀴 갱신, 출발 버튼이 꺼짐, 지도 라벨(거점 이름·단계, 경유지 이름)
- `--phase-demo`, `--deadline-demo` → PhaseBanner 띠 색·기한 상자, 지도 PhaseLabel 색
- 패배: Main 씬에서 패배를 일으키는 플래그(있으면) → Defeat 화면. `--hud-demo-defeat`는 HudDemoWorld(MainCanvas 없는 데모 씬일 수 있음)라 옛 KoreanHud 경로로 그려질 수 있다. MainCanvas가 있는 씬에서 확인 필요
- 전투 경유지 도착(또는 `--combat-result`류) → CombatFx 떠오르는 문구, TopBar "다음 라운드"
- 콘솔 예외가 없는지

## 남은 문제

- 월드 라벨 위치: 지도 생성기가 인스턴스마다 옛 TextMesh 위치를 건물 위/아래로 오버라이드했지만 새 라벨은 모두 건물 아래(프리팹 값)다. 위쪽 거점은 선로 쪽으로 겹칠 수 있다. 디자이너가 프리팹에서 조정하거나, `BuildTopDownMain`이 MapLabel 위치를 넣게 고친다(2-A 범위 밖이라 손대지 않음).
- 편집 모드에서 지도 이름 라벨은 프리팹 예시("주조소"/"북쪽 고개" 등)로 보인다. Play에서는 MapLabel이 옛 TextMesh의 이름이나 RingHubPhaseView 값으로 바꾼다.
- HubPanel에서 자원 기부·슬롯(옛 resource-choices·slots)은 옮기지 않았다(자원은 스토리 015에서 제거, 브리프의 HubPanel 항목에도 없음).
- PhaseHud의 칸 클릭 건설 메뉴는 옮기지 않았다(데이터로 꺼져 있음).
- `CombatFxScreen`(IronRing.Hud)을 `StateCombatEffects`(IronRing.Presentation)가 직접 부르는 역방향 참조. 어셈블리는 하나라 컴파일에는 문제없다.
- 3단계에서 `Assets/UI/` 정리 시 `HudCatalog.json`을 `.meta`와 함께 옮겨야 TopBar·HubPanel의 `_catalogJson` 참조가 유지된다(InteriorTitle과 같음).
- 단계 색(`_phaseColors`)은 테마가 아니라 PhaseBannerScreen·MapLabel 컴포넌트의 직렬화 값이다(옛 USS 색과 같음). 테마로 옮길지는 3단계에서 정한다.
