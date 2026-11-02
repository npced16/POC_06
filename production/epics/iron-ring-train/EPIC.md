# Epic: 황동골 철도 회사 MVP (구 철환 장갑열차) (iron-ring-train)

> **Source**: `design/game-brief.md` (workflow: minimal, no GDD/ADR)
> **Engine**: Unity 6000.3.27f1 (6.3 LTS) · Risk HIGH

> **최신 방향 요약**: 전투·교역·생산 없음, 원형 선로(황동골 + 휴식지 3)에서 타일 배치 → 건물 → 주민·NPC → 돈·번영 → 마을 등급, 유지비 없음, 정착촌 달성 시 선로 개통 → 두 번째 마을 잿골(048). 아래 Goal·Scope·Order는 최초(2026-11-01) 기준 이력이다 — 현재 규칙은 `design/game-brief.md` 참조.

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

현재 유효한 스토리(2026-11-02 정리, 상세는 `production/docs-audit-2026-11-02.md`): **현재** 015·019·020·022·023·025·026·027·029·035·036·039·045·046·047 · **부분 유효** 001·003·005·008·009·012·016·017·021·024·028·030·031·040·041 · **진행 중(스펙)** 048 · 나머지(002·004·006·007·010·011·013·014·018·032·033·034·037·038·042·043·044)는 대체/제거 이력.

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 고리형 선로와 열차 이동 | Logic | Partial(039 원형 선로) | N/A |
| 002 | 서식지 조합 판정과 이벤트 플래그 | Logic | Superseded(016·020) | N/A |
| 003 | 고유 NPC 풀과 확률 합류 | Logic | Partial(023) | N/A |
| 004 | 거점 성장·쇠락·폐허 전환과 패배 판정 | Logic | Superseded(014→045) | N/A |
| 005 | 경유지 이벤트와 간단한 전투 | Integration | Partial(이벤트 유지, 전투 Removed 039) | N/A |
| 006 | 방어시설 건설 | Integration | Removed(039, 코드 보관 047) | N/A |
| 007 | 최소 HUD | UI | Superseded(022) | N/A |
| 008 | 플레이스홀더 아트 세트와 한국어 기반 | Visual/Feel | Partial(한국어 유지, 아트 019) | N/A |
| 009 | 탑뷰 메인 씬과 열차 이동 | Visual/Feel | Partial(선로 039) | N/A |
| 010 | 거점 상태와 전투 연출 | Visual/Feel | Removed(039)/Superseded(036·045) | N/A |
| 011 | 한국어 HUD와 마우스 조작 | UI | Superseded(022) | N/A |
| 012 | 거점 내부 탑뷰 맵과 조사 | Integration | Partial(조사 제거 015) | N/A |
| 013 | 아이템 인벤토리·배치와 태그 조합 서식지 | Logic | Superseded(015·016) | N/A |
| 014 | 거점 단계(폐허→복구→정착→붕괴)와 새 패배 규칙 | Logic | Superseded(045) | N/A |
| 015 | 배치 개편 — 조사 제거, 드래그 앤 드롭·핫바 고스트 배치 | UI | Done | N/A |
| 016 | 서식지 종류 카탈로그와 연결 판정 | Logic | Partial(카탈로그 020) | N/A |
| 017 | NPC 집 배정, 서식지 곁 배회, 클릭 대화·상호작용 | Integration | Partial(대화 027·029) | N/A |
| 018 | MainUI 프리팹 — 씬에 놓인 UI 문서와 편집 모드 미리보기 | UI | Superseded(022) | N/A |
| 019 | 탑뷰 스팀펑크 환경 아트 적용 (참고 이미지 기준) | Visual/Feel | Done | N/A |
| 020 | 서식지 카탈로그 재설계(혼합 묶음·크기 다양화)와 설치칸 확장 | Logic | Done | N/A |
| 021 | 레시피 해금과 도감 | Integration | Partial(해금 030·045) | N/A |
| 022 | UGUI 전환 — 캔버스 하나와 상황별 화면 프리팹 | UI | Done | N/A |
| 023 | 열차 승객 — 경유지 탑승·제안과 힌트·거점별 명단·숨은 조건·도착 연출 | Integration | Done | N/A |
| 024 | UI 스킨 — 사용자 제공 스팀펑크 기어 UI 스프라이트 시트 적용 | UI | Partial(선택 테마로 보관, 026) | N/A |
| 025 | 승객 화면 반영과 UI 잔재 정리 | UI | Done | N/A |
| 026 | UI 단순화 — 장식 스킨은 보관, 깔끔한 기본 스킨으로 | UI | Done | N/A |
| 027 | 조합 완성 → 그 모양대로 건물, 건물 클릭 대화 | Visual/Feel | Done | N/A |
| 028 | 런의 끝과 번영 규칙(점검 S1·S2 반영) | Logic | Partial(런의 끝 045) | N/A |
| 029 | 대화 장면(대형 초상 + 하단 상자), 건물 지붕 디테일, NPC 고정 배치 | Visual/Feel | Done | N/A |
| 030 | 조기 승리와 NPC를 따라가는 레시피 해금 | Logic | Partial(승리 조건 045) | N/A |
| 031 | 부수기·되돌리기, 건물 먼저 고르고 타일 하나씩 놓는 건설 계획 | UI | Partial(A Done / B 대기) | N/A |
| 032 | POC 축소 — 역 1곳과 종점 1곳을 오가는 선로 | Integration | Superseded(039) | N/A |
| 033 | 습격 — 몬스터에게 점령된 종점을 뚫고 지나가기 | Integration | Removed(039) | N/A |
| 034 | 열차 강화 — 칸마다 3×2 장비 그리드와 습격 연동 | Integration | Removed(039) | N/A |
| 035 | 서식지 고정 — 완성된 서식지는 더 커지지 않음 | Logic | Done | N/A |
| 036 | 폐허 연출 — 웅덩이·잡초·모닥불·잔해, 복구될수록 걷힘 | Visual/Feel | Done | N/A |
| 037 | 1차 횡스크롤 여정 + 달리면서 치르는 전투(프리미티브) | Visual/Feel | Removed(039) | N/A |
| 038 | 2차 중간역 정차 — 탑뷰 디펜스·고철 파밍·장비 비용(프리미티브) | Integration | Removed(039) | N/A |
| 039 | 전투 전부 제거, 원형 선로 + 휴식지 3(이벤트), 거점 지명 "황동골" | Integration | Done | N/A |
| 040 | 경제 기반 — 마르크·품목 6종·휴식지 시장·화물칸·거래 패널·정산·파산 | Integration | Partial(돈·장부·운임 유지, 교역 보관 045, 유지비 0 048) | N/A |
| 041 | 생산 건물 — 레시피를 생산 데이터로, 창고, 타일 비용, 적재 | Integration | Partial(타일 가격 유지, 생산·창고 보관 045) | N/A |
| 042 | 주민·노동·만족도·마을 등급·해금 | Logic | Superseded(045) | N/A |
| 043 | 열차 업그레이드·선로 확장 | Integration | Superseded(045) | N/A |
| 044 | 경영 HUD — 장부·건물 정보·목표·결과·경제 이벤트 | UI | Superseded(045) | N/A |
| 045 | 가벼운 마을 경영 — 교역·생산·창고·노동 제거, 건물 수입·주민·등급 | Integration | Done | N/A |
| 046 | 플레이 수정 — Play 예외, HUD 마무리, 데모 인자, 밸런스, 건물 꾸밈 | Integration | Done | N/A |
| 047 | 정리 — 방어시설 보관, 대사 페이지, 번영 상한 | Integration | Done | N/A |
| 048 | 유지비 없음, 정착촌 달성 시 선로 개통 → 두 번째 마을 잿골, 마을별 주민·등급 | Integration | Ready | N/A |

