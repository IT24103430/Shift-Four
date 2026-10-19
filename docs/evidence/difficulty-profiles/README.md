# Task 2 — verified difficulty profiles

**PASS:** final Unity **6000.6.4f1** Editor verification on 19 October 2026, Metal / Apple M1. The tested code is in `/Users/dev/Desktop/game/Shift-Four-Progressive`, branch `feature/progressive-drone-intelligence`, over the preserved Task 1 snapshot.

| Final suite | Passed | Failed | Skipped | Inconclusive | Execution time | Raw evidence |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Edit Mode | 160 | 0 | 0 | 0 | 0.503 s | [XML](EditMode.xml), [log](EditMode.log) |
| Play Mode | 93 | 0 | 0 | 0 | 369.414 s | [XML](PlayMode.xml), [log](PlayMode.log) |
| Total distinct final cases | **253** | **0** | **0** | **0** | | [Full case matrix](TEST-MATRIX.csv) |

Both final processes exited with code 0. The 7/7 [focused difficulty Play Mode cases](FocusedPlayMode.xml) also passed before the full regression run; they are included in the 93 above and are not counted twice. There are **36 new cases**: 29 Edit Mode and 7 Play Mode. All 217 Task 1 cases remain in the final suites without source/assertion changes.

Local execution windows, Asia/Colombo: full Play Mode 14:55:41–15:01:51; final Edit Mode 15:02:37–15:02:38. NUnit durations exclude Editor startup/import. [Machine-readable summary](test-summary.json). The initial 160/160 Edit Mode run is retained in `InitialEditMode.xml`/`.log`; final Edit Mode was rerun after the activation-time health fix.

## Delivered configuration and behavior

- Four Inspector-configurable assets: Scout **100 HP / 1.8 patrol / 2.2 investigate / 2.6 chase**; Guard **150 / 2.0 / 2.5 / 3.0**; Interceptor **125 / 2.5 / 3.1 / 3.7**; Flanker **150 / 2.8 / 3.5 / 4.2**, speeds in m/s.
- Acceleration, angular speed, hearing sensitivity, visual range, three ordered suspicion thresholds and bounded search duration are configurable. [All tuning values and validation rules](../../DRONE_DIFFICULTY_PROFILES.md).
- `DroneDifficultySettings` provides serializable validated value data; `DroneDifficultyProfile` provides the Inspector asset and default lookup; `DroneArchetype` identifies the four default resources.
- `DroneBrain.InitializeDifficulty()` resolves an optional override or default by type and binds health without editing existing prefabs/scenes. `Health.TryInitializeMaximum()` accepts fresh initialization once and rejects reapplication, accepted-damage recovery and fatal resurrection attempts.
- Actual MainLab HP is 100/150/125/150 at startup and after actual GameSession loss/restart. Ordinary damage and natural healing preserve the assets, maximum health and initialization guard. `Health.Died` fires once per life, including reentrant profile attempts during a fatal callback.
- A reproduced inactive-object activation bug is fixed: `Awake` preserves already initialized/damaged health rather than healing 125 to 150. The exact failing regression and subsequent passing run are retained.

Task 2 **applies health**. Movement and sensing/awareness settings are stored and exposed for future tasks; existing 3.5 m/s cruise speed, vision/combat checks, motor behavior and all individual AI decisions remain unchanged. Player walking speed (5 m/s), gun damage (25), fire interval (0.24 s) and HP (100) are asserted unchanged in the new MainLab fixture.

## Verification coverage

