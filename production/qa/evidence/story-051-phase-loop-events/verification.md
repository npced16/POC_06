# Story 051 검증 — 페이즈 루프 · 중간역 사건 · 타일 재고 (로직)

스펙: `production/epics/iron-ring-train/story-051-phase-loop-events.md` · UI 의뢰: `production/handoffs/ui-051.md` "로직 API"
작성: Claude gameplay, 2026-11-02. UI(배너·사건 창·재고 배지)는 codex 담당이라 캡처는 UI가 들어온 뒤 다시 찍는다.

## 컴파일 · 씬
- `python -I tools/bridge/bridge.py refresh --timeout 60` → `{"compiling":false,"errors":"","failed":false}`
- `scene-status` (데모 뒤) → `{"isDirty":false,"path":"Assets/Scenes/Main.unity","playing":false}`

## 데모 실행
`python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --seconds 32 --screenshot <이 폴더>/loop-demo-end.png --extra-args=--loop-demo --no-save-scene`
(`--extra-args`는 `=` 붙여 써야 한다. 값이 `--`로 시작해 argparse가 다음 인자로 오인함.)

실제 로그(Editor.log, 2026-11-02 13:33):
```
LOOP: phase Build lap=1
LOOP: demo start queue=[coal_merchant,drifter_mechanic,overturned_wagon]
LOOP: demo build boiler_room first=True tiles=[tile_boiler,tile_coal,tile_cobble,tile_bunk,tile_bench,tile_pipe,tile_chimney]
LOOP: demo depart lap=1
LOOP: phase Circulate lap=1
LOOP: event coal_merchant at REST-1 lap=1 choices=2
LOOP: stock tile_coal=3
LOOP: stock tile_boiler=1
LOOP: event coal_merchant choice 0 -> money-10, tiles tile_coal+3, tiles tile_boiler+1
LOOP: event drifter_mechanic at REST-2 lap=1 choices=3
PASSENGER: board gardener at REST-2 ...
LOOP: event drifter_mechanic choice 0 -> passenger gardener
LOOP: event overturned_wagon at REST-3 lap=1 choices=2
LOOP: stock tile_bench=1
LOOP: stock tile_bunk=1
LOOP: stock tile_cobble=1
LOOP: event overturned_wagon choice 0 -> tiles tile_bench+1, tiles tile_bunk+1, tiles tile_cobble+1
LOOP: phase Build lap=2
LOOP: demo returned lap=2 stock=[tile_coal=3,tile_boiler=1,tile_bench=1,tile_bunk=1,tile_cobble=1]
LOOP: stock tile_coal=2
LOOP: place tile_coal cell=0 fromStock=True stock 3->2 money 190->190
LOOP: demo done
```
기대값 대조:
| 수용 기준 로그 | 결과 |
|---|---|
| `LOOP: phase Build lap=1` | 있음 |
| `LOOP: event <id> choice <n> -> <effects>` ×3 (휴식지 3곳) | 있음(REST-1·2·3) |
| `LOOP: stock tile_coal=3` | 있음 |
| `LOOP: phase Build lap=2` | 있음 |
| 재고로 배치(돈 그대로) | `fromStock=True stock 3->2 money 190->190` |
| 합류한 승객이 승객 목록에 오름 | `PASSENGER: board gardener at REST-2`(기존 Passengers.Board 경로) |
| 버려진 화물칸 무작위 3 = 열린 타일만 | bench·bunk·cobble — 모두 시작 타일(열림) |

남은 캡처(UI 들어온 뒤): 건설 배너, 사건 창(각 휴식지 선택 전 1.2초 창), 핫바 재고 배지(복귀 직후). `loop-demo-end.png`는 데모 끝 화면(로직만).

## 데이터 키
`Assets/Scripts/Track/Resources/Track/RestEvents.json`
- 최상위: `_seed`(5101), `_autoChooseSeconds`(0 = 플레이어 대기), `_autoContinueSeconds`(4), `_disableAutoBoarding`(true), `_demoQueue`, `_demoBuild`(boiler_room), `_demoPlaceItem`(tile_coal), `_demoChooseSeconds`(1.2), `_demoContinueSeconds`(1.5), `_events[]`
- 사건: `_id`, `_title`, `_text`(ko.json 키), `_weight`, `_minLap`, `_choices[]`(`_label`, `_result`, `_resultFail`, `_effects[]`)
- 효과 `_type`: `tiles`(`_items[{_id,_count}]`, `_randomUnlocked`, `_loseRandom`, `_lockedMoney`) · `passenger`(`_npc` 빈 값 = 무작위, `_count`) · `money`(`_amount`, `_onReturn`) · `prosperity`(`_amount`, 거점 hub-1) · `delay`(`_seconds`) · `nothing`. 아무 효과에나 `_chance`(0..1, 선택지당 한 번 굴림, 실패 시 `_resultFail`).
- 사건 11종: coal_merchant, drifter_mechanic, overturned_wagon, refugee_family, sandstorm, water_keeper, track_crew, mail_sack, broken_automaton, lamp_keeper, ruined_signal(`_minLap` 2).
- 옛 자동 효과 끔: `Assets/Scripts/Combat/Resources/Story005Encounters.json` 모든 `_effect` = `"none"`, `_amount` = 0.
- ko.json 키(87개, 끝에 append): `rest_ev_<id>_{title,text,c<n>,r<n>,f<n>}`, `loop_phase_build`, `loop_phase_circulate`, `loop_phase_short_build`, `loop_phase_short_circulate`, `loop_continue`, `stock_badge`, `stock_price`, `rest_choice_no_money`, `rest_detail_{sep,tile,tile_lost,money,on_return,prosperity,passenger,delay}`.

## 규칙 결정(구현자 기록)
- 재고 결제: 재고 있으면 재고 1·무료(잔고 검사 건너뜀), 없으면 돈. 회수: 재고로 놓은 칸 → 재고 +1·환급 없음, 돈으로 놓은 칸 → 기존 환급. 칸별 기록(`ItemPlacement.IsStockPaid`).
- 페이즈: 선로 첫 역(hub-1) 정차 = Build, 그 밖 = Circulate. 순환 중 열린 다른 마을 정차는 Circulate 유지 + `CanDepart` true(기존 출발 규칙).
- 같은 바퀴 중복 없음: 거점 도착 때 초기화. 그 바퀴에 쓸 사건이 다 떨어지면 중복 허용.
- 돈이 드는 선택지는 잔고 부족이면 고를 수 없음(`IsChoiceAvailable` false).
- 우편 행낭 "배달": `_onReturn` 마르크를 다음 거점 도착 때 지급(`LOOP: pending money+12 paid at hub-1`).
- 옛 휴식지 확률 탑승은 끔(인내 바퀴 하차 규칙은 그대로).
