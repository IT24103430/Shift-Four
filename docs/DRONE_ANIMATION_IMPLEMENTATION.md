# Phase 3 drone animation implementation

> Historical Phase 3 baseline. The current working implementation extends this with visible weapon effects,
> 4° flight banking and collision-aware falling wrecks. See [Advanced drone animation](ADVANCED_DRONE_ANIMATION.md)
> for current behavior, rendered evidence and complete validation results.

Implemented on `feature/gv-asmadala-drone-animation`, Unity **6000.6.4f1**, on 19 October 2026 (Asia/Colombo). Changes are uncommitted for review.

## Dependency and ownership

The working tree was clean before implementation. Both HEAD and the local approved dependency branch `feature/player-camera-drone-separation` already pointed to `1b986d8bb6e16305fae45cce5d1fe8372204dc4c`; the ancestry check passed. This includes `98ba978` (height/collision), `01693a7` (clearance evidence), and `1b986d8` (combat audit/diagnostics). No branch integration operation was necessary. No commit, push, merge, asset migration, scene generator or NavMesh bake was performed.

No scene/prefab ownership changes are needed. The four existing `DroneRotorVisuals` components provide the runtime binding point. MainLab, prefab/model/material assets and project settings remain unchanged. `DroneBody`, all four policy scripts, A*, gates, PlayerController and GameSession remain unchanged. The evidence manifest compares 166 protected files byte-for-byte with the dependency commit.

## Findings and implementation

The old presentation supported constant rotor spin, motor-owned hover, and a first-renderer firing flash. Death immediately froze rotors and directly tinted materials from DroneBrain. There was no movement/bank/brake/hit state model; material access through `.material` also created renderer-specific instances. Older scene overrides could lose the motor's visual reference. The current dependency has valid references, and recovery is now tested with deliberately missing references on inactive clones of each real prefab.

1. **One visual owner.** `DroneMotor` recovers a missing reference only when exactly one existing child presentation component is available. It caches the result and does not scan every frame. The motor keeps navigation and root yaw, and retains its original hover fallback for motor-only fixtures without a presentation component. When the presentation component exists, only that component writes the model pose, including when it is temporarily disabled. No root translation is written by animation.
2. **Accepted gameplay events.** `Health.Damaged` publishes the existing `DamageRecord` after accepted damage and after any existing death handling. Zero/negative damage and hits on dead health remain rejected. Diagnostics and HP rules are unchanged. `DroneBrain.ShotFired` fires after the final existing line-of-sight check and damage call; range, sight, health, cooldown and damage constants are unchanged. DroneBrain no longer controls renderer colors. Its existing death subscription is released on destruction.
3. **Motion and feedback.** `DroneRotorVisuals.LateUpdate` runs at order 200, after `DroneBody` at 100. It samples accepted root displacement/yaw, rather than desired agent velocity. `DroneAnimationPose` derives idle, moving, turning and braking states. Firing adds a brief light tint and small pitch response; accepted damage adds a warm tint and small bank response. Death overrides every state, clears pulses, eases back to the authored pose, fades gray and spins rotors down to zero. Re-enabling a dead visual retains shutdown with stopped rotors.
4. **Validation and documentation.** New Edit and Play Mode fixtures exercise the actual components and imported geometry without saving assets. Existing suites remain intact. This report and the member responsibility document describe integration and visual acceptance.

Damping uses exponential responses with explicit delta time. Rotor travel integrates the exponential speed change, including spin-down. The initial focused run exposed false braking during steady movement: clamping every speed decrease before filtering accumulated numerical noise. Filtering signed speed changes fixed this; a 240 Hz noise regression and actual movement tests now cover it.

The model's authored local position, rotation and scale are retained. Hover is now bounded to **±0.035 m** and combined pitch/bank to **2 degrees**. This is a presentation clearance budget, not a navigation speed or combat balancing change. The existing envelope reserves 0.10 m vertically and 0.05 m radially; adding tilt to the former ±0.08 m hover without reducing bob could consume that clearance. Actual mesh-vertex sampling across all four models, 24 root yaws, eight maximum-tilt axes, both hover extremes and four rotor phases found a minimum **0.039703 m** clearance inside the unchanged envelope. This is sampled validation, not an exhaustive proof for arbitrary replacement models or scales.

Existing rotor meshes remain independent; their existing runtime pivot mechanism is reused and does not multiply on enable/disable. Mesh colliders remain on their corresponding mesh transforms. No transform is driven by both the motor hover and the presentation owner. An incorrectly placed presentation component on the gameplay root disables itself without moving that root.

Renderer colors use cached material property blocks per material slot. Shared materials are never instantiated or modified. Original property blocks, model rest pose and rotor rest rotations are restored on disable. Event subscriptions are paired on enable/disable and tested across both component and GameObject toggles. Stable color values avoid redundant property-block submission; runtime tick code does not allocate collections.

