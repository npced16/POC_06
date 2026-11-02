# Story 048 UI verification

## UI changes

- `TopBarScreen` displays the current town name and `IPopulation.ResidentsAt(CurrentHubId)`. It refreshes after population changes, station arrival, and interior changes.
- `HubPanelScreen` displays the selected town name, `ITownTier.TierNameAt`, local residents, prosperity, and `NextNeedsAt` requirements.
- `InteriorTitleScreen` displays the open town name and its local tier.
- `PhaseBannerScreen` reads current town from the town/population contracts, shows that town's tier and next requirements, and keeps the goal line tied to `hub-1`.
- When `ILineOpener.HubOpened` fires, `PhaseBannerScreen` queues the localized line-open banner for 2.5 seconds and swallows the following `line_opened` toast once to prevent overlap.
- `RunResultScreen` displays each hub in `IRunResult.Hubs` as town name, local tier, and local resident count. `IRunResult.Hubs` contains open towns per the Story 048 API contract.
- Added `ui_line_opened`, `ui_topbar_people_at`, and `ui_result_hub_line` at the end of `ko.json`.

## Bridge verification

- `py -3 -I tools/bridge/bridge.py refresh --timeout 600`: `failed=false`, `compiling=false`, `errors=""` after the UI changes.
- `py -3 -I tools/bridge/bridge.py execute-method --method IronRing.Hud.Editor.BuildMainCanvas.Generate --timeout 600`: succeeded. Rebuilt `TopBar`, `HubPanel`, `InteriorTitle`, `RunResult`, and `PhaseBanner` from their editor builders.
- `py -3 -I tools/bridge/bridge.py scene-status`: `Assets/Scenes/Main.unity`, `isDirty=false`, `playing=false`.
- `ko.json` parsed successfully and all three new UI keys were present.

## Verification limits

No Play Mode interaction or visual capture was performed, per the task instructions. Runtime appearance and the event sequence have not been visually inspected in the open Editor.
