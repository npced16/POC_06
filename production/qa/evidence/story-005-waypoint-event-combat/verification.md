# Story 005 verification handoff

Source frozen for coordinator-owned Unity verification (2026-11-01).

## Observed results

Coordinator run: 67/67 EditMode tests passed, including 13/13 Combat cases (worker parsed the retained `editmode-all.xml`). Demo `build.log` ends with exit 0. Coordinator used the following retained capture filenames, which the worker opened and visually verified:

- `01-combat-in-progress.png`: round 1, held, stocks 5, prosperity 50.
- `02-combat-result.png`: victory after round 3, scrap 8, released.
- `03-combat-defeat.png`: defeat after round 3, scrap 3, prosperity 40, released.

Layout and values match the expectations below. Concrete Story001/004 integration remains a separate coordinator-owned proof gate.

## Tests

- Filter: `IronRing.Combat.Tests` (13 cases).
- File: `Assets/Tests/EditMode/Combat/Editor/WaypointCombatTests.cs`.
- Combined IronRing test run is acceptable; retain XML and log here.
- Expected: all 13 cases pass. No runtime assembly definitions were added.

## Build

```text
"C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod IronRing.Combat.Editor.Story005Build.Build -logFile production/qa/evidence/story-005-waypoint-event-combat/build.log
```

The method lives at `Assets/Scripts/Combat/Editor/Story005Build.cs`. It creates `Assets/Scenes/Demo/Story005/Story005Combat.unity` only if absent, assigns its JSON TextAsset through SerializedObject, and builds SampleScene plus Story005Combat without editing shared build settings. Existing authored demo scene values are preserved.

## Capture

Run each command from the project root; the executable exits after the capture through the existing shared ScreenshotOnArg.

```text
Builds/Demo/Story005/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene Story005Combat --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-005-waypoint-event-combat/combat-progress.png --after 60
Builds/Demo/Story005/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene Story005Combat --combat-result --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-005-waypoint-event-combat/combat-victory.png --after 60
Builds/Demo/Story005/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene Story005Combat --combat-result --combat-defeat --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-005-waypoint-event-combat/combat-defeat.png --after 60
```

Expected progress: COMBAT IN PROGRESS, Round 1 TRAIN WINS (possibly Round 2 if capture rendering is slow), HELD AT WAYPOINT, train strength 10 vs raider strength 8, four resource stocks 5 each, next hub prosperity 50.

Expected victory: three TRAIN WINS rounds, VICTORY, RELEASED - ROUTE CONTINUES, scrap 8, grain/coal/water 5, prosperity 50.

Expected defeat: three RAIDERS WIN rounds, DEFEAT, RELEASED - ROUTE CONTINUES, scrap 3, grain/coal/water 5, prosperity 40.

Capture flags choose deterministic local demo fakes and fast-forward actual resolver steps for result capture; displayed results are not mocked labels. Interactive replay runs one step every 2 seconds. No real-input or final concrete cross-story integration proof is claimed by these screenshots.

## Integration

Construct `WaypointResolver(data, track, inventory, hubs, facilities, random)` using contracts; wire `track.Arrived += resolver.OnWaypointArrived`; call `resolver.Advance()` once per UI/event step while active. Unwire arrivals and dispose on teardown. `Combat`, `LastOutcome`, `EventTitle` provide presentation state. JSON ids match the integration catalog: scrap, grain, coal, water.

The coordinator owns final Story001/004 concrete composition proof. Keep the corresponding story AC open until that proof exists.
