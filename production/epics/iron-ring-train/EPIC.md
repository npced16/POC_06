# Epic: 철환 장갑열차 MVP (iron-ring-train)

> **Source**: `design/game-brief.md` (workflow: minimal, no GDD/ADR)
> **Engine**: Unity 6000.3.27f1 (6.3 LTS) · Risk HIGH

## Goal

고리형 선로를 도는 장갑열차를 몰며 네 거점을 지키고 키워, 서식지 조합으로 고유 NPC를 불러 모으는 스팀펑크 방어 경영 게임.

## Scope (Brief MVP)

1. 거점 4곳과 경유지로 이루어진 고리형 선로 위의 열차 이동
2. 경유지 이벤트 및 간단한 전투 1종
3. 자원 배치로 서식지 조합을 완성하고 이벤트 플래그를 켜는 시스템
4. 고유 NPC 풀과 확률 합류
5. 방어시설 건설 2~3종
6. 거점별 고유 성장 방식과 번성/쇠락 수치, 폐허 전환, 모두 폐허면 패배
7. 최소한의 HUD

## Order

Brief Build order: 이동 → 서식지·NPC → 거점 상태·패배 → 전투·방어시설 → HUD.

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 고리형 선로와 열차 이동 | Logic | Done | N/A |
| 002 | 서식지 조합 판정과 이벤트 플래그 | Logic | Done | N/A |
| 003 | 고유 NPC 풀과 확률 합류 | Logic | Done | N/A |
| 004 | 거점 성장·쇠락·폐허 전환과 패배 판정 | Logic | Done | N/A |
| 005 | 경유지 이벤트와 간단한 전투 | Integration | Done | N/A |
| 006 | 방어시설 건설 | Integration | Done | N/A |
| 007 | 최소 HUD | UI | Done | N/A |
| 008 | 플레이스홀더 아트 세트와 한국어 기반 | Visual/Feel | Done | N/A |
| 009 | 탑뷰 메인 씬과 열차 이동 | Visual/Feel | Done | N/A |
| 010 | 거점 상태와 전투 연출 | Visual/Feel | Done | N/A |
| 011 | 한국어 HUD와 마우스 조작 | UI | Ready | N/A |
| 012 | 거점 내부 탑뷰 맵과 조사 | Integration | Done | N/A |
| 013 | 아이템 인벤토리·배치와 태그 조합 서식지 | Logic | Done | N/A |
| 014 | 거점 단계(폐허→복구→정착→붕괴)와 새 패배 규칙 | Logic | Done | N/A |
| 015 | 배치 개편 — 조사 제거, 드래그 앤 드롭·핫바 고스트 배치 | UI | Done | N/A |
| 016 | 서식지 종류 카탈로그와 연결 판정 | Logic | Done | N/A |
| 017 | NPC 집 배정, 서식지 곁 배회, 클릭 대화·상호작용 | Integration | Done | N/A |
| 018 | MainUI 프리팹 — 씬에 놓인 UI 문서와 편집 모드 미리보기 | UI | Done | N/A |
| 019 | 탑뷰 스팀펑크 환경 아트 적용 (참고 이미지 기준) | Visual/Feel | Ready | N/A |
| 020 | 서식지 카탈로그 재설계(혼합 묶음·크기 다양화)와 설치칸 확장 | Logic | Ready | N/A |
| 021 | 레시피 해금과 도감 | Integration | Ready | N/A |

