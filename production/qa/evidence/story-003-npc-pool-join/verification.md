# Story 003 verification handoff

Source frozen pending coordinator verification (2026-11-01). No worker Unity launches after coordinator notice 5.

## EditMode tests

Namespace filter: `IronRing.Npc.Tests` (11 cases in `Assets/Tests/EditMode/Npc/Editor/NpcRecruiterTests.cs`). The coordinator combined `IronRing.*` pass may be used; retain the XML and confirm all NPC test cases passed, not merely the overall exit status.

## Demo build

Under the coordinator mutex `C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock`, with no existing Unity.exe process:

```powershell
Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe' -ArgumentList '-batchmode -quit -projectPath C:/Users/JUNGLE/Documents/GitHub/POC_06 -executeMethod IronRing.Npc.Editor.Story003Build.Build -logFile production/qa/evidence/story-003-npc-pool-join/build.log' -WindowStyle Hidden -Wait -PassThru
```

Build entry: `IronRing.Npc.Editor.Story003Build.Build`.
Scene: `Assets/Scenes/Demo/Story003/NpcJoinDemo.unity`, created through EditorSceneManager only when absent.
Build: `Builds/Demo/Story003/Game.exe`; explicit build scenes include SampleScene and NpcJoinDemo; no shared EditorBuildSettings edits.

## Player capture

```text
Builds/Demo/Story003/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene NpcJoinDemo --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-003-npc-pool-join/01-npc-joined.png --after 60 -logFile C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-003-npc-pool-join/player.log
```

Expected image: `production/qa/evidence/story-003-npc-pool-join/01-npc-joined.png`.
Expected visible state: IRON RING / NEW ARRIVAL, name Ada Brass, quote Give me a furnace, and I will keep this ring alive., JOINED HUB-1. No clipped name or quote.

The demo scene references `NpcPool.json` and `NpcDemoVisit.json`; production chance stays 0.5, while injected demo roll 0.25 and selection 0 ensure this screenshot shows a successful join.

After the coordinator returns results: inspect XML NPC cases, open PNG visually, update story ACs/status and evidence, and stamp all changed source/data/test/scene files using Python hashlib in Reviewed-Content-Hash format. The standard helper invokes git hash-object, which is prohibited by this task; compute the identical Git blob SHA-1 without invoking Git instead.

## Final result

Completed: coordinator combined NPC cases 11/11 Passed; final build-2.log exit 0; player-2.log capture exit 0; 01-npc-joined.png independently visually inspected with corrected quotation marks. Story status Done. See evidence.md and stamp.txt.
