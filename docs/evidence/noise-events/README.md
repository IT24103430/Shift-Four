# Task 3 — verified shared noise events

**PASS:** new Unity **6000.6.4f1** Editor verification on 19 October 2026, Metal / Apple M1, in `/Users/dev/Desktop/game/Shift-Four-Progressive` on `feature/progressive-drone-intelligence`.

| Suite | Passed | Failed | Skipped | Inconclusive | Execution time | Raw results |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Edit Mode | 195 | 0 | 0 | 0 | 0.486 s | [XML](EditMode.xml), [log](EditMode.log) |
| Play Mode | 101 | 0 | 0 | 0 | 351.645 s | [XML](PlayMode.xml), [log](PlayMode.log) |
| Distinct final cases | **296** | **0** | **0** | **0** | | [Per-case matrix](TEST-MATRIX.csv) |

All 253 Task 2 cases remain unchanged and passed. Task 3 adds **43 cases**: 35 Edit Mode and 8 Play Mode. The separate [focused Play Mode run](FocusedPlayMode.xml) also passed 8/8 before full regression; these cases are already included in the 101 and are not counted twice. Every executed Task 3 run passed; no test-source corrections or weakened existing assertions were needed.

All three Unity processes exited with code 0. Local windows (Asia/Colombo): Edit Mode 15:16:55; focused Play Mode 15:17:52; full Play Mode 15:18:49–15:24:41. Durations exclude Editor startup/import. [Machine-readable summary](test-summary.json).

## Delivered API

`SoundEvents.NoiseEmitted` dispatches immutable `NoiseEvent` values with Position, Category, Intensity, Timestamp and optional Source identity. Categories are Footstep, Sprint, Gunshot, ConsoleInteraction and Impact; central default intensities are 0.20, 0.45, 1.00, 0.40 and 0.30 respectively.

`ReportNoise(position, category, intensity, source = null)` uses gameplay Time.time. `ReportNoise(NoiseEvent)` supports explicit deterministic timestamps. Both return false for silent/invalid input. The original `ReportShot(Vector3)` / `ShotFired` API remains compatible; every accepted gunshot routes through one dispatcher and reaches each modern/legacy listener exactly once.

Synchronous ordered snapshots, unique registration, safe repeated removal, stale Unity-target cleanup, idempotent scene-unload hooks and subsystem reset handle lifecycle safety. One failing callback is removed/logged once per registration, while other listeners and the compatibility channel continue.

The [full contract and exact Task 4 producer/consumer call sites](../../SHARED_NOISE_EVENTS.md) specify units, validation, callback mutation/reentrancy rules and lifecycle ownership. No hearing responses or new player/console/impact producers were added in Task 3. Existing PlayerController gunshots now reach the modern channel through their unchanged legacy call.

## Evidence against requirements

| Requirement | Executed evidence |
| --- | --- |
| Correct creation, categories and intensity | Five category/default cases; exact payload metadata; live gameplay timestamp/source; immutable emission position even after the source moves. |
| Deterministic dispatch | Explicit timestamps, registration order, frozen modern/legacy channel snapshots, mutation during delivery and bounded depth-first reentrant report test. |
| Multiple listeners and no duplicates | Unique callbacks, repeated +=/-=, multicast/null registration, distinct same-frame reports, one bridge per gunshot, actual PlayerController.Shoot. |
| Legacy compatibility | Original ReportShot signature and Action<Vector3> event; direct and typed gunshots bridge once; non-gunshot categories never invoke ShotFired. |
| Registration and fault safety | Enable/disable cycles, destruction without unsubscription, stale-target rejection/pruning, one expected exception per deliberately failing observer registration, healthy later callbacks continue. |
| Scene restart | Three actual GameSession loss/restart cycles; old probe references 0/0 and one fresh Scout listener each cycle. [Trace](generated/noise-restart.csv). |
| Persistent observer safety | A DontDestroyOnLoad observer survives another scene's unload; old scene observer is pruned. |
| Static reset | Repeated subsystem-reset invocation plus enable/resubscribe cycles; both channels clear and the scene-cleanup hook remains singular. Editor domain-reload settings were not changed. |
| Existing gameplay | Unchanged combat, animation/destruction, camera/body collision, difficulty/restart, gates, navigation and predictive/tactical tests passed in the full suite. Actual player shot remains 25 damage and 0.24 s cooldown. |

## Allocation measurement

