# Player jumping and landing — Task 2

Implemented on `feature/fps-player-weapons-prep` in `/Users/dev/Desktop/game/Shift-Four-FPS-Prep`, continuing from baseline `2aece7603222c2fc3964779cf0da75e9a9456dd5`. Task 1's existing uncommitted documentation and evidence remain in place. Unity version: 6000.6.4f1. Verification date: 2026-10-20, Asia/Colombo.

## Behavior

`PlayerController` consumes the existing legacy `Input.GetButtonDown("Jump")` binding (Space), after the existing life/mission/cursor guards. Jump height defaults to **1.1 m** and gravity is a configurable positive downward magnitude, default **20 m/s²**. Initial upward speed is `sqrt(2 * gravity * jumpHeight)`, approximately **6.633 m/s** with the defaults. Finite positive settings are validated in Awake and OnValidate.

The existing CharacterController supplies collision resolution. No Rigidbody is added. Gravity is integrated with displacement `v * dt - 0.5 * g * dt²`, then velocity is reduced by `g * dt`. Collision steps are at most 1/60 s, including during lower frame rates or long frames, so ceiling/landing contacts are handled during the elapsed interval rather than skipping an entire arc.

A launch requires `isGrounded`, a contact normal within the existing slope limit, nonpositive vertical speed, live Health and a mission that has not ended. Launch immediately invalidates the cached support because CharacterController's grounded result refers to its preceding Move. This prevents a second jump request on the takeoff frame as well as during ascent/descent or after walking off a ledge.

An Above collision cancels positive vertical velocity. Gravity continues on subsequent movement steps. Landing on valid support resets downward velocity to the existing −1 m/s grounding bias, allowing a fresh jump. The landing sample and any sample containing airborne movement reset walking-noise accumulation; ordinary footsteps resume only after sufficient new grounded travel. **No optional landing-noise event was added.**

WASD direction/normalization, mouse look, cursor behavior, interaction and shooting remain in their existing paths. Capsule height/radius/center, step offset, slope limit, skin width, camera eye height, pitch and near clip are not changed. A moving jump uses the existing 5 m/s walk speed; no sprint mechanic is introduced. Death/mission end keep their existing movement lock, and scene restart creates fresh movement state.

The integration follows Unity's documented [CharacterController.Move collision flags and explicit gravity requirement](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/CharacterController.Move.html) and [grounded result from the preceding move](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/CharacterController-isGrounded.html).

## Source changes

- `Assets/Scripts/Gameplay/PlayerController.cs`: jump configuration, Jump input, supported launch, continuous gravity, ceiling response and walking-noise gating. Existing private `MovePlayer(direction, deltaTime)` and shooting contracts remain available to the existing tests.
- `Assets/Tests/EditMode/Movement/PlayerJumpEditModeTests.cs`: nine cases covering defaults, valid/invalid tuning, unchanged capsule/camera configuration, absence of Rigidbody and the existing Space binding.
- `Assets/Tests/PlayMode/PlayerJumpPlayModeTests.cs`: twelve collision/movement cases and three actual-MainLab integration cases. New tests receive their own meta/GUID files; existing GUIDs are preserved.

There are no edits to weapons, drones, scenes, imported assets, Health, SoundEvents, InputManager, packages or project rendering settings. Tests create temporary runtime geometry and temporarily move/disable scene objects; they never save those changes. The original checkout/open Unity session remains on `feature/progressive-drone-intelligence-workspace`.

## Verification matrix

| Requirement | Actual check |
| --- | --- |
| Standing jump | 1.1 m measured apex, expected airborne interval, returns to floor, then permits a new launch |
| Configurable arc | A 0.7 m height and 12 m/s² gravity change the actual measured apex, launch velocity and airtime |
| Moving jump | Same height; forward travel equals 5 m/s × airborne duration |
| No double jump | Reject an immediate second request while previous Move still reports grounded; reject requests throughout ascent and descent |
| Low ceiling | Real BoxCollider only 0.25 m above the standing head; Above contact cancels upward velocity and the player lands |
| Raised landing | Running jump lands on a 0.6 m platform and can jump from its top |
| Airborne footsteps | Partial walking distance is cleared at takeoff; flight and landing produce zero ordinary footsteps and no catch-up burst |
| Capsule/camera | Verify unchanged capsule and 1.3 m camera-relative height through an entire jump/landing at −65° camera pitch; near clip remains 0.05 m |
| Walk-off / trigger support | Falling or trigger-only support cannot authorize a jump |
| Death / mission end | Both reject launches; a death during an actual MainLab jump stops Update movement; real GameSession.Restart reloads a healthy player that can jump |
| Locked gates | 35 reachable approaches: all four gates, center/inside-edge/adjacent-wall offsets, repeated jump requests, diagonal pressure; both sides of the three interior gates and the lab side of final exit |
| Room boundary height | Actual MainLab capsule top during a floor jump remains below all twelve 4 m fixed walls |
| Timing | Actual CharacterController motion at deterministic 20/30/60/120/144 Hz timesteps and 120 ms long frames; one second of travel, apex and landing checks |
| Pause / invalid time | Zero, negative and nonfinite movement timesteps neither move the player nor consume launch velocity |