배치 개편(2026-11-01 16:30): 사용자 결정으로 잔해 조사를 없애고, 핫바(1~9)·고스트·드래그 앤 드롭으로 타일을 배치하며, 서식지는 묶음형(같은 타일 N칸 상하좌우 연결, 모양 무관, 크기 3·4·9칸 등)과 패턴형(타일 배열 템플릿이 회전·거울상 8변형 중 하나로 들어맞으면 완성, 예: 물-불-풀 = 풀-불-물, 불-풀-물은 불인정)으로 완성된다. 서식지 종류는 데이터 카탈로그(묶음형 13종·패턴형 3종 이상). 자원·제작 시스템은 당분간 없애고 타일·아이템·시설은 무제한으로 놓는다. 015(UI)와 016(로직)을 병렬로 진행해 2026-11-01 17:00 KST에 둘 다 Done(브리지 컴파일 통과, 캡처 `story-015-placement-hotbar-dragdrop/01~04(-v2).png`, `story-016-habitat-types-clusters/habitat-demo-v2.png`·`-v3-pattern.png` 육안 확인). 코디네이터 통합 수정: 붉은 고스트 색 진하게(카탈로그), 서식지 테두리 정렬 순서 15(타일 위), 칸 클릭 건설 메뉴는 `hub_phase_view.json` `_buildMenuOnCellClick:false`로 꺼서 핫바 7~9와 충돌 제거, 내부 패널에 완성 서식지 이름 나열. 세계관 수정(17:10, 사용자): 타일·서식지를 자연물이 아닌 스팀펑크 마을 요소(보일러·증기관·톱니장치·가스등·석탄더미·철골·벽돌길·물탱크·온실유리·침상·작업대·전신주·굴뚝, 서식지 19종)로 전면 교체 — 2026-11-01 17:25 KST 재검증 완료(캡처 `habitat-demo-v4-steampunk.png`, `story-015/*-steampunk.png`). 012의 조사, 013의 태그 합산·제작, 002의 자원 슬롯은 코드만 보존한다.