[noise-dispatch.csv](generated/noise-dispatch.csv) records **2,000 warmed gunshot reports**, evenly using the explicit-record and original ReportShot entry points:

- 2,000 modern notifications and 2,000 legacy notifications: one per listener/report.
- **0 GC.Alloc events**, 0 allocation-marker samples during the measured workload.
- Positive calibration: a known managed allocation produced **1 allocation event**, validating the recorder.

The measured observers perform only counter increments; delegates are compiled/registered and paths warmed before recording. This proves the measured warmed dispatch workload, not zero allocation bytes, arbitrary subscriber behavior, registration/unregistration, exception handling, native allocations, whole-game GC or stable FPS. Normal dispatch contains no GetInvocationList or log formatting; invocation-list arrays are created only for registration/removal.

## Source and asset integrity

- Only existing source file modified in Task 3: `Assets/Scripts/Gameplay/SoundEvents.cs`.
- New runtime data file: `Assets/Scripts/Gameplay/NoiseEvent.cs` plus metadata. New test sources: SoundEventsEditModeTests, SoundEventsPlayModeTests and NoiseListenerProbe, with metadata. **8 new Unity files**, **1 modified Unity file**, and the new main API document make **10 paths** outside this evidence directory.
- [Exact change list](changed-files.txt), [hash inventory](changed-files.json), [SoundEvents-only patch](SoundEvents.task3.patch), and [Task 3 source archive](task3-source.zip). These distinguish this task from the pre-existing uncommitted Task 1/2 work.
- [All 64 tested C# source hashes](validated-source-sha256.json) match the final sources. [Integrity verification](integrity.json) confirms all **131 existing protected assets** and all difficulty assets/metadata unchanged; no temporary Test Runner scenes remain; exactly one InterceptorPolicy still exists.
- [Original worktree verification](original-worktrees-integrity.json) confirms all three original HEADs/statuses and 1,641 recorded files are preserved.
- Unity's automatic platform-default serialization was archived as [a patch](unity-generated-settings.patch) and an output copy, then only that incidental settings delta was removed in the Progressive worktree after testing. The original settings content is restored.
- `git diff --check` passed. [Destination status](destination-status.txt) shows the expected uncommitted Task 1/2/3 overlay. No commit, push, merge, scene or prefab edit.

The full suite also generated [runtime traces](generated/) and [15 render captures](raw-captures.zip) with [hashes](raw-captures-sha256.json). These are automated fixture outputs, not manual gameplay validation.

## Limits and Task 4 boundary

This is a main-thread gameplay disturbance bus. It has no player-tracking history, propagation, attenuation, hearing response, visual confirmation, weapon authorization or producer polling. Source is identity only; future consumers must use the emission position and enforce bounded age/uncertainty.

Automatic stale cleanup covers bound Unity component/game-object listeners and destroyed Unity targets. Static callbacks and ordinary CLR closures are session-wide and need explicit removal for shorter lifetimes. Task 4 should use named component handlers paired in OnEnable/OnDisable and avoid anonymous closures capturing scene objects. Existing Scout's legacy response is preserved; migrating it later must avoid two parallel hearing memories.

The new API rejects invalid/silent reports rather than logging every one. Fault-isolation tests intentionally inject and expect exceptions; those expected log entries are not test failures. No standalone-build, human-input playthrough or whole-game performance claim is made.

## Reproduce

Close any Editor using this worktree; write fresh output to ignored Logs:

```sh
INTEGRATION_ROOT='/Users/dev/Desktop/game/Shift-Four-Progressive'
UNITY_EDITOR_PATH='/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'
RUN_DIR="$INTEGRATION_ROOT/Logs/NoiseVerification"
mkdir -p "$RUN_DIR/generated"
for PLATFORM in EditMode PlayMode; do
  env SHIFTFOUR_EVIDENCE="$RUN_DIR/generated" "$UNITY_EDITOR_PATH" \
    -batchmode -projectPath "$INTEGRATION_ROOT" -runTests \
    -testPlatform "$PLATFORM" -testResults "$RUN_DIR/$PLATFORM.xml" \
    -logFile "$RUN_DIR/$PLATFORM.log" || break
done
```

Add `-testFilter SoundEventsPlayModeTests` for only the eight focused Play Mode cases. Task 4's precise integration examples are in [SHARED_NOISE_EVENTS.md](../../SHARED_NOISE_EVENTS.md).