The tests call the same private launch/movement paths used by Update, with real CharacterController collisions. They do **not** synthesize operating-system keyboard input or claim a manual playthrough. Space binding and the compiled Update input wiring were inspected. Camera equality uses a 0.1 mm tolerance to account for floating-point world/local transform conversion.

### Measured timestep results

| Timestep rate | Sampled peak (m) | First grounded sample (s) | One-second horizontal travel (m) |
| --- | --- | --- | --- |
| 20 Hz | 1.096637 | 0.700000 | 5.000000 |
| 30 Hz | 1.099972 | 0.666667 | 5.000000 |
| 60 Hz | 1.099972 | 0.666667 | 5.000000 |
| 120 Hz | 1.099973 | 0.658333 | 5.000000 |
| 144 Hz | 1.099973 | 0.659722 | 5.000000 |
| 120 ms frames | 1.091969 | 0.720000 | 5.000000 |

Differences in first-grounded timestamps include sampling quantization. The character still resolves collision in shorter internal steps. The theoretical continuous airtime to the takeoff height is approximately 0.6633 s. Raw measurements are in [frame-rates.csv](evidence/player-jump/generated/frame-rates.csv).

### Gate fixture correction and limits

The first gate stress fixture also teleported the player outside the final exit and drove it diagonally beyond the exterior floor. Four trajectories fell below the authored level and later crossed the gate's infinite plane from underneath. Those were not reachable approaches to a locked gate from gameplay. The retained [exterior trace](evidence/player-jump/invalid-exterior-mainlab-gate-trace.csv) records that diagnosis. The final fixture covers the lab side of that exit and both sides of every interior gate; it does not remove a discovered interior bypass or change scene geometry to get a pass.

Final [gate summaries](evidence/player-jump/generated/mainlab-gate-jumps.csv) and [per-step traces](evidence/player-jump/generated/mainlab-gate-trace.csv) retain the numerical evidence. These checks validate the authored layout at the default jump settings, not arbitrary future geometry, teleports or increased jump heights. Keep these regressions when tuning height or changing collision geometry. The existing exterior floor boundary is not expanded by this task.

## Unity execution and evidence

The original imported Samples still require unavailable Input System/TMPro types and prevent an unmodified project compile, as recorded during [Task 1 preparation](FPS_PREPARATION.md). No sample/package fix is introduced here. Tests run in the same disposable project, where `Assets/Samples` and its meta remain outside Assets. All gameplay, MainLab, prefabs, weapons and tests are included. Unity's disposable-only platform-default settings serialization is retained from Task 1 and is not copied into either real worktree.

| Run | Result |
| --- | --- |
| All existing + new Edit Mode tests | **315 passed**, 0 failed/skipped/inconclusive |
| Final focused Play Mode run | **17 passed**: all **15 jump cases** plus isolated reruns of the two full-suite failures; 0 failed/skipped/inconclusive |
| Full existing + new Play Mode suite | **171/173 passed; 2 failed**; 0 skipped/inconclusive; 519.638 s test duration |
| Baseline-controller control | **2/2 passed** for the same isolated hearing/navigation cases |

The full run's failures are:

- `DroneHearingPlayModeTests.Wall_OccludesQuietSoundsAndAttenuatesLoudReachableSounds`: the loud, occluded sound was rejected.
- `InterceptorSensorIntegrationPlayModeTests.MainLab_SoundResponse_BeforeAfterEvidence`: the Interceptor did not return within 1.2 m of Home before the fixture deadline (remaining distance 8.241 m).

Both pass when selected individually alongside the final jump fixture, and both pass in an additional isolated control using the exact baseline `PlayerController.cs`. The wall-hearing fixture does not create a player. These results do **not** establish a definitive cause, and the full-suite run remains failed. Hearing estimates depend on event timestamps and entity IDs, so suite/run sensitivity is plausible, but was not repaired or claimed proven here. Drone/navigation scripts and their existing tests were left unchanged, as required by scope.

The full run compiled fourteen new jump cases. The additional actual-tuning-arc case was added only to the test file during that run and executed afterward in the final focused run; the production controller is identical across all candidate runs. [The full-run jump fixture snapshot](evidence/player-jump/PlayerJumpPlayModeTests.full-run.cs.txt) preserves that exact test input. Across the full and final focused candidate runs, 174 distinct Play Mode cases were exercised, without counting reruns twice. Detailed outcomes remain separate so passing reruns do not conceal the failed full run.

Exact commands, NUnit XML, logs, source hashes and generated measurements live under [docs/evidence/player-jump](evidence/player-jump); see [test-summary.json](evidence/player-jump/test-summary.json). Early focused failures are retained separately: one strict camera float-equality assertion and the unreachable final-exit exterior trajectories were corrected in the tests. The final controller implementation did not need a gameplay workaround for either fixture issue. The baseline controller was substituted only in the disposable copy for the two-case control and restored in a finally block; neither real worktree was changed for that experiment.

No commit, push or merge was performed. No additional packages were installed. This change remains uncommitted for review in the FPS worktree.
