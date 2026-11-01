# Story 002 verification

## Implementation

- `IronRing.Inventory.ResourceInventory` implements `IResourceInventory` with integer stock transfers, change notifications, invalid-amount validation, and independent read-only snapshots.
- `IronRing.Habitat.HabitatEvaluator` implements `IHabitatSystem` and consumes only `IResourceInventory` and `ITrackRuntime` interfaces. It allows edits only while held at the matching hub, refuses invalid/occupied/unaffordable placement, refunds reclamation, and checks the exact ordered recipe after every successful edit.
- `HabitatDefinition` is immutable pure C# data. `HabitatConfiguration` adapts JSON to validated definitions and stock. No Unity API is used by inventory, definitions, or evaluation.
- The scene-specific `HabitatDemoView` displays stock, four placement slots, the recipe, completion, and event flag. Its local track fake is deliberately separate from Story 001.

## Provisional data

`Assets/Scripts/Habitat/Resources/Story002/habitats.json` defines coal, scrap, water and grain, six starting units each, four slots per hub, and sticky flags for every hub.

| Hub | Slot 1 | Slot 2 | Slot 3 | Slot 4 |
| --- | --- | --- | --- | --- |
| hub-1 | coal | scrap | water | empty |
| hub-2 | water | grain | grain | empty |
| hub-3 | scrap | coal | coal | empty |
| hub-4 | grain | water | scrap | empty |

Recipes require exact slot order. An empty string in JSON becomes an explicitly empty slot requirement, not a wildcard. Current completion becomes false when the recipe breaks; the default sticky event flag remains true and does not raise its event again on recompletion. Non-sticky policy is also supported through data.

## Automated evidence

Twelve tests (8 Habitat, 4 Inventory) authored under `Assets/Tests/EditMode/Habitat/Editor` and `Assets/Tests/EditMode/Inventory/Editor`, without adding assembly definitions:

- Inventory transfer and event quantities; insufficient/unknown stock; snapshot and seed isolation; negative and overflow inputs.
- Place/reclaim/refund and sticky flags; repeatable side-effect-free queries; insufficient stock atomicity; docking, resource, occupancy and slot bounds; distinct per-hub ordered patterns; non-sticky rising edges; exact empty-slot requirements; immutable and validated definitions.

| Acceptance criterion | Evidence |
| --- | --- |
| Data-defined slot counts and allowed resources | `EachHubUsesItsOwnOrderedPatternAndSlotCount`, `PlacementRequiresAllowedResourceEmptySlotAndCurrentDock`, validated `habitats.json` |
| Placement/reclamation only at the docked hub, with inventory transfer | `CompletionAndStickyFlagFollowSuccessfulPlacement`, `PlacementRequiresAllowedResourceEmptySlotAndCurrentDock`, `StockTransfersAndNotificationsAreExact` |
| External per-hub ordered combos | JSON definitions, `EachHubUsesItsOwnOrderedPatternAndSlotCount`, `DefinitionsValidateAndOwnTheirInputs` |
| Re-evaluation after layout changes and event flag raising | `CompletionAndStickyFlagFollowSuccessfulPlacement`, `EmptyPatternPositionsMustRemainEmpty` |
| Deterministic queries | `QueriesAreDeterministicAndSideEffectFree` |
| Insufficient stock refusal without state mutation | `InsufficientStockIsAtomic`, `RejectedSpendDoesNotChangeStock` |
| Completion/flag queries and rising-edge notifications | `CompletionAndStickyFlagFollowSuccessfulPlacement`, `NonStickyPolicyIsControlledByDefinition` |
| Visible placed resources and habitat completion | `01-complete.png`, rendered 1280x720 player frame inspected after the USS overflow fix |

The first coordinator run exited 1 before test discovery: the original IronRing.Resources namespace shadowed UnityEngine.Resources in Track/TrainController.cs. At the coordinator request, implementation and test namespaces and folders were renamed to IronRing.Inventory, preserving meta files; the combined rerun passed 67/67 with all 12 Story002 tests passing (8 Habitat, 4 Inventory), verified directly from editmode-all.xml. The worker's mutex-wrapped `project.yaml` Unity EditMode command was rejected before launch by automatic tool approval with `blocked by policy`; this is not a test verdict. The coordinator accepted execution of tests, build and capture on behalf of this worker. The lock was moved outside project Temp because Unity cleans Temp on exit; the process must be awaited through Start-Process -Wait -PassThru or bash before releasing it.

