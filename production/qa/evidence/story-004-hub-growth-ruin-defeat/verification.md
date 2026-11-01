# Story 004 verification

Implementation: pure `RunState : IHubStateSystem`, immutable per-hub definitions, JSON profile loader, local interface fakes, isolated MonoBehaviour demo and scene builder.

Provisional data: prosperity 0..100; initial 50; ruin at 0; base -5/lap; +3/NPC; +2/facility. Trade facility scale 1.5, shrine NPC scale 1.5, mine decay scale 0.5, farm decay scale 1.5. Ruins are permanent.

## Completed engine verification (2026-11-01)

The initial worker launch was blocked by automatic approval review. The coordinator subsequently ran the authorized tests, build and player capture; `coordinator-validation.txt` records lock acquisition/release and exit codes 0 for each operation. This resumed dispatch independently inspected the XML, build/player logs, both PNGs and current source hashes.

`stamp.txt` records source, data, test and generated scene bytes using SHA-1 over the standard blob header plus file bytes, calculated without invoking Git CLI. All nine original source/data hashes matched the current files before completion; no runtime code changed in this resumed dispatch.

The project.yaml test command with `-testFilter IronRing.Hub.Tests` produced `editmode.xml`: 12 total, 12 passed, 0 failed, 0 skipped. The coordinator's subsequent full EditMode run in `editmode-all.xml` passed 67/67. Engine runs use the corrected external mutex `C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock`, check for existing Unity processes, and release only after the editor exits.

The mutex-protected build used Unity `-batchmode -quit -projectPath . -executeMethod IronRing.Hub.Editor.HubDemoBuild.Build -logFile production/qa/evidence/story-004-hub-growth-ruin-defeat/build.log`; the log reports `Build Finished, Result: Success.` The generated `Assets/Scenes/Demo/Story004/HubDemo.unity` and `Builds/Demo/Story004/Game.exe` exist. Scene creation used Unity APIs and did not change shared build settings.

Run that player without the editor mutex, with `-screen-width 1280 -screen-height 720 -screen-fullscreen 0 --hub-evidence C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-004-hub-growth-ruin-defeat -logFile C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-004-hub-growth-ruin-defeat/player.log`. The dedicated demo scene is first in this isolated build.

The scenario ruins the first hub, traverses the complete retained ring, docks at the ruined hub and captures `01-ruined-hub.png` after 90 rendered frames. It then ruins every hub, captures `02-run-lost.png` after 90 more frames, and exits after allowing screenshot writes to finish. The inspected player log confirms `completed ring and docked at ruined trade; nodes=4; lost=False`, then `every hub ruined; lost=True; nodes=4`.

The first inspected 1280x720 image shows grey broken rubble at TRADE, a train dock marker at that ruin, three standing golden hubs, a connected ring and RUN CONTINUES. The second shows all four hubs as rubble, the intact ring, RUN LOST, and disabled run controls. Text is legible and contained. These are real built-player frames; the demo's train/track and populations are local simulations. Cross-story Track integration and manual input/feel proof remain the coordinator's integration scope, not claims of this isolated story verification.

## Test coverage

- Per-hub initial/bounds: `InitialValueAndLimitsComeFromEachDefinition`.
- Four distinct shipped growth profiles: `ShippedProfilesGrowDifferentlyWithoutHubBranches` (loads actual JSON through AssetDatabase).
- Live interface counts and once-per-lap notification: `LapReadsCurrentPopulationThroughInterfaces`.
- Threshold, no revival: `ExactThresholdLatchesRuinPermanently`.
- 1, 4, 5, 7 hubs: `DefeatUsesEveryHubRatherThanFour`.
- Retained/queryable hub identities: `RuinsRetainEveryTrackIdentityAndRemainQueryableOnVisits`.
- Ordered one-shot events: `EventsDescribeChangesAndDefeatExactlyOnce`.
- Natural lap-decay defeat: `PerLapDecayCanRuinTheEntireRun`.
- Invalid/duplicate/empty/nonfinite data: `RejectsEmptyDuplicateAndNonFiniteData`.

Run result: OBSERVED — inspected 01-ruined-hub.png and 02-run-lost.png: visible rubble, ruined-hub docking, intact ring, continuing run with surviving hubs, and RUN LOST only after all hubs fall.
