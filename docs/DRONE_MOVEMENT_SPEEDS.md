# Progressive drone movement speeds — Task 7

Implementation is in the active `Shift-Four` checkout on `feature/progressive-drone-intelligence-workspace`, starting from `a4ff4fdebcd3a7c461d416dfcc2c565926b37b05` plus the existing Task 6 working changes. No commit, staging or push is performed.

## Configuration and state mapping

The existing Inspector-configurable `DroneDifficultyProfile` assets remain unchanged. A validated settings copy is bound once in `DroneBrain.Start`. Reapplying difficulty/health data during damage or healing cannot reset movement. Custom brains without a matching profile and standalone motors keep their authored NavMesh settings.

| Drone | Patrol (m/s) | Investigation (m/s) | Chase (m/s) |
| --- | ---: | ---: | ---: |
| Scout | 1.8 | 2.2 | 2.6 |
| Guard | 2.0 | 2.5 | 3.0 |
| Interceptor | 2.5 | 3.1 | 3.7 |
| Flanker | 2.8 | 3.5 | 4.2 |

Tune the four assets under `Assets/Resources/DroneDifficulty/` in the Inspector. Authored prefab NavMeshAgent speed remains 3.5 m/s because prefabs are unchanged; entering Play Mode applies the runtime profile above. Select a live drone's NavMeshAgent to observe the changing speed limit. Profile changes are captured at startup rather than reapplied every frame.

Investigation defaults are the midpoint between patrol and chase; an owner can tune that field independently within the validated interval. All four profiles specify acceleration **8 m/s²** and angular speed **120 degrees/s**. Those settings initialize the existing NavMeshAgent once; subsequent speed ramps use its current acceleration, preserving runtime acceleration changes.

| Awareness | Movement mode |
| --- | --- |
| Patrol, Suspicious, ReturnToPost | Patrol |
| Investigate, Search | Investigate |
| Combat | Chase |

`DroneBrain` selects only the movement mode after its existing awareness update (every 0.3 s). Tactical destinations, scoring, interception predictions, firing and LOS checks remain unchanged. This task does not add sound-investigation destinations to individual policies. Hearing can increase movement urgency but cannot authorize shooting or create visual evidence.

## Runtime interface

- `DroneMovementSpeeds`: pure helper with validated immutable profile data, `FromAwareness`, `SetMode`, `Target`, `Current`, `AnimationReference` and explicit-time `Step(dt, acceleration)`.
- `DroneMotor.InitializeMovement(settings)`: returns true only on the first initialization; rejects reinitialization and terminal shutdown. Starts at patrol speed before navigation begins.
- `DroneMotor.SetMovementMode(mode)`: target selection only. Each motor Update moves the speed limit toward that target by at most `agent.acceleration * Time.deltaTime`.
- `HasMovementProfile`, `MovementMode`, `TargetSpeed`: inspect configuration/mode intent.
- `CruiseSpeed`: current smoothed `NavMeshAgent.speed` limit, zero after terminal shutdown.
- `ActualVelocity` / `ActualSpeed`: accepted horizontal root displacement in world m/s, sampled after `DroneBody` collision correction. Initialization/re-enable and existing teleport rejection suppress artificial motion spikes.
- `AnimationReferenceSpeed`: stable chase-speed normalization for profiled actors, so reducing a cap still produces braking cues.
- `DroneRotorVisuals.MovementVelocity` / `MovementSpeed`: the accepted displacement consumed by the animation. Legacy/disabled-motor and falling-wreck presentation retain their existing displacement fallback.

Motor execution order is 150, between body clearance (100) and presentation (200). The new code never writes root position, agent velocity, destination or path while changing a speed. NavMeshAgent retains native acceleration, autoBraking, obstacle avoidance and path following. Existing motor angular limits, intermediate/final stopping rules, route retry limits, graph-version invalidation, standoff caching and body-envelope sweeps remain authoritative.

Interceptor already supplies `NavMeshAgent.speed` to `InterceptorPolicy.StepWithRoutes`; it now automatically receives the current smoothed profile limit instead of the old constant authored 3.5 m/s. `ActualSpeed` is available separately. ETA continues to use travel capacity rather than instantaneous zero velocity at rest/arrival; changing that ETA model would alter tactical prediction and is outside this task. It remains an approximate cruise-based estimate, without acceleration/braking integration.

Combat proximity still uses the existing nonterminal hold/standoff code. `Stop()` remains terminal, clears measured velocity synchronously and disables the motor. No mode request, difficulty reapplication or goal request can restart it.

## Exact source changes

Modified production:

