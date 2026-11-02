# Story 051 UI 검증 기록

## 구현

- 기존 `PhaseBannerScreen` 중앙 배너에 `IPhaseLoop.PhaseChanged`를 연결했다. Ready 시점의 `Phase`와 `Lap`도 읽어 첫 건설 페이즈를 표시한다. 표시 시간은 Inspector의 `_loopBannerSeconds` 기본 2초이며 기존 등급·선로 배너 큐를 재사용한다.
- `TopBarScreen`과 `TopBar.prefab`에 짧은 건설/순환 표시를 추가했다. 출발 버튼은 `IPhaseLoop.CanDepart`일 때만 보이고, 클릭은 `IPhaseLoop.Depart()`로 보낸다. 순환 중 열린 다른 마을 정차도 이 계약을 따른다. 옛 휴식지 결과 토스트는 `IRestEvents`가 있을 때 숨겼다.
- 새 `RestEventScreen`과 `RestEvent.prefab`을 MainCanvas의 마지막 화면으로 등록했다. 전체 화면 그늘이 뒤 입력을 막는다. 760×500 중앙 패널에 제목, 상황 글, 세로 선택지 3개를 고정 프리팹으로 두었다. 불가한 선택지는 이유를 함께 표시하며 비활성화한다. 선택 후 결과·상세와 [계속] 버튼으로 바뀐다. 런타임 UI 생성은 없다.
- `HotbarCellView`, `HotbarSlot.prefab`, `PaletteItem.prefab`에 재고 `×N` 배지를 추가했다. `ITileStock.Changed`로 핫바와 팔레트를 갱신하며 재고가 있으면 가격 줄을 `재고 N`으로 바꾼다. 재고가 0이면 기존 가격을 표시한다.
- UI 문구 키 `loop_phase_build`, `loop_phase_circulate`, `loop_phase_short_build`, `loop_phase_short_circulate`, `loop_continue`, `stock_badge`, `stock_price`는 로직 작업에서 이미 `ko.json` 끝에 추가돼 있어 중복 추가하지 않았다. 새 아이콘은 없다.

## 연결 API

- `IPhaseLoop`: `Phase`, `Lap`, `CanDepart`, `Depart`, `PhaseChanged`
- `IRestEvents`: `IsActive`, `Title`, `Text`, `Choices`, `IsChoiceAvailable`, `UnavailableReason`, `HasResolved`, `ResultText`, `ResultDetail`, `Choose`, `Continue`, `EventStarted`, `EventResolved`, `EventClosed`
- `ITileStock`: `CountOf`, `Changed`

## 브리지 결과

- 첫 refresh `2c834e3e9eaa4811b079e358e884a32b`: 새 화면의 `GameServices` using 누락으로 실패. 수정 후 refresh `01156faeed61441dbfeebb0b1bf9c025`: `compiling:false`, `errors:""`, `failed:false`.
- 생성 전 `scene-status` `3de7d6bd86bf4370abe36bebc60d3989`: `Assets/Scenes/Main.unity`, `playing:false`, `isDirty:false`.
- `.bridge/rebuild/TopBar`, `Hotbar`, `RestEvent`, `HotbarSlot`, `PaletteItem` 표식을 만들고 `BuildMainCanvas.Generate` 실행 `7c067adf0872496aa703091a7583220a`: `ok:true`. 표식 5개 모두 소비됨.
- 생성 후 `scene-status` `129027512f3b408eadd034dd683447c9`: `playing:false`, `isDirty:false`. 생성 후 refresh `3854980ed31246a5a1fc9a34fbcfbb4d`: `compiling:false`, `errors:""`, `failed:false`.
- 마지막 소스 주석·필드 이름 정리 후 refresh `bf69af97b6ca420cb0f4320bf43f8502`: `compiling:false`, `errors:""`, `failed:false`.
- 배너 시간 필드 추가 후 refresh `6cb4956e10aa475ea0e4760e09b9d06d`: `compiling:false`, `errors:""`, `failed:false`. `scene-status` `69f6a803d20e4496a73be972d673741e`는 `playing:false`, `isDirty:false`. `.bridge/rebuild/PhaseBanner` 표식으로 빌더 `d9e1e8afdb474c39a5b4e0ca3f2e8142`를 실행했고 표식 소비 및 프리팹 `_loopBannerSeconds: 2`를 확인했다. 이후 `scene-status` `2df946260ce94ff093f9abab3a6cbfe0`는 `playing:false`, `isDirty:false`.
- 프리팹 직렬화 참조 확인: `RestEvent`의 제목·상황·선택지·결과·계속, `TopBar`의 페이즈 라벨, 핫바 두 부품의 재고 배지. `MainCanvas.prefab`에 `RestEvent` 등록 확인. 씬 YAML은 직접 편집하지 않았다.

## 남은 화면 확인

컴파일과 편집 모드 프리팹 연결은 확인했다. Play Mode 화면·선택지 클릭·1280×720 글자 줄바꿈·재고 0/양수 전환은 이번 UI 검증에서 직접 보지 못했다. `--loop-demo`에서 첫 건설 배너, 휴식지 선택 전 사건 창, 선택 후 결과와 [계속], 복귀 후 재고 배지를 확인하고 화면 캡처를 추가해야 한다.
