# First-person movement animation

`FirstPersonMovementMotion` is saved on `Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab`. It observes the real player's root displacement and `CharacterController.isGrounded` after movement. It does not read movement keys, call `Move`, request a jump, write a player/camera transform or change collision components. Body-relative velocity keeps forward/backward and strafe responses consistent while looking up/down.

`PistolPresentation` samples the component once in `LateUpdate` and composes its offsets on the existing recoil pivot. The whole pistol and both arms move together. The existing grip pose/solver, magazine/reload path and socket-level wall retreat remain intact. Each pose starts from the cached rest transform, so layers do not accumulate offsets or compete to write the pivot. No Animator or root motion is used.

## Saved defaults

Translation values use **metres**; rotation values use **degrees**. Vector rotations are pitch, yaw, roll.

| Setting | Default | Purpose |
|---|---:|---|
| Reference Speed | 5 m/s | Normalizes pose amplitude; does not set player speed |
| Stationary Speed | 0.08 m/s | Ignores tiny horizontal contact corrections |
| Stride Length | 4 m | Distance per complete left/right sway cycle; vertical bob repeats twice per cycle |
| Walk Position Amplitude | (0.0011, 0.0015, 0.0006) m | ±1.1 mm lateral, ±1.5 mm vertical, ±0.6 mm depth |
| Walk Rotation Amplitude | (0.14, 0.06, 0.18)° | Restrained walking pitch/yaw/roll |
| Blend Seconds | 0.12 s | Walking, directional sway and activity envelope time constant |
| Strafe Lag | 0.002 m | Opposes lateral travel, up to 2 mm |
| Forward Lean | 0.18° | Opposite signs for forward/backward travel |
| Strafe Roll | 0.24° | Opposes lateral travel |
| Airborne Position | (0, −0.003, −0.002) m | Stable hold: 3 mm down, 2 mm back |
| Airborne Rotation | (0.5, 0, −0.15)° | Small airborne pitch/roll |
| Airborne Blend Seconds | 0.10 s | Ground/air transition time constant |
| Takeoff Dip / Pitch | 0.002 m / 0.22° | Brief launch response |
| Takeoff Peak Seconds | 0.06 s | Time to peak of the smooth launch pulse |
| Landing Dip / Pitch | 0.004 m / 0.4° | Maximum impact response, scaled by observed downward speed |
| Landing Peak Seconds | 0.045 s | Time to peak of the smooth impact pulse |
| Landing Reference Speed | 7 m/s | Downward speed giving full impact amplitude |
| Activity Strength | 0.35 | Movement amplitude during recoil/reload, with the same smooth envelope |

Walking phase advances only with actual grounded horizontal travel. It freezes immediately when the player stops or is blocked; the held offset then fades to neutral rather than continuing to oscillate. Airborne movement freezes walking phase and fades walking out. The air hold is independent of flight duration, vertical velocity oscillation and airborne strafe input. Idle breathing blends down to 25% at full walk, 15% at full air hold, then returns as movement settles. Its phase keeps running.

Takeoff is a grounded-to-air transition with positive observed vertical velocity, not a request to jump. Walking off an edge therefore produces no false takeoff impulse. Landings use the preceding maximum downward velocity, are capped at full strength, and require at least 0.08 seconds of flight to avoid impact pulses from brief ground-contact chatter. Both impulses use `t × exp(1 − t)`, with time normalized to their respective peak durations; they return smoothly to zero. Normal landing impact is mostly recovered within about 0.3 seconds.

The sampler uses elapsed time, distance, analytic exponential blends and absolute pose reconstruction. Paused/invalid delta time holds state. Disable/re-enable clears motion. Movement above 25 m/s is treated as a teleport/respawn discontinuity and clears motion instead of generating a huge bob/landing response. This threshold does not constrain or write player velocity. Before the first controller `Move`, grounded state can be false even on a stationary spawn; the observer waits for contact or actual vertical motion before establishing the pose state.

## Setup and verification

Edit the component on the pistol prefab to preserve artist settings across restarts and respawns. **Tools → Shift Four → FPS → Add Pistol Movement Settings Once** (`FirstPersonMovementAuthoring.Install`) adds defaults only if the component is missing; it preserves existing tuning. Runtime presentation also supplies defaults for a compatible older pistol prefab.

`FirstPersonMovementEditModeTests` covers four travel directions, stopping, ground chatter, edge drop, takeoff/hold/landing, invalid/paused samples, absolute transform ownership and matching 30/60/120-FPS samples. `FirstPersonMovementPlayModeTests` drives the existing private movement/jump methods on the real MainLab player in test-only fixtures. It checks actual displacement/ground contact, original jump arc/speed, wall blocking and retreat, fixed camera/capsule, hand contacts, local skin stability, layer composition, reload/fire, teleport/reset, initial ground state and actual Game-view captures where available.

Focused regression results, CSV traces and unedited Game-view readbacks are recorded under `Logs/ArmsMovement/`. The underlying [grip's visual limits](GRIP.md) remain: this movement layer preserves its finger pose and skin, including existing wrap gaps and reinforcement creasing.