프리팹·씬 배치 전환(2026-11-01 16시): 사용자 요구("코드로 배치하지 말고 씬에 직접 배치, 프리팹 기반으로 편집 쉽게")에 따라 고정 배치의 런타임 생성을 없앴다. `Assets/Scenes/Main.unity`의 고정 배치는 모두 프리팹 인스턴스다 — `HubInteriors` 루트 아래 `HubInterior` 인스턴스 4개(`Assets/Prefabs/Interior/HubInterior.prefab`, 중첩 `InteriorCell`·`FloorTile`·`TrackTile`·`TrainCar`)와 선로 화면의 거점·경유지·열차·선로 프리팹(`Assets/Prefabs/Map/HubBuilding`·`WaypointMarker`·`Train`·`TrackTile.prefab`). 그림·재질·폰트·조각 프리팹 참조는 카탈로그 `Assets/Prefabs/Interior/InteriorCatalog.asset`과 `Assets/Prefabs/Map/MapCatalog.asset`에 있다. 재생성은 편집기 메서드(브리지 execute-method) `IronRing.Bootstrap.Editor.BuildTopDownMain.Rebuild`(Main 전체를 지우고 다시 놓는 파괴적 재생성)와 `IronRing.Presentation.Interior.Editor.Story012Build.Generate`(프리팹·카탈로그 보장 + 빠진 내부 인스턴스만 추가, 여러 번 실행해도 결과가 같음)로 한다. 아이템·시설·NPC 같은 동적 조각은 카탈로그가 가리키는 프리팹(`ItemPiece.prefab` 등)을 런타임에 인스턴스화한다. 전환 뒤 전체 EditMode 154/154(`production/qa/evidence/integration/editmode-prefab.xml`), 012~014 스크린샷 8장 재캡처·육안 확인, `Main.unity` 저장(isDirty=false). 편집 규칙은 `design/game-brief.md`의 Art & audio direction 아래 한 줄로 고정했다. 남은 항목: 내부 화면 실제 마우스 조작 기록(사용자 확인), `InteriorViewSettings.cs` 스텁 삭제(사용자), 선로 화면 단계 라벨 겹침(014 에이전트 수정 중).

