# 스토리 019 검증 기록: 탑뷰 스팀펑크 환경 아트

날짜: 2026-11-01 · 편집기: Unity 6000.3.27f1(열린 편집기, 브리지로만 조작)

## 바뀐 것

- **팔레트 데이터** `Assets/Art/Palette/SteampunkPalette.json`: 브리프 색 21개(outline·mortar·ground·sandstone·brick·wall·wall_alt·roof·copper·brass·smoke·glow·water·shrub·moss·wood·iron·ember·fabric·background·ui_panel)를 이름 붙여 두고, 색마다 light/base/shade 세 단계를 둔다. 생성기는 `ArtKit.Load()`로 이 파일만 읽고, 코드에는 색 상수가 없다.
- **공용 도구** `Assets/Scripts/Art/Editor/ArtKit.cs`: Swatch, SteampunkPalette 로더, PixelCanvas(알파 합성, 외곽선 레이어)와 모티프(돌바닥, 벽돌, 슬레이트·골함석 지붕, 호박색 창, 굴뚝, 톱니, 상자, 배럴, 관목, 가스등, 연기, 폐허화)를 담는다. 임포터는 PPU 64, Point 필터, 무압축이다.
- **생성기 64×64 / PPU 64**:
  - `SpriteGen`: 선로 6, 거점 4와 폐허 4, 열차, 경유지, npc, raider, wall, turret, workshop, 번영 게이지 2, 내부 4.
  - `SpriteGenTiles`: 타일 13종. 그림이 없는 새 타일 id는 예외 대신 경고를 남기고 기본 리벳 판으로 그린다.
  - `SpriteGenItems`: 옛 아이템 12종. 예전 실패 원인은 카탈로그의 타일 id까지 그리려던 것이었고, 이제 타일은 건너뛴다. ItemSprites.asset은 SpriteGenTiles가 채운다.
  - 새 생성기 `SpriteGenFx`: 연기 6프레임, `fx/smoke_0..5.png`.
  - 새 생성기 `SpriteGenProps`: `props/prop_crate·prop_barrel·prop_shrub·prop_lamp_post.png`.
  - 메뉴는 `IronRing/Art/`에 있다.
- **거점 모양**: 거점마다 건물이 다르다.
  - hub_1 시청: 슬레이트 지붕, 둥근 황동 창, 톱니.
  - hub_2 주조소: 구리 보일러 탑, 골함석 별채, 관.
  - hub_3 창고 부두: 긴 골함석 지붕, 큰 나무문, 기중기, 청록 물길.
  - hub_4 역 요새: 구리 돔과 모서리 탑.
  - `_ruined`은 같은 건물을 어둡게 하고, 윗선을 들쭉날쭉 깎고, 지붕에 구멍과 부러진 들보를 넣는다. 창은 꺼지고 앞에 잔해가 있다.
  - 굴뚝 머리는 네 거점 모두 같은 자리(x 42~48, 윗줄 y 58)라서 프리팹의 연기 하나로 맞는다.
- **GUID 보존**: PNG 파일 이름과 .meta GUID는 그대로다. git diff에서 .meta 55개는 `spritePixelsToUnits: 32 → 64` 한 줄씩만 바뀌었다. 월드 크기(1칸 = 1유닛)는 그대로다.
- **연기**:
  - `Assets/Prefabs/Fx/Smoke.prefab`은 SpriteRenderer(smoke_0)와 새 런타임 컴포넌트 `IronRing.Presentation.Effects.SpriteFrameLoop`(6프레임, 초당 8프레임)로 되어 있다.
  - `HubBuilding.prefab`의 `Sprite` 자식 아래(로컬 0.21, 0.6, 크기 0.45, order 11)와 `Train.prefab` 루트 아래(0, 0.62, 크기 0.5, order 21)에 `Smoke` 자식으로 붙였다.
  - `RingHubPhaseView.Apply`는 스프라이트를 바꿀 때 `Smoke` 자식도 함께 켜고 끈다. Recovering/Settled이면 켜고, Ruined/Collapsed이면 끈다.
- **소품**:
  - 소품 프리팹은 `Assets/Prefabs/Map/Props/`의 Crate, Barrel, Shrub, LampPost다.
  - 배치 메서드는 `IronRing.Presentation.Map.Editor.PropsBuild.Generate`(메뉴 `IronRing/Map/Place Props and Smoke`)다. Main.unity의 `Props` 루트 아래에 거점마다 4개씩, 모두 16개를 링 안쪽으로 놓는다. 없는 것만 추가하고, 모달이 없으며, 다른 것은 다시 만들지 않는다.
- **카메라 배경**: Main.unity Main Camera의 배경색을 팔레트 background #2a2331로 바꿨다. 내부 화면은 같은 카메라를 옮겨 쓴다. Rebuild와 맞추려고 `ring-track.json`의 `_map._background`도 같은 값으로 바꿨다. 런타임은 이 값을 읽지 않는다. `Story008Build` 미리보기 카메라도 팔레트 값을 쓴다.
- **Story008Build.Verify**: PPU 64, 64×64, PNG 28개 기준으로 고쳤다.
- **USS** `Assets/UI/Hud/Resources/Hud/*Style.uss` 6개는 색만 바꿨다.
  - 패널은 짙은 자두색 rgba(43,29,42)이다.
  - 테두리는 황동 rgb(214,160,83)이다.
  - 강조색은 호박색 rgb(255,207,106)이다.
  - 주요 버튼은 구리 rgb(153,86,54)이다.
  - 글자색은 연기색 rgb(242,222,205)이다.
  - 배치, 크기, 요소 이름, UXML은 바꾸지 않았다. 다른 에이전트가 만드는 `CodexStyle.uss`는 건드리지 않았다.