| Requirement | Evidence |
| --- | --- |
| Four correct default profiles | Four authored-asset/resource-load cases, raw-versus-validated values, actual MainLab initialization. |
| Invalid configuration | Negative, NaN and both infinities; ordered movement speeds and suspicion thresholds; zero hearing; idempotent validation; defensive runtime getter and Inspector callback. |
| Independent Inspector overrides | A cloned custom profile initializes only its target instance to 175 HP; default asset JSON remains unchanged. |
| Initialization/reset | Fresh-life contract and explicit legacy Configure reset; actual MainLab loss/restart restores all four maxima/current HP and removes wrecks. |
| No profile-induced healing | Repeated application, damage before first binding/Start, full natural recovery, inactive initialization followed by damage and activation. |
| Fatal notification exactly once | Repeated fatal damage and reentrant binding attempts; one event per life and one more after an explicit reset. |
| Existing animation/death/combat | Unchanged animation, destruction and combat audit suites passed, including actual player shots and gravity wreck behavior. |
| Navigation/prediction/gates | Full existing movement, collision, tactical junction, prediction, 400 graph/gate combinations, mission completion and restart fixtures passed. |

[Generated fixture traces](generated/), [scripted mission trace](generated/scripted-mission.csv), [test-generated captures](raw-captures.zip) and [capture hashes](raw-captures-sha256.json) are retained. These are automated fixtures, not manual gameplay validation.

## Exact changes and integrity

- Only **two existing files** changed from Task 1: `Assets/Scripts/Gameplay/Health.cs` and `Assets/Scripts/AI/Core/DroneBrain.cs`.
- **19 new Unity files** comprise the two runtime source files plus metadata, four assets plus metadata, two test files plus metadata, and three folder metadata files. One new main implementation document makes **22 source/config/test/document paths** outside this evidence directory.
- [Exact Task 2 file inventory](changed-files.txt), [file hashes](changed-files.json) and [source archive](task2-source.zip). The uncommitted Task 1 overlay remains intact; `git diff HEAD` includes that older work as well as Task 2. Use this inventory and the [Health](Health.task2.patch) / [DroneBrain](DroneBrain.task2.patch) patches to review only Task 2.
- [All 60 tested C# source hashes](validated-source-sha256.json) and [profile asset/metadata hashes](profile-asset-sha256.json) match the final files.
- [Integrity check](integrity.json): all **131 pre-existing protected assets** unchanged; individual policies, sound, movement/body, animation, player/console/gates, existing tests, metadata GUIDs, packages and assemblies unchanged; exactly one InterceptorPolicy; no temporary Test Runner scenes left.
- [Original worktree integrity](original-worktrees-integrity.json) confirms all three original worktrees retain their HEADs, statuses and 1,641 recorded files.
- Unity's automatic platform-default rewrite in ProjectSettings was archived as [a patch](unity-generated-settings.patch) and an output copy, then only that incidental delta was removed after testing. The original settings content is restored; no branch reset/discard operation was used.
- `git diff --check` passed. [Destination status](destination-status.txt) records the expected uncommitted Task 1 + Task 2 work. No commit, push, merge or prefab approval was needed/performed.

## Corrected attempts and remaining scope

[FAILURES_AND_LIMITS.md](FAILURES_AND_LIMITS.md) documents the aborted initial fixture, scene-name cleanup failures, frame-timing assumption and confirmed Awake-healing defect. Their logs/XML are preserved. No old tests were weakened or changed.

No sound detection, sensing behavior, awareness state machine, movement-mode switching, policy rewrite or gameplay balance study was started. Full restart remains a scene reload; this task does not implement pooled revival of individual destroyed drones. No standalone-build, manual input or stable-FPS claim is made.

## Reproduce

Close any Editor using the Progressive worktree. These commands preserve the evidence above and write new results to ignored Logs:

```sh
INTEGRATION_ROOT='/Users/dev/Desktop/game/Shift-Four-Progressive'
UNITY_EDITOR_PATH='/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'
RUN_DIR="$INTEGRATION_ROOT/Logs/DifficultyVerification"
mkdir -p "$RUN_DIR/generated"
for PLATFORM in EditMode PlayMode; do
  env SHIFTFOUR_EVIDENCE="$RUN_DIR/generated" "$UNITY_EDITOR_PATH" \
    -batchmode -projectPath "$INTEGRATION_ROOT" -runTests \
    -testPlatform "$PLATFORM" -testResults "$RUN_DIR/$PLATFORM.xml" \
    -logFile "$RUN_DIR/$PLATFORM.log" || break
done
```