NPC 집·대화(017, 2026-11-01 18:05 KST): 사용자 지시로 합류 NPC는 자기 종류의 완성 서식지를 집으로 받아 그 곁(집 칸 + 붙은 빈 칸)만 배회하고, 빈 손 좌클릭이면 대화 패널(직업명·집·대사 순환·격려(정차 1회 번영 +3, 데이터)·닫기)이 열린다. 계약 `INpcHomes`/`INpcInteraction`/`INpcClickSource`, 로직 `Assets/Scripts/Npc/`(NpcHomes·NpcInteraction·NpcInstaller·데이터), 표현 `InteriorNpcWalker`+`NpcBody`+프리팹 콜라이더, HUD `Assets/Scripts/Hud/Npc/NpcDialogHud`. 캡처 `story-017-npc-homes-dialogue/017-homes|panel|encouraged.png`. 메모: 격려 횟수는 출발 이벤트가 없어 다음 도착 때 초기화.

선로 개통(048, 2026-11-02 09:30 KST): 사용자 "나중에 여러 마을로 확장, 유지비 두지 말고 마을별 멸망 시나리오, 지금은 한 마을 느낌, 일정 이상 진행되면 선로 열려서 마을 하나 더" → 유지비 0, 주민·등급 마을별, 정착촌 달성 시 hub-2 잿골 개통.

가볍게 정리(045, 2026-11-02 06:40 KST): 사용자 "게임이 이상하게 가는데?" → 선택 "가볍게 — 마을 키우기만". 교역·생산·창고·노동·만족도 제거(보관), 돈·타일 가격·주민·등급만. 042~044는 045로 대체.

경영 시뮬레이션 전환(040~044, 2026-11-02 04:20 KST): 사용자 "완전 경영 시뮬레이션 쪽으로 싹 다 가주라" → 브리프 전면 재작성("황동골 철도 회사"): 돈(마르크)·품목 6종·휴식지 시장 거래·생산 건물(레시피에 입력/출력/노동자/유지비)·창고·타일 비용·주민/노동/만족도/마을 등급·열차 업그레이드·선로 확장·장부·경제 이벤트. 20바퀴 안에 도시 등급 = 승리, 파산 = 패배. 039 뒤 040부터 순서대로.

전투 제거(039, 2026-11-02 04:05 KST): 사용자 "둘 다 빼자, 전투 별로다. 전투 싹 다 빼고 종점 말고 중간중간 이벤트 생기도록. 테스트맵도 원형으로 만들어 중간 휴식지(이벤트)로, 맨 처음 거점을 지명으로." → 033·034·037·038과 005 주사위 전투 제거(archive/combat-2026-11-02/ 보관), 선로 = 거점 1(황동골) + 휴식지 3 원형, 휴식지마다 이벤트 1개, 검증은 파이프라인만.

여정 화면(037·038, 2026-11-02 02:35 KST): 사용자 "전투는 프리미티브로, 기차가 이동하는 과정에서 지나가는 역 등을 보여주면서 — 횡스크롤뷰로 1차, 2차로 탑뷰". 출발하면 선로 지도 대신 여정 화면(열차 고정·풍경 스크롤·역/폐역/전초 지나감)으로 가고 습격도 그 화면에서; 모든 그림은 프리미티브(색 도형)로 먼저. `_travelView` 플래그로 side/map 전환. 추가 지시(02:45): "지나가면서 전투로 만들거나, 중간역에 정차해서 전투하고 자원 파밍하고 가게끔, 정차 시엔 탑뷰로 디펜스" → 037 = 달리면서 전투(enroute 모드), 038 = 중간역 정차 탑뷰 디펜스 + 고철 파밍, 고철은 열차 장비 비용(코디네이터 가정).

열차 강화·CG 의뢰(034, 2026-11-02 01:00 KST): 사용자 "캐릭터 CG는 codex한테 넘겨서 다시 하게, 첫 거점에서 내 열차 포탑 디펜스를 그리드(칸마다 3×2)로, 열차가 다음 역에서 몰려오는 몹을 잡는 게임으로 열차 강화·거점 강화 반복". 034 = 열차 칸 3량 × 3×2 장비 그리드(포탑·중포·방벽판·부스터·수리), 거점 시설은 열차 장비로 대체, 습격(033)의 IRaidLoadout 제공자 교체. 캐릭터 CG는 `production/handoffs/codex-character-cg.md`로 codex에 의뢰.

