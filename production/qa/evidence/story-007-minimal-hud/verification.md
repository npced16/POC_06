# Story 007 verification ? complete

All seven acceptance criteria were verified against the dedicated scripted-fake demo. Real-system composition remains the integration coordinator's separate scope.

## Results

| Criterion | Evidence |
|---|---|
| Resource amounts by type remain visible | `01-normal.png`, `02-ruined.png`, `03-defeat.png`; Iron, Timber and Coal amounts remain at the top in every state |
| Every data-defined hub shows prosperity and ruin state | Five complete rows in all three PNGs, sourced from `IHubStateSystem.HubIds`; `DemoData.json` demonstrates a count beyond four |
| Current node and lap are visible | All three PNGs show Foundry / Lap 3 from the initial track snapshot; `HudController.OnArrived` and `OnLap` update these fields from events |
| Event-driven updates; no direct system-state mutation | Source inspection of all seven injected interfaces and subscription/disposal pairs; actual resource/habitat/facility updates observed after mouse orders; ruin and RunLost events emitted after binding in the capture fixtures |
| Defeat message when every hub is ruined | `03-defeat.png`: all five rows remain present, RUINED / prosperity 0; explicit RUN LOST banner and disabled gameplay orders (operation panel hidden) |
| No overlap at 1280x720 | Each required PNG was opened after the USS correction and independently checked as exactly 1280x720; all five rows and complete action hint are visible |
| Mouse hub selection, placement and construction | Actual native computer-use mouse clicks selected Glassworks, clicked PLACE RESOURCE, then BUILD DEFENSE; Iron changed 24 -> 23 -> 19 and Glassworks changed to Habitat ready / Facilities 1 |

The actual mouse result is retained as `04-mouse-actions-window.jpg`, the unmodified native window capture at 1282x752 including the OS frame. The three required engine PNGs remain exactly 1280x720. `player-manual.log` records `STORY007 PLACE accepted Glassworks:0 Iron` and `STORY007 BUILD accepted Glassworks Barricade`; no runtime exception was found in that log. No synthetic pointer dispatch or direct handler invocation was used. The demo was closed through its own window close button after retention, and the native window inventory confirmed it was gone.

Optional demo N/F12 keyboard probes did not respond to the injected keystrokes. They are not reported as passed, and `04-mouse-actions.png` was not produced. The native JPEG is the mouse evidence; no claim is made that a live keyboard-driven node/lap transition was observed. Initial node/lap display and the event handlers were verified separately as stated above.

## Build and test boundaries

- Coordinator rebuilt `IronRing.UI.Editor.Story007Build.Build` successfully after the layout correction: `build-r.log`, exit 0; executable `Builds/Demo/Story007/Game.exe`.
- Refreshed engine captures: `player-r-01-normal.log`, `player-r-02-ruined.log`, `player-r-03-defeat.log` (coordinator-reported exit 0).
- Combined EditMode results: `editmode-all.xml`, 67 passed, 0 failed, 0 skipped. These are other-system tests and a compile check, not HUD visual/input tests. No test source was written for this story.
- Initial attempts encountered external NPC/Track compile errors and editor contention. These were escalated without edits to those systems. The final build supersedes those failed attempts.
- First screenshots exposed default-theme label spacing clipping the last row and action hint. The only subsequent source change was `Hud.uss`: normalized label spacing, 32px field input height, and compact defeat rows that hide optional NPC/facility detail lines. The final PNGs were recaptured and reopened.

## Architecture and integration

`HudController` is pure C# and receives ITrackRuntime, IResourceInventory, IHabitatSystem, INpcRecruiter, IHubStateSystem, IWaypointResolver and IFacilityBuilder through its constructor. Initial snapshots seed cached presentation state; subsequent updates subscribe to Arrived, LapCompleted, Changed, EventFlagRaised, NpcJoined, ProsperityChanged, HubRuined, RunLost, EncounterStarted, EncounterEnded and FacilityBuilt. Dispose removes all subscriptions. There is no per-frame HUD polling.

`HudDocument.Bind(model, slotIndices, facilityIds)` receives the model and action catalogs from the composition root. Resource ids come from inventory snapshots/events; hub ids come from hub data. Explicit player commands call only IHabitatSystem.TryPlace and IFacilityBuilder.TryBuild. The HUD never spends inventory, changes prosperity, moves trains, recruits NPCs or resolves encounters itself. The caller owns the model; the view removes its own handlers on destruction/rebinding.

UI Toolkit was selected using `docs/engine-reference/unity/modules/ui.md`, the project's Unity 6.3 reference: dynamic elements, DropdownField and flex layouts fit this event-driven data list. UXML/USS separates layout from the core, and a ScrollView supports larger catalogs. Readability takes precedence over decorative brass styling.

## Demo defaults and ownership

`Assets/UI/Story007/DemoData.json` contains five named hubs, Iron/Timber/Coal with stock 24 each, prosperity 70, lap 3, slots 0/1/2, Barricade/Watchtower/Steam Turret and demo build cost 4. These are editable demonstration fixtures, not new gameplay balance defaults. The five-hub count deliberately verifies expansion beyond the brief's four-hub MVP.

The scene-local bootstrap uses only its own scripted fakes; real systems are not required for this demo. Scene creation and PanelSettings creation used Unity Editor APIs once and preserve existing assets on subsequent builds. No scene YAML, shared contracts, shared bootstrap or EditorBuildSettings was hand-edited. Unity automatically generated `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` and metadata; the coordinator explicitly authorized leaving those files at Unity's default location.

`docs/README.md` was absent; the supplied story and `/dev-story` minimal workflow governed implementation. No Git CLI command was run. `stamp.txt` is generated with Python hashlib using content-addressed blob hashes rather than invoking the repository helper, because that helper calls git hash-object.

## Reproduction

Only the coordinator launches Unity following the centralized execution notice. Build method: `IronRing.UI.Editor.Story007Build.Build`. Editor runs use the external mutex `C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock`, check for an existing Unity process before acquisition, and retain the lock until process exit.

Working directory: `C:/Users/JUNGLE/Documents/GitHub/POC_06`.

```text
Builds/Demo/Story007/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene Story007Hud --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-007-minimal-hud/01-normal.png --after 60
Builds/Demo/Story007/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene Story007Hud --ruined --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-007-minimal-hud/02-ruined.png --after 60
Builds/Demo/Story007/Game.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene Story007Hud --defeat --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-007-minimal-hud/03-defeat.png --after 60
```

For mouse verification, launch a normal instance with `--after 100000` and a screenshot path ending `05-session-end.png`; select Glassworks, Place Resource, and Build Defense. The required result is Iron 19, habitat ready and Facilities 1 at Glassworks. Retain a native window screenshot if the optional F12 demo capture key does not respond to automation.

Run result: OBSERVED ? normal, ruined and defeat HUDs at 1280x720 without overlapping or clipped elements; actual native mouse selection/place/build observed and retained in the evidence files above.
