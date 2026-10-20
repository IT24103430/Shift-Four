# Enhanced Scout — Task 8 review evidence

The proposal is **pending Pamudi's policy-owner review**. Only documents, patch files and harness source were written to the active project by this task. The candidate was applied only to `/tmp/shiftfour-task8.J205sR`, a disposable copy of the active Task 1–7 source. The active Scout policy and every original file under Assets, Packages and ProjectSettings remain byte-identical to the captured starting source.

Unity: **6000.6.4f1**. Starting active HEAD: `a4ff4fdebcd3a7c461d416dfcc2c565926b37b05`. The shared Task 6–7 working changes were already present and included in the copy. Workspace HEAD/index advanced concurrently during verification; this task issued no commit, staging, branch-switch, reset or push commands and did not revert that concurrent work. `integrity.json` records both revisions and the actual source comparison separately from Git state.

## Results

- Baseline focused Edit Mode: **115/115 passed** before candidate application.
- Candidate focused Edit Mode: **115/115 passed**, zero failures/skips.
- Candidate focused Play Mode: **99/99 passed**, zero failures/skips; includes **8/8 new Scout cases** and **91 existing regression cases**.
- MainLab demonstration passed and wrote [MainLab-demo.csv](MainLab-demo.csv).
- `git apply --check EnhancedScout.task8.patch` passed in the active project. The patch was not applied there.

The focused scope covers graph A*, hearing attenuation/category/expiry/gates, awareness evidence/decay, difficulty and health binding, actual movement and speed caps, accepted-motion animation feedback, visual/combat occlusion, bounded local Scout search and return, terminal motor stop and real destruction/restart. This is not a claim that every project test or a human gameplay/balance review was run.

| Play Mode fixture | Passed |
| --- | ---: |
| EnhancedScoutPlayModeTests | 8 |
| DroneMotorPlayModeTests | 26 |
| DroneHearingPlayModeTests | 14 |
| DroneMovementSpeedsPlayModeTests | 8 |
| DroneAnimationPlayModeTests | 8 |
| DroneDestructionPlayModeTests | 7 |
| CombatAuditPlayModeTests | 7 |
| DroneDifficultyPlayModeTests | 7 |
| DroneAwarenessPlayModeTests | 6 |
| SoundEventsPlayModeTests | 8 |

## Scout scenarios

1. Real MainLab gunshot → bounded stationary turn → travel toward uncertain heard point → bounded search → suspicion decay → return → patrol. No visual memory or player damage from sound. Actual NavMesh/A*/motor movement is used.
2. Repeated nearby walking noise builds evidence and passes through suspicious orientation before investigation; one weak step stays below immediate pursuit threshold.
3. Typed sprint noise is heard and investigated. Production sprint controls are absent and remain absent.
4. Fresh actual vision outranks sound and can produce damage through the unchanged real combat checks.
5. A runtime solid cover collider blocks actual visibility and firing; moving the hidden player leaves last-seen memory and Scout's destination unchanged, then memory expires.
6. Local search has no more than three sample goals and does not renew evidence timestamps; expiration selects return.
7. The real closed Checkpoint gate rejects a gunshot from the adjacent Server room. An off-mesh goal is also rejected.
8. Runtime Scout health and patrol target are correct, and actual death disables awareness/hearing, prevents orientation and leaves the motor terminally stopped.

Existing speed regressions additionally check all profile targets, acceleration ramps, braking animation, 30/60/120 Hz simulation cadence, gate recovery and collision holds. The cadence cases are simulation checks, not achieved graphics-frame-rate benchmarks. The baseline and candidate Edit Mode filters cover `WaypointGraphTests`, `DroneAwarenessEditModeTests`, `DroneMovementSpeedsEditModeTests`, `DroneDifficultyEditModeTests` and `HearingModelEditModeTests`.

## Iterations retained

- `ScoutPlayMode.initial.xml/.log`: 0/7 passed; a proposed `NavMeshPath` field initializer invoked a Unity API during MonoBehaviour construction. It was moved to `Awake`; no shared code was changed.
- `ScoutPlayMode.second.xml/.log`: 7/8 passed. The closed-gate fixture used `(-10,0,-3)`, still inside Checkpoint. Corrected to `(-10,0,3)` in Server, across the existing gate at Z=0. The hearing/gate production behavior was correct and was not changed to fit the test.
- `FocusedPlayMode.xml/.log`: final 99/99 passing candidate run, including the corrected fixture and updated hearing/awareness expectations.

