# 스토리 017 표현·HUD 확인 절차 (NPC 배회·클릭·대화 패널)

대상: `InteriorNpcWalker`(집 기준 배회, `NpcBody`, `INpcClickSource` 등록), `HubPhaseNpc.prefab`(트리거 BoxCollider2D), `NpcDialogHud`(+ `NpcDialog.uxml` / `NpcDialogStyle.uss`).

## 컴파일·프리팹

```
python -I tools/bridge/bridge.py refresh --timeout 400
python -I tools/bridge/bridge.py execute-method --method IronRing.Presentation.HubPhase.Editor.HubPhasePrefabBuild.Generate
python -I tools/bridge/bridge.py compile-status
```

기대: `errors` 빈 값, `HubPhaseNpc.prefab`에 `BoxCollider2D`(`m_IsTrigger: 1`).

## 캡처 명령 (좌표는 절대 경로로 바꿀 것)

```
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 260 --screenshot <abs>/017-homes.png "--extra-args=--npc-demo --npc-stage homes"
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 260 --screenshot <abs>/017-panel.png "--extra-args=--npc-demo --npc-stage panel"
python -I tools/bridge/bridge.py play-capture --scene Assets/Scenes/Main.unity --after-frames 260 --screenshot <abs>/017-encouraged.png "--extra-args=--npc-demo --npc-stage encouraged"
```

`<abs>` = `C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-017-npc-homes-dialogue`

## 기대 화면

1. **homes**: hub-1 내부 화면. 서식지 두 종류가 완성되어 있고, NPC 두 명 이상이 각자 집 서식지 칸과 그 상하좌우 빈 칸 위에만 서 있다(거점 전체에 흩어지지 않음). 대화 패널은 보이지 않는다.
2. **panel**: 같은 화면 왼쪽 아래(핫바 위, 상단 배너·서식지 패널과 겹치지 않음)에 대화 패널. 제목 = 직업명, 오른쪽에 "집 <서식지 이름>", 대사 상자에 첫 대사, 버튼 [대화] [격려] [닫기]. 패널이 열린 NPC는 노란빛으로 칠해진다. 결과 줄은 숨김.
3. **encouraged**: 데모 드라이버가 격려를 실행한 뒤 패널이 열린다. [격려] 버튼은 회색(비활성), 버튼 아래와 결과 줄에 비활성 이유(주황), 상단 HUD의 hub-1 번영 숫자가 +N 반영.
   - 주의: 드라이버가 격려를 먼저 실행하므로 "번영 +N" 결과 문장 자체는 HUD가 받지 못한다(계약에 결과 문장 전달 경로 없음). 결과 문장을 화면에 보이려면 드라이버가 격려를 건너뛰고 HUD의 `NpcDialogHud.HandleDemoStage("encouraged")`가 버튼과 같은 경로(`Press`)로 누르게 하면 된다(격려가 활성일 때 자동으로 누름).

## 수동 확인(편집기 플레이)

- 핫바 슬롯을 고른 상태에서 NPC 좌클릭 → 패널이 열리지 않고 배치만 처리.
- 패널·핫바 위 클릭 → NPC 클릭이 발생하지 않음(`PlacementGhost.UiHitTests` + EventSystem).
- [대화] → 대사가 다음 줄로 바뀜, Esc·[닫기]·출발(내부 화면 닫힘) → 패널 닫힘, 다른 NPC 클릭 → 그 NPC로 전환.
