# Interceptor regression diagnosis

Baseline: `origin/dev` **87d580376aacb0d6dd58f1f293b8fe1c35d8e40c**, Unity **6000.6.4f1**. All production AI, tests, scene geometry and saved navigation data were unchanged. Runs used an independent disposable project. These are automated tests, not human playtesting.

## Actual results

| Run | Pass | Fail | Skip |
| --- | ---: | ---: | ---: |
| IdenticalMotorRouteAttempts alone | 0 | 1 | 0 |
| GenuineVisibleMotion alone | 1 | 0 | 0 |
| Both scenarios | 1 | 1 | 0 |
| Both after 48 related navigation/sensor scenarios | 48 | 2 | 0 |
| Complete Play Mode assembly (272 cases) | 269 | 2 | 1 |
| Read-only observed pair | 1 | 1 | 0 |
| GenuineVisibleMotion repeated in one process | 0 | 1 | 0 |

The last row is NUnit's aggregate result for one repeated test case: iteration 1 passed and iteration 2 failed; repeat stopped on that failure. Although requested with `-repeat 10`, ten iterations did **not** execute. The graphical Game-view resolution capture is skipped by the batch-mode suite.

## IdenticalMotorRouteAttempts

The failure is the second attempt's null route assertion, around line 288. The initial predictive destination is valid, but the fixture first runs the baseline motor attempt for 12 seconds. It disables other brains while leaving their DroneMotor components active. Flanker subsequently occupies the staged start/corridor. When the predictive attempt begins, the logical waypoint route exists and the native NavMesh returns `PathComplete`, while full raised-body collision validation correctly rejects the occupied route.

Observed start: `(7.5, 0.01333, -10)`; predictive Storage node: `(10, 0, -10)`. Blockers include Flanker's chassis and animated rotor mesh colliders. The baseline's chosen Control node `(10, 0, 10)` also lacks an initially valid full-body route in this arrangement. These are not identical, isolated motor trials.

This failure occurs on the unchanged remote baseline and was previously reproduced with the original weapon presentation. It is pre-existing; evidence does not establish FPS arms causality.

## GenuineVisibleMotion

Reproduced intermittently without changing any AI or test source, including fresh scenes in the same process. A lightweight Editor observer captured final state synchronously before fixture scene unload. Expensive explanatory collision queries run after the assertion.

Both final states have confidence `1`, eight genuine observations spaced approximately 0.300–0.302 seconds apart, estimated player velocity about `(0,0,1.193)`, and the same drone/player endpoints. The successful run selects InterceptAhead with a feasible Storage route. The failed run correctly falls back to PursueLastKnown, reason **No feasible interception candidate**.

In the failing run the target's ground path and the player's predicted route are complete. The drone's strict route is null because its **start envelope** overlaps Flanker's Rotor1 and FlankerChassis. Flanker is near `(7.716, 0.01333, -7.900)` and its motor remains enabled. In the passing run Flanker is near `(7.537, 0.01333, -7.941)` and the same strict route is clear. Timing and animated collider occupancy matter at this boundary.

Observation timestamps, confidence and motion timing are valid. This reproduction does not require an earlier unrelated test, although suite timing can influence it. No stale static policy state or missing event unsubscription has been demonstrated. Policy history and graph state are per instance; route caches clear per decision; hearing subscriptions and session singleton cleanup have matching lifecycle cleanup.

This is reproducible on the unchanged remote baseline. Its historical introduction is not established; no evidence attributes it to arms changes.

## Additional pre-existing-baseline observation

`InterceptorSensorIntegrationPlayModeTests.MainLab_SoundResponse_BeforeAfterEvidence` fails after related navigation tests and in this complete run: return-home distance approximately **8.32 m**, expected below 1.2 m. The earlier fresh baseline run had 270 pass / 1 fail / 1 skip and did not show this extra failure. This is an intermittent finding on unchanged source, not a newly introduced production change; its independent cause remains unverified.

## Smallest safe correction plan (not implemented)

1. Make the prediction/comparison fixture deterministic: stop and reset non-target motors and native agent paths, retaining physical colliders, or stage actors at verified separated locations.
2. Reset target motor/path/velocity between comparison arms and verify both initial full-body routes immediately before execution.
3. Keep a separate dynamic-obstruction scenario that expects safe pursuit fallback when the route is occupied.
4. Investigate the intermittent sound-return scenario independently before changing its timeout or assertions.

Expected future edit: `Assets/Tests/PlayMode/InterceptorMainLabPlayModeTests.cs`, optionally a shared fixture arrangement helper. No production policy fix, forced InterceptAhead, unrestricted hidden-player lookup, scene regeneration or NavMesh rebake is justified by these two failures. No tests were disabled or weakened in this diagnosis.

## Reproduction and evidence

Exact commands, timestamps and result counts: [matrix.json](evidence/interceptor-diagnosis/matrix.json). Runnable command matrix: [run-matrix.py](evidence/interceptor-diagnosis/run-matrix.py), with local project/output paths at its top. Related test order is preserved alongside it. XML contains actual assertion messages and stacks.

Typical invocation (run from the disposable project root; omit `-quit` for Test Runner):

```sh
"/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$QA_PROJECT" -runTests -testPlatform PlayMode \
  -testFilter 'ShiftFour.Tests.InterceptorMainLabPlayModeTests.MainLab_BaselineComparison_IdenticalMotorRouteAttempts;ShiftFour.Tests.InterceptorMainLabPlayModeTests.MainLab_GenuineVisibleMotion_DrivesActualBrainDecisions' \
  -testResults "$EVIDENCE/results.xml" -logFile "$EVIDENCE/Editor.log"
```

For a complete run replace `-testFilter ...` with `-assemblyNames ShiftFour.Movement.PlayModeTests`. For same-process reproduction use the full GenuineVisibleMotion name and `-repeat 10`.

Observer is evidence-only, outside Assets in this branch. The repeat trace normalizes only a JSON serialization issue: Unity's SceneHandle formatted as `19516:256` must be quoted as a string. Original raw traces and full logs remain in `/Users/dev/Developer/ShiftFour-interceptor-diagnosis/20261021-87d5803`.
