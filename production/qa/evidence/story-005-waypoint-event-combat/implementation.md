# Story 005 implementation and evidence

## Implementation

- `WaypointResolver` implements `IWaypointResolver` and consumes only injected contracts. The composition root wires `ITrackRuntime.Arrived` to `OnWaypointArrived` and calls `Advance()` per presentation step.
- An arrival acquires the encounter hold before publishing its start. Events remain held until `Advance`; combat remains held through three rounds. Completion releases only the encounter's hold, then publishes the result. Duplicate arrival while active and duplicate advancement after completion are ignored.
- Ring list order determines the next hub, including wraparound. Its `CombatBonus` affects strength and its prosperity receives defeat penalties.
- `CombatEncounter` and `WaypointResolver` contain no Unity API calls. Unity JSON serialization is isolated in data and demo types.
- The demo uses local contract fakes; it does not prove integration with other stories' concrete implementations.

## Provisional data and combat rule

`Assets/Scripts/Combat/Resources/Story005Encounters.json` defines event probability 0.5, three rounds, train strength 10, enemy strength sampled uniformly from integers 8 through 14, victory +3 scrap, defeat -2 of a randomly selected configured resource and -10 prosperity to the next hub. The configured penalty resource ids are scrap, grain, coal and water, matching Story 002's catalog per coordinator instruction.

Each round consumes one injected float and wins when `roll < trainStrength / (trainStrength + enemyStrength)`. All three rounds run; a majority wins. Losses clamp to available inventory. Four sample events are abandoned freight (+2 scrap), trackside provisions (+3 grain), coal cache (+2 coal), and leaking water tank (-1 water).

## Verification commands

The project.yaml test command is used with the Combat filter and a retained result path:

```text
Unity.exe -batchmode -runTests -projectPath . -testPlatform EditMode -testFilter IronRing.Combat.Tests -testResults production/qa/evidence/story-005-waypoint-event-combat/editmode.xml -logFile production/qa/evidence/story-005-waypoint-event-combat/tests.log
Unity.exe -batchmode -quit -projectPath . -executeMethod IronRing.Combat.Editor.Story005Build.Build -logFile production/qa/evidence/story-005-waypoint-event-combat/build.log
```

Editor invocations wait for existing Unity processes, then atomically acquire `C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock`, use `Start-Process -Wait`, and release the lock in `finally`. The coordinator moved the lock out of project Temp because Unity cleans that directory.

The isolated build uses `Builds/Demo/Story005/Game.exe` and `--scene Story005Combat`. `--combat-result` advances actual resolver rounds before capture; `--combat-defeat` selects scripted losing rolls. Interactive replay buttons show the same rounds at the Inspector interval.

## Current evidence

The coordinator's combined EditMode run passed 67/67, including all 13 `IronRing.Combat.Tests` cases. The worker independently parsed `editmode-all.xml` and verified every Combat result is `Passed`; `editmode-all.log` is retained beside it. This establishes compilation and deterministic interface-fake logic, not final concrete cross-story composition or visual proof.

Actual successful invocation, verified from the retained log: `Unity.exe -batchmode -runTests -projectPath . -testPlatform EditMode -testFilter IronRing -testResults C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/editmode-all.xml -logFile C:/Users/JUNGLE/Documents/GitHub/POC_06/production/qa/evidence/editmode-all.log`.

The isolated demo build completed with exit 0 (`build.log`) and created `Assets/Scenes/Demo/Story005/Story005Combat.unity` through editor APIs plus `Builds/Demo/Story005/Game.exe`. The coordinator ran captures and the worker opened all three retained PNGs:

- `01-combat-in-progress.png`: round 1 TRAIN WINS, train HELD AT WAYPOINT, strength 10 versus 8, stocks 5 and prosperity 50.
- `02-combat-result.png`: three won rounds, VICTORY, scrap 8, train RELEASED - ROUTE CONTINUES, prosperity 50.
- `03-combat-defeat.png`: three lost rounds, DEFEAT, scrap 3, prosperity 40, train released.

All images are legible at 1280x720, with no observed clipping. These prove rendered demo state; they do not prove physical train traversal, button input, timing feel or concrete cross-story composition.

Early test launches refused access while another worker's batch build held the project; no Unity process was killed. Since the coordinator centralized Unity runs, the worker has not launched any editor or player.

`stamp.txt` uses PowerShell SHA-256 because the workflow's normal review-receipts helper invokes git, which the user prohibits.

## Scope and remaining integration

- `docs/README.md` was absent. Implementation follows the supplied story, `/dev-story`, game brief and coordinator rules.
- The explicit dispatch requests EditMode tests, overriding the earlier general no-tests rule and minimal-tier waiver.
- Other stories are being developed concurrently; their dependency status is not yet complete. The coordinator owns the final concrete composition root and final cross-story integration proof.
- No git commands, shared build-setting changes, or hand-edited scene YAML were used.

## Handoff status

Seven of eight story acceptance criteria are verified. The final concrete Story001/004 connection criterion remains open because this worker is explicitly restricted to interfaces and own-folder fakes, while the coordinator owns `Assets/Scripts/Bootstrap/`. The story remains **Ready**, not Done, until that integration proof is retained. Scoped implementation, 13 passing tests, successful player build and all three screenshots are ready for that integration pass. Final source SHA-256 verification found zero mismatches against `stamp.txt`.
