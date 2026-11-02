# 049 잠긴 마을 숨김 → 개통 시 폐허 등장 (사용자 2026-11-02: "아예 안보이다가 폐허 나타나게끔")

- `MapLockedHub`: 잠긴 동안 역 아래 렌더러 전부, 둘레 폐허 소품(MapRuinProps), 거점 장식 소품(씬 `Props/Prop_{거점 순번}_*`)을 끈다. 클릭도 끈다. 선로만 남는다.
- 개통(ILineOpener.HubOpened) 때 씬에 저장된 켜짐 상태로 되돌리고 `_revealSeconds`(1.5초) 동안 투명 → 불투명으로 나타난다. 라벨은 다른 마을과 같은 "잿골 / 폐허" 두 줄(048의 겹친 "잿골 · 폐허" 문구는 없앴다).
- 브리지 refresh failed=false, 예외 없음.
- 캡처: `049-locked-map.png`(잿골 자리 비어 있음), `049-opening-fade.png`(반투명), `049-opened-map.png`(완전히 나타남).
