# 스토리 033 — 습격 스프라이트 검증 (아트)

작성: technical-artist 서브에이전트, 2026-11-02

## 상태: 생성기 작성 완료, 브리지 실행 대기(BLOCKED)

열린 편집기가 15:39~15:53(UTC) 내내 Play 모드에 있어 브리지 `refresh`가 두 번 모두
`Command requires Edit mode`로 거절됐다. 그래서 컴파일 확인, PNG 생성, 콘택트 시트 검토,
카탈로그 할당은 아직 하지 못했다. 편집기가 Edit 모드로 돌아오면 아래 순서로 실행한다.

```
python -I tools/bridge/bridge.py --timeout 600 refresh
python -I tools/bridge/bridge.py --timeout 600 execute-method --method IronRing.Art.Editor.SpriteGenRaid.Generate
python -I tools/bridge/bridge.py --timeout 600 execute-method --method IronRing.Art.Editor.SpriteGenRaid.AssignCatalog
```

`Generate`는 끝에 `AssignCatalog`를 스스로 부르며, 카탈로그 에셋이 아직 없으면 아무것도 바꾸지 않고
"RaidCatalog not found … (skipped)"를 돌려준다. `RaidBuild.Generate`가 에셋을 만든 뒤 `AssignCatalog`를 한 번 더 부르면 된다.

## 생성기

- `Assets/Scripts/Art/Editor/SpriteGenRaid.cs` — `IronRing.Art.Editor.SpriteGenRaid.Generate`, 메뉴 `IronRing/Art/Generate Raid Sprites`
- 색은 모두 `Assets/Art/Palette/SteampunkPalette.json`(ArtKit). 임포터: PPU 64, Point, 무압축(`ArtKit.ConfigureImporter`).
- 출력 `Assets/Art/Generated/raid/`, 콘택트 시트는 이 폴더로 복사(`raid_contact_sheet.png`).

## 스프라이트 목록

| 파일 | 크기 | 내용 |
|---|---|---|
| raid_spider_0/1.png | 48×48 | 증기 거미. 구리 보일러 몸통(황동 리벳 띠, 녹 점, 압력계), 뒤쪽 굴뚝 배기구와 증기, 어두운 바이저 속 호박색 눈 3개, 황동 송곳니, 다리 8개(쇠, 황동 관절). 프레임마다 다리 짝이 번갈아 들리고 몸통이 1px 흔들린다. |
| raid_automaton_0/1.png | 64×64 | 녹슨 자동인형. 넓은 구리 원통 몸통(리벳 띠 2줄, 녹 줄무늬), 가슴의 화실 창살(불씨 빛), 황동 어깨 갑옷, 어깨 굴뚝 증기, 금 간 외눈 렌즈(호박색, 광륜). 팔 흔들기: 앞팔은 3px 내려 밝게, 뒷팔은 3px 올려 한 단 어둡게, 프레임마다 좌우가 바뀐다. |
| raid_nest.png | 96×96 | 몬스터 둥지. 금 간 쇠 보일러 벌집(구리 띠·리벳), 빛나는 균열과 둥근 증기창 3개, 아래쪽 황동 이빨이 박힌 아가리 속 눈빛, 양옆 파이프, 위 굴뚝 증기, 바닥의 톱니·쇠막대 "뼈". |
| raid_nest_broken.png | 96×96 | 같은 둥지가 주저앉음: 높이 낮아지고 윗부분 들쭉날쭉 뜯김, 굴뚝은 옆으로 쓰러짐, 빛 없음(창·균열 어둡게), 잔해 추가, 전체 35% 어둡게. |
| raid_shell.png | 16×16 | 오른쪽을 향한 황동 포탄(구리 탄두), 왼쪽으로 옅어지는 짧은 꼬리. 코드에서 진행 방향으로 회전해 쓴다. |
| raid_hit.png | 32×32 | 흰빛-호박색 번쩍임 별(십자 큰 빛살 + 대각 작은 빛살, 흰 중심, 불꽃 점). |
| terminal_occupied.png | 128×128 | 폐허 거점 배경: 어둡게 낮춘 돌바닥(가장자리 들쭉날쭉 투명 페이드), 이끼·그을음·발톱 자국, 녹슨 크레인(격자 탑·조종실·지브·끊긴 고리), 가운데가 무너진 승강장, 기울어진 꺼진 가스등, 흩어진 상자·배럴, 넘어진 간판, 그늘 속 호박색 눈 3쌍. |

## 디자인 메모

- 브리프 Art & audio direction 준수: 짙은 외곽선(outline #322438, `EndLayer`), 2~3단 명암, 황동·구리·녹, 호박색 빛(#ffcf6a), 연기(#f2decd). 피·점액 같은 판타지 요소 없음, "위협"은 빛나는 눈·송곳니·이빨 아가리·발톱 자국으로 낸다.
- 거미는 작고 빠른 적이라 실루엣을 넓게(다리 폭 약 42px), 자동인형은 느리고 단단한 적이라 몸통을 크고 각지게 했다. 크기 차이(48 대 64)가 그대로 체급 차이로 읽힌다.
- 바닥색 위 가독성 확인용으로 콘택트 시트 아랫줄은 실제 크기, 윗줄 두 줄은 정수 배 확대.

## 브리지 결과

| 명령 | 결과 |
|---|---|
| refresh (15:39 UTC) | 실패 — `Command requires Edit mode` (편집기 Play 중) |
| scene-status 폴링 15:40~15:52 | `playing: true` 계속 |
| refresh 재시도 (15:53 UTC) | 실패 — 같은 이유 |

## 카탈로그 할당

미완. `Assets/Prefabs/Raid/RaidCatalog.asset`이 아직 없다(작업 시점). `SpriteGenRaid.AssignCatalog`(인자 없음, SerializedObject로
`_spiderFrames`, `_automatonFrames`, `_nest`, `_nestBroken`, `_shell`, `_hit`, `_terminalOccupied`를 필드 이름으로 채움, 빠진 필드는 결과 문자열에 보고)를 준비해 두었다.

## 남은 일

1. 편집기가 Edit 모드일 때 위 세 명령 실행, 컴파일 오류가 있으면 수정.
2. `raid_contact_sheet.png`를 열어 실제 크기 가독성 검토, 필요하면 수치 조정 후 재생성.
3. RaidCatalog 생성 후 `AssignCatalog` 실행.
