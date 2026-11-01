# Story 003 evidence

Implementation: pure C# `NpcRecruiter : INpcRecruiter`, immutable NPC definitions, JSON adapter, isolated MonoBehaviour demo.

Defaults: joinChance 0.5; eight NPCs; two allowed per hub-1 through hub-4; completed habitat required; one quote each. The demo injects a 0.25 probability draw and first candidate selection from a separate JSON asset, so its screenshot is repeatable without changing production probability.

Visit semantics: the contract explicitly calls `OnHubVisited` once per arrival. Each call is one visit; it draws at most one chance value and joins at most one NPC. Reentrant callbacks are ignored. Sequential calls represent subsequent visits because the shared contract exposes no visit id. No subscription to habitat flag events triggers recruitment early.

Scope: Story 002 is consumed only through IHabitatSystem. Prosperity is left to Story 004. No contracts, other story sources, shared build settings or git state changed. The builder is under the owned `Assets/Scripts/Npc/Editor` folder, and creates its scene through EditorSceneManager only when absent.

Verification: coordinator combined EditMode run on 2026-10-31 21:16:50 UTC passed 67/67, including all 11 NPC cases. Independently parsed the NUnit XML and confirmed each NPC result is Passed. Retained copies: editmode-all.xml and editmode-all.log. Final build-2.log records build success and editor exit 0. Coordinator reports player capture exit 0; player-2.log is retained. The final 1280x720 PNG was independently opened and inspected: Ada Brass, the complete arrival quote with curly quotation marks, and JOINED HUB-1 are visible without clipping.

Historical test.log records the earlier blocked run (Track namespace collision); it is superseded by the passing editmode-all.xml/.log pair.


## Acceptance mapping

| Acceptance | Evidence |
| --- | --- |
| External identity, name, quote, habitat data | JsonDataDefinesPoolAndChance; NpcPool.json inspected: 8 unique ids, 2 per hub |
| Flagged visit and configured probability | IneligibleVisitDoesNotRoll; ChanceUsesStrictBoundary |
| Injected deterministic RNG | RepeatedInputsSelectSameNpcAndLaterVisitCanJoinAgain |
| Global uniqueness | JoinedNpcNeverAppearsAtAnotherHubAndExhaustionDoesNotRoll |
| Empty or ineligible pool no-op | EmptyOrUnmatchedPoolIsNoOp; exhausted-pool test |
| Recorded membership and event | VisitJoinsAtMostOneAndPublishesCommittedMembership |
| One decision/join per visit | VisitJoinsAtMostOneAndPublishesCommittedMembership; one float draw asserted |
| Name and one-line quote visible | 01-npc-joined.png, directly inspected at 1280x720 after rebuild |

The only change after the passing logic tests was an approved cosmetic fix replacing question-mark quote delimiters with C# Unicode escapes; the final player rebuild compiled that fix successfully. Core logic, data and tests are unchanged from the passing run. No Unity process was launched by this worker after coordinator notice 5.

Run result: OBSERVED - final standalone-player capture shows Ada Brass and the full arrival quote with correct quotation marks, plus JOINED HUB-1, without clipped text.

All story acceptance criteria verified; no remaining story-scoped work. Integration composition and real habitat/track wiring are intentionally owned by the later coordinator integration pass.
