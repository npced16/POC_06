# 스토리 027 검증 기록 — 조합 건물·클릭 대화

작성: technical-artist(서브에이전트), 2026-11-01. 캡처는 코디네이터 몫이다(아래 "캡처할 것").

## 1. 만든 스프라이트 (`Assets/Art/Generated/buildings/`, 64px · PPU 64 · Point · 무압축)

- 지붕 4계열 × 이웃 마스크 16조각 = 64장: `bld_<machine|housing|storage|glass>_00..15`
  - machine = 쇠 골함석 + 황동 처마, housing = 슬레이트 비늘 기와 + 나무 처마, storage = 널빤지 + 쇠 처마, glass = 쇠 틀 유리판 + 구리 처마
- 오목 모서리 덧조각 4계열 × 4장 = 16장: `bld_<계열>_inner_<ne|se|sw|nw>`
- 문 2종 `bld_door_0`(아치 나무문), `bld_door_1`(리벳 쇠문), 굴뚝 `bld_chimney`
- 상징물 19종 `bld_sign_<id>`(서식지 종류당 1개, 기존 ArtKit 모티프 재사용)
- 콘택트 시트: `buildings_contact_sheet.png`(이 폴더에 복사함)

생성: `IronRing.Art.Editor.SpriteGenBuildings.Generate`(메뉴 IronRing/Art/Generate Building Sprites). 끝에서 `Story012Build.EnsureBuildingAssets()`를 불러 `BuildingPiece.prefab`·`BuildingProp.prefab`을 보장하고 InteriorCatalog 그림 표를 채운다(씬은 건드리지 않음).

## 2. 마스크 → 조각 고르는 방식

- 칸의 격자 좌표 = 칸 앵커 위치를 HubInteriorRoot 로컬로 바꿔 타일 크기로 나눈 정수(위/아래 구역이 선로로 떨어져 있어 자연히 이웃이 아님).
- 4방향 이웃 비트: 1 = 북, 2 = 동, 4 = 남, 8 = 서(그 방향에 같은 묶음 칸이 있음). 값 0..15가 그대로 조각 번호다.
  - 닫힌 쪽(비트 0)에는 처마 띠와 외곽선이 생기고, 열린 쪽은 지붕 무늬가 이어진다(무늬 주기 4·8·16px가 64를 나눠서 이음매 없음).
  - 남쪽이 닫힌 칸은 아래 16px가 정면 벽(창 2개) = 탑뷰 3/4 건물 앞면.
- 대각: 두 직교 이웃이 있고 그 사이 대각 칸이 비면 그 모서리에 오목 모서리 덧조각(0 ne, 1 se, 2 sw, 3 nw)을 얹는다.
- 문: 아래 가장자리(남쪽 이웃 없음) 중 맨 아랫줄에서 가로 중심에 가장 가까운 칸. 굴뚝: 맨 윗줄 오른쪽 칸 + 오프셋(+ `Smoke.prefab` 자식). 상징물: 묶음 무게중심에 가장 가까운 칸.
- 정렬 순서(카탈로그 데이터): 지붕 12, 문·오목 모서리 13, 굴뚝·상징물 14, 연기 15. 지붕 조각은 칸보다 z +0.05 뒤에 놓아 HubInteriorView의 칸 레이캐스트가 칸을 먼저 맞힌다.
- 등장 연출: `HabitatCompleted` 직후 세워진 건물만 0.3초 동안 0.3 아래에서 올라온다(`_buildingRiseSeconds`, 0이면 끔). 내부 화면을 다시 열 때는 연출하지 않는다.
- 갱신: 완성·깨짐·아이템 배치·제거·내부 열림이 있으면 다음 LateUpdate에 `CompletedAt(hub)`를 다시 읽어 "종류+칸 목록"이 바뀐 건물만 다시 세운다(완성 묶음에 칸이 붙는 S3-1 경로도 반영). 건물 칸의 ItemSlot 렌더러를 끄고, 매 동기화 때 되살린 뒤 다시 끈다(깨지면 타일이 보임).
- `ClusterFrameView`는 카탈로그에 건물 그림이 있으면(`HasBuildings`) 테두리를 건너뛴다(코드 경로는 유지).

