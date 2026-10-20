> **Latest assessment:** the user subsequently selected prediction integration into the Control tactical branch. That work is now implemented and tested separately. See [final assessment](../student4-final-assessment/README.md) for fresh rendered 110/60 results on this animation checkout and the combined Control 111/33 results. Earlier measurements below are retained with their original scope.

# Interceptor prediction evidence — 2026-10-19

Student 4 / Intelligent Systems. Baseline: `03e0861035bb328d8766b5838bcb564c8f957c3e`, on `feature/gv-asmadala-drone-animation`. No commit, push, merge, graph edit, scene regeneration or art edit was performed.

## Repeat-request revalidation

The attachment supplied again was byte-for-byte identical to the original request. The existing predictive implementation was audited and rerun on the current `feature/gv-asmadala-drone-animation` checkout; no new runtime or test source changes were necessary. The separately implemented `feature/is-asmadala-control-tactical-navigation` worktree was preserved without code transfer or Git merge. These remain distinct implementations with different reviewed dependencies.

Revalidated at 2026-10-19 11:47:22 Asia/Colombo: **110/110 Edit Mode tests passed; 7/7 focused MainLab Play Mode tests passed.** This includes all 35 previously added predictive tests. The full Play Mode suite was not rerun for this unchanged source; its earlier 57 passed / 2 baseline-reproduced failures / 1 rendered-only skip remains recorded below.

Fresh evidence: [Edit Mode](revalidated-edit.xml), [MainLab Play Mode](revalidated-mainlab.xml), [measured comparison](revalidated-mainlab-comparison.csv), [SHA-256 of tested source files](revalidated-source-sha256.txt). The hashes were checked against both the workspace and isolated test copy.

Both policies again completed the same 2.500 m route with zero measured navigation errors. Single-run times were 0.703 s (baseline) and 1.212 s (predictive); no speedup or capture-rate claim is made.

## Executed results

| Run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Full Edit Mode | 110 | 0 | 0 |
| Full headless Play Mode | 57 | 2 | 1 |
| New Interceptor tests (included above) | 35 | 0 | 0 |
| Original Interceptor, reproducing the two existing failures | 0 | 2 | 0 |

Raw evidence: [Edit Mode XML](final-edit.xml), [Play Mode XML](final-play.xml), [original-baseline failures](baseline-regressions.xml), [controlled comparison](controlled-comparison.csv), [MainLab comparison](mainlab-comparison.csv). Both existing failures and the rendered-only skip are explained below. The full Play Mode suite is **not green**. No C# compilation errors occurred in either final run; existing obsolete-API and Editor/native warnings remain.

## Viva explanation

The interceptor maintains a bounded memory of **visible observations**. It estimates planar motion using exponential smoothing, resets momentum on a reversal/stop, and reduces confidence after implausible speed, acceleration or sharp turns. Invisible decision arguments are ignored. Observation age decreases confidence; old memory expires.

It generates route hypotheses to existing graph junctions. Both the remembered player's route and the drone's route must pass gate-aware A* and complete NavMesh validation for every leg. The drone path additionally samples its actual collision envelope every 0.25 m; this conservatively rejects ground paths blocked at flying height. The score uses actual NavMesh corner lengths rather than Euclidean distance. A route can bend around cover; its initial direction must agree with the last observed heading. Routes requiring excessive detours lose confidence. These are heuristic hypotheses, not knowledge of the player's destination.

For a candidate:

- Player ETA = player path length / estimated speed − observation age.
- Drone ETA = drone path length / configured NavMeshAgent speed.
- Reject if behind the observed motion, outside the four-second horizon, below confidence threshold, or too late to intercept.
- Score = 30 × timing + 20 × forward advantage + 20 × positioning + 25 × confidence + 8 × incumbent stability. Terms are normalized to [0, 1].

The default minimum commitment is 1.2 seconds, with an additional 8-point switching threshold. Reversals, stops, invalid routes, gate changes and immediate visible threats bypass commitment. Only fresh observations can revise the arrival deadline. Failed arrival attempts block the junction for at least three seconds and until a new observation exists.

Fallback order is: justified committed interception; re-evaluate fresh evidence; pursue last genuine sighting; bounded local search; return to Control home. Search uses reachable nearby junctions and four small local probe positions, without altering the graph. Search targets remain committed until reached or invalidated and are not repeatedly revisited. If no fallback route is valid the destination is the drone's own position, allowing safe recovery on a later tick.

`PredictMovement` and `Replan` are transient decision phases; the returned action is `ObserveTarget`, `InterceptAhead`, `PursueLastKnown`, `SearchLastSeenArea`, `ReturnHome` or `WatchHome`. State, reason, memory, confidence, scores, route, selected node, ETA, replan count and junction-switch count are inspectable through `InterceptorBrain.Policy`.

