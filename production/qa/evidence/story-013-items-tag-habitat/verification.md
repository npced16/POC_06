# Story 013 검증 기록 — 아이템 인벤토리·배치와 태그 조합 서식지

날짜: 2026-11-01 · Unity 6000.3.27f1 · 브리지로만 검증(에디터는 열린 상태 유지)

## 자동 검증

| 항목 | 결과 | 증거 |
|---|---|---|
| 컴파일(`refresh`) | failed=false, 오류 없음 | 브리지 출력 |
| `IronRing.Items.Tests` | 27/27 통과 | `editmode.xml` |
| 전체 `IronRing` | 154/154 통과(결함 수정 후 재실행) | `editmode-all.xml` |
| 아이템 스프라이트 생성(`SpriteGenItems.Generate`) | 12종 + 시트 | `items_contact_sheet.png`, `Assets/Art/Generated/items/` |

(1차 실행 때 실패하던 Integration 1건은 014가 테스트를 갱신해 해소됐다.)

### 테스트가 다루는 수용 기준
- 제작: 비용을 정확히 빼고 1개 추가, 자원이 모자라면 거부하고 자원·아이템·이벤트 모두 그대로, 모르는 아이템은 거부
- 배치·회수: 빈 터에만 배치(잔해·부지·시설 칸은 거부), 배치 시 −1·회수 시 +1(수량 보존 반복), 칸 상태 ItemPlaced/Empty 갱신, 이벤트 발생, 칸 쓰기 거부 시 환불
- 태그 서식지: 다중 태그 합산, 조건 하나라도 모자라면 미완성, 플래그는 정확히 1회·회수 후에도 유지(sticky), 거점별 독립, 슬롯 메서드 비활성
- 데이터: 카탈로그 12종 한국어 이름·6태그·4자원 비용, 거점 조건 기본값(주조소 열2+산업1, 정원 식물2+물1, 항구 물2+산업1, 성채 쉼터2+빛1)
- 보상: 전투 승리·이벤트 제목별 아이템 보상, 005 데이터의 선택 필드 파싱

## 스크린샷 3장 (코디네이터 캡처)

공통: 씬 `Assets/Scenes/Main.unity`. 데모(`--items-demo`)는 내부 화면이 `hub-1`(주조소)로 열리고 20프레임 뒤 실행된다.
`--extra-args` 값이 `--`로 시작하므로 반드시 `=` 형식으로 넘긴다.

1. 인벤토리 바와 제작 메뉴 — 화로·증기관 제작 후 제작 메뉴 열림
```
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 180 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-013-items-tag-habitat/01-inventory-craft.png "--extra-args=--items-demo --items-demo-stage craft"
```
2. 아이템 배치 후 내부 화면 — 톱니 장식·화로·증기관을 빈 터 3칸에 배치, 마지막 칸의 회수 메뉴 열림
```
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 180 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-013-items-tag-habitat/02-items-placed.png "--extra-args=--items-demo --items-demo-stage place"
```
3. 서식지 완성 문구 — 같은 배치 후 좌상단 서식지 패널 "서식지 완성!"(열 2/2·산업 2/1)과 완성 배너
```
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 180 --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-013-items-tag-habitat/03-habitat-complete.png "--extra-args=--items-demo"
```

확인 로그(Console): `ITEMS DEMO hub-1: items[...] complete=True flag=True`.
빈 터가 3칸보다 적으면 데모가 실제 조사를 하고, 조사 횟수가 떨어지면 데모 한정으로 `OnDocked`를 다시 불러 채운다(로그 남김).
자원이 모자라면 데모가 부족분만 지급한다(로그 남김).

## 결함 수정 (코디네이터 시각 확인 1차: 02·03 캡처에 아이템이 안 보임)

근본 원인: 데모가 "인덱스 순 첫 잔해 칸"을 조사·배치했다. 0~5번은 위 구역의 맨 윗줄(화면 최상단)이라,
그 줄이 서식지 완성 배너(화면 위 가운데)와 014의 단계·기한 라벨에 통째로 가려졌다. 그래서 바뀐 타일과 아이템이 보이지 않고 잔해 줄만 보였다.
배치 로직은 정상이었다(회수 메뉴 "칸 5 · 증기관"과 완성 판정이 그 증거).

수정:
1. 데모가 선로에 가까운 줄부터 칸을 고른다(위 구역 마지막 줄 → 아래 구역 첫 줄 → …).
2. 완성 배너를 그리드 밖(왼쪽 서식지 패널 아래)으로 옮겼다.
3. 아이템 렌더러가 칸 타일(앵커)의 머티리얼·정렬 레이어를 그대로 쓰고 순서는 타일보다 위다. 012가 2D URP용 별도 스프라이트 머티리얼로 그리기 때문이다.
4. (프리팹 전환, 2차) 012가 내부를 씬 프리팹으로 바꿨다. 이에 맞춰 `ItemCellView`는 렌더러를 코드로 만들지 않는다.
   칸 앵커(`InteriorCellView`)의 `ItemSlot` 아래에 012 카탈로그(`Assets/Prefabs/Interior/InteriorCatalog.asset`)의 `ItemPiece.prefab`을 인스턴스화하고
   스프라이트만 `ItemSpriteSet`에서 넣는다. 회수하면 그 인스턴스를 파괴한다.
   카탈로그는 012와 같은 경로, 즉 씬 `HubInteriorRoot`의 직렬화 참조로 얻는다. 씬과 012 파일은 수정하지 않았다.
   정렬 순서와 머티리얼은 프리팹이 정한다(PieceOrder 10). refresh는 오류 없음, 전체 154/154 통과.

재캡처: 위 2·3번 명령을 같은 인자로 다시 실행한다(새 파일명 권장: `02-items-placed-v2.png`, `03-habitat-complete-v2.png`).

## 알려진 사항
- NpcRecruiter는 GameSession이 이전 `HabitatEvaluator`로 생성한다. Bootstrap을 고치지 않기 위해 `ItemsInstaller`가 리플렉션으로 `_habitats`를 태그 서식지로 교체한다(TODO: Bootstrap에서 직접 주입).
- 아이템·태그 이름은 카탈로그 JSON(한국어)이 단일 출처이고, HUD의 고정 문구만 `ko.json`(`inv_*`)에서 읽는다.
- USS 파일명은 KoreanHud와 같은 이유(Resources 이름 충돌)로 `InventoryHudStyle.uss`다.
