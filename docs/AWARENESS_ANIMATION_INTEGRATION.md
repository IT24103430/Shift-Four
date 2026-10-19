# Task 12 — Awareness and progressive movement animation

The active implementation extends the existing `DroneAnimationPose` and `DroneRotorVisuals`. It does not add an animation component, duplicate VFX, replace wreck physics or modify a drone policy. Existing hover, rotor pivots, turning bank, accepted-shot effects, hit feedback and gravity-driven destruction remain in their original owners.

Unity version: **6000.6.4f1**. No FBX, scene, prefab, material, collider, baked navigation or difficulty asset change is required. Pending Scout/Guard/Flanker proposal packages remain unapplied. This task issues no staging, commit or push commands.

## Movement feedback

`DroneRotorVisuals` remains the sole model-local pose writer, running after `DroneBody` accepts physical movement and `DroneMotor` publishes `ActualVelocity`. It uses the existing stable chase-speed normalization, so changing the progressive speed cap does not hide acceleration or braking. Disabled/unprofiled motors retain the established displacement fallback.

The adapter derives forward acceleration from consecutive accepted world velocities, projects it into root-local forward space, then normalizes by the stable animation reference. Invalid samples, first samples, teleports and long frame gaps cannot introduce a new acceleration spike. `SetAcceleration` clamps positive input to 0–1; the existing pose damps the effort at rate 10/s. Full effort adds at most **0.55°** of forward pitch and **0.06** to the normalized rotor-speed target. Acceleration input is presentation only; it never changes NavMesh speed, movement modes, root transforms or routes.

The existing speed-to-rotor response and exact rotor-travel integration remain. Signed deceleration still controls braking and return to level hover. Turning bank math is unchanged. Hover remains at the authored rest position ±0.02 m; the combined tilt, including scanning, keeps the existing 4° safety cap. Local scale and imported rest rotation are preserved.

## Awareness cues

Awareness is a separate visual input, not a replacement for `DroneVisualState`. An idle drone can display Combat awareness without entering Firing, emitting a tracer or gaining attack permission. The adapter reads only the existing enabled awareness state; it never reads a target to drive scanning.

| Awareness | Feedback |
| --- | --- |
| Patrol / ReturnToPost | Alert color fades away; normal hover/rotors remain |
| Suspicious | Restrained yellow/amber breath at 0.35 Hz, target blend 0.06–0.10 |
| Investigate / Search | Warm amber breath, target blend 0.09–0.14; slow 0.3 Hz local yaw scan up to ±1.2° near rest |
| Combat | Distinct red pulse at 2 Hz, target blend 0.18–0.28 |

Color/strength transitions damp at rate 8/s. Scanning fades in and out, reduces as movement grows, and is suppressed by firing/hit feedback. It never turns the AI root, changes sight direction or supplies observations. Normal moving/turning flight retains its bank response.

Original model colors are the base of every blend. Cached material property blocks apply temporary feedback to individual renderer slots; original alpha, shared materials, existing emission settings and model-specific accents remain intact. Returning to patrol restores the original base color, and disabling the component restores the exact original property blocks. No shader keyword, material asset or renderer material instance is created for awareness.

## Priority and transform ownership

Fatal destruction has highest priority. `Shutdown` immediately clears alert strength, scan yaw, acceleration effort and shot/hit pose flashes, then latches them off. The existing visual death latch cancels muzzle/tracer effects and creates the existing wreck rig after health callbacks stop navigation and disable living hitboxes. Rotor deceleration, failure flash, gravity, impact sparks and settled nonblocking wrecks continue through the original code.

Nonfatal hit feedback retains priority over firing pose feedback. Accepted shots continue to use the existing muzzle/tracer rig and accepted-hit segment. Awareness is reduced beneath these effects; it cannot trigger them. `Health`, `DroneBrain`, `DroneWeaponEffects` and `DroneWreckMotion` are byte-for-byte unchanged.

The existing ownership chain remains:

```text
NavMesh / DroneMotor -> navigation root
DroneBody           -> accepted root movement and collision protection
DroneRotorVisuals   -> living model-local hover/tilt/scan and rotor pivots
DroneWreckMotion    -> separate falling rig after death
```

The motor's legacy bob fallback remains inactive when the visual owner is present. During destruction, the visual component stops writing the model pose; physics owns the separate parent wreck rig. No second script writes competing rotations onto the living model Transform.

## Verification and evidence

**450 tests passed: 297 Edit Mode and 153 Play Mode, with no failures or skips.** The calibrated six-state benchmark recorded zero managed allocation events across 24,000 warmed ticks.

See [actual test results, source audit and commands](evidence/awareness-animation/README.md) and the [rendered comparison viewer](evidence/awareness-animation/index.html). Captures show all four existing imported models in Patrol, Suspicious, Search and Combat. They use controlled awareness snapshots through the real awareness interface; they demonstrate presentation, not a new sensing or combat policy.

The new tests cover awareness transitions, pulse distinction, acceleration/rotor effort, braking, bank preservation, priority, fatal latching, enable/disable restoration, teleport suppression, calibrated allocations and 30/60/120 Hz behavior. MainLab tests exercise actual motor movement and mesh vertices inside the existing body envelope. Existing animation, destruction, firing, movement, sensing, difficulty and policy regressions remain enabled.

Reproduce in a disposable project:

```sh
python3 docs/evidence/awareness-animation/reproduce.py
```

Use `--full` for complete suites, `--demo-only` for the four-model cue captures, or `--prepare-only` to open the printed project in Unity. Active scenes/assets are never opened or saved by the verification workflow.

## Practical limits

- Alert visuals show awareness, not proof of a visible player or permission to fire. Existing live visual/range/cooldown checks remain authoritative.
- Rotor acceleration effort is a normalized visual response, not a thrust/flight simulation. Actual motion can change abruptly for collision safety.
- Combined pose stays within the existing 4° cap; simultaneous strong pitch/bank cannot exceed validated clearance.
- Model material/identity colors remain authored; temporary alert blends intentionally shift appearance while active. Existing emissive surfaces retain their shader configuration; no emission keyword is enabled.
- Allocation measurements cover warmed presentation ticks. Binding and the existing once-per-death physics rig allocate separately. Simulation cadence checks are not GPU/display throughput measurements.