## Integration and diagnostics

All production changes are in `Assets/Scripts/AI/Agents/InterceptorBrain.cs`. `DroneBrain`, `DroneMotor`, `DroneBody`, `WaypointGraph`, other agent policies, Health, gates, scene assets and animations are unchanged. The existing combat destination interface still applies collision-safe standoff to a visible direct engagement. The agent's speed, area mask, flying height and motor behavior remain authoritative.

The existing shared `DroneBrain`/`DroneMotor` plumbing still reads/passes a live target transform. The interceptor discards invisible position/distance arguments, and its deterministic core never receives a player component, velocity, input or destination. The shared motor's invisible branch uses only the supplied policy destination. Physics/navigation occupancy can reject a location containing a real collider, as part of normal collision protection.

Select the Interceptor in Play Mode and enable **Developer Diagnostics**:

- yellow sphere: last genuine observation;
- cyan line/sphere: selected route hypothesis and predicted position;
- grey/green spheres: feasible scored junctions / selected junction;
- green line: validated planned route;
- labels: score, drone/player ETA, confidence, state, observation age and reason.

Gizmos/labels compile only for Editor/development builds. No per-tick Console logging is added. Rejected candidates are excluded, so their scores are not drawn. Prediction drawings clear when the decision falls back.

## Tests and reproduction

Production scripts remain in `Assembly-CSharp`; tests use reflection to preserve the existing assembly layout. The new files are:

- `Assets/Tests/EditMode/Movement/InterceptorPolicyEditModeTests.cs` (and `.meta`): deterministic motion, memory, uncertainty, reversal, stop, acceleration, cover loss, expiry, gate changes, reachability, curved paths, failed arrival, repeat updates, switching threshold, minimum commitment, immediate threat, home and hidden-input tests.
- `Assets/Tests/EditMode/Movement/InterceptorComparisonEditModeTests.cs` (and `.meta`): frozen original decision formula against the predictive policy over eight controlled scenarios.
- `Assets/Tests/PlayMode/InterceptorMainLabPlayModeTests.cs` (and `.meta`): real MainLab A*, NavMesh, collision envelope (including a raised obstacle above the ground path), motor, raycast visibility, wall occlusion and four-gate progression.

MainLab includes only five tactical nodes. Tests find a suitable existing route and staging point without changing assets. One test moves the real player through eight observations, verifies visibility using the unchanged perception raycasts and calls the actual interceptor decision method. Other tests control observation sequences independently while validating routes against the real scene. A separate test executes the expired-observation home fallback through the real motor and checks the transition to WatchHome; this is not a timed full-mission player-capture benchmark.

Unity 6000.6.4f1 is installed locally. The user's project was already open, so tests ran in `/tmp/shift-four-interceptor-validation`, copied with Assets, Packages and ProjectSettings, without Library/Temp or scene editing. Unity resolves bundled URP 17.6.0 from the existing 17.5.0 request; the same resolution appears in the supplied package lock. No dependency file was changed.

Example command, with the same options for `PlayMode`:

```sh
/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -job-worker-count 2 \
  -projectPath /tmp/shift-four-interceptor-validation \
  -runTests -testPlatform EditMode \
  -testResults /tmp/shift-four-interceptor-validation/final-edit.xml \
  -logFile /tmp/shift-four-interceptor-validation/final-edit.log
```

Raw final test results and measured CSVs are stored beside this document. Test-run timestamps in XML are UTC; this report's date uses Asia/Colombo.

## Measurement interpretation

`controlled-comparison.csv` uses exactly the same 37 observations at 0.3-second intervals, fixed drone origin, candidate points and route fixtures for each policy. It measures interception **decision ticks**, feasibility against arrival times, mean proposed route distance, goal changes, invalid route proposals, fallback response, and the new policy's invalidation/junction-switch counters. It does not simulate capture or physical movement. The baseline's unavailable internal counters use −1; a fallback response of −1 means not applicable or not reached. Goal changes include justified pursuit updates and must not be described as unnecessary switches.

`mainlab-comparison.csv` uses the real baked MainLab navigation and unchanged motor/body. Both policies receive identical straight-motion observations and start from the same warped staging position. Routes then execute normally. Completion means coming within 1.1 m of the goal; the time is measured to that threshold, not to full zero-speed settling. Navigation errors count invalid validated goals, departure from NavMesh, incomplete active paths and collision-envelope overlaps. A missing route length is recorded as NaN. Replans/switches are policy counters during the controlled decision phase, not motor-internal retry counts.

The controlled scenarios expose cases where the baseline scores a junction despite an implausible arrival time, a blocked player route or a very long drone detour. The predictive policy is deliberately more conservative. In particular, it may reject a long detour and keep updating pursuit goals rather than intercepting. More goal changes in some scenarios are recorded honestly.

