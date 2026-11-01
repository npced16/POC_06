# Story 001 verification handoff

Source frozen for coordinator verification on 2026-11-01. Workers must not launch Unity; the coordinator owns the combined test pass and sequential demo builds.

## Source and assembly state

- Runtime: `Assets/Scripts/Track/`, namespace `IronRing.Track`, default Assembly-CSharp.
- Tests: `Assets/Tests/EditMode/Track/Editor/RingTrackTests.cs`, namespace `IronRing.Track.Tests`, default Assembly-CSharp-Editor, **10 cases**.
- No Track asmdefs remain. `RingTrack` implements `ITrackRuntime`; `TrackNode` implements `ITrackNode`.
- Both resource loads explicitly use `UnityEngine.Resources.Load`; the prior CS0234 log is stale and predates this fix.
- The opt-in demo bootstrap sets `Application.runInBackground = true`, so capture can progress without focus.
- Do not modify any scene or build settings. The already-enabled `Assets/Scenes/SampleScene.unity` is sufficient; the demo is created only with `--track-demo`.

## Exact editor commands

Working directory: `C:/Users/JUNGLE/Documents/GitHub/POC_06`.

Use the shared external lock `C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock`, check no Unity process is running, and wait for the launched process to exit before releasing the lock.

Combined test pass can use the `project.yaml` test command with `-testFilter IronRing`; this story alone uses:

```text
"C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe" -batchmode -runTests -projectPath . -testPlatform EditMode -testFilter IronRing.Track -testResults production/qa/evidence/story-001-ring-track-train-movement/editmode.xml -logFile production/qa/evidence/story-001-ring-track-train-movement/editmode.log
```

There is **no executeMethod** for this story: no scene generation is needed. Build the existing enabled SampleScene, with the story-specific output path:

```text
"C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe" -batchmode -quit -projectPath . -buildWindows64Player Builds/Demo/Story001/Game.exe -logFile production/qa/evidence/story-001-ring-track-train-movement/build.log
```

Use `Start-Process -Wait -PassThru` or explicit process `WaitForExit()` in PowerShell. A bare call to the GUI executable returns early.

## Exact capture invocation

No editor mutex is needed for this built player:

```text
Builds/Demo/Story001/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --track-demo true --track-screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-001-ring-track-train-movement/01-train-between-nodes.png -logFile C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-001-ring-track-train-movement/player.log
```

The `--track-screenshot` flag intentionally differs from the shared ScreenshotOnArg flag. TrainController captures only when at least 60 frames have rendered and actual travel progress is between 0.4 and 0.9, then exits after five more frames. The player log records source node, destination, progress and lap. It must be rebuilt after the runInBackground fix; the previous preview binary could pause while hidden.

Expected PNG: `01-train-between-nodes.png`, 1280 x 720, showing eight connected nodes, four large gold station markers, four smaller teal waypoint markers and an orange train between FOUNDRY and NORTH PASS. No exceptions should appear in player.log. A screenshot establishes visible layout and in-transit position; it is not a manual pointer/keyboard or feel test.

## Provisional data defaults

`Assets/Scripts/Track/Resources/Track/ring-track.json`: four stations and four waypoints, two seconds per edge, explicit station departure, auto-depart disabled by default, one-second station wait only in an explicitly enabled demo. Hub IDs are FOUNDRY, GARDEN, HARBOR and CITADEL. Positions, sizes, colours and text scale are external data.

## Verified evidence

- `editmode-all.xml` and `editmode-all.log`: coordinator's Unity EditMode run passed **67/67**, including **10/10 Track cases**. The worker independently parsed the retained XML; no failed Track cases exist.
- Source includes the shared contracts, stacked holds, explicit UnityEngine.Resources calls and demo-only background updates. Preliminary local compile artifacts and stale test logs were removed to avoid confusing them with the final combined run.
- `build.log` ends with successful batch exit code 0; the final standalone output is `Builds/Demo/Story001/Game.exe`.
- Coordinator's final result confirms that the player ran with `--track-demo true --track-screenshot ...`, exited 0 and retained the PNG below.
- `01-train-between-nodes.png` was opened and inspected by the worker: 1280×720, all eight nodes connected in a closed ring, four gold stations and four teal waypoints, no clipped labels, orange train between FOUNDRY and NORTH PASS, `LAP 0 / FOUNDRY / TRAVELLING`, and a disabled departure button while moving.
- Run result: OBSERVED — retained in-transit train frame at `production/qa/evidence/story-001-ring-track-train-movement/01-train-between-nodes.png`.
- All eight story acceptance criteria are verified. No manual pointer/keyboard playtest or movement-feel sign-off has been performed; the screenshot proves visible layout and an in-transit position.
- `stamp.txt` records SHA-256 hashes using PowerShell. The repository hash helper was not executed because it calls Git, which this task prohibits.