## 3. 클릭·NPC·대화

- 건물 클릭: `InteriorNpcWalker.HandleClick`(빈 손·드래그 없음·UI 위 아님, NPC 클릭과 같은 규칙)에서 NPC 몸체를 먼저 보고, 없으면 `BuildingPiece`를 찾아 `INpcHomes.HomesAt(hub)` 중 종류가 같고 칸이 겹치는 집의 NPC로 `NpcClicked(hubId, npcId)`를 올린다 → 기존 `NpcDialogScreen`이 열림. 사는 NPC가 없으면 토스트 "`<종류 이름> — 아직 아무도 살지 않는다. 승객이 찾아오길 기다린다.`"(ko.json `building_empty`, 2.5초 = `_emptyBuildingToastSeconds`).
- 문 앞 NPC: `hub_phase_view.json` `_npcWanders:false`면 건물이 선 집의 NPC는 문 칸 아래(`_doorOffsetY` −0.42)까지 걸어가 카메라를 보고 숨쉬기 흔들림(`_breatheAmplitude` 0.03, `_breatheSpeed` 2.4). 도착 걷기(023)도 문 앞에서 끝난다. 건물이 없는 NPC는 예전 배회.
- 대화 패널(Simple 스킨): 폭 500, 제목 줄 전체 폭, "닫기"는 패널 오른쪽 위 모서리(pad 안쪽)에 고정. 본문은 왼쪽 초상 상자(80×80, 몸체 스프라이트 preserveAspect) + 오른쪽 대사·결과 열. 대사는 타자기(`_typeSpeed` 28자/초, 프리팹 값), 패널 위 왼쪽 클릭이면 즉시 완성, 같은 대사로 다시 그리면 다시 치지 않는다.

## 4. 파일

새 파일: `Assets/Scripts/Art/Editor/SpriteGenBuildings.cs`, `Assets/Scripts/Presentation/Interior/BuildingView.cs`, `Assets/Scripts/Presentation/Interior/BuildingPiece.cs`, `Assets/Prefabs/Interior/BuildingPiece.prefab`, `Assets/Prefabs/Interior/BuildingProp.prefab`, `Assets/Art/Generated/buildings/**`(103 PNG)
수정: `InteriorCatalog.cs/.asset`, `Editor/Story012Build.cs`, `ClusterFrameView.cs`, `HabitatCatalog.cs`, `HabitatTypes.json`(19종 `_buildingStyle`·`_signProp`), `InteriorNpcWalker.cs`, `HubPhaseViewSettings.cs`, `hub_phase_view.json`, `NpcDialogScreen.cs`, `NpcDialogScreenBuilder.cs`, `Assets/Prefabs/UI/Screens/NpcDialog.prefab`(재생성), `ko.json`(끝에 키 1개 추가), 데모 `HabitatDemoDriver.cs`·`HabitatDemo.json`(complete 단계의 가스등 거리를 L자로: 위(3,8)(3,9)(2,9), 새 단계 `furnace` = 위 줄 1..3 × 칸 5..7, 가운데 굴뚝).

## 5. 브리지 결과

- `refresh`: 첫 시도는 028 소유 `RunResultScreen.cs(99,41) CS0118`로 실패, 1분 뒤 재시도 `failed:false, errors:""`.
- `execute-method IronRing.Art.Editor.SpriteGenBuildings.Generate`: ok(103 PNG, 카탈로그 표·프리팹 참조 채움).
- `.bridge/rebuild/NpcDialog` 표식 + `execute-method IronRing.Hud.Editor.BuildMainCanvas.Generate`: ok, 표식 소비됨, NpcDialog.prefab에 `_panel`·`_portrait`·`_typeSpeed: 28` 연결.
- `compile-status`: `failed:false`. `scene-status`: Main.unity `isDirty:false`.

## 6. 캡처할 것 (코디네이터)