## Reproduction

1. Under `C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock`, run the `project.yaml` test command with `-testFilter "IronRing.Habitat.Tests;IronRing.Inventory.Tests"` and `-testResults production/qa/evidence/story-002-habitat-combo-flag/editmode.xml`.
2. Under the same mutex, invoke Unity batchmode with `-executeMethod IronRing.Habitat.Demo.Story002Builder.Build`. The builder creates `Assets/Scenes/Demo/Story002/HabitatDemo.unity` only when absent and preserves later manual scene edits. It builds SampleScene and HabitatDemo to `Builds/Demo/Story002/Game.exe` without modifying shared build settings.
3. Run the built player windowed at 1280x720 with `--scene HabitatDemo --screenshot <absolute evidence path>/01-complete.png --after 60`. The demo fills hub-1 through the normal core API using its configured recipe. The coordinator-authorized shared `ScreenshotOnArg.cs` captures rendered frames.
4. Open the retained screenshot and inspect the resource slots, remaining inventory, completion label and raised flag.

Coordinator-centralized execution (source frozen after the Inventory namespace move and compact USS revision):

- Build method: `IronRing.Habitat.Demo.Story002Builder.Build`
- Player: `C:/Users/JUNGLE/Documents/GitHub/POC_06/Builds/Demo/Story002/Game.exe`
- Exact player arguments: `-screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene HabitatDemo --screenshot C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/story-002-habitat-combo-flag/01-complete.png --after 60`
- Expected PNG: `production/qa/evidence/story-002-habitat-combo-flag/01-complete.png`
- Expected visible state: hub-1 docked, slots coal / scrap / water / empty, stock coal=5 / scrap=5 / water=5 / grain=6, HABITAT COMPLETE, EVENT FLAG: RAISED, POLICY: STICKY.
- Test filter for this story: `IronRing.Habitat.Tests;IronRing.Inventory.Tests` (12 tests); the coordinator may run the combined `IronRing.*` suites and retain the common report here.

The first player build succeeded (`build.log`: `Build Finished, Result: Success`). Its 1280x720 capture showed the expected resources, stock, completion and flag, but the recipe labels and message extended outside their panel bounds. That frame is retained as `00-initial-overflow.png`. The stylesheet now resets inherited label/button margins and prevents flex shrinking of panel children; the replacement build succeeded (`build-r.log`) and the new `01-complete.png` was inspected: all recipe labels and the message are inside their panels with no clipped text. No C# or data changed after the passing test run.

Run result: OBSERVED — `01-complete.png` shows hub-1 docked, coal / scrap / water / empty slots, stock 5 / 5 / 5 / 6, HABITAT COMPLETE, and EVENT FLAG: RAISED / POLICY: STICKY; all text fits inside the corrected panels at 1280x720.

## Workflow notes

- Tier: minimal. No ADR, manifest or TR registry was required by this story.
- `docs/README.md` is absent; the explicit story, design brief and `/dev-story` instructions were used.
- The dispatch explicitly authorized tests despite the repository's general no-tests rule and authorized dependency isolation through a local fake track.
- No git CLI commands or hand-authored scene YAML were used.
- `Assets/Scripts/ScreenshotOnArg.cs` was created verbatim from `run-and-observe.md` after the coordinator explicitly expanded ownership for the shared helper.

## Final verification boundaries

- EditMode: 12/12 Story002 cases passed inside the coordinator-run 67/67 combined suite; retained `editmode-all.xml` and `editmode-all.log`.
- Player build: successful, retained `build-r.log`; demo scene and panel were generated through Unity Editor APIs.
- Runtime visual proof: coordinator confirmed player exit 0, retained `player-r-01-complete.log`, and the rendered screenshot was opened and inspected after the stylesheet correction; `00-initial-overflow.png` is historical, while `01-complete.png` is the final evidence.
- Placement, reclamation, refusal and flag semantics were exercised by deterministic core tests. The screenshot uses normal placement API calls during demo startup; physical mouse/keyboard clicks were not separately exercised.
- Content hashes in `stamp.txt` use the same SHA-1 blob format as review-receipts, computed without invoking the prohibited git CLI.