## Executed validation

All tests ran in `/tmp/ShiftFour-AnimationValidation`, refreshed from the working Assets, Packages and ProjectSettings. The user's open editor/project was not used to run or save test scenes. Unity batch mode used `-nographics`; the results do not establish rendered visual quality or GPU frame time.

| Run | Passed / total | Failed / skipped | Test duration |
|---|---:|---:|---:|
| Updated dependency baseline, Edit Mode | 71 / 71 | 0 / 0 | 0.199 s |
| Updated dependency baseline, Play Mode | 38 / 38 | 0 / 0 | 136.373 s |
| Final complete Edit Mode | **82 / 82** | **0 / 0** | 0.210 s |
| Final complete Play Mode | **46 / 46** | **0 / 0** | 147.181 s |

The final run adds 11 Edit Mode cases and 8 Play Mode cases. No compiler error or unexpected gameplay log failed validation. Unity's batch shutdown logs include cloud-connection/debugger-listener service messages; these are separate from the gameplay tests and death overlay.

The first focused Play run passed 6/7 and failed the steady-movement assertion due to the animation's noise-biased braking calculation. After the fix, the focused run passed 7/7. The final suite adds a further invalid-root lifecycle test. No existing tests were removed, weakened or changed.

New coverage includes:

- bounded idle hover; movement, turning, braking and return to idle;
- damage-over-fire and death-over-everything priority; rejected damage and stopped-time behavior;
- damping, hover and rotor travel at 30/60/120 Hz against a 240 Hz reference; spin-down at all three rates; small speed noise at 240 Hz;
- missing visual binding recovery for all four prefab clones and existing MainLab binding/rest-pose alignment;
- repeated component/GameObject enable/disable, exact event subscription counts, no duplicate pivots, rest pose/scale/property-block restoration;
- accepted shots plus rejected cooldown, cover, range and dead-attacker attempts;
- all four models' hit tint, material identity, rotor motion, death and dead-state re-enable;
- maximum pose/rotor geometry against existing collision envelopes, root immobility, and allocation measurement.

Existing tests continue to exercise A*, gate prerequisites, route/arrival behavior, moving blockers, standoff fallback/revalidation, raised player collision, valid sustained and multiple-attacker combat, health diagnostics, death and scene restart. Restart fixtures invoke the same `Restart` method as the R key; they do not synthesize keyboard input. No gameplay exception or frozen Unity player loop was reproduced. `SYSTEM FAILURE — PLAYER ELIMINATED` remains the intended Health-zero overlay, with its existing attacker explanation.

Raw XML and protected-file hashes are in [evidence/drone-animation](evidence/drone-animation). Full local logs, including initial failed-run evidence, are retained in `/tmp/ShiftFour-Phase3Evidence`.

## Performance evidence and limits

The final synchronous presentation check took **29.852 ms for 4,000 ticks** across all four visuals, with **0 managed bytes allocated** after binding (approximately 0.00746 ms per visual tick). This run includes damage tint transitions. Material identity checks also passed.

The unchanged 12-second MainLab/Interceptor stress fixture produced the following final results:

| Marker | Total calls | Maximum calls per frame | Maximum total ms in a frame |
|---|---:|---:|---:|
| FindRoute | 40 | 2 | 0.040 |
| CanConnect | 60 | 3 | 0.022 |
| GoToCombat | 156 | 4 | 0.019 |
| ClearShot | 59 | 3 | 0.048 |
| DroneBody | 292,420 | 4 | 4.498 |
| SyncTransforms | 292,420 | 4 | 0.751 |

All navigation/combat query counts match the baseline's 40 / 60 / 156 / 59. Body/synchronization calls remain four per sampled frame; the uncapped run rendered more simulation frames, so total calls differ. The headless frame sample had p50 **0.137 ms**, p95 **0.198 ms** and maximum **44.304 ms**. The baseline maximum was **44.798 ms**. Navigation markers did not explain those largest frame spikes; this measurement does not isolate their cause. There is no evidence here justifying changes to A*, query caching, collision synchronization or combat balance. Raw baseline/final measurements are in [performance.txt](evidence/drone-animation/performance.txt).

The presentation adds no navigation query or `Physics.SyncTransforms` call. Existing allocating navigation/physics queries and per-drone synchronization are unchanged. The new `ShiftFour.DroneAnimation` marker can be inspected alongside `ShiftFour.DroneBody`, `ShiftFour.SyncTransforms`, `ShiftFour.FindRoute`, `ShiftFour.CanConnect`, `ShiftFour.GoToCombat` and `ShiftFour.ClearShot` in a rendered capture.

