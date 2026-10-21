# Task 9 — Advanced Guard verification

**Pending owner approval:** this is a review patch rehearsed in a disposable Unity project. Original active project assets remain unchanged. See [the handoff](../../ADVANCED_GUARD_HANDOFF.md) for Nimthara's ownership boundary, the shared movement-hook review and the confirmed post/console defects.

Editor: **Unity 6000.6.4f1**. Verification project: `/tmp/shiftfour-task9-kkc6fc_1`, copied from active Task 1–7 source. The separate Task 8 Scout proposal was not applied. Starting HEAD and file hashes are in `before.json`. Workspace HEAD/index can advance concurrently; this task issues no Git write commands and checks original source bytes independently.

## Final results

**269/269 Edit Mode and 108/108 focused Play Mode tests passed**, including all **10 new Guard cases**, with zero failures or skips. These final runs cover 377 passing cases. XML-derived totals are in `test-summary.json` and `TEST-MATRIX.csv`; diagnostic iterations are retained separately.

The final Play Mode scope contains ten new Guard cases and the existing combat, motor, destruction, animation, awareness, difficulty, hearing, sound-event and player-noise regression fixtures. The new cover test uses the real motor to arrive at selected cover, checks its NavMesh attachment and defensive bounds during travel, and verifies unchanged scoring weights. A successful **actual Server console activation** exercises the real producer → bus → hearing → awareness → Guard decision path, including duplicate activation. Other cases test gunshots, evidence expiry, cover commitment/invalidity, observed player relocation, cover-blocked firing, hidden-position memory, open/closed gates, the movement hook, health/speeds and terminal death.

The final Edit Mode suite also covers pure A*, sound, hearing, movement, awareness and difficulty contracts. The simulation-cadence checks run at controlled 30/60/120 Hz; they are not measured rendering-throughput benchmarks. Passing automated tests do not establish subjective Medium difficulty or replace owner gameplay review.

## Reproduce

```sh
python3 docs/evidence/advanced-guard/rehearse.py --demo-only
```

Omit `--demo-only` to run all ten new Guard cases. Use `--prepare-only` to inspect the printed disposable project in the Unity Test Runner. The preparation path was executed and its proposed source compared with the verified project. The script checks preimages before copying, changes only its new temporary project, and prints results/frames paths. It never applies a patch to the active checkout. A fresh copy imports assets on first launch.

Final regression command, after applying the proposal only in the disposable project:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
REVIEW_PROJECT=/tmp/shiftfour-task9-kkc6fc_1
GUARD_RESULTS=/Users/dev/Desktop/game/Shift-Four/docs/evidence/advanced-guard

SHIFTFOUR_GUARD_EVIDENCE="$GUARD_RESULTS" SHIFTFOUR_EVIDENCE="$REVIEW_PROJECT/RegressionEvidence" SHIFTFOUR_DEMO_OUTPUT="$REVIEW_PROJECT/RegressionEvidence/frames" "$UNITY" -batchmode -projectPath "$REVIEW_PROJECT" -runTests -testPlatform PlayMode -testFilter 'ShiftFour.Tests.AdvancedGuardPlayModeTests;ShiftFour.Tests.DroneMotorPlayModeTests;ShiftFour.Tests.DroneDestructionPlayModeTests;ShiftFour.Tests.CombatAuditPlayModeTests;ShiftFour.Tests.DroneMovementSpeedsPlayModeTests;ShiftFour.Tests.DroneDifficultyPlayModeTests;ShiftFour.Tests.DroneHearingPlayModeTests;ShiftFour.Tests.DroneAwarenessPlayModeTests;ShiftFour.Tests.DroneAnimationPlayModeTests;ShiftFour.Tests.SoundEventsPlayModeTests;ShiftFour.Tests.PlayerNoisePlayModeTests' -testResults "$GUARD_RESULTS/FocusedPlayMode.xml" -logFile "$GUARD_RESULTS/FocusedPlayMode.log"

"$UNITY" -batchmode -nographics -projectPath "$REVIEW_PROJECT" -runTests -testPlatform EditMode -testResults "$GUARD_RESULTS/EditMode.xml" -logFile "$GUARD_RESULTS/EditMode.log"
```

## Retained iterations and findings

- `GuardPlayMode.initial.xml/.log`: 0/9 passed because the common setup exposed the unusable authored post. `PostDiagnostic.xml/.log` records body clearance false, a complete native path, and the two fixed-cover colliders overlapping Home. A bounded, validated adjacent-post fallback fixed the proposal without scene changes.
- `GuardPlayMode.second.xml/.log`: 9/9 passed after the post fallback.
- `FocusedPlayMode.before-console-fix.xml/.log`: 100/100 passed, including nine Guard cases and 91 existing regressions, before adding actual-console integration. `GuardPlayMode.xml/.log` then passed 9/9 with a strengthened physical cover-arrival assertion. Production code did not change for that stronger assertion.
- `FocusedEditMode.xml/.log`: 115/115 focused cases passed before the producer fix; retained separately from the final Edit Mode suite.
- `ActualConsole.xml/.log`: the extra actual-console case failed. `ConsoleDiagnostic.xml/.log` showed acoustic transmission **1**, but source navigation projection **false** at the cabinet center. No change was made to hearing's strict attachment, wall or gate rules.
- `GuardConsolePlayMode.xml/.log`: 9/10 passed during anchor tuning. A local offset was scaled by the cabinet's mesh transform, so it did not reach the intended walkable side. The final API expresses offset metres along local axes independently of mesh scale.
- `ActualConsoleFinal.xml/.log`: the real activation case passed after the anchor fix. The final regression reruns it together with all other Guard and noise producer cases against the final proposal.

The final patch adds one optional console offset and assigns it only in the Server section prefab. Other consoles retain zero offset, original origin/category/intensity/source and activation behavior. Existing producer tests remain unmodified. Guard's approach fallback stays inside the reported sound uncertainty radius, protected area and validated routes.

## Evidence files

- `AdvancedGuard.task9.patch`: complete review delta, checked for clean applicability but not applied to active assets.
- `GuardBrain.cs.txt`, `DroneBrain.cs.txt`, `ConsoleSwitch.cs.txt`, `Server.prefab.txt`, `harness/`: exact proposed sources, including stable test meta GUID.
- `rehearse.py`: disposable-project entrypoint.
- `before.json`, `integrity.json`: captured/final original-asset hashes and separate Git-state record.
- `validated-source-sha256.json`: actual final candidate C# source hashes and Server prefab hash.
- `test-summary.json`, `TEST-MATRIX.csv`: actual final NUnit results.
- `MainLab-demo.csv`, `frames/`, [VISUAL_SCENARIO.md](VISUAL_SCENARIO.md): rendered Guard scenario and actual phase/movement trace.
- XML/logs: real Unity output and failure history.

No MainLab generation, NavMesh baking, active owned-asset edits, commit, staging or push was performed by this task. Candidate Unity serialization of platform defaults in its own ProjectSettings copy is recorded separately and excluded from the patch.