1. `--habitat-demo` → L자 가스등 거리 + 배관/기관실 + 보일러실 건물 3채(콘솔 `BUILDING:` 줄 3개).
2. `--habitat-demo --habitat-stage furnace` → 3×3 용광로 건물(machine 지붕, 굴뚝 더미 상징물).
3. `--npc-demo --npc-stage panel` → 대화 패널(초상·타자기 중간). 건물 클릭 경로는 플레이 중 건물을 직접 클릭해 확인.
4. 빈 건물 안내: `--habitat-demo`에서 NPC가 살지 않는 건물을 클릭 → 토스트(콘솔 `BUILDING CLICK: ... 사는 NPC 없음`).

## 7. 남은 문제

- 플레이 확인은 하지 않았다(play-capture는 코디네이터 몫). 마스크 타일링·문 위치·NPC 문 앞 대기·타자기는 캡처로 확인해야 한다.
- 3×3 용광로 배치(철골 8 + 굴뚝 1)는 `iron_depot`(철골 3 + 굴뚝 1, 5칸 이상) 조건도 만족한다. 둘 중 어느 쪽으로 판정할지는 HabitatMatcher 우선순위가 정한다. furnace 단계 로그의 `완성 서식지`를 확인할 것.
- 건물 하나에 NPC가 여럿이면 문 앞에 겹쳐 선다(가로로 벌려 세우지 않음).
- `building_empty` 문구의 "—"는 사용자 문장 그대로 썼다. 폰트 SDF에 해당 글리프가 있는지 캡처로 확인할 것.
- NPC 몸체 스프라이트가 작으면 초상이 80px 상자 안에서 흐릿하게 커질 수 있다(Image preserveAspect, Point 필터는 텍스처 설정을 따름).

## 8. 1차 캡처 검수 후 수정 (2026-11-01)

1. **지붕이 칸보다 작고 이어지지 않음 / 2. 가운데 칸 지붕 빠짐**: 두 증상은 원인이 하나였다. 프리팹 스케일(1), PPU(64), PNG 크기(64×64), 타일 크기(1)는 모두 맞았다. 잘못된 곳은 격자 좌표 계산이었다. 열이 12개라 칸의 로컬 x가 ±0.5, ±1.5…처럼 반정수가 된다. 이것을 `Mathf.RoundToInt`(은행가 반올림, .5는 짝수로)로 바꾸면 이웃한 두 열이 같은 격자 키로 겹친다. 예를 들어 1.5와 2.5가 둘 다 2가 된다. 그 결과 사전에 덮어써진 칸에는 지붕이 생기지 않았다(기관실 15번, 용광로 가운데 열). 남은 칸은 동서 이웃이 없다고 판정돼 양옆에 처마 외곽선이 생겼고, 건물이 좁고 끊겨 보였다. `Mathf.FloorToInt(x + 0.5)`로 바꿨다(`BuildingView.Build`). 이제 모든 칸에 지붕이 놓이고, 안쪽 이음새 없이 건물 가장자리에만 외곽선이 생긴다. 상징물은 원래부터 지붕 위(정렬 14) 별도 조각이다.
2. **대화 패널 닫기 위치**: 프리팹에서는 이미 `Close`가 `Dialog` 바로 아래에 있고, 앵커와 피벗이 (1,1), 위치는 (−12,−12), `LayoutElement.ignoreLayout=1`이었다. 그런데도 캡처에서는 왼쪽 중간에 보였다. 브리지로는 런타임에 무엇이 버튼을 옮기는지 찾지 못했다. 의심 가는 곳은 Main 씬의 MainCanvas 인스턴스에 쌓인 RectTransform 재정의 227개다. 대응으로 `NpcDialogScreen.Initialize`에서 런타임에 다시 고정한다. 부모를 `_panel`로 두고, 앵커와 피벗을 (1,1)로, 위치를 `_closeInset`(−12,−12, 프리팹 값)으로 정하고, `ignoreLayout`을 켜고, 맨 마지막 형제로 보낸다. 다음 캡처에서 확인이 필요하다.

브리지: `refresh` failed:false → 표식 `NpcDialog` + `BuildMainCanvas.Generate` ok → `compile-status` failed:false → `scene-status` isDirty:false. 스프라이트와 Story012Build는 바뀐 것이 없어 다시 돌리지 않았다.
