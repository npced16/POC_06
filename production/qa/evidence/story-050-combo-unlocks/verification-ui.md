# Story 050 UI 검증 기록

## 변경 내용

- `Hotbar.json`의 기본 9칸 배정을 시작 타일 5종(보일러, 석탄더미, 벽돌길, 침상, 작업대)으로 바꿨다.
- `HotbarScreen`이 `IHabitatCodex.IsTileUnlocked`로 잠긴 아이템을 핫바와 팔레트에서 숨긴다. 처음 배정할 때 열린 타일만 빈칸에 채우고, `TileUnlocked` 이벤트가 오면 새 타일을 빈 슬롯에 추가한다. 9칸이 차면 팔레트에만 나타난다. 추가된 슬롯은 Inspector에서 조절할 수 있는 기본 1.5초 동안 강조 테두리가 깜박인다.
- `CodexScreen`은 잠긴 레시피 카드에 `???`와 실루엣을 유지하면서 재료 배열을 보인다. 열린 재료는 이름과 아이콘, 잠긴 재료는 `???`로 표시한다. `TileUnlocked`와 `RecipeUnlocked` 때 열린 도감을 갱신한다. 출처가 `combo`인 레시피는 로직의 묶음 토스트와 겹치지 않도록 도감 토스트를 추가하지 않는다.
- 도감 분모는 `HabitatCodex.TotalCount`를 사용한다. HUD 코드에 등급 상승 시 `레시피 해금` 문구는 없었다. 새 UI 문구가 필요하지 않아 `ko.json`은 수정하지 않았다. 로직의 `unlock_toast_tiles`, `unlock_toast_recipes`, `unlock_name_sep` 키를 사용한다.

## 연결한 API

`GameServices.Get<IHabitatCodex>()`: `IsTileUnlocked`, `UnlockedTileIds`, `TileUnlocked`, `IsUnlocked`, `RecipeUnlocked`. 도감 분모는 구현체 `HabitatCodex.TotalCount`를 읽고, 다른 구현체라면 카탈로그 건물 수를 사용한다.

## 확인 결과와 남은 확인

- `Hotbar.json` JSON 형식 확인: 통과.
- 브리지 `py -3 -I tools/bridge/bridge.py refresh --timeout 20`: 열린 POC_06 Unity 편집기의 응답이 없어 시간 초과. 명령 ID `8e5e48cb8d1747369b7ee4c20b391cfe`; 결과 파일은 생성되지 않았고, 남은 큐 명령은 제거했다.
- 따라서 Unity 임포트·컴파일, `BuildMainCanvas.Generate`, `scene-status`, Play Mode, 콘솔, 시작/해금 직후 캡처는 확인하지 못했다. 씬 YAML은 편집하지 않았다.
- 편집기가 열리면 refresh의 `failed:false` 확인 후 `.bridge/rebuild/Hotbar` 표식을 만들고 `BuildMainCanvas.Generate`를 실행해 변경된 기본 배정이 프리팹 편집 모드 미리보기에도 반영되는지 확인해야 한다. `scene-status`의 `isDirty:false`도 확인해야 한다.
- Play Mode에서 `--unlock-demo --unlock-stage start`로 시작 5종과 잠긴 팔레트 항목 숨김을, `--unlock-demo --unlock-stage first`로 증기관·굴뚝 자동 배정 및 1.5초 깜박임을 확인해야 한다. 9칸이 찬 경우 팔레트만 늘어나는지, 도감의 부분 재료와 `combo` 토스트 중복이 없는지도 확인해야 한다.
