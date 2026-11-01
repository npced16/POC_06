# Story 017 코디네이터 검증 (2026-11-01 18:05 KST)

- 컴파일: refresh/compile-status 오류 없음. Main.unity isDirty=false.
- 결정: encouraged 단계의 격려는 드라이버가 아니라 HUD가 버튼과 같은 경로로 누른다(`NpcHomesDemo` `_driverEncourages` 기본 false). 결과 문장 "격려했다. 번영 +3"이 패널에 보이고 버튼이 비활성(이유 표시)되는 것을 확인.
- 캡처 3장: `017-homes.png`, `017-panel.png`, `017-encouraged.png` (명령: `play-capture --scene Assets/Scenes/Main.unity --after-frames 260 "--extra-args=--npc-demo --npc-stage homes|panel|encouraged"`).
- 관찰: 격려 뒤 상단 배너가 "주조소 · 정착 · 번영 3"으로 바뀜 — 번영 3에 정착 단계가 되는 것은 014의 단계 임계값(데이터) 문제이므로 밸런스 항목으로 남김.
- 알려진 한계: 격려 횟수는 ITrackRuntime에 출발 이벤트가 없어 다음 도착(Arrived) 때 초기화된다. NPC 콜라이더가 칸보다 먼저 맞으면 그 칸 클릭은 나지 않는다(빈 손일 때만 NPC 클릭이 있으므로 배치와는 충돌하지 않음).