The two existing test updates are part of the proposed patch: Scout intentionally transitions through Suspicious before Investigate. Comparisons asserting the other three policies do not change remain in place; hidden-target/no-fire assertions remain intact. They are not applied to the active tests.

## Reproduce

From the repository root, create a disposable review project and run the complete eight-case Scout suite:

```sh
python3 docs/evidence/enhanced-scout/rehearse.py
```

For only the reproducible MainLab demonstration:

```sh
python3 docs/evidence/enhanced-scout/rehearse.py --demo-only
```

The script validates the captured Scout preimage, copies Assets/Packages/ProjectSettings, applies candidate sources only in the new temporary project, and prints the project/results paths. It never runs `git apply` on the active checkout. Use `--unity /absolute/path/to/Unity` on a different machine or `--prepare-only` to inspect the project in the Unity Test Runner. A fresh copy imports assets on first launch. Scene generation and baking are not invoked. The script's preparation path was also executed successfully for this handoff.

Exact executed focused commands (after copying the proposal into the disposable project):

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
REVIEW_PROJECT=/tmp/shiftfour-task8.J205sR
SCOUT_RESULTS=/Users/dev/Desktop/game/Shift-Four/docs/evidence/enhanced-scout

"$UNITY" -batchmode -nographics -projectPath "$REVIEW_PROJECT" -runTests -testPlatform EditMode -testFilter 'ShiftFour.Tests.WaypointGraphTests;ShiftFour.Tests.DroneAwarenessEditModeTests;ShiftFour.Tests.DroneMovementSpeedsEditModeTests;ShiftFour.Tests.DroneDifficultyEditModeTests;ShiftFour.Tests.HearingModelEditModeTests' -testResults "$SCOUT_RESULTS/FocusedEditMode.xml" -logFile "$SCOUT_RESULTS/FocusedEditMode.log"

SHIFTFOUR_SCOUT_EVIDENCE="$SCOUT_RESULTS" SHIFTFOUR_EVIDENCE="$REVIEW_PROJECT/RegressionEvidence" "$UNITY" -batchmode -projectPath "$REVIEW_PROJECT" -runTests -testPlatform PlayMode -testFilter 'ShiftFour.Tests.EnhancedScoutPlayModeTests;ShiftFour.Tests.DroneMotorPlayModeTests;ShiftFour.Tests.DroneDestructionPlayModeTests;ShiftFour.Tests.CombatAuditPlayModeTests;ShiftFour.Tests.DroneMovementSpeedsPlayModeTests;ShiftFour.Tests.DroneDifficultyPlayModeTests;ShiftFour.Tests.DroneHearingPlayModeTests;ShiftFour.Tests.DroneAwarenessPlayModeTests;ShiftFour.Tests.DroneAnimationPlayModeTests;ShiftFour.Tests.SoundEventsPlayModeTests' -testResults "$SCOUT_RESULTS/FocusedPlayMode.xml" -logFile "$SCOUT_RESULTS/FocusedPlayMode.log"
```

The isolated Unity editor also filled default platform fields in its copy of `ProjectSettings.asset` (pixel density, build numbers and OS version defaults). That incidental serialization is recorded in `verification-settings-generated.patch`; it is excluded from the proposal and the active settings remain unchanged.

## Files

- `EnhancedScout.task8.patch`: reviewable production policy plus tests/meta; no prefab or scene delta.
- `ScoutBrain.cs.txt`, `harness/`: exact proposed policy/test sources.
- `rehearse.py`: safe disposable-project preparation and test entrypoint.
- `before.json`: original active file hashes and Git state.
- `integrity.json`: final original-asset comparison and concurrent Git-state explanation.
- `validated-source-sha256.json`: exact C# source used for final candidate verification.
- `test-summary.json`, `TEST-MATRIX.csv`: actual XML-derived results.
- `MainLab-demo.csv`: actual MainLab phase/movement trace.
- XML/log files: raw Unity results and iteration history.

See [the handoff](../../ENHANCED_SCOUT_HANDOFF.md) for policy priorities, runtime configuration, owner approval boundary and review instructions.