습격 개편(033, 2026-11-02 00:45 KST 착수): 사용자 "습격 막아내는 게 재밌었으면 — 몬스터로 점령된 거점 지나가는 느낌". 종점을 둥지 3개가 있는 점령 거점으로, 통과 12초 동안 몬스터가 달려들고 클릭 사격 + 포탑 자동 사격 + 방벽 내구도로 막아낸다. 결과는 기존 Victory/Defeat 신호로 연결.

POC 축소(032, 2026-11-02 00:20 KST 착수): 사용자 "일단 POC 느낌으로 1개 역 1개 종점 도는 방식으로". 선로를 역(hub-1) + 종점 2노드 왕복으로 줄이고(원본 8노드 데이터 보관), 거점 수는 선로의 역 수로 계산, 승객은 역 하나에 모두 내릴 수 있게 플래그.

배치 UX(031, 2026-11-02 00:10 KST 착수): 사용자 "좀 하나씩 나오고, 취소 및 부수는 거 가능하게, 좀 더 직관적으로 연관성 있게(~~건물은 이런 거 있으면 되겠다)". A(부수기·Ctrl+Z 되돌리기) 즉시 착수, B(건물 목록 → 계획 고스트 → 필요한 타일이 하나씩 올라오는 건설 계획)는 해석 확인 뒤.

대화 장면·해금 개편(029·030, 2026-11-01 23:05 KST 착수): 사용자 참고 이미지(마비노기풍: 큰 초상 + 하단 대화 상자 + 계속하기)와 지시 5건 — 지붕 디테일, NPC 건물 고정 배치, 조기 승리 허용, NPC를 따라가는 해금(탑승 시 원하는 설계도, 합류 시 가르치는 설계도; 바퀴·이벤트 해금 끔), 마우스 조작은 사용자 확인 완료. 초상 35장은 종이 인형식 생성(`SpriteGenPortraits`, `NpcPortraitCatalog`). 2026-11-01 23:25 KST 029·030 Done(캡처 `story-029-dialog-scene-roofs-npc-posts/029-v3-dialog*.png`·`029-buildings.png`, `story-030-early-win-npc-unlocks/030-*.png`).

재미 보강(027·028, 2026-11-01 22:40 KST 착수): 사용자 "왜 게임이 더 재미가 없을까 — 조합하면 그 모양대로 건물이 생기고 건물 클릭하면 NPC 창이 나와 옛날 게임처럼 말 걸었으면". 점검 보고서 결론(승리 없음, 타일 칸 수로 정착, 완성 보상 순간 없음)과 합쳐 027(조합 → 자동 타일링 건물, 문 앞 NPC, 건물 클릭 대화, 초상·타자기)과 028(결과 화면·승리/부분/패배, 번영 공식에서 칸당 +1 제거, 승객 탑승 공정성, 핫바 기본 칸, 표시 갱신 버그)로 진행. 2026-11-01 22:55 KST 027·028 Done(캡처 `story-027-combo-buildings-click-talk/027-v2-*.png`, `story-028-run-ending-and-prosperity/028-v2-*.png`).

UI 단순화·게임 점검(026, 2026-11-01 22:15 KST 착수): 사용자 지시 "UI가 쩅하고 구려, 싹 다 날리자, 시트 스타일은 나중에 쓸 수 있게 두고 간단하게, 게임 한번 점검해봐". 테마 스킨 모드(Simple 기본/Sheet 보관)로 되돌리고, 별도 QA 점검 보고서(`production/qa/playtest-2026-11-01.md`)를 만든다.

UI 스킨·승객 화면(024·025, 2026-11-01 21:20 KST 착수): 사용자 지시 "UI 스타일이 하나도 안 이쁘다 — 이 스프라이트 시트를 활용해 달라"(사용자 소유 시트 `Assets/Art/UI/SteampunkUiSheet.png`: 구리·황동 파이프 프레임·제목판·버튼·슬롯) + 후속 1·2 진행. 024는 시트 배경 제거·슬라이스·테마 교체·전체 재스킨, 025는 상단 바 승객 수·승객 화면·대화 출신지·도감 "???"와 UiHitTests·HudSortingOrder 잔재 정리. 병렬 진행, 빌더 충돌을 피하려 024의 빌더 수정은 025 뒤. 2026-11-01 22:05 KST 둘 다 Done(캡처 `story-024-ui-skin-sprite-sheet/024-*.png`). 사용자 추가 지시(21:40) "글자 크기 안 맞음" → 테마 글자 4단계·줄 높이·버튼 높이 통일, 검수 수정 6건 반영.

