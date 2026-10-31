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
| 001 | 고리형 선로와 열차 이동 | Logic | Ready | N/A |
| 002 | 서식지 조합 판정과 이벤트 플래그 | Logic | Ready | N/A |
| 003 | 고유 NPC 풀과 확률 합류 | Logic | Ready | N/A |
| 004 | 거점 성장·쇠락·폐허 전환과 패배 판정 | Logic | Ready | N/A |
| 005 | 경유지 이벤트와 간단한 전투 | Integration | Ready | N/A |
| 006 | 방어시설 건설 | Integration | Ready | N/A |
| 007 | 최소 HUD | UI | Ready | N/A |

## Out of scope

싱글플레이 전용, 거점 5곳 이상 (구조는 확장 가능하게), 분기 선로, NPC 대화 트리, 스토리 컷신.