배치 개편(2026-11-01 16:30): 사용자 결정으로 잔해 조사를 없애고, 핫바(1~9)·고스트·드래그 앤 드롭으로 타일을 배치하며, 서식지는 묶음형(같은 타일 N칸 상하좌우 연결, 모양 무관, 크기 3·4·9칸 등)과 패턴형(타일 배열 템플릿이 회전·거울상 8변형 중 하나로 들어맞으면 완성, 예: 물-불-풀 = 풀-불-물, 불-풀-물은 불인정)으로 완성된다. 서식지 종류는 데이터 카탈로그(묶음형 13종·패턴형 3종 이상). 자원·제작 시스템은 당분간 없애고 타일·아이템·시설은 무제한으로 놓는다. 015(UI)와 016(로직)을 병렬로 진행해 2026-11-01 17:00 KST에 둘 다 Done(브리지 컴파일 통과, 캡처 `story-015-placement-hotbar-dragdrop/01~04(-v2).png`, `story-016-habitat-types-clusters/habitat-demo-v2.png`·`-v3-pattern.png` 육안 확인). 코디네이터 통합 수정: 붉은 고스트 색 진하게(카탈로그), 서식지 테두리 정렬 순서 15(타일 위), 칸 클릭 건설 메뉴는 `hub_phase_view.json` `_buildMenuOnCellClick:false`로 꺼서 핫바 7~9와 충돌 제거, 내부 패널에 완성 서식지 이름 나열. 세계관 수정(17:10, 사용자): 타일·서식지를 자연물이 아닌 스팀펑크 마을 요소(보일러·증기관·톱니장치·가스등·석탄더미·철골·벽돌길·물탱크·온실유리·침상·작업대·전신주·굴뚝, 서식지 19종)로 전면 교체 — 2026-11-01 17:25 KST 재검증 완료(캡처 `habitat-demo-v4-steampunk.png`, `story-015/*-steampunk.png`). 012의 조사, 013의 태그 합산·제작, 002의 자원 슬롯은 코드만 보존한다.

프리팹·씬 배치 전환(2026-11-01 16시): 사용자 요구("코드로 배치하지 말고 씬에 직접 배치, 프리팹 기반으로 편집 쉽게")에 따라 고정 배치의 런타임 생성을 없앴다. `Assets/Scenes/Main.unity`의 고정 배치는 모두 프리팹 인스턴스다 — `HubInteriors` 루트 아래 `HubInterior` 인스턴스 4개(`Assets/Prefabs/Interior/HubInterior.prefab`, 중첩 `InteriorCell`·`FloorTile`·`TrackTile`·`TrainCar`)와 선로 화면의 거점·경유지·열차·선로 프리팹(`Assets/Prefabs/Map/HubBuilding`·`WaypointMarker`·`Train`·`TrackTile.prefab`). 그림·재질·폰트·조각 프리팹 참조는 카탈로그 `Assets/Prefabs/Interior/InteriorCatalog.asset`과 `Assets/Prefabs/Map/MapCatalog.asset`에 있다. 재생성은 편집기 메서드(브리지 execute-method) `IronRing.Bootstrap.Editor.BuildTopDownMain.Rebuild`(Main 전체를 지우고 다시 놓는 파괴적 재생성)와 `IronRing.Presentation.Interior.Editor.Story012Build.Generate`(프리팹·카탈로그 보장 + 빠진 내부 인스턴스만 추가, 여러 번 실행해도 결과가 같음)로 한다. 아이템·시설·NPC 같은 동적 조각은 카탈로그가 가리키는 프리팹(`ItemPiece.prefab` 등)을 런타임에 인스턴스화한다. 전환 뒤 전체 EditMode 154/154(`production/qa/evidence/integration/editmode-prefab.xml`), 012~014 스크린샷 8장 재캡처·육안 확인, `Main.unity` 저장(isDirty=false). 편집 규칙은 `design/game-brief.md`의 Art & audio direction 아래 한 줄로 고정했다. 남은 항목: 내부 화면 실제 마우스 조작 기록(사용자 확인), `InteriorViewSettings.cs` 스텁 삭제(사용자), 선로 화면 단계 라벨 겹침(014 에이전트 수정 중).

NPC 집·대화(017, 2026-11-01 18:05 KST): 사용자 지시로 합류 NPC는 자기 종류의 완성 서식지를 집으로 받아 그 곁(집 칸 + 붙은 빈 칸)만 배회하고, 빈 손 좌클릭이면 대화 패널(직업명·집·대사 순환·격려(정차 1회 번영 +3, 데이터)·닫기)이 열린다. 계약 `INpcHomes`/`INpcInteraction`/`INpcClickSource`, 로직 `Assets/Scripts/Npc/`(NpcHomes·NpcInteraction·NpcInstaller·데이터), 표현 `InteriorNpcWalker`+`NpcBody`+프리팹 콜라이더, HUD `Assets/Scripts/Hud/Npc/NpcDialogHud`. 캡처 `story-017-npc-homes-dialogue/017-homes|panel|encouraged.png`. 메모: 격려 횟수는 출발 이벤트가 없어 다음 도착 때 초기화.