NPC 다양화(023, 2026-11-01 20:50 KST 착수): 사용자 지시 "서식지별로 찾아오는 유형이 다른데 여러 명이 올 수 있고 확률은 각자 다르게 숨은 조건으로, 어디에서 들어오는지도", "마을마다 찾아오는 사람이 다르도록". 사용자 추가 결정(20:55) "1 방식(열차 승객)으로, 탑승하면서 제안하도록(이런 걸 만들어줘·이런 마을이면 좋겠어 힌트)". NPC 약 35명, 경유지 탑승(확률·탑승 조건·정원 3·참을성 3바퀴), 제안·힌트 데이터, 거점별 명단, 숨은 조건 6종, 열차에서 내려 집으로 걸어가는 도착 연출과 출신지 표시. 정착 규칙은 답이 없어 코디네이터 제안(번영 40으로만 정착)을 `_settleByHabitat:false` 데이터로 적용. 022와 병렬(HUD 반영은 022 뒤).

UGUI 전환(022, 2026-11-01 20:20 KST 착수): 사용자 최종 결정 "UGUI로 싹 다 대체, 프리팹화해서 상황별 패널을 보기 쉽게". UI Toolkit(018·021의 MainUI·UXML/USS)을 모두 걷어내고 `MainCanvas.prefab` + 화면별 프리팹(TopBar·HubPanel·Defeat·PhaseBanner·InteriorTitle·Hotbar·Toast·NpcDialog·Codex·CombatFx)을 SetActive로 켜고 끄는 구조로 바꾼다. 선로 화면 라벨은 HubBuilding·WaypointMarker 프리팹의 월드 공간 TMP로 옮긴다. 같은 날 사용자 결정으로 선로 화면 건물 1.5배(HubBuilding 프리팹 Sprite 스케일 3), 빈 칸 호박색 점 제거, 판정 우선순위(큰 묶음 우선) 유지. 정착 임계값은 023에서 `_settleByHabitat:false`로 적용. 2026-11-01 21:10 KST 022 Done: 1단계 틀(TMP 폰트·UiTheme·UiScreens·UiKit·BuildMainCanvas) → 2단계 화면 10개 프리팹(A: TopBar·HubPanel·Defeat·PhaseBanner·월드 라벨·CombatFx / B: Hotbar·NpcDialog·Codex) → 3단계 UI Toolkit 자산·코드 전면 삭제, 옛 데모는 `archive/legacy-uitoolkit/`. 남은 정리: `PlacementGhost.UiHitTests`·`HudSortingOrder` 잔재, 승객 HUD 반영(상단 바 승객 수·승객 패널·대화 출신지·도감 ???).

아트·다양화·도감(019~021, 2026-11-01 19:40 KST 착수): 사용자 결정 — (1) UI는 UI Toolkit 유지(UGUI 전환 제안은 철회), (2) 참고 이미지("Top-down Steampunk 2D Game Environment Art", 저장소 밖 보관)의 팔레트·모티프를 브리프에 글로 고정하고 모든 스프라이트를 64px로 다시 그린다(019), (3) "같은 타일 4칸" 11종이 너무 비슷하므로 혼합 묶음(`set`) 종류를 더해 같은 타일 4·혼합 9·패턴 6 = 19종, 크기 2~9칸으로 재설계하고 바닥에 보이는 칸을 전부 설치칸(위·아래 각 4×12)으로 넓힌다(020), (4) 레시피는 시작 3개 + 바퀴·이벤트·승리·우연한 완성으로 해금되어 도감에 쌓이고, "서식지 조건" 패널은 도감으로 대체한다(021). 019와 020은 병렬, 021은 020 뒤. 2026-11-01 20:10 KST 셋 다 Done(캡처: `story-019-steampunk-environment-art/019-map-depart.png`·`019-interior-habitats.png`, `story-020-habitat-catalog-redesign-grid/020-habitat-complete-v2.png` 외 2장, `story-021-recipe-unlock-codex/021-codex-start|lap|discover.png`·`021-editmode-codex.png`). 코디네이터 조정: 내부 카메라 5.5→7.0(HUD 겹침). 열린 질문: 선로 화면 건물 크기, 빈 칸의 호박색 점, 판정 우선순위(큰 묶음 우선).

