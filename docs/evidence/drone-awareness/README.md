# Task 6 — Shared awareness evidence

Active project: `/Users/dev/Desktop/game/Shift-Four`

Branch: `feature/progressive-drone-intelligence-workspace`

Starting HEAD: `a4ff4fdebcd3a7c461d416dfcc2c565926b37b05`.

The active project already contained verified hearing and the user's existing working changes. Task 6 preserves them. Its source/config/tests are applied to this active project and mirrored into `Shift-Four-Progressive` for tests because the active project's Unity editor is running. The full C# manifest confirms both copies match; the active editor is left running.

## Actual Unity results

Unity 6000.6.4f1, batch mode, 2026-10-19. Raw XML timestamps are UTC.

| Run | Passed | Failed / skipped | Execution seconds |
| --- | --- | --- | --- |
| [EditMode](EditMode.xml) | 251/251 | 0 / 0 | 0.394 |
| [FocusedPlayMode](FocusedPlayMode.xml) | 6/6 | 0 / 0 | 1.820 |
| [PlayMode](PlayMode.xml) | 128/128 | 0 / 0 | 354.151 |

All final processes exited 0. The full suites contain **379 unique cases**, including **26 new Edit Mode and 6 new Play Mode cases**. Focused Play Mode is a subset and is not counted again. [TEST-MATRIX.csv](TEST-MATRIX.csv) lists full-suite results; corresponding local logs are retained.

The first focused Play Mode run passed 5/6. The vision fixture had disabled CharacterController for teleportation and left it disabled, so actual ClearShot could not hit the player collider. The corrected fixture re-enables the controller before synchronizing transforms and performing the same real CanSeePlayer assertion. The following focused and full runs both passed. [Initial XML](FocusedPlayMode.initial.xml) and its log are preserved. No production behavior or existing regression assertion was weakened to address this fixture failure.

## Focused coverage

The deterministic helper tests cover:

- Patrol → Suspicious and suspicion accumulation from distinct emissions.
- Suspicious → Investigate with sufficient strength, suspicion and dwell.
- Investigate → Search using an emission-anchored deadline.
- Search → ReturnToPost → Patrol, including arrival and bounded recovery timeout.
- Valid visual confirmation immediately entering Combat, including during search dwell.
- Visual loss clearing confirmation immediately and entering Search after grace/dwell.
- Separate visual/hearing memory, lifetime expiry and ignored hidden-position arguments.
- New noise interrupting search and pending evidence surviving the minimum dwell.
- Repeated polling not adding suspicion/refreshing evidence; repeated loud noise never causing Combat or state chatter.
- Weak repeated noise raising Suspicious while failing the investigation reliability threshold.
- Decay/exit hysteresis, clock gaps, stale/future/altered records and gate-invalidated hearing availability.
- Immediate latched death shutdown, invalid time/positions and validated configuration/profile copies.

MainLab Play Mode tests use the actual scene and classes:

- All four drones expose awareness; actual hearing plus awareness updates preserve each policy's decision name/destination/score.
- Actual CanSeePlayer confirmation drives Combat, then sight loss drives Search while preserving the visual snapshot rather than following hidden movement.
- Actual typed gunshots raise suspicion; hidden targets remain visually unconfirmed, lastSeenAt stays unchanged and TryShoot causes no damage.
- Real Health.TakeDamage/Died disables awareness synchronously on all four drones, with no further observation processing.
- An actual mission loss/restart disables all old models and creates fresh enabled services.
- Four isolated services traverse Combat → Search → ReturnToPost → Patrol over 4,000 ticks under calibrated allocation measurement.

Full regressions include hearing isolation/gates and restarts, legacy gunshots, A*, motor/body collision protection, Control/Interceptor behavior, camera, health initialization/reapplication, death and animations.

## Lightweight processing measurement

[awareness-ticks.csv](generated/awareness-ticks.csv) records:

- Models: 4
- Total ticks: 4000
- GC.Alloc events: 0
- Marker samples: 0
- Positive allocation calibration: 1
- Elapsed milliseconds including measurement overhead: 0.4634

The helper introduces no physics, graph queries, path creation, global actor search, new event subscriptions or independent Update loops. This measurement isolates awareness processing and transitions; upstream vision, hearing and existing tactical/navigation costs remain separate. It is a local batch timing, not a hardware-independent frame-time guarantee.

## Interface, policy guidance and exact files

[DRONE_AWARENESS.md](../../DRONE_AWARENESS.md) contains the final API, all six states, default/validated settings, evidence lifetime rules, disabled-state semantics and guidance for Scout, Guard, Interceptor and Flanker.

- [changed-files.json](changed-files.json): only DroneBrain.cs is modified among existing files. Two new production classes and two new test files each have a .meta, plus the implementation document.
- [DroneBrain.task6.patch](DroneBrain.task6.patch) and [prior text](DroneBrain.task5.txt): review the small shared adapter against Task 5.
- [task6-source.zip](task6-source.zip): exact ten-file task payload, including the final implementation document.
- [task6-before.json](task6-before.json): starting tracked/untracked file inventory, HEAD/branch, status and index entries; previous uncommitted work is included.
- [validated-source-sha256.json](validated-source-sha256.json): all 76 C# files used for final verification.
- [verification-before.json](verification-before.json): verification-worktree baseline and source manifest before mirroring Task 6.
- [integrity.json](integrity.json): active/test source hashes match; 131 protected files/profile assets unchanged; individual policies, Interceptor/Control tactics, motor/body, vision/fire methods unchanged; no temporary test scenes; original HEAD and index entries preserved.

Unity's incidental platform-default changes in the verification worktree are archived as `verification-settings-generated.txt` and `.patch`; its exact pre-test settings are restored after the batch editor exits. Active project settings are untouched. Generated regression screenshots are compressed into `generated/screenshots.zip`, verified against `screenshots-sha256.json`, and redundant loose TGA files are removed only from this task's generated output.

No individual tactics, saved scenes, prefabs, models, navigation data, hearing/producers, Health or difficulty assets are changed. No commit, push or index staging is performed.

## Reproduction

Use one Unity process at a time on the matching verification worktree:

```sh
SHIFTFOUR_EVIDENCE=/Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-awareness/generated /Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/dev/Desktop/game/Shift-Four-Progressive -runTests -testPlatform EditMode -testResults /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-awareness/EditMode.xml -logFile /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-awareness/EditMode.log
SHIFTFOUR_EVIDENCE=/Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-awareness/generated /Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/dev/Desktop/game/Shift-Four-Progressive -runTests -testPlatform PlayMode -testFilter ShiftFour.Tests.DroneAwarenessPlayModeTests -testResults /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-awareness/FocusedPlayMode.xml -logFile /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-awareness/FocusedPlayMode.log
```

Remove the testFilter for full Play Mode. Use fresh output paths to preserve these records. Run finalize-evidence.py only after Unity exits; it performs the recorded integrity checks and archives only this run's generated artifacts.