아트·다양화·도감(019~021, 2026-11-01 19:40 KST 착수): 사용자 결정 — (1) UI는 UI Toolkit 유지(UGUI 전환 제안은 철회), (2) 참고 이미지("Top-down Steampunk 2D Game Environment Art", 저장소 밖 보관)의 팔레트·모티프를 브리프에 글로 고정하고 모든 스프라이트를 64px로 다시 그린다(019), (3) "같은 타일 4칸" 11종이 너무 비슷하므로 혼합 묶음(`set`) 종류를 더해 같은 타일 4·혼합 9·패턴 6 = 19종, 크기 2~9칸으로 재설계하고 바닥에 보이는 칸을 전부 설치칸(위·아래 각 4×12)으로 넓힌다(020), (4) 레시피는 시작 3개 + 바퀴·이벤트·승리·우연한 완성으로 해금되어 도감에 쌓이고, "서식지 조건" 패널은 도감으로 대체한다(021). 019와 020은 병렬, 021은 020 뒤.

UI 프리팹(018, 2026-11-01 18:45 KST 착수): 사용자 결정으로 HUD도 씬의 프리팹 인스턴스(`Assets/Prefabs/UI/MainUI.prefab`, 루트 `MainUI` 아래 HUD별 자식 6개에 UIDocument + HUD 스크립트)로 둔다. HUD 스크립트는 씬의 UIDocument에 바인딩만 하고 런타임 생성은 MainUI 없는 데모 씬의 대비책으로만 남긴다. 목적은 편집 모드 Game 뷰 미리보기(UIDocument가 씬에 있으면 Play 없이 그려짐)와 UI Builder 편집 흐름. 재생성은 `IronRing.Hud.Editor.BuildMainUi.Generate`(멱등). 2026-11-01 19:30 KST Done(컴파일 통과, 편집 모드 Game 뷰 캡처 `story-018-main-ui-prefab/018-editmode-gameview.png`, Play 캡처 2장). 후속 결정(사용자, 19:20): 디자이너가 씬에서 직접 고치도록 UI를 UGUI 캔버스 + 화면별 프리팹(켜고 끄기)으로 전환한다(019). 018의 UI Toolkit 문서는 그때 대체된다.

개발 방식 결정(2026-11-01 16:10): 사용자 결정으로 자동 테스트를 두지 않는다(AX 기반 개발). `Assets/Tests`는 `archive/legacy-tests/`로 옮겼고 `project.yaml`의 `testing.strict.logic/integration/config`는 false다. 이후 완료 증거는 브리지 컴파일 확인과 스크린샷이며, 문서의 `editmode-*.xml` 언급은 이전 기록이다. 개발 순서는 스펙 우선(SDD): 브리프·스토리를 먼저 고치고 구현하며, 판단이 애매하면 `.claude/agents/*.md`의 역할 정의를 따른다. 선로 화면 단계 라벨 겹침은 수정 완료(`story-009-topdown-main-scene/03-ring-prefabs-v2.png`).

거점 내부 전환(2026-11-01 오후): 사용자 결정으로 거점은 폐허에서 시작하고, 정차하면 내부 탑뷰(위·아래 3×6칸)에서 정차당 3회 잔해를 조사하며, 자원 슬롯 대신 아이템(조사 발견·이벤트 보상·자원 제작)을 배치한 태그 조합으로 서식지를 완성한다. 순서: 012 → 013 → 014. 브리프의 핵심 루프·목표·MVP 3·6번이 함께 갱신되었다.

표현 계층 전환(2026-11-01): 001~007은 로직과 패널형 데모로 검증되었고, 008~011이 같은 로직 위에 탑뷰 2D 씬과 한국어 UI를 얹는다. 순서: 008 → 009·010·011 병렬 → 코디네이터 통합 실행.

검증 방식: 008~011의 컴파일·테스트·씬 생성·Play 캡처는 편집기를 연 상태에서 편집기 내 브리지(`tools/bridge`, `py -3 -I tools/bridge/bridge.py …`)로 실행한다. batchmode 재실행이나 Unity 종료 없이 열린 편집기에 명령을 큐잉하고 `.bridge/results/<id>.json`에서 결과를 받는다. 011은 스크린샷 4장은 확보했으나 실제 마우스 조작 기록이 없어 Ready로 남긴다.

## Out of scope

싱글플레이 전용, 거점 5곳 이상 (구조는 확장 가능하게), 분기 선로, NPC 대화 트리, 스토리 컷신.