- `Assets/Scripts/AI/Core/DroneBrain.cs`: one-time movement binding and awareness-to-movement adapter; preserves previous Task 6 additions.
- `Assets/Scripts/AI/Movement/DroneMotor.cs`: bounded speed-cap integration, accepted-motion interface and sampling order.
- `Assets/Scripts/Gameplay/DroneRotorVisuals.cs`: consume/expose accepted velocity and use stable animation normalization.

New production:

- `Assets/Scripts/AI/Movement/DroneMovementSpeeds.cs` and `.meta`.

Tests:

- New `Assets/Tests/EditMode/Movement/DroneMovementSpeedsEditModeTests.cs` and `.meta`: four default profiles, all six awareness states, bounded and frame-independent ramps, invalid values and immutable validation.
- New `Assets/Tests/PlayMode/DroneMovementSpeedsPlayModeTests.cs` and `.meta`: real NavMesh movement for all four profiles/modes, no destination/A* churn, measured acceleration/deceleration, animation braking, 30/60/120 Hz turning/arrival, raised walls/player holds, gate invalidation/recovery and terminal stop.
- Modified `Assets/Tests/PlayMode/DroneAnimationPlayModeTests.cs` and `MainLabProximityPlayModeTests.cs`: control fixture simulation at 60 Hz and restore previous frame/capture settings, keeping transient feedback and per-frame travel checks independent of editor/render stalls.
- Modified `Assets/Tests/PlayMode/ControlTacticalNavigationPlayModeTests.cs`: the controlled visible-sighting fixture explicitly ramps the real motor into chase before calling the tactical adapter directly.
- Modified `Assets/Tests/PlayMode/InterceptorMainLabPlayModeTests.cs`: initialize chase for direct confirmed-sighting fixtures and use bounded polling for actual staging clearance as slower patrol actors leave the area. All original collision/LOS/prediction assertions remain enabled.
- Modified `Assets/Tests/PlayMode/DroneDifficultyPlayModeTests.cs`: replace the obsolete Task 2 assertion that movement must stay at 3.5 m/s with the now-bound profile interval/current motor speed. Health, vision, weapons, braking, acceleration and turn-rate assertions are retained.

No individual policy, DroneBody, A*, gate implementation, Health, player/console code, difficulty asset, scene, prefab, NavMesh, model or material is edited.

## Verification

Final actual results and evidence are recorded here after Unity completes. Tests run in the existing matching `Shift-Four-Progressive` verification worktree because the active project remains open in Unity. Only Task 7 source delta is mirrored there; the active checkout contains all implementation changes.

The first focused run is retained as `FocusedPlayMode.initial.xml`/`.log`: 4/8 passed. New-test assumptions incorrectly required arrival near the *edge* of the stopping radius and zero motion during valid static-wall sidestepping. Corrected tests enforce arrival within the stopping radius, angular/speed limits, no wall/player penetration and matching accepted-motion data. Production code did not change to satisfy those assumptions.

The first full run is retained as `PlayMode.initial.xml`/`.log`: **130/136 passed**. The six failures exposed legacy fixture setup assumptions: direct `Decide` calls bypass awareness and left the drone at patrol speed, and a fixed gate-opening wait ended while the slower Scout still occupied the staging area. Fixtures now explicitly ramp the real chase mode for confirmed-sighting scenarios and poll for genuinely clear staging for at most six seconds. Tactical production code, scene geometry and the existing prediction/route/LOS assertions are unchanged. Diagnostic rerun evidence is retained as `InterceptorFixtures.initial.xml`/`.log`; its remaining five staging failures include actor positions and rejection counts.

The second full run is retained as `PlayMode.second.xml`/`.log`: **134/136 passed**, including the corrected prediction fixtures. A short damage-flash assertion observed two delayed frames beyond its lifetime; a proximity check compared movement from one frame with a different frame's delta. Those two fixture classes now use a controlled 60 Hz simulation and restore previous timing settings. Existing damage, rotor, material, speed, collision and death assertions are retained. Focused rerun evidence is `TimingFixtures.xml`/`.log`. Production code remains unchanged from the initial speed implementation.

Accepted-displacement sampling retains the existing 20 m/s teleport/restart spike cutoff; the requested 1.8–4.2 m/s defaults are well below it.

The 30/60/120 Hz fixtures control simulation delta through `Time.captureDeltaTime`; these are deterministic simulation cadence checks, not claims about GPU throughput or achieved display frame rate. Collision holds and validated detours may abruptly reduce velocity for safety. Acceleration bounds are tested on unobstructed motion; safety correction remains higher priority.