## 브리지 결과

| 명령 | id | 결과 |
|---|---|---|
| refresh(첫 시도) | 851630df… | failed=true, 내 파일이 아닌 `Habitats/HabitatsInstaller.cs`의 `HabitatCodex` 미존재(021 작업 중) |
| refresh(1분 뒤 재시도) | 55f7f8f7… | failed=false |
| execute-method SpriteGen.Generate | 626ea331… | ok |
| execute-method SpriteGenTiles.Generate | 99c8399c… | ok |
| execute-method SpriteGenItems.Generate | 4a7fa229… | ok |
| execute-method SpriteGenFx.Generate | 7d596065… | ok |
| execute-method SpriteGenProps.Generate | 7558fb2b… | ok |
| refresh(임포트 마무리) | ba22a7e5… | failed=false |
| compile-status | 7aa35ce6… | failed=false |
| PropsBuild.Generate 1회 | 3dae2450… | ok |
| PropsBuild.Generate 2회(멱등) | 8ad7e078… | ok, 소품 16개 그대로 |
| scene-status | 3be4ce96… | Assets/Scenes/Main.unity, isDirty=false |

## 콘택트 시트(이 폴더)

- `contact_sheet.png`: 선로, 거점과 폐허, 열차, 인물, 시설, 게이지, 내부 칸.
- `tiles_contact_sheet.png`: 타일 13종.
- `items_contact_sheet.png`: 아이템 12종.
- `fx_contact_sheet.png`: 연기 6프레임.
- `props_contact_sheet.png`: 소품 4종.

시트를 보고 참고 이미지와 비교했다(이미지는 저장소에 넣지 않았다). 어두운 보라 회색 바탕, 보라빛 돌바닥과 이끼 줄눈, 적갈색 벽과 슬레이트 지붕, 구리·황동 장식, 호박색 창 불빛과 가스등 광륜, 짙은 외곽선과 2~3단 명암이 모두 보인다. 폐허 거점은 확연히 어둡고 불이 꺼져 있다.

## 아직 남은 것(코디네이터 Play 캡처 필요)

1. 선로 화면 `Assets/Scenes/Main.unity`: 거점 4곳, 연기(복구 중·정착 거점과 열차), 소품 16개, 새 배경색, HUD 색.
2. 내부 화면 `--habitat-demo`: 서식지 세 종류가 완성된 모습. 64px 바닥, 빈 터, 타일이 보여야 한다.
3. 위 캡처를 이전 캡처와 나란히 보고 이 기록에 결과를 덧붙인다. AC 체크박스는 캡처를 본 뒤에 닫는다.

## 코디네이터 검수 (2026-11-01 20:05 KST)

- 콘택트 시트 확인: 돌바닥 바탕에 구리 보일러·황동 증기관·톱니·가스등 광륜·불씨 석탄·리벳 철골·돌 포장·청록 물탱크·온실·침상·작업대·전신주·연기 굴뚝. 거점 4곳 건물 모양이 다르고 폐허는 어둡다. 참고 이미지의 분위기(어두운 보라 회색 바탕, 따뜻한 호박색 불빛, 구리·황동, 짙은 외곽선)가 난다.
- `019-map-depart.png`(브리지 play-capture, `--map-depart` 200프레임): 거점 건물·소품(관목·가로등·상자·배럴)·톱니 경유지·연기 나는 열차·새 배경색·새 HUD 색. 시작 시 거점이 전부 폐허라 건물 연기는 꺼져 있다(RingHubPhaseView 토글은 코드 확인). `--phase-demo` 200프레임 캡처는 단계가 아직 바뀌지 않아 동일해서 삭제.
- `019-interior-habitats.png`(`--habitat-demo`): 96칸 리벳 판과 돌바닥, 새 타일로 완성된 증기 기관실·가스등 거리·보일러실. 021 작업분(도감 버튼, 배너 "서식지 3", "발견: 증기 기관실" 토스트)이 함께 보인다.
- `019-map.png`: 플래그 없는 기본 시작은 내부 화면이다(참고용으로 보관).
- 관찰: 선로 화면에서 건물(1유닛)이 라벨보다 작아 보인다. 거점 스케일을 키우는 것은 사용자 판단으로 남긴다. 내부 빈 칸의 호박색 점 96개가 다소 눈에 띈다(취향 확인 필요).

## 사용자 결정 반영 (2026-11-01 20:25 KST)

- 선로 화면 건물 1.5배: `HubBuilding.prefab`의 Sprite 자식 스케일 2 → 3. `019-map-depart-v2.png`. 라벨이 건물에 겹치는 것은 022에서 라벨을 프리팹의 월드 공간 TMP로 옮기며 디자이너가 조정.
- 빈 칸의 호박색 점 제거: `SpriteGen.cs` interior_empty의 가운데 원 삭제 후 `SpriteGen.Generate` 재실행. `019-interior-nodots.png`.
