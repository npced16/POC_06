# Story 050: 조합 해금 — 건물을 처음 완성하면 새 타일이 열린다

> **Epic**: 황동골 철도 회사 MVP (iron-ring-train)
> **Status**: Done
> **Layer**: Core + Presentation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`
**Requirement**: 사용자 지시(2026-11-02 11:50): "조합 등등 해서 새로운 아이템 해금하는 방식으로" → 질문 뒤 선택: **"건물 첫 완성 → 새 타일"**, 마을 등급은 **"주민·선로·승리만"**(레시피 해금에서 손 뗌).
지금은 타일 13종이 처음부터 다 열려 있고 레시피는 등급으로만 열려 "새로 알아낸다"는 맛이 없다. 기본 타일 5종으로 시작해, 건물을 처음 지을 때마다 다음 재료가 열리는 사슬로 바꾼다.

**ADR**: N/A (minimal) | **Engine**: Unity 6000.3.27f1 | **Risk**: LOW

---

## 건물 정리 (사용자 2026-11-02 12:00 "이상한 건 없애주라 건물 중엔")

- **뺀다(겹침)**: 가로등길 `lamp_road`(가스등 거리와 겹침), 대형 톱니공장 `grand_cogworks`(톱니공장과 겹침), 저수조 `cistern`(급수탑과 겹침), 공구점 `toolshop`(톱니공장과 겹침). `HabitatTypes.json`에서 제거하고 원본은 `archive/habitats-2026-11-02/`에 JSON 조각으로 보관. 그 건물 전용 NPC(patrol·chief_engineer·water_keeper·tinker)는 등장 후보에서 빠지게(데이터에서 제거 또는 비활성, 초상화 파일은 그대로).
- **재료 고침(어색함)**: 철골 창고 `iron_depot` = 철골3 + **작업대1**(굴뚝 대신), 전신소 `telegraph_line` = 전선2 + **철골1**(전신주, 가스등 대신).
- 남는 건물 15종.

## 해금 사슬 (사용자 "열리는 타일도 연관있게" — 데이터: `CodexUnlocks.json` 새 키)

- 시작 타일 `_starterTiles`: 보일러 `tile_boiler`, 석탄더미 `tile_coal`, 벽돌길 `tile_cobble`, 침상 `tile_bunk`, 작업대 `tile_bench`
- 건물 첫 완성 → 열리는 타일 `_tileUnlocks`:

| 처음 완성한 건물 | 재료 | 열리는 타일 | 연관 |
|---|---|---|---|
| 보일러실 `boiler_room` | 보일러2+석탄1 | 증기관 `tile_pipe`, 굴뚝 `tile_chimney` | 증기를 보낼 관, 연기를 뺄 굴뚝 |
| 석탄 야적장 `coal_yard` | 석탄 묶음 | 철골 `tile_girder` | 석탄으로 쇠를 녹여 철골 |
| 벽돌 광장 `cobble_plaza` | 벽돌길 묶음 | 가스등 `tile_gaslamp` | 광장을 밝히는 가스등 |
| 숙소 `barracks` | 침상3+작업대1 | 물탱크 `tile_tank` | 사람이 살면 물이 필요 |
| 철골 창고 `iron_depot` | 철골3+작업대1 | 톱니장치 `tile_gear` | 쇠로 기계 부품을 깎음 |
| 급수탑 `water_tower` | 물탱크/철골 패턴 | 온실유리 `tile_glass` | 물이 생기면 온실 |
| 가스등 거리 `lamp_street` | 가스등 묶음 | 전선 `tile_wire` | 불빛 다음은 전기 |

- 레시피 "알려짐" 규칙: **재료 타일이 모두 열린 레시피**는 자동으로 알려진다. 예외 `_recipeRequires`: `power_house` ← `engine_room`, `blast_furnace` ← `iron_depot`.
- 아직 모르는 레시피는 도감에 "???" 칸, 열린 재료만 이름이 보인다(예: "보일러 + ???").
- 13종 전부는 위 7단계로 열린다. 첫 완성 여부는 전역(잿골에서 처음 지어도 열림).

## Acceptance Criteria

- [ ] 시작 시 핫바·팔레트에는 시작 타일 5종만 보인다(잠긴 타일은 슬롯에서 빠지고, 팔레트에서도 안 보임). 장식(`deco`)은 그대로.
- [ ] 건물을 처음 완성하면 그 순간 표의 타일이 열린다: 토스트 "새 타일: 증기관 · 굴뚝"(1회), 핫바 빈 슬롯에 자동으로 들어가고(9칸이 차면 팔레트에만), 새 타일 슬롯은 잠깐 반짝임. 같은 건물을 두 번째로 지으면 아무 일도 없다.
- [ ] 새 타일로 재료가 다 갖춰진 레시피는 같은 순간 도감에 "알려짐"이 되고 토스트 "새 설계도: 배관 회랑"(여러 개면 묶어서 한 줄).
- [ ] 마을 등급은 레시피를 열지 않는다(`TownTiers.json` `_unlockRecipes` 비움 또는 무시). 등급 상승 토스트에서 "레시피 해금" 문구 제거. 주민 목표·잿골 개통·승리는 그대로.
- [ ] 기존 `_starters`/`_unlockOrder`/`_perLap` 해금(스토리 021)은 새 규칙으로 대체(코드는 새 규칙만 쓰게, 옛 키는 남겨도 무시).
- [ ] 데모 `--unlock-demo [--unlock-stage start|first|all]`: 보일러실을 지어 증기관·굴뚝이 열리는 로그 `UNLOCK: tile tile_pipe,tile_chimney by boiler_room`(이때 새로 알려지는 설계도는 없음 — 배관 회랑은 톱니가 열리는 철골 창고 때 알려짐), 사슬 끝까지 자동으로 지어 `UNLOCK: recipes known 15/15`, `UNLOCK: all tiles 13/13`.
- [ ] 검증: 브리지 refresh failed=false, 생성기 필요시 실행, scene-status isDirty=false, 데모 로그, 캡처(핫바 5종 시작 · 해금 직후) `production/qa/evidence/story-050-combo-unlocks/`.
- [ ] 브리프 `design/game-brief.md`: "타일은 기본 5종으로 시작, 건물을 처음 지으면 다음 타일이 열린다(조합 해금), 등급은 주민·선로·승리만" 반영.

## Implementation Notes

- 로직(Claude gameplay): `HabitatCodex`(새 규칙, `ITileUnlocks` 또는 IHabitatCodex append-only 확장: `IsTileUnlocked(itemId)`, `UnlockedTileIds`, `event TileUnlocked(string[] itemIds, string byRecipeId)`), 첫 완성 감지(기존 완성 이벤트), `TownTiers`(레시피 해금 제거), 데이터 JSON, 로직 토스트 문구, 데모 드라이버. `production/handoffs/ui-050.md` "로직 API" 절을 채운다.
- UI(codex): 핫바/팔레트 잠긴 타일 숨김·자동 추가·반짝임, 도감 "???" 칸과 부분 재료 표시, 해금 토스트 배치. `production/handoffs/ui-050.md`.
- 씬 YAML 직접 편집 금지(생성기만).

## Out of Scope

- 타일 두 개를 합성하는 별도 합성 칸, 자유 발견(레시피 숨김) 방식, 새 타일·새 레시피 추가.

## Files

- `Assets/Scripts/Habitats/**`, `Assets/Scripts/Items/**`(필요시), `Assets/Scripts/Economy/TownTiers.cs`·`Resources/Economy/TownTiers.json`, `Assets/Scripts/Contracts/IronRingContracts.cs`, `Assets/Scripts/Hud/**`(codex), `Assets/Prefabs/UI/**`(codex), `ko.json`, `design/game-brief.md`

## Test Evidence

**Story Type**: Integration — `production/qa/evidence/story-050-combo-unlocks/verification.md` + 캡처