UI 프리팹(018, 2026-11-01 18:45 KST 착수): 사용자 결정으로 HUD도 씬의 프리팹 인스턴스(`Assets/Prefabs/UI/MainUI.prefab`, 루트 `MainUI` 아래 HUD별 자식 6개에 UIDocument + HUD 스크립트)로 둔다. HUD 스크립트는 씬의 UIDocument에 바인딩만 하고 런타임 생성은 MainUI 없는 데모 씬의 대비책으로만 남긴다. 목적은 편집 모드 Game 뷰 미리보기(UIDocument가 씬에 있으면 Play 없이 그려짐)와 UI Builder 편집 흐름. 재생성은 `IronRing.Hud.Editor.BuildMainUi.Generate`(멱등). 2026-11-01 19:30 KST Done(컴파일 통과, 편집 모드 Game 뷰 캡처 `story-018-main-ui-prefab/018-editmode-gameview.png`, Play 캡처 2장). 후속 결정(사용자, 19:20): 디자이너가 씬에서 직접 고치도록 UI를 UGUI 캔버스 + 화면별 프리팹(켜고 끄기)으로 전환한다(019). 018의 UI Toolkit 문서는 그때 대체된다.

개발 방식 결정(2026-11-01 16:10): 사용자 결정으로 자동 테스트를 두지 않는다(AX 기반 개발). `Assets/Tests`는 `archive/legacy-tests/`로 옮겼고 `project.yaml`의 `testing.strict.logic/integration/config`는 false다. 이후 완료 증거는 브리지 컴파일 확인과 스크린샷이며, 문서의 `editmode-*.xml` 언급은 이전 기록이다. 개발 순서는 스펙 우선(SDD): 브리프·스토리를 먼저 고치고 구현하며, 판단이 애매하면 `.claude/agents/*.md`의 역할 정의를 따른다. 선로 화면 단계 라벨 겹침은 수정 완료(`story-009-topdown-main-scene/03-ring-prefabs-v2.png`).

거점 내부 전환(2026-11-01 오후): 사용자 결정으로 거점은 폐허에서 시작하고, 정차하면 내부 탑뷰(위·아래 3×6칸)에서 정차당 3회 잔해를 조사하며, 자원 슬롯 대신 아이템(조사 발견·이벤트 보상·자원 제작)을 배치한 태그 조합으로 서식지를 완성한다. 순서: 012 → 013 → 014. 브리프의 핵심 루프·목표·MVP 3·6번이 함께 갱신되었다.

표현 계층 전환(2026-11-01): 001~007은 로직과 패널형 데모로 검증되었고, 008~011이 같은 로직 위에 탑뷰 2D 씬과 한국어 UI를 얹는다. 순서: 008 → 009·010·011 병렬 → 코디네이터 통합 실행.

검증 방식: 008~011의 컴파일·테스트·씬 생성·Play 캡처는 편집기를 연 상태에서 편집기 내 브리지(`tools/bridge`, `py -3 -I tools/bridge/bridge.py …`)로 실행한다. batchmode 재실행이나 Unity 종료 없이 열린 편집기에 명령을 큐잉하고 `.bridge/results/<id>.json`에서 결과를 받는다. 011은 스크린샷 4장은 확보했으나 실제 마우스 조작 기록이 없어 Ready로 남긴다.

## Out of scope

싱글플레이 전용, 거점 5곳 이상 (구조는 확장 가능하게), 분기 선로, NPC 대화 트리, 스토리 컷신.
