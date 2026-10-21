# Task 5 — Drone hearing evidence

Worktree: `/Users/dev/Desktop/game/Shift-Four-Progressive`  
Branch: `feature/progressive-drone-intelligence`  
HEAD: `a523d7d953118cfbb154042b6304723b02ea0e39` (unchanged; no commit or push).

The original `Shift-Four` checkout remains on `feature/gv-asmadala-drone-animation` and is unchanged. Open `Shift-Four-Progressive` to see Task 1–5 changes in the IDE Source Control view.

## Actual Unity results

Unity 6000.6.4f1, batch mode, 2026-10-19. Raw XML records UTC timestamps.

| Run | Passed | Failed / skipped | Execution seconds |
| --- | --- | --- | --- |
| [EditMode](EditMode.xml) | 225/225 | 0 / 0 | 0.633 |
| [FocusedPlayMode](FocusedPlayMode.xml) | 14/14 | 0 / 0 | 3.522 |
| [PlayMode](PlayMode.xml) | 122/122 | 0 / 0 | 351.426 |

The final full suites contain 347 unique cases, including 13 new Edit Mode and 14 new Play Mode cases. Focused Play Mode is a subset and is not counted again. Final Unity processes exited 0. XML and corresponding logs are retained. [TEST-MATRIX.csv](TEST-MATRIX.csv) lists every full-suite result.

The first compile attempt rejected obsolete GetInstanceID under this Unity version; the implementation now uses GetEntityId().GetHashCode(). The original compiler log is preserved as `EditMode.compile-initial.log`. The following full Edit Mode run passed. The initial 12-case focused Play Mode run passed; two additional logical-gate/second-budget cases were then added, and the final 14-case focused run passed. No failing behavioral assertion was removed or weakened.

## Focused coverage

Runtime-only test NavMeshes are built from collider geometry and removed during cleanup; saved MainLab and navigation assets are not modified. The two MainLab-specific tests load the actual saved scene.

- Nearby detection for all five categories through the typed event bus.
- Out-of-range and zero-sensitivity rejection with zero physics work.
- Distance attenuation and stronger sensitivity increasing detectability.
- Real wall occlusion: quiet footsteps suppressed, reachable loud sound attenuated to the configured transmission strength.
- Solid enclosed area rejection even with wall transmission set to 1, using disconnected native NavMesh islands.
- Logical closed gate rejection even on an otherwise complete native navigation surface.
- Actual MainLab Control gate rejection, including a premature open graph flag; detection succeeds after actual gate animation completes and connectivity opens.
- Emission-anchored expiry, repeated-read stability, duplicate rejection and stale/future event rejection.
- Configurable bounded uncertainty; moved/destroyed source identity cannot move the stored goal or extend expiry.
- Listener disable/enable/destroy cleanup; gate-version changes invalidate previous observations.
- Per-frame burst and cross-frame per-second query budgets, including recovery after the window resets.
- All four drones receive sensors; nearby sound cannot update visual memory or authorize TryShoot. Scout consumes the approximate goal rather than supplied hidden player coordinates.

The updated existing SoundEvents restart regression retains three actual MainLab loss/restart cycles and now verifies **four unique typed subscriptions**, zero legacy Scout subscriptions, and zero references to old sensors/observers after each restart. [Restart trace](generated/noise-restart.csv) records all three cycles. Legacy gunshot emission, damage, cooldown, multiple listeners, lifecycle and dispatch tests still pass, alongside the full movement, camera, navigation, policy, health, death and animation regressions.

## Measured performance

[hearing-burst.csv](generated/hearing-burst.csv) records the final 1,000-report, single-sensor burst after warmup:

- Evaluations: 2
- Physics queries: 6
- Budget drops: 998
- Memory capacity: 4
- GC.Alloc events / samples: 16
- Positive allocation calibration: 1
- Elapsed milliseconds, including measurement overhead: 0.8161

Two evaluations use exactly six physics calls; rejected overload is not queued. The 16 allocation events come from accepted detection/route work, including the existing allocating graph API. They are marker events, not byte measurements, and do not imply per-event allocation for the other 998 reports. This is a local batch measurement, not a hardware-independent frame-time guarantee.

Production defaults cap each sensor at two evaluations/frame and eight/second. Four sensors therefore allow at most eight evaluations (24 sensor-owned physics calls) per frame and 32 evaluations (96 sensor-owned physics calls) per second. Delegated graph/NavMesh work is separately bounded by at most four route attempts per evaluation and the sensor's 32-node graph limit. Current MainLab uses five nodes. Hearing has no Update loop or per-frame physics polling.

## Artifacts and preservation

- [API, settings and limitations](../../DRONE_HEARING.md): planar single-floor assumption, conservative wall/gate behavior, source uncertainty, strict off-mesh rejection, overload false negatives and current policy adoption.
- [changed-files.json](changed-files.json): exact delta from Task 4. Three modified existing files, five new C# files with five .meta files, Perception folder .meta and implementation document. Inherited Task 1–4 changes are excluded.
- [task5-source.zip](task5-source.zip): complete exact task delta. Prior text and Task 5 patches for DroneBrain, ScoutBrain and SoundEventsPlayModeTests support review.
- [task5-before.json](task5-before.json): starting tracked/untracked file inventory. [validated-source-sha256.json](validated-source-sha256.json): all 72 C# files used for final verification.
- [integrity.json](integrity.json): 131 protected assets/settings unchanged; difficulty assets unchanged; no source drift; exactly one InterceptorPolicy; no temporary test scenes. Shared visual-update, vision and firing method bodies are byte-for-byte unchanged.
- [original-worktrees-integrity.json](original-worktrees-integrity.json): original three worktrees retain HEAD, status and all 1,641 inventoried file hashes.
- Unity's incidental ProjectSettings.asset platform defaults were archived as `ProjectSettings.unity-generated.txt` and `.patch`, then the verified original settings content was restored after the batch editor exited.
- 15 inherited regression screenshots are preserved in [screenshots.zip](generated/screenshots.zip), checked against [SHA-256 hashes](generated/screenshots-sha256.json). Redundant loose TGA files were removed only from this task's generated evidence directory.

No edits to MainLab, prefabs, models, gate code, NavMesh assets, input settings, Task 3 bus or Task 4 producers. Scout's minimal adapter was within the earlier explicit user approval; the other three individual policies are unchanged.

## Reproduction

From the integration worktree, run one Unity process at a time:

```sh
SHIFTFOUR_EVIDENCE="$PWD/docs/evidence/drone-hearing/generated" /Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/docs/evidence/drone-hearing/EditMode.xml" -logFile "$PWD/docs/evidence/drone-hearing/EditMode.log"
SHIFTFOUR_EVIDENCE="$PWD/docs/evidence/drone-hearing/generated" /Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter ShiftFour.Tests.DroneHearingPlayModeTests -testResults "$PWD/docs/evidence/drone-hearing/FocusedPlayMode.xml" -logFile "$PWD/docs/evidence/drone-hearing/FocusedPlayMode.log"
```

Remove `-testFilter` for the full Play Mode suite. Use fresh output paths to preserve these runs. Run `finalize-evidence.py` only after Unity exits; it validates the recorded delta and snapshots, archives generated output and restores only the verified incidental settings changes.
