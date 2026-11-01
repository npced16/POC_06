# Story 015 검증 — 핫바·고스트·드래그 배치

## 컴파일·생성 확인 (2026-11-01)

- `python -I tools/bridge/bridge.py refresh --timeout 400` → `failed: false`, 오류 없음
- `python -I tools/bridge/bridge.py execute-method --method IronRing.Presentation.Interior.Editor.Story012Build.Generate` → 성공
  - 생성: `Assets/Prefabs/Interior/GhostPiece.prefab`(SpriteRenderer, `GhostPiece.mat`, 정렬 25), `ClusterFrame.prefab`(`ClusterFrame.png` 테두리, 정렬 5)
  - `InteriorCatalog.asset`: `_ghostPiecePrefab`, `_clusterFramePrefab`, 고스트 초록/붉음 색, 스냅 반경, 서식지 색, 시설 아이콘(wall·turret·workshop) 채워짐

## 스크린샷 4장 (코디네이터 캡처)

공통: 씬 `Assets/Scenes/Main.unity`, 시작 정차 거점의 내부 화면이 열린 뒤 데모가 20프레임 기다려 실행된다(`Hotbar.json` `_demo._waitFrames`). 데모 칸은 `Hotbar.json`의 `_demo`(아래 구역 기준).
016(모든 칸 Empty, 무제한 타일)이 들어간 뒤에 캡처한다. 그 전에는 칸이 잔해라 배치가 거부될 수 있다.

```powershell
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 150 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-015-placement-hotbar-dragdrop/01-hotbar-ghost-green.png --extra-args "--placement-demo --placement-stage ghost"
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 150 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-015-placement-hotbar-dragdrop/02-ghost-blocked-red.png --extra-args "--placement-demo --placement-stage blocked"
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 150 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-015-placement-hotbar-dragdrop/03-drag.png --extra-args "--placement-demo --placement-stage drag"
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 150 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-015-placement-hotbar-dragdrop/04-placed-L.png --extra-args "--placement-demo --placement-stage placed"
```

(브리지 CLI의 extraArgs 옵션 이름이 다르면 그 이름으로 같은 문자열을 넘긴다. 값은 SessionState `IronRing.Bridge.Capture.ExtraArgs`로 전달된다.)

| 파일 | 단계 | 기대 화면 | 콘솔 로그 |
|---|---|---|---|
| 01-hotbar-ghost-green.png | ghost | 하단 핫바 9칸(1번 강조), 아래 구역 0줄 4칸 위 초록 반투명 고스트, 상단 자원 바·인벤토리 바·제작 버튼 없음 | `PLACEMENT DEMO ghost ... canPlace=True` |
| 02-ghost-blocked-red.png | blocked | 같은 칸에 1번 타일이 놓이고 그 위 고스트가 붉음 | `canPlace=False` |
| 03-drag.png | drag | 2번 슬롯 항목을 끄는 중: 아래 구역 1줄 2칸 위 초록 고스트 + 옆 아이콘 칩 | `PLACEMENT DEMO drag ... canPlace=True` |
| 04-placed-L.png | placed | 1번 타일 4개가 L자(0줄 1·2·3칸 + 1줄 1칸), 고스트 없음. 016이 그 타일 4칸을 서식지로 판정하면 테두리와 "<이름> 완성!" 토스트 | `배치 ... = True` 4회 |

## 수동 확인 항목 (플레이 중)

- 키 1~9로 슬롯 선택, 같은 키 다시 누르면 해제
- 좌클릭 배치, 우클릭·Esc 취소, 선택 없이 놓인 아이템 우클릭 → 회수
- 팔레트 버튼 → 항목 클릭 → 슬롯 클릭으로 배정 변경
- 슬롯·팔레트 항목을 칸으로 끌어 놓기(UI 위에서 놓으면 취소)
- 핫바·팔레트 위 클릭은 칸에 배치되지 않음
- 출발 버튼, 단계 배너, 선로 화면 전환 정상

## 알려진 블로커

- 시설 배치: `HubInterior.TryBuildFacility`(012, Interior 로직)는 FacilityPlot 칸만 받는다. 016에서 모든 칸이 Empty가 되면 시설 배치가 거부된다(소모 없음, 경고 로그). Empty → FacilityBuilt 허용이 필요하다.

## 코디네이터 검수 (2026-11-01 17:00 KST)

- 4장 모두 캡처·육안 확인. 첫 캡처에서 붉은 고스트가 흐리고(`02-ghost-blocked-red.png`) 서식지 테두리가 타일 밑에 가려져(`04-placed-L.png`) 보이지 않았다.
- 수정: `InteriorCatalog.asset` `_ghostInvalidColor` → (1, 0.15, 0.15, 0.85); `ClusterFrame.prefab` 정렬 순서 5 → 15(타일 10 위, 고스트 25 아래); `PhaseHud` 칸 클릭 건설 메뉴를 `hub_phase_view.json` `_buildMenuOnCellClick:false`로 끔(시설은 핫바 7~9); `InventoryHud` 왼쪽 패널에 완성 서식지 이름 나열(`inv_habitat_list`).
- 재캡처: `02-ghost-blocked-red-v2.png`(붉은 고스트 확인), `04-placed-L-v2.png`(L자 4칸 테두리 + "초원 완성!" + 패널 "완성 서식지: 초원").
- 시설 배치는 시작 거점이 폐허라 규칙상 거부된다(붉은 고스트). 복구 중 이후 실제 배치는 사용자 플레이에서 확인.

## 스팀펑크 타일 재캡처 (2026-11-01 17:25 KST)

- `01-hotbar-ghost-green-v2-steampunk.png`(핫바 1~6 보일러·증기관·톱니장치·가스등·철골·벽돌길, 초록 고스트), `02-ghost-blocked-red-v3-steampunk.png`(붉은 고스트), `03-drag-v2-steampunk.png`(드래그), `04-placed-L-v3-steampunk.png`(보일러 4칸 L자 → "보일러실 완성!", 패널 "완성 서식지: 보일러실").
- 자연 타일 시절 캡처는 `archive/legacy-captures/nature-tiles/`로 옮겼다.