These confidence scores are explanatory heuristics, not calibrated probabilities. Travel times assume cruising speed and use a configurable 0.6-second allowance; acceleration, body avoidance and dynamic obstacles can delay arrival. No general speed, capture-rate or full-mission performance improvement is claimed. A larger capture benchmark would require multiple player behaviors and repeated timed trials.

## Requested scenario coverage

| Requirement | Test evidence |
| --- | --- |
| Straight movement | `StraightMotion_SelectsAheadWithFeasibleRouteTiming`; `MainLab_GenuineVisibleMotion_DrivesActualBrainDecisions` |
| Reversal | `Reversal_InvalidatesForwardCommitmentImmediately`; MainLab state trace |
| Stationary target / stop | `StationarySightings_NeverIntercept`; `Stop_DiscardsSmoothedMomentum` |
| Acceleration / sharp turn | `SuddenAcceleration_IsNotTreatedAsStableEvidence`; `SharpTurn_LowersConfidenceAndUsesActualObservation` |
| Disappears behind wall | `MainLab_WallOcclusionAndMovingHiddenPlayer_DoNotUpdateMemory` |
| Old observations | `ExpiredHistory_IsEmptyAndReturnsHome`; `ObservationGap_DoesNotInferVelocityAcrossUnseenMovement` |
| Locked gate | `LockedGate_RejectsCandidatesAndOpeningTriggersReplan`; MainLab locked gate test |
| Opening / replan | Same tests, using actual four-gate opening animation and version updates in MainLab |
| No candidate | `NoReachableCandidate_PursuesLastGenuineSighting`; `CompleteNavigationFailure_HoldsOwnPositionSafely` |
| Failed interception | `FailedArrival_IsNotRepeatedUsingSameOldEvidence` |
| Repeated updates | `RepeatedUpdates_KeepCommitmentWithoutExtendingArrivalWindow`; `ArrivalWindow_OnlyFreshObservationsMayReviseIt` |
| Hysteresis | `SwitchingThreshold_BlocksMeasuredBetterAlternativeThenAllowsItWhenRemoved`; `MinimumCommitment_RetainsRouteEvenWithZeroSwitchingThreshold`; invalid route and threat override tests |
| Uncertainty | `Uncertainty_DecaysWithAgeAndCannotCreateFurtherPredictions`; curved path / long detour tests |
| Return home | `MainLab_ExpiredObservations_ReturnPhysicallyToControlHome`; deterministic home tests |
| Hidden information | `HiddenPosition_NeverChangesMemoryOrDestination`; actual wall/hidden player test |



### Actual comparison values

The controlled straight case produced 12 timing-feasible interception ticks out of 15 baseline interception ticks, and 10 out of 10 predictive ticks. Mean proposed interception path distances were 7.280 m and 3.245 m respectively. Total goal changes were 24 and 29; this does **not** demonstrate fewer overall goal changes. Cover-loss fallback occurred after 3.900 s for the baseline and 1.200 s for the predictive policy. The locked-gate fixture recorded 27 invalid baseline goal proposals and zero predictive proposals; its player stayed on the locked side. The long-detour fixture yielded 15 baseline interception ticks, none timing-feasible, and zero predictive interception ticks. These are fixture measurements, not capture outcomes.

Measured MainLab motor execution:

| Policy | Route length (m) | Completed | Seconds to 1.1 m | Navigation errors |
| --- | ---: | --- | ---: | ---: |
| baseline | 2.500 | True | 0.707 | 0 |
| predictive | 2.500 | True | 1.219 | 0 |

These timings come from one execution per policy and do not establish a speed improvement. Successful player captures were not measured.

## Existing full-suite failures

Two existing Play Mode checks fail in this local headless environment:

- `DroneMotorPlayModeTests.MovingAgent_RespectsTurnLimitAndHoldsHeadingAfterArrival` fails its frame-by-frame turn bound.
- `MainLabProximityPlayModeTests.MainLab_AllFourSpawnEnvelopesAndPolicyDestinationsRemainUsable` fails a frame-by-frame speed bound on another agent (Guard or Flanker in recorded runs).

Both failures were reproduced after replacing **only the isolated copy's** Interceptor file with the exact original from the baseline commit. See [baseline-regressions.xml](baseline-regressions.xml): 2 tests run, 2 failed. The actual working file was never reverted. This establishes that those failures also exist without this policy; their root cause is not diagnosed or repaired in this task. Shared movement and other agent policies remain unchanged.

The final headless run also skips `RenderedMainLab_AllFourAcceptedCombatAndDestructionSequences`, which explicitly requires a graphics device. The other combat, animation, health and destruction checks still execute normally.
