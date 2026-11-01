# Story 006 verification

Date: 2026-11-01
Status: Verified; story Done.

Implemented the interface-only pure C# facility builder, JSON catalog, isolated demo fakes, MonoBehaviour facility display and Editor scene/player builder. Core construction checks docking, known ids, ruin policy, per-hub capacity and every resource before spending, prevents reentrant construction during spending, applies configured prosperity once, and exposes summed combat bonuses plus committed build events.

Defaults: wall 3 scrap / +3 combat; turret 4 scrap + 1 coal / +5 combat; workshop 3 scrap + 2 grain / +0 combat / prosperity facility / provisional +2 prosperity on construction. Capacity 3 per hub; allowBuildInRuins false. Values are in Assets/Scripts/Defense/Data/Facilities.json.

## Executed evidence

- Coordinator ran the project.yaml EditMode command with -testFilter IronRing.Defense.Tests and story-specific results/log paths under the shared Editor mutex. editmode.xml: 9 total, 9 passed, 0 failed, 0 skipped; run ended 2026-10-31 21:31:02 UTC. tests.log records the command line and Unity 6000.3.27f1.
- coordinator-validation.txt records mutex acquisition/release and test exit 0, demo build exit 0, player exit 0. The corrected mutex is C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock and remains held until the Editor exits. This supersedes the initial policy-blocked attempt; the worker did not bypass that restriction.
- build.log records Build Finished, Result: Success. Assets/Scenes/Demo/Story006/FacilityDemo.unity was generated using Unity Editor APIs; the dedicated player is Builds/Demo/Story006/Game.exe.
- editmode-all.xml additionally records 67/67 passing tests, including Story 005 FacilityBonus_UsesNextHubAndChangesRound. Read-only source review confirms WaypointResolver calls IFacilityBuilder.CombatBonus for the next hub and CombatEncounter adds it to train strength.
- Current Defense implementation, data and test SHA256 values match stamp.txt. This verification pass made only documentation changes.

## Acceptance mapping

| Criterion | Evidence |
|---|---|
| Three external-data facility types, costs and effects | CatalogContainsProvisionalData; CustomDefinitionsControlEffectsAndLimit |
| Docked construction and shortage refusal without state changes | DockingAndKnownIdsAreRequired; InsufficientSecondaryResourceChangesNothing; BuildsSpendApplyEffectsAndPublishCommittedState |
| Independent per-hub capacity | LimitIsPerHubAndRefusalDoesNotSpend |
| Prosperity and combat effects | BuildsSpendApplyEffectsAndPublishCommittedState; CustomDefinitionsControlEffectsAndLimit; Story 005 FacilityBonus_UsesNextHubAndChangesRound and reviewed interface consumption |
| Configurable ruin policy | RuinPolicyIsDataDriven(False), RuinPolicyIsDataDriven(True) |
| Built facilities visible | 01-built-facilities.png, inspected in this verification pass |
| Successful construction emits committed events | BuildsSpendApplyEffectsAndPublishCommittedState; QueriesDoNotMutateAndCallbacksCannotReenterBuild |

Run result: OBSERVED - 01-built-facilities.png shows wall, turret and workshop silhouettes and labels at Foundry Hub, combat +8, prosperity +2, capacity 3/3, three build events and disabled ruin construction, legibly inside a 1280x720 frame.

## Evidence boundary

The screenshot is from the dedicated running player and proves the isolated facility view. It does not prove manual build-menu input or full-game traversal. Facility effects are tested across contract boundaries using local fakes; the shared real-system composition root remains the coordinator's separate integration pass, in accordance with dispatch ownership. Player logs contain no managed exception; they contain graphics shutdown diagnostics including a ComputeBuffer disposal warning.
