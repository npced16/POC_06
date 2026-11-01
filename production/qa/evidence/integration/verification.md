# Iron Ring real-system integration verification

## Status
Verified on 2026-11-01. The coordinator ran Unity and both player passes; the worker inspected the XML, logs and all four final 1280x720 PNGs. Full regression passed 69/69, then the strengthened integration suite passed 2/2. The final Main build and autoplay player both exited 0; the Resource/Slot layout defect found in the first capture was fixed and the final images are readable.

## Composition
MainRoot creates GameSession using the controller-owned RingTrack and seven production JSON assets. GameSession wires arrivals to NpcRecruiter and WaypointResolver, and lap completion to RunState; HabitatEvaluator reads docking from the same track. FacilityLink resolves constructor ordering and forwards every request to the real FacilityBuilder. HudController and HudDocument bind the real seven interfaces. No fake subsystem is used.

Canonical hub IDs are hub-1 through hub-4, with authored display names. Resources are coal, scrap, water and grain. MainSettings.json owns demo timing, seeds, target hub and facility. Separate seeded random streams produce an initial victory, a defeat with penalties, and an eligible next-visit NPC join; gameplay outcomes still pass through the production rules.

## Coordinator execution commands
Working directory: C:/Users/JUNGLE/Documents/GitHub/POC_06. Unity and player processes must be serialized by the coordinator.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe' -batchmode -runTests -projectPath . -testPlatform EditMode -testFilter IronRing.Integration.Tests -testResults production/qa/evidence/integration/editmode-integration-2.xml -logFile production/qa/evidence/integration/editmode-integration-2.log
& 'C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe' -batchmode -quit -projectPath . -executeMethod IronRing.Bootstrap.Editor.MainBuild.Build -logFile production/qa/evidence/integration/build-2.log
& './Builds/Demo/Main/Game.exe' -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --integration-demo --evidence-dir C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/integration -logFile C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/integration/player-2.log
```

MainBuild.Build creates Assets/Scenes/Main.unity once through EditorSceneManager and preserves an existing Main scene on reruns. It puts Main first in EditorBuildSettings.scenes, so the unmodified project.yaml commands.build also produces a playable Builds/Win64/POC_06.exe. The dedicated build contains Main and writes Builds/Demo/Main/Game.exe. Launch without --integration-demo for manual controls: select hub, depart, place/return resources, assemble the displayed recipe, build a defense, and optionally advance combat steps; combat also advances on an authored timer.

## Expected checkpoints, retained and visually verified
- 01-main-hud.png: initial real inventory with all four resource IDs, four hubs and route nodes.
- 02-combat.png: a real waypoint encounter holding the train, with round/strength display.
- 03-habitat-npc.png: hub-2 recipe ready, an NPC joined on a later visit, and one constructed wall.
- 04-ruin.png: at least one ruined hub still listed on the unchanged ring, with other hubs surviving.

The demo exits automatically after its fourth capture. IntegrationFlowTests additionally proves the initial victory reward, defeat resource/prosperity penalty, later-visit recruitment, facility count, lap decay, continued travel to ruins and loss only when all hubs are ruined. Terminal damage in the test completes the all-ruined assertion; the player demonstration uses ordinary laps and combat until the first ruin.

## Changes outside Bootstrap and integration evidence/tests
- Track/ring-track.json: station IDs aligned with hub-1 through hub-4.
- Hub/Data/HubProfiles.json: profile IDs aligned; tuning preserved.
- UI/Story007/DemoData.json: demonstration catalog aligned to the canonical four hubs and four resources.
- Track/TrainController.cs: explicit presentation and keyboard options allow integrated UI ownership; UnityEngine.Debug is qualified. Movement rules are unchanged.
- Tests/EditMode/Hub/Editor/HubStateTests.cs: four fixture hub ID literals aligned, with unchanged expected values; explicitly authorized by the coordinator.
- Story005 final composition AC is checked, both Status lines are verified/Done, and all seven EPIC table entries now match their Done story files.

No git commands, assembly definitions, or hand-edited scene YAML were used. Unity and player execution belongs to the coordinator. Manual mouse interaction is separate from autoplay evidence.

## Execution evidence
- `editmode-all.xml` and `editmode-all.log`: 69/69 tests passed with filter IronRing, including the original two integration tests.
- `editmode-integration-2.xml` and `editmode-integration-2.log`: final strengthened IronRing.Integration.Tests suite passed 2/2, including exact per-hub lap changes from real NPC and facility counts.
- `build-2.log`: final IronRing.Bootstrap.Editor.MainBuild.Build completed successfully and produced Builds/Demo/Main/Game.exe. Initial build evidence remains in build.log.
- `player-2.log`: final autoplay exited 0 and recorded checkpoint states: hub-1/lap 0; NORTH PASS/lap 0; hub-2/lap 1/NPCs 1; hub-1/lap 6/NPCs 2/ruins 2. Initial player evidence remains in player.log.
- Final 03 image shows Garden/Shrine with water/grain/grain/empty slots, habitat ready, NPCs 1 and Facilities 1. Final 04 shows Foundry and Citadel ruined while Garden (51.5) and Harbor (15) survive; all eight route nodes and four hub rows remain.
- Assets/Scenes/Main.unity and Bootstrap/UI/MainPanel.asset were generated through the editor builder. ProjectSettings/EditorBuildSettings.asset has Main enabled first, followed by SampleScene; this settings mutation is required by the integration task. The separate project.yaml build command was not rerun; its configured scene ordering was inspected.

## Retained image SHA-256
- 01-main-hud.png: BB8F2FE60ECEA3C7F7AA747680C0BC666B7ECED85DBEB92C01302BC897215185
- 02-combat.png: B2470AE94A902F2DF6B908E0E9AA6DB17F516F7F640834C267043BCF75C5AD8C
- 03-habitat-npc.png: 86B06D395AAC71CE3BFEAACE986DB03ACE6B74ED56FF87F2DEB7F0AE82DFBC99
- 04-ruin.png: 3545B8443506C0B184ED3E9F2B81B7398AEE5EED62B47FC626F3C8DE12801F89