Batch frame timing is uncapped despite the existing fixture's target-frame-rate request. It includes editor/test overhead, and individual spikes cannot be attributed to animation from frame duration alone. The synchronous 4,000-tick allocation check exercises hit-color transitions as well as hover/rotors; its CPU average is not an end-to-end gameplay FPS measurement. Material property blocks may have rendering/batching costs that require GPU/render-thread profiling in a Development Build.

## Manual visual acceptance — still required

These steps have not been visually signed off in a rendered Unity session. Use MainLab in Unity 6000.6.4f1, leave assets unsaved, keep the Console visible, and repeat the state checks for each drone:

| Drone | Room | Movement to observe |
|---|---|---|
| Scout | Checkpoint | Patrol, investigate a shot, chase and turn back to patrol/search. |
| Guard | Server | Reposition between cover points, brake at cover and hold. |
| Interceptor | Control | Move laterally in view, then break sight; inspect junction turns and arrival. |
| Flanker | Storage | Provoke lateral approaches, direction changes and a close-range hold. |

1. At rest, inspect the small vertical hover and four independently counter-spinning rotors. The model should retain its configured flying height; the navigation root must stay on its path surface.
2. During movement and turns, inspect subtle forward pitch and inward bank without double yaw, oscillation or snapping. At arrival/blocked movement, motion should settle into hover rather than continue leaning at desired velocity.
3. Stand within unobstructed attack range. Each accepted shot should produce one short light flash and small pitch response. Solid cover, cooldown, out-of-range and dead attackers must not produce new shot flashes. Watch HP/diagnostics while checking synchronization.
4. Fire one player shot into each living drone. Its existing material detail should remain visible under the brief warm hit tint. On a fatal shot, shutdown must override any simultaneous firing/hit pulse; rotors should smoothly stop (about two seconds), the model should settle at rest height and fade gray. No new attack should follow death.
5. Inspect rotor tips, wings and mesh colliders in Scene view while banking near players, low overhead props, cover and gates. Repeat from multiple sides. The automated swept-envelope test supports clearance, but dynamic rendered contact appearance still needs human review.
6. In Play Mode only, toggle the visual component and then the drone GameObject. Check rest restoration, no extra rotor pivots, no duplicate flashes and no rotor restart on a dead drone. Exit Play Mode without saving runtime edits.
7. Compare a rendered run at 30/60/120 FPS (temporary runtime target-frame-rate/vSync controls only). Look for similar hover frequency, damping, flash duration and shutdown time. Record CPU/GC/render-thread/GPU tracks, especially property-block submission and animated mesh-collider costs.
8. Allow sustained combat to eliminate the player. Confirm the explicit overlay, moving editor/player-loop time and no red gameplay exception; press R and verify health, drones and gates reset. Complete drone-plus-console prerequisites normally for all four rooms.

No rendered screenshot/video, subjective animation-feel sign-off, hardware GPU profile or full human start-to-exit walkthrough is claimed. No scene/prefab/model ownership approval is pending because none of those assets was changed.

## Changed files and rollback

| File | Change |
|---|---|
| `Assets/Scripts/AI/Movement/DroneMotor.cs` | Cached, unambiguous visual binding recovery and presentation-owner guard around legacy hover. |
| `Assets/Scripts/AI/Core/DroneBrain.cs` | Accepted-shot event, remove material animation writes, destroy-time death unsubscription. |
| `Assets/Scripts/Gameplay/Health.cs` | Accepted-damage presentation event; existing diagnostics retained. |
| `Assets/Scripts/Gameplay/DroneRotorVisuals.cs` | Single owner of bounded visual pose, rotors, colors and lifecycle. |
| `Assets/Scripts/Gameplay/DroneAnimationPose.cs` + meta | Deterministic presentation state and damping/integrated rotor travel. |
| `Assets/Tests/EditMode/Movement/DroneAnimationEditModeTests.cs` + meta | Presentation math, priorities, noise and time-step regressions. |
| `Assets/Tests/PlayMode/DroneAnimationPlayModeTests.cs` + meta | Actual prefab/MainLab binding, events, lifecycle, motion, geometry and allocation checks. |
| `docs/members/04-movement-interceptor.md` | Link this implementation and the visual review checklist to the Agent Controller responsibility. |
| This report and `docs/evidence/drone-animation/` | Reproducible results, performance output and protected-file verification. |

Changes are reversible at the source level: restore the four modified runtime scripts to the dependency commit and remove the new pose helper/tests if rejecting this implementation. No serialized migration or asset rollback is required. Do not discard unrelated working changes when reviewing/reverting.

To reproduce the suites on a closed or isolated project (repeat with `-testPlatform EditMode`):

```sh
/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath /tmp/ShiftFour-AnimationValidation \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/ShiftFour-Phase3Evidence/review-play.xml \
  -logFile /tmp/ShiftFour-Phase3Evidence/review-play.log
```
