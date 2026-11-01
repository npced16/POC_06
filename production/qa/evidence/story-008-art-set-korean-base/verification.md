# Story 008 검증 기록

## 구현

- `SpriteGen.Generate`: 32×32 스프라이트 23개, 240×160 contact_sheet.png, manifest.json을 생성한다. 6종 선로, 정상/폐허 거점 8종, 열차, 경유지, 습격자, 시설 3종, NPC, 게이지 배경/채움이다.
- 팔레트는 `Assets/Art/palette.json`의 12색이다. 임포터는 32 PPU, Point, 무압축, 밉맵 비활성이다.
- `FontSetup.Generate`: NotoSansKR 원본과 OFL을 사용하고 TextCore FontAsset, PanelTextSettings, 공유 PanelSettings 및 USS를 생성한다.
- 런타임 사용: UIDocument.panelSettings에 `KoreanUi.DefaultPanel`을 지정하고 root에 `KoreanUi.Apply(root)`를 호출한다. 폰트는 자식 요소로 상속된다.
- `Strings.Get(key)`는 한국어 73개 항목을 조회한다. 거점 ID hub-1..4는 hub_1..4, 경유지는 waypoint_north/east/south/west, 자원은 resource_coal/scrap/water/grain, 시설은 facility_wall/turret/workshop 키를 사용한다. 누락 키는 KeyNotFoundException으로 알려준다.
- `Story008Build.Generate`는 전용 씬을 최초 한 번 생성하고 기존 씬의 제작자 배치를 보존한다. `Build`는 이 씬만 빌드한다.

## 현재 증거

- 정적 검사: JSON 73개 값 모두 한글 포함, 키 snake_case 확인. 원본 OTF와 OFL 파일 복사 완료.
- Unity 컴파일: 코디네이터의 열린 편집기 브리지 `compile-status` 결과 오류 없음.
- EditMode 테스트: `editmode.xml`을 직접 읽어 Passed, total 12, passed 12, failed 0을 확인했다.
- 스프라이트 생성/재생성: `generation-check.txt`에서 23개 32×32 스프라이트, 32 PPU·Point·무압축, 시트 포함 PNG 24개 SHA256의 두 번 생성 일치를 확인했다. `contact_sheet.png`를 보관했다.
- 폰트 및 전용 씬/플레이어: 코디네이터가 열린 편집기에서 `Story008Build.Build`를 실행하여 성공했다. 생성 FontAsset과 기본 패널/USS, 전용 씬 및 `Builds/Demo/Story008/Game.exe`가 생성되었다.
- 실제 화면: `01-korean-art.png`는 편집기 Play 캡처이고 `02-korean-art-player.png`는 1280×720 빌드 플레이어 캡처다. 두 이미지는 코디네이터가 생성했으며 이 워커는 후자를 직접 열어 제목·거점명·경유지·자원·시설명이 네모 글자나 잘림 없이 표시되고 23개 아트가 모두 보이는 것을 확인했다.
- 런타임 소스는 마지막 동결 후 변경하지 않았다. `source-stamp.txt`는 최종 소스와 생성 애셋의 SHA256 목록이다.
- `player.log`의 종료 구간에는 ComputeBuffer 정리 및 D3D12 리소스 해제 경고가 남아 있다. 캡처와 테스트는 통과했으며 이 경고의 원인은 이번 스토리에서 조사하지 않았다.

Run result: OBSERVED — `02-korean-art-player.png`에서 한국어와 전체 스프라이트 시트 표시 확인.

## 코디네이터 실행

2026-11-01 코디네이터 지시로 실행 방식을 변경했다. 사용자가 열어 둔 Unity 편집기를 유지하며 배치 모드를 사용하지 않는다. 다른 워커가 준비하는 `tools/bridge/bridge.py`를 통해 코디네이터가 아래 메서드와 테스트를 열린 편집기 안에서 실행한다. 이 워커는 Unity 실행·종료·재시작을 하지 않는다.

1. 에디터 메서드 `IronRing.Art.Editor.Story008Build.Verify`: 결정성과 임포터 검증.
2. 에디터 메서드 `IronRing.Art.Editor.Story008Build.Build`: 폰트와 전용 씬 생성 및 플레이어 빌드. 최초 생성은 씬을 전환하므로 코디네이터가 현재 편집 내용 저장 상태를 확인하고 실행한다.
3. EditMode 테스트 필터 `IronRing.Localization.Tests`, 결과 경로 `production/qa/evidence/story-008-art-set-korean-base/editmode.xml`.
4. `Builds/Demo/Story008/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-008-art-set-korean-base/01-korean-art.png --after 90`

플레이어 캡처를 선택하면 기존 ScreenshotOnArg가 캡처 후 플레이어만 종료한다. `--scene`을 생략하여 중복 부팅을 피하고 전용 빌드의 첫 씬을 사용한다. 코디네이터가 브리지의 에디터 Play 캡처를 선택하면 실제 사용한 명령과 이미지 경로를 별도로 기록한다.

## 절차와 기본값

- workflow minimal, ADR/매니페스트/선행 스토리는 해당 없음. 스토리 자체를 스펙으로 사용했다. docs/README.md는 존재하지 않아 읽을 수 없었고 공용 문서는 추가하지 않았다.
- 이번 요청에서 명시한 Localization 테스트만 작성했다. 테스트 파일은 기존 프로젝트와 같은 기본 Editor 어셈블리에 두었으며 asmdef는 추가하지 않았다.
- 소유 범위 밖 공용 스프린트/세션 상태, 기존 영문 HUD, 계약, Bootstrap, Main 씬은 수정하지 않았다. Git 명령과 Unity 실행은 수행하지 않았다.
- 임시 시각 기본값: 12색 황동·청록 팔레트, 32픽셀 원본, 6열 시트, 데모 1280×720 및 원본 3배 확대. 게임 규칙 변경은 없다.
- Unity API는 로컬 `docs/engine-reference/unity/modules/ui.md`와 Unity 공식 [FontAsset 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Modules/TextCoreTextEngine/Managed/TextAssets/FontAsset.cs), [TextSettings 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Modules/TextCoreTextEngine/Managed/TextAssets/TextSettings.cs)를 확인했다.
